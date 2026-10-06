using System;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// A two-button confirmation over the screen it asks about (plan Task 10, also Task 12's
    /// pause confirms). Cancel is the default focus and Esc cancels: the safe answer is the one
    /// a stray Enter or Esc gives. Pushed as an overlay so the screen under it stays visible.
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        Text title, body;
        Button cancel, confirm;
        ScreenStack stack;
        Action pending;

        public bool IsOpen => gameObject.activeSelf;
        public GameObject DefaultFocus => cancel.gameObject;
        public string Title => title.text;
        public Button ConfirmButton => confirm;

        public static ConfirmDialog Create(Canvas canvas)
        {
            var scrim = Ui.Image("Confirm", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var d = scrim.gameObject.AddComponent<ConfirmDialog>();
            var frame = UiKit.Frame("Panel", scrim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 360));
            d.title = UiKit.Text("Title", frame.transform, "", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(d.title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(640, 64));
            d.body = UiKit.Text("Body", frame.transform, "", UiFonts.Role.Body, TextAnchor.UpperCenter);
            Ui.Place(d.body.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(600, 112));
            // Cancel left, the action right: the reading order ends on the commitment.
            d.cancel = UiKit.Button("Cancel", frame.transform, "Cancel", d.Cancel, UiKit.Tier.Secondary);
            Ui.Place((RectTransform)d.cancel.transform, new Vector2(0.5f, 0), new Vector2(-152, 40), new Vector2(264, 64));
            d.confirm = UiKit.Button("Confirm", frame.transform, "Confirm", d.Accept, UiKit.Tier.Primary);
            Ui.Place((RectTransform)d.confirm.transform, new Vector2(0.5f, 0), new Vector2(152, 40), new Vector2(264, 64));
            scrim.gameObject.SetActive(false);
            return d;
        }

        public void Ask(string titleText, string bodyText, string confirmText, Action onConfirm, ScreenStack screens)
        {
            title.text = titleText;
            body.text = bodyText;
            confirm.GetComponentInChildren<Text>().text = confirmText;
            pending = onConfirm;
            stack = screens;
            stack.PushOverlay(gameObject, () => DefaultFocus, Cancel);
        }

        public void Cancel()
        {
            pending = null;
            if (stack != null && stack.Top == gameObject) stack.Pop();
        }

        // Pop first, then act: the action may itself push or clear screens.
        public void Accept()
        {
            var act = pending;
            pending = null;
            if (stack != null && stack.Top == gameObject) stack.Pop();
            act?.Invoke();
        }
    }
}
