using System;
using System.Collections.Generic;
using BorrowedHex.Presentation.WorldArt;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// Rare-moment words ("Parry!", "Chain x4!") in world space above the moment (spec 4.2.4).
    /// Pooled and capped: in a busy fight the newest word is the one that matters, so a fourth
    /// callout recycles the oldest, and a repeat refreshes the live one instead of stacking a
    /// tower of identical text. Ages run on real time, so words still play out during hit-stop.
    /// </summary>
    public sealed class CalloutText : IDisposable
    {
        public const float Life = 0.6f, Rise = 0.6f, PopIn = 0.08f, Height = 1.6f;
        const float PopScale = 1.5f;
        // Two words at one moment (Parry! and Perfect! off the same catch) used to print on
        // top of each other (final review minor). A word lands one line above any live word
        // near the same spot; StackRadius is how near counts, on the floor plane.
        public const float StackStep = 0.45f, StackRadius = 1.5f;

        sealed class Item { public TextMesh Text; public Vector3 Origin; public float Age; public bool Live; }
        readonly List<Item> items = new List<Item>();
        readonly Transform root;
        readonly Font font;

        public CalloutText(Transform parent)
        {
            root = new GameObject("Callouts").transform;
            root.SetParent(parent, false);
            // m5x7 (spec 6) when the local art is imported. It is licensed third-party content,
            // so it lives in the git-ignored WorldArt folder; a public checkout falls back to the
            // built-in font and still shows its words.
            font = Resources.Load<Font>("WorldArt/m5x7");
            if (font == null) font = UI.Ui.Font;
            else { PointFilter(font); Font.textureRebuilt += PointFilter; }
        }

        // A dynamic font rebuilds its atlas when new glyphs appear and the new texture comes back
        // bilinear. Re-point it every time, or a pixel font blurs mid-run.
        void PointFilter(Font f)
        {
            if (f == font && f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point;
        }

        public int LiveCount { get { int n = 0; foreach (var i in items) if (i.Live) n++; return n; } }
        public List<string> LiveTexts { get { var l = new List<string>(); foreach (var i in items) if (i.Live) l.Add(i.Text.text); return l; } }
        public List<Vector3> LiveOrigins { get { var l = new List<Vector3>(); foreach (var i in items) if (i.Live) l.Add(i.Origin); return l; } }

        public void Show(string text, Color color, Vector3 at)
        {
            Item item = null;
            foreach (var i in items) if (i.Live && i.Text.text == text) { item = i; break; }   // repeat: refresh
            if (item == null) foreach (var i in items) if (!i.Live) { item = i; break; }
            if (item == null && items.Count < FeedbackPolicy.MaxCallouts) item = Create();
            if (item == null)   // full: the oldest gives way
                foreach (var i in items) if (item == null || i.Age > item.Age) item = i;
            item.Live = true; item.Age = 0f;
            item.Origin = Stacked(item, at + Vector3.up * Height);
            item.Text.text = text; item.Text.color = color;
            item.Text.gameObject.SetActive(true);
            Apply(item, null);
        }

        Vector3 Stacked(Item self, Vector3 origin)
        {
            // At most MaxCallouts steps: there are never more live words than that to clear.
            for (int step = 0; step < FeedbackPolicy.MaxCallouts; step++)
            {
                bool clash = false;
                foreach (var i in items)
                {
                    if (i == self || !i.Live) continue;
                    var flat = new Vector2(i.Origin.x - origin.x, i.Origin.z - origin.z);
                    if (flat.magnitude < StackRadius && Mathf.Abs(i.Origin.y - origin.y) < StackStep) { clash = true; break; }
                }
                if (!clash) break;
                origin += Vector3.up * StackStep;
            }
            return origin;
        }

        Item Create()
        {
            var go = new GameObject("Callout");
            go.transform.SetParent(root, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            // 0.08, tuned from the capture: at 0.05 a word was ~60 px wide at the gameplay camera.
            text.fontSize = 64; text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            var item = new Item { Text = text };
            items.Add(item);
            return item;
        }

        /// <summary>The whole animation as a pure function of age, so it is testable without frames.</summary>
        public static void Pose(float age, out float rise, out float scale, out float alpha)
        {
            float t = Mathf.Clamp01(age / Life);
            rise = Rise * (1f - (1f - t) * (1f - t));                    // ease out: fast up, then hangs
            scale = age < PopIn ? Mathf.Lerp(PopScale, 1f, age / PopIn) : 1f;
            alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;              // readable for most of its life
        }

        public void Tick(float dt, Camera cam)
        {
            foreach (var item in items)
            {
                if (!item.Live) continue;
                item.Age += dt;
                if (item.Age >= Life) { item.Live = false; item.Text.gameObject.SetActive(false); continue; }
                Apply(item, cam);
            }
        }

        static void Apply(Item item, Camera cam)
        {
            Pose(item.Age, out var rise, out var scale, out var alpha);
            var t = item.Text.transform;
            t.position = item.Origin + Vector3.up * rise;
            t.localScale = Vector3.one * scale;
            if (cam != null) t.rotation = cam.transform.rotation;
            var c = item.Text.color; c.a = alpha; item.Text.color = c;
        }

        public void Dispose()
        {
            Font.textureRebuilt -= PointFilter;
            if (root != null) WorldArtLibrary.Release(root.gameObject);
            items.Clear();
        }
    }
}
