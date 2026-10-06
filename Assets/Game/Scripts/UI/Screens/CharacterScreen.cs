using System;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The Character screen (plan Task 8): one ornate frame holding the mastery header, two tabs
    /// (Skills, Capture style) and a Body the two views parent into. It replaced two separate
    /// full-screen panels because both answer the same question, "what will my next run be",
    /// and the mastery header belongs above both.
    ///
    /// The screen owns no profile logic. GameRoot listens to <see cref="TabChanged"/> and shows
    /// the matching view; only the active tab's view is active, so each view's
    /// activeInHierarchy-based IsOpen stays truthful.
    /// </summary>
    public sealed class CharacterScreen : MonoBehaviour
    {
        public const int SkillsTab = 0, StyleTab = 1;
        /// <summary>The last tab shown, so returning to the screen lands where the player left (session only).</summary>
        public static int LastTab = SkillsTab;

        UiKit.TabStrip tabs;
        Text level, xpText, points;
        Image xpFill;
        Button back;

        public RectTransform Body { get; private set; }
        /// <summary>Raised with the tab index whenever a tab is shown, including on Open.</summary>
        public event Action<int> TabChanged;
        public event Action Back;
        /// <summary>Set by GameRoot: the active view's own default focus. The Back icon otherwise.</summary>
        public Func<GameObject> ViewFocus;
        public GameObject DefaultFocus => ViewFocus?.Invoke() ?? back.gameObject;
        public int Tab => tabs.Selected;

        public static CharacterScreen Create(Canvas canvas)
        {
            var scrim = Ui.Image("Character", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var s = scrim.gameObject.AddComponent<CharacterScreen>();
            s.Build(scrim.transform);
            scrim.gameObject.SetActive(false);
            return s;
        }

        void Build(Transform root)
        {
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 960));
            var band = Ui.Rect("Band", frame.transform);
            band.anchorMin = new Vector2(0, 1); band.anchorMax = new Vector2(1, 1); band.pivot = new Vector2(0.5f, 1);
            band.sizeDelta = new Vector2(0, 120); band.anchoredPosition = Vector2.zero;

            // Left: what the player has earned. Level large, the bar and its numbers under it.
            level = UiKit.Text("Level", band, "", UiFonts.Role.Heading);
            Ui.Place(level.rectTransform, new Vector2(0, 1), new Vector2(64, -20), new Vector2(480, 56));
            var tray = Ui.Image("Xp", band, UiPalette.PanelDeep);
            var ts = UiSkin.Sprite("bar.tray");
            if (ts != null) { tray.sprite = ts; tray.type = Image.Type.Sliced; tray.color = Color.white; }
            Ui.Place(tray.rectTransform, new Vector2(0, 1), new Vector2(64, -80), new Vector2(220, 26));
            // The fill grows by anchorMax.x, not Image.fillAmount: it works on the flat fallback too.
            xpFill = Ui.Image("Fill", tray.transform, UiPalette.Honey);
            var fs = UiSkin.Sprite("fill.green");
            if (fs != null) { xpFill.sprite = fs; xpFill.type = Image.Type.Sliced; xpFill.color = Color.white; }
            xpFill.rectTransform.anchorMin = Vector2.zero; xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.offsetMin = new Vector2(4, 4); xpFill.rectTransform.offsetMax = new Vector2(-4, -4);
            // Bar and numbers end before the centred tabs begin (Task 8 capture: XP ran under them).
            xpText = UiKit.Text("XpText", band, "", UiFonts.Role.Small);
            Ui.Place(xpText.rectTransform, new Vector2(0, 1), new Vector2(296, -72), new Vector2(180, 40));

            // Middle: the two views.
            tabs = UiKit.Tabs(band, new[] { "Skills", "Capture style" }, i => { LastTab = i; TabChanged?.Invoke(i); });
            Ui.Place(tabs.Rect, new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(tabs.Width, 64));

            // Right: what can be spent, and the way out.
            points = UiKit.Text("Points", band, "", UiFonts.Role.Body, TextAnchor.MiddleRight);
            points.color = UiPalette.Honey;
            Ui.Place(points.rectTransform, new Vector2(1, 1), new Vector2(-152, -40), new Vector2(320, 40));
            back = UiKit.Button("Back", band, "", () => Back?.Invoke(), UiKit.Tier.Icon);
            var cs = UiSkin.Sprite("btn.close");
            if (cs != null) { var img = back.GetComponent<Image>(); img.sprite = cs; img.type = Image.Type.Simple; }
            else back.GetComponentInChildren<Text>().text = "×";
            Ui.Place((RectTransform)back.transform, new Vector2(1, 1), new Vector2(-48, -32), new Vector2(56, 56));
            UiTooltip.Attach(back, () => "Back");

            Body = Ui.Rect("Body", frame.transform);
            Ui.Stretch(Body);
            Body.offsetMin = new Vector2(32, 24); Body.offsetMax = new Vector2(-32, -128);   // 1536x808: the Accord map + inspector
        }

        /// <summary>
        /// Show a tab. Raises TabChanged even when that tab is already selected: TabStrip.Select
        /// ignores a repeat, but GameRoot needs the call to (re)show the view with fresh data.
        /// </summary>
        public void Open(int tab)
        {
            if (tabs.Selected == tab) { LastTab = tab; TabChanged?.Invoke(tab); }
            else tabs.Select(tab);
        }

        public void SetHeader(MasteryState m)
        {
            level.text = $"Mastery {m.level}";
            bool max = m.level >= Mastery.MaxLevel;
            int cost = max ? 1 : Mastery.CostToAdvance(m.level);
            xpText.text = max ? "Max level" : $"{m.xp} / {cost} XP";
            xpFill.rectTransform.anchorMax = new Vector2(max ? 1f : Mathf.Clamp01((float)m.xp / cost), 1);
            points.text = $"◆ {m.points} skill point{(m.points == 1 ? "" : "s")}";
        }
    }
}
