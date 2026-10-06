// UiFontsTests.cs
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class UiFontsTests
    {
        [Test]
        public void FontsAreNeverNull() { Assert.NotNull(UiFonts.Display); Assert.NotNull(UiFonts.Body); }

        // Pixel fonts are only crisp at whole multiples of their design size.
        [Test]
        public void EveryRoleIsAWholeMultipleOfItsFontsNativeSize()
        {
            foreach (UiFonts.Role r in System.Enum.GetValues(typeof(UiFonts.Role)))
            {
                int native = UiFonts.UsesDisplay(r) ? UiFonts.NativeDisplay : UiFonts.NativeBody;
                Assert.AreEqual(0, UiFonts.Size(r) % native, r.ToString());
            }
        }

        // Confirms each native size against the real glyphs: at a whole multiple of the native
        // size every advance is that exact multiple. Advance, not glyphHeight: Unity pads the glyph
        // rect by a constant 2 px, so heights never double even at the right size. Measured on
        // 2026-10-04: across sizes 12..20 only 16 (alagard) and 18 (m6x11plus) pass this.
        [Test]
        public void NativeSizeScalesEveryAdvanceExactly()
        {
            if (!UiFonts.HasPixelFonts) Assert.Ignore("local fonts not imported");
            const string probe = "HxMiWagq";
            foreach (var (f, n) in new[] { (UiFonts.Display, UiFonts.NativeDisplay), (UiFonts.Body, UiFonts.NativeBody) })
                foreach (char ch in probe)
                {
                    f.RequestCharactersInTexture(probe, n); f.GetCharacterInfo(ch, out var one, n);
                    Assert.Greater(one.advance, 0, f.name + " " + ch);
                    for (int k = 2; k <= 3; k++)
                    {
                        f.RequestCharactersInTexture(probe, n * k); f.GetCharacterInfo(ch, out var many, n * k);
                        Assert.AreEqual(one.advance * k, many.advance, $"{f.name} '{ch}' at x{k}");
                    }
                }
        }
    }
}
