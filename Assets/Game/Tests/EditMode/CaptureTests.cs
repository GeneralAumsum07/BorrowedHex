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
    // Phase 3 boundary vectors (section 9) as exact-time unit tests of the rule objects.
    public class CaptureRuleTests
    {
        static PlayerStats Stats => PlayerStats.FromConfig(TestSims.Config);

        static AttackSnapshot Shot(string id, int shotId = 1) =>
            AttackSnapshot.From(new AttackCatalog(TestSims.Config.combat).Get(id), 50, shotId, 0f);

        [Test]
        public void PacketCapturedAt1_DoesNotReleaseAt3_99_AndReleasesOnceAt4()
        {
            var store = new PacketStore(2);
            store.Create(1, 1, 1.0, 3f, 12);
            Assert.AreEqual(0, store.Advance(3.99).Count);
            Assert.AreEqual(1, store.Advance(4.0).Count);
            Assert.AreEqual(0, store.Advance(4.0).Count, "released exactly once");
            Assert.AreEqual(2, store.FreeSlots, "slot freed immediately");
        }

        [Test]
        public void Expiry_ToleratesFloatingAccumulation()
        {
            // 240 steps of 1/60 accumulated in double land a hair under 4.0.
            double t = 0; for (int i = 0; i < 60; i++) t += 1f / 60f;
            var store = new PacketStore(2);
            store.Create(1, 1, t, 3f, 12);
            double later = t; for (int i = 0; i < 180; i++) later += 1f / 60f;
            Assert.AreEqual(1, store.Advance(later).Count);
        }

        [Test]
        public void AppendingAt1_20_KeepsExpiryAt4()
        {
            var c = new CaptureController();
            var store = new PacketStore(2);
            var ids = new IdGenerator();
            Assert.IsTrue(c.TryActivate(1.0, Stats));
            Assert.AreEqual(CaptureResult.CreatedPacket, c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Hostile, false, 1.0, store, Stats, ids));
            Assert.AreEqual(CaptureResult.Appended, c.TryCapture(Shot(AttackIds.Bolt, 2), AttackFaction.Hostile, false, 1.20, store, Stats, ids));
            var p = store.Packets[0];
            store.Advance(1.20);
            Assert.AreEqual(2.8f, p.Remaining(1.20), 1e-6f);
            Assert.AreEqual(2, p.Payloads.Count);
        }

        [Test]
        public void RocketCost4_DoesNotFitPacketWith10Of12Used()
        {
            var c = new CaptureController();
            var store = new PacketStore(2);
            var ids = new IdGenerator();
            c.TryActivate(1.0, Stats);
            for (int i = 0; i < 10; i++)
                c.TryCapture(Shot(AttackIds.Bolt, i + 1), AttackFaction.Hostile, false, 1.05, store, Stats, ids);
            Assert.AreEqual(10, store.Packets[0].CapacityUsed);
            Assert.AreEqual(CaptureResult.PacketFull, c.TryCapture(Shot(AttackIds.Rocket, 99), AttackFaction.Hostile, false, 1.1, store, Stats, ids));
            Assert.AreEqual(10, store.Packets[0].CapacityUsed, "a rejected shot is not partially stored");
            Assert.AreEqual(1, store.Packets.Count, "a full packet never spills into a second one");
        }

        [Test]
        public void EmptyActivation_CostsRecovery_ButNoSlot()
        {
            var c = new CaptureController();
            var store = new PacketStore(2);
            Assert.IsTrue(c.TryActivate(1.0, Stats));
            Assert.IsFalse(c.TryActivate(1.3, Stats), "recovery blocks a new attempt");
            Assert.IsTrue(c.TryActivate(1.0 + Stats.CaptureRecovery, Stats));
            Assert.AreEqual(2, store.FreeSlots);
        }

        [Test]
        public void ReturnedEchoAndNonCapturable_AreNotEligible()
        {
            var c = new CaptureController();
            var store = new PacketStore(2);
            var ids = new IdGenerator();
            c.TryActivate(1.0, Stats);
            Assert.AreEqual(CaptureResult.NotEligible, c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Returned, false, 1.0, store, Stats, ids));
            Assert.AreEqual(CaptureResult.NotEligible, c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Hostile, true, 1.0, store, Stats, ids));
            var s = Shot(AttackIds.Bolt); s.Capturable = false;
            Assert.AreEqual(CaptureResult.NotEligible, c.TryCapture(s, AttackFaction.Hostile, false, 1.0, store, Stats, ids));
        }

        [Test]
        public void BothSlotsFull_NewActivationCannotCreateAPacket()
        {
            var c = new CaptureController();
            var store = new PacketStore(2);
            var ids = new IdGenerator();
            c.TryActivate(0.0, Stats); c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Hostile, false, 0.0, store, Stats, ids);
            c.TryActivate(1.0, Stats); c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Hostile, false, 1.0, store, Stats, ids);
            c.TryActivate(2.0, Stats);
            Assert.AreEqual(CaptureResult.SlotsFull, c.TryCapture(Shot(AttackIds.Bolt), AttackFaction.Hostile, false, 2.0, store, Stats, ids));
        }

        [Test]
        public void CaptureRegion_RequiresApproachingShotInsideCone()
        {
            var player = Vector2.zero; var aim = Vector2.right;
            Assert.IsTrue(CaptureGeometry.InRegion(player, aim, 45f, 2.8f, new Vector2(2f, 0f), Vector2.left, 0.18f));
            Assert.IsFalse(CaptureGeometry.InRegion(player, aim, 45f, 2.8f, new Vector2(2f, 0f), Vector2.right, 0.18f), "moving away");
            Assert.IsFalse(CaptureGeometry.InRegion(player, aim, 45f, 2.8f, new Vector2(-2f, 0f), Vector2.right, 0.18f), "behind the player");
            Assert.IsFalse(CaptureGeometry.InRegion(player, aim, 45f, 2.8f, new Vector2(4f, 0f), Vector2.left, 0.18f), "out of range");
        }
    }

    // Phase 3 integration checks through the full tick pipeline.
    public class CaptureIntegrationTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 AimEast = new Vector2(5f, 0f);
        static PlayerCommand Hold => PlayerCommand.Moving(Vector2.zero).WithAim(AimEast);
        static PlayerCommand Catch => Hold.WithCatch();

        static ArenaSim Sim()
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        static ProjectileActor ShotFrom(ArenaSim sim, Vector2 from, float speed = -1f, string id = AttackIds.Bolt, int source = 999)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(id), source, sim.Ids.Next(), 0f);
            if (speed > 0) s.Speed = speed;
            return sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Hold, Dt);
        }

        [Test]
        public void SameTickCapture_PreventsThatShotsDamage()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(1.2f, 0f), speed: 240f); // reaches and passes the player this tick
            sim.Tick(Catch, Dt);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Assert.AreEqual(1, sim.Packets.Packets[0].Payloads.Count);
        }

        [Test]
        public void CaptureAndImpactAtTheSameInstant_CaptureWins()
        {
            // A shot inside the cone that is already touching the body: capture entry and
            // player impact are both at t=0 of the sweep. The tie must resolve as a capture.
            var sim = Sim();
            float touch = sim.Player.Radius + sim.Attacks.Get(AttackIds.Bolt).Radius;
            ShotFrom(sim, new Vector2(touch, 0f));
            sim.Tick(Catch, Dt);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
        }

        [Test]
        public void ImpactFromOutsideTheCone_DuringOpenWindow_StillHurts()
        {
            // Aim east, shot falls from the north: never inside the cone, so it is not catchable
            // even at body contact (section 2: "must be inside the active cone/range").
            var sim = Sim();
            ShotFrom(sim, new Vector2(0f, 1.5f));
            sim.Tick(Catch, Dt);
            Run(sim, 30);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
        }

        [Test]
        public void UncapturedShots_StillHurt()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0f));
            Run(sim, 30);
            Assert.AreEqual(10, sim.Score.DamageTaken);

            // Window open but the shot comes from behind the aim cone.
            var sim2 = Sim();
            ShotFrom(sim2, new Vector2(-2f, 0f));
            sim2.Tick(Catch, Dt);
            Run(sim2, 30);
            Assert.AreEqual(10, sim2.Score.DamageTaken);
            Assert.AreEqual(0, sim2.Packets.Packets.Count);
        }

        [Test]
        public void CapturedVolley_ReleasesOnce_AsReturnedShotsWithSourceKept()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0.1f), source: 4242);
            ShotFrom(sim, new Vector2(2f, -0.1f), source: 4242);
            sim.Tick(Catch, Dt);
            Run(sim, 10);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Assert.AreEqual(2, sim.Packets.Packets[0].Payloads.Count, "second shot appended in the same window");
            Assert.AreEqual(0, sim.Score.DamageTaken);

            int releases = 0, root = 0;
            sim.Events.PacketReleased += (_, r) => { releases++; root = r; };
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, releases);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
            int returned = 0;
            foreach (var p in sim.Projectiles)
                if (p.Faction == AttackFaction.Returned) { returned++; Assert.AreEqual(4242, p.Shot.SourceActorId); Assert.AreEqual(root, p.RootReleaseId); }
            Assert.AreEqual(2, returned);
        }

        [Test]
        public void ReleaseFollowsCurrentAim()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0f));
            sim.Tick(Catch, Dt);
            // Turn to face north before expiry: the volley goes north, not back east.
            var north = PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(0f, 5f));
            sim.Tick(north.WithRelease(), Dt);
            Assert.AreEqual(1, sim.Projectiles.Count);
            Assert.Greater(sim.Projectiles[0].Velocity.y, 0f);
            Assert.AreEqual(0f, sim.Projectiles[0].Velocity.x, 1e-3f);
        }

        [Test]
        public void SelectedSlotBackfires_OtherSlotStaysFrozen()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0f));
            sim.Tick(Catch, Dt);
            Run(sim, 60); // 1 s later, recovery is over
            ShotFrom(sim, new Vector2(2f, 0f));
            sim.Tick(Catch, Dt);
            Run(sim, 2);
            Assert.AreEqual(2, sim.Packets.Packets.Count);

            int releases = 0;
            sim.Events.PacketReleased += (_, __) => releases++;
            Run(sim, 120); // ~3.05 s after the first capture
            Assert.AreEqual(0, releases);
            Assert.AreEqual(1, sim.Score.Backfires);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Run(sim, 60);
            Assert.AreEqual(0, releases);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Assert.AreEqual(3f, sim.Packets.Packets[0].Remaining(sim.Clock.Now), 1e-6f);
        }

        [Test]
        public void Pause_DoesNotConsumePacketLifetime()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0f));
            sim.Tick(Catch, Dt);
            float remaining = sim.Packets.Packets[0].Remaining(sim.Clock.Now);
            sim.Clock.SetPauseReason(PauseReason.Menu, true);
            Run(sim, 600); // ten paused seconds
            sim.Clock.SetPauseReason(PauseReason.Menu, false);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Assert.AreEqual(remaining, sim.Packets.Packets[0].Remaining(sim.Clock.Now));
            Assert.Greater(sim.Packets.Packets[0].Remaining(sim.Clock.Now), 2.9f);
        }

        [Test]
        public void ReturnedShots_CannotBeRecaptured()
        {
            var sim = Sim();
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 1, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Returned, new Vector2(2f, 0f), Vector2.left);
            sim.Tick(Catch, Dt);
            Run(sim, 20);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
        }

        [Test]
        public void DeletingTheShooter_DoesNotBreakRelease()
        {
            var sim = Sim();
            var e = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(6f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = double.MaxValue;
            ShotFrom(sim, new Vector2(2f, 0f), source: e.ActorId);
            sim.Tick(Catch, Dt);
            sim.Enemies.Clear(); // the shooter is gone entirely
            Assert.DoesNotThrow(() => sim.Tick(Hold.WithRelease(), Dt));
            Assert.AreEqual(1, sim.CountProjectiles(AttackFaction.Returned));
            Assert.AreEqual(e.ActorId, sim.Projectiles[0].Shot.SourceActorId);
        }

        [Test]
        public void FullPacket_RejectedShotKeepsFlyingAndHurts()
        {
            var sim = Sim();
            // Fill the window's packet to 12/12 with a tight cluster, then send one more.
            for (int i = 0; i < 12; i++) ShotFrom(sim, new Vector2(2f, -0.3f + 0.05f * i));
            sim.Tick(Catch, Dt);
            Run(sim, 5);
            Assert.IsTrue(sim.Packets.Packets[0].IsFull);
            int damageBefore = sim.Score.DamageTaken;
            int rejected = 0;
            sim.Events.CaptureRejected += (_, r) => { if (r == CaptureResult.PacketFull) rejected++; };
            ShotFrom(sim, new Vector2(1.5f, 0f));
            Run(sim, 20);
            Assert.AreEqual(1, rejected);
            Assert.AreEqual(damageBefore + 10, sim.Score.DamageTaken);
        }

        [Test]
        public void Death_CancelsPacketsAndTheirReleases()
        {
            var sim = Sim();
            ShotFrom(sim, new Vector2(2f, 0f));
            sim.Tick(Catch, Dt);
            LeaveOneSecond(sim);
            sim.Player.ClearInvulnerability();
            sim.DamagePlayer(1, 0);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
            Run(sim, 300);
            Assert.AreEqual(0, sim.CountProjectiles(AttackFaction.Returned));
        }

        [Test]
        public void ExpiryBeforeCapture_FreedSlotIsUsableOnTheSameTick()
        {
            var sim = Sim();
            // Fill both slots.
            ShotFrom(sim, new Vector2(2f, 0f)); sim.Tick(Catch, Dt); Run(sim, 59);
            ShotFrom(sim, new Vector2(2f, 0f)); sim.Tick(Catch, Dt); Run(sim, 2);
            Assert.AreEqual(2, sim.Packets.Packets.Count);
            // Advance to the tick on which the first packet expires, and catch on that tick.
            double firstExpiry = sim.Clock.Now + sim.Packets.Packets[0].Remaining(sim.Clock.Now);
            while (sim.Clock.Now + Dt < firstExpiry - 1e-6) sim.Tick(Hold, Dt);
            ShotFrom(sim, new Vector2(0.9f, 0f), speed: 60f);
            sim.Tick(Catch, Dt);
            Assert.AreEqual(10, sim.Score.DamageTaken, "only the backfire cost time; the freed slot accepted the catch");
            Assert.AreEqual(2, sim.Packets.Packets.Count);
        }
    }
}
