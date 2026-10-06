using System.Collections;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Spec 5: every screen fits across the 80-130% interface scales, with and without the art.
    ///
    /// Why only the scale varies: the canvas uses ScaleWithScreenSize at match 0.5. For any 16:9
    /// screen (1280x720, 1600x900, 1920x1080) the factor is screen/reference on both axes, so
    /// the canvas is laid out at exactly reference = 1920x1080 / scale whatever the pixel size;
    /// the three 16:9 sizes are one case per scale. The ultrawide case changes the aspect, and
    /// the PlayMode game view cannot be resized, so it is not reproducible here (ledgered).
    /// </summary>
    public class LayoutSweepTests
    {
        static readonly float[] Scales = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };
        static readonly string[] Names = { "main", "settings", "hud", "pause", "upgrade", "results" };

        GameObject camGo, rootGo;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("SweepCamera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            GameRoot.StorageOverride = new MemoryProfileStorage();
            GameRoot.SkipStory = true; // predates the lore holds; NarrativeFlowTests covers them
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UiSkin.ForceFlat = false;   // a static: never leak the flat skin into other tests
            if (rootGo != null) Object.Destroy(rootGo);
            Object.Destroy(camGo);
            GameRoot.StorageOverride = null;
            GameRoot.SkipStory = false;
            yield return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator EveryScreenFitsAtEveryScale([Values(false, true)] bool flat)
        {
            // The skin is chosen as the screens are built, so the root is made after the flag.
            UiSkin.ForceFlat = flat;
            rootGo = new GameObject("GameRoot");
            root = rootGo.AddComponent<GameRoot>();
            yield return null;
            root.SetFocus(true);
            var scaler = root.Canvas.GetComponent<CanvasScaler>();
            var visible = new Rect(0, 0, Screen.width, Screen.height);
            var failures = new List<string>();
            foreach (var scale in Scales)
            {
                // The same assignment ApplySettings makes for the Interface scale setting.
                scaler.referenceResolution = new Vector2(1920f, 1080f) / scale;
                foreach (var name in Names)
                {
                    var host = default(Transform);
                    yield return Open(name, t => host = t);
                    Canvas.ForceUpdateCanvases();
                    string where = $"{name} @ {scale:P0}{(flat ? " flat" : "")}";
                    if (host == null) { failures.Add($"{where}: screen did not open"); continue; }
                    failures.AddRange(LayoutProbe.Check(host, visible, where));
                    // Overlap among the controls of a menu: two buttons on top of each other
                    // are the clearest sign a column ran out of room at this scale.
                    var controls = new List<(string, Rect)>();
                    foreach (var b in host.GetComponentsInChildren<Button>(false))
                        if (b.GetComponentInParent<ScrollRect>() == null) controls.Add((b.transform.parent.name + "/" + b.name, LayoutProbe.ScreenBox((RectTransform)b.transform)));
                    failures.AddRange(LayoutProbe.Overlaps(controls, where));
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>Opens a screen by the path a player takes and reports its root.</summary>
        IEnumerator Open(string name, System.Action<Transform> found)
        {
            switch (name)
            {
                case "main":
                    root.ShowMainMenu();
                    yield return Frames(2);
                    found(root.Main.transform);
                    break;
                case "settings":
                    root.ShowMainMenu();
                    yield return Frames(1);
                    typeof(GameRoot).GetMethod("OpenSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(root, null);
                    yield return Frames(2);
                    found(root.Settings.transform);
                    break;
                case "hud":
                    root.PlayShort();
                    root.SetMenuOpen(false);
                    yield return Frames(3);
                    // The busiest HUD: a full set of four upgrades.
                    root.Sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit, UpgradeId.PartingGift);
                    yield return Frames(3);
                    found(root.Hud.transform);
                    break;
                case "pause":
                    root.PlayShort();
                    yield return Frames(2);
                    root.SetMenuOpen(true);
                    yield return Frames(2);
                    found(root.Menu.transform);
                    break;
                case "upgrade":
                    root.PlayShort();
                    root.SetMenuOpen(false);
                    yield return Frames(3);
                    root.Sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit);
                    root.Sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.PartingGift, 3), new UpgradeOffer(UpgradeId.FinalSecond, 3), new UpgradeOffer(UpgradeId.Overflow, 3));
                    yield return Frames(3);
                    found(root.Flow.transform.Find("UpgradeChoice"));
                    break;
                case "results":
                    root.PlayShort();
                    root.SetMenuOpen(false);
                    yield return Frames(3);
                    root.Sim.DamagePlayer(100000, 0);
                    for (int i = 0; i < 20 && root.Sim.State != RunState.Results; i++) yield return null;
                    yield return Frames(3);
                    found(root.Flow.transform.Find("Results"));
                    break;
            }
        }
    }
}
