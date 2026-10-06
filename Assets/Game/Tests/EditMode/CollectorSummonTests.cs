using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class CollectorSummonTests
    {
        const float Dt = 1f / 60f;

        static ArenaSim Fight(int seed = 11)
        {
            var cfg = GameConfig.CreateDefault();
            cfg.collector.teleportChance = 0;
            var sim = new ArenaSim(cfg, new RunSetup { Sandbox = true, Seed = seed });
            sim.AutoSpawn = false;
            P5.Invulnerable(sim);
            sim.SpawnBoss();
            return sim;
        }

        static void Run(ArenaSim sim, float seconds)
        {
            for (int i = 0; i < seconds / Dt; i++) sim.Tick(P5.Still, Dt);
        }

        // Removing the summon execution (or spawning one/three adds) must fail this test.
        [Test]
        public void CollectorPeriodicallyCastsAndSpawnsExactlyTwoOrdinaryEnemies()
        {
            var sim = Fight();
            var arrivals = new List<EnemyActor>();
            bool sawCast = false;
            sim.Events.EnemyTelegraph += e => sawCast |= e.IsBoss && e.Boss.Pattern.ToString() == "Summon";
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) arrivals.Add(e); };
            Run(sim, 24);
            Assert.That(arrivals, Is.Empty, "The first summon must wait its cooldown.");
            Run(sim, 12);
            Assert.That(sawCast, Is.True, "Summon has its own visible wind-up.");
            Assert.That(arrivals.Count, Is.EqualTo(2));
            Assert.That(arrivals[0].SpawnedAt, Is.EqualTo(arrivals[1].SpawnedAt));
            foreach (var e in arrivals)
            {
                CollectionAssert.Contains(new[] { ActorCategory.Acolyte, ActorCategory.Pursuer,
                    ActorCategory.ScatterCaster, ActorCategory.SiegeFamiliar }, e.Category);
                Assert.That(e.ActiveAt - e.SpawnedAt, Is.EqualTo(.8).Within(.0001));
                foreach (var wall in sim.Walls)
                    Assert.That(Geometry2D.CircleOverlapsRect(e.PrevPosition, e.Radius, wall), Is.False);
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(9.5f, 5.5f)]
        [TestCase(6f, 1.8f)]
        public void SummonPlacesBothEnemiesWithinFourUnitsOfCollector(float x, float y)
        {
            var sim = Fight();
            Run(sim, 1.1f);
            var boss = sim.LivingBoss();
            boss.Position = boss.PrevPosition = new Vector2(x, y);
            sim.Player.Position = boss.Position + Vector2.left * 1.5f;
            boss.Boss.Pattern = BossPattern.Summon;
            boss.Boss.Stage = BossStage.Reposition;
            boss.Boss.StageEndsAt = sim.Clock.Now;
            var arrivals = new List<EnemyActor>();
            sim.Events.EnemySpawned += e =>
            {
                if (e.IsBoss) return;
                arrivals.Add(e);
                Assert.That(Vector2.Distance(e.Position, boss.Position), Is.InRange(2.5f, 4.001f));
                Assert.That(Vector2.Distance(e.Position, sim.Player.Position),
                    Is.GreaterThan(sim.Player.Radius + e.Radius + .6f));
                foreach (var wall in sim.Walls)
                    Assert.That(Geometry2D.CircleOverlapsRect(e.Position, e.Radius + .3f, wall), Is.False);
            };
            Run(sim, 2);
            Assert.That(arrivals.Count, Is.EqualTo(2));
            Assert.That(Vector2.Distance(boss.Boss.SummonedPositions[0], boss.Boss.SummonedPositions[1]),
                Is.GreaterThanOrEqualTo(arrivals[0].Radius + arrivals[1].Radius + .6f));
        }

        [Test]
        public void ABlockedSummonWaitsUntilBothNearbySpawnsAreLegal()
        {
            var sim = Fight();
            Run(sim, 1.1f);
            var boss = sim.LivingBoss();
            boss.Position = boss.PrevPosition = Vector2.zero;
            boss.Boss.Pattern = BossPattern.Summon;
            boss.Boss.Stage = BossStage.Reposition;
            boss.Boss.StageEndsAt = sim.Clock.Now;
            // Leave no nearby space: never accept a far-away fallback or half a pair.
            sim.Walls.Clear();
            sim.Walls.Add(new Rect(-4.8f, -4.8f, 9.6f, 9.6f));
            int arrivals = 0;
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) arrivals++; };
            Run(sim, 2);
            Assert.That(arrivals, Is.Zero);
            Assert.That(boss.Boss.SummonsResolved, Is.Zero);
            sim.Walls.Clear();
            Run(sim, .1f);
            Assert.That(arrivals, Is.EqualTo(2));
            Assert.That(boss.Boss.SummonsResolved, Is.EqualTo(1));
        }

        [Test]
        public void CollectorRangedBoltsAreFifteenPercentFasterWithoutChangingOrdinaryBolts()
        {
            var sim = Fight();
            int bossShots = 0, ordinaryShots = 0;
            sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(-8, 0));
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.Kind != AttackKind.Bolt) return;
                bool boss = p.Shot.SourceCategory == ActorCategory.Boss;
                Assert.That(p.Shot.Speed, Is.EqualTo(boss ? 10.35f : 9f).Within(.0001));
                if (boss) bossShots++; else ordinaryShots++;
            };
            Run(sim, 20);
            Assert.That(bossShots, Is.GreaterThan(0));
            Assert.That(ordinaryShots, Is.GreaterThan(0));
        }

        [Test]
        public void SummonUsesTheCastingPose()
        {
            Assert.That(Enum.TryParse("Summon", out BossPattern summon), Is.True);
            Assert.That(Presentation.WorldArt.WorldArtPolicy.Clip(BossStage.Telegraph, summon), Is.EqualTo("Attack3"));
        }

        [Test]
        public void SummonArrivalsKeepTheirRandomCadenceAndPauseWithGameplay()
        {
            var sim = Fight();
            var boss = sim.LivingBoss();
            var times = new List<double>();
            sim.Events.EnemyFired += e =>
            {
                if (e == boss && e.Boss.Pattern == BossPattern.Summon) times.Add(sim.Clock.Now);
            };
            Run(sim, 12);
            double frozen = sim.Clock.Now;
            sim.SetPause(PauseReason.Manual, true);
            Run(sim, 60);
            Assert.That(sim.Clock.Now, Is.EqualTo(frozen));
            Assert.That(times, Is.Empty);
            sim.SetPause(PauseReason.Manual, false);
            Run(sim, 108);
            Assert.That(times.Count, Is.EqualTo(4));
            double previous = boss.ActiveAt;
            foreach (double time in times)
            {
                Assert.That(time - previous, Is.InRange(25d, 30.05d), "Cast reservations avoid cooldown drift.");
                previous = time;
            }
        }

        [Test]
        public void SummonRollsRepeatForASeedAndAllowEveryKindAndDuplicates()
        {
            List<string> Arrivals(int seed)
            {
                var sim = Fight(seed);
                var log = new List<string>();
                sim.Events.EnemySpawned += e =>
                {
                    if (!e.IsBoss) log.Add($"{e.Category} {e.Position.x:F4},{e.Position.y:F4} {sim.Clock.Now:F4}");
                };
                Run(sim, 65);
                return log;
            }
            CollectionAssert.AreEqual(Arrivals(11), Arrivals(11));
            CollectionAssert.AreNotEqual(Arrivals(11), Arrivals(12));
            var kinds = new HashSet<ActorCategory>();
            bool duplicates = false;
            for (int seed = 1; seed <= 24; seed++)
            {
                var sim = Fight(seed);
                var pair = new List<ActorCategory>();
                sim.Events.EnemySpawned += e => { if (!e.IsBoss) { pair.Add(e.Category); kinds.Add(e.Category); } };
                Run(sim, 34);
                Assert.That(pair.Count, Is.EqualTo(2));
                duplicates |= pair[0] == pair[1];
            }
            Assert.That(kinds.Count, Is.EqualTo(4));
            Assert.That(duplicates, Is.True, "The second roll must not exclude the first kind.");
        }

        [Test]
        public void KillingTheCollectorDuringItsCastCancelsTheSummon()
        {
            var sim = Fight();
            var boss = sim.LivingBoss();
            for (int i = 0; i < 35 / Dt; i++)
            {
                sim.Tick(P5.Still, Dt);
                if (boss.Boss.Pattern == BossPattern.Summon && boss.Boss.Stage == BossStage.Telegraph) break;
            }
            Assert.That(boss.Boss.Stage, Is.EqualTo(BossStage.Telegraph));
            Assert.That(boss.Boss.Pattern, Is.EqualTo(BossPattern.Summon));
            int spawned = 0;
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) spawned++; };
            // Exercise the real death path: health and Alive are separate actor state.
            Assert.That(sim.DamageEnemy(boss, 1000, DamageCategory.ReturnedProjectile, default, 0), Is.True);
            Run(sim, 35);
            Assert.That(spawned, Is.Zero);
        }

        [TestCase(BossPattern.BoltStream, false)]
        [TestCase(BossPattern.FanVolley, false)]
        [TestCase(BossPattern.BoltStream, true)]
        [TestCase(BossPattern.FanVolley, true)]
        public void SummonWaitsForRangedExecutionAndFullRecovery(BossPattern ranged, bool freeBeforeCast)
        {
            var cfg = GameConfig.CreateDefault();
            cfg.collector.teleportChance = 0;
            cfg.endless.maxHostileProjectiles = 1;
            cfg.endless.waveLength = 10000;
            var sim = new ArenaSim(cfg, new RunSetup { Mode = GameMode.Endless, Seed = 11 });
            P5.Invulnerable(sim);
            var boss = sim.SpawnBoss();
            var shot = Combat.AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), boss.ActorId, sim.Ids.Next(), 0);
            // Keep one hostile shot alive outside geometry/player reach to saturate the budget.
            var blocker = sim.SpawnProjectile(shot, AttackFaction.Hostile, new Vector2(1000, 1000), Vector2.right);
            blocker.ExpireAt = double.PositiveInfinity;
            int arrivals = 0;
            int fired = 0;
            double lastShotAt = 0;
            sim.Events.EnemySpawned += e => { if (!e.IsBoss && boss.Boss.Pattern == BossPattern.Summon) arrivals++; };
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.SourceActorId != boss.ActorId) return;
                fired++;
                lastShotAt = sim.Clock.Now;
            };
            sim.Tick(P5.Still, Dt);
            // Pin the requested attack once the warning ends; no ordinary wave can free the slot.
            Run(sim, 1);
            boss.Boss.Pattern = ranged;
            boss.Boss.Stage = ranged == BossPattern.BoltStream ? BossStage.Active : BossStage.Telegraph;
            boss.Boss.StageEndsAt = sim.Clock.Now;
            boss.Boss.ShotsLeft = 12;
            boss.Boss.NextShotAt = sim.Clock.Now;
            double deadline = boss.Boss.NextSummonAt;
            while (sim.Clock.Now < deadline + .05)
            {
                // The timer can become due while an attack is held, or while it has just
                // regained budget. Neither case may interrupt the move or its recovery.
                if (freeBeforeCast && sim.Clock.Now >= deadline - cfg.collector.summonTelegraph - .3)
                    cfg.endless.maxHostileProjectiles = 1000;
                sim.Walls.Clear(); sim.Tick(P5.Still, Dt);
                if (boss.Boss.Pattern == BossPattern.Summon)
                {
                    Assert.That(fired, Is.EqualTo(ranged == BossPattern.BoltStream ? 12 : 9));
                    Assert.That(sim.Clock.Now - lastShotAt, Is.GreaterThanOrEqualTo(.5 - .0001), "Full recovery precedes Summon.");
                }
            }
            Assert.That(arrivals, Is.Zero, "A due summon may run late while the previous move finishes.");
            if (!freeBeforeCast)
            {
                Assert.That(boss.Boss.Pattern, Is.EqualTo(ranged));
                Assert.That(fired, Is.Zero);
                cfg.endless.maxHostileProjectiles = 1000;
            }
            for (int i = 0; i < 6 / Dt && arrivals == 0; i++)
            {
                sim.Walls.Clear(); sim.Tick(P5.Still, Dt);
                if (boss.Boss.Pattern == BossPattern.Summon)
                {
                    Assert.That(fired, Is.EqualTo(ranged == BossPattern.BoltStream ? 12 : 9));
                    Assert.That(sim.Clock.Now - lastShotAt, Is.GreaterThanOrEqualTo(.5 - .0001));
                }
            }
            Assert.That(arrivals, Is.EqualTo(2), "The queued summon executes after the attack, recovery and cast.");
        }

        [TestCase(BossPattern.BoltStream)]
        [TestCase(BossPattern.FanVolley)]
        public void AnOverdueSummonCannotShortenRecovery(BossPattern previous)
        {
            var sim = Fight();
            Run(sim, 1.1f);
            var boss = sim.LivingBoss();
            boss.Boss.Pattern = previous;
            boss.Boss.Stage = BossStage.Recover;
            double recoverUntil = boss.Boss.StageEndsAt = sim.Clock.Now + .5;
            boss.Boss.NextSummonAt = sim.Clock.Now - 1;
            while (sim.Clock.Now + Dt < recoverUntil)
            {
                sim.Tick(P5.Still, Dt);
                Assert.That(boss.Boss.Pattern, Is.EqualTo(previous));
                Assert.That(boss.Boss.Stage, Is.EqualTo(BossStage.Recover));
            }
            sim.Tick(P5.Still, Dt);
            sim.Tick(P5.Still, Dt);
            Assert.That(boss.Boss.Pattern, Is.EqualTo(BossPattern.Summon));
            Assert.That(sim.Clock.Now, Is.GreaterThanOrEqualTo(recoverUntil));
        }
    }
}
