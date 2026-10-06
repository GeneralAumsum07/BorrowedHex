using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Modal pause screen (plan Task 12): an ornate frame over the scrim. The scrim is a
    /// raycast target on purpose: while it is up, clicks cannot fall through to the HUD or be
    /// read as a gameplay catch.
    ///
    /// Buttons carry stable names (Resume, Settings, RestartRun, MainMenuButton, Quit) because
    /// tests and the confirm flow find them by name; the captions may change, the names do not.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public static readonly Vector2 FrameSize = new Vector2(640, 560);
        const float ActionWidth = 360f, ActionHeight = 56f;

        Button resume;
        RectTransform actions, frame;
        Image heldSheet;
        readonly List<HeldRow> heldRows = new List<HeldRow>();
        readonly List<(Text text, Func<string> label)> labelled = new List<(Text, Func<string>)>();

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>Keyboard users land on Resume (focus is set by the ScreenStack).</summary>
        public GameObject DefaultFocus => resume.gameObject;

        /// <param name="onQuit">Null on WebGL: closing a tab is the browser's job, so no Quit.</param>
        public static PauseMenu Create(Canvas canvas, Action onResume, Action onRestart, Action onQuit)
        {
            var scrim = Ui.Image("PauseMenu", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            var menu = scrim.gameObject.AddComponent<PauseMenu>();
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in

            // Centred, 560 tall: its top edge sits 260 px below the screen's, well clear of the
            // HUD life bar (top 24..76), which stays visible over the pause as context.
            var frame = UiKit.Frame("Panel", scrim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, FrameSize);
            menu.frame = frame.rectTransform;

            var title = UiKit.Text("Title", frame.transform, "Paused", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(480, 64));

            // Left edge flush with the action column below (-180), so the stack reads as one list.
            // Resume is the one Primary: it is what most pauses end with. The key hint sits beside
            // it rather than in a footer line, so it reads as "this button = Esc / P".
            menu.resume = UiKit.Button("Resume", frame.transform, "Resume", onResume, UiKit.Tier.Primary);
            Ui.Place((RectTransform)menu.resume.transform, new Vector2(0.5f, 1), new Vector2(-20, -128), new Vector2(320, 64));
            var hint = UiKit.Text("Hint", frame.transform, "Esc / P", UiFonts.Role.Small, TextAnchor.MiddleLeft);
            hint.color = UiPalette.Muted;
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(208, -128), new Vector2(120, 64));

            // The Secondary actions stack in a column that sizes itself, so AddButton can append
            // without anyone re-measuring the frame.
            menu.actions = Ui.Rect("Actions", frame.transform);
            Ui.Place(menu.actions, new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(ActionWidth, 0));
            Ui.Column(menu.actions, 12);
            menu.actions.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            menu.AddButton(() => "Restart run", onRestart, "RestartRun");

            // Quit is Quiet and set apart on the bottom edge: the one button that ends the session
            // should not sit in the column a fast Down-Down-Enter runs through.
            if (onQuit != null)
            {
                var quit = UiKit.Button("Quit", frame.transform, "Quit game", onQuit, UiKit.Tier.Quiet);
                Ui.Place((RectTransform)quit.transform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(240, 48));
            }

            // Spec: names and effects through "pause inspection". A parchment sheet beside the
            // frame (not inside it: four effects at reading size are taller than the frame).
            menu.heldSheet = UiKit.Frame("Held", scrim.transform, UiKit.FrameKind.Parchment);
            Ui.Place(menu.heldSheet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(SheetX, 0), new Vector2(SheetWidth, 200));
            var heading = UiKit.Text("Heading", menu.heldSheet.transform, "Held upgrades", UiFonts.Role.Sub, TextAnchor.MiddleLeft);
            heading.color = UiPalette.Ink;
            Ui.Place(heading.rectTransform, new Vector2(0, 1), new Vector2(SheetPad, -SheetPad), new Vector2(SheetWidth - 2 * SheetPad, 48));
            menu.heldSheet.gameObject.SetActive(false);

            scrim.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>
        /// Another Secondary action in the column. The label is re-read every time the menu opens,
        /// so it can name the current choice. <paramref name="first"/> puts it above the existing
        /// ones (Settings goes above Restart run), otherwise it is appended.
        /// </summary>
        public Button AddButton(Func<string> label, Action onClick, string name = "Extra", bool first = false)
        {
            var b = UiKit.Button(name, actions, label(), onClick, UiKit.Tier.Secondary);
            Ui.Sized(b, ActionHeight);
            if (first) b.transform.SetSiblingIndex(0);
            labelled.Add((b.GetComponentInChildren<Text>(), label));
            return b;
        }

        // With upgrades held, the frame and the sheet sit side by side, centred as a pair:
        // frame centre -324, sheet centre +324, a 48 gap between them.
        const float SheetWidth = 600f, SheetX = 324f, SheetPad = 40f, RowGap = 16f;

        sealed class HeldRow
        {
            public RectTransform Root;
            public Image Glyph;
            public Text Name, Effect;
        }

        /// <summary>
        /// Fills the "Held upgrades" sheet: glyph, name and rank, then the effect at this rank.
        /// Hidden, and the pause frame re-centred, when nothing is held. Rows are pooled, so
        /// re-opening pause never leaves a destroyed-but-not-yet-gone row in the layout.
        /// </summary>
        public void SetHeld(IReadOnlyList<UpgradeOffer> held, UpgradeTuning t)
        {
            bool any = held != null && held.Count > 0;
            heldSheet.gameObject.SetActive(any);
            frame.anchoredPosition = new Vector2(any ? -SheetX : 0f, 0f);
            if (!any) return;
            float inner = SheetWidth - 2 * SheetPad;
            float y = SheetPad + 48 + RowGap;   // below the heading
            for (int i = 0; i < held.Count; i++)
            {
                if (i == heldRows.Count) heldRows.Add(NewRow(i));
                var row = heldRows[i];
                var o = held[i];
                row.Root.gameObject.SetActive(true);
                row.Glyph.sprite = UiGlyphs.Get("upgrade." + o.Id);
                row.Name.text = $"{UpgradeInfo.Name(o.Id)} {o.Rank}";
                row.Effect.text = UpgradeInfo.Describe(o.Id, o.Rank, t);
                // Sized to its text at the sheet's width, so the sheet is exactly as tall as it needs.
                float effectH = Mathf.Ceil(Ui.TextHeight(row.Effect, inner));
                row.Effect.rectTransform.sizeDelta = new Vector2(inner, effectH);
                float rowH = 48 + effectH;
                Ui.Place(row.Root, new Vector2(0, 1), new Vector2(SheetPad, -y), new Vector2(inner, rowH));
                y += rowH + RowGap;
            }
            for (int i = held.Count; i < heldRows.Count; i++) heldRows[i].Root.gameObject.SetActive(false);
            heldSheet.rectTransform.sizeDelta = new Vector2(SheetWidth, y - RowGap + SheetPad);
        }

        HeldRow NewRow(int i)
        {
            var r = new HeldRow { Root = Ui.Rect("Held" + i, heldSheet.transform) };
            r.Glyph = Ui.Image("Glyph", r.Root, Color.white);
            r.Glyph.preserveAspect = true;
            r.Glyph.raycastTarget = false;
            Ui.Place(r.Glyph.rectTransform, new Vector2(0, 1), new Vector2(0, -4), new Vector2(40, 40));
            r.Name = UiKit.Text("Name", r.Root, "", UiFonts.Role.Body, TextAnchor.MiddleLeft);
            r.Name.color = UiPalette.Ink;
            Ui.Place(r.Name.rectTransform, new Vector2(0, 1), new Vector2(52, 0), new Vector2(SheetWidth - 2 * SheetPad - 52, 48));
            r.Effect = UiKit.Text("Effect", r.Root, "", UiFonts.Role.Small, TextAnchor.UpperLeft);
            r.Effect.color = UiPalette.ParchSoft;
            Ui.Place(r.Effect.rectTransform, new Vector2(0, 1), new Vector2(0, -48), new Vector2(SheetWidth - 2 * SheetPad, 40));
            return r;
        }

        public void Show(bool on)
        {
            gameObject.SetActive(on);
            foreach (var (text, label) in labelled) text.text = label();
        }
    }
}
