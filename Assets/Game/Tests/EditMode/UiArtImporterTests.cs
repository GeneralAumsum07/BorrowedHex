using System.IO;
using BorrowedHex.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // The UI packs are licensed and git-ignored; Sources() is the committed recipe for rebuilding
    // them. These tests pin the recipe and the recolour maths, not the presence of the files.
    public class UiArtImporterTests
    {
        [Test]
        public void EverySourceHasAnExtensionAndADistinctDestination()
        {
            var s = UiArtImporter.Sources();
            Assert.Greater(s.Count, 10);
            foreach (var pair in s)
            {
                StringAssert.EndsWith(Path.GetExtension(pair.Value), pair.Key, $"{pair.Key} keeps its source type");
                Assert.IsFalse(pair.Key.Contains("/"), "flat destination folder");
            }
        }

        [Test]
        public void GoldMapsReadOnlyFilesThatAreImported()
        {
            foreach (var pair in UiArtImporter.GoldMaps())
                Assert.IsTrue(UiArtImporter.Sources().ContainsKey(pair.Value), $"{pair.Key} maps a copied file");
        }

        [Test]
        public void GoldMapKeepsAlphaAndBrightnessOrder()
        {
            // Three violet pixels of rising brightness plus one clear pixel.
            var px = new[] { new Color32(40, 20, 70, 255), new Color32(90, 50, 150, 255),
                             new Color32(200, 160, 255, 128), new Color32(255, 0, 255, 0) };
            UiArtImporter.GoldMap(px);
            Assert.AreEqual(255, px[0].a); Assert.AreEqual(128, px[2].a); Assert.AreEqual(0, px[3].a);
            float L(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            Assert.Less(L(px[0]), L(px[1])); Assert.Less(L(px[1]), L(px[2]));
            // The darkest opaque pixel lands on the ramp's ink, the brightest on its ivory.
            Assert.AreEqual(UiArtImporter.Ramp[0], px[0]);
            Assert.AreEqual(UiArtImporter.Ramp[3].r, px[2].r);
        }

        [Test]
        public void GoldMapOfAFlatSheetDoesNotDivideByZero()
        {
            var px = new[] { new Color32(80, 40, 120, 255), new Color32(80, 40, 120, 255) };
            UiArtImporter.GoldMap(px);
            Assert.AreEqual(px[0], px[1]);
        }
    }
}
