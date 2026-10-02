using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Enemies;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Phase 5 run flow: explicit states, the short-mode schedule, the encounter director and
    /// terminal resolution. Lives in the sim, not in a MonoBehaviour, so the whole beginning-
    /// to-end run is testable headless and a restart (new sim) resets all of it at once.
    ///
    /// Short-mode timeline, all in ACTIVE gameplay seconds (the clock does not move while
    /// paused, so "40 s" means 40 s of play however long a menu stayed open):
    ///   0–40 encounter 1 → upgrade choice → 40–80 encounter 2 → upgrade choice →
    ///   80–120 encounter 3 → upgrade choice → boss intro → 120–180 boss window.
    ///
    /// Terminal ordering (section 6 / Phase 5 check), resolved once at the END of each tick:
    ///   1. player dead            → Death        (wins over a boss killed on the same tick)
    ///   2. boss killed            → Victory
    ///   3. 180 s with boss alive  → TimeExpired
    /// Checking at the end of the tick, rather than inside the damage calls, is what makes the
    /// ordering independent of which of the two hits happened to resolve first in the tick.
    ///
    /// Sandbox runs (dev panel, tests) keep the old open-ended behaviour: no schedule, no
    /// terminal state. Only a real short run ends.
    /// </summary>
    public sealed partial class ArenaSim
    {
        public RunState State { get; private set; } = RunState.Ready;
        RunState stateBeforePause = RunState.Ready;

        /// <summary>A scored short run (not the sandbox): the only kind with a schedule and an end.</summary>
        public bool IsShortRun => !Setup.Sandbox && Setup.Mode == GameMode.Short;

        /// <summary>0-based encounter in progress; equals encounterCount once the boss phase starts.</summary>
        public int Encounter { get; private set; }
        /// <summary>Upgrade transitions reached so far (40/80/120 s).</summary>
        public int TransitionsReached { get; private set; }

        public RunScore Score { get; private set; }
        /// <summary>Frozen once when the run ends; null while it is running.</summary>
        public RunSummary Summary { get; private set; }
        /// <summary>The boss of this run (stays set after it dies, for the results screen).</summary>
        public EnemyActor Boss { get; private set; }

        // Tolerance for schedule boundaries: 60 Hz steps accumulate double rounding, and a
        // transition must fire ON the 2400th tick, not one tick late.
        const double Eps = 1e-6;

        void InitRun()
        {
            Score = new RunScore(this);
            nextFormationAt = Config.shortMode.firstSpawnDelay;
        }

        void SetState(RunState s)
        {
            if (State == s) return;
            State = s;
            Events.RaiseRunStateChanged(s);
        }

        /// <summary>
        /// Player-facing pauses (menu, focus loss, manual). Separate from the flow's own pauses
        /// (UpgradeChoice, BossIntro, Results), which the flow sets itself. State shows Paused
        /// while any of these is held and returns to exactly the state it came from, so pausing
        /// over an upgrade choice resumes onto the upgrade choice, not into combat.
        /// </summary>
        public void SetPause(PauseReason reason, bool on)
        {
            Clock.SetPauseReason(reason, on);
            bool held = Clock.HasPauseReason(PauseReason.Menu) || Clock.HasPauseReason(PauseReason.FocusLost)
                        || Clock.HasPauseReason(PauseReason.Manual);
            if (held && State != RunState.Paused && State != RunState.Results)
            {
                stateBeforePause = State;
                SetState(RunState.Paused);
            }
            else if (!held && State == RunState.Paused)
            {
                SetState(stateBeforePause);
            }
        }

        /// <summary>Called by Tick before the clock moves: the first unpaused tick starts combat.</summary>
        void BeginIfReady()
        {
            if (State == RunState.Ready) SetState(RunState.Combat);
        }

        /// <summary>Step 8 of the tick: terminal resolution, then the schedule.</summary>
        void TickRunFlow(double now)
        {
            Score.Tick(now);
            if (!IsShortRun || State == RunState.Results) return;

            if (!Player.Alive) { EndRun(RunEndReason.Death); return; }
            if (Boss != null && Boss.Killed) { EndRun(RunEndReason.Victory); return; }

            var sm = Config.shortMode;
            if (now >= sm.TotalLength - Eps) { EndRun(RunEndReason.TimeExpired); return; }

            if (TransitionsReached < sm.encounterCount && now >= (TransitionsReached + 1) * sm.encounterLength - Eps)
            {
                // Section 6: the choice pauses gameplay; packets and enemies are untouched and
                // every timer (packet expiry, telegraphs, combo) is frozen with the clock.
                TransitionsReached++;
                Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
                SetState(RunState.UpgradeChoice);
                return;
            }

            TickEncounterDirector(now);
        }

        /// <summary>
        /// Leave the upgrade choice. Phase 5 has no upgrades yet (Phase 6), so this is the
        /// Continue button. After the third choice the boss intro starts instead of combat.
        /// </summary>
        public bool ContinueFromUpgrade()
        {
            if (State != RunState.UpgradeChoice) return false;
            Clock.SetPauseReason(PauseReason.UpgradeChoice, false);
            Encounter++;
            if (TransitionsReached >= Config.shortMode.encounterCount) BeginBossIntro();
            else SetState(RunState.Combat);
            return true;
        }

        /// <summary>
        /// Section 6 boss transition: ordinary spawns stop (the director only runs in Combat),
        /// ordinary enemies and their unclaimed shots are despawned WITHOUT score (DespawnEnemy
        /// raises no kill), captured packets and the player's own shots are kept. The boss
        /// appears now — so the intro banner shows it on screen — but the clock is held by
        /// BossIntro, and its spawn warning only starts counting once the intro ends.
        /// </summary>
        void BeginBossIntro()
        {
            ClearArena();
            spawnQueue.Clear();
            Boss = SpawnBoss();
            Clock.SetPauseReason(PauseReason.BossIntro, true);
            SetState(RunState.BossIntro);
        }

        /// <summary>The intro banner is done: the boss window starts.</summary>
        public bool CompleteBossIntro()
        {
            if (State != RunState.BossIntro) return false;
            Clock.SetPauseReason(PauseReason.BossIntro, false);
            SetState(RunState.BossCombat);
            return true;
        }

        /// <summary>
        /// End the run once. The summary is frozen here and never recomputed, so anything
        /// that happens after (a stray event, a UI poll) cannot change the result shown.
        /// </summary>
        void EndRun(RunEndReason reason)
        {
            if (Summary != null) return;
            double now = Clock.Now;
            if (reason == RunEndReason.Victory) Score.AddVictoryBonus(Config.shortMode.TotalLength - now);
            Summary = RunSummary.Freeze(this, reason, now);
            // Nothing scheduled may fire into the results screen (section 3).
            Scheduler.CancelAll();
            CancelCapture();
            Clock.SetPauseReason(PauseReason.Results, true);
            SetState(RunState.Results);
            Events.RaiseRunEnded(Summary);
        }

        /// <summary>Active seconds left in the current encounter or the boss window.</summary>
        public float SecondsLeftInPhase()
        {
            var sm = Config.shortMode;
            double end = Encounter < sm.encounterCount ? (Encounter + 1) * sm.encounterLength : sm.TotalLength;
            return (float)System.Math.Max(0.0, end - Clock.Now);
        }

        // ---- Encounter director -----------------------------------------------------------

        readonly Queue<ActorCategory> spawnQueue = new Queue<ActorCategory>();
        double nextFormationAt;
        int formationsThisEncounter = -1;
        int formationEncounter = -1;
        Formation lastFormation;

        /// <summary>Members waiting for room under the cap (section 6: queued spawns wait).</summary>
        public int QueuedSpawns => spawnQueue.Count;

        /// <summary>
        /// Short-mode spawns. A formation is due every <c>spawnInterval</c> seconds, or after a
        /// short breather when the arena is empty. Its members enter the arena while fewer than
        /// 12 ordinary enemies are alive; the rest wait in the queue, and while anything is
        /// waiting no NEW formation is queued — so a cap hit delays pressure instead of
        /// stockpiling it into a burst the moment a slot frees.
        /// </summary>
        void TickEncounterDirector(double now)
        {
            if (State != RunState.Combat) return;
            var sm = Config.shortMode;

            if (formationEncounter != Encounter)
            {
                // New encounter: its signature formation leads, straight away.
                formationEncounter = Encounter;
                formationsThisEncounter = 0;
                if (Encounter > 0) nextFormationAt = now;
            }

            if (spawnQueue.Count == 0)
            {
                bool empty = AliveOrdinaryCount() == 0;
                if (empty && nextFormationAt - now > sm.emptyArenaBreather) nextFormationAt = now + sm.emptyArenaBreather;
                if (now >= nextFormationAt - Eps)
                {
                    var f = NextFormation();
                    foreach (var m in f.Members) spawnQueue.Enqueue(m);
                    int i = UnityEngine.Mathf.Clamp(Encounter, 0, sm.spawnInterval.Length - 1);
                    nextFormationAt = now + sm.spawnInterval[i];
                }
            }

            while (spawnQueue.Count > 0 && AliveOrdinaryCount() < sm.maxOrdinaryEnemies)
            {
                var c = spawnQueue.Dequeue();
                SpawnEnemy(c, FindSpawnPoint(Config.combat.For(c).bodyRadius));
            }
        }

        /// <summary>
        /// The first formation of an encounter is its signature (so the roster change is seen
        /// at once); later ones are a seeded pick from the encounter's pool that never repeats
        /// the previous formation back to back.
        /// </summary>
        Formation NextFormation()
        {
            int enc = UnityEngine.Mathf.Min(Encounter, EnemySpawnService.EncounterPools.Length - 1);
            var pool = EnemySpawnService.EncounterPools[enc];
            Formation f;
            if (formationsThisEncounter == 0) f = pool[0];
            else
            {
                f = pool[Random.NextInt(0, pool.Length)];
                if (f == lastFormation && pool.Length > 1) f = pool[(System.Array.IndexOf(pool, f) + 1) % pool.Length];
            }
            formationsThisEncounter++;
            lastFormation = f;
            return f;
        }
    }
}
