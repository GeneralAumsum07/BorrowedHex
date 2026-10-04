using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The source packs remain local Resources content. Runtime slicing avoids depending on
    /// hand-authored atlas GUIDs and lets a public checkout keep its placeholder art fallback.
    /// Created sprites are owned by one presentation, rather than leaking on every restart.
    /// </summary>
    public sealed class WorldArtLibrary : IDisposable
    {
        readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
        readonly Dictionary<string, Color32[]> pixels = new Dictionary<string, Color32[]>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly string folder;
        public WorldArtLibrary(string resourceFolder = "WorldArt") => folder = resourceFolder;
        public Texture2D Texture(string name) => Resources.Load<Texture2D>(folder + "/" + name);
        public bool HasBoss => Texture("Necromancer") != null;
        public Color32[] Pixels(string name)
        {
            if (pixels.TryGetValue(name, out var cached)) return cached;
            var texture = Texture(name);
            return pixels[name] = texture != null ? texture.GetPixels32() : Array.Empty<Color32>();
        }

        public Sprite[] Boss(string clip)
        {
            string[] names = { "Idle", "Run", "Attack1", "Attack2", "Attack3", "Hurt", "Death" };
            int[] lengths = { 8, 8, 13, 13, 17, 5, 10 };
            int row = Array.IndexOf(names, clip);
            if (row < 0) return Array.Empty<Sprite>();
            if (sheets.TryGetValue("Boss/" + clip, out var cached)) return cached;
            var texture = Texture("Necromancer");
            var frames = new List<Sprite>();
            if (texture != null && texture.width == 2720 && texture.height == 896)
            {
                var pixels = texture.GetPixels32();
                int rowY = texture.height - (row + 1) * 128, bottom = 127;
                // Anchor a whole clip at its lowest opaque row. A fixed .22 pivot put
                // visible boots below y=0; a per-frame pivot would introduce pose jitter.
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < lengths[row] * 160; x++)
                        if (pixels[(rowY + y) * texture.width + x].a > 8) bottom = Math.Min(bottom, y);
                for (int x = 0; x < lengths[row]; x++)
                    frames.Add(Make(texture, new Rect(x * 160, texture.height - (row + 1) * 128, 160, 128),
                        new Vector2(0.5f, bottom / 128f), 32));
            }
            return sheets["Boss/" + clip] = frames.ToArray();
        }

        public Sprite[] Effect(string name, int cell = 32)
        {
            // The cell size is part of the key (final review minor): one sheet cut at two cell
            // sizes is two different frame lists, and caching by name alone returned whichever
            // was asked for first. The 32 default keeps the bare name, as before.
            string key = cell == 32 ? name : name + "@" + cell;
            if (sheets.TryGetValue(key, out var cached)) return cached;
            var tex = Texture(name);
            var frames = new List<Sprite>();
            if (tex != null && tex.width % cell == 0 && tex.height % cell == 0)
            {
                // Source sheets are read left-to-right, top-to-bottom. Some CodeManu sheets
                // pad their final row; strip only trailing blanks, preserving intentional
                // transparent frames within an animation's actual timing.
                var pixels = tex.GetPixels32();
                for (int y = tex.height - cell; y >= 0; y -= cell)
                    for (int x = 0; x < tex.width; x += cell)
                        frames.Add(Make(tex, new Rect(x, y, cell, cell), new Vector2(0.5f, 0.5f), cell));
                while (frames.Count > 0 && Empty(pixels, tex.width, frames[frames.Count - 1].rect))
                { DestroyOwned(frames[frames.Count - 1]); frames.RemoveAt(frames.Count - 1); }
            }
            return sheets[key] = frames.ToArray();
        }

        static bool Empty(Color32[] pixels, int width, Rect r)
        {
            for (int y = (int)r.y; y < r.yMax; y++)
                for (int x = (int)r.x; x < r.xMax; x++)
                    if (pixels[y * width + x].a > 8) return false;
            return true;
        }

        public Sprite Prop(string name, float ppu = 32)
        {
            if (sheets.TryGetValue(name, out var cached)) return cached.Length > 0 ? cached[0] : null;
            var tex = Texture(name);
            var frames = Array.Empty<Sprite>();
            if (tex != null)
            {
                var pixels = tex.GetPixels32(); int left = tex.width, right = 0, bottom = tex.height, top = 0;
                for (int y = 0; y < tex.height; y++) for (int x = 0; x < tex.width; x++)
                    if (pixels[y * tex.width + x].a > 8)
                    { left = Math.Min(left, x); right = Math.Max(right, x + 1); bottom = Math.Min(bottom, y); top = Math.Max(top, y + 1); }
                if (right > left && top > bottom)
                    frames = new[] { Make(tex, new Rect(left, bottom, right - left, top - bottom), new Vector2(.5f, 0), ppu) };
            }
            sheets[name] = frames;
            return frames.Length > 0 ? frames[0] : null;
        }

        Sprite Make(Texture2D tex, Rect rect, Vector2 pivot, float ppu)
        {
            var sprite = Sprite.Create(tex, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
            owned.Add(sprite);
            return sprite;
        }

        void DestroyOwned(UnityEngine.Object value) { owned.Remove(value); Release(value); }
        public static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
        public void Dispose() { foreach (var value in owned) Release(value); owned.Clear(); sheets.Clear(); pixels.Clear(); }
    }
}
