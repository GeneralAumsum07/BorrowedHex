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
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly string folder;
        public WorldArtLibrary(string resourceFolder = "WorldArt") => folder = resourceFolder;
        public Texture2D Texture(string name) => Resources.Load<Texture2D>(folder + "/" + name);
        public bool HasBoss => Texture("Necromancer") != null;

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
                for (int x = 0; x < lengths[row]; x++)
                    frames.Add(Make(texture, new Rect(x * 160, texture.height - (row + 1) * 128, 160, 128),
                        new Vector2(0.5f, 0.22f), 32));
            return sheets["Boss/" + clip] = frames.ToArray();
        }

        public Sprite[] Effect(string name, int cell = 32)
        {
            if (sheets.TryGetValue(name, out var cached)) return cached;
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
            return sheets[name] = frames.ToArray();
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
            var frames = tex == null ? Array.Empty<Sprite>() : new[] {
                Make(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0), ppu) };
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
        public void Dispose() { foreach (var value in owned) Release(value); owned.Clear(); sheets.Clear(); }
    }
}
