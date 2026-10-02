using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// Section 7 capture styles. Snatcher has no numbers of its own: it IS the baseline catch in
    /// <see cref="CaptureTuning"/>, so tuning the baseline tunes Snatcher and nothing drifts.
    /// The other two only state what they change.
    /// </summary>
    [Serializable]
    public class StyleTuning
    {
        [Header("Collector")]
        [Tooltip("Total catch cone in degrees (section 7: 140).")]
        public float collectorConeAngle = 140f;
        [Tooltip("Catch window in seconds (section 7: 0.40).")]
        public float collectorWindow = 0.40f;
        [Tooltip("Recovery in seconds, from the press (section 7: 1.00).")]
        public float collectorRecovery = 1.00f;

        [Header("Daredevil")]
        // The plan gives no number for the sweep's width, so this is my placeholder (D83). It
        // is measured from the player's centre: 1.0 reaches ~0.65 past the 0.35 body, i.e. a
        // shot has to be properly in the dash's path, not merely nearby, to be taken.
        [Tooltip("Reach of the capturing dash around the player's centre, along the whole dash path.")]
        public float daredevilCatchRadius = 1.0f;
    }

    public partial class GameConfig
    {
        [Header("Capture styles")]
        public StyleTuning styles = new StyleTuning();
    }
}
