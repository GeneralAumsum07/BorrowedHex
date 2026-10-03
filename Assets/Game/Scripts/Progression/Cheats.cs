using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    /// <summary>
    /// Developer cheats, toggled from the main menu (owner request, 3 Oct 2026).
    ///
    /// Session-only on purpose: these are statics, never fields of <see cref="PlayerProfile"/>,
    /// so nothing here can be serialized. The save validator is strict (D73) and would reject a
    /// file that owned nodes above its mastery level, so "unlock all" is expressed as a query
    /// override (<see cref="SkillTree.ActiveNodes"/>, <see cref="SkillTree.IsOwned"/>) rather
    /// than by writing nodes into the profile.
    /// Restarting the game turns every cheat off.
    /// </summary>
    public static class Cheats
    {
        /// <summary>Hits still land (flash, invulnerability window) but cost no life, and the life clock is frozen.</summary>
        public static bool Invincible;

        /// <summary>Every skill node is active in runs and shows as owned in the tree (D101: owned = active).</summary>
        public static bool UnlockAllNodes;

        public static bool AnyActive => Invincible || UnlockAllNodes;

        /// <summary>
        /// Switch the unlocked tree on or off. Nothing in the profile changes either way (D101
        /// removed equips, the only state the cheat used to leave behind), so turning it off
        /// leaves exactly what was bought.
        /// </summary>
        public static void SetUnlockAllNodes(bool on) => UnlockAllNodes = on;

        /// <summary>
        /// Stamp the cheats into a run's setup. ANY active cheat marks the run Debug: an
        /// invincible run or one with unearned passives must never award XP, records or
        /// achievements (Debug is the existing contract FinalizeRun checks).
        /// </summary>
        public static void ApplyTo(RunSetup setup)
        {
            setup.Invincible = Invincible;
            if (AnyActive) setup.Debug = true;
        }
    }
}
