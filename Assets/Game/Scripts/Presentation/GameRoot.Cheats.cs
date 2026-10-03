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
            Main.Show(false);
            CheatPanel.Show(ToggleInvincible, ToggleUnlockAll, CloseCheats);
        }

        void ToggleInvincible() => Cheats.Invincible = !Cheats.Invincible;

        void ToggleUnlockAll()
        {
            Cheats.SetUnlockAllNodes(!Cheats.UnlockAllNodes, Profile.Profile);
            // Switching it off may have unequipped unearned nodes; save so the file matches
            // what the tree panel now shows (the save itself never holds cheat nodes).
            if (!Cheats.UnlockAllNodes) Profile.Save();
        }

        void CloseCheats()
        {
            CheatPanel.Hide();
            RefreshMainMenu();
            Main.Show(true);
        }

        static string CheatsWarning()
        {
            if (!Cheats.AnyActive) return null;
            string which = Cheats.Invincible && Cheats.UnlockAllNodes ? "Invincible, all skills unlocked"
                : Cheats.Invincible ? "Invincible" : "All skills unlocked";
            return $"CHEATS ON ({which}): runs give no XP, records or achievements.";
        }
    }
}
