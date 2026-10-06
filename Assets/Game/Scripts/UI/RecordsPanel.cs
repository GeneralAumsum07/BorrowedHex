using System;
using System.Collections.Generic;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Records and Achievements (spec 2, plan Task 11), one ornate screen with two tabs. Records
    /// are aligned rows (mode, style, best, time) that expand into a parchment Details block, so
    /// seed and build stay available without crowding the list; Achievements filter by earned
    /// state with the completion count shown once. Every string comes from <see cref="RecordRows"/>.
    /// </summary>
    public sealed class RecordsPanel : MonoBehaviour
    {
        public const int RecordsTab = 0, AchievementsTab = 1;

        UiKit.TabStrip tabs, filters;
        Button back;
        RectTransform recordsView, achievementsView, recordsList, achievementsList;
        Text totals, completion, empty;
        Action onBack;
        PlayerProfile profile;
        AchievementFilter filter = AchievementFilter.All;
        // At most one record open at a time: the list stays scannable.
        GameObject openDetails;
        readonly List<Button> rows = new List<Button>();

        public bool IsOpen => gameObject.activeSelf;
        public int Tab => tabs.Selected;
        /// <summary>What the ScreenStack focuses: the first row when there is one, else the tab.</summary>
        public GameObject DefaultFocus => tabs.Selected == RecordsTab && rows.Count > 0 ? rows[0].gameObject : tabs.Buttons[Mathf.Max(0, tabs.Selected)].gameObject;

        // Column x positions and widths inside a row (row width 1488 after the scroll track).
        static readonly float[] ColX = { 32, 352, 672, 1208 };
        static readonly float[] ColW = { 288, 288, 504, 248 };

        public static RecordsPanel Create(Canvas canvas)
        {
            var scrim = Ui.Image("Records", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var p = scrim.gameObject.AddComponent<RecordsPanel>();
            p.Build(scrim.transform);
            scrim.gameObject.SetActive(false);
            return p;
        }

        void Build(Transform root)
        {
            // The same shell as the Character screen: ornate frame, a 120 band with the tabs
            // centred and the close icon right, the view below.
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 960));
            var band = Ui.Rect("Band", frame.transform);
            band.anchorMin = new Vector2(0, 1); band.anchorMax = new Vector2(1, 1); band.pivot = new Vector2(0.5f, 1);
            band.sizeDelta = new Vector2(0, 120); band.anchoredPosition = Vector2.zero;
            tabs = UiKit.Tabs(band, new[] { "Records", "Achievements" }, ShowTab);
            Ui.Place(tabs.Rect, new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(tabs.Width, 64));
            back = UiKit.Button("Back", band, "", () => onBack?.Invoke(), UiKit.Tier.Icon);
            var cs = UiSkin.Sprite("btn.close");
            if (cs != null) { var img = back.GetComponent<Image>(); img.sprite = cs; img.type = Image.Type.Simple; }
            else back.GetComponentInChildren<Text>().text = "×";
            Ui.Place((RectTransform)back.transform, new Vector2(1, 1), new Vector2(-48, -32), new Vector2(56, 56));
            UiTooltip.Attach(back, () => "Back");

            var body = Ui.Stretch(Ui.Rect("Body", frame.transform));
            body.offsetMin = new Vector2(32, 32); body.offsetMax = new Vector2(-32, -128);
            BuildRecords(body);
            BuildAchievements(body);
        }

        void BuildRecords(RectTransform body)
        {
            recordsView = Ui.Stretch(Ui.Rect("RecordsView", body));
            // Column heads in Small Muted, aligned with the row columns below.
            var head = Ui.Rect("Head", recordsView);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1);
            head.sizeDelta = new Vector2(0, 40);
            string[] heads = { "Mode", "Style", "Best", "Time" };
            for (int i = 0; i < 4; i++) Column(head, "H" + i, heads[i], i, UiFonts.Role.Small, UiPalette.Muted);

            var scroll = UiKit.ScrollView("List", recordsView, out _);
            var sr = (RectTransform)scroll.parent.parent;
            Ui.Stretch(sr); sr.offsetMin = new Vector2(0, 72); sr.offsetMax = new Vector2(0, -48);
            recordsList = scroll;

            empty = UiKit.Text("Empty", recordsView, "No finished runs yet. Your first run will be written here.", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            empty.color = UiPalette.Muted;
            Ui.Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 80));

            // Lifetime totals on a fixed strip: always visible however long the list grows.
            var strip = Ui.Image("Totals", recordsView, UiPalette.PanelDeep);
            strip.rectTransform.anchorMin = Vector2.zero; strip.rectTransform.anchorMax = new Vector2(1, 0);
            strip.rectTransform.pivot = new Vector2(0.5f, 0); strip.rectTransform.sizeDelta = new Vector2(0, 56);
            totals = UiKit.Text("Text", strip.transform, "", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            Ui.Stretch(totals.rectTransform);
        }

        void BuildAchievements(RectTransform body)
        {
            achievementsView = Ui.Stretch(Ui.Rect("AchievementsView", body));
            filters = UiKit.Tabs(achievementsView, new[] { "All", "Earned", "Locked" }, i => { filter = (AchievementFilter)i; FillAchievements(); });
            Ui.Place(filters.Rect, new Vector2(0, 1), new Vector2(0, -8), new Vector2(filters.Width, 64));
            // Completion once, in the header: not repeated per filter or per entry.
            completion = UiKit.Text("Completion", achievementsView, "", UiFonts.Role.Sub, TextAnchor.MiddleRight);
            Ui.Place(completion.rectTransform, new Vector2(1, 1), new Vector2(-24, -12), new Vector2(280, 56));
            var scroll = UiKit.ScrollView("List", achievementsView, out _);
            var sr = (RectTransform)scroll.parent.parent;
            Ui.Stretch(sr); sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0, -80);
            achievementsList = scroll;
        }

        static Text Column(Transform row, string name, string text, int i, UiFonts.Role role, Color color)
        {
            // Time is right-aligned so durations line up on their last digit.
            var t = UiKit.Text(name, row, text, role, i == 3 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            t.color = color;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(ColX[i], 0); rt.sizeDelta = new Vector2(ColW[i], 0);
            return t;
        }

        public void Show(PlayerProfile p, Action backAction)
        {
            profile = p;
            onBack = backAction;
            FillRecords();
            if (tabs.Selected < 0) tabs.Select(RecordsTab); else ShowTab(tabs.Selected);
            if (filters.Selected < 0) filters.Select(0); else FillAchievements();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void ShowTab(int i)
        {
            if (tabs.Selected != i) { tabs.Select(i); return; }   // Select calls back here
            recordsView.gameObject.SetActive(i == RecordsTab);
            achievementsView.gameObject.SetActive(i == AchievementsTab);
        }

        static void Clear(RectTransform list)
        {
            // Detach before Destroy: Destroy is deferred to the frame end, and the layout must
            // not count the old children in the meantime.
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i).gameObject;
                c.transform.SetParent(null, false);
                Destroy(c);
            }
        }

        void FillRecords()
        {
            Clear(recordsList);
            rows.Clear();
            openDetails = null;
            empty.gameObject.SetActive(profile.records.Count == 0);
            totals.text = RecordRows.Summary(profile.stats);
            foreach (var r in profile.records)
            {
                var data = RecordRows.Row(r);
                var card = UiKit.Frame("Row", recordsList, UiKit.FrameKind.Card);
                Ui.Sized(card, 64);
                var b = card.gameObject.AddComponent<Button>();
                Column(card.transform, "Mode", data.Mode, 0, UiFonts.Role.Body, UiPalette.Ivory);
                Column(card.transform, "Style", data.Style, 1, UiFonts.Role.Body, UiPalette.Ivory);
                Column(card.transform, "Value", data.Value, 2, UiFonts.Role.Body, UiPalette.Honey);
                Column(card.transform, "Time", data.Duration, 3, UiFonts.Role.Body, UiPalette.Ivory);

                // Details sits right after its row in the column, hidden until the row is pressed.
                var sheet = UiKit.Frame("Details", recordsList, UiKit.FrameKind.Parchment);
                Ui.Sized(sheet, 152);
                var text = UiKit.Text("Text", sheet.transform, data.Details, UiFonts.Role.Small, TextAnchor.UpperLeft);
                text.color = UiPalette.Ink;   // dark ink: the light palette vanishes on parchment
                Ui.Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(32, 16); text.rectTransform.offsetMax = new Vector2(-32, -16);
                sheet.gameObject.SetActive(false);
                b.onClick.AddListener(() => Toggle(sheet.gameObject));
                rows.Add(b);
            }
            // Wire the rows to the tab strip above them for keyboard travel.
            for (int i = 0; i < rows.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? rows[i - 1] : tabs.Buttons[RecordsTab],
                    selectOnDown = i + 1 < rows.Count ? rows[i + 1] : null };
                rows[i].navigation = nav;
            }
        }

        void Toggle(GameObject sheet)
        {
            bool open = !sheet.activeSelf;
            if (openDetails != null && openDetails != sheet) openDetails.SetActive(false);
            sheet.SetActive(open);
            openDetails = open ? sheet : null;
        }

        void FillAchievements()
        {
            if (profile == null) return;
            Clear(achievementsList);
            completion.text = RecordRows.Completion(profile);
            completion.color = UiPalette.Honey;
            foreach (var (def, earned) in RecordRows.Achievements(profile, filter))
            {
                var card = UiKit.Frame("Entry", achievementsList, UiKit.FrameKind.Card);
                Ui.Sized(card, 104);
                // Emblem: a lit gem when earned, a dim one when not; the word says it too.
                var gem = Ui.Image("Emblem", card.transform, earned ? Color.white : new Color(1, 1, 1, 0.35f));
                var gs = UiSkin.Sprite(earned ? "gem.1" : "gem.4");
                if (gs != null) gem.sprite = gs;
                gem.raycastTarget = false;
                Ui.Place(gem.rectTransform, new Vector2(0, 0.5f), new Vector2(48, 0), new Vector2(40, 44));
                var name = UiKit.Text("Name", card.transform, def.Name, UiFonts.Role.Sub, TextAnchor.MiddleLeft);
                name.color = earned ? UiPalette.Honey : UiPalette.Ivory;
                Ui.Place(name.rectTransform, new Vector2(0, 1), new Vector2(112, -12), new Vector2(1040, 40));
                name.rectTransform.pivot = new Vector2(0, 1);
                var cond = UiKit.Text("Condition", card.transform, def.Description, UiFonts.Role.Body, TextAnchor.MiddleLeft);
                cond.color = earned ? UiPalette.Ivory : UiPalette.Muted;
                Ui.Place(cond.rectTransform, new Vector2(0, 0), new Vector2(112, 12), new Vector2(1120, 44));
                cond.rectTransform.pivot = new Vector2(0, 0);
                if (earned)
                {
                    var check = Ui.Image("Check", card.transform, Color.white);
                    check.sprite = UiGlyphs.Get("state.check"); check.raycastTarget = false;
                    Ui.Place(check.rectTransform, new Vector2(1, 0.5f), new Vector2(-200, 0), new Vector2(36, 36));
                    var word = UiKit.Text("Earned", card.transform, "Earned", UiFonts.Role.Body, TextAnchor.MiddleLeft);
                    word.color = UiPalette.Honey;
                    Ui.Place(word.rectTransform, new Vector2(1, 0.5f), new Vector2(-48, 0), new Vector2(136, 44));
                }
            }
        }
    }
}
