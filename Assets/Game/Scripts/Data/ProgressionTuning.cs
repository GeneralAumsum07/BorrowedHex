using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// Section 7 skill-tree passive magnitudes. The XP formula and level costs are fixed rules
    /// (Progression.Mastery) because a saved profile is validated against them; these numbers
    /// only change what an equipped node does inside a run, so they are safe to tune.
    /// </summary>
    [Serializable]
    public class ProgressionTuning
    {
        [Header("Precision")]
        [Tooltip("precision_angle: degrees added to the catch cone.")]
        public float precisionAngle = 15f;
        [Tooltip("precision_capacity: energy added to each packet.")]
        public int precisionCapacity = 2;
        [Tooltip("quick_draw: damage bonus for firing soon after a swap (0.3 = +30%).")]
        public float quickDrawBonus = 0.30f;
        [Tooltip("quick_draw: seconds after a swap during which the bonus applies.")]
        public float quickDrawWindow = 0.30f;

        [Header("Mobility")]
        [Tooltip("mobility_speed: movement speed multiplier bonus (0.05 = +5%).")]
        public float mobilitySpeed = 0.05f;
        [Tooltip("mobility_dash_recovery: seconds removed from the dash cooldown.")]
        public float mobilityDashRecovery = 0.10f;
        [Tooltip("mobility_dash_distance: units added to the dash, same duration.")]
        public float mobilityDashDistance = 0.30f;

        [Header("Resilience")]
        [Tooltip("resilience_grace: seconds added to post-hit invulnerability.")]
        public float resilienceGrace = 0.15f;
        [Tooltip("resilience_time: seconds added to the starting clock and its cap.")]
        public float resilienceTime = 20f;
        [Tooltip("resilience_dash_grace: seconds added to dash invulnerability (capped at the dash duration).")]
        public float resilienceDashGrace = 0.04f;
    }

    public partial class GameConfig
    {
        [Header("Permanent progression")]
        public ProgressionTuning progression = new ProgressionTuning();
    }
}
