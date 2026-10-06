using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Menu colour tokens (plan section 0.3). Golds are the Dark Ages pack's own swatches so
    /// text never fights the frames. Combat colours stay in FeedbackColors and never come from here.
    /// </summary>
    public static class UiPalette
    {
        static Color Hex(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static readonly Color Ink = Hex(0x0E0B14);          // deepest background
        public static readonly Color PanelDeep = Hex(0x15121C, 0.96f);
        public static readonly Color Panel = Hex(0x1E1928, 0.94f);   // flat fallback for frames
        public static readonly Color Ivory = Hex(0xEEE7D8);         // body text
        public static readonly Color Muted = Hex(0x9A90A6);         // secondary text, disabled
        public static readonly Color Honey = Hex(0xDCC47C);         // headings, primary captions
        public static readonly Color Camel = Hex(0xC19149);         // frame lines (flat fallback), quiet buttons
        public static readonly Color Violet = Hex(0x9B6BE0);        // magic: focus, mastery, selection
        public static readonly Color Blood = Hex(0xB3373F);         // Life
        public static readonly Color Warning = Hex(0xDA7777);       // warnings, failures (text)
        public static readonly Color Good = Hex(0x8FC77A);          // earned, saved
        public static readonly Color Scrim = Hex(0x07050B, 0.78f);  // behind modal screens
        public static readonly Color ParchSoft = Hex(0x4A3B2A);     // secondary text on parchment (Ink is primary)
    }
}
