using static BorrowedHex.Tests.ClockFixtures;
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
    // Phase 2 checks (section 9): swept hits, wall ordering, single damage, faction rules,
    // clean pooling, plus the acolyte's telegraph-then-volley rhythm.
    public class ProjectileCollisionTests
    {
        const float Dt = 1f / 60f;
        static readonly PlayerCommand Idle = PlayerCommand.Moving(Vector2.zero);

        static AttackSnapshot Bolt(ArenaSim sim, float speed = -1f)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), sourceActorId: 999, shotId: sim.Ids.Next(), 0f);
            if (speed > 0) s.Speed = speed;
            return s;
        }

        static ArenaSim SimWithPlayerAt(Vector2 pos)
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = pos;
            return sim;
        }

        static EnemyActor ActiveEnemy(ArenaSim sim, Vector2 pos)
        {
            var e = sim.SpawnEnemy(ActorCategory.Acolyte, pos);
            e.ActiveAt = sim.Clock.Now; // skip the spawn warning for collision tests
            e.Phase = EnemyPhase.Idle;
            e.PhaseEndsAt = double.MaxValue; // never shoots during these tests
            return e;
        }

        [Test]
        public void FastShotCrossingPlayerInOneStep_StillHits()
        {
            var sim = SimWithPlayerAt(Vector2.zero);
            // 240 u/s = 4 units per step; it starts 1.5 units away and would end 2.5 units past.
            sim.SpawnProjectile(Bolt(sim, 240f), AttackFaction.Hostile, new Vector2(-1.5f, 0f), Vector2.right);
            sim.Tick(Idle, Dt);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.AreEqual(0, sim.Projectiles.Count, "the projectile ends on impact");
        }

        [Test]
        public void WallBetweenShotAndPlayer_BlocksTheLaterImpact()
        {
            // Pillar at x∈[-6.6,-5.4], z∈[2.9,4.1]; shoot north through it at the player beyond.
            var sim = SimWithPlayerAt(new Vector2(-6f, 5.5f));
            int ended = 0; ProjectileEndReason reason = ProjectileEndReason.Expired;
            sim.Events.ProjectileEnded += (p, r) => { ended++; reason = r; };
            sim.SpawnProjectile(Bolt(sim, 300f), AttackFaction.Hostile, new Vector2(-6f, 1.5f), Vector2.up);
            sim.Tick(Idle, Dt); // one step covers 5 units: pillar AND player are both on the segment
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(ProjectileEndReason.HitWall, reason);
        }

        [Test]
        public void Projectile_CannotDamageTwice_AfterDespawning()
        {
            var sim = SimWithPlayerAt(Vector2.zero);
            var p = sim.SpawnProjectile(Bolt(sim), AttackFaction.Hostile, new Vector2(0f, -1f), Vector2.up);
            for (int i = 0; i < 20; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.IsFalse(p.Active);

            // Even with invulnerability gone and time passing, the dead shot does nothing more.
            sim.Player.ClearInvulnerability();
            for (int i = 0; i < 60; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void HostileShots_IgnoreEnemies_AndHurtThePlayer()
        {
            var sim = SimWithPlayerAt(new Vector2(0f, -5f));
            var enemy = ActiveEnemy(sim, new Vector2(0f, -2f));
            sim.SpawnProjectile(Bolt(sim), AttackFaction.Hostile, new Vector2(0f, 0f), Vector2.down);
            for (int i = 0; i < 60; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(enemy.MaxHealth, enemy.Health, "hostile fire passes through enemies");
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void ReturnedShots_IgnoreThePlayer_AndHurtEnemies()
        {
            var sim = SimWithPlayerAt(new Vector2(0f, -2f));
            var enemy = ActiveEnemy(sim, new Vector2(0f, -5f));
            sim.SpawnProjectile(Bolt(sim), AttackFaction.Returned, new Vector2(0f, 0f), Vector2.down, rootReleaseId: 77);
            DamageEvent seen = default;
            sim.Events.EnemyDamaged += (e, d) => seen = d;
            for (int i = 0; i < 60; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(0, sim.Score.DamageTaken, "returned fire passes through the player");
            Assert.AreEqual(enemy.MaxHealth - 1f, enemy.Health, 1e-4f);
            Assert.AreEqual(999, seen.SourceActorId, "provenance: original shooter survives the return");
            Assert.AreEqual(77, seen.RootReleaseId);
        }

        [Test]
        public void PooledProjectile_IsFullyResetOnReuse()
        {
            var sim = SimWithPlayerAt(Vector2.zero);
            var a = sim.SpawnProjectile(Bolt(sim), AttackFaction.Returned, new Vector2(5f, 5f), Vector2.right,
                rootReleaseId: 12, isEcho: true, power: 2f, pierce: 3);
            a.HitActors.Add(4242);
            int oldId = a.ProjectileId;
            sim.ClearProjectiles();
            Assert.AreEqual(1, sim.ProjectilePool.FreeCount);

            var b = sim.SpawnProjectile(Bolt(sim), AttackFaction.Hostile, new Vector2(5f, 5f), Vector2.left);
            Assert.AreSame(a, b, "the pool reused the object");
            Assert.AreNotEqual(oldId, b.ProjectileId);
            Assert.AreEqual(0, b.HitActors.Count, "no stale piercing targets");
            Assert.AreEqual(0, b.RootReleaseId);
            Assert.IsFalse(b.IsEcho);
            Assert.AreEqual(1f, b.PowerMultiplier);
            Assert.AreEqual(0, b.PierceRemaining);
            Assert.AreEqual(AttackFaction.Hostile, b.Faction);
        }

        [Test]
        public void PiercingShot_HitsEachEnemyOnce()
        {
            var sim = SimWithPlayerAt(new Vector2(0f, -6f));
            var e1 = ActiveEnemy(sim, new Vector2(-3f, 0f));
            var e2 = ActiveEnemy(sim, new Vector2(-1f, 0f));
            sim.SpawnProjectile(Bolt(sim), AttackFaction.Returned, new Vector2(-5f, 0f), Vector2.right, pierce: 1);
            for (int i = 0; i < 60; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(e1.MaxHealth - 1f, e1.Health, 1e-4f);
            Assert.AreEqual(e2.MaxHealth - 1f, e2.Health, 1e-4f);
        }

        [Test]
        public void ShotsExpireAfterTheirLifetime()
        {
            var sim = SimWithPlayerAt(new Vector2(0f, -6f));
            var s = Bolt(sim, 0.5f);
            s.Lifetime = 0.5f;
            sim.SpawnProjectile(s, AttackFaction.Hostile, new Vector2(8f, 6f), Vector2.left);
            for (int i = 0; i < 29; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(1, sim.Projectiles.Count);
            for (int i = 0; i < 3; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(0, sim.Projectiles.Count);
        }

        [Test]
        public void Acolyte_WarnsThenTelegraphsThenFiresThreeBolts()
        {
            var sim = SimWithPlayerAt(new Vector2(0f, -5f));
            var e = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(0f, 2f));
            int telegraphs = 0, fired = 0;
            sim.Events.EnemyTelegraph += _ => telegraphs++;
            sim.Events.EnemyFired += _ => fired++;
            var t = sim.Config.combat;

            // Spawn warning: harmless and immune.
            sim.SpawnProjectile(Bolt(sim), AttackFaction.Returned, new Vector2(0f, 0.5f), Vector2.up);
            sim.Tick(Idle, Dt);
            Assert.AreEqual(e.MaxHealth, e.Health, "an enemy in its spawn warning cannot be hurt");

            int ticksToFire = Mathf.CeilToInt((t.spawnWarning + t.acolyte.firstShotDelay + t.acolyte.telegraph) / Dt) + 2;
            for (int i = 0; i < ticksToFire; i++) sim.Tick(Idle, Dt);
            Assert.AreEqual(1, telegraphs);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(3, sim.CountProjectiles(AttackFaction.Hostile), "three-bolt volley");
        }

        [Test]
        public void Death_CancelsPendingWorkAndClearsShots()
        {
            var sim = SimWithPlayerAt(Vector2.zero);
            bool ran = false;
            sim.Scheduler.Schedule(sim.Clock.Now + 0.5, () => ran = true);
            sim.SpawnProjectile(Bolt(sim), AttackFaction.Hostile, new Vector2(8f, 6f), Vector2.left);
            LeaveOneSecond(sim);
            sim.DamagePlayer(1, 0);
            for (int i = 0; i < 60; i++) sim.Tick(Idle, Dt);
            Assert.IsFalse(ran);
            Assert.AreEqual(0, sim.Projectiles.Count);
        }
    }
}
