// UiSkinTests.cs
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class UiSkinTests
    {
        [TearDown] public void TearDown() => UiSkin.ForceFlat = false;

        // Review Focus 1: a public checkout has no UiArt; nothing may throw or return junk.
        [Test]
        public void MissingArtFallsBackToFlatColour()
        {
            UiSkin.ForceFlat = true;
            Assert.IsFalse(UiSkin.HasArt);
            foreach (var id in UiSkin.Ids) Assert.IsNull(UiSkin.Sprite(id), id);
            Assert.IsEmpty(UiSkin.Frames("pointer"));
        }

        [Test]
        public void EveryRectLiesInsideItsSheet()
        {
            if (!UiSkin.HasArt) Assert.Ignore("local UI art not imported");
            foreach (var id in UiSkin.Ids)
            {
                var s = UiSkin.Sprite(id);
                Assert.NotNull(s, id);
                Assert.LessOrEqual(s.rect.xMax, s.texture.width, id);
                Assert.LessOrEqual(s.rect.yMax, s.texture.height, id);
                // A border wider than half the sprite collapses the 9-slice centre to nothing.
                Assert.Less(s.border.x + s.border.z, s.rect.width, id);
                Assert.Less(s.border.y + s.border.w, s.rect.height, id);
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, id);
            }
        }

        [Test]
        public void SpritesAreTwoReferencePixelsPerArtPixel()
        {
            if (!UiSkin.HasArt) Assert.Ignore("local UI art not imported");
            Assert.AreEqual(50f, UiSkin.Sprite("frame.card").pixelsPerUnit);
        }
    }
}
