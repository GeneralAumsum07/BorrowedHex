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
            foreach (var sheet in Sheets) Assert.IsTrue(sources.ContainsKey(sheet + ".png"), sheet);
            Assert.IsTrue(sources.ContainsKey("m5x7.ttf"));
        }
    }
}
