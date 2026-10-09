using System;
using System.Collections.Generic;

namespace BorrowedHex.Narrative
{
    /// <summary>How a scene ended. Only Completed and Skipped count as "seen".</summary>
    public enum NarrativeEnd
    {
        Completed,
        /// <summary>The player pressed Skip scene.</summary>
        Skipped,
        /// <summary>Already seen and "Skip familiar scenes" is on: it never appeared.</summary>
        AutoSkipped,
        /// <summary>Torn down mid-scene (restart, menu return): a partial view is not a viewing.</summary>
        Cancelled,
    }

    /// <summary>A scene the flow should present now, or hand straight on if <see cref="AutoSkip"/>.</summary>
    public sealed class NarrativeDelivery
    {
        public readonly NarrativeScene Scene;
        public readonly bool AutoSkip;
        /// <summary>From the Story menu: changes no discovery state when it ends.</summary>
        public readonly bool IsReplay;

        public NarrativeDelivery(NarrativeScene scene, bool autoSkip, bool isReplay)
        {
            Scene = scene; AutoSkip = autoSkip; IsReplay = isReplay;
        }
    }

    /// <summary>
    /// Lore plan Task 1: decides WHICH scene plays at each story trigger and records what the
    /// player has discovered. Pure C# with no Unity dependency, so every rule is EditMode-testable;
    /// GameRoot only translates sim/presentation moments into the On* calls and plays what comes
    /// back.
    ///
    /// Rules:
    ///  - Only an eligible run (a scored, non-debug short run) delivers or unlocks anything.
    ///  - Each scene is delivered at most once per run ID: pause/resume and repeated arrival
    ///    notifications re-raise the same moment, and must not replay the scene.
    ///  - Reaching a trigger unlocks its scene; Completed/Skipped mark it seen; Cancelled does not.
    ///  - A seen scene with "Skip familiar scenes" on is delivered as AutoSkip: the flow still
    ///    learns of the moment (so it hands on) but nothing is shown.
    ///  - Victory completes the story whether or not the ending is watched.
    /// </summary>
    public sealed class NarrativeDirector
    {
        readonly NarrativeProfileState state;
        readonly HashSet<string> delivered = new HashSet<string>();
        string runId;
        bool eligible;
        bool confrontationEnded;
        NarrativeDelivery current;

        /// <summary>Raised once when a started delivery ends, however it ends.</summary>
        public event Action<NarrativeScene, NarrativeEnd> SceneFinished;

        public NarrativeDirector(NarrativeProfileState state) => this.state = state ?? new NarrativeProfileState();

        public NarrativeProfileState State => state;
        public NarrativeScene Current => current?.Scene;
        /// <summary>A scene is on screen.</summary>
        public bool IsPlaying => current != null;
        /// <summary>The run flow must wait (no combat, no upgrade input, no results input).</summary>
        public bool BlocksFlow => IsPlaying;
        /// <summary>
        /// The boss fight may not start yet: this run still owes the confrontation. False in
        /// practice and endless runs, which never wait for a story.
        /// </summary>
        public bool HasPendingBossScene => eligible && !confrontationEnded;
        public bool RunEligible => eligible;

        /// <summary>A new run (or a replaced sim). Ends any scene from the old run as Cancelled.</summary>
        public void BeginRun(string id, bool storyEligible)
        {
            Cancel();
            runId = id;
            eligible = storyEligible;
            confrontationEnded = false;
            delivered.Clear();
        }

        // ---- Triggers (plan section 1, "Story placement") -------------------------------

        public NarrativeDelivery OnRunStarted() => Deliver(NarrativeCatalog.Prologue);

        /// <summary>Encounters one and two each have a scene; the third leads to the Sanctum instead.</summary>
        public NarrativeDelivery OnEncounterCleared(int encountersCleared) =>
            encountersCleared == 1 ? Deliver(NarrativeCatalog.LastKindness)
            : encountersCleared == 2 ? Deliver(NarrativeCatalog.ForgottenAnswer)
            : null;

        /// <summary>The Sanctum pull and reveal have finished; the final upgrades are still hidden.</summary>
        public NarrativeDelivery OnSanctumArrived() => Deliver(NarrativeCatalog.Anomaly);

        public NarrativeDelivery OnFinalUpgradeResolved() => Deliver(NarrativeCatalog.Confrontation);

        public NarrativeDelivery OnVictory()
        {
            if (!eligible) return null;
            // Completion is earned by the win, not by watching the ending.
            state.storyCompleted = true;
            return Deliver(NarrativeCatalog.OpenWindow);
        }

        NarrativeDelivery Deliver(string id)
        {
            if (!eligible || runId == null || !delivered.Add(id)) return null;
            state.Unlock(id);
            bool familiar = state.IsSeen(id) && state.settings.skipFamiliar;
            return new NarrativeDelivery(NarrativeCatalog.Get(id), familiar, false);
        }

        // ---- Story menu ----------------------------------------------------------------

        /// <summary>Unlocked scenes in narrative order. Undiscovered scenes stay hidden.</summary>
        public List<NarrativeScene> Replayable()
        {
            var list = new List<NarrativeScene>();
            foreach (var s in NarrativeCatalog.Scenes) if (state.IsUnlocked(s.Id)) list.Add(s);
            return list;
        }

        /// <summary>A replay of an unlocked scene; null if it was never reached.</summary>
        public NarrativeDelivery Replay(string id)
        {
            var scene = NarrativeCatalog.Get(id);
            return scene != null && state.IsUnlocked(id) ? new NarrativeDelivery(scene, false, true) : null;
        }

        // ---- Playback ------------------------------------------------------------------

        /// <summary>
        /// Begin a delivery. An AutoSkip delivery ends at once (AutoSkipped) without ever
        /// becoming the playing scene, so the flow can hand on in the same frame.
        /// </summary>
        public void Start(NarrativeDelivery delivery)
        {
            if (delivery == null) return;
            Cancel();
            if (delivery.AutoSkip) { End(delivery, NarrativeEnd.AutoSkipped); return; }
            current = delivery;
        }

        /// <summary>The playing scene ended by completion or Skip scene. Cancellation goes through <see cref="Cancel"/>.</summary>
        public void Finish(NarrativeEnd how)
        {
            if (current == null) return;
            var d = current;
            current = null;
            End(d, how);
        }

        /// <summary>Teardown: the playing scene (if any) ends as Cancelled and is not marked seen.</summary>
        public void Cancel() => Finish(NarrativeEnd.Cancelled);

        void End(NarrativeDelivery d, NarrativeEnd how)
        {
            if (!d.IsReplay)
            {
                if (how == NarrativeEnd.Completed || how == NarrativeEnd.Skipped) state.MarkSeen(d.Scene.Id);
                // Any end, even a teardown, releases the boss hold: a cancelled run has no boss left to start.
                if (d.Scene.Id == NarrativeCatalog.Confrontation) confrontationEnded = true;
            }
            SceneFinished?.Invoke(d.Scene, how);
        }
    }
}
