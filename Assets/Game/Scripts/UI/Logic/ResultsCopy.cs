using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>What the results screen shows about the profile, split by how loudly it says it.</summary>
    public struct ResultsOutcome
    {
        public string XpLine;          // "+46 XP", or null when the run was not recorded
        public float XpFrac;           // the mastery bar's fill AFTER this run
        public string LevelUp;         // "Mastery 3 → 4  ·  +1 skill point", or null
        public List<string> Rewards;   // one row each: new bests, first records, new achievements
        public List<string> Details;   // the XP breakdown and the unchanged record comparisons
        public string Warning;         // "Not saved: …" when the save failed
        public string NotRecorded;     // "Practice run: not recorded" / "Cheats active: not recorded"
    }

    /// <summary>
    /// Results copy (spec 2, Results): outcome first, then XP and rewards; everything else is
    /// folded into Details. Pure, so the wording is tested without a GameRoot. The strings are the
    /// ones GameRoot's FinalizeText* built before, regrouped, not rewritten.
    /// </summary>
    public static class ResultsCopy
    {
        const string Sep = "  ·  ";

        public static ResultsOutcome FromFinalize(FinalizeResult r, MasteryState m, string saveWarning)
        {
            var o = new ResultsOutcome { Rewards = new List<string>(), Details = new List<string>() };
            if (r == null) return o;
            if (!r.Applied)
            {
                // "already finalized" only happens on a replayed finalize: nothing new to say.
                o.NotRecorded = r.SkippedBecause == "sandbox" ? "Practice run: not recorded"
                    : r.SkippedBecause == "debug" ? "Cheats active: not recorded" : null;
                return o;
            }
            if (r.Xp != null)
            {
                var x = r.Xp;
                o.XpLine = $"+{x.Total} XP";
                // Only the non-zero terms, so a short loss reads "kills 4" rather than a wall of zeros.
                var parts = new List<string>();
                if (x.NormalKills > 0) parts.Add($"kills {x.NormalKills * Mastery.XpPerNormalKill}");
                if (x.OverstayedKills > 0) parts.Add($"overstayed {x.OverstayedKills * Mastery.XpPerOverstayedKill}");
                if (x.BossKills > 0) parts.Add($"boss {x.BossKills * Mastery.XpPerBossKill}");
                if (x.Encounters > 0) parts.Add($"encounters {x.Encounters * Mastery.XpPerEncounter}");
                if (x.PerfectHits > 0) parts.Add($"perfect {x.PerfectHits}");
                if (x.ScoreXp > 0) parts.Add($"score {x.ScoreXp}");   // D102
                if (parts.Count > 0) o.Details.Add("XP: " + string.Join(", ", parts));
            }
            m ??= new MasteryState();
            // At max level the bar reads full rather than an empty 0/0 (as the main menu does).
            o.XpFrac = m.level >= Mastery.MaxLevel ? 1f : Mathf.Clamp01(m.xp / (float)Mastery.CostToAdvance(m.level));
            if (r.LevelsGained > 0)
                o.LevelUp = $"Mastery {r.LevelBefore} → {r.LevelAfter}{Sep}+{r.LevelsGained} skill point{(r.LevelsGained == 1 ? "" : "s")}";

            foreach (var id in r.NewAchievements)
                o.Rewards.Add($"Achievement: {Achievements.Find(id)?.Name ?? id}");
            foreach (var rec in r.Records)
            {
                bool time = rec.Kind == Records.LongestRun;
                string label = time ? "Longest run" : "Best score";
                string Fmt(float v) => time ? $"{(int)v / 60}:{(int)v % 60:00}" : ((int)v).ToString();
                // A first record is a reward too: it is the first time the line exists at all.
                if (rec.Previous == null) o.Rewards.Add($"{label}: {Fmt(rec.ThisValue)} (first record)");
                else
                {
                    float prev = time ? rec.Previous.duration : rec.Previous.score;
                    if (rec.IsNewBest) o.Rewards.Add($"New {label.ToLowerInvariant()}: {Fmt(rec.ThisValue)} (was {Fmt(prev)})");
                    else o.Details.Add($"{label}: {Fmt(prev)} (this run {Fmt(rec.ThisValue)})");
                }
            }
            o.Warning = r.Saved ? null : $"Not saved: {saveWarning}";
            return o;
        }

        /// <summary>The headline word and its colour. Sentence case (spec copy rules).</summary>
        public static (string title, Color colour) Title(RunEndReason reason) => reason switch
        {
            RunEndReason.Victory => ("Victory", UiPalette.Honey),
            RunEndReason.Death => ("Defeated", UiPalette.Blood),
            RunEndReason.TimeExpired => ("Out of time", UiPalette.Warning),
            RunEndReason.Retired => ("Retired", UiPalette.Honey),
            _ => (reason.ToString(), UiPalette.Ivory),
        };

        /// <summary>
        /// The line under the title, only when it adds information; null otherwise ("Defeated"
        /// over "You fell" was the spec's example of a line that only repeats the title).
        /// </summary>
        public static string Subtitle(RunEndReason reason, GameMode mode, int cycle, int waves, int bosses, int victoryBonus)
        {
            if (mode == GameMode.Endless)
                return $"Cycle {cycle}{Sep}{waves} wave{(waves == 1 ? "" : "s")}{Sep}{bosses} boss{(bosses == 1 ? "" : "es")}";
            if (victoryBonus > 0) return $"Time bonus +{victoryBonus}";
            return null;
        }
    }
}
