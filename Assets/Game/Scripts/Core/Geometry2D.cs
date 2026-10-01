using UnityEngine;

namespace BorrowedHex.Core
{
    /// <summary>
    /// Analytic collision on the flat gameplay plane. A Vector2 here is world (x, z).
    ///
    /// Why not PhysX: every actor is a circle on one plane and every obstacle is a box, so an
    /// exact swept test is a few lines, deterministic, testable in EditMode, cheap on WebGL,
    /// and handles initial overlap explicitly (the SphereCast caveat noted in section 8).
    /// It is still one 3D-world collision model; nothing uses Physics2D.
    /// </summary>
    public static class Geometry2D
    {
        public static Vector2 ToPlane(Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 ToWorld(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        /// <summary>
        /// Earliest normalized time t in [0,1] at which a circle of radius r moving p0→p1
        /// touches a static circle (c, R). Initial overlap returns t = 0.
        /// </summary>
        public static bool SweepCircleVsCircle(Vector2 p0, Vector2 p1, float r, Vector2 c, float R, out float t)
        {
            float rr = r + R;
            Vector2 m = p0 - c;
            float cTerm = Vector2.Dot(m, m) - rr * rr;
            if (cTerm <= 0f) { t = 0f; return true; }
            Vector2 d = p1 - p0;
            float a = Vector2.Dot(d, d);
            t = 1f;
            if (a < 1e-12f) return false;
            float b = Vector2.Dot(m, d);
            if (b >= 0f) return false; // moving away and not overlapping
            float disc = b * b - a * cTerm;
            if (disc < 0f) return false;
            t = (-b - Mathf.Sqrt(disc)) / a;
            return t >= 0f && t <= 1f;
        }

        /// <summary>
        /// Swept circle against an axis-aligned box (Minkowski-expanded by r, square corners).
        /// The square-corner approximation over-reports hits by at most r·(√2−1) at corners,
        /// which errs toward walls blocking shots — the readable failure direction.
        /// </summary>
        public static bool SweepCircleVsRect(Vector2 p0, Vector2 p1, float r, Rect box, out float t)
        {
            float minX = box.xMin - r, maxX = box.xMax + r, minY = box.yMin - r, maxY = box.yMax + r;
            t = 0f;
            if (p0.x >= minX && p0.x <= maxX && p0.y >= minY && p0.y <= maxY) return true;
            Vector2 d = p1 - p0;
            float tMin = 0f, tMax = 1f;
            if (!Slab(p0.x, d.x, minX, maxX, ref tMin, ref tMax)) return false;
            if (!Slab(p0.y, d.y, minY, maxY, ref tMin, ref tMax)) return false;
            t = tMin;
            return true;
        }

        static bool Slab(float p, float d, float min, float max, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(d) < 1e-9f) return p >= min && p <= max;
            float inv = 1f / d;
            float t1 = (min - p) * inv, t2 = (max - p) * inv;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }

        public static bool CircleOverlapsRect(Vector2 c, float r, Rect box)
        {
            float cx = Mathf.Clamp(c.x, box.xMin, box.xMax);
            float cy = Mathf.Clamp(c.y, box.yMin, box.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            return dx * dx + dy * dy < r * r;
        }

        /// <summary>Point inside a cone of the given half-angle and range.</summary>
        public static bool InCone(Vector2 apex, Vector2 dir, float halfAngleDeg, float range, Vector2 p)
        {
            Vector2 to = p - apex;
            float dist = to.magnitude;
            if (dist > range) return false;
            if (dist < 1e-5f) return true;
            float cos = Vector2.Dot(to / dist, dir.normalized);
            return cos >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        }

        /// <summary>Closest point on segment ab to p, as normalized parameter in [0,1].</summary>
        public static float ClosestT(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = Vector2.Dot(ab, ab);
            if (len2 < 1e-12f) return 0f;
            return Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }
    }
}
