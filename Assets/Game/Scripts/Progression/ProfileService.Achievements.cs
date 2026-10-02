using System.Collections.Generic;
using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    /// <summary>Phase 10: achievements and records, inside the same idempotent finalization as XP.</summary>
    public sealed partial class ProfileService
    {
        partial void ApplyAchievementsAndRecords(RunSummary summary, RunSetup setup, FinalizeResult result)
        {
            // After XP (ApplyProgression), so mastery levels reached this run count.
            result.NewAchievements = Achievements.Award(Profile, summary);
            result.Records = Records.Submit(Profile, summary, setup);
        }

        static partial void ValidateMore(PlayerProfile p, ref string why)
        {
            why ??= Achievements.Validate(p);
            why ??= Records.Validate(p);
            ValidateLater(p, ref why);
        }

        // Phase 11 (style ids).
        static partial void ValidateLater(PlayerProfile p, ref string why);
    }

}
