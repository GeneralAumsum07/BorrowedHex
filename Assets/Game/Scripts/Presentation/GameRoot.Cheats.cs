using BorrowedHex.Progression;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// The main menu's Cheats entry (owner request, 3 Oct 2026). The rules live in
    /// <see cref="Cheats"/>; this half only opens the panel and keeps the menu honest about
    /// what a cheat costs (a cheated run never counts).
    /// </summary>
    public sealed partial class GameRoot
    {
        public CheatsPanel CheatPanel { get; private set; }

        void OpenCheats()
        {
            CheatPanel.Show(ToggleInvincible, ToggleUnlockAll, CloseCheats);
            // Esc runs the same close as Back, so the main menu's warning line refreshes either way.
            Screens.Push(CheatPanel.gameObject, () => CheatPanel.DefaultFocus, CloseCheats);
        }

        void ToggleInvincible() => Cheats.Invincible = !Cheats.Invincible;

        // The profile is untouched either way (see Cheats.SetUnlockAllNodes), so no save.
        void ToggleUnlockAll() => Cheats.SetUnlockAllNodes(!Cheats.UnlockAllNodes);

        void CloseCheats()
        {
            if (Screens.Top == CheatPanel.gameObject) Screens.Pop();
            RefreshMainMenu();
        }
    }
}
