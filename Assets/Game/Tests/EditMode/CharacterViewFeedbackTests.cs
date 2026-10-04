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
    }
}
