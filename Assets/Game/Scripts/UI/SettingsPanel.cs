using System;
using System.Collections.Generic;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Settings (Phase 8, rebuilt as labelled rows in plan Task 7): display mode, UI scale and two
    /// readability options. Label left, current value and control right, so the state is read
    /// at a glance instead of parsed out of a button caption. Each change is applied at once and
    /// saved (an explicit save point).
    ///
    /// No aim sensitivity: aim follows the absolute cursor position, so a sensitivity value
    /// would change nothing (D74, open question for the owner).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        static readonly float[] Scales = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };

        // A default stand-in until Show passes the live profile: the rows read their values while
        // they are being built (ToggleRow/StepperRow refresh once at creation), before any profile
        // exists. The brief's version left this null and would throw in Create.
        ProfileSettings settings = new ProfileSettings();
        Action onChanged, onBack;
        readonly List<UiKit.Row> rows = new List<UiKit.Row>();
        Button back;
        bool allowWindowed;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>What the ScreenStack focuses when this screen comes up: the first row's control.</summary>
        public GameObject DefaultFocus => rows[0].Control.gameObject;

        public static SettingsPanel Create(Canvas canvas, bool allowWindowed)
        {
            var dim = Ui.Image("Settings", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(dim.rectTransform);
            var sp = dim.gameObject.AddComponent<SettingsPanel>();
            dim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            sp.allowWindowed = allowWindowed;
            sp.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return sp;
        }

        /// <summary>
        /// One step along the scale list. Clamps at the ends: a stepper's "›" at 130% that wrapped
        /// to 80% would shrink the whole UI under the player's cursor.
        /// </summary>
        public static float StepScale(float current, int dir)
        {
            int nearest = 0;
            for (int i = 1; i < Scales.Length; i++)
                if (Mathf.Abs(Scales[i] - current) < Mathf.Abs(Scales[nearest] - current)) nearest = i;
            // An off-list value (hand-edited save) counts as already past its nearest step in the
            // direction of travel only if it lies beyond it; otherwise it snaps to that step.
            bool beyond = dir > 0 ? current > Scales[nearest] + 1e-3f : current < Scales[nearest] - 1e-3f;
            bool on = Mathf.Abs(current - Scales[nearest]) < 1e-3f;
            int target = on || beyond ? nearest + dir : nearest;
            return Scales[Mathf.Clamp(target, 0, Scales.Length - 1)];
        }

        /// <summary>
        /// As launched, fullscreen, windowed (desktop only), wrapping: a closed list of three, so
        /// both arrows reach every mode. An unknown saved value starts from "as launched".
        /// </summary>
        public static int StepDisplay(int mode, int dir, bool allowWindowed)
        {
            var order = allowWindowed ? new[] { -1, 1, 0 } : new[] { -1, 1 };
            int i = Array.IndexOf(order, mode); if (i < 0) i = 0;
            return order[(i + dir + order.Length) % order.Length];
        }

        void Build(RectTransform root)
        {
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 560));
            var head = UiKit.Text("Heading", frame.transform, "Settings", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 64));
            var body = Ui.Rect("Rows", frame.transform);
            Ui.Place(body, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(752, 4 * UiKit.RowHeight + 3 * 16));
            Ui.Column(body, 16);
            // The lambdas read `settings` at click time: Show swaps in the live profile object.
            rows.Add(UiKit.StepperRow(body, "Display", () => settings.displayMode switch { 1 => "Fullscreen", 0 => "Windowed", _ => "As launched" },
                d => { settings.displayMode = StepDisplay(settings.displayMode, d, allowWindowed); onChanged?.Invoke(); }));
            rows.Add(UiKit.StepperRow(body, "Interface scale", () => $"{Mathf.RoundToInt(settings.uiScale * 100)}%",
                d => { settings.uiScale = StepScale(settings.uiScale, d); onChanged?.Invoke(); }));
            rows.Add(UiKit.ToggleRow(body, "Reduce flashes", () => settings.reduceFlashes, v => { settings.reduceFlashes = v; onChanged?.Invoke(); }));
            rows.Add(UiKit.ToggleRow(body, "Control hints", () => settings.showHints, v => { settings.showHints = v; onChanged?.Invoke(); }));
            back = UiKit.Button("Back", frame.transform, "Back", () => onBack?.Invoke(), UiKit.Tier.Secondary);
            Ui.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 64));
        }

        public void Show(ProfileSettings s, Action changed, Action back)
        {
            settings = s;
            onChanged = changed;
            onBack = back;
            Refresh();
            gameObject.SetActive(true);
            // No SetSelectedGameObject here: the ScreenStack owns focus (Task 4).
        }

        public void Hide() => gameObject.SetActive(false);

        void Refresh() { foreach (var r in rows) r.Refresh(); }
    }

    /// <summary>
    /// Readability options the HUD reads every frame. Static because they are presentation-only
    /// and global to the window; the sim never sees them.
    /// </summary>
    public static class DisplayOptions
    {
        public static bool ReduceFlashes;
        public static bool ShowHints = true;
    }
}
