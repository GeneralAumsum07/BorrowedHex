using System.Collections.Generic;
using BorrowedHex.Core;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Phase 0 checks: the gameplay clock, run identity, the delayed-action scheduler
    // and the swept XZ geometry that every later combat rule depends on.
    public class CoreTests
    {
        [Test]
        public void PausedClock_IgnoresAdvanceRequests()
        {
            var clock = new GameplayClock();
            clock.Advance(1f);
            clock.SetPaused(true);
            clock.Advance(5f);
            Assert.AreEqual(1.0, clock.Now, 1e-9);
            clock.SetPaused(false);
            clock.Advance(0.5f);
            Assert.AreEqual(1.5, clock.Now, 1e-9);
        }

        [Test]
        public void Clock_StaysPausedUntilEveryReasonIsCleared()
        {
            // Upgrade choice and focus loss can overlap; clearing one must not resume combat.
            var clock = new GameplayClock();
            clock.SetPauseReason(PauseReason.UpgradeChoice, true);
            clock.SetPauseReason(PauseReason.FocusLost, true);
            clock.SetPauseReason(PauseReason.FocusLost, false);
            clock.Advance(1f);
            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(0.0, clock.Now, 1e-9);
        }

        [Test]
        public void Clock_RejectsInvalidDeltas()
        {
            var clock = new GameplayClock();
            clock.Advance(float.NaN);
            clock.Advance(-1f);
            clock.Advance(float.PositiveInfinity);
            Assert.AreEqual(0.0, clock.Now, 1e-9);
        }

        [Test]
        public void RunIds_DifferOnRestart()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 50; i++) Assert.IsTrue(seen.Add(RunIdFactory.Create()));
        }

        [Test]
        public void Scheduler_RunsInDueOrder_AndCancelAllDropsPending()
        {
            var s = new GameplayScheduler();
            var log = new List<int>();
            s.Schedule(2.0, () => log.Add(2));
            s.Schedule(1.0, () => log.Add(1));
            s.Schedule(5.0, () => log.Add(5));
            s.RunDue(2.0);
            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
            s.CancelAll();
            s.RunDue(10.0);
            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
        }

        [Test]
        public void SeededRandom_IsReproducible()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            for (int i = 0; i < 20; i++) Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
        }

        [Test]
        public void SweepCircle_HitsTargetCrossedWithinOneStep()
        {
            // A fast shot that starts before the target and ends beyond it in one tick still hits.
            bool hit = Geometry2D.SweepCircleVsCircle(new Vector2(-5, 0), new Vector2(5, 0), 0.1f,
                Vector2.zero, 0.4f, out float t);
            Assert.IsTrue(hit);
            Assert.AreEqual(0.45f, t, 1e-4f);
        }

        [Test]
        public void SweepCircle_InitialOverlapReportsTimeZero()
        {
            bool hit = Geometry2D.SweepCircleVsCircle(new Vector2(0.1f, 0), new Vector2(3, 0), 0.1f,
                Vector2.zero, 0.4f, out float t);
            Assert.IsTrue(hit);
            Assert.AreEqual(0f, t, 1e-6f);
        }

        [Test]
        public void SweepBox_FindsWallBeforeTarget()
        {
            var wall = new Rect(1f, -1f, 0.5f, 2f);
            bool hit = Geometry2D.SweepCircleVsRect(new Vector2(0, 0), new Vector2(4, 0), 0.1f, wall, out float t);
            Assert.IsTrue(hit);
            Assert.AreEqual(0.225f, t, 1e-4f); // (1 - 0.1) / 4
        }

        [Test]
        public void Cone_RejectsPointsBehindApex()
        {
            Assert.IsTrue(Geometry2D.InCone(Vector2.zero, Vector2.right, 45f, 2.8f, new Vector2(2f, 0.5f)));
            Assert.IsFalse(Geometry2D.InCone(Vector2.zero, Vector2.right, 45f, 2.8f, new Vector2(-1f, 0f)));
            Assert.IsFalse(Geometry2D.InCone(Vector2.zero, Vector2.right, 45f, 2.8f, new Vector2(3f, 0f)));
        }
    }
}
