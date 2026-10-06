using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The practice / debug tools, folded behind one Quiet toggle at the top right (plan Task 12).
    /// They used to be a permanent wall of buttons down the right edge of every sandbox run;
    /// a player practising wants the arena, not twelve buttons, until they reach for one.
    ///
    /// Grouping is decided by the label, so GameRoot's existing AddDevButton calls need no
    /// change and a new tool lands somewhere sensible without anyone wiring it up.
    /// </summary>
    public sealed class PracticeDrawer : MonoBehaviour
    {
        // Fixed display order: Reset is added first (by the HUD), yet "Run tools" is shown last,
        // so a group's place comes from this list, not from when its first button arrived.
        static readonly string[] Order = { "Spawn", "Arena", "Run tools" };
        public const float ButtonWidth = 288f, ButtonHeight = 48f;

        Button toggle;
        Text toggleLabel;
        RectTransform panel;
        readonly Dictionary<string, RectTransform> groups = new Dictionary<string, RectTransform>();

        public bool Open => panel.gameObject.activeSelf;
        public Button ToggleButton => toggle;

        /// <summary>Which heading a tool sits under, from its caption.</summary>
        public static string Group(string label) =>
            label.StartsWith("+ ") ? "Spawn"
            : label == "Clear arena" || label.StartsWith("Auto-spawn") ? "Arena"
            : "Run tools";

        /// <summary>
        /// Builds the drawer under <paramref name="parent"/> (the HUD root). The toggle hangs
        /// under the 64-px pause button at the top right; the panel drops below the toggle.
        /// </summary>
        public static PracticeDrawer Create(Transform parent)
        {
            // A plain rect, no Graphic: the layout tests treat the toggle and the panel as the
            // HUD's top-level boxes, and a stretched graphic here would swallow them.
            var root = Ui.Stretch(Ui.Rect("PracticeDrawer", parent));
            var d = root.gameObject.AddComponent<PracticeDrawer>();
            d.toggle = UiKit.Button("PracticeToggle", root, "", d.Toggle, UiKit.Tier.Quiet);
            Ui.Place((RectTransform)d.toggle.transform, new Vector2(1, 1), new Vector2(-40, -112), new Vector2(ButtonWidth, ButtonHeight));
            d.toggleLabel = d.toggle.GetComponentInChildren<Text>();

            var frame = UiKit.Frame("PracticePanel", root, UiKit.FrameKind.Card);
            d.panel = frame.rectTransform;
            // Width = button + padding; height follows the content, so adding a tool never
            // needs a number changed here.
            Ui.Place(d.panel, new Vector2(1, 1), new Vector2(-40, -168), new Vector2(ButtonWidth + 48, 0));
            var col = Ui.Column(d.panel, 4);
            // 24 = the card sprite's 12-art-px slice border at 2 reference px per art px.
            col.padding = new RectOffset(24, 24, 20, 24);
            frame.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            d.SetOpen(false);
            return d;
        }

        /// <summary>A tool button under its group's heading; the heading appears on first use.</summary>
        public Button Add(string label, Action a)
        {
            var b = UiKit.Button("Dev_" + label, GroupFor(Group(label)), label, a, UiKit.Tier.Secondary);
            Ui.Sized(b, ButtonHeight);
            return b;
        }

        RectTransform GroupFor(string name)
        {
            if (groups.TryGetValue(name, out var g)) return g;
            g = Ui.Rect("Group_" + name, panel);
            Ui.Column(g, 4);
            var heading = UiKit.Text("Heading", g, name, UiFonts.Role.Sub, TextAnchor.MiddleLeft);
            heading.color = UiPalette.Honey;
            Ui.Sized(heading, 40);
            groups[name] = g;
            // Re-seat every group by the fixed order (cheap: three at most).
            int i = 0;
            foreach (var n in Order)
                if (groups.TryGetValue(n, out var each)) each.SetSiblingIndex(i++);
            return g;
        }

        public void Toggle() => SetOpen(!Open);

        /// <summary>Open or shut; the caption's arrow says which way the next press goes.</summary>
        public void SetOpen(bool on)
        {
            panel.gameObject.SetActive(on);
            toggleLabel.text = on ? "Practice tools ▴" : "Practice tools ▾";
        }
    }
}
