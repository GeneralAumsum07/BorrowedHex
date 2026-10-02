using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Progression
{
    /// <summary>What one finalization did, for the results screen. Later phases add XP, achievements, records.</summary>
    public sealed class FinalizeResult
    {
        public bool Applied;
        /// <summary>Why nothing was applied: "sandbox", "debug", "already finalized".</summary>
        public string SkippedBecause;
        public bool Saved;

        // Phase 9: what the run earned toward mastery.
        public XpBreakdown Xp;
        public int LevelBefore, LevelAfter, LevelsGained;
    }

    /// <summary>
    /// Owns the loaded profile: load-with-recovery, validation, explicit saves and the single
    /// idempotent run finalization (section 8). Pure C# over an <see cref="IProfileStorage"/>,
    /// so every rule here is tested without touching the real save.
    /// </summary>
    public sealed partial class ProfileService
    {
        readonly IProfileStorage storage;
        public PlayerProfile Profile { get; private set; }

        /// <summary>Non-null when something went wrong that the player should know (shown on the menu).</summary>
        public string Warning { get; private set; }
        /// <summary>True once a save has failed: progress is kept for this session only.</summary>
        public bool InMemoryOnly { get; private set; }
        /// <summary>True when the newest snapshot was invalid and an older one (or a fresh profile) was used.</summary>
        public bool Recovered { get; private set; }

        ProfileService(IProfileStorage storage, PlayerProfile profile)
        {
            this.storage = storage;
            Profile = profile;
        }

        /// <summary>
        /// Load the highest-generation VALID snapshot. Invalid ones are moved aside (never
        /// deleted), so a bad primary cannot later be rotated over a good backup. Nothing valid
        /// at all gives a default profile; nothing is written until the first explicit save.
        /// </summary>
        public static ProfileService Load(IProfileStorage storage)
        {
            List<string> snapshots;
            try { snapshots = storage.ReadAll(); }
            catch (Exception e)
            {
                var s = new ProfileService(storage, PlayerProfile.CreateDefault());
                s.InMemoryOnly = true;
                s.Warning = $"Save storage unavailable ({e.Message}). Progress will not be kept.";
                return s;
            }

            PlayerProfile best = null;
            int invalid = 0;
            foreach (var json in snapshots)
            {
                if (TryParse(json, out var p, out _))
                {
                    if (best == null || p.generation > best.generation) best = p;
                }
                else
                {
                    invalid++;
                    try { storage.PreserveInvalid(json); } catch (Exception) { /* evidence is best effort */ }
                }
            }

            var service = new ProfileService(storage, best ?? PlayerProfile.CreateDefault());
            if (invalid > 0)
            {
                service.Recovered = true;
                service.Warning = best != null
                    ? "Your save was damaged; the last good copy was loaded."
                    : "Your save was damaged and no good copy was found; a new profile was started. The damaged file was kept.";
            }
            return service;
        }

        public static string ToJson(PlayerProfile p) => JsonUtility.ToJson(p);

        /// <summary>Parse and validate. Any structural or range problem rejects the whole snapshot.</summary>
        public static bool TryParse(string json, out PlayerProfile profile, out string why)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(json)) { why = "empty"; return false; }
            try { profile = JsonUtility.FromJson<PlayerProfile>(json); }
            catch (Exception e) { why = "not JSON: " + e.Message; profile = null; return false; }
            if (profile == null) { why = "null"; return false; }
            if (!Validate(profile, out why)) { profile = null; return false; }
            return true;
        }

        /// <summary>
        /// Strict on purpose (my ruling, D73): a value out of range means the file was damaged or
        /// edited, and a clamped guess is worse than the last good backup. Missing lists (an
        /// older build's file) are filled in, not rejected.
        /// </summary>
        public static bool Validate(PlayerProfile p, out string why)
        {
            why = null;
            if (p.version != PlayerProfile.CurrentVersion) { why = $"version {p.version}"; return false; }
            if (p.generation < 0) { why = "generation"; return false; }
            p.settings ??= new ProfileSettings();
            p.mastery ??= new MasteryState();
            p.stats ??= new ProfileStats();
            p.ownedNodes ??= new List<string>();
            p.equippedNodes ??= new List<string>();
            p.achievements ??= new List<AchievementEntry>();
            p.records ??= new List<RunRecord>();
            p.finalizedRunIds ??= new List<string>();
            if (string.IsNullOrEmpty(p.styleId)) p.styleId = "snatcher";

            var s = p.settings;
            if (s.displayMode < -1 || s.displayMode > 1) { why = "displayMode"; return false; }
            if (!(s.uiScale >= ProfileSettings.MinUiScale - 1e-4f && s.uiScale <= ProfileSettings.MaxUiScale + 1e-4f)) { why = "uiScale"; return false; }

            var m = p.mastery;
            if (m.level < 1 || m.level > Mastery.MaxLevel) { why = "level"; return false; }
            if (m.xp < 0 || m.totalXp < 0 || m.points < 0) { why = "xp/points"; return false; }
            // You can never hold more points than levels gained, minus what nodes cost.
            if (m.points + p.ownedNodes.Count > m.level - 1) { why = "points exceed levels"; return false; }

            var st = p.stats;
            if (st.runs < 0 || st.victories < 0 || st.victories > st.runs || st.kills < 0 || st.bossesDefeated < 0
                || st.perfectShots < 0 || st.packetsReleased < 0 || st.backfires < 0
                || !(st.secondsPlayed >= 0f) || float.IsInfinity(st.secondsPlayed)) { why = "stats"; return false; }

            if (p.equippedNodes.Count > Mastery.MaxEquipped) { why = "too many equipped"; return false; }
            foreach (var id in p.equippedNodes)
                if (!p.ownedNodes.Contains(id)) { why = "equipped but not owned"; return false; }
            if (new HashSet<string>(p.ownedNodes).Count != p.ownedNodes.Count) { why = "duplicate node"; return false; }
            foreach (var a in p.achievements)
                if (a == null || string.IsNullOrEmpty(a.id)) { why = "achievement"; return false; }
            foreach (var r in p.records)
                if (r == null || r.score < 0) { why = "record"; return false; }
            return ValidateExtra(p, out why);
        }

        // Later phases add their own checks (known node ids, ...) without growing this file.
        static partial void ValidateExtraImpl(PlayerProfile p, ref string why);
        static bool ValidateExtra(PlayerProfile p, out string why)
        {
            string w = null;
            ValidateExtraImpl(p, ref w);
            why = w;
            return w == null;
        }

        /// <summary>
        /// An explicit save point (run finalized, settings changed, tree changed). Failure is
        /// reported and play continues with the in-memory profile (section 8).
        /// </summary>
        public bool Save()
        {
            Profile.generation++;
            try
            {
                storage.Write(ToJson(Profile), Profile.generation);
                return true;
            }
            catch (Exception e)
            {
                InMemoryOnly = true;
                Warning = $"Saving failed ({e.Message}). Progress is kept until you close the game.";
                return false;
            }
        }

        /// <summary>
        /// The ONE path from a finished run into the profile. Keyed on the run ID, so calling it
        /// twice (a double event, a retry) adds nothing the second time. Sandbox and debug runs
        /// never submit (RunSetup.Debug's contract).
        /// </summary>
        public FinalizeResult FinalizeRun(RunSummary summary, RunSetup setup)
        {
            var result = new FinalizeResult();
            if (summary == null) { result.SkippedBecause = "no summary"; return result; }
            if (setup != null && setup.Sandbox) { result.SkippedBecause = "sandbox"; return result; }
            if (setup != null && setup.Debug) { result.SkippedBecause = "debug"; return result; }
            if (Profile.finalizedRunIds.Contains(summary.RunId)) { result.SkippedBecause = "already finalized"; return result; }

            var st = Profile.stats;
            st.runs++;
            if (summary.Reason == RunEndReason.Victory) st.victories++;
            st.kills += summary.Kills;
            st.bossesDefeated += summary.BossesDefeated;
            st.perfectShots += summary.PerfectShots;
            st.packetsReleased += summary.PacketsReleased;
            st.backfires += summary.Backfires;
            st.secondsPlayed += Mathf.Max(0f, summary.Duration);

            ApplyProgression(summary, setup, result);

            Profile.finalizedRunIds.Add(summary.RunId);
            while (Profile.finalizedRunIds.Count > PlayerProfile.FinalizedRunIdsKept) Profile.finalizedRunIds.RemoveAt(0);
            result.Applied = true;
            result.Saved = Save();
            return result;
        }

        // Mastery (Phase 9), achievements and records (Phase 10) plug in here, inside the same
        // idempotent finalization, so none of them can be applied twice for one run.
        partial void ApplyProgression(RunSummary summary, RunSetup setup, FinalizeResult result);
    }

    /// <summary>Mastery constants (section 7); the XP rules are in Mastery.cs.</summary>
    public static partial class Mastery
    {
        public const int MaxLevel = 10;
        public const int MaxEquipped = 3;
    }
}
