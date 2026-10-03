using BorrowedHex.Core;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Main-menu cheats (owner request, 3 Oct 2026): invincibility and an unlocked skill tree.
    /// Both are session-only flags, so every test resets them; a leaked flag would make an
    /// unrelated test invincible.
    /// </summary>
    public class CheatTests
    {
        const float Dt = 1f / 60f;

        [SetUp, TearDown]
        public void Reset()
        {
            Cheats.Invincible = false;
            Cheats.UnlockAllNodes = false;
        }

        static ArenaSim ShortRun(bool invincible)
        {
            var setup = new RunSetup { Mode = GameMode.Short, Seed = 1 };
            setup.Invincible = invincible;
            return new ArenaSim(TestSims.Config, setup);
        }

        [Test]
        public void Invincible_AHitLandsButCostsNoLife()
        {
            var sim = ShortRun(true);
            float before = sim.LifeSeconds;
            Assert.IsTrue(sim.DamagePlayer(20, 0), "the hit still lands (flash, invulnerability window)");
            Assert.AreEqual(before, sim.LifeSeconds);
            Assert.IsTrue(sim.Player.Alive);
        }

        [Test]
        public void Invincible_TheLifeClockDoesNotTick()
        {
            var sim = ShortRun(true);
            float before = sim.LifeSeconds;
            for (int i = 0; i < 120; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            Assert.AreEqual(before, sim.LifeSeconds);
        }

        [Test]
        public void NotInvincible_ByDefault_HitsAndTimeStillCost()
        {
            // Guards the cheat against leaking into ordinary runs.
            var sim = ShortRun(false);
            float before = sim.LifeSeconds;
            sim.DamagePlayer(20, 0);
            Assert.AreEqual(before - 20f, sim.LifeSeconds, 1e-3);
        }

        [Test]
        public void ApplyTo_MarksACheatedRunDebug_SoItAwardsNothing()
        {
            var plain = new RunSetup();
            Cheats.ApplyTo(plain);
            Assert.IsFalse(plain.Debug);
            Assert.IsFalse(plain.Invincible);

            Cheats.Invincible = true;
            var cheated = new RunSetup();
            Cheats.ApplyTo(cheated);
            Assert.IsTrue(cheated.Invincible);
            Assert.IsTrue(cheated.Debug, "a cheated run must never reach XP, records or achievements");

            Cheats.Invincible = false;
            Cheats.UnlockAllNodes = true;
            var unlocked = new RunSetup();
            Cheats.ApplyTo(unlocked);
            Assert.IsFalse(unlocked.Invincible);
            Assert.IsTrue(unlocked.Debug, "unearned passives also make the run debug");
        }

        [Test]
        public void UnlockAll_AFreshProfileCanEquipATierThreeNode()
        {
            var p = PlayerProfile.CreateDefault();
            Assert.IsNotNull(SkillTree.WhyCannotEquip(p, SkillTree.QuickDraw), "locked without the cheat");
            Cheats.UnlockAllNodes = true;
            Assert.IsTrue(SkillTree.IsOwned(p, SkillTree.QuickDraw));
            Assert.IsTrue(SkillTree.TryEquip(p, SkillTree.QuickDraw, out var why), why);
            // The equip cap is a game rule, not a lock: it still holds.
            Assert.IsTrue(SkillTree.TryEquip(p, SkillTree.MobilitySpeed, out _));
            Assert.IsTrue(SkillTree.TryEquip(p, SkillTree.ResilienceTime, out _));
            Assert.IsFalse(SkillTree.TryEquip(p, SkillTree.PrecisionAngle, out _));
        }

        [Test]
        public void UnlockAll_NeverWritesUnearnedNodesIntoTheSave()
        {
            // The strict validator (D73) rejects "equipped but not owned"; a cheat-equipped node
            // written to disk would make the next launch throw the whole save away.
            var store = new MemoryProfileStorage();
            var service = ProfileService.Load(store);
            Cheats.UnlockAllNodes = true;
            SkillTree.TryEquip(service.Profile, SkillTree.QuickDraw, out _);
            Assert.IsTrue(service.Save());

            Assert.IsTrue(ProfileService.TryParse(store.Snapshots[0], out var saved, out var why), why);
            CollectionAssert.DoesNotContain(saved.equippedNodes, SkillTree.QuickDraw);
            CollectionAssert.IsEmpty(saved.ownedNodes);
            // The session still has it equipped while the cheat is on.
            CollectionAssert.Contains(service.Profile.equippedNodes, SkillTree.QuickDraw);
        }

        [Test]
        public void UnlockAll_TurningItOff_UnequipsWhatWasNeverEarned()
        {
            var p = PlayerProfile.CreateDefault();
            Cheats.SetUnlockAllNodes(true, p);
            SkillTree.TryEquip(p, SkillTree.QuickDraw, out _);
            Cheats.SetUnlockAllNodes(false, p);
            Assert.IsFalse(Cheats.UnlockAllNodes);
            CollectionAssert.IsEmpty(p.equippedNodes);
        }
    }
}
