using BorrowedHex.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // D6: hit reaction is visual only. The sprite nudges and squashes; the root (which sits on
    // the logical position) never moves, so art can never shift a hitbox.
    public class CharacterViewFeedbackTests
    {
        GameObject parent;
        CharacterView view;

        [SetUp] public void SetUp() { parent = new GameObject("CVTest"); view = CharacterView.Create(parent.transform, "E", PixelSprites.Kind.Magician, 1f, .4f); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(parent);

        [Test]
        public void RecoilNudgesTheSpriteAndSettlesWithoutMovingTheRoot()
        {
            float t0 = Time.unscaledTime;
            view.Recoil(Vector2.right);
            view.ApplyRecoil(t0);
            Assert.AreEqual(CharacterView.RecoilDistance, view.BillboardOffset.x, 1e-4);
            Assert.AreEqual(Vector3.zero, view.transform.localPosition, "the root never moves");
            view.ApplyRecoil(t0 + CharacterView.RecoilSeconds);
            // A tolerance, not exact zero: (t0 + 0.1) - t0 is not exactly 0.1 in float once the
            // editor has been up a while, so k lands near 1e-5 rather than at 0.
            Assert.Less(view.BillboardOffset.magnitude, 1e-4f, "settled by 0.1 s");
        }

        [Test]
        public void SilhouetteSwapsTheMaterialAndRestoresIt()
        {
            var body = view.GetComponentInChildren<SpriteRenderer>(true);
            var normal = view.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().sharedMaterial;
            view.SetSilhouette(true);
            Assert.IsTrue(view.Silhouette);
            view.SetSilhouette(false);
            Assert.IsFalse(view.Silhouette);
            Assert.AreSame(normal, view.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().sharedMaterial);
            Assert.NotNull(body);
        }

        SpriteRenderer Body => view.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>();

        // Playtest: enemies showed no white flash when hit. The old flash lerped a multiply tint
        // toward white, and the tint was already white, so it changed nothing. A real white
        // flash draws the sprite through the silhouette material for the flash's length.
        [Test]
        public void AHitFlashDrawsTheSpriteWhiteAndThenRestoresIt()
        {
            var normal = Body.sharedMaterial;
            float t0 = Time.unscaledTime;
            view.Flash(0.1f);
            view.ApplyFlash(t0);
            Assert.IsTrue(view.DrawnWhite);
            Assert.AreNotSame(normal, Body.sharedMaterial, "drawn through the silhouette material");
            view.ApplyFlash(t0 + 1f);
            Assert.IsFalse(view.DrawnWhite);
            Assert.AreSame(normal, Body.sharedMaterial, "the sprite's own material is back");
        }

        // The flash and the impact silhouette share the material: whichever ends first must not
        // turn the other off.
        [Test]
        public void AFlashEndingDuringAnImpactKeepsTheSilhouette()
        {
            var normal = Body.sharedMaterial;
            float t0 = Time.unscaledTime;
            view.Flash(0.05f);
            view.SetSilhouette(true);
            view.ApplyFlash(t0 + 1f);                       // the flash is over, the impact is not
            Assert.IsTrue(view.DrawnWhite);
            view.SetSilhouette(false);
            // SetSilhouette applies at the real clock, which an EditMode test cannot move, so
            // the 0.05 s flash may still be "running" there; step past it explicitly.
            view.ApplyFlash(t0 + 1f);
            Assert.IsFalse(view.DrawnWhite);
            Assert.AreSame(normal, Body.sharedMaterial);
        }

        // A faded or telegraphing enemy (ArenaView tints it translucent) flashes translucent,
        // not as a sudden opaque block, and keeps that tint after.
        [Test]
        public void TheFlashKeepsTheTintsAlpha()
        {
            float t0 = Time.unscaledTime;
            view.SetTint(new Color(1f, 1f, 1f, 0.45f));
            view.Flash(0.1f);
            view.ApplyFlash(t0);
            Assert.AreEqual(0.45f, Body.color.a, 1e-4);
            view.ApplyFlash(t0 + 1f);
            Assert.AreEqual(0.45f, Body.color.a, 1e-4);
        }
    }
}
