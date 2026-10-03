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
                foreach (string part in new[] { "Text", "Main", "Swap" })
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
                    root.Tree.Show(profile, root.Config.progression, null, null);
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    string where = cheat ? "skill tree (cheat)" : "skill tree (fresh)";
                    var panel = (RectTransform)root.Tree.transform.Find("Panel");
                    var panelBox = ScreenBox(panel);
                    AssertOnScreen(new[] { ("Panel", panelBox) }, where);

                    var parts = new List<(string, Rect)>();
                    foreach (RectTransform child in panel)
                        if (child.gameObject.activeInHierarchy) parts.Add((child.name, ScreenBox(child)));
                    Assert.AreEqual(12, parts.Count(p => p.Item1.StartsWith("Node_")), "all twelve nodes have a card");
                    AssertNoOverlaps(parts, where);
                    var outside = parts.Where(p => !Inside(p.Item2, panelBox)).Select(p => $"{p.Item1} {p.Item2}").ToList();
                    Assert.IsEmpty(outside, $"{where}: outside the panel {panelBox}:\n" + string.Join("\n", outside));
                    AssertTextFits(panel, where);
                    root.Tree.Hide();
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

            // "Cluttered": a fixed-height body left a gap half the panel tall. Each text block
            // is now sized to what it holds, give or take a line of slack.
            Canvas.ForceUpdateCanvases();
            foreach (string block in new[] { "Body", "Progress" })
            {
                var t = panel.Find(block).GetComponent<Text>();
                if (string.IsNullOrEmpty(t.text)) continue;
                float gap = t.rectTransform.rect.height - t.preferredHeight;
                Assert.LessOrEqual(gap, 30f, $"results {block}: {gap:0} px of empty space under the text");
            }
        }
    }
}
