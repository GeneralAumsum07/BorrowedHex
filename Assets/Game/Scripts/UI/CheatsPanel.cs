using System;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The Cheats panel, opened from the main menu. Same shape as <see cref="SettingsPanel"/>:
    /// every entry is a button that flips its value, so mouse, keyboard navigation and a
    /// browser all work alike. It holds no rules: the toggles are actions GameRoot passes in,
    /// and the labels read <see cref="Cheats"/> directly so they cannot drift from the state.
    /// </summary>
    public sealed class CheatsPanel : MonoBehaviour
    {
        Action onInvincible, onUnlockAll, onBack;
        Text invincible, unlockAll;
        Button first;

        public bool IsOpen => gameObject.activeSelf;

        public static CheatsPanel Create(Canvas canvas)
        {
            var dim = Ui.Image("Cheats", canvas.transform, new Color(0, 0, 0, 0.75f));
            Ui.Stretch(dim.rectTransform);
            var cp = dim.gameObject.AddComponent<CheatsPanel>();
            cp.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return cp;
        }

        void Build(RectTransform root)
        {
            var p = Ui.Image("Panel", root, Ui.Panel);
            Ui.Place(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 0));
            p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(p.rectTransform, 14);
            col.padding = new RectOffset(40, 40, 32, 36);
            var title = Ui.Sized(Ui.Label("Title", p.transform, "CHEATS", 52), 72);
            title.color = Ui.Accent;
            first = Ui.Sized(Ui.Button("Invincible", p.transform, "", () => Flip(onInvincible), 26), 62);
            invincible = first.GetComponentInChildren<Text>();
            unlockAll = Ui.Sized(Ui.Button("UnlockAll", p.transform, "", () => Flip(onUnlockAll), 26), 62).GetComponentInChildren<Text>();
            // Said once here so the toggles need no fine print of their own.
            var note = Ui.Sized(Ui.Label("Note", p.transform,
                "For this session only. While any cheat is on, runs give no XP, records or achievements.", 20), 56);
            note.color = new Color(1, 1, 1, 0.6f);
            Ui.Sized(Ui.Button("Back", p.transform, "Back", () => onBack?.Invoke(), 28), 66);
        }

        public void Show(Action toggleInvincible, Action toggleUnlockAll, Action back)
        {
            onInvincible = toggleInvincible;
            onUnlockAll = toggleUnlockAll;
            onBack = back;
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(first.gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        void Flip(Action a)
        {
            a?.Invoke();
            Refresh();
        }

        void Refresh()
        {
            invincible.text = "Invincibility: " + (Cheats.Invincible ? "On" : "Off");
            unlockAll.text = "Unlock all skill tree nodes: " + (Cheats.UnlockAllNodes ? "On" : "Off");
        }
    }
}
