using System.Collections;
using BorrowedHex.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Phase 1 PlayMode checks that need the real frame loop: focus loss pauses combat, and a
    /// click on a HUD button goes to the UI instead of becoming a catch.
    /// A virtual mouse is added for the test and removed afterwards so the real device state
    /// is untouched.
    /// </summary>
    public class GameRootPlayModeTests
    {
        GameObject camGo, rootGo;
        GameRoot root;
        Mouse mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("TestCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 17, -17.5f);
            cam.transform.LookAt(new Vector3(0, 0, -2.2f));
            mouse = InputSystem.AddDevice<Mouse>();
            rootGo = new GameObject("GameRoot");
            root = rootGo.AddComponent<GameRoot>();
            yield return null;
            // The editor may report itself unfocused while tests run; start from a live run.
            root.SetFocus(true);
            root.SetMenuOpen(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(rootGo);
            Object.Destroy(camGo);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(es.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                Object.Destroy(c.gameObject);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FocusLoss_PausesCombat_AndRegainingFocusKeepsMenuOpen()
        {
            yield return null;
            double before = root.Sim.Clock.Now;
            root.SetFocus(false);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(root.Sim.Clock.IsPaused);
            Assert.AreEqual(before, root.Sim.Clock.Now, 1e-9, "clock must not advance while unfocused");

            root.SetFocus(true);
            yield return null;
            Assert.IsTrue(root.Menu.IsOpen, "focus regain lands on the pause menu, not live combat");
            Assert.IsTrue(root.Sim.Clock.IsPaused);

            root.SetMenuOpen(false);
            // Real time, not frame count: uncapped test frames can be shorter than one sim step.
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(root.Sim.Clock.Now, before + 0.05);
        }

        [UnityTest]
        public IEnumerator ClickOnHudButton_DoesNotCatch_ButClickOnArenaDoes()
        {
            // 1) Hover the HUD pause button for a frame so the UI knows the pointer is on it.
            var rt = (RectTransform)root.Hud.PauseButton.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // overlay canvas: world == screen pixels
            Vector2 onButton = (corners[0] + corners[2]) * 0.5f;
            yield return Click(onButton);

            Assert.AreEqual(0, root.Input.AcceptedCatchPresses, "a HUD click must never become a catch");
            Assert.IsTrue(root.Menu.IsOpen, "the click reached the pause button");

            // 2) Control case: resume, then click empty arena space — that one IS a catch.
            root.SetMenuOpen(false);
            yield return Click(new Vector2(Screen.width * 0.5f, Screen.height * 0.35f));
            Assert.AreEqual(1, root.Input.AcceptedCatchPresses);
        }

        IEnumerator Click(Vector2 pos)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, false));
            yield return null;
            yield return null;
        }
    }
}
