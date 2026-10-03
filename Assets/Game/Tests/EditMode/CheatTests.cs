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
        public void UnlockAll_MakesEveryNodeActive_ForAFreshProfile()
        {
            // D101 removed the equip step, so "unlock all" now means every node is ACTIVE in the
            // next run, not merely equippable. The run and the Style preview read ActiveNodes.
            var p = PlayerProfile.CreateDefault();
            CollectionAssert.IsEmpty(SkillTree.ActiveNodes(p), "nothing is active without the cheat");
            Cheats.UnlockAllNodes = true;
            Assert.IsTrue(SkillTree.IsOwned(p, SkillTree.QuickDraw));
            Assert.AreEqual(SkillTree.Nodes.Count, SkillTree.ActiveNodes(p).Count);
            var s = Loadout.Resolve(TestSims.Config, SkillTree.ActiveNodes(p));
            Assert.Greater(s.LifePerDamage, 0f, "even the Blood Price nodes apply");
        }

        [Test]
        public void UnlockAll_NeverWritesUnearnedNodesIntoTheSave()
        {
            // The strict validator (D73) rejects nodes above the profile's mastery; a cheat node
            // written to disk would make the next launch throw the whole save away.
            var store = new MemoryProfileStorage();
            var service = ProfileService.Load(store);
            Cheats.UnlockAllNodes = true;
            Assert.IsTrue(service.Save());
            Assert.IsTrue(ProfileService.TryParse(store.Snapshots[0], out var saved, out var why), why);
            CollectionAssert.IsEmpty(saved.ownedNodes);
        }

        [Test]
        public void UnlockAll_TurningItOff_LeavesOnlyWhatWasBought()
        {
            var p = PlayerProfile.CreateDefault();
            p.mastery.level = 2;
            p.mastery.points = 1;
            Cheats.SetUnlockAllNodes(true);
            // Buying is still refused for an "owned" node while the cheat is on, so nothing is spent.
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out _));
            Cheats.SetUnlockAllNodes(false);
            Assert.IsFalse(Cheats.UnlockAllNodes);
            CollectionAssert.IsEmpty(SkillTree.ActiveNodes(p));
            Assert.AreEqual(1, p.mastery.points);
        }
    }
}