using UnityEngine;

namespace BorrowedHex.Core
{
    /// <summary>
    /// D100 (owner): life is shown x10 so small gains (lifesteal, D99) read as whole numbers.
    /// DISPLAY ONLY: the sim, the tuning and every test keep counting seconds, so one constant
    /// changes what the player reads without touching balance. Every number the player sees
    /// about life goes through here, so the scale can never be applied twice or forgotten.
    /// </summary>
    public static class LifeDisplay
    {
        public const float Scale = 10f;

        public static int Points(double seconds) => Mathf.RoundToInt((float)(seconds * Scale));

        /// <summary>"+12" / "-100" for the floating pops; 0 reads as "0".</summary>
        public static string Signed(double seconds)
        {
            int p = Points(seconds);
            return p > 0 ? "+" + p : p.ToString();
        }
    }
}
