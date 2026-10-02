using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Player;

namespace BorrowedHex.Progression
{
    /// <summary>
    /// Builds a run's <see cref="PlayerStats"/> once, at run start, from the config plus the
    /// equipped passives (section 7). The sim only ever reads the result, so changing the
    /// loadout between runs can never leak into a run already in progress. Encounter upgrades
    /// are a separate layer the sim applies at the moment they act (D68).
    /// </summary>
    public static class Loadout
    {
        public static PlayerStats Resolve(GameConfig config, IEnumerable<string> passives)
            => Resolve(config, passives, CaptureStyles.Snatcher);

        /// <summary>
        /// Style first, then passives: a style sets the base catch, a passive adjusts whatever
        /// base it finds (Precision widens Collector's cone too, D83).
        /// </summary>
        public static PlayerStats Resolve(GameConfig config, IEnumerable<string> passives, string styleId)
        {
            var s = PlayerStats.FromConfig(config);
            CaptureStyles.Apply(s, styleId, config.styles);
            var t = config.progression ?? new ProgressionTuning();
            if (passives == null) return s;
            // A set, so a duplicated id (which validation already refuses) still applies once.
            foreach (var id in new HashSet<string>(passives)) Apply(s, id, t);
            return s;
        }

        static void Apply(PlayerStats s, string id, ProgressionTuning t)
        {
            switch (id)
            {
                case SkillTree.PrecisionAngle: s.CaptureConeAngle += t.precisionAngle; break;
                case SkillTree.PrecisionCapacity: s.PacketCapacity += t.precisionCapacity; break;
                case SkillTree.QuickDraw:
                    s.QuickDrawBonus = t.quickDrawBonus;
                    s.QuickDrawWindow = t.quickDrawWindow;
                    break;
                case SkillTree.MobilitySpeed: s.MoveSpeed *= 1f + t.mobilitySpeed; break;
                // Floor so a tuning pass can never make the dash free to spam.
                case SkillTree.MobilityDashRecovery: s.DashCooldown = System.Math.Max(0.05f, s.DashCooldown - t.mobilityDashRecovery); break;
                // Same duration, so the dash simply moves faster (PlayerMotor derives speed).
                case SkillTree.MobilityDashDistance: s.DashDistance += t.mobilityDashDistance; break;
                case SkillTree.ResilienceGrace: s.HitInvulnerability += t.resilienceGrace; break;
                // StartingSeconds is both the start and the cap (ArenaSim.Run), so one field covers both.
                case SkillTree.ResilienceTime: s.StartingSeconds += t.resilienceTime; break;
                // Capped at the dash duration, as PlayerMotor already does for the base value.
                case SkillTree.ResilienceDashGrace:
                    s.DashInvulnerability = System.Math.Min(s.DashDuration, s.DashInvulnerability + t.resilienceDashGrace);
                    break;
                // Unknown ids are ignored: validation keeps them out of a saved profile, and a
                // test may pass an arbitrary list.
            }
        }
    }
}
