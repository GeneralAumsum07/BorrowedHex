using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// The Collector (section 4, as changed by the owner for Phase 5, D38): 50 health, and four
    /// patterns in a fixed cycle — bolt stream, sweeping melee, fan volley, ground slam. The two
    /// ranged patterns fire ordinary capturable bolts (the boss's ammunition for the player);
    /// the two melee patterns are avoidable hazards only (section 4: "charges and melee attacks
    /// are avoidable hazards, not automatically stealable abilities"). The rocket attack of the
    /// original plan is gone by owner direction.
    ///
    /// The cycle alternates ranged and melee on purpose: the plan forbids "an extended period
    /// with neither targets nor ammunition", and with this order no melee pattern is ever more
    /// than one pattern away from a volley the player can catch.
    /// </summary>
    [Serializable]
    public class BossTuning
    {
        public string displayName = "The Collector";
        public int health = 50;
        public float bodyRadius = 0.9f;
        public float moveSpeed = 2.6f;
        [Tooltip("Score for the kill (section 6).")]
        public int killValue = 250;

        [Tooltip("Seconds of harmless spawn warning after the intro banner.")]
        public float spawnWarning = 1.0f;
        [Tooltip("Longest a repositioning gap may last before the next pattern starts anyway.")]
        public float repositionMax = 1.0f;
        [Tooltip("Pause after each pattern: the punish window.")]
        public float recover = 0.5f;
        [Tooltip("Ranged patterns try to fire from about this far away.")]
        public float rangedDistance = 6f;

        [Header("Bolt stream")]
        public float streamTelegraph = 0.7f;
        public float streamAimLock = 0.2f;
        public int streamShots = 12;
        public float streamInterval = 0.12f;
        [Tooltip("Degrees per second the stream turns to follow the player while firing.")]
        public float streamTurnRate = 70f;

        [Header("Fan volley")]
        public float fanTelegraph = 0.85f;
        public float fanAimLock = 0.3f;
        public float[] fanSpreadDeg = { -48f, -36f, -24f, -12f, 0f, 12f, 24f, 36f, 48f };

        [Header("Sweeping melee")]
        public float sweepTelegraph = 0.9f;
        public float sweepAimLock = 0.3f;
        [Tooltip("Blade length from the boss's centre.")]
        public float sweepReach = 3.0f;
        [Tooltip("The sweep covers aim ± this many degrees.")]
        public float sweepHalfAngle = 70f;
        [Tooltip("Seconds the blade takes to cross the whole arc (a fast swing, but a real one).")]
        public float sweepDuration = 0.3f;

        [Header("Ground slam")]
        public float slamTelegraph = 1.1f;
        [Tooltip("Slam radius around the boss's centre.")]
        public float slamRadius = 3.2f;
    }

    /// <summary>Section 6 short-mode schedule and score numbers.</summary>
    [Serializable]
    public class ShortModeTuning
    {
        [Tooltip("Length of each of the three timed encounters (active seconds).")]
        public float encounterLength = 40f;
        public int encounterCount = 3;
        [Tooltip("Boss window; the run ends TimeExpired when it runs out with the boss alive.")]
        public float bossWindow = 60f;
        [Tooltip("Hard cap on simultaneously active ordinary enemies; further spawns wait.")]
        public int maxOrdinaryEnemies = 12;
        [Tooltip("First formation arrives this long after the run starts.")]
        public float firstSpawnDelay = 1f;
        [Tooltip("Seconds between formations, per encounter (pressure rises encounter by encounter).")]
        public float[] spawnInterval = { 7f, 6f, 5f };
        [Tooltip("When the arena is empty, the next formation comes after this breather instead.")]
        public float emptyArenaBreather = 1.5f;

        [Header("Score (section 6)")]
        public float comboStep = 0.25f;
        public float comboMax = 3f;
        public float comboTimer = 5f;
        [Tooltip("Points per unused active second on a victory.")]
        public int victoryBonusPerSecond = 2;

        public float TotalLength => encounterLength * encounterCount + bossWindow;
        public float BossStartsAt => encounterLength * encounterCount;
    }

    public partial class GameConfig
    {
        public BossTuning collector = new BossTuning();
        public ShortModeTuning shortMode = new ShortModeTuning();
    }
}
