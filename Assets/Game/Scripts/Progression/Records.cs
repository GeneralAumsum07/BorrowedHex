using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    /// <summary>What the results screen needs to compare this run with the previous best.</summary>
    public sealed class RecordOutcome
    {
        public string Kind;
        /// <summary>The best before this run, or null if this mode/style had none.</summary>
        public RunRecord Previous;
        public bool IsNewBest;
        public float ThisValue;
    }

    /// <summary>
    /// Personal records (section 6): one best per mode, capture style and record kind, with the
    /// metadata the plan lists (build, seed, mastery, passives). Personal, not a leaderboard,
    /// so different builds and loadouts are kept side by side in the metadata, not separated.
    /// </summary>
    public static class Records
    {
        /// <summary>Highest score. Every mode keeps one.</summary>
        public const string BestScore = "score";
        /// <summary>Longest survival (endless only, Phase 12).</summary>
        public const string LongestRun = "survival";

        public static bool IsKnownKind(string k) => k == BestScore || k == LongestRun;

        public static RunRecord Find(PlayerProfile p, string mode, string style, string kind)
        {
            foreach (var r in p.records)
                if (r.mode == mode && r.styleId == style && (r.kind ?? BestScore) == kind) return r;
            return null;
        }

        static float ValueOf(RunRecord r, string kind) => kind == LongestRun ? r.duration : r.score;

        /// <summary>
        /// Submit a finished run under each record kind its mode keeps. A tie does not replace
        /// the earlier record (the first to reach a value holds it).
        /// </summary>
        public static List<RecordOutcome> Submit(PlayerProfile p, RunSummary s, RunSetup setup)
        {
            var outcomes = new List<RecordOutcome>();
            var candidate = FromSummary(s, setup, p.mastery.level);
            foreach (var kind in KindsFor(s.Mode))
            {
                candidate.kind = kind;
                var prev = Find(p, candidate.mode, candidate.styleId, kind);
                var o = new RecordOutcome { Kind = kind, Previous = prev == null ? null : Copy(prev), ThisValue = ValueOf(candidate, kind) };
                if (prev == null || o.ThisValue > ValueOf(prev, kind))
                {
                    if (prev != null) p.records.Remove(prev);
                    p.records.Add(Copy(candidate));
                    o.IsNewBest = true;
                }
                outcomes.Add(o);
            }
            return outcomes;
        }

        static IEnumerable<string> KindsFor(GameMode mode)
        {
            yield return BestScore;
            if (mode == GameMode.Endless) yield return LongestRun;
        }

        static RunRecord FromSummary(RunSummary s, RunSetup setup, int masteryNow) => new RunRecord
        {
            mode = s.Mode.ToString(),
            styleId = string.IsNullOrEmpty(s.StyleId) ? "snatcher" : s.StyleId,
            score = s.Score,
            duration = s.Duration,
            kills = s.Kills,
            reason = s.Reason.ToString(),
            runId = s.RunId,
            seed = s.Seed,
            buildVersion = setup?.BuildVersion ?? "",
            // The level the run was PLAYED at, so a record shows the build that earned it.
            masteryLevel = setup?.MasteryLevel ?? masteryNow,
            passives = setup != null ? new List<string>(setup.PassiveIds) : new List<string>(),
        };

        static RunRecord Copy(RunRecord r) => new RunRecord
        {
            mode = r.mode, styleId = r.styleId, kind = r.kind, score = r.score, duration = r.duration, kills = r.kills,
            reason = r.reason, runId = r.runId, seed = r.seed, buildVersion = r.buildVersion,
            masteryLevel = r.masteryLevel, passives = new List<string>(r.passives ?? new List<string>()),
        };

        /// <summary>Profile validation: a known kind and at most one record per mode, style and kind.</summary>
        public static string Validate(PlayerProfile p)
        {
            var seen = new HashSet<string>();
            foreach (var r in p.records)
            {
                if (string.IsNullOrEmpty(r.kind)) r.kind = BestScore;   // older file
                if (!IsKnownKind(r.kind)) return $"record kind {r.kind}";
                if (string.IsNullOrEmpty(r.mode) || !(r.duration >= 0f)) return "record";
                if (!seen.Add(r.mode + "|" + r.styleId + "|" + r.kind)) return "duplicate record";
            }
            return null;
        }
    }
}
