using System;
using BorrowedHex.Core;
using BorrowedHex.Combat;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation.Audio;
using BorrowedHex.Runs;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Lore plan (Docs/LORE_IMPLEMENTATION_PLAN.md) Task 3: the adapter between the run and the
    /// story. NarrativeDirector decides WHICH scene a moment owes (and remembers what was seen);
    /// NarrativeCatalog owns the words; NarrativePanel owns how a scene looks. This file only
    /// turns sim/presentation moments into director triggers and holds the run while a scene is up.
    ///
    /// Short run order (scored, non-debug only):
    ///   run start                → prologue, before the first gameplay tick
    ///   encounter 1 / 2 clear     → scene over the (hidden) upgrade choice
    ///   encounter 3 clear         → sim prepares the Sanctum and boss → SanctumArrival
    ///   pull and reveal landed    → anomaly → CompleteSanctumArrival → final upgrades
    ///   final choice              → BossIntro → confrontation → fight
    ///   victory                   → ending, holding the results
    /// Every other run (endless, practice, tutorial, debug) still passes through the same
    /// handoffs here, with no scene: arrival completes and the fight starts as soon as allowed.
    ///
    /// Every scene holds PauseReason.Narrative, which only this file sets or clears: the clock
    /// is frozen (no life spent while reading) and any other hold keeps its own ownership.
    /// </summary>
    public sealed partial class GameRoot
    {
        public NarrativePanel Story { get; private set; }
        /// <summary>The discovery rules, over the profile's narrative group (saved at story boundaries).</summary>
        public NarrativeDirector Director { get; private set; }
        /// <summary>The main menu's Story screen (lore plan Task 4).</summary>
        public StoryPanel StoryMenu { get; private set; }
        bool replaying;

        /// <summary>The dialogue tick (lore plan Task 5), fed by Story.Ticked in runs and replays alike.</summary>
        public DialogueTextAudio TickAudio { get; private set; }
        /// <summary>The silent combat caption (lore plan Task 5, section G).</summary>
        public UnityEngine.UI.Text ReactionCaption { get; private set; }
        // How long a shown caption stays, counted only while it is actually visible.
        const float ReactionSeconds = 3f;
        float reactionLeft;
        // Fresh per run: "once per run" is this object's lifetime.
        CombatReactions reactions;

        /// <summary>
        /// Test hook only (like IgnoreFocus): older PlayMode tests drive a short run straight
        /// into combat/boss/results and predate the story holds. Never set by game code.
        /// </summary>
        public static bool SkipStory;

        // The sim the story is bound to, so its subscriptions come off when it is replaced.
        ArenaSim storySim;
        // The arrival is reported once per run: pause/resume re-enters SanctumArrival.
        bool arrivalReported;
        int storyRunSerial;

        /// <summary>
        /// Story beats belong to scored short runs only: never the tutorial, practice, endless
        /// or a debug run (plan section 1).
        /// </summary>
        bool StoryRun => !SkipStory && kind == RunKind.Short && Sim != null && Sim.IsShortRun && !Sim.Setup.Debug;

        /// <summary>
        /// The boss fight may not start: a scene is up, or this run still owes its confrontation.
        /// GameRoot is the only caller of CompleteBossIntro for short runs (WorldPresentation
        /// reports the pull's end by clearing WorldTransition and does no more).
        /// </summary>
        public bool NarrativeHoldsBossIntro => Story != null && (Story.IsPlaying || Director.HasPendingBossScene);

        /// <summary>Dialogue sprites: the existing character art, never new portraits (plan Task 2).</summary>
        static UnityEngine.Sprite Portrait(Speaker s) =>
            s == Speaker.Collector ? PixelSprites.Get(PixelSprites.Kind.Collector)
            : s == Speaker.Rogue ? PixelSprites.Get(PixelSprites.Kind.Magician)
            : null;

        void BuildNarrative()
        {
            // Bound to the live profile object: discoveries made in a run are in the profile at
            // once, and reach disk at the next save point (Present's story boundaries). Debug,
            // cheated, endless, practice and tutorial runs are ineligible, so the director never
            // writes to it for them.
            Director = new NarrativeDirector(Profile.Profile.narrative);
            Story = NarrativePanel.Create(canvas, Portrait);
            // Optional pictures (plan Task 6): a sprite at Resources/Narrative/<key> overrides
            // the black of an illustrated lore page. None ship today; a missing one is null and
            // the page stays black, so the story never depends on art. (Resources caches loads.)
            Story.Illustrations = key => UnityEngine.Resources.Load<UnityEngine.Sprite>("Narrative/" + key);
            DockStory();
            StoryMenu = StoryPanel.Create(canvas);
            Main.AddEntry("story", "Story", OpenStory);

            TickAudio = gameObject.AddComponent<DialogueTextAudio>();
            // Read at each tick, so a Settings change mid-scene is heard at the next letter.
            TickAudio.Volume = () => Director.State.settings.dialogueVolume;
            Story.Ticked += TickAudio.Tick;

            // Compact, no box: one line just under the HUD's top band (objective -88, boss bar
            // -128..-150), so it never covers the arena's centre or a HUD readout.
            ReactionCaption = UiKit.Text("Reaction", canvas.transform, "", UiFonts.Role.Body, UnityEngine.TextAnchor.MiddleCenter);
            Ui.Place(ReactionCaption.rectTransform, new UnityEngine.Vector2(0.5f, 1), new UnityEngine.Vector2(0, -184), new UnityEngine.Vector2(900, 40));
            ReactionCaption.color = UiPalette.Honey;
            ReactionCaption.raycastTarget = false;
            ReactionCaption.gameObject.SetActive(false);
            // Above the HUD and the upgrade panel (a paid upgrade's line shows over the cards),
            // under the story and the pause menu.
            ReactionCaption.transform.SetSiblingIndex(Story.transform.GetSiblingIndex());
        }

        /// <summary>
        /// Just under the pause menu: Esc during a scene shows the menu ON TOP of the story,
        /// and everything built later (confirm dialog, main-menu screens) stays above too.
        /// </summary>
        void DockStory() => Story.transform.SetSiblingIndex(Menu.transform.GetSiblingIndex());

        // ---- Story menu (lore plan Task 4) ---------------------------------------------

        void OpenStory()
        {
            StoryMenu.Show(() => Profile.Profile.narrative, Replay, CloseStory);
            Screens.Push(StoryMenu.gameObject, () => StoryMenu.DefaultFocus, CloseStory);
        }

        void CloseStory()
        {
            if (Screens.Top == StoryMenu.gameObject) Screens.Pop();
        }

        /// <summary>
        /// Reread an unlocked scene from the main menu. No run starts (the backdrop stays the
        /// backdrop), nothing is scored, and the director leaves seen/completed untouched.
        /// </summary>
        void Replay(string id)
        {
            var d = Director.Replay(id);
            if (d == null || Story.IsPlaying) return;
            Director.Start(d);
            replaying = true;
            // Above the Story list for the replay only; docked back under the pause menu after.
            Story.transform.SetAsLastSibling();
            Story.Frozen = false;
            Story.ReduceFlashes = DisplayOptions.ReduceFlashes;
            Story.InstantText = Director.State.settings.instantText;
            Story.Play(d.Scene, how =>
            {
                TickAudio.Stop();
                Director.Finish(how);
                EndReplay();
            });
        }

        void EndReplay()
        {
            replaying = false;
            DockStory();
            // Focus back on the list, where the replay was chosen.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && StoryMenu.IsOpen) es.SetSelectedGameObject(StoryMenu.DefaultFocus);
        }

        /// <summary>Esc on the main menu during a replay: end the replay, stay on the list.</summary>
        bool EscapeReplay()
        {
            if (!replaying || !Story.IsPlaying) return false;
            Story.Skip();
            return true;
        }

        /// <summary>
        /// From BeginRun: tear the old run's story down completely (subscriptions, typing, the
        /// panel, the pending scenes) and bind the new sim. The old sim is discarded whole, so
        /// its Narrative pause goes with it; nothing else's pause is touched.
        /// </summary>
        void BindNarrative()
        {
            UnbindNarrative();
            arrivalReported = false;
            Director.BeginRun($"run-{++storyRunSerial}", StoryRun);
            reactions = new CombatReactions();
            Flow.Recall = null;
            storySim = Sim;
            Sim.Events.RunStateChanged += OnStoryRunState;
            Sim.Events.RunEnded += OnStoryRunEnded;
            Sim.Events.ShotCaptured += OnReactCapture;
            Sim.Events.PacketBackfired += OnReactBackfire;
            Sim.Events.UpgradePaid += OnReactPaid;
            Present(Director.OnRunStarted(), null);
        }

        void UnbindNarrative()
        {
            Story.Cancel();
            TickAudio.Stop();
            Director.Cancel();
            if (replaying) EndReplay();
            Flow.HoldUpgrades = false;
            HideReaction();
            if (storySim == null) return;
            storySim.Events.RunStateChanged -= OnStoryRunState;
            storySim.Events.RunEnded -= OnStoryRunEnded;
            storySim.Events.ShotCaptured -= OnReactCapture;
            storySim.Events.PacketBackfired -= OnReactBackfire;
            storySim.Events.UpgradePaid -= OnReactPaid;
            storySim = null;
        }

        void OnStoryRunState(RunState s)
        {
            // Encounters one and two. The third clear goes to SanctumArrival instead, and a
            // resume re-raising UpgradeChoice is deduplicated by the director.
            if (s == RunState.UpgradeChoice) Present(Director.OnEncounterCleared(Sim.TransitionsReached), null);
        }

        void OnStoryRunEnded(RunSummary summary)
        {
            // Death and time expiry stay instantly restartable: no blocking scene there, only
            // the recall line on the results (a retire is a choice, not a recall: no line).
            if (summary.Reason == RunEndReason.Death || summary.Reason == RunEndReason.TimeExpired)
            {
                if (StoryRun) Flow.Recall = NarrativeCatalog.RecallCaption;
                return;
            }
            if (summary.Reason != RunEndReason.Victory) return;
            // Score, records and rewards were finalized by OnRunEnded inside the same tick; the
            // ending only holds the results panel, it never finalizes anything again.
            Present(Director.OnVictory(), null);
        }

        /// <summary>
        /// Show <paramref name="delivery"/> (if any) under a Narrative hold, then run
        /// <paramref name="then"/>. A null or familiar (auto-skipped) delivery hands on at once.
        /// </summary>
        void Present(NarrativeDelivery delivery, Action then)
        {
            if (delivery == null) { then?.Invoke(); return; }
            Director.Start(delivery);
            // Story boundary: the trigger was reached, so its unlock is kept even if this run is
            // abandoned mid-scene (plan Task 4). One write per scene start, never per letter.
            Profile.Save();
            if (!Director.IsPlaying) { then?.Invoke(); return; }   // AutoSkipped: never shown

            Sim.SetPause(PauseReason.Narrative, true);
            TickAudio.Stop();   // a new scene: nothing from the last one plays on
            // Capture the sim this scene belongs to: a restart mid-scene cancels the panel
            // (no callback), and this guard also keeps any stale callback off the new sim.
            var owner = Sim;
            Story.InstantText = Director.State.settings.instantText;
            Story.Play(delivery.Scene, how =>
            {
                if (Sim != owner) return;
                TickAudio.Stop();
                Director.Finish(how);
                Profile.Save();   // story boundary: seen (and completion) reach disk
                Sim.SetPause(PauseReason.Narrative, false);
                SyncGameplayInput();
                then?.Invoke();
            });
            SyncGameplayInput();
        }

        /// <summary>
        /// Every frame, before the boss-intro check: freeze typing under the pause menu, put
        /// focus back after a scene, and report the Sanctum arrival / deliver the confrontation
        /// once their gates have cleared.
        /// </summary>
        void TickNarrative()
        {
            if (Story == null || Sim == null) return;
            Story.Frozen = Screens.Count > 0 || Sim.Clock.HasPauseReason(PauseReason.FocusLost);
            // Frozen typing raises no ticks; this cuts the one already sounding. Resuming plays
            // only new letters' ticks, never the missed ones.
            if (Story.Frozen) TickAudio.Stop();
            TickReaction();
            // Settings can change from the pause menu mid-scene; the setter ignores a repeat.
            Story.InstantText = Director.State.settings.instantText;
            Story.ReduceFlashes = DisplayOptions.ReduceFlashes;
            Story.RestoreFocusIfPending();
            // Upgrade cards open only once nothing is being read over them.
            Flow.HoldUpgrades = Story.IsPlaying;
            if (Story.IsPlaying) return;

            // The pull and reveal have landed (classic arenas never travel, so at once). A
            // pause menu shows State == Paused, so arrival is never reported under it.
            if (Sim.State == RunState.SanctumArrival && !arrivalReported
                && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition))
            {
                arrivalReported = true;
                Present(Director.OnSanctumArrived(), () => Sim.CompleteSanctumArrival());
                return;
            }
            // The final choice has been made: the confrontation, then GameRoot's boss start.
            if (Sim.State == RunState.BossIntro && Director.HasPendingBossScene
                && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition))
                Present(Director.OnFinalUpgradeResolved(), null);
        }

        // ---- Combat reactions (lore plan Task 5, section G) ------------------------------
        // Silent, instant, never pausing, never typed, never asking for input. Only in a story
        // run with the setting on; the rules (once per run, shared cooldown, discard) are
        // CombatReactions'.

        void OnReactCapture(CapturedPacket p, AttackSnapshot a, UnityEngine.Vector2 at, CaptureResult r) => React(CombatReaction.FirstCapture);
        void OnReactBackfire(CapturedPacket p) => React(CombatReaction.FirstBackfire);
        void OnReactPaid(UpgradeOffer o, float cost) => React(CombatReaction.FirstPaidUpgrade);

        void React(CombatReaction r)
        {
            if (!StoryRun || !Director.State.settings.combatReactions) return;
            // "Promptly": nothing blocking over the arena right now. UpgradeChoice counts as
            // live, since the first paid upgrade happens on the upgrade panel itself.
            var s = Sim.State;
            bool canShow = !Story.IsPlaying && Screens.Count == 0
                && (s == RunState.Combat || s == RunState.BossCombat || s == RunState.UpgradeChoice);
            // Gameplay time, so a pause menu does not run the cooldown down.
            if (!reactions.Offer(r, (float)Sim.Clock.Now, canShow)) return;
            ReactionCaption.text = CombatReactions.Line(r);
            reactionLeft = ReactionSeconds;
        }

        void TickReaction()
        {
            if (reactionLeft <= 0f) return;
            // Hidden, not dropped, under a story or a menu; its time runs only while seen.
            bool visible = !Story.IsPlaying && Screens.Count == 0;
            ReactionCaption.gameObject.SetActive(visible);
            if (!visible) return;
            reactionLeft -= UnityEngine.Time.unscaledDeltaTime;
            if (reactionLeft <= 0f) HideReaction();
        }

        void HideReaction()
        {
            reactionLeft = 0f;
            if (ReactionCaption != null) ReactionCaption.gameObject.SetActive(false);
        }
    }
}
