using System;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// A small framed menu: Tutorial and Practice (spec "Training opens a small menu"). They left
    /// the main column because they are visited once or rarely, and the column is for the loop
    /// a returning player repeats: Play, Character, Records.
    /// </summary>
    public sealed class TrainingMenu : MonoBehaviour
    {
        Button tutorial, practice, back;
        // A first-time player is most likely here for the tutorial, so it takes focus when it can.
        public GameObject DefaultFocus => tutorial.interactable ? tutorial.gameObject : practice.gameObject;
        public event Action Back;

        public static TrainingMenu Create(Canvas canvas, Action<string> press)
        {
            // A full-screen scrim: it dims the main menu behind and swallows clicks meant for it.
            var scrim = Ui.Image("TrainingMenu", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var m = scrim.gameObject.AddComponent<TrainingMenu>();
            var frame = UiKit.Frame("Panel", scrim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 400));
            var head = UiKit.Text("Heading", frame.transform, "Training", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 64));
            // Keys, not methods: MainMenu.Press applies the same enabled check as every other entry.
            m.tutorial = UiKit.Button("Tutorial", frame.transform, "Tutorial", () => press("tutorial"));
            Ui.Place((RectTransform)m.tutorial.transform, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(480, 64));
            m.practice = UiKit.Button("Practice", frame.transform, "Practice", () => press("practice"));
            Ui.Place((RectTransform)m.practice.transform, new Vector2(0.5f, 1), new Vector2(0, -208), new Vector2(480, 64));
            m.back = UiKit.Button("Back", frame.transform, "Back", () => m.Back?.Invoke(), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)m.back.transform, new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(240, 56));
            scrim.gameObject.SetActive(false);
            return m;
        }

        public void SetEnabled(bool tut, bool prac) { tutorial.interactable = tut; practice.interactable = prac; }
    }
}
