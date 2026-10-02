using System.Collections;
using BorrowedHex.Core;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
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
        MemoryProfileStorage storage;

        /// <summary>Launch lands on the main menu; most tests want a live short run.</summary>
        IEnumerator StartShortRun()
        {
            root.PlayShort();
            root.SetFocus(true);
            root.SetMenuOpen(false);
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("TestCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 17, -17.5f);
            cam.transform.LookAt(new Vector3(0, 0, -2.2f));
            mouse = InputSystem.AddDevice<Mouse>();
            // Never the real save (section 8): an in-memory profile per test.
            storage = new MemoryProfileStorage();
            GameRoot.StorageOverride = storage;
            rootGo = new GameObject("GameRoot");
            root = rootGo.AddComponent<GameRoot>();
            yield return null;
            root.SetFocus(true);
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
            GameRoot.StorageOverride = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FocusLoss_PausesCombat_AndRegainingFocusKeepsMenuOpen()
        {
            yield return StartShortRun();
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
            yield return StartShortRun();
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

        [UnityTest]
        public IEnumerator Launch_ShowsTheMainMenu_AndNothingResumesCombatByAccident()
        {
            Assert.IsTrue(root.InMainMenu);
            Assert.IsTrue(root.Main.IsOpen);
            Assert.IsTrue(root.Sim.Clock.IsPaused, "the backdrop arena is frozen");
            Assert.IsFalse(root.Input.Gameplay.enabled);
            // Focus churn and the pause key must not start or resume anything.
            root.SetFocus(false);
            yield return null;
            root.SetFocus(true);
            root.SetMenuOpen(false);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.IsTrue(root.InMainMenu);
            Assert.IsTrue(root.Sim.Clock.IsPaused);
            Assert.IsFalse(root.Menu.IsOpen, "no pause menu over the main menu");
            // A click on empty space behind the menu is not a catch.
            yield return Click(new Vector2(Screen.width * 0.1f, Screen.height * 0.1f));
            Assert.AreEqual(0, root.Input.AcceptedCatchPresses);
        }

        [UnityTest]
        public IEnumerator Play_StartsAShortRun_AndMainMenuAbandonsItWithoutRecording()
        {
            yield return StartShortRun();
            Assert.IsFalse(root.InMainMenu);
            Assert.AreEqual(GameMode.Short, root.Sim.Setup.Mode);
            Assert.IsFalse(root.Sim.Setup.Sandbox);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(root.Sim.Clock.Now, 0.05, "combat is live");
            root.ShowMainMenu();
            yield return null;
            Assert.IsTrue(root.InMainMenu);
            Assert.AreEqual(0, root.Profile.Profile.stats.runs, "an abandoned run records nothing");
            Assert.AreEqual(0, storage.Writes);
        }

        [UnityTest]
        public IEnumerator AFinishedRun_IsSavedOnce_AndTheResultsOfferTheMainMenu()
        {
            yield return StartShortRun();
            root.Sim.DamagePlayer(100000, 0);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(RunState.Results, root.Sim.State);
            Assert.AreEqual(1, root.Profile.Profile.stats.runs);
            Assert.AreEqual(1, storage.Writes, "one explicit save at finalization");
            Assert.IsTrue(root.LastFinalize.Applied);
            // Reloading from the same storage sees the run.
            Assert.AreEqual(1, ProfileService.Load(storage).Profile.stats.runs);
        }

        [UnityTest]
        public IEnumerator TheTree_FromTheMainMenu_BuysAndEquips_AndTheNextRunUsesIt()
        {
            var m = root.Profile.Profile.mastery;
            m.level = 2;
            m.points = 1;
            root.Main.Press("mastery");
            yield return null;
            Assert.IsTrue(root.Tree.IsOpen);
            Assert.IsFalse(root.Main.IsOpen);
            root.Tree.Click(SkillTree.PrecisionAngle);   // buy
            root.Tree.Click(SkillTree.PrecisionAngle);   // equip
            Assert.AreEqual(2, storage.Writes, "each tree change is a save point");
            CollectionAssert.Contains(root.Profile.Profile.equippedNodes, SkillTree.PrecisionAngle);
            root.ShowMainMenu();
            Assert.IsFalse(root.Tree.IsOpen);
            yield return StartShortRun();
            float baseCone = root.Config.capture.coneAngle;
            Assert.AreEqual(baseCone + root.Config.progression.precisionAngle, root.Sim.Stats.CaptureConeAngle, 1e-4f);
            CollectionAssert.AreEqual(new[] { SkillTree.PrecisionAngle }, root.Sim.Setup.PassiveIds);
        }

        [UnityTest]
        public IEnumerator AFinishedRun_ShowsXpAndARecord_AndTheRecordsPanelListsIt()
        {
            yield return StartShortRun();
            root.Sim.DamagePlayer(100000, 0);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(1, root.LastFinalize.Records.Count);
            Assert.IsTrue(root.LastFinalize.Records[0].IsNewBest);
            root.ShowMainMenu();
            root.Main.Press("records");
            yield return null;
            Assert.IsTrue(root.RecordsView.IsOpen);
            StringAssert.Contains("Short", RecordsPanelText());
            root.ShowMainMenu();
            Assert.IsFalse(root.RecordsView.IsOpen);
        }

        string RecordsPanelText() => BorrowedHex.UI.RecordsPanel.RecordsBody(root.Profile.Profile);

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
