using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Owner-approved Phase 6 boundaries; literals come from the brief, not the sim.</summary>
    public class BorrowedTimeTests
    {
        static PlayerCommand Still => PlayerCommand.Moving(Vector2.zero);

        static CapturedPacket Packet(ArenaSim sim, string attack = AttackIds.Bolt)
        {
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(attack), 42, sim.Ids.Next(), 0));
            p.CapacityUsed = p.Payloads[0].EnergyCost;
            return p;
        }

        [Test]
        public void CatchFillsSelectedEmptySlotFirst()
        {
            var store = new PacketStore(2);
            store.CycleSelection();
            var p = store.Create(1, 1, 0, 3, 12);
            Assert.AreEqual(1, p.Slot);
            Assert.AreEqual(1, store.SelectedSlot);
        }

        [Test]
        public void FrozenPacketResumesAndExpiresAtSeven()
        {
            var store = new PacketStore(2);
            var p = TestSims.Seed(store, 1, 1, 1, 3, 12);
            Assert.AreEqual(0, store.Advance(2).Count);
            store.CycleSelection();
            Assert.AreEqual(0, store.Advance(5).Count, "frozen at two seconds remaining");
            Assert.AreEqual(2f, p.Remaining(5), 1e-6f);
            store.CycleSelection();
            Assert.AreEqual(0, store.Advance(6.99).Count);
            Assert.AreEqual(1, store.Advance(7).Count);
            Assert.AreEqual(0, store.Advance(8).Count, "expiry happens once");
        }

        // D93: power is no longer linear (2 s used to be exactly 1.7), so the expectation comes
        // from the curve itself; what this test pins is that only the DECAYED time is used.
        [TestCase(0f)]
        [TestCase(2f)]
        public void FiredPowerUsesOnlyDecayedTime(float decay)
        {
            var sim = TestSims.Sandbox();
            float expected = sim.Stats.Power.Evaluate(decay, 3f);
            Packet(sim);
            if (decay > 0) sim.Tick(Still, decay);
            float actual = 0;
            sim.Events.ProjectileSpawned += p => { if (p.Faction == AttackFaction.Returned) actual = p.PowerMultiplier; };
            sim.Tick(Still.WithRelease(), 0.000001f);
            Assert.AreEqual(expected, actual, 0.00001f);
        }

        [Test]
        public void UnselectedPacketNeverBackfiresOrGainsPower()
        {
            var sim = TestSims.Sandbox();
            sim.Packets.CycleSelection();
            Packet(sim);
            sim.Packets.CycleSelection();
            sim.Tick(Still, 10f);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            float power = 0;
            sim.Events.ProjectileSpawned += p => power = p.PowerMultiplier;
            sim.Tick(Still.WithCycle().WithRelease(), 0.001f);
            Assert.AreEqual(1f, power, 1e-6f, "this tick decays the previously selected empty slot");
        }

        [TestCase(2.99f, 1, 0)]
        [TestCase(3f, 0, 10)]
        public void ExpiryBackfiresBeforeFireEvenDuringInvulnerability(float decay, int releases, int damage)
        {
            var sim = TestSims.Sandbox();
            Packet(sim);
            sim.Player.InvulnerableUntil = 100;
            int fired = 0;
            sim.Events.PacketReleased += (_, __) => fired++;
            sim.Tick(Still.WithRelease(), decay);
            Assert.AreEqual(releases, fired);
            Assert.AreEqual(damage, sim.Score.DamageTaken);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
            Assert.AreEqual(0, sim.Packets.SelectedSlot);
        }

        [Test]
        public void OrdinaryHitCostsTenSecondsAndPostHitInvulnerabilityBlocksTheNext()
        {
            var sim = P5.Short();
            sim.Tick(Still, 2);
            Assert.IsTrue(sim.DamagePlayer(10, 123));
            // Derived from the cap so retuning the life budget (D65) does not break the test.
            Assert.AreEqual(sim.Stats.StartingSeconds - 12f, sim.SecondsLeftInRun(), 1e-5f);
            Assert.IsFalse(sim.DamagePlayer(10, 123));
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void KillAddsTimeButCapsAtTheLifeCap()
        {
            var sim = P5.Short();
            sim.Tick(Still, 2);
            var e = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(8, 0));
            e.ActiveAt = 0;
            P5.Kill(sim, e);
            Assert.AreEqual(180f, sim.SecondsLeftInRun(), 1e-5f);
        }

        [TestCase(true, RunEndReason.Death)]
        [TestCase(false, RunEndReason.TimeExpired)]
        public void LastClockChangeDeterminesLossReason(bool hit, RunEndReason reason)
        {
            var sim = P5.Short();
            P5.Invulnerable(sim);
            // Stop 8 s short of the cap (a 10 s hit then kills) or 0.01 s short (time runs out).
            float life = sim.Stats.StartingSeconds;
            sim.Tick(Still, hit ? life - 8f : life - 0.01f);
            sim.ClearArena();
            sim.Player.ClearInvulnerability();
            if (hit) sim.DamagePlayer(10, 42);
            sim.Tick(Still, 0.02f);
            Assert.AreEqual(reason, sim.Summary.Reason);
            Assert.AreEqual(0f, sim.SecondsLeftInRun());
            Assert.AreEqual(0, sim.Projectiles.Count);
        }
    }
}
