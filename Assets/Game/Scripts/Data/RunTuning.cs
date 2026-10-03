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
        // 2.6 → 3.0 (D49) → 3.3 (D54) → 3.6 (owner, D55).
        public float moveSpeed = 3.6f;
        [Tooltip("Life seconds lost to ANY boss attack, its bolts included.")]
        public int hitDamage = 20;
        [Tooltip("Life seconds lost on touching the boss's body.")]
        public int contactDamage = 10;
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
        // 1.8 → 2.4 (owner, D53: slams were rare). At 1.8 the slam band was so thin that a
        // player rarely stood in it when the boss chose; the boss walks in to ~1.8 before a
        // sweep, and the player has usually stepped back out by the next choice.
        public float slamChooseDistance = 2.4f;
        [Tooltip("Closer than this (and in sight): sweep.")]
        // 4.0 → 5.0 (owner, D53: "melee a little more often"); the fan band shrinks to 5-7.5.
        public float sweepChooseDistance = 5.0f;
        [Tooltip("Closer than this: fan volley; farther: bolt stream.")]
        public float fanMaxDistance = 7.5f;
        [Tooltip("After this many melee patterns in a row the next is ranged, so bolts keep coming.")]
        public int maxMeleeInARow = 2;
        [Tooltip("The same pattern at most this many times in a row; then its partner (fan↔stream, slam↔sweep).")]
        public int maxSameInARow = 2;
        [Tooltip("When the position table picks the sweep, the chance (seeded) it slams instead.")]
        [Range(0f, 1f)] public float slamInsteadOfSweepChance = 0.35f;

        [Header("Teleport (D49)")]
        [Tooltip("Only considered when the player is at least this far away.")]
        public float teleportMinDistance = 6f;
        [Tooltip("Chance per pattern start, when far away and off cooldown (seeded).")]
        // 0.35 → 0.40 (D53) → 0.48 (owner, D55: "another 8%", read as 8 points).
        [Range(0f, 1f)] public float teleportChance = 0.48f;
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
        // Owner direction: the stream has no range limit. Its bolts ignore the bolt's lifetime
        // too (36 units), which is shorter than the Sanctum's 52-unit diagonal; a wall or a
        // hit is the only thing that ends them.
        [Tooltip("Stream bolts never expire; only a wall or a hit ends them.")]
        public bool streamUnlimited = true;

        [Header("Fan volley")]
        public float fanTelegraph = 0.72f;
        public float fanAimLock = 0.25f;
        public float[] fanSpreadDeg = { -48f, -36f, -24f, -12f, 0f, 12f, 24f, 36f, 48f };
        [Tooltip("Units a fan bolt travels before it expires (owner: very long, 30).")]
        public float fanRange = 30f;
        // Section 4: a repeated endless boss gains ONE predefined variation, not only health.
        // The variation is mine (D84; the plan names none): the same fan plus a bolt at
        // ±60°, so the side-step that beat the first boss's fan no longer clears it.
        [Tooltip("Endless: the repeated boss's fan.")]
        public float[] fanSpreadRepeatDeg = { -60f, -48f, -36f, -24f, -12f, 0f, 12f, 24f, 36f, 48f, 60f };

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
        // 0.95 → 0.8 (owner, D54: faster slam). Still longer than the sweep's 0.77 s wind-up:
        // the slam cannot be parried, so its warning is the player's only answer.
        public float slamTelegraph = 0.8f;
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
        // 180 → 300 (owner, D56): the faster boss (D55) needs ~73 s even for an ideal bot.
        // 300 → 180 (owner, D65): shorter life budget; shown as a heart + bar, not a timer.
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
        [Tooltip("D102: score for an Overcharged release, times the combo multiplier (placeholder).")]
        public int overchargeScore = 20;
        [Tooltip("D102: score for the Nth kill of a chain (index N-1), times the combo; the last repeats. Mirrors combat.chainBonusSeconds (placeholder).")]
        public int[] chainScore = { 0, 5, 10, 15 };
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
