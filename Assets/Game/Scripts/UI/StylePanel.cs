using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Capture style choice (Phase 11), opened from the main menu between runs. One card per
    /// style showing the EFFECTIVE numbers the next run will use: style and equipped passives
    /// resolved through the same Loadout call the run itself makes, so the card cannot drift
    /// from play. Clicking a card selects it and saves at once (like the tree).
    /// </summary>
    public sealed class StylePanel : MonoBehaviour
    {
        PlayerProfile profile;
        GameConfig config;
        Action onChanged, onBack;
        readonly Dictionary<string, Text> cardTexts = new Dictionary<string, Text>();
        readonly Dictionary<string, Image> cardFills = new Dictionary<string, Image>();
        Button back;

        static readonly Color Selected = new Color(0.20f, 0.42f, 0.30f, 1f);
        static readonly Color Other = new Color(0.16f, 0.13f, 0.22f, 1f);

        public bool IsOpen => gameObject.activeSelf;

        public static StylePanel Create(Canvas canvas)
        {
            var dim = Ui.Image("Styles", canvas.transform, new Color(0, 0, 0, 0.85f));
            Ui.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<StylePanel>();
            p.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return p;
        }

        void Build(RectTransform root)
        {
            var panel = Ui.Image("Panel", root, Ui.Panel).rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 900));
            var title = Ui.Label("Title", panel, "CAPTURE STYLE", 52);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1400, 64));
            title.color = Ui.Accent;

            // Three columns, one per style, in the plan's order (Snatcher, Collector, Daredevil).
            for (int i = 0; i < CaptureStyles.All.Count; i++)
            {
                string id = CaptureStyles.All[i].Id;
                var btn = Ui.Button($"Style_{id}", panel, "", () => Click(id), 22);
                Ui.Place(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2((i - 1) * 500f, -420f), new Vector2(470, 520));
                var t = btn.GetComponentInChildren<Text>();
                t.supportRichText = true;
                t.alignment = TextAnchor.UpperLeft;
                t.rectTransform.offsetMin = new Vector2(20, 16);
                t.rectTransform.offsetMax = new Vector2(-20, -16);
                cardTexts[id] = t;
                cardFills[id] = btn.GetComponent<Image>();
            }

            var note = Ui.Label("Note", panel, "Every style keeps two slots, the 3 s hex timer, frozen unselected slots and the backfire. Applies from the next run.", 20);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1400, 30));
            note.color = new Color(1, 1, 1, 0.6f);
            back = Ui.Button("Back", panel, "Back", () => onBack?.Invoke(), 26);
            Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(300, 60));
        }

        public void Show(PlayerProfile p, GameConfig c, Action changed, Action backAction)
        {
            profile = p;
            config = c;
            onChanged = changed;
            onBack = backAction;
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(back.gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The card's action; public so tests and keyboard play go through the same path.</summary>
        public void Click(string id)
        {
            if (!CaptureStyles.IsKnown(id) || profile.styleId == id) { Refresh(); return; }
            profile.styleId = id;
            onChanged?.Invoke();
            Refresh();
        }

        /// <summary>Card body for one style; static so EditMode tests can read it without a canvas.</summary>
        public static string CardBody(CaptureStyle style, PlayerProfile p, GameConfig c, bool selected)
        {
            var stats = Loadout.Resolve(c, p.equippedNodes, style.Id);
            string state = selected ? "<color=#8CF0A8>SELECTED</color>" : "Click to select";
            return $"<b><size=30>{style.Name}</size></b>\n<color=#FAD150>{style.TradeOff}</color>\n\n" +
                   $"{style.Summary}\n\n<size=20>{CaptureStyles.Describe(stats)}</size>\n\n<size=20>{state}</size>";
        }

        void Refresh()
        {
            string current = CaptureStyles.Resolve(profile.styleId).Id;
            foreach (var s in CaptureStyles.All)
            {
                bool sel = s.Id == current;
                cardTexts[s.Id].text = CardBody(s, profile, config, sel);
                cardFills[s.Id].color = sel ? Selected : Other;
            }
        }
    }
}
