using System;
using System.Collections.Generic;
using BorrowedHex.Presentation.WorldArt;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// Spec 4.3: on the rarest moments the actors involved turn into solid white silhouettes and
    /// the screen flashes faintly in the moment's colour, for exactly 2 rendered frames. Counted
    /// in frames, not seconds, because the effect is a "frame" by definition and it plays during
    /// hit-stop, when no sim time passes.
    /// </summary>
    public sealed class ImpactFrame : IDisposable
    {
        public const int Frames = 2;
        public const float FlashAlpha = 0.2f;

        readonly Canvas canvas;
        readonly Image flash;
        readonly List<CharacterView> lit = new List<CharacterView>();
        int framesLeft;

        public bool Active => flash.enabled;

        public ImpactFrame()
        {
            // Above the HUD: 20% alpha never hides it, and the flash should cover everything.
            canvas = UI.Ui.CreateCanvas("ImpactFlash", 500);
            var go = new GameObject("Flash", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            flash = go.AddComponent<Image>();
            flash.raycastTarget = false;   // never eats a click
            flash.enabled = false;
        }

        public void Begin(Color color, params CharacterView[] actors)
        {
            End();
            framesLeft = Frames;
            color.a = FlashAlpha;
            flash.color = color;
            flash.enabled = true;
            if (actors == null) return;
            foreach (var a in actors) if (a != null) { a.SetSilhouette(true); lit.Add(a); }
        }

        /// <summary>Call once at the end of each rendered frame.</summary>
        public void Tick()
        {
            if (!Active) return;
            // Frame 1 and frame 2 each decrement; the third call (before frame 3 draws) ends it.
            if (framesLeft == 0) End(); else framesLeft--;
        }

        void End()
        {
            flash.enabled = false;
            foreach (var a in lit) if (a != null) a.SetSilhouette(false);
            lit.Clear();
            framesLeft = 0;
        }

        public void Dispose()
        {
            End();
            if (canvas != null) WorldArtLibrary.Release(canvas.gameObject);
        }
    }
}
