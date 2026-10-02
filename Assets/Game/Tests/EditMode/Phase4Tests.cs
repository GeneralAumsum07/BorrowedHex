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
    static class P4
    {
        public const float Dt = 1f / 60f;
        public static PlayerCommand Still => PlayerCommand.Moving(Vector2.zero);

        public static ArenaSim Sim(int seed = 1)
        {
            var sim = TestSims.Sandbox(seed);
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        /// <summary>Spawn an enemy already past its warning and parked (it will not act).</summary>
        public static EnemyActor Parked(ArenaSim sim, ActorCategory c, Vector2 at)
        {
            var e = sim.SpawnEnemy(c, at);
            e.ActiveAt = 0;
            e.Phase = EnemyPhase.Recover; // no brain transitions out of Recover except the pursuer's timer
            e.PhaseEndsAt = double.MaxValue;
            return e;
        }

        public static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Still, Dt);
        }

        public static ProjectileActor Returned(ArenaSim sim, string attackId, Vector2 from, Vector2 dir, int source = 900)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(attackId), source, sim.Ids.Next(), 0f);
            return sim.SpawnProjectile(s, AttackFaction.Returned, from, dir);
        }
    }

    // Phase 4: rockets, explosions, fans, attribution.
    public class AttackPayloadTests
    {
        [Test]
        public void ReturnedRocket_DamagesEnemiesInRadius_ButNotThePlayer()
        {
            var sim = P4.Sim();
            var a = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            var b = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4.2f, 1.3f));
            sim.Player.Position = new Vector2(2.6f, 1.4f); // inside the blast radius, off the rocket's path
            P4.Returned(sim, AttackIds.Rocket, new Vector2(0.5f, 0f), Vector2.right);
            P4.Run(sim, 60);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
            Assert.AreEqual(3f, a.Health, 1e-4f, "8 - 5");
            Assert.AreEqual(3f, b.Health, 1e-4f, "neighbour inside the radius");
        }

        [Test]
        public void Explosion_DamagesEachActorExactlyOnce_IncludingTheDirectHitTarget()
        {
            var sim = P4.Sim();
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4.6f, 0.9f));
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(9f, 0f)); // outside
            var hits = new Dictionary<int, int>();
            sim.Events.EnemyDamaged += (e, d) => { hits.TryGetValue(e.ActorId, out int n); hits[e.ActorId] = n + 1; Assert.AreEqual(DamageCategory.Explosion, d.Category); };
            int bursts = 0;
            sim.Events.Explosion += (_, __, ___) => bursts++;
            P4.Returned(sim, AttackIds.Rocket, new Vector2(0.5f, 0f), Vector2.right);
            P4.Run(sim, 90);
            Assert.AreEqual(1, bursts);
            Assert.AreEqual(2, hits.Count);
            foreach (var n in hits.Values) Assert.AreEqual(1, n);
        }

        [Test]
        public void ReturnedRocket_BurstsOnAPillar()
        {
            var sim = P4.Sim();
            var w = sim.Config.arena.pillars[1]; // (5.4, 2.9) 1.2 x 1.2
            var e = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(w.xMin - 0.2f, w.yMax + 0.9f));
            sim.Player.Position = new Vector2(1f, -3f);
            P4.Returned(sim, AttackIds.Rocket, new Vector2(1f, w.center.y), Vector2.right);
            ProjectileEndReason why = ProjectileEndReason.Cleared;
            sim.Events.ProjectileEnded += (p, r) => why = r;
            P4.Run(sim, 90);
            Assert.AreEqual(ProjectileEndReason.HitWall, why);
            Assert.AreEqual(3f, e.Health, 1e-4f);
        }

        [Test]
        public void HostileRocket_DealsOnePlayerHit_AndNoAreaDamageToEnemies()
        {
            var sim = P4.Sim();
            var bystander = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(0f, 1.1f));
            var siege = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(6f, 0f));
            AttackEmitter.FireVolley(sim, AttackIds.Rocket, siege.ActorId, siege.Position, siege.Radius, Vector2.left, new[] { 0f });
            P4.Run(sim, 120);
            Assert.AreEqual(sim.Stats.MaxHealth - 1, sim.Player.Health);
            Assert.AreEqual(bystander.MaxHealth, bystander.Health);
        }

        [Test]
        public void KillingAShooterWithItsOwnShot_KeepsSourceAttribution()
        {
            var sim = P4.Sim();
            var acolyte = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(4f, 0f));
            acolyte.Health = 1f;
            DamageEvent kill = default;
            int kills = 0;
            sim.Events.EnemyKilled += (e, d) => { kills++; kill = d; };
            P4.Returned(sim, AttackIds.Bolt, new Vector2(1f, 0f), Vector2.right, source: acolyte.ActorId);
            P4.Run(sim, 60);
            Assert.AreEqual(1, kills);
            Assert.AreEqual(acolyte.ActorId, kill.TargetActorId);
            Assert.AreEqual(acolyte.ActorId, kill.SourceActorId, "its own snapshot");
            Assert.IsTrue(acolyte.Killed);
        }

        [Test]
        public void ReturnedFan_KeepsItsFiveShotSpread()
        {
            var sim = P4.Sim();
            var spreads = sim.Config.combat.scatter.volleySpreadDeg;
            double now = sim.Clock.Now;
            Assert.IsTrue(sim.Capture.TryActivate(now, sim.Stats));
            foreach (var off in spreads)
            {
                var snap = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), off);
                sim.Capture.TryCapture(snap, AttackFaction.Hostile, false, now, sim.Packets, sim.Stats, sim.Ids);
            }
            Assert.AreEqual(5, sim.Packets.Packets[0].Payloads.Count);

            Vector2 aim = new Vector2(0.6f, 0.8f);
            ReleaseService.Release(sim, sim.Packets.Packets[0], Vector2.zero, aim, 1f);
            var angles = new List<float>();
            foreach (var p in sim.Projectiles) angles.Add(Vector2.SignedAngle(aim, p.Velocity));
            angles.Sort();
            Assert.AreEqual(spreads.Length, angles.Count);
            for (int i = 0; i < spreads.Length; i++) Assert.AreEqual(spreads[i], angles[i], 0.01f);
        }

        [Test]
        public void CapturedRocket_ReturnsAsARocket()
        {
            var sim = P4.Sim();
            double now = sim.Clock.Now;
            sim.Capture.TryActivate(now, sim.Stats);
            var snap = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Rocket), 77, sim.Ids.Next(), 0f);
            Assert.AreEqual(CaptureResult.CreatedPacket, sim.Capture.TryCapture(snap, AttackFaction.Hostile, false, now, sim.Packets, sim.Stats, sim.Ids));
            Assert.AreEqual(4, sim.Packets.Packets[0].CapacityUsed);
            ReleaseService.Release(sim, sim.Packets.Packets[0], Vector2.zero, Vector2.right, 1f);
            Assert.AreEqual(AttackKind.Rocket, sim.Projectiles[0].Shot.Kind);
            Assert.AreEqual(AttackFaction.Returned, sim.Projectiles[0].Faction);
        }
    }

    // Phase 4: enemy behaviour, spawning, kill bookkeeping.
    public class EnemyEncounterTests
    {
        [Test]
        public void EnemiesNeverSpawnOnThePlayer()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var sim = P4.Sim(seed);
                var b = sim.Config.arena.bounds;
                sim.Player.Position = new Vector2(sim.Random.Range(b.xMin + 1, b.xMax - 1), sim.Random.Range(b.yMin + 1, b.yMax - 1));
                foreach (var f in EnemySpawnService.Formations) EnemySpawnService.Spawn(sim, f);
                foreach (var e in sim.Enemies)
                    Assert.GreaterOrEqual((e.Position - sim.Player.Position).magnitude, sim.Config.combat.minSpawnDistance,
                        $"seed {seed} {e.Category}");
            }
        }

        [Test]
        public void SpawnWarning_IsHarmlessAndImmune()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(0.9f, 0f));
            Assert.IsFalse(sim.DamageEnemy(e, 5f, DamageCategory.ReturnedProjectile, default, 0));
            P4.Run(sim, Mathf.FloorToInt(sim.Config.combat.spawnWarning / P4.Dt) - 2);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
            Assert.AreEqual(e.MaxHealth, e.Health);
        }

        [Test]
        public void Pursuer_StrikeHitsAPlayerWhoStays_Once()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(1.3f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            int telegraphs = 0;
            sim.Events.EnemyTelegraph += _ => telegraphs++;
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.pursuer.telegraph / P4.Dt) + 3);
            Assert.AreEqual(1, telegraphs);
            Assert.AreEqual(sim.Stats.MaxHealth - 1, sim.Player.Health);
        }

        [Test]
        public void Pursuer_StrikeMissesAPlayerWhoLeavesTheMarker()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(1.3f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(EnemyPhase.Telegraph, e.Phase);
            sim.Player.Position = new Vector2(-3.5f, 0f); // step well out of the strike circle
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.pursuer.telegraph / P4.Dt) + 2);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
            Assert.AreEqual(EnemyPhase.Recover, e.Phase);
        }

        [Test]
        public void ScatterCasterFiresAFiveShotFan_SiegeFiresOneRocket()
        {
            var sim = P4.Sim();
            var s = sim.SpawnEnemy(ActorCategory.ScatterCaster, new Vector2(-6f, 0f));
            s.ActiveAt = 0; s.Phase = EnemyPhase.Idle; s.PhaseEndsAt = 0;
            var f = sim.SpawnEnemy(ActorCategory.SiegeFamiliar, new Vector2(7f, 0f));
            f.ActiveAt = 0; f.Phase = EnemyPhase.Idle; f.PhaseEndsAt = 0;
            var fired = new Dictionary<int, List<AttackKind>>();
            sim.Events.ProjectileSpawned += p =>
            {
                if (!fired.TryGetValue(p.Shot.SourceActorId, out var l)) fired[p.Shot.SourceActorId] = l = new List<AttackKind>();
                l.Add(p.Shot.Kind);
            };
            // Long enough for the slower siege telegraph, short enough for one volley each.
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.siege.telegraph / P4.Dt) + 2);
            Assert.AreEqual(5, fired[s.ActorId].Count);
            CollectionAssert.AreEqual(new[] { AttackKind.Rocket }, fired[f.ActorId]);
        }

        [Test]
        public void Despawn_IsNotAKill_AndOverkillRaisesOneKill()
        {
            var sim = P4.Sim();
            var gone = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(6f, 0f));
            var dead = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(-6f, 0f));
            int kills = 0, despawns = 0;
            sim.Events.EnemyKilled += (_, __) => kills++;
            sim.Events.EnemyDespawned += _ => despawns++;
            sim.DespawnEnemy(gone);
            Assert.IsTrue(sim.DamageEnemy(dead, 5f, DamageCategory.Explosion, default, 0));
            Assert.IsFalse(sim.DamageEnemy(dead, 5f, DamageCategory.Explosion, default, 0));
            Assert.AreEqual(1, kills);
            Assert.AreEqual(1, despawns);
            Assert.IsFalse(gone.Killed);
            Assert.IsTrue(dead.Killed);
            Assert.AreEqual(10, dead.KillValue);
        }

        [Test]
        public void Formations_AllSpawn_AndOnlyTheDeliberateOneIsMeleeOnly()
        {
            int meleeOnly = 0;
            foreach (var f in EnemySpawnService.Formations)
            {
                var sim = P4.Sim();
                Assert.DoesNotThrow(() => EnemySpawnService.Spawn(sim, f), f.Name);
                Assert.AreEqual(f.Members.Length, sim.Enemies.Count);
                if (!f.HasRanged) meleeOnly++;
            }
            Assert.AreEqual(1, meleeOnly);
        }

        [Test]
        public void EliteKillValue_IsOneAndAHalfTimesBase()
        {
            var sim = P4.Sim();
            Assert.AreEqual(30, sim.SpawnEnemy(ActorCategory.ScatterCaster, new Vector2(6, 0), elite: true).KillValue);
            Assert.AreEqual(38, sim.SpawnEnemy(ActorCategory.SiegeFamiliar, new Vector2(-6, 0), elite: true).KillValue);
        }
    }
}
