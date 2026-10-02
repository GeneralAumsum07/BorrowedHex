namespace BorrowedHex.Progression
{
    /// <summary>Phase 11: the saved capture style.</summary>
    public sealed partial class ProfileService
    {
        static partial void ValidateLater(PlayerProfile p, ref string why)
        {
            // Section 7: an unknown style id falls back to Snatcher; it never makes the profile
            // invalid. A save written by a newer build with a style this one lacks would
            // otherwise lose the player's whole profile over one cosmetic-scale field.
            if (!CaptureStyles.IsKnown(p.styleId)) p.styleId = CaptureStyles.Snatcher;
        }
    }
}
