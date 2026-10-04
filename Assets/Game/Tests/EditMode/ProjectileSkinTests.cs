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
        [TestCase(ActorCategory.Boss, "Homing Orb")]
        [TestCase(ActorCategory.Player, "Arcane Orb")]
        [TestCase(ActorCategory.Pursuer, "Arcane Orb")]
        public void ShapeFollowsTheSchool(ActorCategory school, string sheet)
        {
            Assert.AreEqual(sheet, ProjectileSkins.Sheet(school, AttackKind.Bolt));
            CollectionAssert.Contains(FeedbackAssetTests.Sheets, sheet);
        }

        [Test]
        public void ARiposteIsAlwaysTheSlash() =>
            Assert.AreEqual("Dark Slash", ProjectileSkins.Sheet(ActorCategory.Acolyte, AttackKind.Riposte));

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
        public void TheHaloIsTranslucentAndKeepsTheStateHue()
        {
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
