using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// Section 5 encounter-upgrade numbers. Arrays are indexed by rank - 1: short mode always
    /// offers rank 1; endless mode (Phase 12) offers rank = cycle, capped at 3. Keeping the rank
    /// table here rather than in code means a tuning pass never touches combat rules.
    /// </summary>
    [Serializable]
    public class UpgradeTuning
    {
        [Tooltip("Distinct upgrades offered at each choice.")]
        public int offerCount = 3;
        [Tooltip("D96: most upgrades held at once. At this many, only rank-ups are offered (owner value 4).")]
        public int maxHeld = 4;
        [Tooltip("D96: highest rank. Must match the length of the rank tables below (3).")]
        public int maxRank = 3;
        [Tooltip("D96: fraction of CURRENT life a paid pick (add or rank up) costs, by how many upgrades you hold BEFORE it (0, 1, 2, 3+). Owner values 0.15, 0.25, 0.40, 0.50; the last repeats.")]
        public float[] takeCostByHeld = { 0.15f, 0.25f, 0.40f, 0.50f };

        [Header("Piercing Return")]
        [Tooltip("Extra enemies a returned non-explosive payload passes through, per rank.")]
        public int piercePerRank = 1;

        [Header("Echo Volley")]
        public float echoDelay = 0.20f;
        [Tooltip("Echo damage as a fraction of the release, by rank.")]
        public float[] echoFraction = { 0.25f, 0.40f, 0.55f };

        [Header("Heavy Orbit")]
        [Tooltip("Reach from the player's centre to an enemy's body, by rank.")]
        public float[] orbitRadius = { 1.0f, 1.2f, 1.4f };
        public float orbitDamage = 1f;
        [Tooltip("Seconds between orbit hits on the SAME enemy.")]
        public float orbitInterval = 0.35f;

        [Header("Parting Gift")]
        public float[] partingGiftDamage = { 1f, 2f, 3f };
        public float[] partingGiftRadius = { 1.5f, 1.75f, 2.0f };

        [Header("Final Second")]
        [Tooltip("Added to the perfect-catch bonus (0.20 = +20 percentage points), by rank.")]
        public float[] finalSecondBonus = { 0.20f, 0.40f, 0.60f };

        [Header("Fusion")]
        [Tooltip("Power multiplier of a fused packet (section 5: +25% power).")]
        public float fusionPowerScale = 1.25f;

        /// <summary>Rank lookup that never throws: ranks beyond the table reuse its last entry.</summary>
        public static float ByRank(float[] table, int rank) =>
            table == null || table.Length == 0 ? 0f : table[Mathf.Clamp(rank - 1, 0, table.Length - 1)];
    }

    public partial class GameConfig
    {
        public UpgradeTuning upgrades = new UpgradeTuning();
    }
}
