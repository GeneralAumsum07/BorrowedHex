using BorrowedHex.Core;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Phase 14: the Tutorial menu entry (D86). The run itself is a RunSetup.ForTutorial sandbox
    /// whose script lives in the sim (TutorialDirector); this half only starts it, shows its
    /// panel and freezes the arena behind the completion card.
    /// </summary>
    public sealed partial class GameRoot
    {
        public TutorialPanel TutorialUi { get; private set; }

        public void PlayTutorial() => StartRun(RunKind.Tutorial);

        void BuildTutorial()
        {
            TutorialUi = TutorialPanel.Create(canvas, PlayShort, ShowMainMenu);
            // The completion card is a menu: the combat HUD steps back under it, as under pause.
            TutorialUi.CardChanged += open => Hud.SetCovered(open);
        }

        /// <summary>
        /// Once the last lesson is passed the card is up: freeze the sim behind it so nothing
        /// moves while the player reads, and (via SyncGameplayInput) take input away so a click
        /// on "Play a run" cannot also arrive as a catch. Manual is its own pause reason, so
        /// closing the pause menu over the card never resumes the arena.
        /// </summary>
        void TickTutorialCard()
        {
            if (Sim.Tutorial == null || !Sim.Tutorial.IsComplete) return;
            if (!Sim.Clock.HasPauseReason(PauseReason.Manual)) Sim.SetPause(PauseReason.Manual, true);
        }
    }
}
