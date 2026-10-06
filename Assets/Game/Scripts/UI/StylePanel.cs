using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The Capture Style tab of the Character screen (Phase 11, compacted in plan Task 8). One
    /// card per style: emblem, name, one trade-off sentence and three aligned numbers, so the
    /// styles compare at a glance. The numbers are the EFFECTIVE ones the next run will use
    /// (style and owned passives resolved through the same Loadout call the run makes), so a
    /// card cannot drift from play. The long text lives behind Details. Clicking a card selects
    /// it and saves at once (like the tree).
    /// </summary>
    public sealed class StylePanel : MonoBehaviour
    {
        public struct StyleSummary { public string Name, TradeOff; public (string label, string value)[] Compare; }

        sealed class Card
        {
            public Button Button; public Image Frame; public GameObject Badge; public Text SelectedWord;
            public Text Trade; public Text[] Values;
        }

        PlayerProfile profile;
        GameConfig config;
        Action onChanged, onBack;
        readonly Dictionary<string, Card> cards = new Dictionary<string, Card>();
        GameObject details;
        Text[] detailTexts;

        // activeInHierarchy, not activeSelf: the panel lives inside the Character screen, so it
        // is only "open" while that screen is up AND this tab is the active one.
        public bool IsOpen => gameObject.activeInHierarchy;
        /// <summary>The selected card, so keyboard focus lands on the current choice.</summary>
        public GameObject DefaultFocus => cards[CaptureStyles.Resolve(profile?.styleId).Id].Button.gameObject;

        /// <summary>Built inside <paramref name="host"/> (CharacterScreen.Body), stretched over it.</summary>
        public static StylePanel Create(RectTransform host)
        {
            var root = Ui.Stretch(Ui.Rect("Styles", host));
            var p = root.gameObject.AddComponent<StylePanel>();
            p.Build(root);
            root.gameObject.SetActive(false);
            return p;
        }

        void Build(RectTransform root)
        {
            // Three columns, one per style, in the plan's order (Snatcher, Collector, Daredevil).
            for (int i = 0; i < CaptureStyles.All.Count; i++)
                BuildCard(root, CaptureStyles.All[i], i);

            var more = UiKit.Button("Details", root, "Details", () => details.SetActive(!details.activeSelf), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)more.transform, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 56));
            BuildDetails(root);
        }

        void BuildCard(RectTransform root, CaptureStyle style, int i)
        {
            string id = style.Id;
            var frame = UiKit.Frame($"Style_{id}", root, UiKit.FrameKind.Card);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 1), new Vector2((i - 1) * CardStep, -24), new Vector2(CardWidth, 560));
            // The whole card is the button: a big target, and the focus pointer sits beside it.
            // No colour transition: the frame swap and the badge are the selected state.
            var btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Click(id));
            var c = new Card { Button = btn, Frame = frame, Values = new Text[3] };

            // The style's emblem glyph (UiGlyphs, 48x48) inside the portrait arch.
            var portrait = Ui.Image("Portrait", frame.transform, UiPalette.PanelDeep);
            var ps = UiSkin.Sprite("portrait");
            if (ps != null) { portrait.sprite = ps; portrait.color = Color.white; }
            portrait.raycastTarget = false;
            Ui.Place(portrait.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(132, 144));
            var glyph = UiGlyphs.Get("style." + id);
            if (glyph != null)
            {
                var emblem = Ui.Image("Emblem", portrait.transform, Color.white);
                emblem.sprite = glyph; emblem.raycastTarget = false;
                // Low in the arch: the portrait art's crest fills its top third.
                Ui.Place(emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(48, 48));
            }
            else
            {
                // Fallback for a style added without a glyph: its initial in the display face.
                var emblem = UiKit.Text("Emblem", portrait.transform, style.Name.Substring(0, 1), UiFonts.Role.Heading, TextAnchor.MiddleCenter);
                Ui.Stretch(emblem.rectTransform);
            }

            var name = UiKit.Text("Name", frame.transform, style.Name, UiFonts.Role.Sub, TextAnchor.MiddleCenter);
            Ui.Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(CardWidth - 40, 40));
            c.SelectedWord = UiKit.Text("SelectedWord", frame.transform, "Selected", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            c.SelectedWord.color = UiPalette.Honey;
            Ui.Place(c.SelectedWord.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -216), new Vector2(CardWidth - 40, 40));
            c.Trade = UiKit.Text("TradeOff", frame.transform, style.TradeOff, UiFonts.Role.Body, TextAnchor.UpperCenter);
            Ui.Place(c.Trade.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(CardWidth - 48, 80));
            // A plain camel hairline: the sliced divider.a art broke into a stub at card width
            // (Task 8 capture), and a 2-unit line is all a card needs between text and numbers.
            var div = Ui.Image("Divider", frame.transform, UiPalette.Camel);
            div.raycastTarget = false;
            Ui.Place(div.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -352), new Vector2(360, 2));

            // Labels left, values right, the same rows on every card so the eye reads across.
            var labels = CompareLabels;
            for (int r = 0; r < labels.Length; r++)
            {
                float y = -380 - r * 48;
                var l = UiKit.Text("Label" + r, frame.transform, labels[r], UiFonts.Role.Small);
                Ui.Place(l.rectTransform, new Vector2(0, 1), new Vector2(40, y), new Vector2(160, 40));
                c.Values[r] = UiKit.Text("Value" + r, frame.transform, "", UiFonts.Role.Body, TextAnchor.MiddleRight);
                Ui.Place(c.Values[r].rectTransform, new Vector2(1, 1), new Vector2(-40, y), new Vector2(200, 40));
            }

            // The selected mark: a gem with a tick in the top-right corner (not a green fill, spec).
            var badge = Ui.Image("Badge", frame.transform, UiPalette.Honey);
            var gs = UiSkin.Sprite("gem.1");
            if (gs != null) { badge.sprite = gs; badge.color = Color.white; }
            badge.raycastTarget = false;
            Ui.Place(badge.rectTransform, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(48, 52));
            var tick = UiKit.Text("Tick", badge.transform, "✓", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            tick.color = UiPalette.Ink;
            Ui.Stretch(tick.rectTransform);
            c.Badge = badge.gameObject;
            cards[id] = c;
        }

        void BuildDetails(RectTransform root)
        {
            // A parchment sheet over the cards with every number Describe knows. A toggle inside
            // the tab, not a stacked screen: stacking it would deactivate the Character screen
            // that contains it (Task 8 ruling).
            var sheet = UiKit.Frame("Details", root, UiKit.FrameKind.Parchment);
            Ui.Place(sheet.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1280, 560));
            var content = UiKit.ScrollView("Scroll", sheet.transform, out _);
            var scrollRt = (RectTransform)content.parent.parent;
            Ui.Stretch(scrollRt); scrollRt.offsetMin = new Vector2(40, 88); scrollRt.offsetMax = new Vector2(-40, -32);
            detailTexts = new Text[CaptureStyles.All.Count];
            for (int i = 0; i < detailTexts.Length; i++)
            {
                detailTexts[i] = UiKit.Text("Body" + i, content, "", UiFonts.Role.Body, TextAnchor.UpperLeft);
                detailTexts[i].color = UiPalette.Ink;   // dark ink on parchment
            }
            var close = UiKit.Button("Close", sheet.transform, "Close", () => details.SetActive(false), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 56));
            details = sheet.gameObject;
            details.SetActive(false);
        }

        public void Show(PlayerProfile p, GameConfig c, Action changed, Action backAction)
        {
            profile = p;
            config = c;
            onChanged = changed;
            onBack = backAction;   // the Character screen's Back handles leaving; kept for the API
            details.SetActive(false);
            Refresh();
            gameObject.SetActive(true);
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

        // 440 wide on a 520 step leaves an 80 gap: room for the focus pointer (FocusPointer.Reach)
        // drawn left of a focused card without it landing on the neighbour's text.
        const float CardWidth = 440f, CardStep = 520f;

        static readonly string[] CompareLabels = { "Catch", "Window", "Recovery" };

        /// <summary>
        /// The card's short face: name, one trade-off sentence, three aligned numbers. The rows are
        /// the fields CaptureStyles.Describe prints, in its formats, so card and Details agree.
        /// Daredevil's catch is the dash, so its row values say so instead of showing a cone it
        /// does not use.
        /// </summary>
        public static StyleSummary CardSummary(CaptureStyle style, PlayerProfile p, GameConfig c)
        {
            var st = Loadout.Resolve(c, SkillTree.ActiveNodes(p), style.Id);
            var values = st.CatchIsDash
                ? new[] { $"{st.DashCatchRadius:0.0} m path", $"{st.DashDuration:0.00} s dash", $"{st.DashCooldown:0.00} s shared" }
                : new[] { $"{st.CaptureConeAngle:0}° cone", $"{st.CaptureWindow:0.00} s", $"{Math.Max(st.CaptureRecovery, st.CaptureWindow):0.00} s" };
            var compare = new (string, string)[CompareLabels.Length];
            for (int i = 0; i < compare.Length; i++) compare[i] = (CompareLabels[i], values[i]);
            return new StyleSummary { Name = style.Name, TradeOff = style.TradeOff, Compare = compare };
        }

        /// <summary>The Details text for one style: everything Describe knows. Static for EditMode tests.</summary>
        public static string CardBody(CaptureStyle style, PlayerProfile p, GameConfig c, bool selected)
        {
            var stats = Loadout.Resolve(c, SkillTree.ActiveNodes(p), style.Id);
            return $"{style.Name}{(selected ? "  — Selected" : "")}\n{style.TradeOff}\n\n{style.Summary}\n\n{CaptureStyles.Describe(stats)}";
        }

        void Refresh()
        {
            string current = CaptureStyles.Resolve(profile.styleId).Id;
            var frames = new[] { UiSkin.Sprite("frame.card"), UiSkin.Sprite("frame.cardAlt") };
            for (int i = 0; i < CaptureStyles.All.Count; i++)
            {
                var s = CaptureStyles.All[i];
                var c = cards[s.Id];
                bool sel = s.Id == current;
                // Selected reads three ways: the alternate frame, the gem badge and the word.
                if (frames[0] != null && frames[1] != null) c.Frame.sprite = frames[sel ? 1 : 0];
                else c.Frame.GetComponent<Outline>().effectColor = sel ? UiPalette.Honey : UiPalette.Camel;
                c.Badge.SetActive(sel);
                c.SelectedWord.gameObject.SetActive(sel);
                var sum = CardSummary(s, profile, config);
                for (int r = 0; r < c.Values.Length; r++) c.Values[r].text = sum.Compare[r].value;
                detailTexts[i].text = CardBody(s, profile, config, sel);
            }
        }
    }
}
