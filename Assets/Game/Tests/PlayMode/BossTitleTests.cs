using System.Collections;
using BorrowedHex.Core;
using BorrowedHex.Progression;
using BorrowedHex.Presentation;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Task 17: the boss name is shown once. Through a full world-arena boss intro, the
    /// cinematic title and the flow banner are never both on screen in the same frame.
    /// Kept apart from WorldPresentationPlayModeTests, which carries the owner's uncommitted work.
    /// </summary>
    public class BossTitleTests
    {
        GameObject rootObject, artObject, cameraObject;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameRoot.StorageOverride = new MemoryProfileStorage();
            cameraObject = new GameObject("BossTitleTestCamera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            rootObject = new GameObject("BossTitleTestRoot");
            root = rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBossNameIsNeverShownTwiceInOneFrame()
        {
            // Same wiring as the owner's intro test: the observer drives the cinematic overlay.
            artObject = new GameObject("BossTitleWorldArt");
            var observer = artObject.AddComponent<WorldPresentation>();
            observer.BindRoot(root);
            root.useWorldArenas = true; root.PlayShort();
            root.Sim.Player.InvulnerableUntil = double.MaxValue;
            yield return null;
            yield return null;
            typeof(BorrowedHex.Runs.ArenaSim).GetMethod("BeginBossIntro", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(root.Sim, new object[] { 0 });

            var banner = root.Flow.transform.Find("BossBanner").gameObject;
            bool sawTitle = false, sawBanner = false;
            // Long enough to cover the whole intro (~7.5 s) and the banner's hold and fade after it.
            float until = Time.unscaledTime + 11f;
            while (Time.unscaledTime < until)
            {
                yield return new WaitForEndOfFrame();
                bool title = WorldIntroOverlay.TitleShowing, band = banner.activeSelf;
                sawTitle |= title; sawBanner |= band;
                Assert.That(title && band, Is.False, $"both names on screen at state {root.Sim.State}");
            }
            // The test is only meaningful if the cinematic title really appeared.
            Assert.That(sawTitle, Is.True, "the cinematic title never showed");
            TestContext.WriteLine($"banner seen: {sawBanner}, state at end: {root.Sim.State}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (artObject != null) Object.Destroy(artObject);
            Object.Destroy(rootObject);
            Object.Destroy(cameraObject);
            GameRoot.StorageOverride = null;
            yield return null;
        }
    }
}
