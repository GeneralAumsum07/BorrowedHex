using BorrowedHex.Core;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Phase 12: the endless scheduler. Same combat, enemies, boss, score and life clock as a
    /// short run; only the schedule differs (section 6 "Endless mode", D84):
    ///
    ///   wave 1 → wave 2 → choice → wave 3 → wave 4 → choice → wave 5 → wave 6
    ///     → arena cleared without rewards → boss → +30 s → (the deferred wave-6 choice) → next cycle
    ///
    /// A wave is 30 ACTIVE seconds of the Combat state, counted by its own countdown rather
    /// than by Clock.Now. That is what keeps boss time out of the wave schedule: the gameplay
    /// clock keeps running through the boss fight (AI timers need it), the wave countdown does
    /// not move outside Combat. Pauses stop both, because a paused sim does not tick at all.
    ///
    /// Waves end on time, not on a clear: enemies still alive when a choice opens stay frozen
    /// with the clock and are still there after it. Only the boss transition clears the arena.
    /// </summary>
    public sealed partial class ArenaSim
    {
        /// <summary>A scored endless run (not the sandbox).</summary>
        public bool IsEndlessRun => !Setup.Sandbox && Setup.Mode == GameMode.Endless;

        /// <summary>1-based cycle in progress. A cycle is six waves and a boss.</summary>
        public int Cycle { get; private set; } = 1;
        /// <summary>1-based wave in the current cycle. Stays at the last wave through the boss.</summary>
        public int Wave { get; private set; } = 1;
        /// <summary>Waves whose 30 s ran out (XP counts these, section 7).</summary>
        public int WavesCompleted { get; private set; }
        public int CyclesCompleted => Cycle - 1;
        double waveSecondsLeft;
        public float WaveSecondsLeft => (float)waveSecondsLeft;

        void InitEndless()
        {
            waveSecondsLeft = Config.endless.waveLength;
        }

        /// <summary>Called from TickCombat with the tick's dt: only Combat moves the wave countdown.</summary>
        void TickWaveClock(float dt)
        {
            if (IsEndlessRun && State == RunState.Combat) waveSecondsLeft = System.Math.Max(0, waveSecondsLeft - dt);
        }

        /// <summary>Step 8 for endless runs: terminals, the boss hand-off, then the wave director.</summary>
        void TickEndlessFlow(double now)
        {
            // Terminals: Death first, as in short mode. A boss kill is not terminal here, so
            // there is no Victory to order against; time running out is checked BEFORE the boss
            // kill so a kill can never revive a clock this tick already emptied (the same rule
            // RewardKillTime applies to ordinary kills).
            if (!Player.Alive) { EndRun(RunEndReason.Death); return; }
            if (lifeSeconds <= Eps) { lifeSeconds = 0; EndRun(RunEndReason.TimeExpired); return; }

            if (State == RunState.BossCombat && Boss != null && Boss.Killed) { OnEndlessBossKilled(); return; }
            if (State != RunState.Combat) return;

            TickWaveDirector(now);
            if (waveSecondsLeft <= Eps) CompleteWave();
        }

        void CompleteWave()
        {
            var et = Config.endless;
            WavesCompleted++;
            if (Wave >= et.wavesPerCycle)
            {
                // Section 6: regular spawns stop, ordinary enemies and their shots go without
                // rewards, the boss appears. The wave-six choice waits for the boss to fall.
                BeginBossIntro(CyclesCompleted);
                return;
            }
            if (Wave % et.choiceEveryWaves == 0) { OpenEndlessChoice(); return; }
            Wave++;
            StartWave();
        }

        void StartWave()
        {
            waveSecondsLeft = Config.endless.waveLength;
            // The roster rises in pairs of waves: waves 1-2 use the first encounter's pool,
            // 3-4 the second, 5-6 the third. Encounter doubles as the pool index so the shared
            // formation picker and spawn intervals work unchanged.
            Encounter = Mathf.Min((Wave - 1) / 2, EnemySpawnService.EncounterPools.Length - 1);
        }

        /// <summary>
        /// Opens a choice whose offers have the rank of the cycle they will be USED in. After the
        /// boss that is already the next cycle (Cycle was advanced first), so the deferred choice
        /// offers the stronger rank its upgrade will actually fight with.
        /// </summary>
        void OpenEndlessChoice()
        {
            TransitionsReached++;
            OpenUpgradeChoice(Mathf.Min(Cycle, Config.endless.maxOfferRank));
            Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
            SetState(RunState.UpgradeChoice);
        }

        void OnEndlessBossKilled()
        {
            var et = Config.endless;
            // +30 s capped at the starting clock, reported like a kill's reward so the HUD's
            // gain pop and the run's "time gained" both include it.
            double before = lifeSeconds;
            lifeSeconds = System.Math.Min(Stats.StartingSeconds, lifeSeconds + et.bossKillSeconds);
            float gained = (float)(lifeSeconds - before);
            Score.RecordTimeGained(gained);
            if (gained > 0) Events.RaiseLifeClockChanged(gained, Boss.Position);
            // Forget the dead boss so this branch cannot fire twice; it stays in the score.
            Boss = null;
            Cycle++;
            OpenEndlessChoice();
        }

        /// <summary>Endless leg of ContinueFromUpgrade: the next wave (or the next cycle's first).</summary>
        void ContinueEndless()
        {
            Wave = Wave >= Config.endless.wavesPerCycle ? 1 : Wave + 1;
            StartWave();
            SelectWorldArena(Mathf.Clamp((Wave - 1) / 2, 0, 2));
            RestorePillars();
            SetState(RunState.Combat);
        }

        /// <summary>
        /// Section 6: retiring is allowed between waves, i.e. while an endless choice is open.
        /// The run ends as Retired and finalizes normally, so progression earned is kept.
        /// </summary>
        public bool RetireRun()
        {
            if (!IsEndlessRun || State != RunState.UpgradeChoice) return false;
            Clock.SetPauseReason(PauseReason.UpgradeChoice, false);
            Offers.Clear();
            EndRun(RunEndReason.Retired);
            return true;
        }

        /// <summary>
        /// Dev tool: end the current wave now. Using it marks the run as debug-assisted, so it
        /// can never submit rewards or records (RunSetup.Debug, checked at finalization).
        /// </summary>
        public bool DebugSkipWave()
        {
            if (!IsEndlessRun || State != RunState.Combat) return false;
            Setup.Debug = true;
            waveSecondsLeft = 0;
            return true;
        }

        // ---- Wave director --------------------------------------------------------------

        int wavePool = -1;

        /// <summary>
        /// Endless spawns: formations from the wave's pool arrive back to back for the whole
        /// wave, every <c>spawnInterval</c> seconds or after the short breather when the arena
        /// is empty. There is no fixed plan as in short mode: a wave ends on time, so the
        /// director just keeps the pressure on. Members wait in the queue above the cap of 18.
        /// </summary>
        void TickWaveDirector(double now)
        {
            var sm = Config.shortMode;
            if (wavePool != Encounter)
            {
                // A new pool leads with its signature formation, straight away (after the very
                // first wave's short delay, which InitRun already set).
                wavePool = Encounter;
                formationsThisEncounter = 0;
                if (WavesCompleted > 0) nextFormationAt = now;
            }

            if (spawnQueue.Count == 0)
            {
                bool empty = AliveOrdinaryCount() == 0;
                if (empty && nextFormationAt - now > sm.emptyArenaBreather) nextFormationAt = now + sm.emptyArenaBreather;
                if (now >= nextFormationAt - Eps)
                {
                    foreach (var m in NextFormation().Members) spawnQueue.Enqueue(m);
                    int i = Mathf.Clamp(Encounter, 0, sm.spawnInterval.Length - 1);
                    nextFormationAt = now + sm.spawnInterval[i];
                }
            }

            while (spawnQueue.Count > 0 && AliveOrdinaryCount() < Config.endless.maxOrdinaryEnemies)
            {
                var c = spawnQueue.Dequeue();
                ApplyCycleScaling(SpawnEnemy(c, FindSpawnPoint(Config.combat.For(c).bodyRadius)));
            }
        }

        // ---- Scaling --------------------------------------------------------------------

        /// <summary>Health multiplier for ordinary enemies spawned now.</summary>
        public float EnemyHealthScale => 1f + Config.endless.healthPerCycle * EndlessCycles;
        public float EnemyMoveScale => Mathf.Min(Config.endless.moveMax, 1f + Config.endless.movePerCycle * EndlessCycles);
        public float EnemyCooldownScale => Mathf.Max(Config.endless.cooldownMin, 1f - Config.endless.cooldownPerCycle * EndlessCycles);
        public float BossHealthScale => 1f + Config.endless.bossHealthPerCycle * EndlessCycles;

        /// <summary>Completed cycles that count for scaling: zero outside endless.</summary>
        int EndlessCycles => IsEndlessRun ? CyclesCompleted : 0;

        /// <summary>
        /// Seconds an ordinary enemy may live before it overstays. Shortened per cycle in endless
        /// (section 6), the plain combat value everywhere else. Read by the sim AND the view's
        /// warning ring, so the warning can never disagree with the evolution.
        /// </summary>
        /// The tutorial returns infinity (D87): an enemy must never evolve on its own while a
        /// new player is still learning to catch; its last lesson evolves enemies explicitly.
        public float OverstaySeconds => Setup.Tutorial ? float.PositiveInfinity
            : IsEndlessRun
            ? Mathf.Max(Config.endless.overstayMinSeconds, Config.combat.overstaySeconds - Config.endless.overstayStepPerCycle * CyclesCompleted)
            : Config.combat.overstaySeconds;

        /// <summary>
        /// Applied once at spawn, so an enemy keeps the scaling of the cycle it was born in.
        /// Overstaying later multiplies on top of these (see TickEnemies). Section 6: in endless,
        /// elites come only from overstaying, so nothing here sets Elite.
        /// </summary>
        void ApplyCycleScaling(EnemyActor e)
        {
            if (EndlessCycles == 0) return;
            e.MaxHealth = e.Health = e.MaxHealth * EnemyHealthScale;
            e.MoveScale = EnemyMoveScale;
            e.CooldownScale = EnemyCooldownScale;
        }

        // ---- Projectile budget ----------------------------------------------------------

        /// <summary>
        /// Section 6: a full projectile budget DELAYS an emission; live shots are never dropped
        /// or recycled. Emitters ask before firing and, when refused, hold their (visible) wind-up
        /// and try again next tick. Only endless has a budget: short mode never comes near it,
        /// and leaving it out keeps short mode exactly as tuned.
        /// </summary>
        public bool HostileRoomFor(int shots)
        {
            if (!IsEndlessRun) return true;
            return HostileProjectileCount() + shots <= Config.endless.maxHostileProjectiles;
        }

        /// <summary>Live hostile projectiles (HUD, tests).</summary>
        public int HostileProjectileCount()
        {
            int live = 0;
            foreach (var p in Projectiles) if (p.Active && p.Faction == AttackFaction.Hostile) live++;
            return live;
        }
    }
}
