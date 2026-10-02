using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The launch screen (Phase 8, replacing D43's in-run mode switch). Entries are keyed so
    /// later phases can switch on what they add (endless, tree, styles, achievements) without
    /// rebuilding the layout. A disabled entry stays visible and greyed, with the reason in its
    /// label, so the player can see what exists and what does not yet.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        Text profileLine, warningLine;
        readonly Dictionary<string, Button> entries = new Dictionary<string, Button>();
        readonly Dictionary<string, string> baseLabels = new Dictionary<string, string>();
        // Looked up at click time, so a later phase can switch a disabled entry on with its action.
        readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();
        RectTransform panel;
        Button first;

        public bool IsOpen => gameObject.activeSelf;

        public static MainMenu Create(Canvas canvas)
        {
            // Opaque-ish backdrop: the idle arena shows through faintly behind the title.
            var dim = Ui.Image("MainMenu", canvas.transform, new Color(0.02f, 0.01f, 0.05f, 0.82f));
            Ui.Stretch(dim.rectTransform);
            var menu = dim.gameObject.AddComponent<MainMenu>();
            menu.Build(dim.rectTransform);
            return menu;
        }

        void Build(RectTransform root)
        {
            var title = Ui.Label("Title", root, "BORROWED HEX", 96);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1400, 120));
            title.color = Ui.Accent;
            title.fontStyle = FontStyle.Bold;
            var sub = Ui.Label("Subtitle", root, "Everything is temporary. Even the spells you steal.", 28);
            Ui.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(1400, 40));
            sub.color = new Color(1, 1, 1, 0.7f);

            profileLine = Ui.Label("Profile", root, "", 24);
            Ui.Place(profileLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(1400, 34));
            profileLine.color = new Color(0.75f, 0.95f, 1f);
            warningLine = Ui.Label("Warning", root, "", 22);
            Ui.Place(warningLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1600, 60));
            warningLine.color = new Color(1f, 0.55f, 0.45f);

            var p = Ui.Image("Panel", root, Ui.Panel);
            panel = p.rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(560, 0));
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel, 12);
            col.padding = new RectOffset(36, 36, 28, 28);
        }

        /// <summary>Add an entry in display order. <paramref name="onClick"/> null = visibly disabled.</summary>
        public Button AddEntry(string key, string label, Action onClick, string disabledReason = null)
        {
            actions[key] = onClick;
            var b = Ui.Sized(Ui.Button(key, panel, label, () => { if (actions.TryGetValue(key, out var a)) a?.Invoke(); }, 30), 64);
            entries[key] = b;
            baseLabels[key] = label;
            if (first == null) first = b;
            SetEntry(key, onClick != null, disabledReason);
            return b;
        }

        /// <summary>Enable or grey out an entry; a disabled entry shows why (e.g. "later build").</summary>
        public void SetEntry(string key, bool enabled, string disabledReason = null)
        {
            if (!entries.TryGetValue(key, out var b)) return;
            b.interactable = enabled;
            var text = b.GetComponentInChildren<Text>();
            text.text = enabled || string.IsNullOrEmpty(disabledReason) ? baseLabels[key] : $"{baseLabels[key]}  <size=20>({disabledReason})</size>";
            text.color = enabled ? Ui.Ink : new Color(1, 1, 1, 0.35f);
        }

        /// <summary>Give a (disabled) entry its action and switch it on.</summary>
        public void EnableEntry(string key, Action onClick)
        {
            actions[key] = onClick;
            SetEntry(key, onClick != null);
        }

        /// <summary>Invoke an entry as if clicked (tests, keyboard shortcuts); a disabled entry does nothing.</summary>
        public void Press(string key)
        {
            if (IsEntryEnabled(key) && actions.TryGetValue(key, out var a)) a?.Invoke();
        }

        public bool IsEntryEnabled(string key) => entries.TryGetValue(key, out var b) && b.interactable;

        public void SetProfileLine(string s) => profileLine.text = s;
        public void SetWarning(string s) => warningLine.text = s ?? "";

        public void Show(bool on)
        {
            gameObject.SetActive(on);
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(on && first != null ? first.gameObject : null);
        }
    }
}
