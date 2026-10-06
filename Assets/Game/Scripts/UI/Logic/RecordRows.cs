using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Progression;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>One record as the Records tab lays it out: four aligned columns plus a Details block.</summary>
    public struct RecordRow { public string Mode, Style, Value, Duration, Details; }

    public enum AchievementFilter { All, Earned, Locked }

    /// <summary>
    /// What the Records and Achievements tabs show (spec 2): the essentials aligned up front,
    /// metadata (seed, build) only in Details, and skills by readable name instead of internal id.
    /// Pure, so every wording rule is tested without a canvas.
    /// </summary>
    public static class RecordRows
    {
        public static RecordRow Row(RunRecord r)
        {
            // An id with no node (a skill removed or renamed since the run) is shown raw rather
            // than dropped: the record should still say the run had something there.
            string skills = r.passives == null || r.passives.Count == 0 ? "none"
                : string.Join(", ", r.passives.Select(id => SkillTree.Find(id)?.Name ?? id));
            return new RecordRow
            {
                Mode = r.mode,
                // Resolve maps a null/unknown id to Snatcher: records from before styles existed
                // were all Snatcher runs.
                Style = CaptureStyles.Resolve(r.styleId).Name,
                Value = r.kind == Records.LongestRun ? $"Survived {FormatTime(r.duration)}" : $"Score {r.score}",
                Duration = FormatTime(r.duration),
                Details = $"{r.reason} · {r.kills} kills · mastery {r.masteryLevel}\nSkills: {skills}\nSeed {r.seed} · build {r.buildVersion}",
            };
        }

        /// <summary>Lifetime totals as one line: the strip above the Records footer.</summary>
        public static string Summary(ProfileStats st) =>
            $"{st.runs} runs · {st.victories} wins · {st.kills} kills · {st.bossesDefeated} bosses · " +
            $"{st.perfectShots} perfect · {st.backfires} backfires · {FormatTime(st.secondsPlayed)} played";

        /// <summary>
        /// The achievements a filter keeps, in definition order. Achievements.Has is the authority
        /// on "earned" (it may dedupe or validate entries), so the profile list is never read directly.
        /// </summary>
        public static List<(AchievementDef def, bool earned)> Achievements(PlayerProfile p, AchievementFilter f) =>
            Progression.Achievements.All.Select(a => (a, Progression.Achievements.Has(p, a.Id)))
                .Where(x => f == AchievementFilter.All || (f == AchievementFilter.Earned) == x.Item2).ToList();

        public static string Completion(PlayerProfile p) =>
            $"{Progression.Achievements.All.Count(a => Progression.Achievements.Has(p, a.Id))} / {Progression.Achievements.All.Count}";

        /// <summary>m:ss, or h:mm:ss from an hour up (lifetime play time gets there).</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }
}
