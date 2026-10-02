using System;
using System.Text;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Achievements and personal records (Phase 10), read-only, opened from the main menu.
    /// Two text columns: every achievement (earned in gold, the rest dimmed with their
    /// condition, so the player knows what to try) and the best result per mode and style.
    /// </summary>
    public sealed class RecordsPanel : MonoBehaviour
    {
        Text achievementsText, recordsText;
        Button back;
        Action onBack;

        public bool IsOpen => gameObject.activeSelf;

        public static RecordsPanel Create(Canvas canvas)
        {
            var dim = Ui.Image("Records", canvas.transform, new Color(0, 0, 0, 0.85f));
            Ui.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<RecordsPanel>();
            p.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return p;
        }

        void Build(RectTransform root)
        {
            var panel = Ui.Image("Panel", root, Ui.Panel).rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 900));
            var title = Ui.Label("Title", panel, "ACHIEVEMENTS & RECORDS", 52);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1400, 64));
            title.color = Ui.Accent;

            achievementsText = Ui.Label("Achievements", panel, "", 23, TextAnchor.UpperLeft);
            Ui.Place(achievementsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-370, -460), new Vector2(720, 680));
            achievementsText.supportRichText = true;
            recordsText = Ui.Label("Records", panel, "", 23, TextAnchor.UpperLeft);
            Ui.Place(recordsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(390, -460), new Vector2(680, 680));
            recordsText.supportRichText = true;

            back = Ui.Button("Back", panel, "Back", () => onBack?.Invoke(), 26);
            Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(300, 60));
        }

        public void Show(PlayerProfile p, Action backAction)
        {
            onBack = backAction;
            achievementsText.text = AchievementsBody(p);
            recordsText.text = RecordsBody(p);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(back.gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        public static string AchievementsBody(PlayerProfile p)
        {
            int got = 0;
            foreach (var a in Achievements.All) if (Achievements.Has(p, a.Id)) got++;
            var sb = new StringBuilder();
            sb.Append($"<b>ACHIEVEMENTS</b>  {got} / {Achievements.All.Count}\n\n");
            foreach (var a in Achievements.All)
            {
                bool has = Achievements.Has(p, a.Id);
                sb.Append(has ? $"<color=#FAD150>{a.Name}  (earned)</color>" : $"<color=#8A8496>{a.Name}</color>");
                sb.Append($"\n<size=19><color=#{(has ? "D8D2E6" : "8A8496")}>   {a.Description}</color></size>\n");
            }
            return sb.ToString();
        }

        public static string RecordsBody(PlayerProfile p)
        {
            var sb = new StringBuilder();
            sb.Append("<b>PERSONAL RECORDS</b>\n\n");
            if (p.records.Count == 0) sb.Append("<color=#8A8496>No finished runs yet.</color>\n");
            foreach (var r in p.records)
            {
                string what = r.kind == Records.LongestRun ? $"survived {FormatTime(r.duration)}" : $"score {r.score}";
                sb.Append($"<color=#FAD150>{r.mode} · {Style(r.styleId)}</color>: {what}\n");
                string passives = r.passives != null && r.passives.Count > 0 ? string.Join(", ", r.passives) : "no passives";
                sb.Append($"<size=19><color=#B8B0C8>   {r.reason}, {r.kills} kills, {FormatTime(r.duration)}, mastery {r.masteryLevel}, {passives}\n" +
                          $"   seed {r.seed}, build {r.buildVersion}</color></size>\n");
            }
            var st = p.stats;
            sb.Append($"\n<b>TOTALS</b>\n{st.runs} runs, {st.victories} wins, {st.kills} kills, {st.bossesDefeated} bosses\n" +
                      $"{st.perfectShots} perfect shots, {st.backfires} backfires, {FormatTime(st.secondsPlayed)} played");
            return sb.ToString();
        }

        static string Style(string id) => string.IsNullOrEmpty(id) ? "Snatcher" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }
}
