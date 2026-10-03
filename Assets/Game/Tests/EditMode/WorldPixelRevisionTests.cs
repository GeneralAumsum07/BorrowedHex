using System;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class WorldPixelRevisionTests
    {
        [Test]
        public void CameraLooksDownAtThirtyDegreesFromTwentyUnits()
        {
            // The owner's chosen pitch. At this angle the north wall, not the pitch, hides the
            // void; the PlayMode NoVoidVisible test guards that.
            Assert.That(WorldCameraPolicy.Rotation().eulerAngles.x, Is.EqualTo(30).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.magnitude, Is.EqualTo(20).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.y, Is.EqualTo(10f).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.z, Is.EqualTo(-17.32f).Within(.01));
        }

        [Test]
        public void EveryOrdinaryStageHasTwoOrThreeDistinctCoverKinds()
        {
            for (int stage = 0; stage < 3; stage++)
                Assert.That(new System.Collections.Generic.HashSet<DecayPropKind>(WorldArenaLayouts.Create(stage).propKinds).Count,
                    Is.InRange(2, 3), "Each environment needs a varied cover vocabulary.");
        }

        [Test]
        public void SanctumTitleWaitsForAllEightPillarsAndTheDarkArrival()
        {
            var type = typeof(ArenaSim).Assembly.GetType("BorrowedHex.Presentation.WorldArt.WorldIntroPolicy");
            Assert.That(type, Is.Not.Null);
            var count = type.GetMethod("LitPillars"); var title = type.GetMethod("ShowTitle");
            Assert.That((int)count.Invoke(null, new object[] { 1f }), Is.Zero);
            Assert.That((bool)title.Invoke(null, new object[] { 1f }), Is.False);
            int previous = 0;
            for (float t = 0; t < 10; t += .1f)
            {
                int lit = (int)count.Invoke(null, new object[] { t });
                Assert.That(lit, Is.InRange(previous, Math.Min(8, previous + 1)));
                if ((bool)title.Invoke(null, new object[] { t })) Assert.That(lit, Is.EqualTo(8));
                previous = lit;
            }
        }

        [Test]
        public void EveryCollectorPoseKeepsItsLowestVisiblePixelAboveTheFloor()
        {
            using (var library = new WorldArtLibrary())
            {
                if (!library.HasBoss) Assert.Ignore("Owner's local Collector sheet is optional.");
                foreach (string clip in new[] { "Idle", "Run", "Attack1", "Attack2", "Attack3", "Hurt", "Death" })
                    foreach (var sprite in library.Boss(clip))
                    {
                        var texture = sprite.texture; var pixels = texture.GetPixels32(); var rect = sprite.rect;
                        int bottom = (int)rect.yMax;
                        for (int y = (int)rect.y; y < rect.yMax; y++)
                            for (int x = (int)rect.x; x < rect.xMax; x++)
                                if (pixels[y * texture.width + x].a > 8) bottom = Math.Min(bottom, y);
                        Assert.That(bottom - rect.y - sprite.pivot.y, Is.GreaterThanOrEqualTo(-.01f), clip);
                    }
            }
        }
    }
}
