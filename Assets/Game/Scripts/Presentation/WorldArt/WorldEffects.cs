using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Bounded presentation pool; all animation advances on the simulation clock.</summary>
    public sealed class WorldEffects : System.IDisposable
    {
        sealed class Effect
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public double Began;
            public float Fps;
            public bool Loop, Ground;
        }
        readonly List<Effect> effects = new List<Effect>();
        readonly Transform root;
        readonly WorldArtLibrary art;
        public int ActiveCount { get { int n = 0; foreach (var e in effects) if (e.Renderer.enabled) n++; return n; } }

        public WorldEffects(Transform parent, WorldArtLibrary library, string name = "WorldEffects")
        {
            art = library;
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
        }

        public void Spawn(string name, Vector3 at, float scale, double now, Color color, bool ground = false, bool loop = false, float fps = 12, int cell = 32)
            => Spawn(art.Effect(name, cell), at, scale, now, color, ground, loop, fps);

        public void Spawn(Sprite[] frames, Vector3 at, float scale, double now, Color color, bool ground, bool loop, float fps)
        {
            if (frames.Length == 0 || fps <= 0) return;
            Effect effect = null;
            foreach (var candidate in effects) if (!candidate.Renderer.enabled) { effect = candidate; break; }
            if (effect == null)
            {
                if (effects.Count >= 64) return; // endless waves cannot grow this pool indefinitely
                var go = new GameObject("PixelEffect"); go.transform.SetParent(root, false);
                effect = new Effect { Renderer = go.AddComponent<SpriteRenderer>() }; effects.Add(effect);
            }
            effect.Frames = frames; effect.Began = now; effect.Fps = fps; effect.Loop = loop; effect.Ground = ground;
            var sr = effect.Renderer;
            sr.enabled = true; sr.sprite = frames[0]; sr.color = color;
            sr.transform.position = at; sr.transform.localScale = Vector3.one * scale;
            sr.transform.rotation = ground ? Quaternion.Euler(90, 0, 0) : Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            // Telegraphs already use -5 and above. Decorative ground bursts sit under them.
            sr.sortingOrder = ground ? -6 : 1;
        }

        public void Render(double now, Camera camera)
        {
            foreach (var e in effects)
            {
                if (!e.Renderer.enabled) continue;
                double elapsed = System.Math.Max(0, now - e.Began);
                if (!e.Loop && elapsed >= e.Frames.Length / e.Fps) { e.Renderer.enabled = false; continue; }
                e.Renderer.sprite = e.Frames[WorldArtPolicy.Frame(elapsed, e.Frames.Length, e.Fps, e.Loop)];
                if (!e.Ground && camera != null) e.Renderer.transform.rotation = camera.transform.rotation;
            }
        }

        public void Clear() { foreach (var e in effects) e.Renderer.enabled = false; }
        public void Dispose() { if (root != null) WorldArtLibrary.Release(root.gameObject); effects.Clear(); }
    }
}
