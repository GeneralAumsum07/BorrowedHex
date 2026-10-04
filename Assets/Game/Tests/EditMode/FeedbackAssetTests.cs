using BorrowedHex.EditorTools;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Every sheet the VFX pass names must import the way WorldArtLibrary needs it: readable
    // (it reads pixels to strip trailing blank frames) and point filtered (pixel art). A sheet
    // that silently fails to import would just draw nothing, which no other test would notice.
    public class FeedbackAssetTests
    {
        public static readonly string[] Sheets =
        {
            // Shot shapes (spec 3.1)
            "Magic Missile", "Ice Shard Shot", "Fireball Shot", "Homing Orb", "Arcane Orb", "Dark Slash",
            // Trails (3.3)
            "Fire Trail", "Spark Trail",
            // Endings, hits, kills, moments (3.4, 4.1)
            "Small Pop", "Block Spark", "Star Burst", "Casting", "Hit Spark", "Pierce Spark", "Chain Lightning",
            "Shock Hit", "Smoke Burst", "Weak Hit", "Heavy Hit", "Blast", "Parry Flash", "Critical Star",
            "Overload", "Big Boom", "Zap Ring",
            // Skill nodes and passives (5.2, 5.3)
            "Spark Burst", "Charge Up", "Heal", "Notify Ping", "Shield Bubble",
            // Playtest pass: the Collector's stream (a recoloured Plasma Shot), the slam's dust,
            // the sweep's slash.
            "Collector Plasma", "Landing Dust", "Dirt Kick", "Wide Cleave",
        };

        // Casting is the one 100-pixel sheet (already imported for the boss); the rest are 32.
        public static int CellOf(string sheet) => sheet == "Casting" ? 100 : 32;

        [TestCaseSource(nameof(Sheets))]
        public void EverySheetImportsReadableAndPointFilteredAndSlicesIntoFrames(string sheet)
        {
            using (var art = new WorldArtLibrary())
            {
                // Licensed packs are local-only (.gitignore); a public checkout has none of them.
                if (!art.HasBoss) Assert.Ignore("Local licensed art is optional in a public checkout.");
                var tex = art.Texture(sheet);
                Assert.NotNull(tex, $"{sheet}.png is in Resources/WorldArt");
                Assert.IsTrue(tex.isReadable, "WorldArtLibrary reads pixels");
                Assert.AreEqual(FilterMode.Point, tex.filterMode);
                int cell = CellOf(sheet);
                int cells = tex.width / cell * (tex.height / cell);
                Assert.That(art.Effect(sheet, cell).Length, Is.InRange(2, cells), "an animation, not a still");
            }
        }

        [Test]
        public void TheCalloutFontIsInResources()
        {
            // The font is licensed third-party content too, so it lives beside the sheets.
            using (var art = new WorldArtLibrary())
                if (!art.HasBoss) Assert.Ignore("Local licensed art is optional in a public checkout.");
            Assert.NotNull(Resources.Load<Font>("WorldArt/m5x7"));
        }

        // The sheets are git-ignored, so the importer is the only committed way to recreate
        // them: every sheet this pass uses, and the font, must be on its list.
        [Test]
        public void TheImporterRecreatesEverySheetAndTheFont()
        {
            var sources = WorldArtImporter.Sources();
            var recolours = WorldArtImporter.Recolours();
            foreach (var sheet in Sheets)
                Assert.IsTrue(sources.ContainsKey(sheet + ".png") || recolours.ContainsKey(sheet + ".png"), sheet);
            // A recolour is made from a copied sheet, so its source must be on the copy list.
            foreach (var r in recolours.Values) Assert.IsTrue(sources.ContainsKey(r.From), r.From);
            Assert.IsTrue(sources.ContainsKey("m5x7.ttf"));
        }

        // The Collector's stream is Plasma Shot hue-shifted from teal into the Collector's
        // magenta. A hue rotation keeps each pixel's brightness and saturation, so the shading
        // survives; a multiply tint would only darken the teal.
        [Test]
        public void TheHueShiftMovesTealToMagentaAndKeepsShadingAndAlpha()
        {
            var teal = new Color32(40, 200, 190, 255);
            var shifted = WorldArtImporter.ShiftHue(teal, WorldArtImporter.Recolours()["Collector Plasma.png"].Degrees);
            Color.RGBToHSV(shifted, out var h, out var sat, out var v);
            Color.RGBToHSV(teal, out _, out var sat0, out var v0);
            Assert.That(h * 360f, Is.InRange(280f, 320f), "magenta, the Homing Orb hue");
            Assert.AreEqual(sat0, sat, 0.02f); Assert.AreEqual(v0, v, 0.02f);
            var clear = new Color32(40, 200, 190, 0);
            Assert.AreEqual(0, WorldArtImporter.ShiftHue(clear, 120f).a, "transparency is untouched");
        }
    }
}
