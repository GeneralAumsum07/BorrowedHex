using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Phase 5 test helpers: scored short runs, driven tick by tick.</summary>
    static class P5
    {
        public const float Dt = 1f / 60f;
        public static PlayerCommand Still => PlayerCommand.Moving(Vector2.zero);

        public static ArenaSim Short(int seed = 1) =>
            new ArenaSim(TestSims.Config, new RunSetup { Seed = seed, Mode = GameMode.Short, Sandbox = false });

        /// <summary>
        /// Hostile hits pass through (section 3: invulnerable players let shots through), so a
        /// schedule test can run the full 180 s without the player's survival being the subject.
        /// </summary>
        public static void Invulnerable(ArenaSim sim) => sim.Player.InvulnerableUntil = 1e9;

        /// <summary>Tick until the state changes away from <paramref name="from"/> or the tick budget runs out.</summary>
        public static int TickWhile(ArenaSim sim, RunState from, int maxTicks = 20000)
        {
            int n = 0;
            while (sim.State == from && n < maxTicks) { sim.Tick(Still, Dt); n++; }
            return n;
        }

        /// <summary>
        /// A player who kills everything the moment it can be hurt (past its spawn warning).
        /// Encounters end only on a full clear (D50), so schedule tests need this player.
        /// </summary>
        public static void KillActiveOrdinaries(ArenaSim sim)
        {
            double now = sim.Clock.Now;
            foreach (var e in sim.Enemies)
                if (e.Alive && !e.IsBoss && now >= e.ActiveAt) Kill(sim, e);
        }

        /// <summary>Tick, clearing as enemies arrive, until the current encounter is cleared.</summary>
        public static int ClearEncounter(ArenaSim sim, int maxTicks = 20000)
        {
            int n = 0;
            if (sim.State == RunState.Ready) { sim.Tick(Still, Dt); n++; }
            while (sim.State == RunState.Combat && n < maxTicks)
            {
                sim.Tick(Still, Dt);
                n++;
                KillActiveOrdinaries(sim);
            }
            return n;
        }

        /// <summary>Drive a run through its three cleared encounters to the start of the boss fight.</summary>
        public static void ToBossCombat(ArenaSim sim)
        {
            Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                ClearEncounter(sim);
                Assert.AreEqual(RunState.UpgradeChoice, sim.State, $"transition {i + 1}");
                Assert.IsTrue(sim.ContinueFromUpgrade());
            }
            Assert.AreEqual(RunState.BossIntro, sim.State);
            Assert.IsTrue(sim.CompleteBossIntro());
        }

        public static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Still, Dt);
        }

        public static void Kill(ArenaSim sim, EnemyActor e, int root = 0, AttackKind kind = AttackKind.Bolt)
        {
            var shot = new AttackSnapshot { Kind = kind, SourceActorId = 900, ShotId = sim.Ids.Next() };
            sim.DamageEnemy(e, e.Health, DamageCategory.ReturnedProjectile, shot, root);
        }

        public static void Hit(ArenaSim sim, EnemyActor e, float amount, int root)
        {
            var shot = new AttackSnapshot { Kind = AttackKind.Bolt, SourceActorId = 900, ShotId = sim.Ids.Next() };
            sim.DamageEnemy(e, amount, DamageCategory.ReturnedProjectile, shot, root);
        }
    }

    public class RunLifecycleTests
    {
        [Test]
        public void NewShortRun_StartsReady_AndTheFirstTickEntersCombat()
        {
            var sim = P5.Short();
            Assert.AreEqual(RunState.Ready, sim.State);
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(RunState.Combat, sim.State);
        }

        [Test]
        public void ClearingTheEncounter_OpensTheUpgradeChoice_AndPausesEveryTimer()
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            Assert.AreEqual(1, sim.TransitionsReached);
            Assert.AreEqual(0, sim.EnemiesLeftInEncounter());
            Assert.Less(sim.Clock.Now, sim.Config.shortMode.TotalLength, "cleared well inside the run clock");
            Assert.IsTrue(sim.Clock.HasPauseReason(PauseReason.UpgradeChoice));

            // Frozen: more ticks move neither the clock nor a packet's expiry.
            double at = sim.Clock.Now;
            var packet = sim.Packets.Create(sim.Ids.Next(), 0, at, 0.5f, 12);
            for (int i = 0; i < 120; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(at, sim.Clock.Now);
            CollectionAssert.Contains(sim.Packets.Packets, packet, "a 0.5 s packet survives 2 s of choice");

            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.AreEqual(RunState.Combat, sim.State);
            Assert.AreEqual(1, sim.Encounter);
            sim.Tick(P5.Still, P5.Dt);
            Assert.Greater(sim.Clock.Now, at);
            Assert.Greater(sim.EnemiesLeftInEncounter(), 0, "encounter 2 has its own plan");
        }

        [Test]
        public void AnEncounter_NeverEndsWhileAnyEnemyLives()
        {
            // Owner direction (D50): the objective is "kill all enemies", not "survive".
            var sim = P5.Short();
            P5.Invulnerable(sim);
            P5.Run(sim, 60 * 90);
            Assert.AreEqual(RunState.Combat, sim.State);
            Assert.AreEqual(0, sim.TransitionsReached);
            Assert.Greater(sim.EnemiesLeftInEncounter(), 0);
        }

        [Test]
        public void EnemiesLeft_IsTheWholePlan_AndCountsDownOnlyOnKills()
        {
            var sim = P5.Short(4);
            P5.Invulnerable(sim);
            sim.Tick(P5.Still, P5.Dt);
            int planned = sim.EnemiesLeftInEncounter();
            // Four formations of two or three members each (encounter 1, D50).
            Assert.GreaterOrEqual(planned, 8);
            Assert.LessOrEqual(planned, 12);
            // Spawning moves members from "planned" to "alive" without changing the total.
            P5.Run(sim, 60 * 20);
            Assert.AreEqual(planned, sim.EnemiesLeftInEncounter());
            EnemyActor victim = null;
            foreach (var e in sim.Enemies) if (e.Alive && !e.IsBoss) { victim = e; break; }
            Assert.NotNull(victim);
            P5.Kill(sim, victim);
            Assert.AreEqual(planned - 1, sim.EnemiesLeftInEncounter());
        }

        [Test]
        public void TheSharedRunClock_EndsTheRun_EvenMidEncounter()
        {
            // One 3:00 clock for the whole run (owner ruling): a player who never clears
            // encounter 1 still runs out of time.
            var sim = P5.Short();
            P5.Invulnerable(sim);
            P5.TickWhile(sim, RunState.Ready);
            P5.TickWhile(sim, RunState.Combat);
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.TimeExpired, sim.Summary.Reason);
            Assert.AreEqual(0, sim.Encounter);
            Assert.AreEqual(180.0, sim.Clock.Now, 1e-4);
            Assert.AreEqual(0f, sim.SecondsLeftInRun());
        }

        [Test]
        public void PausingOverTheUpgradeChoice_ResumesOntoTheUpgradeChoice()
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            sim.SetPause(PauseReason.Menu, true);
            Assert.AreEqual(RunState.Paused, sim.State);
            Assert.IsFalse(sim.ContinueFromUpgrade(), "the menu covers the choice; Continue is not live");
            sim.SetPause(PauseReason.Menu, false);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
        }

        [Test]
        public void PauseInCombat_FreezesTime_AndRestoresCombat()
        {
            var sim = P5.Short();
            for (int i = 0; i < 30; i++) sim.Tick(P5.Still, P5.Dt);
            double t = sim.Clock.Now;
            sim.SetPause(PauseReason.Menu, true);
            sim.SetPause(PauseReason.FocusLost, true);
            for (int i = 0; i < 30; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(t, sim.Clock.Now);
            sim.SetPause(PauseReason.Menu, false);
            Assert.AreEqual(RunState.Paused, sim.State, "focus loss still holds the pause");
            sim.SetPause(PauseReason.FocusLost, false);
            Assert.AreEqual(RunState.Combat, sim.State);
        }

        [Test]
        public void BossTransition_DespawnsOrdinariesWithoutReward_KeepsPackets_AndShowsTheBoss()
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                P5.ClearEncounter(sim);
                if (i < 2) sim.ContinueFromUpgrade();
            }
            Assert.AreEqual(3, sim.TransitionsReached);
            // Encounters end cleared, so plant leftovers to prove the cleanup: an ordinary
            // enemy and one of its shots in flight.
            var leftover = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(6f, 0f));
            leftover.ActiveAt = 0;
            var bolt = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), leftover.ActorId, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(bolt, AttackFaction.Hostile, new Vector2(5f, 0f), Vector2.left);
            Assert.Greater(sim.AliveOrdinaryCount(), 0);
            var packet = sim.Packets.Create(sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            Assert.NotNull(packet);
            int score = sim.Score.Score, kills = sim.Score.Kills;

            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.AreEqual(RunState.BossIntro, sim.State);
            Assert.AreEqual(0, sim.AliveOrdinaryCount());
            Assert.AreEqual(0, sim.CountProjectiles(AttackFaction.Hostile));
            Assert.AreEqual(score, sim.Score.Score, "cleanup despawns give no score");
            Assert.AreEqual(kills, sim.Score.Kills);
            CollectionAssert.Contains(sim.Packets.Packets, packet, "captured packets survive the transition");
            Assert.NotNull(sim.Boss);
            Assert.IsTrue(sim.Boss.Alive);
            Assert.AreEqual(50f, sim.Boss.MaxHealth);
            Assert.IsTrue(sim.Clock.IsPaused, "the intro banner holds the clock");

            Assert.IsTrue(sim.CompleteBossIntro());
            Assert.AreEqual(RunState.BossCombat, sim.State);
            // No ordinary spawns during the boss window.
            for (int i = 0; i < 600; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(0, sim.AliveOrdinaryCount());
        }

        [Test]
        public void LivingBossAt180Seconds_EndsTheRun_TimeExpired()
        {
            var sim = P5.Short();
            P5.ToBossCombat(sim);
            P5.TickWhile(sim, RunState.BossCombat);
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.TimeExpired, sim.Summary.Reason);
            Assert.AreEqual(180.0, sim.Clock.Now, 1e-4);
            Assert.AreEqual(0, sim.Summary.VictoryBonus);
        }

        [Test]
        public void KillingTheBoss_WinsImmediately_WithTwoPointsPerUnusedSecond()
        {
            var sim = P5.Short();
            P5.ToBossCombat(sim);
            for (int i = 0; i < 90; i++) sim.Tick(P5.Still, P5.Dt); // past its spawn warning
            int before = sim.Score.Score;
            P5.Kill(sim, sim.Boss);
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.Victory, sim.Summary.Reason);
            // Whole unused seconds of the shared clock, two points each.
            int unused = (int)System.Math.Floor(180.0 - sim.Clock.Now + 1e-6);
            Assert.Greater(unused, 0);
            Assert.AreEqual(unused * 2, sim.Summary.VictoryBonus);
            Assert.AreEqual(before + 250 + unused * 2, sim.Summary.Score);
            Assert.AreEqual(1, sim.Summary.BossesDefeated);
        }

        [Test]
        public void DeathAndBossDefeatOnTheSameTick_DeathWins()
        {
            var sim = P5.Short();
            P5.ToBossCombat(sim);
            for (int i = 0; i < 90; i++) sim.Tick(P5.Still, P5.Dt);
            var boss = sim.Boss;
            boss.Health = 1f;
            sim.Player.InvulnerableUntil = 0;
            sim.Player.Health = 1;
            // Both shots connect inside the next tick's projectile sweep.
            var mine = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 900, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(mine, AttackFaction.Returned, boss.Position + Vector2.right * (boss.Radius + 0.2f), Vector2.left, rootReleaseId: 77);
            var theirs = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), boss.ActorId, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(theirs, AttackFaction.Hostile, sim.Player.Position + Vector2.right * (sim.Player.Radius + 0.2f), Vector2.left);
            sim.Tick(P5.Still, P5.Dt);
            Assert.IsTrue(boss.Killed, "fixture: the boss did die this tick");
            Assert.IsFalse(sim.Player.Alive, "fixture: the player did die this tick");
            Assert.AreEqual(RunEndReason.Death, sim.Summary.Reason);
        }

        [Test]
        public void Death_EndsTheRunOnce_AndTheSummaryNeverChangesAfterwards()
        {
            var sim = P5.Short();
            for (int i = 0; i < 300; i++) sim.Tick(P5.Still, P5.Dt);
            int ended = 0;
            sim.Events.RunEnded += _ => ended++;
            sim.Player.InvulnerableUntil = 0;
            sim.Player.Health = 1;
            sim.DamagePlayer(1, 0);
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(RunState.Results, sim.State);
            var summary = sim.Summary;
            Assert.AreEqual(RunEndReason.Death, summary.Reason);
            int score = summary.Score;
            float duration = summary.Duration;

            for (int i = 0; i < 300; i++) sim.Tick(P5.Still, P5.Dt);
            foreach (var e in sim.Enemies) if (e.Alive) P5.Kill(sim, e); // late events after the end
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(1, ended);
            Assert.AreSame(summary, sim.Summary);
            Assert.AreEqual(score, sim.Summary.Score);
            Assert.AreEqual(duration, sim.Summary.Duration);
            Assert.AreEqual(RunState.Results, sim.State);
        }

        [Test]
        public void SandboxRuns_HaveNoScheduleAndNeverEnd()
        {
            var sim = TestSims.Sandbox();
            for (int i = 0; i < 2500; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(RunState.Combat, sim.State, "no upgrade pause at 40 s in the sandbox");
            sim.Player.InvulnerableUntil = 0;
            sim.DamagePlayer(sim.Player.Health, 0);
            sim.Tick(P5.Still, P5.Dt);
            Assert.IsNull(sim.Summary);
            Assert.AreEqual(RunState.Combat, sim.State);
        }

        [Test]
        public void RestartingRepeatedly_NeverDuplicatesListeners()
        {
            // Every restart builds a new sim from the same config; the score service of each
            // run must hear its own kills exactly once and nothing from the runs before it.
            ArenaSim sim = null;
            for (int run = 0; run < 5; run++)
            {
                sim = P5.Short(run);
                sim.Tick(P5.Still, P5.Dt);
            }
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(5f, 5f));
            e.ActiveAt = 0;
            P5.Kill(sim, e);
            Assert.AreEqual(10, sim.Score.Score);
            Assert.AreEqual(1, sim.Score.Kills);
        }
    }
}
