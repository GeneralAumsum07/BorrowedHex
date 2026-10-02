using System;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Settings (Phase 8): display mode, UI scale and two readability options. Every entry is a
    /// button that cycles its value, so it works the same with mouse, keyboard navigation and
    /// in a browser. Each change is applied at once and saved (an explicit save point).
    ///
    /// No aim sensitivity: aim follows the absolute cursor position, so a sensitivity value
    /// would change nothing (D74, open question for the owner).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        static readonly float[] Scales = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };

        ProfileSettings settings;
        Action onChanged, onBack;
        Text display, scale, flashes, hints;
        Button first;
        bool allowWindowed;

        public bool IsOpen => gameObject.activeSelf;

        public static SettingsPanel Create(Canvas canvas, bool allowWindowed)
        {
            var dim = Ui.Image("Settings", canvas.transform, new Color(0, 0, 0, 0.75f));
            Ui.Stretch(dim.rectTransform);
            var sp = dim.gameObject.AddComponent<SettingsPanel>();
            sp.allowWindowed = allowWindowed;
            sp.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return sp;
        }

        void Build(RectTransform root)
        {
            var p = Ui.Image("Panel", root, Ui.Panel);
            Ui.Place(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 0));
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(p.rectTransform, 14);
            col.padding = new RectOffset(40, 40, 32, 36);
            var title = Ui.Sized(Ui.Label("Title", p.transform, "SETTINGS", 52), 72);
            title.color = Ui.Accent;
            first = Ui.Sized(Ui.Button("Display", p.transform, "", CycleDisplay, 26), 62);
            display = first.GetComponentInChildren<Text>();
            scale = Ui.Sized(Ui.Button("Scale", p.transform, "", CycleScale, 26), 62).GetComponentInChildren<Text>();
            flashes = Ui.Sized(Ui.Button("Flashes", p.transform, "", () => { settings.reduceFlashes = !settings.reduceFlashes; Changed(); }, 26), 62).GetComponentInChildren<Text>();
            hints = Ui.Sized(Ui.Button("Hints", p.transform, "", () => { settings.showHints = !settings.showHints; Changed(); }, 26), 62).GetComponentInChildren<Text>();
            Ui.Sized(Ui.Button("Back", p.transform, "Back", () => onBack?.Invoke(), 28), 66);
        }

        public void Show(ProfileSettings s, Action changed, Action back)
        {
            settings = s;
            onChanged = changed;
            onBack = back;
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(first.gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        void CycleDisplay()
        {
            // As launched -> fullscreen -> windowed (desktop only) -> as launched.
            settings.displayMode = settings.displayMode switch
            {
                -1 => 1,
                1 => allowWindowed ? 0 : -1,
                _ => -1,
            };
            Changed();
        }

        void CycleScale()
        {
            int i = 0;
            while (i < Scales.Length && Scales[i] <= settings.uiScale + 1e-3f) i++;
            settings.uiScale = i >= Scales.Length ? Scales[0] : Scales[i];
            Changed();
        }

        void Changed()
        {
            Refresh();
            onChanged?.Invoke();
        }

        void Refresh()
        {
            display.text = "Display: " + (settings.displayMode switch { 1 => "Fullscreen", 0 => "Windowed", _ => "As launched" });
            scale.text = $"Interface scale: {Mathf.RoundToInt(settings.uiScale * 100)}%";
            flashes.text = "Reduce flashes: " + (settings.reduceFlashes ? "On" : "Off");
            hints.text = "Control hints: " + (settings.showHints ? "On" : "Off");
        }
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
