using System.Collections.Generic;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>The code-drawn icon set (plan Task 9): coverage, size, palette and legibility.</summary>
    public class UiGlyphsTests
    {
        static IEnumerable<string> Required()
        {
            foreach (var n in SkillTree.Nodes) yield return "skill." + n.Id;
            foreach (var b in new[] { "precision", "mobility", "resilience", "blood" }) yield return "branch." + b;
            foreach (UpgradeId u in System.Enum.GetValues(typeof(UpgradeId))) yield return "upgrade." + u;
            foreach (var p in new[] { "bolt", "riposte", "rocket" }) yield return "payload." + p;
            foreach (var s in new[] { "frozen", "unstable", "fused", "locked", "overcharge", "check" }) yield return "state." + s;
            foreach (var st in CaptureStyles.All) yield return "style." + st.Id;
        }

        [Test]
        public void EveryThingThatNeedsAnIconHasOne()
        {
            foreach (var id in Required()) Assert.NotNull(UiGlyphs.Get(id), id);
        }

        [Test]
        public void GlyphsAre12By12OnThePixelGrid()
        {
            foreach (var id in UiGlyphs.Ids)
            {
                var s = UiGlyphs.Get(id);
                Assert.AreEqual(12, (int)s.rect.width, id); Assert.AreEqual(12, (int)s.rect.height, id);
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, id);
            }
        }

        [Test]
        public void BitmapsUseOnlyTheRampAndAreSquare()
        {
            foreach (var pair in UiGlyphs.Bitmaps)
            {
                Assert.AreEqual(12, pair.Value.Length, pair.Key);
                foreach (var row in pair.Value)
                {
                    Assert.AreEqual(12, row.Length, pair.Key);
                    foreach (char c in row) StringAssert.Contains(c.ToString(), ".dmhw", pair.Key);
                }
            }
        }

        // A glyph must read at a glance: enough ink, an outline, and not a copy of another one.
        [Test]
        public void GlyphsAreDistinctAndHaveSubstance()
        {
            var seen = new HashSet<string>();
            foreach (var pair in UiGlyphs.Bitmaps)
            {
                string flat = string.Concat(pair.Value);
                Assert.IsTrue(seen.Add(flat), $"{pair.Key} duplicates another glyph");
                int ink = 0, outline = 0;
                foreach (char c in flat) { if (c != '.') ink++; if (c == 'd') outline++; }
                Assert.GreaterOrEqual(ink, 20, $"{pair.Key} too faint to read");
                Assert.Greater(outline, 0, $"{pair.Key} needs a dark outline to read on gold frames");
            }
        }

        [Test] public void UnknownIdsAreNull() => Assert.IsNull(UiGlyphs.Get("nope"));
    }
}
