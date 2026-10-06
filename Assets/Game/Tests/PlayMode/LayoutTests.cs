using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Layout guards for the HUD and the run-flow panels (owner report: "overlapping and
    /// cluttered"). The UI is built in code, so a moved number can silently stack two things on
    /// top of each other; these tests measure the real, laid-out rectangles in screen pixels.
    ///
    /// The rules, applied to whatever is visible in the busiest states the game can reach:
    /// 1. No two HUD elements' boxes overlap. A box is the space an element is given, not the
    ///    ink it happens to draw this frame, so a label that is empty now still owns its space.
    /// 2. An open panel overlaps no visible HUD element (the HUD it would cover is hidden).
    /// 3. Inside a panel, a card's text and its buttons do not overlap and stay inside the card.
    /// 4. Every text fits its box (no line spills out of the bottom).
    /// 5. The results body is sized to its text: no large dead gap (the "cluttered" half).
    /// 6. Everything is on screen.
    /// </summary>
    public class LayoutTests
    {
        GameObject camGo, rootGo;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("TestCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 17, -17.5f);
            cam.transform.LookAt(new Vector3(0, 0, -2.2f));
            // Never the real save (section 8).
            GameRoot.StorageOverride = new MemoryProfileStorage();
            GameRoot.SkipStory = true; // predates the lore holds; NarrativeFlowTests covers them
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
            GameRoot.StorageOverride = null;
            GameRoot.SkipStory = false;
            yield return null;
        }

        // ---- Measuring ------------------------------------------------------------------------

        /// <summary>
        /// A rect in screen pixels. The game canvas is ScreenSpaceOverlay, where world space IS
        /// screen space, so the world corners are already the on-screen box.
        /// </summary>
        static Rect ScreenBox(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>
        /// Overlap with half a pixel of grace, so two boxes that merely share an edge (a common
        /// result of exact layout arithmetic) are not reported.
        /// </summary>
        static bool Overlap(Rect a, Rect b) =>
            a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

        static bool Inside(Rect inner, Rect outer) =>
            inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

        static Rect ScreenRect => new Rect(0, 0, Screen.width, Screen.height);

        static bool Shows(Graphic g) =>
            g.isActiveAndEnabled && (g is Text || g.color.a > 0.01f);

        /// <summary>
        /// The HUD's top-level elements: every showing Graphic with no Graphic above it inside the
        /// HUD. That keeps the life bar but not its fill, a slot but not its label, a button but
        /// not its caption, because the children live inside their parent's box by construction.
        /// </summary>
        static List<(string name, Rect box)> HudBoxes(GameplayHud hud)
        {
            var list = new List<(string, Rect)>();
            foreach (var g in hud.GetComponentsInChildren<Graphic>(false))
            {
                if (!Shows(g)) continue;
                bool nested = false;
                for (var t = g.transform.parent; t != null && t != hud.transform; t = t.parent)
                    if (t.GetComponent<Graphic>() != null) { nested = true; break; }
                if (nested) continue;
                list.Add((g.name, ScreenBox(g.rectTransform)));
            }
            return list;
        }

        static void AssertNoOverlaps(List<(string name, Rect box)> boxes, string where)
        {
            var bad = new List<string>();
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (Overlap(boxes[i].box, boxes[j].box))
                        bad.Add($"{boxes[i].name} {boxes[i].box} x {boxes[j].name} {boxes[j].box}");
            Assert.IsEmpty(bad, $"{where}: overlapping boxes:\n" + string.Join("\n", bad));
        }

        static void AssertOnScreen(IEnumerable<(string name, Rect box)> boxes, string where)
        {
            var bad = boxes.Where(b => !Inside(b.box, ScreenRect)).Select(b => $"{b.name} {b.box}").ToList();
            Assert.IsEmpty(bad, $"{where}: off screen (screen {Screen.width}x{Screen.height}):\n" + string.Join("\n", bad));
        }

        /// <summary>
        /// Every showing, non-empty Text under <paramref name="under"/> fits its box. Text.preferredHeight
        /// lays the string out at the box's current width, so a wrap the designer did not plan
        /// for shows up as a height larger than the box.
        /// </summary>
        static void AssertTextFits(Transform under, string where)
        {
            var bad = new List<string>();
            foreach (var t in under.GetComponentsInChildren<Text>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                float need = t.preferredHeight, have = t.rectTransform.rect.height;
                if (need > have + 1f) bad.Add($"{t.transform.parent.name}/{t.name}: needs {need:0} px, has {have:0} (\"{Short(t.text)}\")");
            }
            Assert.IsEmpty(bad, $"{where}: text spills out of its box:\n" + string.Join("\n", bad));
        }

        static string Short(string s) => s.Length > 60 ? s.Substring(0, 60).Replace("\n", " | ") + "..." : s.Replace("\n", " | ");

        static RectTransform OpenPanel(RunFlowPanels flow, string dimName)
        {
            var dim = flow.transform.Find(dimName);
            Assert.IsNotNull(dim, dimName);
            Assert.IsTrue(dim.gameObject.activeInHierarchy, $"{dimName} should be showing");
            return (RectTransform)dim.Find("Panel");
        }

        /// <summary>A panel is on screen, overlaps no showing HUD element, and all its text fits.</summary>
        void AssertPanelClean(RectTransform panel, string where)
        {
            Canvas.ForceUpdateCanvases();
            var box = ScreenBox(panel);
            AssertOnScreen(new[] { ("Panel", box) }, where);
            var hits = HudBoxes(root.Hud).Where(h => Overlap(h.box, box)).Select(h => $"{h.name} {h.box}").ToList();
            Assert.IsEmpty(hits, $"{where}: panel {box} covers HUD elements:\n" + string.Join("\n", hits));
            AssertTextFits(panel, where);
        }

        /// <summary>Each showing card: text and buttons are disjoint and inside the card.</summary>
        static void AssertCardsClean(RectTransform panel, string where)
        {
            for (int i = 0; i < 3; i++)
            {
                var card = panel.Find($"Card{i}");
                if (card == null || !card.gameObject.activeInHierarchy) continue;
                var cardBox = ScreenBox((RectTransform)card);
                var parts = new List<(string, Rect)>();
                // Task 15: the card body split into a glyph, a name line, the effect ("Text") and
                // the locked line; each must clear the action column and stay inside the card.
                foreach (string part in new[] { "Glyph", "Name", "Text", "Locked", "Main", "Swap" })
                {
                    var p = card.Find(part);
                    if (p != null && p.gameObject.activeInHierarchy) parts.Add(($"Card{i}/{part}", ScreenBox((RectTransform)p)));
                }
                AssertNoOverlaps(parts, where);
                var outside = parts.Where(p => !Inside(p.Item2, cardBox)).Select(p => p.Item1).ToList();
                Assert.IsEmpty(outside, $"{where}: outside Card{i} {cardBox}: " + string.Join(", ", outside));
            }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ---- Tests ----------------------------------------------------------------------------

        // Plan Task 6: the left column, the top-right Training/Settings pair and the footer never
        // collide, and nothing is pushed off the edge at the reference resolution.
        [UnityTest]
        public IEnumerator MainMenu_NothingOverlapsAndEveryControlIsOnScreen()
        {
            root.ShowMainMenu();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var boxes = new List<(string, Rect)>();
            foreach (var b in root.Main.GetComponentsInChildren<Button>(false)) boxes.Add((b.name, ScreenBox((RectTransform)b.transform)));
            // Searched by name at any depth: the title, status and cheat notice live under the
            // column, and a direct-child Find would skip them without failing.
            var texts = root.Main.GetComponentsInChildren<Text>(false);
            foreach (var n in new[] { "Title", "Status", "Warning", "CheatNotice" })
            {
                var t = texts.FirstOrDefault(x => x.name == n);
                if (t != null && t.text != "") boxes.Add((n, ScreenBox(t.rectTransform)));
            }
            AssertNoOverlaps(boxes, "main menu");
            AssertOnScreen(boxes, "main menu");
            AssertTextFits(root.Main.transform, "main menu");
        }

        [UnityTest]
        public IEnumerator Hud_InCombat_WithFourUpgrades_NothingOverlapsAndAllTextFits()
        {
            yield return Frames(30);
            var sim = root.Sim;
            // The busiest the left column gets: a full, locked set, every name on its own line.
            sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit, UpgradeId.PartingGift);
            yield return Frames(3);
            Canvas.ForceUpdateCanvases();
            var boxes = HudBoxes(root.Hud);
            Assert.Greater(boxes.Count, 8, "the HUD should be showing its elements");
            AssertNoOverlaps(boxes, "HUD in combat");
            AssertOnScreen(boxes, "HUD in combat");
            AssertTextFits(root.Hud.transform, "HUD in combat");
        }

        // Task 12: the practice tools fold into a drawer under the pause button. Opened in a
        // sandbox run (the only kind that shows it), every tool is on screen, its text fits,
        // and the drawer covers no other HUD element.
        [UnityTest]
        public IEnumerator Hud_PracticeDrawerOpen_NothingOverlapsAndAllTextFits()
        {
            root.PlaySandbox();
            root.SetMenuOpen(false);
            yield return Frames(3);
            Assert.IsFalse(root.Hud.Drawer.Open, "every run starts with the drawer shut");
            root.Hud.Drawer.Toggle();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var boxes = HudBoxes(root.Hud);
            Assert.IsTrue(boxes.Exists(b => b.name == "PracticePanel"), "the drawer panel is showing");
            AssertNoOverlaps(boxes, "HUD with the practice drawer open");
            AssertOnScreen(boxes, "HUD with the practice drawer open");
            var tools = root.Hud.Drawer.GetComponentsInChildren<Button>(false).Select(b => (b.name, ScreenBox((RectTransform)b.transform))).ToList();
            Assert.Greater(tools.Count, 10, "every tool is in the drawer");
            AssertNoOverlaps(tools, "practice tools");
            AssertOnScreen(tools, "practice tools");
            AssertTextFits(root.Hud.Drawer.transform, "practice drawer");
        }

        // Task 12: over the pause, only the life bar of the HUD stays, and it clears the frame.
        [UnityTest]
        public IEnumerator Pause_OnlyTheLifeBarShowsAndItClearsTheFrame()
        {
            yield return Frames(3);
            root.SetMenuOpen(true);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var shown = HudBoxes(root.Hud).Select(b => b.name).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "LifeBar", "LifeHeart" }, shown, "HUD left showing under the pause");
            var frame = ScreenBox((RectTransform)root.Menu.transform.Find("Panel"));
            var hits = HudBoxes(root.Hud).Where(h => Overlap(h.box, frame)).Select(h => h.name).ToList();
            Assert.IsEmpty(hits, "the pause frame covers the life bar");
            AssertTextFits(root.Menu.transform, "pause menu");
            root.SetMenuOpen(false);
            yield return Frames(2);
            Assert.Greater(HudBoxes(root.Hud).Count, 2, "resuming brings the HUD back");
        }

        [UnityTest]
        public IEnumerator UpgradePanel_PaidButtons_IgnoreAClickTheMomentThePanelOpens()
        {
            // Final review, Important 2: the panel opens mid-fight, under a held left button
            // (aim / fire). A click that lands in the first instant was aimed at the arena, not
            // at a card, and must not spend life. A deliberate click a moment later still buys.
            yield return Frames(10);
            var sim = root.Sim;
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.Overflow, 1));
            yield return null;   // the panel opens in RunFlowPanels.LateUpdate
            var main = OpenPanel(root.Flow, "UpgradeChoice").Find("Card0/Main").GetComponent<UnityEngine.UI.Button>();
            double life = sim.LifeSeconds;
            main.onClick.Invoke();
            Assert.AreEqual(RunState.UpgradeChoice, sim.State, "a click on the opening frame is ignored");
            Assert.AreEqual(0, sim.HeldUpgrades.Count);
            Assert.AreEqual(life, sim.LifeSeconds, "no life spent");
            yield return new WaitForSecondsRealtime(RunFlowPanels.PaidClickGuard + 0.1f);
            main.onClick.Invoke();
            Assert.AreEqual(1, sim.HeldUpgrades.Count, "after the guard, the same click buys");
        }

        [UnityTest]
        public IEnumerator UpgradePanel_EveryCardShape_IsCleanAndClearsTheHud()
        {
            yield return Frames(10);
            var sim = root.Sim;

            // 2 held: a rank-up (one paid button) beside new cards (Add + Swap), as in the report.
            sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley);
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.PiercingReturn, 2), new UpgradeOffer(UpgradeId.Overflow, 1), new UpgradeOffer(UpgradeId.FinalSecond, 1));
            yield return Frames(3);
            var panel = OpenPanel(root.Flow, "UpgradeChoice");
            AssertPanelClean(panel, "2 held");
            AssertCardsClean(panel, "2 held");
            // Task 15: the held line in the panel is the one listing; the HUD's chips (the whole
            // Combat group, which has no separate "Upgrades" child) hide while the choice is open.
            var combat = root.Hud.transform.Find("Combat");
            Assert.IsNotNull(combat, "HUD Combat group");
            Assert.IsFalse(combat.gameObject.activeInHierarchy, "the HUD's upgrade chips hide under the choice");
            sim.ContinueFromUpgrade();
            yield return Frames(2);

            // 3 held: every new card carries the extra "your last card" line, at the top rank,
            // so each card shows its longest text. Two draws cover all four remaining upgrades.
            sim.DebugHold(UpgradeId.HeavyOrbit);
            var draws = new[]
            {
                new[] { UpgradeId.PartingGift, UpgradeId.FinalSecond, UpgradeId.Overflow },
                new[] { UpgradeId.Fusion, UpgradeId.Overflow, UpgradeId.PartingGift },
            };
            foreach (var d in draws)
            {
                sim.DebugOpenChoice(d.Select(id => new UpgradeOffer(id, 3)).ToArray());
                yield return Frames(3);
                panel = OpenPanel(root.Flow, "UpgradeChoice");
                AssertPanelClean(panel, $"3 held, {string.Join("/", d)}");
                AssertCardsClean(panel, $"3 held, {string.Join("/", d)}");

                // The swap step with three targets is the tallest version of the panel.
                var swap = panel.Find("Card0/Swap");
                Assert.IsTrue(swap != null && swap.gameObject.activeInHierarchy, "Swap should show with 3 held");
                swap.GetComponent<Button>().onClick.Invoke();
                yield return Frames(2);
                AssertPanelClean(panel, "swap targets, 3 held");
                panel.Find("Back").GetComponent<Button>().onClick.Invoke();
                sim.ContinueFromUpgrade();
                yield return Frames(2);
            }

            // 4 held (locked): rank-ups only, plus a locked new card with its red line and no buttons.
            sim.DebugHold(UpgradeId.PartingGift);
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.HeavyOrbit, 3), new UpgradeOffer(UpgradeId.Fusion, 3), new UpgradeOffer(UpgradeId.EchoVolley, 3));
            yield return Frames(3);
            panel = OpenPanel(root.Flow, "UpgradeChoice");
            AssertPanelClean(panel, "4 held");
            AssertCardsClean(panel, "4 held");
        }

        /// <summary>
        /// D99 added a fourth branch, so the tree went from three 460-wide columns to four
        /// 360-wide ones. Every direct child of the panel (title, header, branch names, the
        /// twelve cards, status, note, buttons) must be disjoint, inside the panel, with its text
        /// fitting - checked with the longest card text each state can produce.
        /// </summary>
        // Task 11 capture: the tab art carries an ornament at each end, and a long label
        // ("Achievements", "Capture style") ran under it on the fixed 280-wide tab. Every tab
        // label must fit between the ornaments, on every screen that has tabs.
        [UnityTest]
        public IEnumerator Tabs_EveryLabelClearsTheTabOrnaments()
        {
            var bad = new List<string>();
            // Measure each strip while it is showing: a hidden view is never laid out, so its
            // tabs read as the default 100-wide rect.
            void Measure(Component host)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var tab in host.GetComponentsInChildren<Button>(false))
                {
                    if (!tab.name.StartsWith("Tab")) continue;
                    var label = tab.GetComponentInChildren<Text>(true);
                    float room = ((RectTransform)tab.transform).rect.width - BorrowedHex.UI.UiKit.TabInsetLeft - BorrowedHex.UI.UiKit.TabInsetRight;
                    if (label.preferredWidth > room + 1f) bad.Add($"{host.name}/{tab.name} \"{label.text}\": needs {label.preferredWidth:0}, has {room:0}");
                }
            }
            root.ShowMainMenu();
            yield return Frames(2);
            Measure(root.Main);
            root.Character.gameObject.SetActive(true);
            yield return Frames(2);
            Measure(root.Character);
            root.Character.gameObject.SetActive(false);
            root.RecordsView.Show(root.Profile.Profile, null);
            foreach (int tab in new[] { BorrowedHex.UI.RecordsPanel.RecordsTab, BorrowedHex.UI.RecordsPanel.AchievementsTab })
            {
                root.RecordsView.ShowTab(tab);
                yield return Frames(2);
                Measure(root.RecordsView);
            }
            root.RecordsView.Hide();
            Assert.IsEmpty(bad, "tab labels under the end ornaments:\n" + string.Join("\n", bad));
        }

        [UnityTest]
        public IEnumerator SkillTree_FourBranches_NothingOverlapsAndAllTextFits()
        {
            yield return Frames(2);
            var profile = root.Profile.Profile;
            try
            {
                // Two passes: a fresh profile (every card "Locked: needs ...", the longest state
                // line) and the cheat (every card "ACTIVE (cheat)").
                foreach (bool cheat in new[] { false, true })
                {
                    Cheats.SetUnlockAllNodes(cheat);
                    // Task 8: the tree lives in the Character screen's Body, so its host must be up.
                    root.Character.gameObject.SetActive(true);
                    root.Tree.Show(profile, root.Config.progression, null, null);
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    string where = cheat ? "skill tree (cheat)" : "skill tree (fresh)";
                    var panel = (RectTransform)root.Tree.transform.Find("Panel");   // Panel is kept as the name
                    var map = (RectTransform)panel.Find("Map");
                    var nodes = new List<(string, Rect)>();
                    foreach (RectTransform child in map)
                        if (child.name.StartsWith("Node_") && child.gameObject.activeInHierarchy) nodes.Add((child.name, ScreenBox(child)));
                    Assert.AreEqual(12, nodes.Count, "all twelve nodes are on the map");
                    // Names too: on a diagonal branch a name under its seal lands on the next
                    // seal inward, which the seals-only check never saw (Task 10 capture).
                    var labelled = new List<(string, Rect)>(nodes);
                    foreach (RectTransform child in map)
                        if (child.name.StartsWith("Node_")) labelled.Add((child.name + "/Name", ScreenBox((RectTransform)child.Find("Name"))));
                    AssertNoOverlaps(labelled, where);
                    var blocks = new List<(string, Rect)> { ("Map", ScreenBox(map)), ("Inspector", ScreenBox((RectTransform)panel.Find("Inspector"))) };
                    AssertNoOverlaps(blocks, where);
                    AssertOnScreen(nodes.Concat(blocks), where);
                    AssertTextFits(panel, where);
                    root.Tree.Hide();
                    root.Character.gameObject.SetActive(false);
                }
            }
            finally { Cheats.SetUnlockAllNodes(false); }   // a static: never leak into other tests
        }

        [UnityTest]
        public IEnumerator Results_FitTheirTextWithoutDeadSpaceAndClearTheHud()
        {
            yield return Frames(10);
            var sim = root.Sim;
            sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley);
            sim.DamagePlayer(100000, 0);
            for (int i = 0; i < 20 && sim.State != RunState.Results; i++) yield return null;
            Assert.AreEqual(RunState.Results, sim.State);
            yield return Frames(3);
            var panel = OpenPanel(root.Flow, "Results");
            AssertPanelClean(panel, "results");

            // "Cluttered": a fixed-height body left a gap half the panel tall. Task 16 order:
            // Title, Headline, Progress, then the buttons; no visible block is followed by more
            // than 30 px of nothing, and Progress is exactly as tall as what it holds.
            Canvas.ForceUpdateCanvases();
            var shown = new List<RectTransform>();
            foreach (RectTransform c in panel)
                if (c.gameObject.activeSelf && !(c.GetComponent<LayoutElement>()?.ignoreLayout ?? false)) shown.Add(c);
            var names = shown.Select(c => c.name).ToList();
            foreach (var n in new[] { "Title", "Headline", "Progress", "Buttons" })
                Assert.Contains(n, names, "results block showing");
            Assert.Less(names.IndexOf("Title"), names.IndexOf("Headline"));
            Assert.Less(names.IndexOf("Headline"), names.IndexOf("Progress"));
            Assert.Less(names.IndexOf("Progress"), names.IndexOf("Buttons"));
            Assert.IsFalse(panel.Find("DetailsView").gameObject.activeSelf, "Details opens collapsed");
            for (int i = 1; i < shown.Count; i++)
            {
                // In canvas units: the 30-px rule is a design-grid rule, and the game view may be
                // scaled (16 units of spacing read as 32 screen px at 2x).
                float gap = (ScreenBox(shown[i - 1]).yMin - ScreenBox(shown[i]).yMax) / panel.GetComponentInParent<Canvas>().scaleFactor;
                Assert.LessOrEqual(gap, 30f, $"results: {gap:0} px between {shown[i - 1].name} and {shown[i].name}");
            }
            var prog = (RectTransform)panel.Find("Progress");
            float slack = prog.rect.height - LayoutUtility.GetPreferredHeight(prog);
            Assert.LessOrEqual(Mathf.Abs(slack), 1f, $"results Progress: {slack:0} px of slack");
        }
    }
}
