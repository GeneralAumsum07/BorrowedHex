using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The nightmare layer behind every front-end screen (plan 0.4): a banded vignette over the
    /// idle arena, plus slow ash and embers drifting up from the bottom edge. Motes reuse the
    /// licensed WorldArt effect sheets; without them the layer is just the vignette. Nothing here
    /// flashes: motes fade in and out over seconds, and Reduce flashes freezes the title breath.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        public const float BreathSeconds = 4f;
        const int MaxMotes = 18;
        const float MoteLife = 7f;

        sealed class Mote { public Image Img; public Sprite[] Frames; public float Born, X, Speed, Sway; }
        readonly List<Mote> motes = new List<Mote>();
        readonly List<Sprite[]> sheets = new List<Sprite[]>();
        RectTransform rt;
        float nextSpawn;
        System.Random rng = new System.Random(7);

        public int LiveMotes => motes.Count;

        /// <summary>0..1 brightness of the title's violet aura. Constant under Reduce flashes.</summary>
        public static float TitleGlow(float now) => DisplayOptions.ReduceFlashes ? 0.6f
            : 0.6f + 0.4f * Mathf.Sin(now / BreathSeconds * Mathf.PI * 2f);

        public static Atmosphere Create(Transform parent)
        {
            var rt = Ui.Rect("Atmosphere", parent);
            Ui.Stretch(rt);
            var a = rt.gameObject.AddComponent<Atmosphere>();
            a.rt = rt;
            // Low-res texture, scaled up: the bands become chunky pixel steps on screen.
            var v = rt.gameObject.AddComponent<RawImage>();
            v.texture = PixelGeometry.Vignette(96, 54);
            v.raycastTarget = false;
            foreach (var name in new[] { "Ash Fall", "Embers Ambient", "Dust Motes" })
            {
                var tex = Resources.Load<Texture2D>("WorldArt/" + name);
                if (tex == null) continue;
                int n = tex.width / 32;
                var f = new Sprite[n];
                for (int i = 0; i < n; i++) f[i] = Sprite.Create(tex, new Rect(i * 32, 0, 32, 32), new Vector2(0.5f, 0.5f), UiSkin.PixelsPerUnit);
                a.sheets.Add(f);
            }
            return a;
        }

        public void Tick(float now)
        {
            if (sheets.Count > 0 && motes.Count < MaxMotes && now >= nextSpawn)
            {
                nextSpawn = now + 0.45f;
                var frames = sheets[rng.Next(sheets.Count)];
                var img = Ui.Image("Mote", rt, new Color(1, 1, 1, 0));
                img.raycastTarget = false; img.sprite = frames[0];
                img.rectTransform.sizeDelta = new Vector2(64, 64);   // 32-px cell at 2x
                motes.Add(new Mote { Img = img, Frames = frames, Born = now, X = (float)rng.NextDouble(),
                                     Speed = 40f + (float)rng.NextDouble() * 50f, Sway = (float)rng.NextDouble() * 6.28f });
            }
            var size = rt.rect.size;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                float age = now - m.Born;
                if (age > MoteLife) { Destroy(m.Img.gameObject); motes.RemoveAt(i); continue; }
                // Rise from below the bottom edge with a slow sideways sway; fade in, then out.
                float x = (m.X - 0.5f) * size.x + Mathf.Sin(age * 0.6f + m.Sway) * 30f;
                float y = -size.y * 0.5f - 32f + age * m.Speed;
                // Whole reference pixels only, in steps of 2 (one art pixel), so motes never shimmer.
                m.Img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / 2f) * 2f, Mathf.Round(y / 2f) * 2f);
                float a = Mathf.Min(1f, age / 1.5f) * Mathf.Min(1f, (MoteLife - age) / 2f) * 0.55f;
                m.Img.color = new Color(1, 1, 1, a);
                m.Img.sprite = m.Frames[(int)(age * 8f) % m.Frames.Length];
            }
        }

        void Update() => Tick(Time.unscaledTime);
    }
}
