using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The 3 Oct 2026 rework (Docs/REWORK_PLAN.md): hand rule, priming, school resistance,
    /// Overcharge, kill chains, keeping an upgrade. One fixture so the rework's rules are
    /// readable in one place.
    /// </summary>
    public class ReworkTests
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
            sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Hold, Dt);
        }

        /// <summary>Catch one bolt, then let the catch recovery finish so the next press is live.</summary>
        static void CatchOne(ArenaSim sim)
        {
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
        }

        // ---- Rule A: the hand rule (D89) -----------------------------------------------------

        [Test]
        public void HandRule_FullHand_RejectsTheCatch_EvenWithAnEmptyPocket()
        {
            var sim = Sim();
            CatchOne(sim);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
            Assert.IsNull(sim.Packets.InSlot(1), "no automatic banking into the other slot");
        }

        [Test]
        public void HandRule_QPocketsTheHex_ThenTheFreeHandCatches()
        {
            var sim = Sim();
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
            CatchOne(sim);
            Assert.AreSame(first, sim.Packets.InSlot(0), "the pocketed hex is untouched");
            Assert.IsNotNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void HandRule_CycleAndCatchOnOneTick_CatchLandsInTheNewHand()
        {
            // Review Focus 1: cycle runs before catch in TickPlayer, so Q + click together is
            // "pocket, then catch" — the most natural panic input must not be punished.
            var sim = Sim();
            CatchOne(sim);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCycle().WithCatch(), Dt);
            Assert.IsNotNull(sim.Packets.InSlot(1));
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
        }

        [Test]
        public void HandRule_BothSlotsFull_StillReportsSlotsFull()
        {
            var sim = Sim();
            CatchOne(sim);
            TestSims.Pocket(sim);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.SlotsFull, got);
        }

        [Test]
        public void HandRule_ParryStillWorks_WithAFullHand()
        {
            // A parry makes no hex, so a full hand must not stop the catch window opening.
            var sim = Sim();
            CatchOne(sim);
            Assert.IsTrue(sim.Capture.IsReady(sim.Clock.Now));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
        }

        [Test]
        public void Store_Create_UsesOnlyTheSelectedSlot()
        {
            var store = new PacketStore(2);
            Assert.IsNotNull(store.Create(1, 1, 0, 3f, 12));
            Assert.IsNull(store.Create(2, 2, 0, 3f, 12), "selected slot occupied: no fallback");
            store.CycleSelection();
            Assert.AreEqual(1, store.Create(3, 3, 0, 3f, 12).Slot);
        }

        [Test]
        public void Overflow_TriggersOnAFullHand_WithThePocketEmpty()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            CapturedPacket released = null;
            sim.Events.PacketReleased += (p, _) => released = p;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            Assert.AreNotSame(first, sim.Packets.InSlot(0));
            Assert.IsNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void Fusion_NeedsBothSlotsFull_AFullHandAloneIsRejected()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Fusion);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
        }

        // ---- Rule B: priming (D90) ------------------------------------------------------------

        [Test]
        public void Priming_UnprimedRelease_IsRefusedOnce_ThenFiresWhenPrimed()
        {
            // Review Focus 2: the refused press keeps the hex, raises ONE refusal, fires nothing.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            Assert.IsNotNull(p);
            int refused = 0, released = 0;
            sim.Events.ReleaseRefused += _ => refused++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, refused);
            Assert.AreEqual(0, released);
            Assert.AreSame(p, sim.Packets.InSlot(0));
            // 0.4 s after capture it fires.
            while (sim.Clock.Now - p.CapturedAt < 0.4 - 1e-6) sim.Tick(Hold, Dt);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
            Assert.AreEqual(1, refused, "no refusal for the primed press");
        }

        [Test]
        public void Priming_ContinuesWhilePocketed()
        {
            // R1: priming is time since capture, so the juggle (catch, pocket, fire the other,
            // swap back, fire) never pays the 0.4 s twice.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Run(sim, 30);   // 0.5 s pocketed: DecayedTime barely moved
            Assert.Less(p.DecayedTime, 0.1);
            Assert.IsTrue(sim.IsPrimed(p));
            TestSims.Pocket(sim);   // back to slot 0
            int released = 0;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
        }

        [Test]
        public void Priming_OverflowForcedRelease_IgnoresIt()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var first = sim.Packets.InSlot(0);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
            // Still inside 0.4 s? Recovery is 0.65 s, so force freshness for the check:
            first.CapturedAt = sim.Clock.Now;
            Assert.IsFalse(sim.IsPrimed(first));
            CapturedPacket released = null;
            sim.Events.PacketReleased += (pk, _) => released = pk;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released, "Overflow fires an unprimed hex (D91)");
        }
    }
}
