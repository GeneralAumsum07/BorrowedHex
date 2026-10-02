using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Fixed packet slots, early release (right mouse) and slot cycling (Q). D31-D34.
    public class SlotTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 AimEast = new Vector2(5f, 0f);
        static PlayerCommand Hold => PlayerCommand.Moving(Vector2.zero).WithAim(AimEast);

        static ArenaSim Sim()
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        static void Shot(ArenaSim sim, Vector2 from, string id = AttackIds.Bolt)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(id), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, Core.AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Hold, Dt);
        }

        /// <summary>Catch <paramref name="count"/> shots in ONE activation, then let recovery finish.</summary>
        static void CatchVolley(ArenaSim sim, int count, string id = AttackIds.Bolt)
        {
            for (int i = 0; i < count; i++) Shot(sim, new Vector2(2f, (i - (count - 1) * 0.5f) * 0.15f), id);
            sim.Tick(Hold.WithCatch(), Dt);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
        }

        [Test]
        public void OwnerExample_SlotOneKeepsItsVolley_LaterCatchesGoToSlotTwo()
        {
            // "Captures 2 of 3 bolts → slot 1 holds those 2 until released; anything caught in
            // the meantime, any kind, from anyone, goes to slot 2, never slot 1."
            var sim = Sim();
            CatchVolley(sim, 2);
            var first = sim.Packets.InSlot(0);
            Assert.IsNotNull(first);
            Assert.AreEqual(2, first.Payloads.Count);

            CatchVolley(sim, 1, AttackIds.Rocket);
            Assert.AreSame(first, sim.Packets.InSlot(0));
            Assert.AreEqual(2, first.Payloads.Count, "slot 1 is locked: nothing appended");
            Assert.IsNotNull(sim.Packets.InSlot(1));
            Assert.AreEqual(Core.AttackKind.Rocket, sim.Packets.InSlot(1).Payloads[0].Kind);
        }

        [Test]
        public void SlotsKeepTheirPosition_WhenTheOtherReleases()
        {
            var sim = Sim();
            CatchVolley(sim, 1);
            CatchVolley(sim, 1);
            var second = sim.Packets.InSlot(1);
            sim.Tick(Hold.WithRelease(), Dt); // selected slot 0 fires
            Assert.IsNull(sim.Packets.InSlot(0));
            Assert.AreSame(second, sim.Packets.InSlot(1), "slot 2 does not slide into slot 1");
            // The freed slot 1 takes the next catch.
            CatchVolley(sim, 1);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            Assert.AreSame(second, sim.Packets.InSlot(1));
        }

        [Test]
        public void RightClick_ReleasesTheSelectedSlotNow_AlongTheAim()
        {
            var sim = Sim();
            CatchVolley(sim, 2);
            var released = new List<CapturedPacket>();
            sim.Events.PacketReleased += (p, _) => released.Add(p);
            int before = sim.Projectiles.Count;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released.Count);
            Assert.AreEqual(0, sim.Packets.Packets.Count, "slot freed immediately");
            int returned = 0;
            foreach (var p in sim.Projectiles)
                if (p.Active && p.Faction == Core.AttackFaction.Returned) { returned++; Assert.Greater(p.Velocity.x, 0f); }
            Assert.AreEqual(2, returned);
            Assert.AreEqual(before + 2, sim.Projectiles.Count);
        }

        [Test]
        public void EarlyReleasedPacket_DoesNotFireAgainAtItsExpiry()
        {
            var sim = Sim();
            CatchVolley(sim, 1);
            int releases = 0;
            sim.Events.PacketReleased += (_, __) => releases++;
            sim.Tick(Hold.WithRelease(), Dt);
            Run(sim, Mathf.CeilToInt(sim.Stats.PacketLifetime / Dt) + 10);
            Assert.AreEqual(1, releases);
        }

        [Test]
        public void Cycle_SelectsSlotTwo_SoRightClickFiresItFirst()
        {
            var sim = Sim();
            CatchVolley(sim, 1);
            CatchVolley(sim, 1, AttackIds.Rocket);
            var first = sim.Packets.InSlot(0);
            var second = sim.Packets.InSlot(1);
            CapturedPacket fired = null;
            sim.Events.PacketReleased += (p, _) => fired = p;
            sim.Tick(Hold.WithCycle(), Dt);
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreSame(second, fired);
            Assert.AreSame(first, sim.Packets.InSlot(0), "unselected slot untouched");
            sim.Tick(Hold.WithCycle(), Dt);
            Assert.AreEqual(0, sim.Packets.SelectedSlot, "cycling wraps around");
        }

        [Test]
        public void RightClick_OnAnEmptySelectedSlot_FiresTheOtherOne()
        {
            // Never a dead button: with the selection on an empty slot, release the one that is full.
            var sim = Sim();
            CatchVolley(sim, 1);
            CatchVolley(sim, 1);
            sim.Tick(Hold.WithRelease(), Dt);          // slot 0 fires, selection stays on 0
            var remaining = sim.Packets.InSlot(1);
            CapturedPacket fired = null;
            sim.Events.PacketReleased += (p, _) => fired = p;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreSame(remaining, fired);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
        }

        [Test]
        public void RightClick_WithNothingStored_DoesNothing()
        {
            var sim = Sim();
            int releases = 0;
            sim.Events.PacketReleased += (_, __) => releases++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, releases);
            Assert.AreEqual(0, sim.Projectiles.Count);
        }

        [Test]
        public void ReleasingDuringItsOwnWindow_LaterCatchesInThatWindowStartAFreshSlot()
        {
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);              // caught into slot 0, window still open
            var first = sim.Packets.InSlot(0);
            Assert.IsNotNull(first);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
            sim.Tick(Hold.WithRelease(), Dt);            // fired early, inside the window
            Assert.AreEqual(PacketStatus.Released, first.Status);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold, Dt);                          // same window: caught again
            var next = sim.Packets.InSlot(0);
            Assert.IsNotNull(next, "window still catches after an early release");
            Assert.AreNotSame(first, next, "never appended to a released packet");
            Assert.AreEqual(1, next.Payloads.Count);
        }

        [Test]
        public void ReleaseFreesASlotForACatchOnTheSameTick()
        {
            // Both slots full; release + catch pressed together: the catch uses the freed slot.
            var sim = Sim();
            CatchVolley(sim, 1);
            CatchVolley(sim, 1);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithRelease().WithCatch(), Dt);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
            Assert.AreEqual(2, sim.Packets.Packets.Count);
            Assert.IsNotNull(sim.Packets.InSlot(0));
        }

        [Test]
        public void CommandBuilders_DoNotMutateTheReceiver()
        {
            var cmd = PlayerCommand.Moving(Vector2.zero);
            var withAll = cmd.WithCatch().WithRelease().WithCycle().WithDash().WithAim(Vector2.one);
            Assert.IsTrue(withAll.Catch && withAll.Release && withAll.CycleSlot && withAll.Dash && withAll.HasAim);
            Assert.IsFalse(cmd.Catch || cmd.Release || cmd.CycleSlot || cmd.Dash || cmd.HasAim,
                "a stored command must not become a held button");
        }

        [Test]
        public void DeadPlayer_CannotRelease()
        {
            var sim = Sim();
            CatchVolley(sim, 1);
            int releases = 0;
            sim.Events.PacketReleased += (_, __) => releases++;
            sim.Player.InvulnerableUntil = double.NegativeInfinity;
            for (int i = 0; i < sim.Stats.MaxHealth; i++)
            {
                sim.Player.InvulnerableUntil = double.NegativeInfinity;
                sim.DamagePlayer(1, 0);
            }
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, releases);
        }
    }
}
