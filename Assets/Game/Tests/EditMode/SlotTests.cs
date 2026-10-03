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
        public void OwnerExample_SlotOneKeepsItsVolley_PocketedCatchesGoToSlotTwo()
        {
            // "Captures 2 of 3 bolts → slot 1 holds those 2 until released; anything caught in
            // the meantime, any kind, from anyone, goes to slot 2, never slot 1." Since the hand
            // rule (D89) the meantime catch needs Q first: the store no longer banks by itself.
            var sim = Sim();
            CatchVolley(sim, 2);
            var first = sim.Packets.InSlot(0);
            Assert.IsNotNull(first);
            Assert.AreEqual(2, first.Payloads.Count);

            TestSims.Pocket(sim);
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
            TestSims.Pocket(sim);              // D89: the second catch needs a free hand
            CatchVolley(sim, 1);
            var second = sim.Packets.InSlot(1);
            Assert.IsNotNull(second);
            TestSims.Pocket(sim);              // back to slot 0
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
            TestSims.Pocket(sim);              // D89: the second catch needs a free hand
            CatchVolley(sim, 1, AttackIds.Rocket);
            TestSims.Pocket(sim);              // back to slot 0, the state this test starts from
            var first = sim.Packets.InSlot(0);
            var second = sim.Packets.InSlot(1);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
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
        public void RightClick_OnAnEmptySelectedSlot_FiresNothing()
        {
            // Owner direction (D33 revised): the selection is binding. An empty selected slot
            // means right mouse does nothing, even while the other slot is full.
            var sim = Sim();
            CatchVolley(sim, 1);
            TestSims.Pocket(sim);                      // D89: the second catch needs a free hand
            CatchVolley(sim, 1);
            TestSims.Pocket(sim);                      // back to slot 0
            sim.Tick(Hold.WithRelease(), Dt);          // slot 0 fires, selection stays on 0
            var remaining = sim.Packets.InSlot(1);
            int releases = 0;
            sim.Events.PacketReleased += (_, __) => releases++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, releases);
            Assert.AreSame(remaining, sim.Packets.InSlot(1), "slot 2 keeps its packet");
            sim.Tick(Hold.WithCycle(), Dt);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, releases, "selecting slot 2 first is what fires it");
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
        public void ReleaseDuringItsOwnWindow_IsRefused_HexIsStillUnstable()
        {
            // Before D90 a hex could be fired inside its own catch window and the window then
            // started a fresh slot. The 0.4 s priming outlasts the 0.25 s window, so now the
            // press is simply refused and the hex stays put.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var first = sim.Packets.InSlot(0);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(PacketStatus.Collecting, first.Status, "0.4 s priming outlasts the 0.25 s window (D90)");
            Assert.AreSame(first, sim.Packets.InSlot(0));
        }

        [Test]
        public void ReleaseFreesASlotForACatchOnTheSameTick()
        {
            // Both slots full; release + catch pressed together: the catch uses the freed slot.
            var sim = Sim();
            CatchVolley(sim, 1);
            TestSims.Pocket(sim);                      // D89: the second catch needs a free hand
            CatchVolley(sim, 1);
            TestSims.Pocket(sim);                      // slot 0 selected and full again
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithRelease().WithCatch(), Dt);
            Assert.AreEqual(0, sim.Score.DamageTaken);
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
            for (int i = 0; i < 300; i++)
            {
                sim.Player.InvulnerableUntil = double.NegativeInfinity;
                sim.DamagePlayer(1, 0);
            }
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, releases);
        }
    }
}
