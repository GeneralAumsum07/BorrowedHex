using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // VFX pass (spec 2026-10-04, section 2 "Sim changes"): the presentation needs three moments
    // the sim already decides but never announced, and the school of the shot behind each hit.
    // These signals are additive: no rule changes, so every other suite must stay green.
    public class FeedbackSignalTests
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

        static void Shot(ArenaSim sim, Vector2 from)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        // Catch one bolt, then wait out the catch recovery so the next press is live (ReworkTests).
        static void CatchOne(ArenaSim sim)
        {
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            int wait = Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2;
            for (int i = 0; i < wait; i++) sim.Tick(Hold, Dt);
        }

        [Test]
        public void OverflowFired_ComesOnlyFromOverflowsForcedRelease()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            int fired = 0;
            sim.Events.OverflowFired += _ => fired++;
            CatchOne(sim);
            Assert.AreEqual(0, fired, "a catch into a free hand is not Overflow");
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);   // full hand: Overflow fires the held hex
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OverflowFired_NeverOnAnOrdinaryRelease()
        {
            var sim = Sim();
            int fired = 0, released = 0;
            sim.Events.OverflowFired += _ => fired++;
            sim.Events.PacketReleased += (_, __) => released++;
            CatchOne(sim);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released, "the hex fired");
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void PartingGiftBurst_ComesOnceImmediatelyBeforeItsExplosion()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.PartingGift);
            var order = new List<string>();
            float radius = 0f;
            sim.Events.PartingGiftBurst += (_, r) => { order.Add("gift"); radius = r; };
            sim.Events.Explosion += (_, __, ___) => order.Add("explosion");
            CatchOne(sim);
            sim.Tick(Hold.WithRelease(), Dt);
            CollectionAssert.AreEqual(new[] { "gift", "explosion" }, order);
            Assert.Greater(radius, 0f, "the burst carries the gift's true radius");
        }

        // MasteryTests' Quick Draw rig: a hex in slot 1, swapped away and back, then fired
        // ticksAfterSwap ticks later. Returns how many QuickDrawFired events the release raised.
        static int QuickDrawsAfter(bool node, int ticksAfterSwap)
        {
            var setup = RunSetup.ForSandbox(1);
            setup.Stats = Loadout.Resolve(TestSims.Config, node ? new[] { SkillTree.QuickDraw } : new string[0]);
            var sim = new ArenaSim(TestSims.Config, setup);
            var aim = PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(8f, 0f));
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 42, sim.Ids.Next(), 0));
            p.Status = PacketStatus.Stored;
            sim.Tick(aim, Dt);
            sim.Tick(aim.WithCycle(), Dt);
            sim.Tick(aim.WithCycle(), Dt);   // the swap Quick Draw measures
            for (int i = 0; i < ticksAfterSwap; i++) sim.Tick(aim, Dt);
            int fired = 0, released = 0;
            sim.Events.QuickDrawFired += _ => fired++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(aim.WithRelease(), Dt);
            Assert.AreEqual(1, released, "the hex fired");
            return fired;
        }

        [Test]
        public void QuickDrawFired_OnlyInsideThePostSwapWindow()
        {
            Assert.AreEqual(1, QuickDrawsAfter(true, 3), "0.067 s after the swap");
            Assert.AreEqual(0, QuickDrawsAfter(true, 19), "past the 0.3 s window");
            Assert.AreEqual(0, QuickDrawsAfter(false, 3), "no node, no bonus, no signal");
        }

        [Test]
        public void DamageEvent_CarriesTheShotsSchool()
        {
            var sim = Sim();
            P4.Parked(sim, ActorCategory.Acolyte, new Vector2(4f, 0f));
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            s.SourceCategory = ActorCategory.ScatterCaster;
            sim.SpawnProjectile(s, AttackFaction.Returned, new Vector2(2f, 0f), Vector2.right);
            DamageEvent? got = null;
            sim.Events.EnemyDamaged += (_, d) => got = d;
            for (int i = 0; i < 60 && got == null; i++) sim.Tick(Hold, Dt);
            Assert.IsTrue(got.HasValue, "the returned bolt hit the acolyte");
            Assert.AreEqual(ActorCategory.ScatterCaster, got.Value.SourceCategory);
        }
    }
}
