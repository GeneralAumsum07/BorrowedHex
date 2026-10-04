using BorrowedHex.Presentation;
using BorrowedHex.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Spec 4.3: silhouettes and a 20% flash for exactly 2 rendered frames.
    public class ImpactFrameTests
    {
        [Test]
        public void LastsTwoRenderedFramesThenRestoresEveryone()
        {
            var parent = new GameObject("IFTest");
            var actor = CharacterView.Create(parent.transform, "A", PixelSprites.Kind.Magician, 1f, .4f);
            var impact = new ImpactFrame();
            try
            {
                impact.Begin(Color.yellow, actor, null);   // a null actor (already despawned) is skipped
                Assert.IsTrue(impact.Active); Assert.IsTrue(actor.Silhouette);
                impact.Tick();   // end of rendered frame 1
                impact.Tick();   // end of rendered frame 2
                Assert.IsTrue(impact.Active, "both frames were drawn lit");
                impact.Tick();
                Assert.IsFalse(impact.Active); Assert.IsFalse(actor.Silhouette);
            }
            finally { impact.Dispose(); Object.DestroyImmediate(parent); }
        }

        // The flash canvas is its own root object, so on play-mode exit (or a test teardown that
        // sweeps every Canvas) Unity can destroy it before ArenaView.OnDestroy disposes the frame.
        // Dispose must then be a quiet no-op, not a MissingReferenceException.
        [Test]
        public void DisposeAfterTheCanvasIsGoneDoesNotThrow()
        {
            var impact = new ImpactFrame();
            impact.Begin(Color.yellow);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "ImpactFlash") Object.DestroyImmediate(c.gameObject);
            Assert.DoesNotThrow(() => impact.Tick());
            Assert.DoesNotThrow(() => impact.Dispose());
            Assert.IsFalse(impact.Active);
        }
    }
}
