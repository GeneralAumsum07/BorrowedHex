using UnityEngine;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// Parry contact test (D36, owner direction). Two thin bands must touch:
    ///   - the player's PARRY BAND: an arc around the player at radius R (centre line), spanning
    ///     the catch cone's angle, thickened by half its width each side;
    ///   - the strike's RIM BAND: the outer edge of the enemy strike circle, centre line at
    ///     (strikeRadius - rimWidth/2), likewise thickened.
    /// Standing inside the strike circle is what hurts; touching its rim with the band is what
    /// parries. So the test is about the edges, never about the player's body.
    ///
    /// Two thickened curves overlap exactly when some point of one centre line lies within the
    /// sum of the half-widths of the other's centre line. For a point q on the arc, its
    /// distance to the rim's centre line is | |q - C| - rimR |. And |q - C| varies continuously
    /// along the arc, so the set of values it takes is exactly the interval [nearest, farthest]:
    /// the bands meet iff that interval overlaps [rimR - tol, rimR + tol]. Exact and closed-form,
    /// no sampling — the decision can never flicker with arc resolution.
    /// </summary>
    public static class ParryGeometry
    {
        public static bool BandsMeet(Vector2 player, Vector2 aim, float halfAngleDeg, float ringRadius, float ringWidth,
            Vector2 strikeCentre, float strikeRadius, float rimWidth)
        {
            if (aim.sqrMagnitude < 1e-8f) return false;
            float rimR = strikeRadius - rimWidth * 0.5f;
            float tol = (ringWidth + rimWidth) * 0.5f;

            Vector2 toC = strikeCentre - player;
            float d = toC.magnitude;
            // Angle between the aim and the direction to the strike centre, in [0, 180].
            float off = d < 1e-6f ? 0f : Vector2.Angle(aim, toC);
            float half = Mathf.Clamp(halfAngleDeg, 0f, 180f);

            // Law of cosines: distance from C to the arc point at angular separation a from toC.
            float At(float a) => Mathf.Sqrt(Mathf.Max(0f, ringRadius * ringRadius + d * d
                                                         - 2f * ringRadius * d * Mathf.Cos(a * Mathf.Deg2Rad)));
            // Nearest: the arc point angularly closest to toC (zero separation if toC is inside
            // the cone). Farthest: an end of the arc, or the far side if the arc reaches it.
            float nearest = At(Mathf.Max(0f, off - half));
            float farthest = At(Mathf.Min(180f, off + half));

            return nearest <= rimR + tol && farthest >= rimR - tol;
        }

        /// <summary>
        /// The parry band against an ARC band (the Collector's sweep, D47): the arc is centred on
        /// <paramref name="arcCentre"/> at <paramref name="arcRadius"/> (centre line), spans
        /// <paramref name="arcAim"/> ± <paramref name="arcHalfDeg"/>, and is
        /// <paramref name="arcWidth"/> thick.
        ///
        /// The closed form above relies on the rim being a whole circle; a partial arc adds an
        /// angular limit that breaks it. So this samples the parry band's centre line instead,
        /// at a spacing of at most a quarter of the combined tolerance: two samples can never
        /// straddle a contact zone, so the answer cannot flicker with sampling. Each sample
        /// must be within tolerance of the arc's radius AND inside its angular span.
        /// </summary>
        public static bool BandMeetsArc(Vector2 player, Vector2 aim, float halfAngleDeg, float ringRadius, float ringWidth,
            Vector2 arcCentre, Vector2 arcAim, float arcHalfDeg, float arcRadius, float arcWidth)
        {
            if (aim.sqrMagnitude < 1e-8f || arcAim.sqrMagnitude < 1e-8f || ringRadius <= 0f) return false;
            float tol = (ringWidth + arcWidth) * 0.5f;
            float stepDeg = Mathf.Max(0.25f, tol * 0.25f / ringRadius * Mathf.Rad2Deg);
            int steps = Mathf.CeilToInt(2f * halfAngleDeg / stepDeg);
            Vector2 a = aim.normalized;
            for (int i = 0; i <= steps; i++)
            {
                float ang = -halfAngleDeg + 2f * halfAngleDeg * i / Mathf.Max(1, steps);
                Vector2 q = player + Core.Geometry2D.Rotate(a, ang) * ringRadius;
                Vector2 rel = q - arcCentre;
                float d = rel.magnitude;
                if (Mathf.Abs(d - arcRadius) > tol) continue;
                if (Vector2.Angle(arcAim, rel) <= arcHalfDeg) return true;
            }
            return false;
        }
    }
}
