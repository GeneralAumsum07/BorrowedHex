using System.Collections;
using BorrowedHex.Core;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Plan Task 12, Review Focus 3: leaving a counted run from the pause menu asks first, the
    /// safe answer (Cancel) is the default, and backing out of the question returns to pause,
    /// never to the live run. Practice runs are free to restart and do not ask.
    /// </summary>
    public class PauseConfirmTests
    {
        GameObject camGo, rootGo;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("Cam") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            // Never the real save.
            GameRoot.StorageOverride = new MemoryProfileStorage();
            rootGo = new GameObject("GameRoot");
            root = rootGo.AddComponent<GameRoot>();
            yield return null;
            root.SetFocus(true);
            root.PlayShort();
            root.SetMenuOpen(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(rootGo);
            Object.Destroy(camGo);
            // The canvas and EventSystem are their own root objects. Left behind, the next test's
            // GameObject.Find("RestartRun") hits this test's still-open pause menu instead.
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(es.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                Object.Destroy(c.gameObject);
            GameRoot.StorageOverride = null;
            yield return null;
        }

        static Button Find(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"no active object named {name}");
            return go.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator RestartInACountedRunAsksFirstAndDefaultsToCancel()
        {
            root.SetMenuOpen(true);
            yield return null;
            Assert.IsTrue(root.IsCountedRunActive, "a short run counts");
            // The seed is drawn fresh by every BeginRun, so an unchanged seed means no restart.
            int seed = root.Sim.Setup.Seed;
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Cancel", EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual(seed, root.Sim.Setup.Seed, "nothing restarted yet");
        }

        [UnityTest]
        public IEnumerator EscOnConfirmReturnsToPauseNotToTheRun()
        {
            root.SetMenuOpen(true);
            yield return null;
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.IsTrue(root.Screens.Escape());
            yield return null;
            Assert.IsTrue(root.Menu.IsOpen, "back on the pause menu");
            Assert.IsTrue(root.Sim.Clock.HasPauseReason(PauseReason.Menu), "the run never resumed");
        }

        // P shares the pause action with Esc. On the confirm it must back out one level like
        // Esc, not toggle the pause menu shut underneath it and resume the run.
        [UnityTest]
        public IEnumerator PIsEscOnTheConfirmDialog()
        {
            root.SetMenuOpen(true);
            yield return null;
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.IsTrue(root.Confirm.IsOpen);
            root.Input.SimulatePause();
            yield return null;   // GameRoot.Update consumes it
            Assert.IsFalse(root.Confirm.IsOpen, "the question closed");
            Assert.IsTrue(root.Menu.IsOpen, "back on the pause menu");
            Assert.IsTrue(root.Sim.Clock.HasPauseReason(PauseReason.Menu), "the run never resumed");
        }

        [UnityTest]
        public IEnumerator AbandonFromPauseAsksFirst()
        {
            root.SetMenuOpen(true);
            yield return null;
            Find("MainMenuButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(root.Confirm.IsOpen, "the question is up");
            Assert.IsFalse(root.InMainMenu, "still in the run");
            root.Confirm.ConfirmButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(root.InMainMenu, "confirmed: back to the main menu");
        }

        [UnityTest]
        public IEnumerator PracticeRunsRestartWithoutAsking()
        {
            root.PlaySandbox();
            root.SetMenuOpen(true);
            yield return null;
            Assert.IsFalse(root.IsCountedRunActive);
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.IsFalse(root.Confirm.IsOpen, "no question for practice");
        }
    }
}
