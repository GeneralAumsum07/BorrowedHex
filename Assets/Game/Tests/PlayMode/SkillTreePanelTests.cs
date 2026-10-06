using System.Collections;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Review Focus 5 (plan Task 10): the Accord splits selection from purchase, so a click is
    /// free, a repeated Unlock spends one point, and a refused Unlock says why.
    /// </summary>
    public class SkillTreePanelTests
    {
        GameObject camGo, rootGo; GameRoot root;

        [UnitySetUp] public IEnumerator SetUp()
        {
            camGo = new GameObject("Cam") { tag = "MainCamera" }; camGo.AddComponent<Camera>();
            GameRoot.StorageOverride = new MemoryProfileStorage();   // never the real save
            rootGo = new GameObject("GameRoot"); root = rootGo.AddComponent<GameRoot>();
            yield return null;
        }

        // As GameRootPlayModeTests: the root's canvas and EventSystem are separate objects.
        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(rootGo); Object.Destroy(camGo);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None)) Object.Destroy(es.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            GameRoot.StorageOverride = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectingNeverBuysAndADoubleUnlockSpendsOnePoint()
        {
            var m = root.Profile.Profile.mastery; m.level = 2; m.points = 2;
            root.Main.Press("mastery");
            yield return null;
            root.Tree.Click(SkillTree.PrecisionAngle);
            Assert.AreEqual(2, m.points, "selection is free");
            root.Tree.UnlockSelected();
            root.Tree.UnlockSelected();
            Assert.AreEqual(1, m.points);
            Assert.AreEqual(1, root.Profile.Profile.ownedNodes.Count);
        }

        [UnityTest]
        public IEnumerator WithNoPointsTheUnlockButtonSaysWhy()
        {
            var m = root.Profile.Profile.mastery; m.level = 2; m.points = 0;
            root.Main.Press("mastery");
            yield return null;
            root.Tree.Click(SkillTree.PrecisionAngle);
            yield return null;
            Assert.IsFalse(root.Tree.UnlockButton.interactable);
            StringAssert.Contains(SkillTree.WhyCannotBuy(root.Profile.Profile, SkillTree.PrecisionAngle), root.Tree.InspectorText);
        }

        // The kit's disabled sprite is its idle sprite, so a refused Unlock must say so another
        // way: it fades, and comes back when the node can be bought.
        [UnityTest]
        public IEnumerator ARefusedUnlockLooksRefused()
        {
            var m = root.Profile.Profile.mastery; m.level = 2; m.points = 0;
            root.Main.Press("mastery");
            yield return null;
            root.Tree.Click(SkillTree.PrecisionAngle);
            var group = root.Tree.UnlockButton.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "the Unlock button fades through a CanvasGroup");
            Assert.Less(group.alpha, 0.6f, "no points: faded");
            m.points = 1;
            root.Tree.Click(SkillTree.PrecisionAngle);
            Assert.AreEqual(1f, group.alpha, 1e-4f, "buyable: full strength");
        }
    }
}
