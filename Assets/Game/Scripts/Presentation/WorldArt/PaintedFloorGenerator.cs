using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Paints one arena floor as a single image (no tiling, so no visible repeat), plus small
    /// seamless textures for the outer ground and the enclosure ribbon. Ported from the swatch
    /// generator the owner approved on 2026-10-04; the Courtyard and Graveyard numbers are
    /// those approved values verbatim. Pure: no Unity objects, deterministic per theme.
    /// </summary>
    public static class PaintedFloorGenerator
    {
        public const int PixelsPerUnit = 40;
        // Decal counts were approved on a 768x480 swatch; scale them by area so density holds.
        const float SwatchArea = 768f * 480f;

        // ---------- noise ----------
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        // Smoothstep value noise: soft low-frequency drift reads as "painted".
        static float Noise(float x, float y, int seed, int periodX = 0, int periodY = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x1 = xi + 1, y1 = yi + 1;
            // Wrapping the integer lattice makes the noise periodic, which is what lets the
            // outer-ground and ribbon textures repeat without a seam.
            if (periodX > 0) { xi = Mod(xi, periodX); x1 = Mod(x1, periodX); }
            if (periodY > 0) { yi = Mod(yi, periodY); y1 = Mod(y1, periodY); }
            float a = Hash(xi, yi, seed), b = Hash(x1, yi, seed), c = Hash(xi, y1, seed), d = Hash(x1, y1, seed);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        static int Mod(int value, int period) { int m = value % period; return m < 0 ? m + period : m; }

        static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0, amplitude = .5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            { sum += Noise(x, y, seed + o * 31) * amplitude; norm += amplitude; x *= 2.03f; y *= 2.03f; amplitude *= .5f; }
            return sum / norm;
        }

        /// Periodic fbm. Lacunarity is exactly 2 (not 2.03) and the period doubles per
        /// octave, so every octave wraps on the same tile boundary.
        public static float FbmTiled(float u, float v, int seed, int octaves, int periodX, int periodY)
        {
            float sum = 0, amplitude = .5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, seed + o * 31, periodX << o, periodY << o) * amplitude;
                norm += amplitude; u *= 2; v *= 2; amplitude *= .5f;
            }
            return sum / norm;
        }

        static float Sat(float v) => Mathf.Clamp01(v);

        static void Set(PaintedImage d, int i, Color c) { d.R[i] = c.r; d.G[i] = c.g; d.B[i] = c.b; }

        // ---------- bases ----------
        // Two-colour fbm field with a third "patch" colour in the troughs of a second field.
        static void PaintBase(PaintedImage d, int seed, float frequency, Color low, Color high, Color patchColor, float patch)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float n = Fbm(x * frequency, y * frequency, seed, 5);
                float m = Fbm(x * frequency * .6f + 50, y * frequency * .6f + 50, seed + 7, 4);
                Set(d, y * d.Width + x, Color.Lerp(Color.Lerp(low, high, Sat((n - .3f) / .4f)), patchColor, Sat((patch - m) / .08f)));
            }
        }

        // Seamless variant for the outer ground and ribbon. cellsX/cellsY are lattice cells
        // across the image and must be even (the patch field runs at half frequency).
        static void PaintBaseTiled(PaintedImage d, int seed, int cellsX, int cellsY, Color low, Color high, Color patchColor, float patch)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float u = x * (float)cellsX / d.Width, v = y * (float)cellsY / d.Height;
                float n = FbmTiled(u, v, seed, 5, cellsX, cellsY);
                float m = FbmTiled(u * .5f + 50, v * .5f + 50, seed + 7, 4, cellsX / 2, cellsY / 2);
                Set(d, y * d.Width + x, Color.Lerp(Color.Lerp(low, high, Sat((n - .3f) / .4f)), patchColor, Sat((patch - m) / .08f)));
            }
        }

        // Domain-warped jittered-grid Voronoi flagstones with mossy cracks (Courtyard).
        static void Flagstones(PaintedImage d, int seed, float cell, float gap, Color stone, Color crack)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float gx = x / cell + (Fbm(x * .02f, y * .02f, seed + 40, 3) - .5f) * .45f;
                float gy = y / cell + (Fbm(x * .02f + 9, y * .02f + 9, seed + 41, 3) - .5f) * .45f;
                int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                float d1 = 9, d2 = 9; int best = 0;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int cx = ix + ox, cy = iy + oy;
                    float px = cx + .15f + .7f * Hash(cx, cy, seed), py = cy + .15f + .7f * Hash(cx, cy, seed + 1);
                    float dd = Mathf.Sqrt((gx - px) * (gx - px) + (gy - py) * (gy - py));
                    if (dd < d1) { d2 = d1; d1 = dd; best = cx * 7919 + cy; } else if (dd < d2) d2 = dd;
                }
                float tone = .65f + .5f * Hash(best, 3, seed);
                float wear = .85f + .3f * Fbm(x * .03f, y * .03f, seed + 11, 4);
                float edge = Sat((d2 - d1 - gap) / .05f);              // 0 in the crack, 1 on the stone
                float bevel = Sat((d2 - d1) / .25f) * .15f + .85f;     // stones darken toward their rims
                Set(d, y * d.Width + x, Color.Lerp(crack, stone * (tone * wear * bevel), edge));
            }
        }

        // Regular basalt tiles with a faint inlaid line every `inlayEvery` tiles (Sanctum).
        static void Tiles(PaintedImage d, int seed, int cell, float gap, Color stone, Color crack, int inlayEvery, Color inlay)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                int tx = x / cell, ty = y / cell;
                float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
                float edgeDistance = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
                float tone = .75f + .4f * Hash(tx, ty, seed);
                float wear = .85f + .3f * Fbm(x * .025f, y * .025f, seed + 5, 4);
                var color = Color.Lerp(crack, stone * (tone * wear), Sat((edgeDistance - gap) / .03f));
                // The ritual grid: a thin pale line along every Nth tile seam, fading with wear.
                bool line = tx % inlayEvery == 0 && x % cell < 2 || ty % inlayEvery == 0 && y % cell < 2;
                if (line) color = Color.Lerp(color, inlay, .55f * wear);
                Set(d, y * d.Width + x, color);
            }
        }

        // Worn footpaths: two sinuous bands of bare earth crossing the field (Graveyard).
        static void Footpath(PaintedImage d, int seed, float width, Color earth, float strength)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float u = x / (float)d.Width, v = y / (float)d.Height;
                float along = Mathf.Abs(v - (.5f + .16f * Mathf.Sin(u * 6.283f * 1.3f + seed))) * d.Height;
                float across = Mathf.Abs(u - (.42f + .1f * Mathf.Sin(v * 6.283f * .9f + seed * 2))) * d.Width;
                float ragged = (Fbm(x * .05f, y * .05f, seed, 3) - .5f) * width * .8f;
                float path = Mathf.Max(Sat(1 - (along + ragged) / width), Sat(1 - (across + ragged) / (width * .7f)));
                int i = y * d.Width + x;
                Set(d, i, Color.Lerp(new Color(d.R[i], d.G[i], d.B[i]), earth, path * strength));
            }
        }

        // Pale mineral flecks (Cave): a few-pixel soft dots.
        static void Flecks(PaintedImage d, int seed, int count, Color fleck)
        {
            var random = new System.Random(seed);
            for (int n = 0; n < count; n++)
            {
                int cx = random.Next(d.Width), cy = random.Next(d.Height); float radius = 1 + (float)random.NextDouble() * 1.5f;
                for (int y = cy - 3; y <= cy + 3; y++) for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if (x < 0 || y < 0 || x >= d.Width || y >= d.Height) continue;
                    float k = Sat(1 - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius) * .6f;
                    int i = y * d.Width + x; Set(d, i, Color.Lerp(new Color(d.R[i], d.G[i], d.B[i]), fleck, k));
                }
            }
        }

        // Seamless brick courses for the ribbon (Courtyard, Sanctum). Width must be a multiple
        // of brickWidth and height of 2*brickHeight so the running bond wraps.
        static void Masonry(PaintedImage d, int seed, int brickWidth, int brickHeight, Color stone, Color mortar)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                int row = y / brickHeight; int shifted = x + (row % 2) * brickWidth / 2;
                int column = (shifted / brickWidth) % (d.Width / brickWidth);
                float fx = (shifted % brickWidth) / (float)brickWidth, fy = (y % brickHeight) / (float)brickHeight;
                float edge = Mathf.Min(Mathf.Min(fx, 1 - fx) * brickWidth, Mathf.Min(fy, 1 - fy) * brickHeight);
                float tone = .7f + .45f * Hash(column, row, seed);
                int i = y * d.Width + x;
                var under = new Color(d.R[i], d.G[i], d.B[i]);
                Set(d, i, Color.Lerp(mortar, under * tone + stone * .35f, Sat((edge - 1.5f) / 1.5f)));
            }
        }

        // ---------- decals ----------
        /// Bilinear, premultiplied stamping (no black fringes); daytime pack colours are
        /// desaturated then multiplied into the night palette.
        public static void Scatter(PaintedImage d, DecalSheet sheet, int count, int seed, float scale, Color tint,
            float desaturate, float alpha, int maxSprite)
        {
            if (sheet == null || sheet.Sprites.Count == 0) return;
            var pool = sheet.Sprites.FindAll(r => r.width <= maxSprite && r.height <= maxSprite);
            if (pool.Count == 0) pool = sheet.Sprites;
            var random = new System.Random(seed);
            for (int n = 0; n < count; n++)
            {
                var rect = pool[random.Next(pool.Count)];
                int x = random.Next(-(int)(rect.width * scale) / 2, d.Width), y = random.Next(-(int)(rect.height * scale) / 2, d.Height);
                Stamp(d, sheet.Image, rect, x, y, scale, tint, desaturate, alpha * (.7f + .3f * (float)random.NextDouble()));
            }
        }

        static void Stamp(PaintedImage d, PaintedImage s, RectInt r, int dx, int dy, float scale, Color tint, float desaturate, float alpha)
        {
            int ow = (int)(r.width * scale), oh = (int)(r.height * scale);
            for (int y = 0; y < oh; y++)
            {
                int ty = dy + y; if (ty < 0 || ty >= d.Height) continue;
                for (int x = 0; x < ow; x++)
                {
                    int tx = dx + x; if (tx < 0 || tx >= d.Width) continue;
                    Bilinear(s, r.x + x / scale - .5f, r.y + y / scale - .5f, out float cr, out float cg, out float cb, out float ca);
                    if (ca <= .01f) continue;
                    float lum = cr * .3f + cg * .59f + cb * .11f;
                    cr = Mathf.Lerp(cr, lum, desaturate) * tint.r; cg = Mathf.Lerp(cg, lum, desaturate) * tint.g; cb = Mathf.Lerp(cb, lum, desaturate) * tint.b;
                    float a = ca * alpha; int o = ty * d.Width + tx;
                    d.R[o] = Mathf.Lerp(d.R[o], cr, a); d.G[o] = Mathf.Lerp(d.G[o], cg, a); d.B[o] = Mathf.Lerp(d.B[o], cb, a);
                }
            }
        }

        static void Bilinear(PaintedImage s, float x, float y, out float r, out float g, out float b, out float a)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, s.Width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, s.Height - 1);
            int x1 = Mathf.Min(s.Width - 1, x0 + 1), y1 = Mathf.Min(s.Height - 1, y0 + 1);
            float fx = Sat(x - x0), fy = Sat(y - y0);
            int i00 = y0 * s.Width + x0, i10 = y0 * s.Width + x1, i01 = y1 * s.Width + x0, i11 = y1 * s.Width + x1;
            float a00 = s.A[i00], a10 = s.A[i10], a01 = s.A[i01], a11 = s.A[i11];
            a = Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
            if (a < 1e-4f) { r = g = b = 0; return; }
            r = Mathf.Lerp(Mathf.Lerp(s.R[i00] * a00, s.R[i10] * a10, fx), Mathf.Lerp(s.R[i01] * a01, s.R[i11] * a11, fx), fy) / a;
            g = Mathf.Lerp(Mathf.Lerp(s.G[i00] * a00, s.G[i10] * a10, fx), Mathf.Lerp(s.G[i01] * a01, s.G[i11] * a11, fx), fy) / a;
            b = Mathf.Lerp(Mathf.Lerp(s.B[i00] * a00, s.B[i10] * a10, fx), Mathf.Lerp(s.B[i01] * a01, s.B[i11] * a11, fx), fy) / a;
        }

        // ---------- per-theme recipes ----------
        static DecalSheet Sheet(IReadOnlyDictionary<string, DecalSheet> decals, string key)
            => decals != null && decals.TryGetValue(key, out var sheet) ? sheet : null;

        public static PaintedImage Floor(string theme, int width, int height, IReadOnlyDictionary<string, DecalSheet> decals)
        {
            var d = new PaintedImage(width, height);
            float area = width * height / SwatchArea;
            int N(int swatchCount) => Mathf.Max(1, Mathf.RoundToInt(swatchCount * area));
            DecalSheet grass = Sheet(decals, "grass"), rocks = Sheet(decals, "rocks"), vegetation = Sheet(decals, "vegetation");
            switch (theme)
            {
                case "Graveyard": // approved swatch e2_painted
                    PaintBase(d, 21, .007f, new Color(.08f, .11f, .07f), new Color(.17f, .21f, .12f), new Color(.15f, .11f, .08f), .44f);
                    Footpath(d, 25, 46, new Color(.13f, .10f, .08f), .7f);
                    Scatter(d, grass, N(30), 22, .5f, new Color(.48f, .62f, .40f), .35f, .9f, 400);
                    Scatter(d, rocks, N(22), 23, .5f, new Color(.55f, .55f, .6f), .6f, .9f, 160);
                    Scatter(d, vegetation, N(16), 24, .5f, new Color(.9f, .88f, .95f), .9f, .9f, 60);
                    break;
                case "Cave": // first pass: violet-grey rock, dark damp pools, mineral flecks
                    PaintBase(d, 31, .006f, new Color(.10f, .09f, .13f), new Color(.20f, .18f, .24f), new Color(.03f, .03f, .05f), .40f);
                    Flecks(d, 33, N(900), new Color(.45f, .5f, .75f));
                    Scatter(d, rocks, N(18), 34, .5f, new Color(.5f, .48f, .62f), .6f, .9f, 160);
                    break;
                case "Sanctum": // first pass: worn basalt tiles with faint inlaid lines
                    Tiles(d, 41, 80, .04f, new Color(.14f, .13f, .16f), new Color(.03f, .025f, .04f), 4, new Color(.30f, .24f, .42f));
                    Scatter(d, rocks, N(10), 43, .5f, new Color(.5f, .5f, .58f), .6f, .8f, 160);
                    break;
                default: // Courtyard, approved swatch e1_painted; also the fallback theme
                    Flagstones(d, 3, 64, .035f, new Color(.21f, .21f, .25f), new Color(.04f, .05f, .04f));
                    Scatter(d, grass, N(14), 5, .5f, new Color(.45f, .58f, .40f), .45f, .85f, 300);
                    Scatter(d, rocks, N(28), 6, .5f, new Color(.55f, .55f, .62f), .55f, .9f, 160);
                    Scatter(d, vegetation, N(10), 7, .5f, new Color(.9f, .88f, .95f), .9f, .9f, 60);
                    break;
            }
            return d;
        }

        // Base colours shared by the outer ground (darker) and the ribbon.
        static (Color low, Color high, Color patch) Palette(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return (new Color(.06f, .08f, .05f), new Color(.12f, .15f, .09f), new Color(.10f, .08f, .06f));
                case "Cave": return (new Color(.08f, .07f, .10f), new Color(.16f, .14f, .19f), new Color(.03f, .03f, .05f));
                case "Sanctum": return (new Color(.07f, .06f, .09f), new Color(.14f, .12f, .16f), new Color(.05f, .04f, .07f));
                default: return (new Color(.08f, .08f, .09f), new Color(.15f, .15f, .17f), new Color(.06f, .07f, .05f));
            }
        }

        public static PaintedImage Outer(string theme, int size)
        {
            var d = new PaintedImage(size, size); var p = Palette(theme);
            // 55% of the ring palette: the ground past the walls should sink into the fog.
            PaintBaseTiled(d, 61, 8, 8, p.low * .55f, p.high * .55f, p.patch * .55f, .42f);
            return d;
        }

        public static PaintedImage Ribbon(string theme, int width, int height)
        {
            var d = new PaintedImage(width, height); var p = Palette(theme);
            PaintBaseTiled(d, 71, 8, 4, p.low, p.high, p.patch, .40f);
            // Built themes (and the Courtyard fallback) get coursed masonry; Graveyard reads
            // as hedge/earth and Cave as raw rock, so they keep the plain painted base.
            if (theme != "Graveyard" && theme != "Cave")
                Masonry(d, 73, width / 8, height / 8, p.high, p.low * .4f);
            return d;
        }
    }
}
