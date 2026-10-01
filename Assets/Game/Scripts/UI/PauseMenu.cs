using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Modal pause panel. The full-screen dimmer is a raycast target on purpose: while it is
    /// up, clicks cannot fall through to the HUD or be read as a gameplay catch.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        Button resume;
        public bool IsOpen => gameObject.activeSelf;

        public static PauseMenu Create(Canvas canvas, Action onResume, Action onRestart, Action onQuit)
        {
            var dim = Ui.Image("PauseMenu", canvas.transform, new Color(0, 0, 0, 0.6f));
            Ui.Stretch(dim.rectTransform);
            var menu = dim.gameObject.AddComponent<PauseMenu>();

            var panel = Ui.Image("Panel", dim.transform, Ui.Panel);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 0));
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel.rectTransform, 18);
            col.padding = new RectOffset(40, 40, 36, 40);

            var title = Ui.Label("Title", panel.transform, "PAUSED", 56);
            title.color = Ui.Accent;
            Ui.Sized(title, 80);
            menu.resume = Ui.Sized(Ui.Button("Resume", panel.transform, "Resume", onResume), 72);
            Ui.Sized(Ui.Button("Restart", panel.transform, "Restart run", onRestart), 72);
            // Quit is omitted on WebGL (closing a tab is the browser's job) by passing null.
            if (onQuit != null) Ui.Sized(Ui.Button("Quit", panel.transform, "Quit", onQuit), 72);
            var hint = Ui.Label("Hint", panel.transform, "Esc / P to resume", 22);
            hint.color = new Color(1, 1, 1, 0.6f);
            Ui.Sized(hint, 30);

            dim.gameObject.SetActive(false);
            return menu;
        }

        public void Show(bool on)
        {
            gameObject.SetActive(on);
            // Keyboard users land on Resume; clearing on close stops Enter re-triggering it.
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(on ? resume.gameObject : null);
        }
    }
}
