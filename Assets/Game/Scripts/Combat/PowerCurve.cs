using UnityEngine;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// D93: how much a hex hits for, by how long it has decayed. Replaces the old linear
    /// 1 + 0.35 * t (1.00 -> 2.05), which had no sweet spot: holding 2.4 s or 2.9 s felt the same.
    ///
    /// Two parts:
    ///  - an eased ramp from 1 to PeakPower that ends where the Overcharge zone starts. Convex
    ///    (Exponent > 1), so most of the gain comes late: an early fire is honest but weak.
    ///  - the Overcharge zone, the last OverchargeWindow seconds before expiry: power x
    ///    OverchargeMultiplier, a "perfect release". One tick too late is still a backfire,
    ///    because expiry is resolved before input (D17).
    /// A pure value type: the sim, the HUD and the tests all evaluate the same numbers.
    /// </summary>
    public readonly struct PowerCurve
    {
        public readonly float PeakPower;
        public readonly float Exponent;
        public readonly float OverchargeWindow;
        public readonly float OverchargeMultiplier;

        public PowerCurve(float peakPower, float exponent, float overchargeWindow, float overchargeMultiplier)
        {
            PeakPower = peakPower;
            Exponent = exponent;
            OverchargeWindow = overchargeWindow;
            OverchargeMultiplier = overchargeMultiplier;
        }

        // The zone start is lifetime - window computed in float (3f - 0.35f = 2.6500001), so the
        // tolerance must cover float error, not just double error: 1e-5 s is far below one tick.
        const double ZoneEpsilon = 1e-5;

        public bool IsOvercharged(double decayed, float lifetime) =>
            OverchargeWindow > 0f && decayed >= lifetime - OverchargeWindow - ZoneEpsilon && decayed < lifetime;

        public float Evaluate(double decayed, float lifetime)
        {
            // The ramp reaches its peak exactly where the zone begins, so the zone is a clean
            // step up, readable as "now!", not a slope the player has to estimate.
            float rampEnd = Mathf.Max(1e-4f, lifetime - OverchargeWindow);
            float u = Mathf.Clamp01((float)(decayed / rampEnd));
            float p = 1f + (PeakPower - 1f) * Mathf.Pow(u, Exponent);
            return IsOvercharged(decayed, lifetime) ? p * OverchargeMultiplier : p;
        }
    }
}
