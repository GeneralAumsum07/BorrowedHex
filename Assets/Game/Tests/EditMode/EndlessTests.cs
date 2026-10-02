using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Phase 12: the endless scheduler (section 6 "Endless mode", D84).
    public class EndlessTests
    {
        const int WaveTicks = 30 * 60;

        /// <summary>
        /// An endless run whose life clock cannot be the subject: a huge starting clock (the
        /// cap moves with it, so +30 s on a boss kill is never clipped) and an invulnerable
        /// player. Each test that needs the real clock says so.
        /// </summary>
        static ArenaSim Endless(int seed = 1, GameConfig config = null, bool bigClock = true)
        {
            config ??= TestSims.Config;
            var stats = PlayerStats.FromConfig(config);
            if (bigClock) stats.StartingSeconds = 100000f;
            var sim = new ArenaSim(config, new RunSetup { Seed = seed, Mode = GameMode.Endless, Stats = stats });
            P5.Invulnerable(sim);
            return sim;
        }

        static GameConfig Fresh() => GameConfig.CreateDefault();

        /// <summary>From a boss intro: start the fight, wait out the spawn warning, kill it.</summary>
        static void KillBoss(ArenaSim sim, int fightTicks = 90)
        {
            Assert.AreEqual(RunState.BossIntro, sim.State);
            Assert.IsTrue(sim.CompleteBossIntro());
            P5.Run(sim, fightTicks);
            Assert.AreEqual(RunState.BossCombat, sim.State);
            P5.Kill(sim, sim.Boss);
            sim.Tick(P5.Still, P5.Dt);
        }

        [Test]
        public void ACycle_IsSixWaves_ChoicesAfterTwoAndFour_ThenTheBoss_ThenTheDeferredChoice()
        {
            var sim = Endless();
            sim.Tick(P5.Still, P5.Dt);   // Ready -> Combat
            var stops = new List<string>();
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                for (int k = 0; k < 3; k++)
                {
                    P5.TickWhile(sim, RunState.Combat, 40000);
                    stops.Add($"{sim.State} w{sim.Wave} c{sim.Cycle}");
                    if (sim.State == RunState.UpgradeChoice)
                    {
                        Assert.AreEqual(cycle, sim.Offers[0].Rank, "offers have the rank of the cycle they are used in");
                        Assert.IsTrue(sim.ContinueFromUpgrade());
                    }
                }
                double lifeBefore = sim.LifeSeconds;
                KillBoss(sim);
                Assert.AreEqual(RunState.UpgradeChoice, sim.State, "the deferred wave-six choice opens when the boss falls");
                Assert.AreEqual(lifeBefore + 30.0 - 91 * P5.Dt, sim.LifeSeconds, 0.05, "+30 s on a boss kill");
                Assert.AreEqual(cycle + 1, sim.Cycle);
                Assert.AreEqual(cycle + 1, sim.Offers[0].Rank, "the post-boss choice already has the next cycle's rank");
                stops.Add($"{sim.State} w{sim.Wave} c{sim.Cycle}");
                Assert.IsTrue(sim.ContinueFromUpgrade());
                Assert.AreEqual(1, sim.Wave, "a new cycle starts at wave 1");
            }
            CollectionAssert.AreEqual(new[]
            {
                "UpgradeChoice w2 c1", "UpgradeChoice w4 c1", "BossIntro w6 c1", "UpgradeChoice w6 c2",
                "UpgradeChoice w2 c2", "UpgradeChoice w4 c2", "BossIntro w6 c2", "UpgradeChoice w6 c3",
            }, stops);
            Assert.AreEqual(12, sim.WavesCompleted);
            Assert.AreEqual(2, sim.Score.BossesDefeated);
            Assert.IsNull(sim.Summary, "a boss kill is not terminal in endless");
        }

        [Test]
        public void AWave_IsThirtyActiveSeconds_AndEnemiesStayThroughAChoice()
        {
            var sim = Endless();
            int ticks = P5.TickWhile(sim, RunState.Ready);
            ticks += P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            Assert.AreEqual(2 * WaveTicks, ticks, 1, "two 30 s waves, to the tick");
            int alive = sim.AliveOrdinaryCount();
            Assert.Greater(alive, 0, "fixture: enemies are alive at the wave boundary");
            sim.ContinueFromUpgrade();
            Assert.AreEqual(alive, sim.AliveOrdinaryCount(), "a wave ends on time, not on a clear: survivors stay");
        }

        [Test]
        public void BossTime_DoesNotAdvanceTheWaveSchedule()
        {
            var sim = Endless();
            sim.Tick(P5.Still, P5.Dt);
            for (int k = 0; k < 2; k++) { P5.TickWhile(sim, RunState.Combat, 40000); sim.ContinueFromUpgrade(); }
            P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.AreEqual(RunState.BossIntro, sim.State);
            // A long boss fight: 45 s of gameplay time pass on the clock.
            double before = sim.Clock.Now;
            KillBoss(sim, 45 * 60);
            Assert.Greater(sim.Clock.Now - before, 45.0, "the gameplay clock did run during the boss");
            sim.ContinueFromUpgrade();
            Assert.AreEqual(30f, sim.WaveSecondsLeft, 1e-4f, "the next wave starts full, whatever the boss took");
            int ticks = P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.AreEqual(2 * WaveTicks, ticks, 1, "and the next choice is exactly two waves later");
        }

        [Test]
        public void Pause_FreezesTheWaveClockTheLifeClockAndScaling()
        {
            var sim = Endless();
            P5.Run(sim, 600);
            float wave = sim.WaveSecondsLeft, life = sim.LifeSeconds, hp = sim.EnemyHealthScale;
            double now = sim.Clock.Now;
            sim.SetPause(PauseReason.Menu, true);
            P5.Run(sim, 3 * WaveTicks);
            Assert.AreEqual(wave, sim.WaveSecondsLeft);
            Assert.AreEqual(life, sim.LifeSeconds);
            Assert.AreEqual(now, sim.Clock.Now);
            Assert.AreEqual(hp, sim.EnemyHealthScale);
            Assert.AreEqual(1, sim.Wave);
            sim.SetPause(PauseReason.Menu, false);
            Assert.AreEqual(RunState.Combat, sim.State);
        }

        [Test]
        public void CycleScaling_AppliesToNewSpawns_AndOverstayMultipliesOnTop()
        {
            var sim = Endless();
            Assert.AreEqual(25f, sim.OverstaySeconds);
            var firstCycle = new List<EnemyActor>();
            sim.Events.EnemySpawned += e => { if (!e.IsBoss && sim.Cycle == 1) firstCycle.Add(e); };
            sim.Tick(P5.Still, P5.Dt);
            for (int k = 0; k < 2; k++) { P5.TickWhile(sim, RunState.Combat, 40000); sim.ContinueFromUpgrade(); }
            P5.TickWhile(sim, RunState.Combat, 40000);
            KillBoss(sim);
            sim.ContinueFromUpgrade();

            Assert.AreEqual(1.15f, sim.EnemyHealthScale, 1e-5f);
            Assert.AreEqual(1.05f, sim.EnemyMoveScale, 1e-5f);
            Assert.AreEqual(0.95f, sim.EnemyCooldownScale, 1e-5f);
            Assert.AreEqual(23f, sim.OverstaySeconds, 1e-5f, "overstay timer -2 s per cycle");
            foreach (var e in firstCycle) Assert.IsFalse(e.Elite && !e.Overstayed, "elites come only from overstaying");

            var spawned = new List<EnemyActor>();
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) spawned.Add(e); };
            P5.Run(sim, 10 * 60);
            Assert.Greater(spawned.Count, 0);
            var c = sim.Config.combat;
            foreach (var e in spawned)
            {
                Assert.AreEqual(c.For(e.Category).health * 1.15f, e.MaxHealth, 1e-3f, $"{e.Category} health");
                Assert.AreEqual(1.05f, e.MoveScale, 1e-5f);
                Assert.AreEqual(0.95f, e.CooldownScale, 1e-5f);
                Assert.IsFalse(e.Elite);
            }
            // Live past the shorter overstay timer: the evolution multiplies the cycle values.
            var probe = spawned[0];
            P5.Run(sim, 24 * 60);
            if (probe.Alive)
            {
                Assert.IsTrue(probe.Overstayed && probe.Elite);
                Assert.AreEqual(c.For(probe.Category).health * 1.15f * c.overstayHealthScale, probe.MaxHealth, 1e-3f);
                Assert.AreEqual(1.05f * c.overstayMoveScale, probe.MoveScale, 1e-5f);
                Assert.AreEqual(0.95f * c.overstayCooldownScale, probe.CooldownScale, 1e-5f);
            }
            else Assert.Inconclusive("probe died; overstay stacking not observed with this seed");
        }

        [Test]
        public void CycleScaling_StopsAtItsCaps()
        {
            var cfg = Fresh();
            cfg.endless.movePerCycle = 0.5f;
            cfg.endless.cooldownPerCycle = 0.5f;
            cfg.endless.overstayStepPerCycle = 20f;
            var sim = Endless(2, cfg);
            sim.Tick(P5.Still, P5.Dt);
            for (int k = 0; k < 2; k++) { P5.TickWhile(sim, RunState.Combat, 40000); sim.ContinueFromUpgrade(); }
            P5.TickWhile(sim, RunState.Combat, 40000);
            KillBoss(sim);
            Assert.AreEqual(1.25f, sim.EnemyMoveScale, 1e-5f, "movement capped at +25%");
            Assert.AreEqual(0.70f, sim.EnemyCooldownScale, 1e-5f, "attack interval floored at 70%");
            Assert.AreEqual(15f, sim.OverstaySeconds, 1e-5f, "overstay floored at 15 s");
        }

        [Test]
        public void TheRepeatedBoss_HasMoreHealth_AndTheEncoreFan()
        {
            var sim = Endless();
            sim.Tick(P5.Still, P5.Dt);
            var bosses = new List<EnemyActor>();
            sim.Events.EnemySpawned += e => { if (e.IsBoss) bosses.Add(e); };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int k = 0; k < 2; k++) { P5.TickWhile(sim, RunState.Combat, 40000); sim.ContinueFromUpgrade(); }
                P5.TickWhile(sim, RunState.Combat, 40000);
                KillBoss(sim);
                sim.ContinueFromUpgrade();
            }
            var t = sim.Config.collector;
            Assert.AreEqual(2, bosses.Count);
            Assert.IsFalse(bosses[0].Boss.Encore);
            Assert.AreEqual(t.health, bosses[0].MaxHealth, 1e-4f);
            Assert.IsTrue(bosses[1].Boss.Encore, "a repeated boss gains its predefined variation");
            Assert.AreEqual(t.health * 1.2f, bosses[1].MaxHealth, 1e-3f, "boss health +20% per completed cycle");
            Assert.AreEqual(t.fanSpreadRepeatDeg, CollectorBoss.FanOf(bosses[1], t));
            Assert.Greater(t.fanSpreadRepeatDeg.Length, t.fanSpreadDeg.Length);
        }

        [Test]
        public void EnemyCap_IsEighteen_AndTheProjectileBudgetDelaysButNeverDrops()
        {
            var cfg = Fresh();
            cfg.endless.maxHostileProjectiles = 12;
            var sim = Endless(4, cfg);
            int maxAlive = 0, maxShots = 0, droppedOutsideBoss = 0;
            // The boss transition clears hostile shots by design (section 6); nothing else may.
            // Clears are counted per tick and only excused on the tick that opened the boss intro.
            int clearedThisTick = 0;
            sim.Events.ProjectileEnded += (p, r) => { if (r == ProjectileEndReason.Cleared) clearedThisTick++; };
            int fired = 0;
            sim.Events.EnemyFired += e => fired++;
            // Waves 1-6 with nobody dying: the cap and the budget are both pushed.
            for (int k = 0; k < 3 && sim.State != RunState.BossIntro; k++)
            {
                for (int i = 0; i < 2 * WaveTicks + 2 && (sim.State == RunState.Combat || sim.State == RunState.Ready); i++)
                {
                    clearedThisTick = 0;
                    sim.Tick(P5.Still, P5.Dt);
                    if (sim.State != RunState.BossIntro) droppedOutsideBoss += clearedThisTick;
                    maxAlive = Mathf.Max(maxAlive, sim.AliveOrdinaryCount());
                    maxShots = Mathf.Max(maxShots, sim.HostileProjectileCount());
                }
                if (sim.State == RunState.UpgradeChoice) sim.ContinueFromUpgrade();
            }
            Assert.AreEqual(18, maxAlive, "the cap of 18 is reached and never passed");
            Assert.LessOrEqual(maxShots, 12, "the budget holds");
            Assert.AreEqual(12, maxShots, "fixture: the budget was actually reached");
            Assert.Greater(fired, 0);
            Assert.AreEqual(0, droppedOutsideBoss, "live shots are never removed to make room");

            // The boss obeys it too (stream and fan), through a whole fight's worth of patterns.
            Assert.AreEqual(RunState.BossIntro, sim.State);
            sim.CompleteBossIntro();
            for (int i = 0; i < 40 * 60; i++)
            {
                sim.Tick(P5.Still, P5.Dt);
                Assert.LessOrEqual(sim.HostileProjectileCount(), 12);
            }
        }

        [Test]
        public void ShortMode_HasNoBudget_AndTheOldOverstayTimer()
        {
            var sim = P5.Short(1);
            Assert.IsTrue(sim.HostileRoomFor(10000));
            Assert.AreEqual(sim.Config.combat.overstaySeconds, sim.OverstaySeconds);
            Assert.IsFalse(sim.IsEndlessRun);
            Assert.IsFalse(sim.RetireRun(), "retirement is endless-only");
        }

        [Test]
        public void ALateDeath_ProducesACompleteValidSummary()
        {
            var sim = Endless(6);
            sim.Tick(P5.Still, P5.Dt);
            for (int k = 0; k < 2; k++) { P5.TickWhile(sim, RunState.Combat, 40000); sim.ContinueFromUpgrade(); }
            P5.TickWhile(sim, RunState.Combat, 40000);
            KillBoss(sim);
            sim.ContinueFromUpgrade();
            P5.Run(sim, WaveTicks + 600);   // into wave 2 of cycle 2
            sim.Player.InvulnerableUntil = 0;
            sim.DamagePlayer(1000000, 0);
            sim.Tick(P5.Still, P5.Dt);

            var s = sim.Summary;
            Assert.IsNotNull(s);
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.Death, s.Reason);
            Assert.AreEqual(GameMode.Endless, s.Mode);
            Assert.AreEqual(7, s.WavesCompleted);
            Assert.AreEqual(7, s.EncountersCompleted, "endless XP counts completed waves");
            Assert.AreEqual(2, s.Cycle);
            Assert.AreEqual(1, s.BossesDefeated);
            Assert.Greater(s.Duration, 6 * 30f + 30f);
            Assert.Greater(s.Kills, 0);
            Assert.IsFalse(s.Debug);
            Assert.AreEqual(0, sim.HostileProjectileCount(), "nothing left flying into the results");

            var service = ProfileService.Load(new MemoryProfileStorage());
            var r = service.FinalizeRun(s, sim.Setup);
            Assert.IsTrue(r.Applied, r.SkippedBecause);
            Assert.AreEqual(7, r.Xp.Encounters);
            Assert.IsTrue(r.Records.Exists(o => o.Kind == Records.LongestRun && o.IsNewBest), "survival record");
        }

        [Test]
        public void TheRealClock_EndsAnEndlessRunTimeExpired()
        {
            var sim = Endless(7, bigClock: false);
            P5.TickWhile(sim, RunState.Ready);
            for (int i = 0; i < 400 * 60 && sim.State != RunState.Results; i++)
            {
                sim.Tick(P5.Still, P5.Dt);
                if (sim.State == RunState.UpgradeChoice) sim.ContinueFromUpgrade();
            }
            Assert.AreEqual(RunEndReason.TimeExpired, sim.Summary.Reason);
            Assert.AreEqual(sim.Stats.StartingSeconds, sim.Summary.Duration, 0.05f, "no kills: the clock lasts exactly its start");
        }

        [Test]
        public void Retiring_IsOnlyBetweenWaves_AndKeepsProgression()
        {
            var sim = Endless(8);
            P5.Run(sim, 60);
            Assert.IsFalse(sim.RetireRun(), "not mid-wave");
            P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            sim.SetPause(PauseReason.Menu, true);
            Assert.IsFalse(sim.RetireRun(), "not from the pause menu over a choice");
            sim.SetPause(PauseReason.Menu, false);
            Assert.IsTrue(sim.RetireRun());
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.Retired, sim.Summary.Reason);
            Assert.AreEqual(2, sim.Summary.WavesCompleted);
            Assert.IsFalse(sim.RetireRun(), "once");

            var service = ProfileService.Load(new MemoryProfileStorage());
            var r = service.FinalizeRun(sim.Summary, sim.Setup);
            Assert.IsTrue(r.Applied, r.SkippedBecause);
            Assert.AreEqual(2, r.Xp.Encounters, "completed waves are what endless XP counts (5 XP each)");
            Assert.AreEqual(1, service.Profile.stats.runs);
        }

        [Test]
        public void ADebugAssistedSession_CannotSubmit()
        {
            // A long session driven by the dev tool: every wave skipped, two whole cycles.
            var sim = Endless(9);
            sim.Tick(P5.Still, P5.Dt);
            Assert.IsFalse(sim.Setup.Debug);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int w = 0; w < 6; w++)
                {
                    Assert.IsTrue(sim.DebugSkipWave());
                    sim.Tick(P5.Still, P5.Dt);
                    if (sim.State == RunState.UpgradeChoice) sim.ContinueFromUpgrade();
                }
                KillBoss(sim);
                sim.ContinueFromUpgrade();
            }
            Assert.IsTrue(sim.Setup.Debug, "using the tool marks the run");
            Assert.AreEqual(2, sim.Score.BossesDefeated);
            P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.IsTrue(sim.RetireRun());
            Assert.IsTrue(sim.Summary.Debug);

            var store = new MemoryProfileStorage();
            var service = ProfileService.Load(store);
            var r = service.FinalizeRun(sim.Summary, sim.Setup);
            Assert.IsFalse(r.Applied);
            Assert.AreEqual("debug", r.SkippedBecause);
            // ...even when the setup is not available to the finalizer.
            Assert.AreEqual("debug", service.FinalizeRun(sim.Summary, null).SkippedBecause);
            Assert.AreEqual(0, service.Profile.stats.runs);
            Assert.AreEqual(0, service.Profile.records.Count);
            Assert.AreEqual(0, service.Profile.achievements.Count, "Second Encore was earned only with help");
        }
    }
}
