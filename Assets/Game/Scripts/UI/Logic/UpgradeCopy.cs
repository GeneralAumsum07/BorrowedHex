using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>A purchase seen in Life points: before, after, and the price between them.</summary>
    public struct LifeCost { public int Now, After, Cost, Percent; public float FracNow, FracAfter; }

    /// <summary>
    /// Upgrade-choice copy (spec 2, Upgrade offers): costs in Life, said once, exactly.
    /// Pure, so the numbers are tested without a sim; RunFlowPanels only fills the strings in.
    /// </summary>
    public static class UpgradeCopy
    {
        const string Sep = "  ·  ";   // the separator the HUD and menus use

        /// <summary>The one seconds-to-points conversion (LifeDisplay), so no test hard-codes the ratio.</summary>
        public static int Points(float seconds) => LifeDisplay.Points(seconds);

        /// <param name="capSeconds">The bar's full width (starting Life): only the fractions use it.</param>
        public static LifeCost LifePreview(float lifeSeconds, float capSeconds, float costSeconds)
        {
            float cap = Mathf.Max(0.0001f, capSeconds);
            int now = Points(lifeSeconds);
            // After is converted on its own and Cost is the DIFFERENCE, not Points(cost): Points
            // rounds, and Points(a) - Points(b) can differ from Points(a - b) by one. The button
            // shows Cost too, so the bar and the button can never disagree.
            int after = Points(lifeSeconds - costSeconds);
            return new LifeCost
            {
                Now = now, After = after, Cost = now - after,
                // The sim charges a fraction of CURRENT Life (ArenaSim.TakeCost), so the percent
                // is of current Life; against the cap it would understate the price at low Life.
                Percent = lifeSeconds > 0f ? Mathf.RoundToInt(costSeconds / lifeSeconds * 100f) : 0,
                FracNow = Mathf.Clamp01(lifeSeconds / cap),
                FracAfter = Mathf.Clamp01((lifeSeconds - costSeconds) / cap),
            };
        }

        /// <summary>"Held 2/4"; the last-slot and full-set warnings are said here, once, not per card.</summary>
        public static string HeldLine(int held, int max) =>
            held >= max ? $"Held {held}/{max}{Sep}rank-ups only"
            : held == max - 1 ? $"Held {held}/{max}{Sep}last open slot"
            : $"Held {held}/{max}";

        /// <summary>The paid button: what it does, then its exact price in Life.</summary>
        public static string Action(bool rankUp, int heldCount, int rank, int cost) =>
            (rankUp ? $"Rank up to {rank}" : heldCount == 0 ? "Take" : "Add") + $"{Sep}{cost} Life";
    }
}
