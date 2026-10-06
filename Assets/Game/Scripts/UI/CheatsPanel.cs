using System;
using System.Collections.Generic;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The Cheats panel, opened from the main menu. Same frame and rows as
    /// <see cref="SettingsPanel"/>. It holds no rules: the toggles are actions GameRoot passes in,
    /// and the rows read <see cref="Cheats"/> directly so they cannot drift from the state.
    /// </summary>
    public sealed class CheatsPanel : MonoBehaviour
    {
        Action onInvincible, onUnlockAll, onBack;
        readonly List<UiKit.Row> rows = new List<UiKit.Row>();

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>What the ScreenStack focuses when this screen comes up.</summary>
        public GameObject DefaultFocus => rows[0].Control.gameObject;

        public static CheatsPanel Create(Canvas canvas)
        {
            var dim = Ui.Image("Cheats", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(dim.rectTransform);
            var cp = dim.gameObject.AddComponent<CheatsPanel>();
            dim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            cp.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return cp;
        }

        void Build(RectTransform root)
        {
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 480));
            var head = UiKit.Text("Heading", frame.transform, "Cheats", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 64));
            var body = Ui.Rect("Rows", frame.transform);
            Ui.Place(body, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(752, 2 * UiKit.RowHeight + 16));
            Ui.Column(body, 16);
            // The row's own setter is ignored: the flip lives in GameRoot (it also refreshes the
            // main menu's cheat notice), and the row re-reads Cheats right after.
            rows.Add(UiKit.ToggleRow(body, "Invincibility", () => Cheats.Invincible, _ => Flip(onInvincible)));
            rows.Add(UiKit.ToggleRow(body, "All skills unlocked", () => Cheats.UnlockAllNodes, _ => Flip(onUnlockAll)));
            // Said once here so the toggles need no fine print of their own. Spec wording, verbatim.
            var note = UiKit.Text("Note", frame.transform, "Session only. Active cheats disable XP, records, and achievements.", UiFonts.Role.Small);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -272), new Vector2(752, 80));
            var back = UiKit.Button("Back", frame.transform, "Back", () => onBack?.Invoke(), UiKit.Tier.Secondary);
            Ui.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 64));
        }

        public void Show(Action toggleInvincible, Action toggleUnlockAll, Action back)
        {
            onInvincible = toggleInvincible;
            onUnlockAll = toggleUnlockAll;
            onBack = back;
            Refresh();
            gameObject.SetActive(true);
            // No SetSelectedGameObject here: the ScreenStack owns focus (Task 4).
        }

        public void Hide() => gameObject.SetActive(false);

        void Flip(Action a)
        {
            a?.Invoke();
            Refresh();
        }

        void Refresh() { foreach (var r in rows) r.Refresh(); }
    }
}
