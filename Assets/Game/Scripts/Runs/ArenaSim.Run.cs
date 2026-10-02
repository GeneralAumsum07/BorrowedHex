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
    /// Short-mode flow (owner-revised, D50): encounter 1 → upgrade choice → encounter 2 →
    /// upgrade choice → encounter 3 → upgrade choice → boss intro → boss. Each encounter is a
    /// fixed, seeded list of formations and ends when every member has been KILLED. One shared
    /// life budget of 300 seconds (D59) covers all of it: time and hits spend it, kills restore
    /// it up to the starting cap. Pauses cost nothing; elapsed gameplay time never rewinds.
    ///
    /// Terminal ordering (section 6 / Phase 5 check), resolved once at the END of each tick:
    ///   1. player dead            → Death        (wins over a boss killed on the same tick)
    ///   2. boss killed            → Victory
    ///   3. life drained by time    → TimeExpired (in any phase, encounters included)
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
        /// <summary>Upgrade transitions reached so far (one per cleared encounter).</summary>
        public int TransitionsReached { get; private set; }

        public RunScore Score { get; private set; }
        /// <summary>Frozen once when the run ends; null while it is running.</summary>
        public RunSummary Summary { get; private set; }
        /// <summary>The boss of this run (stays set after it dies, for the results screen).</summary>
        public EnemyActor Boss { get; private set; }
        // Kept separate from elapsed gameplay time: kills can buy life, never rewind AI timers.
        double lifeSeconds;
        public float LifeSeconds => (float)lifeSeconds;

        // Tolerance for schedule boundaries: 60 Hz steps accumulate double rounding, and a
        // transition must fire ON the 2400th tick, not one tick late.
        const double Eps = 1e-6;

        void InitRun()
        {
            lifeSeconds = Stats.StartingSeconds;
            Score = new RunScore(this);
            Events.EnemyKilled += RewardKillTime;
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
            if (State == RunState.Results) return;
            if (!IsShortRun)
            {
                if (lifeSeconds <= Eps && Player.Alive)
                {
                    Player.Alive = false;
                    Player.Dashing = false;
                    Events.RaisePlayerDied();
                }
                return;
            }

            if (!Player.Alive) { EndRun(RunEndReason.Death); return; }
            if (Boss != null && Boss.Killed) { EndRun(RunEndReason.Victory); return; }

            var sm = Config.shortMode;
            if (lifeSeconds <= Eps) { lifeSeconds = 0; EndRun(RunEndReason.TimeExpired); return; }

            TickEncounterDirector(now);

            if (State == RunState.Combat && EncounterCleared())
            {
                // Section 6: the choice pauses gameplay; packets are untouched and every timer
                // (packet expiry, combo) is frozen with the clock.
                TransitionsReached++;
                Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
                SetState(RunState.UpgradeChoice);
            }
        }

        /// <summary>Every formation of this encounter has arrived and every member is dead.</summary>
        bool EncounterCleared() =>
            encounterPlan.Count > 0 && planIndex >= encounterPlan.Count && spawnQueue.Count == 0 && AliveOrdinaryCount() == 0;

        /// <summary>
        /// Ordinary enemies still to kill in this encounter: alive, waiting under the cap, and
        /// in formations not yet arrived. Known exactly because the plan is drawn up front.
        /// </summary>
        public int EnemiesLeftInEncounter()
        {
            if (State == RunState.BossIntro || State == RunState.BossCombat || Encounter >= Config.shortMode.encounterCount) return 0;
            int n = AliveOrdinaryCount() + spawnQueue.Count;
            for (int i = planIndex; i < encounterPlan.Count; i++) n += encounterPlan[i].Members.Length;
            return n;
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
            RestorePillars();
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
            if (reason == RunEndReason.Victory) Score.AddVictoryBonus(lifeSeconds);
            Summary = RunSummary.Freeze(this, reason, now);
            // Nothing scheduled may fire into the results screen (section 3).
            Scheduler.CancelAll();
            CancelCapture();
            ClearProjectiles();
            Player.Alive = false;
            Player.Dashing = false;
            Clock.SetPauseReason(PauseReason.Results, true);
            SetState(RunState.Results);
            Events.RaiseRunEnded(Summary);
        }

        /// <summary>Active seconds left on the shared run clock.</summary>
        public float SecondsLeftInRun() => LifeSeconds;

        void TickLifeClock(float dt) => lifeSeconds = System.Math.Max(0, lifeSeconds - dt);

        void RewardKillTime(EnemyActor enemy, Combat.DamageEvent damage)
        {
            // A kill cannot revive a clock already depleted by this tick's hit or ticking.
            if (Summary != null || !Player.Alive || lifeSeconds <= 0 || enemy.IsBoss) return;
            float reward = Config.combat.For(enemy.Category).killSeconds * (enemy.Elite ? 1.5f : 1f);
            double before = lifeSeconds;
            lifeSeconds = System.Math.Min(Stats.StartingSeconds, lifeSeconds + reward);
            float gained = (float)(lifeSeconds - before);
            Score.RecordTimeGained(gained);
            if (gained > 0) Events.RaiseLifeClockChanged(gained, enemy.Position);
        }

        // ---- Encounter director -----------------------------------------------------------

        readonly Queue<ActorCategory> spawnQueue = new Queue<ActorCategory>();
        double nextFormationAt;
        int formationsThisEncounter = -1;
        int formationEncounter = -1;
        Formation lastFormation;
        // The encounter's formations, drawn when it starts (so "enemies left" is exact and a
        // seed fixes the whole encounter), and how many of them have been queued.
        readonly List<Formation> encounterPlan = new List<Formation>();
        int planIndex;

        /// <summary>Members waiting for room under the cap (section 6: queued spawns wait).</summary>
        public int QueuedSpawns => spawnQueue.Count;

        /// <summary>
        /// Short-mode spawns. The encounter's planned formations arrive one at a time: the next
        /// is due every <c>spawnInterval</c> seconds, or after a short breather when the arena
        /// is empty (so fast killing is rewarded with a faster encounter). Members enter while
        /// fewer than 12 ordinary enemies are alive; the rest wait in the queue, and while
        /// anything is waiting no NEW formation is queued. Once the plan is used up nothing
        /// more spawns: the encounter ends when the last member dies.
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
                encounterPlan.Clear();
                planIndex = 0;
                int count = sm.FormationsIn(Encounter);
                for (int k = 0; k < count; k++) encounterPlan.Add(NextFormation());
            }

            if (spawnQueue.Count == 0 && planIndex < encounterPlan.Count)
            {
                bool empty = AliveOrdinaryCount() == 0;
                if (empty && nextFormationAt - now > sm.emptyArenaBreather) nextFormationAt = now + sm.emptyArenaBreather;
                if (now >= nextFormationAt - Eps)
                {
                    var f = encounterPlan[planIndex++];
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
