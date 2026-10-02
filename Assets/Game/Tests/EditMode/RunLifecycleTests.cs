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

        /// <summary>Drive a run through its three upgrade choices to the start of the boss window.</summary>
        public static void ToBossCombat(ArenaSim sim)
        {
            Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                TickWhile(sim, sim.State == RunState.Ready ? RunState.Ready : RunState.Combat);
                if (sim.State == RunState.Combat) TickWhile(sim, RunState.Combat);
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
        public void At40ActiveSeconds_TheUpgradeChoicePausesEveryTimer()
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            int ticks = P5.TickWhile(sim, RunState.Ready) + P5.TickWhile(sim, RunState.Combat);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            Assert.AreEqual(2400, ticks, "exactly on the 40 s tick, not one late");
            // 1e-4: the 60 Hz step is the float 1/60 (0.016666668), so 2400 of them sum to
            // 40.000002 — the tick COUNT above is the exact check.
            Assert.AreEqual(40.0, sim.Clock.Now, 1e-4);
            Assert.IsTrue(sim.Clock.HasPauseReason(PauseReason.UpgradeChoice));

            // Frozen: more ticks move neither the clock nor any enemy.
            var before = new List<Vector2>();
            foreach (var e in sim.Enemies) before.Add(e.Position);
            int alive = sim.AliveOrdinaryCount();
            Assert.Greater(alive, 0, "enemies are preserved through the choice");
            for (int i = 0; i < 120; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(40.0, sim.Clock.Now, 1e-4);
            for (int i = 0; i < before.Count; i++) Assert.AreEqual(before[i], sim.Enemies[i].Position);

            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.AreEqual(RunState.Combat, sim.State);
            Assert.AreEqual(1, sim.Encounter);
            sim.Tick(P5.Still, P5.Dt);
            Assert.Greater(sim.Clock.Now, 40.0);
        }

        [Test]
        public void PausingOverTheUpgradeChoice_ResumesOntoTheUpgradeChoice()
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            P5.TickWhile(sim, RunState.Ready);
            P5.TickWhile(sim, RunState.Combat);
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
                if (sim.State == RunState.Ready) P5.TickWhile(sim, RunState.Ready);
                P5.TickWhile(sim, RunState.Combat);
                if (i < 2) sim.ContinueFromUpgrade();
            }
            Assert.AreEqual(3, sim.TransitionsReached);
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
            for (int i = 0; i < 90; i++) sim.Tick(P5.Still, P5.Dt); // past its spawn warning: 121.5 s
            int before = sim.Score.Score;
            P5.Kill(sim, sim.Boss);
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(RunState.Results, sim.State);
            Assert.AreEqual(RunEndReason.Victory, sim.Summary.Reason);
            // 180 - 121.5167 = 58.48 → 58 whole unused seconds.
            Assert.AreEqual(116, sim.Summary.VictoryBonus);
            Assert.AreEqual(before + 250 + 116, sim.Summary.Score);
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
