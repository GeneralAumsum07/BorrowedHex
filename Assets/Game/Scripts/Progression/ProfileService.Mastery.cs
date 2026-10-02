using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    /// <summary>Phase 9: XP and levels inside the single idempotent finalization; tree validation.</summary>
    public sealed partial class ProfileService
    {
        partial void ApplyProgression(RunSummary summary, RunSetup setup, FinalizeResult result)
        {
            var m = Profile.mastery;
            result.LevelBefore = m.level;
            result.Xp = Mastery.RunXp(summary);
            result.LevelsGained = Mastery.Grant(m, result.Xp.Total);
            result.LevelAfter = m.level;
            // Achievements and records (Phase 10) run after XP, so a level reached this run
            // (Persistent Student, Fully Trained) is visible to them.
            ApplyAchievementsAndRecords(summary, setup, result);
        }

        partial void ApplyAchievementsAndRecords(RunSummary summary, RunSetup setup, FinalizeResult result);

        static partial void ValidateExtraImpl(PlayerProfile p, ref string why)
        {
            why ??= SkillTree.Validate(p);
            ValidateMore(p, ref why);
        }

        // Phase 10+ checks (known achievement ids, style ids).
        static partial void ValidateMore(PlayerProfile p, ref string why);
    }
}
