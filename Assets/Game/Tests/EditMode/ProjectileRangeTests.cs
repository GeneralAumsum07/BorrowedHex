using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Hostile ranges (owner-approved, 2026-10-04): Scatter 10, Acolyte 15, Siege 22, the
    // Collector's fan 30, and its bolt stream unlimited. Acolyte, Scatter and the Collector all
    // fire the same "bolt" definition, so range belongs to the shooter, not the attack.
    public class ProjectileRangeTests
    {
        const float Dt = 1f / 60;

        // Every shot's flight, keyed by projectile id: where it started, and how it ended.
        sealed class Flight { public Vector2 From; public int Source; public float Spread; public bool Ended; public ProjectileEndReason Why; public float Travelled; }

        // A world arena with every wall removed, re-cleared each tick in case cover re-forms:
        // the arena is 48x36, so with walls in place a long shot would always end on one and
        // the test could not tell range from geometry.
        static ArenaSim OpenWorld(out Dictionary<int, Flight> flights)
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = 11, Sandbox = true, WorldArenas = true });
            sim.AutoSpawn = false;
            sim.Player.Position = Vector2.zero;
            // Invulnerable players let hostile shots pass straight through (ArenaSim.Projectiles),
            // so a shot aimed at the player keeps flying until its own range ends it.
            P5.Invulnerable(sim);
            var log = new Dictionary<int, Flight>();
            sim.Events.ProjectileSpawned += p => log[p.ProjectileId] = new Flight
                { From = p.Position, Source = p.Shot.SourceActorId, Spread = p.Shot.SourceSpreadHalfAngle };
            sim.Events.ProjectileEnded += (p, why) =>
            {
                var f = log[p.ProjectileId];
                f.Ended = true; f.Why = why; f.Travelled = (p.Position - f.From).magnitude;
            };
            flights = log;
            return sim;
        }

        static void Run(ArenaSim sim, float seconds)
        {
            for (int i = 0; i < seconds / Dt; i++) { sim.Walls.Clear(); sim.Tick(P5.Still, Dt); }
        }

        static EnemyActor Ready(ArenaSim sim, ActorCategory kind, Vector2 at)
        {
            var e = sim.SpawnEnemy(kind, at);
            e.ActiveAt = sim.Clock.Now; // skip the spawn warning
            return e;
        }

        [TestCase(ActorCategory.ScatterCaster, 10f)]
        [TestCase(ActorCategory.Acolyte, 15f)]
        [TestCase(ActorCategory.SiegeFamiliar, 22f)]
        public void OrdinaryShooterShotsExpireAtTheirShootersRange(ActorCategory kind, float range)
        {
            var sim = OpenWorld(out var flights);
            var e = Ready(sim, kind, new Vector2(-8, 0));
            Run(sim, 12);
            int expired = 0;
            foreach (var f in flights.Values)
            {
                if (f.Source != e.ActorId || !f.Ended) continue;
                Assert.That(f.Why, Is.EqualTo(ProjectileEndReason.Expired), "Nothing else stands in an open arena.");
                Assert.That(f.Travelled, Is.EqualTo(range).Within(.1f));
                expired++;
            }
            Assert.That(expired, Is.GreaterThan(0), $"{kind} fired and its shots ran out of range.");
        }

        [Test]
        public void TheCollectorsFanReachesThirtyAndItsStreamNeverRunsOut()
        {
            var sim = OpenWorld(out var flights);
            var boss = sim.SpawnBoss();
            boss.ActiveAt = sim.Clock.Now;
            int fanExpired = 0, streamFar = 0;
            // Alternate the player's distance so the boss chooses both ranged patterns: inside
            // fanMaxDistance it fans, beyond it streams (CollectorBoss.ChooseByPosition).
            for (int round = 0; round < 6; round++)
            {
                sim.Player.Position = boss.Position + new Vector2(round % 2 == 0 ? 6f : 14f, 0);
                Run(sim, 4);
            }
            foreach (var pair in flights)
            {
                var f = pair.Value;
                if (f.Source != boss.ActorId) continue;
                bool fan = f.Spread > 0; // the stream fires a one-bolt "volley" with no spread
                if (fan && f.Ended)
                {
                    Assert.That(f.Why, Is.EqualTo(ProjectileEndReason.Expired));
                    Assert.That(f.Travelled, Is.EqualTo(30f).Within(.1f));
                    fanExpired++;
                }
                if (!fan)
                {
                    Assert.That(f.Ended, Is.False, "A stream bolt ignores both range and lifetime.");
                    var live = sim.Projectiles.Find(p => p.ProjectileId == pair.Key);
                    // 36 units is the bolt's whole flight under its 4 s lifetime (speed 9).
                    if ((live.Position - f.From).magnitude > 36f) streamFar++;
                }
            }
            Assert.That(fanExpired, Is.GreaterThan(0), "The boss fanned at least once.");
            Assert.That(streamFar, Is.GreaterThan(0), "A stream bolt flew past the bolt's lifetime distance.");
        }

        [Test]
        public void TheShippedConfigAssetCarriesTheApprovedRanges()
        {
            // The game loads this asset, not CreateDefault(). Unity fills a field missing from
            // it with the nested class's constructor value (EnemyTuning.range = 15), not the
            // per-kind initializer in CombatTuning, so the asset itself must hold each value.
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.GameConfig>("Assets/Game/Data/GameConfig.asset");
            Assert.NotNull(cfg);
            Assert.That(cfg.combat.scatter.range, Is.EqualTo(10f));
            Assert.That(cfg.combat.acolyte.range, Is.EqualTo(15f));
            Assert.That(cfg.combat.siege.range, Is.EqualTo(22f));
            Assert.That(cfg.collector.fanRange, Is.EqualTo(30f));
            Assert.That(cfg.collector.streamUnlimited, Is.True);
        }
    }
}
