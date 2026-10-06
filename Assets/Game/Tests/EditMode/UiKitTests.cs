using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    public class UiKitTests
    {
        Canvas canvas;
        [SetUp] public void SetUp() { UiSkin.ForceFlat = true; canvas = Ui.CreateCanvas("KitTest", 0); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(canvas.gameObject); UiSkin.ForceFlat = false; }

        [Test]
        public void PrimaryButtonsUseTheDisplayFaceAndHoney()
        {
            var b = UiKit.Button("Go", canvas.transform, "Play", null, UiKit.Tier.Primary);
            var t = b.GetComponentInChildren<Text>();
            Assert.AreSame(UiFonts.Display, t.font);
            Assert.AreEqual(UiPalette.Honey, t.color);
            Assert.AreEqual(UiFonts.Size(UiFonts.Role.Button), t.fontSize);
        }

        [Test]
        public void QuietButtonsHaveNoPlate()
        {
            var b = UiKit.Button("Q", canvas.transform, "Cheats", null, UiKit.Tier.Quiet);
            Assert.AreEqual(0f, b.GetComponent<Image>().color.a, 1e-4, "raycastable but invisible");
        }

        // State is never colour-alone (spec): the toggle says On/Off in words too.
        [Test]
        public void ToggleRowFlipsAndSaysSo()
        {
            bool v = false;
            var row = UiKit.ToggleRow(canvas.transform, "Reduce flashes", () => v, x => v = x);
            Assert.AreEqual("Off", row.Value.text);
            ((Button)row.Control).onClick.Invoke();
            Assert.IsTrue(v); Assert.AreEqual("On", row.Value.text);
        }

        [Test]
        public void StepperCallsBothDirections()
        {
            int total = 0;
            var row = UiKit.StepperRow(canvas.transform, "UI scale", () => total.ToString(), d => total += d);
            row.Rect.Find("Less").GetComponent<Button>().onClick.Invoke();
            row.Rect.Find("More").GetComponent<Button>().onClick.Invoke();
            row.Rect.Find("More").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, total); Assert.AreEqual("1", row.Value.text);
        }

        // Task 7: Settings builds its rows against a placeholder profile, then Show swaps in the
        // real one and calls Refresh. The gem must follow, not just the On/Off text.
        [Test]
        public void ARefreshedToggleRowMovesItsGemWithTheValue()
        {
            bool v = false;
            var row = UiKit.ToggleRow(canvas.transform, "Reduce flashes", () => v, x => v = x);
            var gem = row.Control.transform.Find("Gem");
            Assert.IsFalse(gem.gameObject.activeSelf);
            v = true;            // changed elsewhere (a loaded profile), not by a click
            row.Refresh();
            Assert.AreEqual("On", row.Value.text);
            Assert.IsTrue(gem.gameObject.activeSelf, "the mark agrees with the text");
        }

        // Task 7 capture: the focus pointer, drawn left of the focused control, sat on top of the
        // row's value ("As launched"). The value must end where the pointer's reach begins.
        [Test]
        public void ARowsValueLeavesRoomForTheFocusPointer()
        {
            var rows = new[]
            {
                UiKit.ToggleRow(canvas.transform, "Toggle", () => true, _ => { }),
                UiKit.StepperRow(canvas.transform, "Stepper", () => "As launched", _ => { }),
            };
            foreach (var row in rows)
            {
                row.Rect.sizeDelta = new Vector2(752, UiKit.RowHeight);   // the Settings column width
                Canvas.ForceUpdateCanvases();
                var v = new Vector3[4]; row.Value.rectTransform.GetWorldCorners(v);
                var c = new Vector3[4]; ((RectTransform)row.Control.transform).GetWorldCorners(c);
                float scale = canvas.transform.lossyScale.x;
                Assert.LessOrEqual(v[2].x, c[0].x - FocusPointer.Reach * scale + 0.5f, row.Label.text);
            }
        }

        [Test]
        public void TabsReportAndKeepTheirSelection()
        {
            int picked = -1;
            var tabs = UiKit.Tabs(canvas.transform, new[] { "Skills", "Capture style" }, i => picked = i);
            tabs.Select(1);
            Assert.AreEqual(1, tabs.Selected); Assert.AreEqual(1, picked);
        }

        // WCAG contrast of a (possibly translucent) label over an opaque face. Linear-space
        // luminance, so the ratio matches what a contrast checker reports for the sRGB values.
        static float Contrast(Color text, Color face)
        {
            var c = Color.Lerp(face, new Color(text.r, text.g, text.b, 1f), text.a);
            float L(Color x) { var l = x.linear; return 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b; }
            float a = L(c), b = L(face);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        // Task 6 capture: on the gold tab art an ENABLED unselected label (Muted) read as disabled.
        // Camel is the darkest gold the tab face uses; Panel is the flat fallback's face.
        [TestCase(true)]
        [TestCase(false)]
        public void TabLabelsContrastWithTheirTabFace(bool art)
        {
            UiSkin.ForceFlat = !art;
            if (art && !UiSkin.HasArt) Assert.Ignore("UI art not imported in this checkout");
            var tabs = UiKit.Tabs(canvas.transform, new[] { "Short run", "Endless" }, null);
            tabs.Select(0);
            var face = art ? UiPalette.Camel : UiPalette.Panel;
            for (int i = 0; i < 2; i++)
            {
                var t = tabs.Rect.GetChild(i).GetComponentInChildren<Text>();
                Assert.GreaterOrEqual(Contrast(t.color, face), 4.5f, $"tab {i} ({(i == 0 ? "selected" : "unselected")}) on {(art ? "art" : "flat")}");
            }
        }

        [Test]
        public void ADisabledTabReadsDimmerThanAnEnabledOne()
        {
            var tabs = UiKit.Tabs(canvas.transform, new[] { "A", "B", "C" }, null);
            tabs.Select(0);
            tabs.SetInteractable(2, false);
            Text Label(int i) => tabs.Rect.GetChild(i).GetComponentInChildren<Text>();
            Assert.IsFalse(tabs.Rect.GetChild(2).GetComponent<Button>().interactable);
            Assert.Less(Contrast(Label(2).color, UiPalette.Panel), Contrast(Label(1).color, UiPalette.Panel), "disabled must look different from merely unselected");
        }

        [Test]
        public void ATooltipWaitsBeforeShowing()
        {
            var b = UiKit.Button("Gear", canvas.transform, "", null, UiKit.Tier.Icon);
            var tip = UiTooltip.Attach(b, () => "Settings");
            tip.Hover(true, 10f);
            tip.Tick(10.2f); Assert.IsFalse(tip.Showing);
            tip.Tick(10.4f); Assert.IsTrue(tip.Showing);
            StringAssert.Contains("Settings", tip.Label.text);
            tip.Hover(false, 10.5f); tip.Tick(10.5f); Assert.IsFalse(tip.Showing);
        }

        [Test]
        public void ScrollViewClipsItsContent()
        {
            var content = UiKit.ScrollView("Rows", canvas.transform, out var scroll);
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>(), "rows never draw over the footer");
            Assert.AreSame(content, scroll.content);
            Assert.IsFalse(scroll.horizontal);
        }
    }
}
