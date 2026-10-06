using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Named UI sprites cut at runtime from the git-ignored Resources/UiArt sheets. The table is
    /// the single committed record of every rect and 9-slice border (plan Task 2). When the art is
    /// missing every lookup returns null and the kit draws flat colour instead.
    /// </summary>
    public static class UiSkin
    {
        /// <summary>Tests force the public-checkout path regardless of what is imported locally.</summary>
        public static bool ForceFlat;
        public const float PixelsPerUnit = 50f;   // canvas reference PPU is 100: one art pixel = 2 reference px

        struct Spec { public string File; public RectInt Rect; public Vector4 Border; public int Frames; }

        static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>();
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

        static void Add(string id, string file, int x, int y, int w, int h, int l = 0, int b = 0, int r = 0, int t = 0, int frames = 1)
            => specs[id] = new Spec { File = file, Rect = new RectInt(x, y, w, h), Border = new Vector4(l, b, r, t), Frames = frames };

        static UiSkin()
        {
            const string D = "DarkAges";
            Add("frame.ornate", D, 0, 256, 96, 96, 32, 32, 32, 32);
            Add("frame.parchment", D, 96, 256, 96, 96, 16, 16, 16, 16);
            Add("frame.card", D, 210, 274, 60, 60, 12, 12, 12, 12);
            Add("frame.cardAlt", D, 306, 274, 60, 60, 12, 12, 12, 12);
            Add("plate.dark", D, 0, 225, 64, 23, 12, 8, 12, 8);
            Add("plate.darkAlt", D, 64, 225, 64, 23, 12, 8, 12, 8);
            Add("plate.crest", D, 128, 225, 64, 31, 16, 8, 16, 14);
            Add("square.dark", D, 194, 224, 28, 27, 8, 8, 8, 8);
            Add("square.crest", D, 256, 224, 32, 32, 8, 8, 8, 12);
            Add("scroll.track", D, 12, 135, 7, 84, 0, 8, 0, 8);
            Add("bar.tray", D, 197, 202, 86, 13, 8, 0, 8, 0);
            Add("bar.trayDark", D, 102, 204, 84, 7, 6, 0, 6, 0);
            // The crest from bar.tray's top edge, cut on its own (plus the base line under it):
            // inside the tray it sat in the 9-slice middle and smeared with any width. The HUD
            // lays it over the centre of a plain bar.trayDark instead (plan Task 13).
            Add("bar.crest", D, 232, 209, 17, 6);
            Add("fill.blue", D, 296, 204, 82, 4, 1, 0, 1, 0);
            Add("fill.red", D, 199, 172, 82, 4, 1, 0, 1, 0);
            Add("fill.green", D, 295, 172, 82, 4, 1, 0, 1, 0);
            int[,] gems = { { 10, 74, 13, 12 }, { 41, 73, 12, 13 }, { 73, 73, 12, 13 }, { 105, 74, 13, 12 },
                            { 10, 42, 13, 12 }, { 41, 41, 12, 13 }, { 73, 41, 12, 13 }, { 105, 42, 13, 12 } };
            for (int i = 0; i < 8; i++) Add("gem." + i, D, gems[i, 0], gems[i, 1], gems[i, 2], gems[i, 3]);
            Add("mark.x", D, 76, 108, 7, 8);
            Add("divider.a", D, 297, 76, 37, 6, 12, 0, 12, 0);
            Add("divider.b", D, 296, 44, 37, 6, 12, 0, 12, 0);
            Add("filigree.a", D, 192, 96, 64, 64, 16, 16, 16, 16);
            Add("filigree.b", D, 256, 96, 64, 64, 16, 16, 16, 16);
            Add("strip.parchment", D, 0, 0, 64, 32, 8, 8, 8, 8);
            Add("tab", "DwTab", 0, 0, 96, 32, 12, 8, 30, 8, frames: 4);   // right border holds the whole scroll ornament, so it never stretches
            Add("pointer", "DwPointer", 0, 0, 32, 19, frames: 5);
            Add("btn.close", "DwClose", 0, 0, 32, 32, frames: 4);
            Add("btn.options", "DwOptions", 0, 0, 32, 32, frames: 4);
            Add("arrow.left", "DwLeft", 0, 0, 19, 18, frames: 4);
            Add("arrow.right", "DwRight", 0, 0, 19, 18, frames: 4);
            Add("arrow.up", "DwUp", 0, 0, 19, 18, frames: 4);
            Add("arrow.down", "DwDown", 0, 0, 19, 18, frames: 4);
            Add("portrait", "DwPortrait", 0, 0, 66, 72);
        }

        public static IReadOnlyCollection<string> Ids => specs.Keys;

        public static bool HasArt => !ForceFlat && Resources.Load<Texture2D>("UiArt/DarkAges") != null;

        /// <summary>The sprite (frame 0 of an animated piece), or null without the art.</summary>
        public static Sprite Sprite(string id)
        {
            if (!HasArt || !specs.TryGetValue(id, out var s)) return null;
            if (cache.TryGetValue(id, out var hit) && hit != null) return hit;
            return cache[id] = Cut(s, 0);
        }

        /// <summary>All frames of an animated piece (pointer, tab states, buttons); empty without the art.</summary>
        public static Sprite[] Frames(string id)
        {
            if (!HasArt || !specs.TryGetValue(id, out var s)) return new Sprite[0];
            if (frameCache.TryGetValue(id, out var hit) && hit.Length > 0 && hit[0] != null) return hit;
            var f = new Sprite[s.Frames];
            for (int i = 0; i < f.Length; i++) f[i] = Cut(s, i);
            return frameCache[id] = f;
        }

        static Sprite Cut(Spec s, int frame)
        {
            var tex = Resources.Load<Texture2D>("UiArt/" + s.File);
            if (tex == null) return null;
            var r = new Rect(s.Rect.x + frame * s.Rect.width, s.Rect.y, s.Rect.width, s.Rect.height);
            // FullRect mesh: Tight meshes break 9-slicing. The border makes Image.Type.Sliced work.
            var sp = UnityEngine.Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, s.Border);
            sp.name = s.File + "#" + frame;
            return sp;
        }
    }
}
