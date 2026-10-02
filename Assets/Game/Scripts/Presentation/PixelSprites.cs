using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Placeholder pixel-art sprites generated from tiny character maps, so the 2.5D look
    /// (camera-facing pixel characters in a 3D arena) exists from Phase 1 without any art
    /// assets. The artist's sprites replace these through CharacterView; nothing in gameplay
    /// reads sprite size.
    /// </summary>
    public static class PixelSprites
    {
        public enum Kind { Magician, Acolyte, Pursuer, ScatterCaster, SiegeFamiliar, Collector }

        // '.' transparent; other characters index the palette given per sprite.
        static readonly string[] Magician =
        {
            ".....HH.....",
            "....HHHH....",
            "....HHGH....",
            "...HHHHHH...",
            "...HHHHHH...",
            "HHHHHHHHHHHH",
            "...SSSSSS...",
            "...SEESES...",
            "...SSSSSS...",
            "..RRRRRRRR..",
            ".RRRRGGRRRR.",
            "SRRRRGGRRRRS",
            ".RRRRGGRRRR.",
            ".RRRRGGRRRR.",
            ".RRRRRRRRRR.",
            "..RRRRRRRR..",
            "...BB..BB...",
            "...BB..BB...",
        };

        static readonly string[] Hooded =
        {
            "....HHHH....",
            "...HHHHHH...",
            "..HHHHHHHH..",
            "..HHDDDDHH..",
            "..HDEDDEDH..",
            "..HDDDDDDH..",
            "..HHDDDDHH..",
            "..RRRRRRRR..",
            ".RRRRGRRRRR.",
            "RRRRRGRRRRRR",
            "SRRRRGRRRRRS",
            ".RRRRGRRRRR.",
            ".RRRRRRRRRR.",
            ".RRRRRRRRRR.",
            "RRRRRRRRRRRR",
            "RRRRRRRRRRRR",
        };

        static readonly string[] Beast =
        {
            "..H......H..",
            "..HH....HH..",
            "..RRRRRRRR..",
            ".RREERREERR.",
            ".RRRRRRRRRR.",
            "RRRRGGGGRRRR",
            "RRRRRRRRRRRR",
            "RRRRRRRRRRRR",
            ".RR.RRRR.RR.",
            ".BB.B..B.BB.",
        };

        static readonly string[] Golem =
        {
            "...HHHHHH...",
            "..HHHHHHHH..",
            "..HHEHHEHH..",
            "..HHHHHHHH..",
            "RRRRRRRRRRRR",
            "RRGGRRRRGGRR",
            "RRGGRRRRGGRR",
            "RRRRRRRRRRRR",
            "RRRRRRRRRRRR",
            ".RRRR..RRRR.",
            ".BBBB..BBBB.",
        };

        static Dictionary<char, Color32> Palette(Kind kind)
        {
            Color32 skin = new Color32(240, 200, 160, 255), eye = new Color32(20, 16, 30, 255), boot = new Color32(40, 32, 40, 255);
            switch (kind)
            {
                case Kind.Magician:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(70, 50, 140, 255), ['G'] = new Color32(250, 210, 80, 255),
                        ['S'] = skin, ['E'] = eye, ['R'] = new Color32(110, 70, 190, 255), ['B'] = boot,
                    };
                case Kind.Acolyte:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(150, 30, 40, 255), ['D'] = new Color32(30, 10, 15, 255), ['E'] = new Color32(255, 220, 90, 255),
                        ['R'] = new Color32(190, 50, 55, 255), ['G'] = new Color32(240, 200, 120, 255), ['S'] = skin,
                    };
                case Kind.ScatterCaster:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(30, 90, 160, 255), ['D'] = new Color32(10, 20, 40, 255), ['E'] = new Color32(120, 255, 255, 255),
                        ['R'] = new Color32(50, 130, 210, 255), ['G'] = new Color32(160, 240, 255, 255), ['S'] = skin,
                    };
                case Kind.Collector:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(30, 30, 30, 255), ['D'] = new Color32(5, 5, 5, 255), ['E'] = new Color32(255, 80, 200, 255),
                        ['R'] = new Color32(80, 20, 90, 255), ['G'] = new Color32(250, 210, 80, 255), ['S'] = new Color32(200, 200, 210, 255),
                    };
                case Kind.Pursuer:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(230, 230, 220, 255), ['R'] = new Color32(120, 90, 60, 255), ['E'] = new Color32(255, 60, 40, 255),
                        ['G'] = new Color32(240, 240, 240, 255), ['B'] = boot,
                    };
                case Kind.SiegeFamiliar:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(130, 130, 120, 255), ['E'] = new Color32(255, 140, 30, 255), ['R'] = new Color32(100, 100, 95, 255),
                        ['G'] = new Color32(255, 140, 30, 255), ['B'] = new Color32(60, 60, 58, 255),
                    };
                default:
                    return new Dictionary<char, Color32>
                    {
                        ['H'] = new Color32(60, 50, 40, 255), ['G'] = new Color32(255, 230, 140, 255),
                        ['E'] = new Color32(255, 255, 220, 255), ['B'] = new Color32(50, 40, 35, 255),
                    };
            }
        }

        static string[] Map(Kind kind)
        {
            switch (kind)
            {
                case Kind.Magician: return Magician;
                case Kind.Pursuer: return Beast;
                case Kind.SiegeFamiliar: return Golem;
                default: return Hooded;
            }
        }

        static readonly Dictionary<Kind, Sprite> cache = new Dictionary<Kind, Sprite>();
        static readonly Dictionary<Kind, Sprite> evolvedCache = new Dictionary<Kind, Sprite>();
        static Sprite blob;

        /// <summary>Sprite with its pivot at the feet, so the transform sits on the ground.</summary>
        public static Sprite Get(Kind kind)
        {
            if (cache.TryGetValue(kind, out var s) && s != null) return s;
            var map = Map(kind);
            var pal = Palette(kind);
            int h = map.Length, w = map[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // crisp pixels, the whole point of the style
                wrapMode = TextureWrapMode.Clamp,
                name = "Sprite_" + kind,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = map[h - 1 - y]; // texture rows go bottom-up
                for (int x = 0; x < w; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    px[y * w + x] = pal.TryGetValue(c, out var col) ? col : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // 12 px across ≈ 1 unit: characters read at roughly 1.4–1.6 units tall.
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 12f);
            s.name = tex.name;
            cache[kind] = s;
            return s;
        }

        /// <summary>A wider horned silhouette, dark body and bright outline, per enemy palette.</summary>
        public static Sprite Overstayed(Kind kind)
        {
            if (evolvedCache.TryGetValue(kind, out var sprite) && sprite != null) return sprite;
            var map = Map(kind);
            var palette = Palette(kind);
            int w = map[0].Length + 6, h = map.Length + 4;
            var pixels = new Color32[w * h];
            for (int y = 0; y < map.Length; y++)
                for (int x = 0; x < map[y].Length; x++)
                {
                    if (!palette.TryGetValue(map[y][x], out var color)) continue;
                    if (map[y][x] != 'E') color = new Color32((byte)(color.r * 0.55f), (byte)(color.g * 0.45f), (byte)(color.b * 0.6f), 255);
                    pixels[(h - 5 - y) * w + x + 3] = color;
                }
            // Horns above the hood and spikes at both shoulders change the silhouette,
            // rather than asking the player to recognize a subtle tint in a crowded fight.
            var spike = new Color32(90, 20, 70, 255);
            for (int i = 0; i < 4; i++)
            {
                pixels[(h - 1 - i) * w + 2 + i] = spike;
                pixels[(h - 1 - i) * w + w - 3 - i] = spike;
                pixels[(h / 2 + i / 2) * w + i] = spike;
                pixels[(h / 2 + i / 2) * w + w - 1 - i] = spike;
            }
            var outlined = (Color32[])pixels.Clone();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                    if (pixels[y * w + x].a == 0 && (pixels[y * w + x - 1].a > 0 || pixels[y * w + x + 1].a > 0
                        || pixels[(y - 1) * w + x].a > 0 || pixels[(y + 1) * w + x].a > 0))
                        outlined[y * w + x] = new Color32(200, 60, 155, 255);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { name = "Overstayed_" + kind, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(outlined);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0), 12);
            evolvedCache[kind] = sprite;
            return sprite;
        }

        /// <summary>Soft round shadow used as the ground anchor under every character.</summary>
        public static Sprite Blob()
        {
            if (blob != null) return blob;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BlobShadow" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d)) * 255);
                    px[y * n + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            blob = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return blob;
        }

        // HUD life icon (D65). Kept as a char map like the characters so it reads as the
        // same pixel-art family; 'O' is a dark outline so it stays legible over any arena colour.
        static readonly string[] HeartMap =
        {
            ".OO...OO.",
            "ORRO.ORRO",
            "ORHRORRRO",
            "ORRRRRRRO",
            ".ORRRRRO.",
            "..ORRRO..",
            "...ORO...",
            "....O....",
        };

        static Sprite heart;

        /// <summary>Pixel heart for the life bar (cached, point filtered, centre pivot).</summary>
        public static Sprite Heart()
        {
            if (heart != null) return heart;
            var palette = new Dictionary<char, Color32>
            {
                ['O'] = new Color32(40, 8, 16, 255),
                ['R'] = new Color32(225, 45, 65, 255),
                ['H'] = new Color32(255, 190, 200, 255), // highlight pixel
            };
            int h = HeartMap.Length, w = HeartMap[0].Length;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Maps are written top row first; textures fill bottom row first.
                    char c = HeartMap[h - 1 - y][x];
                    px[y * w + x] = palette.TryGetValue(c, out var col) ? col : new Color32(0, 0, 0, 0);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { name = "Heart", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            heart = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            return heart;
        }

        static Sprite disc, ring, pixel;

        /// <summary>
        /// 1x1 white sprite pivoted at its left-middle edge: scale X = length, Y = width, so
        /// one transform draws a ground line (aim telegraphs) starting exactly at its origin.
        /// </summary>
        public static Sprite Pixel()
        {
            if (pixel != null) return pixel;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Pixel", filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return pixel;
        }

        /// <summary>Flat white disc/ring sprites for telegraphs, capture cones and markers (cached).</summary>
        public static Sprite Disc(bool ringShape)
        {
            if (ringShape && ring != null) return ring;
            if (!ringShape && disc != null) return disc;
            var made = MakeDisc(ringShape);
            if (ringShape) ring = made; else disc = made;
            return made;
        }

        static readonly Dictionary<int, Sprite> sectors = new Dictionary<int, Sprite>();

        /// <summary>
        /// Filled circular sector pointing along +X with its pivot at the apex and radius 1 unit,
        /// so scale == range and a yaw rotation aims it. A brighter rim marks the far edge, which
        /// is the line incoming shots must cross. Cached per whole-degree half-angle because the
        /// angle only changes with capture style / Precision upgrades, never per frame.
        /// </summary>
        public static Sprite Sector(float halfAngleDeg)
        {
            int key = Mathf.RoundToInt(halfAngleDeg);
            if (sectors.TryGetValue(key, out var s) && s != null) return s;
            const int n = 128; // texture spans x∈[-1,1], y∈[-1,1]; apex at the centre
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Sector" + key };
            var px = new Color32[n * n];
            float cosHalf = Mathf.Cos(key * Mathf.Deg2Rad);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inside = d <= 1f && d > 1e-4f && dx / d >= cosHalf;
                    // Alpha encodes fill vs rim; the SpriteRenderer colour supplies the hue.
                    byte a = !inside ? (byte)0 : d >= 0.93f ? (byte)255 : (byte)110;
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
            sectors[key] = s;
            return s;
        }

        static readonly Dictionary<int, Sprite> bands = new Dictionary<int, Sprite>();

        /// <summary>
        /// A thin arc band (D36 parry band): the part of a sector between radius
        /// <paramref name="innerFraction"/> and 1, pivot at the apex, pointing along +X.
        /// Scale == the band's OUTER radius. Cached per (half-angle, inner fraction in percent):
        /// both only change with tuning, never per frame.
        /// </summary>
        public static Sprite ArcBand(float halfAngleDeg, float innerFraction)
        {
            int ang = Mathf.RoundToInt(halfAngleDeg), inner = Mathf.RoundToInt(Mathf.Clamp01(innerFraction) * 100f);
            int key = ang * 1000 + inner;
            if (bands.TryGetValue(key, out var s) && s != null) return s;
            const int n = 192; // thin bands need more pixels than the filled sector to stay crisp
            float cosHalf = Mathf.Cos(ang * Mathf.Deg2Rad), lo = inner / 100f;
            s = Bake(n, "ArcBand" + key, (dx, dy, d) => d <= 1f && d >= lo && d > 1e-4f && dx / d >= cosHalf);
            bands[key] = s;
            return s;
        }

        static readonly Dictionary<int, Sprite> annuli = new Dictionary<int, Sprite>();

        /// <summary>
        /// A ring whose hole is <paramref name="innerFraction"/> of its radius (Disc(true) has a
        /// fixed 0.86 hole). Used for the strike circle's rim band, whose width is tuning.
        /// Scale == outer radius. Cached per inner fraction in percent.
        /// </summary>
        public static Sprite Annulus(float innerFraction)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp01(innerFraction) * 100f);
            if (annuli.TryGetValue(key, out var s) && s != null) return s;
            float lo = key / 100f;
            s = Bake(128, "Annulus" + key, (dx, dy, d) => d <= 1f && d >= lo);
            annuli[key] = s;
            return s;
        }

        /// <summary>White-on-transparent mask over x,y ∈ [-1,1], centred pivot, 2 units across.</summary>
        static Sprite Bake(int n, string name, System.Func<float, float, float, bool> on)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = on(dx, dy, d) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
        }

        static Sprite MakeDisc(bool ring)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = ring ? "Ring" : "Disc" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool on = ring ? d <= 1f && d >= 0.86f : d <= 1f;
                    px[y * n + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
        }
    }
}
