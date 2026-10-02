using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// The Collector (section 4, as changed by the owner, D38 and D48): 50 health and four
    /// patterns — bolt stream, sweeping melee, fan volley, ground slam — chosen from the
    /// player's position and line of sight. The two ranged patterns fire ordinary capturable
    /// bolts (the boss's ammunition for the player). The slam is an unparryable hazard; the
    /// sweep can be parried through its gold arc (D47). The rocket attack of the original plan
    /// is gone by owner direction. All four attacks were sped up ~15% after the first playtest.
    /// </summary>
    [Serializable]
    public class BossTuning
    {
        public string displayName = "The Collector";
        public int health = 50;
        public float bodyRadius = 0.9f;
        public float moveSpeed = 3.0f;
        [Tooltip("Half hearts lost to ANY boss attack, its bolts included (4 = two hearts).")]
        public int hitDamage = 4;
        [Tooltip("Half hearts lost on touching the boss's body (2 = one heart).")]
        public int contactDamage = 2;
        [Tooltip("Score for the kill (section 6).")]
        public int killValue = 250;

        [Tooltip("Seconds of harmless spawn warning after the intro banner.")]
        public float spawnWarning = 1.0f;
        [Tooltip("Longest a repositioning gap may last before the next pattern starts anyway.")]
        // 1.0 → 1.5 (owner, D52: "move around a little more"). It also lengthens the worst
        // bolt drought; ShortRunTests computes that bound from this field.
        public float repositionMax = 1.5f;
        [Tooltip("Pause after each pattern: the punish window.")]
        public float recover = 0.5f;
        [Tooltip("Ranged patterns try to fire from about this far away.")]
        public float rangedDistance = 6f;
        [Tooltip("Ranged firing spots are swung this many degrees (at least) round the player, either way, so the boss circles instead of backing straight off.")]
        public float strafeMinDeg = 25f;
        [Tooltip("...and at most this many.")]
        public float strafeMaxDeg = 55f;

        [Header("Choosing a pattern (D48)")]
        [Tooltip("Closer than this: slam.")]
        public float slamChooseDistance = 1.8f;
        [Tooltip("Closer than this (and in sight): sweep.")]
        public float sweepChooseDistance = 4.0f;
        [Tooltip("Closer than this: fan volley; farther: bolt stream.")]
        public float fanMaxDistance = 7.5f;
        [Tooltip("After this many melee patterns in a row the next is ranged, so bolts keep coming.")]
        public int maxMeleeInARow = 2;
        [Tooltip("The same pattern at most this many times in a row; then its partner (fan↔stream, slam↔sweep).")]
        public int maxSameInARow = 2;

        [Header("Teleport (D49)")]
        [Tooltip("Only considered when the player is at least this far away.")]
        public float teleportMinDistance = 6f;
        [Tooltip("Chance per pattern start, when far away and off cooldown (seeded).")]
        [Range(0f, 1f)] public float teleportChance = 0.35f;
        [Tooltip("Visible wind-up: the boss fades and the arrival spot is marked.")]
        public float teleportTelegraph = 0.6f;
        public float teleportCooldown = 5f;
        [Tooltip("Arrives this far behind the player (opposite their aim).")]
        public float teleportBehindDistance = 2.6f;

        [Header("Bolt stream")]
        public float streamTelegraph = 0.6f;
        public float streamAimLock = 0.17f;
        public int streamShots = 12;
        public float streamInterval = 0.105f;
        [Tooltip("Degrees per second the stream turns to follow the player while firing.")]
        public float streamTurnRate = 70f;

        [Header("Fan volley")]
        public float fanTelegraph = 0.72f;
        public float fanAimLock = 0.25f;
        public float[] fanSpreadDeg = { -48f, -36f, -24f, -12f, 0f, 12f, 24f, 36f, 48f };

        [Header("Sweeping melee")]
        public float sweepTelegraph = 0.77f;
        public float sweepAimLock = 0.25f;
        [Tooltip("Blade length from the boss's centre.")]
        public float sweepReach = 3.0f;
        [Tooltip("The sweep covers aim ± this many degrees.")]
        public float sweepHalfAngle = 70f;
        [Tooltip("Seconds the blade takes to cross the whole arc (a fast swing, but a real one).")]
        public float sweepDuration = 0.26f;
        [Tooltip("Parry arc centre line, from the boss's centre. Placed so a parry band can only "
                 + "touch it while the player stands inside the blade's reach (in harm's way).")]
        public float sweepParryArcRadius = 1.95f;
        public float sweepParryArcWidth = 0.09f;
        [Tooltip("The gold arc appears this long after the sweep's wind-up starts...")]
        public float sweepParryDelay = 0.15f;
        [Tooltip("...and stays this long, gone before the blade moves.")]
        public float sweepParryDuration = 0.3f;
        [Tooltip("Recovery after a parried sweep: the punish window is part of the reward.")]
        public float parriedRecover = 1.2f;

        [Header("Ground slam")]
        public float slamTelegraph = 0.95f;
        [Tooltip("Slam radius around the boss's centre.")]
        public float slamRadius = 3.2f;
    }

    /// <summary>
    /// Section 6 short-mode schedule and score numbers, revised by the owner (D50): each
    /// encounter is a fixed set of formations that must ALL be killed, and one shared
    /// three-minute clock covers the whole run, boss included.
    /// </summary>
    [Serializable]
    public class ShortModeTuning
    {
        [Tooltip("The whole run's clock (active seconds). Running out ends the run TimeExpired.")]
        public float runLength = 180f;
        public int encounterCount = 3;
        [Tooltip("Formations per encounter; the encounter ends when every member is dead.")]
        public int[] formationsPerEncounter = { 4, 5, 5 };
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

        public float TotalLength => runLength;

        public int FormationsIn(int encounter) =>
            formationsPerEncounter[Mathf.Clamp(encounter, 0, formationsPerEncounter.Length - 1)];
    }

    public partial class GameConfig
    {
        public BossTuning collector = new BossTuning();
        public ShortModeTuning shortMode = new ShortModeTuning();
    }
}
