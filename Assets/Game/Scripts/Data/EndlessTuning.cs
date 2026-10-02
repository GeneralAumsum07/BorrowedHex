using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// Section 6 "Endless mode": the scheduler's numbers. Combat, enemies, the boss and the
    /// life clock are shared with short mode; only what endless changes lives here, so tuning
    /// an enemy never needs a second edit for endless.
    /// </summary>
    [Serializable]
    public class EndlessTuning
    {
        [Header("Waves")]
        [Tooltip("Active seconds per normal wave. Boss time does not count towards it.")]
        public float waveLength = 30f;
        [Tooltip("Waves per cycle; the boss follows the last one.")]
        public int wavesPerCycle = 6;
        [Tooltip("An upgrade choice after every this-many waves (the last one's is deferred until the boss falls).")]
        public int choiceEveryWaves = 2;
        [Tooltip("Life seconds restored by a boss kill, capped at the starting clock.")]
        public float bossKillSeconds = 30f;
        [Tooltip("Hard cap on simultaneously active ordinary enemies; further spawns wait.")]
        public int maxOrdinaryEnemies = 18;
        [Tooltip("Highest rank an offer can have (rank = cycle number, capped here).")]
        public int maxOfferRank = 3;

        [Header("Per completed cycle")]
        [Tooltip("Ordinary enemy health: +this fraction per completed cycle (no cap).")]
        public float healthPerCycle = 0.15f;
        [Tooltip("Movement: +this fraction per completed cycle...")]
        public float movePerCycle = 0.05f;
        [Tooltip("...up to this multiplier.")]
        public float moveMax = 1.25f;
        [Tooltip("Attack interval: -this fraction per completed cycle...")]
        public float cooldownPerCycle = 0.05f;
        [Tooltip("...down to this multiplier.")]
        public float cooldownMin = 0.70f;
        [Tooltip("Boss health: +this fraction per completed cycle.")]
        public float bossHealthPerCycle = 0.20f;
        [Tooltip("Overstay timer: -this many seconds per completed cycle...")]
        public float overstayStepPerCycle = 2f;
        [Tooltip("...down to this many seconds.")]
        public float overstayMinSeconds = 15f;

        [Header("Projectile budget")]
        // The plan gives no number. 80 is my placeholder (D84): eighteen enemies at their
        // fastest rarely hold more than ~50 shots in the air, so the budget only bites in the
        // pile-ups it exists for. It is a cap on NEW emissions, never a reason to delete a shot.
        [Tooltip("Live hostile projectiles at which new hostile emissions wait (visibly) for room.")]
        public int maxHostileProjectiles = 80;
    }

    public partial class GameConfig
    {
        [Header("Endless mode")]
        public EndlessTuning endless = new EndlessTuning();
    }
}
