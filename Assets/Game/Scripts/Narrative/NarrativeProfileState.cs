using System;
using System.Collections.Generic;

namespace BorrowedHex.Narrative
{
    /// <summary>
    /// Lore plan Tasks 1 and 4: what the player has discovered, kept in the profile.
    /// Unlocked, seen and completed are deliberately separate: reaching a trigger unlocks a
    /// scene for the Story menu, finishing or explicitly skipping it marks it seen (so it may
    /// be skipped as familiar next time), and only a victory completes the story.
    /// </summary>
    [Serializable]
    public sealed class NarrativeProfileState
    {
        public List<string> unlocked = new List<string>();
        public List<string> seen = new List<string>();
        public bool storyCompleted;
        public NarrativeSettings settings = new NarrativeSettings();

        public bool IsUnlocked(string id) => unlocked.Contains(id);
        public bool IsSeen(string id) => seen.Contains(id);

        public void Unlock(string id) { if (!unlocked.Contains(id)) unlocked.Add(id); }
        public void MarkSeen(string id) { if (!seen.Contains(id)) seen.Add(id); }

        /// <summary>
        /// JsonUtility leaves a group missing from an older save as null; this restores the
        /// plan's defaults instead, so a legacy profile reads as "nothing discovered yet".
        /// </summary>
        public void Repair()
        {
            if (unlocked == null) unlocked = new List<string>();
            if (seen == null) seen = new List<string>();
            if (settings == null) settings = new NarrativeSettings();
            settings.dialogueVolume = Math.Max(0f, Math.Min(1f, settings.dialogueVolume));
        }

        /// <summary>An in-memory copy for development previews (debug runs never touch the real profile).</summary>
        public NarrativeProfileState Clone() => new NarrativeProfileState
        {
            unlocked = new List<string>(unlocked),
            seen = new List<string>(seen),
            storyCompleted = storyCompleted,
            settings = settings.Clone(),
        };
    }

    /// <summary>Narrative settings (plan Task 4 defaults).</summary>
    [Serializable]
    public sealed class NarrativeSettings
    {
        /// <summary>A scene already seen is skipped at its trigger, for quick retries.</summary>
        public bool skipFamiliar = true;
        /// <summary>Show whole passages and lines at once (both layouts).</summary>
        public bool instantText;
        /// <summary>Dialogue tick loudness, 0..1. 0 mutes it; the story and controls are unchanged.</summary>
        public float dialogueVolume = 0.35f;
        /// <summary>Silent one-line rogue reactions during combat.</summary>
        public bool combatReactions = true;

        public NarrativeSettings Clone() => (NarrativeSettings)MemberwiseClone();
    }
}
