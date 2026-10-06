namespace BorrowedHex.UI
{
    public enum MenuSlot { Play, Mode, Character, Records, Training, Settings, Cheats, Quit, Hidden }

    /// <summary>
    /// Where each legacy main-menu key lives in the three-destination layout (spec 2). Keys stay
    /// the programmatic contract: GameRoot partials and tests keep calling AddEntry/Press with
    /// them, so the visible layout can change without touching a single caller.
    /// </summary>
    public static class MenuRouting
    {
        // Selector order: the first entry is the default mode and the fallback when the
        // remembered one is disabled.
        public static readonly string[] ModeKeys = { "play_short", "endless" };
        public static readonly string[] TrainingKeys = { "tutorial", "practice" };

        public static MenuSlot SlotFor(string key) => key switch
        {
            "play_short" or "endless" => MenuSlot.Mode,
            "mastery" => MenuSlot.Character,
            "records" => MenuSlot.Records,
            "tutorial" or "practice" => MenuSlot.Training,
            "settings" => MenuSlot.Settings,
            "cheats" => MenuSlot.Cheats,
            "quit" => MenuSlot.Quit,
            // "style" and any future key: still pressable, no button of its own. Hidden rather
            // than an error, so a new phase's entry can land before its control does.
            _ => MenuSlot.Hidden,
        };
    }
}
