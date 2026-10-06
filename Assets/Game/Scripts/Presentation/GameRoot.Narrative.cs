using BorrowedHex.Core;
using BorrowedHex.Runs;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Lore plan (Docs/LORE_IMPLEMENTATION_PLAN.md), submission cut: the short run's story told
    /// on typed black screens. GameRoot owns WHEN a scene plays; NarrativePanel owns how it looks.
    ///
    /// Beats, all in a scored (non-debug) short run only, each at most once per run:
    ///   run start          → prologue            (before the first gameplay tick)
    ///   encounter 1 clear  → last kindness       (over the upgrade choice, before it is usable)
    ///   encounter 2 clear  → forgotten answer    (likewise)
    ///   Sanctum pull done  → anomaly + confrontation, before the boss fight starts
    ///   victory            → open window         (before the results panel is usable)
    ///
    /// Deviation from the plan, deliberate for the deadline: the third upgrade choice still
    /// comes BEFORE the Sanctum pull (the sim's existing order), so the anomaly plays after the
    /// pull rather than between the pull and the upgrades. No sim reordering, no new RunState.
    ///
    /// Every scene holds PauseReason.Narrative, which only this file sets or clears: the clock
    /// is frozen (no life spent while reading) and any other hold keeps its own ownership.
    /// </summary>
    public sealed partial class GameRoot
    {
        public NarrativePanel Story { get; private set; }

        /// <summary>
        /// Test hook only (like IgnoreFocus): older PlayMode tests drive a short run straight
        /// into combat/boss/results and predate the story holds. Never set by game code.
        /// </summary>
        public static bool SkipStory;

        // Per-run delivery flags. A pause/resume re-raises RunStateChanged with the same state,
        // so without these a scene would replay on every resume.
        bool storyEnc1, storyEnc2, storyBoss, storyEnd;

        /// <summary>
        /// Story beats belong to scored short runs only: never the tutorial, practice, endless
        /// or a debug run (plan section 1).
        /// </summary>
        bool StoryRun => !SkipStory && kind == RunKind.Short && Sim != null && Sim.IsShortRun && !Sim.Setup.Debug;

        /// <summary>
        /// The world presentation must not start the boss fight while the Sanctum scene is owed
        /// or playing; GameRoot starts it once the scene ends (see TickNarrative).
        /// </summary>
        public bool NarrativeHoldsBossIntro => Story != null && (Story.IsPlaying || (StoryRun && !storyBoss));

        void BuildNarrative()
        {
            Story = NarrativePanel.Create(canvas);
            // Just under the pause menu: Esc during a scene shows the menu ON TOP of the story,
            // and everything built later (confirm dialog, main-menu screens) stays above too.
            Story.transform.SetSiblingIndex(Menu.transform.GetSiblingIndex());
        }

        /// <summary>From BeginRun: drop the old run's scene and bind the new sim.</summary>
        void BindNarrative()
        {
            Story.Cancel();
            storyEnc1 = storyEnc2 = storyBoss = storyEnd = false;
            if (!StoryRun) return;
            Sim.Events.RunStateChanged += OnStoryRunState;
            Sim.Events.RunEnded += OnStoryRunEnded;
            PlayScene(Prologue);
        }

        void OnStoryRunState(RunState s)
        {
            if (s != RunState.UpgradeChoice) return;
            if (Sim.TransitionsReached == 1 && !storyEnc1) { storyEnc1 = true; PlayScene(LastKindness); }
            else if (Sim.TransitionsReached == 2 && !storyEnc2) { storyEnc2 = true; PlayScene(ForgottenAnswer); }
        }

        void OnStoryRunEnded(RunSummary summary)
        {
            // Death and time expiry stay instantly restartable: no blocking scene there.
            if (summary.Reason != RunEndReason.Victory || storyEnd) return;
            storyEnd = true;
            // Score, records and rewards were finalized by OnRunEnded inside the same tick; the
            // ending only holds the results panel, it never finalizes anything again.
            PlayScene(OpenWindow);
        }

        void PlayScene(NarrativePage[] scene)
        {
            Sim.SetPause(PauseReason.Narrative, true);
            // Capture the sim this scene belongs to: a restart mid-scene cancels the panel
            // (no callback), but this guard also keeps a stale callback off the new sim.
            var owner = Sim;
            Story.Play(scene, () =>
            {
                if (Sim != owner) return;
                Sim.SetPause(PauseReason.Narrative, false);
                SyncGameplayInput();
            });
            SyncGameplayInput();
        }

        /// <summary>
        /// Every frame, before the boss-intro check: freeze typing under the pause menu, put
        /// focus back after a scene, and deliver the Sanctum scene once the pull has landed.
        /// </summary>
        void TickNarrative()
        {
            if (Story == null) return;
            Story.Frozen = Screens.Count > 0 || (Sim != null && Sim.Clock.HasPauseReason(PauseReason.FocusLost));
            Story.RestoreFocusIfPending();
            if (!StoryRun || storyBoss || Story.IsPlaying) return;
            // Classic arenas have no travel, so this fires straight away; world arenas wait
            // for the pull and reveal to finish (WorldTransition cleared).
            if (Sim.State == RunState.BossIntro && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition))
            {
                storyBoss = true;
                PlayScene(Confrontation);
            }
        }

        // ---- Script (plan section 2; kept in code so no optional asset can remove it) -------

        static NarrativePage N(string text) => new NarrativePage(null, text);
        static NarrativePage S(string speaker, string text) => new NarrativePage(speaker, text);

        static readonly NarrativePage[] Prologue =
        {
            N("The Collector executed you. Your heart is beating again—but every beat spends time that isn't yours."),
            N("The baker whispered warmth into his ovens. The healer borrowed another morning. You made coins disappear. The children waited, smiling, for their return."),
            N("Later, you carried letters through wards where nobody left. Some letters escaped. One register didn't survive your visit. The next entry was your own."),
            N("You caught the binding meant to claim you and stole its remaining time. Your true name stayed in his Ledger."),
            N("A physician refused to let his patients die. He kept their bodies, their homes, and finally the world."),
            N("Your world remained. Its health did not. Those who stayed too long became something worse."),
            N("Every heartbeat spends that time. Your own spellmaking died with your life. Borrow your enemies' magic. Reach the Ledger. Take your name back."),
        };

        static readonly NarrativePage[] LastKindness =
        {
            N("Before he was The Collector, Avel Sere was a mortal physician. During a famine, he borrowed years to keep his patients alive."),
            N("His sister Mara recorded their last wishes. When her time came, she asked him to open the window."),
            N("He closed it, cut the ending from her healing spell, and made her stay."),
            N("Beside her name, she wrote: Enough. He crossed it out."),
        };

        static readonly NarrativePage[] ForgottenAnswer =
        {
            N("Deep in the House, patients lie beneath clean blankets. Beside each bed is a record of consent dated centuries ago."),
            S("An unfiled request", "I asked for time to see my son. He came. We spoke. He went home. What is the rest of this for?"),
            S("Mara's correction", "In the Ledger's margins, Mara writes: He remembers everything about us except the last thing we said."),
        };

        // The anomaly revelation runs straight into the confrontation: both play in the Sanctum,
        // after the pull, before the fight.
        static readonly NarrativePage[] Confrontation =
        {
            N("The Collector means to seal every renewal into one unbroken circle. Nothing within his keeping would ever be permitted to leave."),
            N("But you are neither ordinarily alive nor one of his bound dead. Your unfinished entry keeps that circle open."),
            N("Killing you only begins the failed renewal again. He must reclaim your torn entry. Mara's corrections show where the buried exits remain."),
            S("The Collector", "There you are. Your bed is still made."),
            S("The Collector", "You have already spent several of my patients. Shall I tell you their names?"),
            S("You", "You kept their signatures. Did you keep their answers?"),
            S("The Collector", "They were tired."),
            S("You", "They told you what they wanted."),
            S("The Collector", "Until your entry is settled, nothing can be finished."),
            S("You", "Then I'm opening their entries before I close mine."),
        };

        static readonly NarrativePage[] OpenWindow =
        {
            N("The Collector falls. His Ledger remains. While its renewals endure, even he can be recalled."),
            N("You place the torn transfer beside your true name. Closing only your entry would complete his circle."),
            N("You leave your name open and restore the dismissals he buried."),
            N("The dead can leave. Those with time remaining can spend it freely. Exhausted buildings fall. Places capable of growth can change again."),
            N("Mara's entry closes. Beside it, one word remains: Enough."),
            N("The world does not become young. What survives can finally have a future."),
            S("The Collector", "The lamp. Please. Don't leave me in the dark."),
            S("You", "Is there someone I should send for?"),
            N("You open the window. You stay until the lamp goes out. Then you close your own entry."),
            N("There will be no further recall. The unused time leaves your hands. There is light beyond the window."),
        };
    }
}
