using System;
using System.Collections.Generic;

namespace BorrowedHex.Progression
{
    /// <summary>
    /// The one saved file (section 8: versioned, validated JSON). Plain [Serializable] fields
    /// so Unity's JsonUtility reads and writes it on Windows and Web alike; no dictionaries,
    /// because JsonUtility cannot serialize them.
    ///
    /// Fields for later phases (mastery, nodes, achievements, records, style) live here from the
    /// start so a save written by an early build loads unchanged in a later one: a field the
    /// JSON lacks keeps its default.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        /// <summary>Schema version. Bump only when a field's MEANING changes, and migrate.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>Incremented on every save; the loader prefers the highest valid generation.</summary>
        public int generation;

        public ProfileSettings settings = new ProfileSettings();
        public MasteryState mastery = new MasteryState();
        public ProfileStats stats = new ProfileStats();

        /// <summary>
        /// Skill-tree nodes bought (Phase 9). D101: owning a node makes it active. Old saves'
        /// equippedNodes key is ignored by JsonUtility, so they still load.
        /// </summary>
        public List<string> ownedNodes = new List<string>();
        /// <summary>Selected capture style (Phase 11); unknown ids fall back to Snatcher.</summary>
        public string styleId = "snatcher";

        /// <summary>
        /// Lore plan Task 4: story discoveries and story settings. Additive, so the version
        /// stays 1: an older save simply has no group, and Validate fills in the defaults.
        /// </summary>
        public Narrative.NarrativeProfileState narrative = new Narrative.NarrativeProfileState();

        public List<AchievementEntry> achievements = new List<AchievementEntry>();
        public List<RunRecord> records = new List<RunRecord>();

        /// <summary>
        /// Run IDs already applied, newest last. Finalization is idempotent per run ID
        /// (section 8); only recent IDs are kept because a run can only be finalized while
        /// its sim is alive, so an old ID can never come back.
        /// </summary>
        public List<string> finalizedRunIds = new List<string>();
        public const int FinalizedRunIdsKept = 32;

        public static PlayerProfile CreateDefault() => new PlayerProfile();
    }

    [Serializable]
    public sealed class ProfileSettings
    {
        /// <summary>-1: leave the display as launched. 0: windowed. 1: fullscreen.</summary>
        public int displayMode = -1;
        /// <summary>HUD and menu scale, 0.8–1.4.</summary>
        public float uiScale = 1f;
        /// <summary>Readability: no life-bar flash or heartbeat, softer hit flashes.</summary>
        public bool reduceFlashes;
        /// <summary>Readability: the control hint line above the packet panels.</summary>
        public bool showHints = true;

        public const float MinUiScale = 0.8f, MaxUiScale = 1.4f;
    }

    [Serializable]
    public sealed class MasteryState
    {
        public int level = 1;
        /// <summary>XP toward the next level (excess carries over, section 7).</summary>
        public int xp;
        /// <summary>Total XP ever earned, kept counting at level 10.</summary>
        public int totalXp;
        /// <summary>Points earned and not yet spent on nodes.</summary>
        public int points;
    }

    [Serializable]
    public sealed class ProfileStats
    {
        public int runs;
        public int victories;
        public int kills;
        public int bossesDefeated;
        public int perfectShots;
        public int packetsReleased;
        public int backfires;
        public float secondsPlayed;
    }

    [Serializable]
    public sealed class AchievementEntry
    {
        public string id;
        /// <summary>Run that earned it (for the results screen; never used for validity).</summary>
        public string runId;
    }

    /// <summary>Best result per mode and capture style (section 6), with the metadata the plan asks for.</summary>
    [Serializable]
    public sealed class RunRecord
    {
        public string mode;
        public string styleId;
        /// <summary>Records.BestScore or Records.LongestRun; empty in an older file means best score.</summary>
        public string kind = "score";
        public int score;
        public float duration;
        public int kills;
        public string reason;
        public string runId;
        public int seed;
        public string buildVersion;
        public int masteryLevel;
        public List<string> passives = new List<string>();
    }
}
