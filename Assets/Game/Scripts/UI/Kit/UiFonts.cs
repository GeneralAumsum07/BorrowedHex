using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The two pixel faces: alagard for display, m6x11plus for body (plan Q2, the owner's pick;
    /// m5x7 stays the world-callout face and loads through <see cref="LoadPixel"/> too). Both are
    /// licensed third-party files in git-ignored folders, so each falls back to the built-in font
    /// and the game stays readable in a public checkout. Sizes come from roles, never literals,
    /// so the whole UI stays on each font's pixel grid.
    /// </summary>
    public static class UiFonts
    {
        public enum Role { Title, Heading, Sub, Button, Body, Number, Small }

        // Design size of each face: one font pixel per screen pixel at this size. Read from the
        // TTFs (units-per-em / outline grid: alagard 1024/64 = 16, m6x11plus 1152/64 = 18) and
        // pinned against Unity's rasteriser by UiFontsTests.NativeSizeScalesEveryAdvanceExactly.
        public const int NativeDisplay = 16, NativeBody = 18;
        public const string DisplayFile = "UiArt/alagard", BodyFile = "UiArt/m6x11plus";

        // One entry per resource path, so a font loaded by two callers (UI and callouts) gets
        // exactly one atlas-rebuild hook instead of one per caller.
        static readonly Dictionary<string, Font> loaded = new Dictionary<string, Font>();

        public static bool HasPixelFonts => Display != Ui.Font && Body != Ui.Font;
        public static Font Display => LoadPixel(DisplayFile);
        public static Font Body => LoadPixel(BodyFile);

        public static bool UsesDisplay(Role r) => r == Role.Title || r == Role.Heading || r == Role.Sub || r == Role.Button;

        /// <summary>Reference-pixel size per role: whole multiples of the face's native size.</summary>
        public static int Size(Role r)
        {
            switch (r)
            {
                case Role.Title: return NativeDisplay * 6;    // the logo
                case Role.Heading: return NativeDisplay * 3;  // screen titles
                case Role.Sub: return NativeDisplay * 2;      // card names, section heads
                case Role.Button: return NativeDisplay * 2;
                case Role.Number: return NativeBody * 3;      // score, big stats
                default: return NativeBody * 2;               // Body and Small: hierarchy by colour, not by a blurry size
            }
        }

        public static Font For(Role r) => UsesDisplay(r) ? Display : Body;

        /// <summary>
        /// A pixel font from Resources, point-filtered and kept that way, or the built-in font when
        /// the file is not imported. Never null.
        /// </summary>
        public static Font LoadPixel(string path)
        {
            // A destroyed Font compares equal to null, so a stale cache entry reloads.
            if (loaded.TryGetValue(path, out var hit) && hit != null) return hit;
            var f = Resources.Load<Font>(path);
            if (f == null) return loaded[path] = Ui.Font;
            PointFilter(f);
            // A dynamic font rebuilds its atlas when new glyphs appear, and the new texture
            // comes back bilinear. Re-point it every time, or text blurs mid-session.
            Font.textureRebuilt += rebuilt => { if (rebuilt == f) PointFilter(f); };
            return loaded[path] = f;
        }

        public static void PointFilter(Font f)
        {
            if (f != null && f.material != null && f.material.mainTexture != null)
                f.material.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
