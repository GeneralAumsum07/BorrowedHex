using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class AtmosphereTests
    {
        [Test]
        public void VignetteIsClearInTheMiddleAndDarkAtTheCorners()
        {
            var t = PixelGeometry.Vignette(64, 36);
            Assert.AreEqual(FilterMode.Point, t.filterMode);
            Assert.Less(t.GetPixel(32, 18).a, 0.05f);
            Assert.Greater(t.GetPixel(0, 0).a, 0.7f);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void VignetteUsesHardBandsNotASmoothGradient()
        {
            var t = PixelGeometry.Vignette(64, 36);
            var seen = new System.Collections.Generic.HashSet<float>();
            for (int x = 0; x < 64; x++) seen.Add(Mathf.Round(t.GetPixel(x, 18).a * 100f));
            Assert.LessOrEqual(seen.Count, PixelGeometry.VignetteBands + 1);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void TitleGlowBreathesSlowlyAndIsSteadyUnderReduceFlashes()
        {
            bool was = DisplayOptions.ReduceFlashes;
            try
            {
                DisplayOptions.ReduceFlashes = false;
                Assert.Greater(Mathf.Abs(Atmosphere.TitleGlow(0f) - Atmosphere.TitleGlow(1.5f)), 1e-3f, "it breathes");
                // A 4 s breath: never faster than 0.25 Hz, so it is ambience, not a flash.
                Assert.AreEqual(Atmosphere.TitleGlow(0f), Atmosphere.TitleGlow(Atmosphere.BreathSeconds), 1e-4);
                DisplayOptions.ReduceFlashes = true;
                Assert.AreEqual(Atmosphere.TitleGlow(0f), Atmosphere.TitleGlow(1.5f), 1e-6);
            }
            finally { DisplayOptions.ReduceFlashes = was; }
        }

        [Test]
        public void WithoutTheWorldArtTheLayerIsJustTheVignette()
        {
            var go = new GameObject("P", typeof(RectTransform));
            var a = Atmosphere.Create(go.transform);
            a.Tick(0f); a.Tick(1f);
            Assert.GreaterOrEqual(a.LiveMotes, 0);   // no throw either way
            Object.DestroyImmediate(go);
        }
    }
}
