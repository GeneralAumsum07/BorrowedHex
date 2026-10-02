using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    public sealed class AchievementDef
    {
        public readonly string Id, Name, Description;
        public AchievementDef(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Section 7's ten achievements. Combat facts are gathered by RunScore during the run (so
    /// piercing and echoes are de-duplicated at the source) and frozen into the RunSummary;
    /// this class only reads that frozen record plus the profile's mastery, so evaluation is
    /// pure and runs inside the single idempotent finalization.
    /// </summary>
    public static class Achievements
    {
        public const string ReturnPolicy = "return_policy";
        public const string FirstBorrow = "first_borrow";
        public const string CrowdControl = "crowd_control";
        public const string PerfectTiming = "perfect_timing";
        public const string Untouchable = "untouchable";
        public const string MixedBag = "mixed_bag";
        public const string FinalNotice = "final_notice";
        public const string SecondEncore = "second_encore";
        public const string PersistentStudent = "persistent_student";
        public const string FullyTrained = "fully_trained";

        public const int CrowdControlKills = 5;
        public const int PerfectTimingShots = 5;

        public static readonly IReadOnlyList<AchievementDef> All = new[]
        {
            new AchievementDef(ReturnPolicy, "Return Policy", "A returned payload kills the enemy that cast it"),
            new AchievementDef(FirstBorrow, "First Borrow", "A returned payload damages an enemy"),
            new AchievementDef(CrowdControl, "Crowd Control", "One release, with its echo, kills five different enemies"),
            new AchievementDef(PerfectTiming, "Perfect Timing", "Five perfect shots damage enemies in one run"),
            new AchievementDef(Untouchable, "Untouchable", "Clear a short-mode encounter without a hit or a backfire"),
            new AchievementDef(MixedBag, "Mixed Bag", "Kill enemies with returned bolts and returned rockets in one run"),
            new AchievementDef(FinalNotice, "Final Notice", "Win short mode"),
            new AchievementDef(SecondEncore, "Second Encore", "Defeat two bosses in one endless run"),
            new AchievementDef(PersistentStudent, "Persistent Student", "Reach mastery 5"),
            new AchievementDef(FullyTrained, "Fully Trained", "Reach mastery 10"),
        };

        public static AchievementDef Find(string id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        /// <summary>Every achievement this run (and the mastery after it) satisfies, earned before or not.</summary>
        public static List<string> MetBy(RunSummary s, MasteryState m)
        {
            var met = new List<string>();
            if (s.ReturnPolicy) met.Add(ReturnPolicy);
            if (s.FirstBorrow) met.Add(FirstBorrow);
            if (s.BestVolleyKills >= CrowdControlKills) met.Add(CrowdControl);
            if (s.PerfectHits >= PerfectTimingShots) met.Add(PerfectTiming);
            if (s.Mode == GameMode.Short && s.UntouchableEncounters > 0) met.Add(Untouchable);
            // Boss bolts return as heavy shots (D61); they are still bolts for Mixed Bag.
            s.KillsByKind.TryGetValue(AttackKind.Bolt, out int bolts);
            s.KillsByKind.TryGetValue(AttackKind.HeavyShot, out int heavy);
            s.KillsByKind.TryGetValue(AttackKind.Rocket, out int rockets);
            if (bolts + heavy > 0 && rockets > 0) met.Add(MixedBag);
            if (s.Mode == GameMode.Short && s.Reason == RunEndReason.Victory) met.Add(FinalNotice);
            if (s.Mode == GameMode.Endless && s.BossesDefeated >= 2) met.Add(SecondEncore);
            if (m != null && m.level >= 5) met.Add(PersistentStudent);
            if (m != null && m.level >= 10) met.Add(FullyTrained);
            return met;
        }

        public static bool Has(PlayerProfile p, string id)
        {
            foreach (var a in p.achievements) if (a.id == id) return true;
            return false;
        }

        /// <summary>Record newly met achievements once (section 7: completion persists once). Returns the new ones.</summary>
        public static List<string> Award(PlayerProfile p, RunSummary s)
        {
            var fresh = new List<string>();
            foreach (var id in MetBy(s, p.mastery))
            {
                if (Has(p, id)) continue;
                p.achievements.Add(new AchievementEntry { id = id, runId = s.RunId });
                fresh.Add(id);
            }
            return fresh;
        }

        /// <summary>Profile validation: known ids, each at most once.</summary>
        public static string Validate(PlayerProfile p)
        {
            var seen = new HashSet<string>();
            foreach (var a in p.achievements)
            {
                if (Find(a.id) == null) return $"unknown achievement {a.id}";
                if (!seen.Add(a.id)) return $"duplicate achievement {a.id}";
            }
            return null;
        }
    }
}
