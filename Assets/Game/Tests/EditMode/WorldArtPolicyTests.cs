using System;
using BorrowedHex.Enemies;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    // Reflection lets the initial RED run report the missing presentation contract without
    // breaking Unity's compilation (and thereby disconnecting the live editor).
    public class WorldArtPolicyTests
    {
        static object Call(string method, params object[] args)
        {
            var type = typeof(CollectorBoss).Assembly.GetType("BorrowedHex.Presentation.WorldArt.WorldArtPolicy");
            Assert.That(type, Is.Not.Null, "World art must have a deterministic, simulation-time animation policy.");
            return type.GetMethod(method).Invoke(null, args);
        }

        [Test]
        public void FramesLoopForIdleButHoldFinalFrameForOneShots()
        {
            Assert.That(Call("Frame", 1.1, 8, 10f, true), Is.EqualTo(3));
            Assert.That(Call("Frame", 1.1, 8, 10f, false), Is.EqualTo(7));
        }

        [Test]
        public void FrozenGameplayTimeProducesTheSameFrame()
        {
            var before = Call("Frame", 0.4, 13, 12f, false);
            Assert.That(Call("Frame", 0.4, 13, 12f, false), Is.EqualTo(before));
            Assert.That(Call("Frame", 0.6, 13, 12f, false), Is.Not.EqualTo(before));
        }

        [Test]
        public void InvalidOrEmptyAnimationInputsAreSafe()
        {
            Assert.That(Call("Frame", double.NaN, 0, 12f, true), Is.EqualTo(0));
            Assert.That(Call("Frame", -1.0, 8, 12f, true), Is.EqualTo(0));
        }

        [TestCase(BossStage.Reposition, BossPattern.Slam, "Run")]
        [TestCase(BossStage.Teleport, BossPattern.Sweep, "Attack3")]
        [TestCase(BossStage.Active, BossPattern.BoltStream, "Attack1")]
        [TestCase(BossStage.Active, BossPattern.FanVolley, "Attack3")]
        [TestCase(BossStage.Active, BossPattern.Sweep, "Attack2")]
        [TestCase(BossStage.Active, BossPattern.Slam, "Attack2")]
        [TestCase(BossStage.Recover, BossPattern.Slam, "Idle")]
        public void BossPatternsSelectAnExistingPose(BossStage stage, BossPattern pattern, string expected)
            => Assert.That(Call("Clip", stage, pattern), Is.EqualTo(expected));

        [Test]
        public void EncountersChangeSceneryAndBossesAlwaysUseSanctum()
        {
            Assert.That(Call("Theme", 0, false), Is.EqualTo("Courtyard"));
            Assert.That(Call("Theme", 1, false), Is.EqualTo("Graveyard"));
            Assert.That(Call("Theme", 2, false), Is.EqualTo("Cave"));
            Assert.That(Call("Theme", 3, false), Is.EqualTo("Courtyard"));
            Assert.That(Call("Theme", 1, true), Is.EqualTo("Sanctum"));
        }

        [Test]
        public void MissingLocalArtReturnsSafeEmptyAnimations()
        {
            using (var art = new Presentation.WorldArt.WorldArtLibrary("MissingArtForTest"))
            {
                Assert.That(art.HasBoss, Is.False);
                Assert.That(art.Boss("Idle"), Is.Empty);
                Assert.That(art.Effect("Torch"), Is.Empty);
                Assert.That(art.Prop("Tomb"), Is.Null);
            }
        }

        [Test]
        public void ImportedAtlasRetainsItsGridAndAllSevenAnimations()
        {
            using (var art = new Presentation.WorldArt.WorldArtLibrary())
            {
                if (!art.HasBoss) Assert.Ignore("Local licensed art is optional in a public checkout.");
                Assert.That(art.Texture("Necromancer").width, Is.EqualTo(2720));
                Assert.That(art.Texture("Necromancer").height, Is.EqualTo(896));
                string[] clips = { "Idle", "Run", "Attack1", "Attack2", "Attack3", "Hurt", "Death" };
                int[] counts = { 8, 8, 13, 13, 17, 5, 10 };
                for (int i = 0; i < clips.Length; i++) Assert.That(art.Boss(clips[i]).Length, Is.EqualTo(counts[i]), clips[i]);
                Assert.That(art.Effect("Teleport").Length, Is.GreaterThan(0));
                Assert.That(art.Effect("Vortex", 100).Length, Is.GreaterThan(0));
            }
        }
    }
}
