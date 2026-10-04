using BorrowedHex.Core;
using BorrowedHex.Presentation.Feedback;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    // D2: shape follows the source school; colour follows the state (spec 3.1-3.3).
    public class ProjectileSkinTests
    {
        [TestCase(ActorCategory.Acolyte, "Magic Missile")]
        [TestCase(ActorCategory.ScatterCaster, "Ice Shard Shot")]
        [TestCase(ActorCategory.SiegeFamiliar, "Fireball Shot")]
        [TestCase(ActorCategory.Player, "Arcane Orb")]
        [TestCase(ActorCategory.Pursuer, "Arcane Orb")]
        public void ShapeFollowsTheSchool(ActorCategory school, string sheet)
        {
            Assert.AreEqual(sheet, ProjectileSkins.Sheet(school, AttackKind.Bolt, 0f));
            CollectionAssert.Contains(FeedbackAssetTests.Sheets, sheet);
        }

        // Playtest: the Collector's two long-range attacks must not look alike. Both fire the
        // Bolt definition, so the shape is told apart by the volley's spread: the stream fires
        // single bolts (spread 0), the fan fires a spread.
        [Test]
        public void TheCollectorsStreamAndFanLookDifferent()
        {
            Assert.AreEqual("Collector Plasma", ProjectileSkins.Sheet(ActorCategory.Boss, AttackKind.Bolt, 0f), "the bolt stream");
            Assert.AreEqual("Homing Orb", ProjectileSkins.Sheet(ActorCategory.Boss, AttackKind.Bolt, 20f), "the wide fan");
            CollectionAssert.Contains(FeedbackAssetTests.Sheets, "Collector Plasma");
        }

        // The Homing Orb fills more of its cell than the other sheets, so one global scale
        // made it overshoot its hitbox outline by about 30% (final review minor).
        [Test]
        public void TheOrbIsScaledToItsFill()
        {
            Assert.AreEqual(ProjectileSkins.SpriteScale, ProjectileSkins.SpriteScaleFor("Magic Missile"), 1e-6);
            Assert.Less(ProjectileSkins.SpriteScaleFor("Homing Orb"), ProjectileSkins.SpriteScale / 1.25f);
        }

        // Playtest: the shapes read a little small at 5.
        [Test]
        public void ShotSpritesAreSlightlyBiggerThanTheFirstPass() =>
            Assert.AreEqual(6f, ProjectileSkins.SpriteScale, 1e-6);

        [Test]
        public void ARiposteIsAlwaysTheSlash() =>
            Assert.AreEqual("Dark Slash", ProjectileSkins.Sheet(ActorCategory.Acolyte, AttackKind.Riposte, 0f));

        [Test]
        public void ColourFollowsTheStateInTheExistingPriority()
        {
            Assert.AreEqual(FeedbackColors.Overcharge, ProjectileSkins.State(AttackFaction.Returned, AttackKind.Rocket, true), "overcharge wins");
            Assert.AreEqual(FeedbackColors.Riposte, ProjectileSkins.State(AttackFaction.Returned, AttackKind.Riposte, false));
            Assert.AreEqual(FeedbackColors.Returned, ProjectileSkins.State(AttackFaction.Returned, AttackKind.Rocket, false), "a returned rocket is the player's");
            Assert.AreEqual(FeedbackColors.Rocket, ProjectileSkins.State(AttackFaction.Hostile, AttackKind.Rocket, false));
            Assert.AreEqual(FeedbackColors.Hostile, ProjectileSkins.State(AttackFaction.Hostile, AttackKind.Bolt, false));
        }

        [Test]
        public void TheHaloIsAFaintOutlineInTheStateHue()
        {
            // Playtest: a filled halo was too prominent. It is now a thin ring (ArenaView draws
            // PixelSprites.Disc(true)) just outside the true hitbox: faint, but still the
            // honest hitbox signal.
            Assert.That(ProjectileSkins.HaloAlpha, Is.InRange(0.3f, 0.6f));
            Assert.That(ProjectileSkins.HaloScale, Is.InRange(1f, 1.2f), "hugs the hitbox");
            var halo = ProjectileSkins.Halo(AttackFaction.Hostile, AttackKind.Bolt, false);
            Assert.AreEqual(ProjectileSkins.HaloAlpha, halo.a, 1e-6);
            Assert.AreEqual(FeedbackColors.Hostile.r, halo.r, 1e-6);
        }

        [Test]
        public void TrailsEchoesAndPuffs()
        {
            Assert.AreEqual(.12f, ProjectileSkins.TrailSeconds(AttackFaction.Returned), 1e-6);
            Assert.AreEqual(.08f, ProjectileSkins.TrailSeconds(AttackFaction.Hostile), 1e-6);
            Assert.AreEqual(.6f, ProjectileSkins.SpriteAlpha(true), 1e-6);
            Assert.AreEqual(1f, ProjectileSkins.SpriteAlpha(false), 1e-6);
            Assert.AreEqual("Fire Trail", ProjectileSkins.Puffs(AttackKind.Rocket, false));
            Assert.AreEqual("Spark Trail", ProjectileSkins.Puffs(AttackKind.Bolt, true));
            Assert.AreEqual("Spark Trail", ProjectileSkins.Puffs(AttackKind.Rocket, true), "the state wins over the kind");
            Assert.IsNull(ProjectileSkins.Puffs(AttackKind.Bolt, false));
        }
    }
}
