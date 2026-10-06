using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Runtime textures for UI geometry drawn on the pixel grid (plan 0.6): no anti-aliasing,
    /// point filtering, hard steps. Generated, not shipped, so they need no licence and no import.
    /// </summary>
    public static class PixelGeometry
    {
        public const int VignetteBands = 8;

        static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>Elliptical darkening, quantised to bands so it reads as pixel art (low-res, scaled up).</summary>
        public static Texture2D Vignette(int w, int h)
        {
            var t = New(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.9f);
                    // The outermost band is VignetteBands - 1, so the corners land on exactly 0.85.
                    float band = Mathf.Min(Mathf.Floor(d * VignetteBands), VignetteBands - 1);
                    float a = band / (VignetteBands - 1f) * 0.85f;
                    px[y * w + x] = new Color32(7, 5, 11, (byte)(Mathf.Clamp01(a) * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>
        /// A 1-art-pixel ring of radius <paramref name="r"/> art px, with gaps around the given axes
        /// (degrees, 0 = +x, counter-clockwise). The Accord's rings stop short of every branch axis
        /// so a ring can never read as a connection between branches (spec).
        /// </summary>
        public static Texture2D Ring(int r, float[] gapAxes, float gapHalfWidth, Color32 ink)
        {
            int size = r * 2 + 3; var t = New(size, size);
            var px = new Color32[size * size];
            // Midpoint circle: an exact pixel ring, no anti-aliasing.
            int x = r, y = 0, err = 1 - r, c = size / 2;
            while (x >= y)
            {
                foreach (var (dx, dy) in new[] { (x, y), (y, x), (-y, x), (-x, y), (-x, -y), (-y, -x), (y, -x), (x, -y) })
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                    bool inGap = false;
                    foreach (var g in gapAxes) if (Mathf.Abs(Mathf.DeltaAngle(ang, g)) < gapHalfWidth) inGap = true;
                    if (!inGap) px[(c + dy) * size + (c + dx)] = ink;
                }
                y++;
                if (err < 0) err += 2 * y + 1; else { x--; err += 2 * (y - x) + 1; }
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>
        /// A filled seal: <paramref name="rim"/> on the outer 2 art pixels, <paramref name="fill"/>
        /// inside (a clear fill makes an outline ring). Diameter in art px.
        /// </summary>
        public static Texture2D Disc(int d, Color32 rim, Color32 fill)
        {
            var t = New(d, d); var px = new Color32[d * d];
            float c = (d - 1) / 2f, r = d / 2f;
            for (int y = 0; y < d; y++) for (int x = 0; x < d; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (dist <= r - 0.5f) px[y * d + x] = dist > r - 2.5f ? rim : fill;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>
        /// A white square ring, <paramref name="thickness"/> art px thick, for a 9-slice border
        /// (slice = thickness). White so an Image tint gives an exact colour: the hex slots'
        /// overcharge border must be FeedbackColors.Overcharge, not a gilt sprite multiplied by it.
        /// </summary>
        public static Texture2D Frame(int size, int thickness)
        {
            var t = New(size, size); var px = new Color32[size * size];
            var ink = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                if (x < thickness || y < thickness || x >= size - thickness || y >= size - thickness) px[y * size + x] = ink;
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>
        /// A downward chevron (a "V"), 2 art px thick, white for tinting: the hex slots'
        /// selection marker, drawn above the selected slot and pointing at it.
        /// </summary>
        public static Texture2D Chevron(int w, int h)
        {
            var t = New(w, h); var px = new Color32[w * h];
            var ink = new Color32(255, 255, 255, 255);
            for (int r = 0; r < h; r++)   // r = 0 is the top row; texture row 0 is the bottom
            {
                int y = h - 1 - r;
                foreach (int x in new[] { r, r + 1, w - 1 - r, w - 2 - r })
                    if (x >= 0 && x < w) px[y * w + x] = ink;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>The tier-3 seal: a diamond, rim on its outer edge. Diameter (tip to tip) in art px.</summary>
        public static Texture2D Diamond(int d, Color32 rim, Color32 fill)
        {
            var t = New(d, d); var px = new Color32[d * d];
            float c = (d - 1) / 2f;
            for (int y = 0; y < d; y++) for (int x = 0; x < d; x++)
            {
                float m = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (m <= c) px[y * d + x] = m > c - 2f ? rim : fill;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }
    }
}
