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
        public enum Kind { Magician, Acolyte, Pursuer, ScatterCaster, SiegeFamiliar, Collector, Lantern }

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

        static readonly string[] LanternMap =
        {
            "....HH....",
            "...H..H...",
            "..HHHHHH..",
            "..HGGGGH..",
            "..HGEEGH..",
            "..HGEEGH..",
            "..HGGGGH..",
            "..HHHHHH..",
            "....BB....",
            "....BB....",
            "....BB....",
            "...BBBB...",
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
                case Kind.Lantern: return LanternMap;
                default: return Hooded;
            }
        }

        static readonly Dictionary<Kind, Sprite> cache = new Dictionary<Kind, Sprite>();
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

        /// <summary>Flat white disc/ring sprites for telegraphs, capture cones and markers.</summary>
        public static Sprite Disc(bool ring)
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
