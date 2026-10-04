using System;
using System.Collections.Generic;
using System.IO;
using BorrowedHex.Presentation.WorldArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Rebuild the curated local art library from the owner's extracted downloads. Import
    /// through Unity so metadata stays owned by the editor; never patch serialized YAML.
    /// This deliberately does not call ProjectBootstrap or alter tuning/player settings.
    /// </summary>
    public static class WorldArtImporter
    {
        public const string Destination = "Assets/Game/Resources/WorldArt";
        static readonly string World = "OpenWorldandcavedung_1.0/Open World And Cave Dungeon/";
        static readonly string Essentials = "Pixel VFX Essentials/Spritesheets/";

        [MenuItem("Borrowed Hex/Art/Import Local Downloads")]
        public static void ImportDefault() => Import(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads/itch_downloads"));

        /// <summary>
        /// Every local file the game loads from Resources/WorldArt, keyed by its destination file
        /// name (with extension), valued by its path under the downloads folder. That folder is
        /// git-ignored (licensed packs), so this list is the committed record of how to rebuild it.
        /// </summary>
        public static Dictionary<string, string> Sources()
        {
            var files = new Dictionary<string, string> {
                ["Necromancer"] = "Necromancer_creativekind-Sheet.png",
                ["WorldGround"] = World + "1. OpenWorld/1.First Layer/mainground.png",
                ["CaveGround"] = World + "2. Caves/1.First Layer/maingroundA.png",
                ["CaveWalls"] = World + "2. Caves/0.Rockslevelbuild/maincave_buildA.png",
                ["DeadTree"] = World + "1. OpenWorld/4.SingleObj/Decorative/DestTree/Dtree_01.png",
                ["Rock"] = World + "2. Caves/4. SingleObj/Rocks/rockA_01.png",
                ["Obelisk"] = World + "1. OpenWorld/4.SingleObj/Special/monA_01.png",
                ["Tomb"] = World + "Destructive objects/Tomb A/tomb-A-main-static-00.png",
                ["TombCrack1"] = World + "Destructive objects/Tomb A/tomb-A-crack-1-00.png",
                ["TombCrack2"] = World + "Destructive objects/Tomb A/tomb-A-crack-2-00.png",
                ["TombRubble"] = World + "Destructive objects/Tomb A/tomb-A-static-destroyed-00.png",
                ["GraveSky"] = "Graveyard scenery and backgrounds/Final/Background_0.png",
                ["GraveSilhouette"] = "Graveyard scenery and backgrounds/Final/Background_1.png",
                ["GraveTiles"] = "Graveyard scenery and backgrounds/Final/Tiles.png",
                ["GraveBrush"] = "Graveyard scenery and backgrounds/Final/brush.png",
                ["Temple"] = "RTB_v1.0/background4.png",
                ["TempleDark"] = "RTB_v1.0/background3.png",
                ["MossGround"] = World + "1. OpenWorld/2.Second Layer/grassB_2.png",
                ["Crystal"] = World + "Destructive objects/Crystal A/crystal-A-main-static-00.png",
                ["Urn"] = World + "Destructive objects/Urn A/urn-A-main-static-00.png",
                ["Ruin"] = World + "1. OpenWorld/4.SingleObj/Special/monC_01.png",
                ["RunePillar"] = World + "1. OpenWorld/4.SingleObj/Special/monB_01.png",
                ["RockTree"] = World + "2. Caves/4. SingleObj/Rocks/rockB_01.png",
                ["Vortex"] = "Free Pixel Effects Pack/13_vortex_spritesheet.png",
                ["Midnight"] = "Free Pixel Effects Pack/18_midnight_spritesheet.png",
                ["Casting"] = "Free Pixel Effects Pack/4_casting_spritesheet.png",
            };
            foreach (var pair in new[] { "Smoke and Dust/Fog Drift", "Smoke and Dust/Ash Fall", "Smoke and Dust/Dust Cloud",
                "Smoke and Dust/Footstep", "Ambient/Dust Motes", "Ambient/Embers Ambient", "Fire/Torch", "Fire/Blue Flame",
                "Explosions/Rock Burst", "Explosions/Shockwave", "Magic/Teleport", "Magic/Dark Curse",
                "Slashes/Wide Cleave", "Slashes/Dark Slash", "Hit Sparks/Parry Flash", "Hit Sparks/Heavy Hit", "Projectiles/Magic Missile",
                // VFX pass (spec 2026-10-04 section 6): shot shapes, trails, hits, moments, passives.
                "Projectiles/Ice Shard Shot", "Projectiles/Fireball Shot", "Projectiles/Homing Orb",
                "Magic/Arcane Orb", "Magic/Heal", "Magic/Shield Bubble", "Fire/Fire Trail",
                "Lightning/Spark Trail", "Lightning/Chain Lightning", "Lightning/Overload", "Lightning/Charge Up",
                "Lightning/Spark Burst", "Lightning/Zap Ring",
                "Hit Sparks/Hit Spark", "Hit Sparks/Pierce Spark", "Hit Sparks/Critical Star", "Hit Sparks/Weak Hit",
                "Hit Sparks/Block Spark", "Hit Sparks/Shock Hit",
                "Explosions/Small Pop", "Explosions/Smoke Burst", "Explosions/Big Boom", "Explosions/Blast",
                "Pickups and UI/Star Burst", "Pickups and UI/Notify Ping",
                // Playtest pass: the Collector stream's source sheet (recoloured below), the
                // slam's ground dust and the sweep's kicked-up dirt.
                "Projectiles/Plasma Shot", "Smoke and Dust/Landing Dust", "Smoke and Dust/Dirt Kick" })
                files[Path.GetFileName(pair)] = Essentials + pair + ".png";
            // Keys gain their extension here: the callout font is not a texture.
            var withExtension = new Dictionary<string, string>();
            foreach (var pair in files) withExtension[pair.Key + ".png"] = pair.Value;
            // The callout font (spec 4.2.4): third-party like the sheets, so it lives beside them.
            withExtension["m5x7.ttf"] = "m5x7.ttf";
            return withExtension;
        }

        /// <summary>A sheet made from another imported sheet by rotating its hue.</summary>
        public struct Recolour { public string From; public float Degrees; }

        /// <summary>
        /// Derived sheets, keyed by destination file name. They are generated from a copied
        /// sheet rather than downloaded, so like Sources() this list is the committed recipe.
        /// Plasma Shot is teal; +120 degrees lands on the Collector's magenta (the Homing Orb's
        /// hue). A sprite tint could not do this: tints multiply, so teal would only go dark.
        /// </summary>
        public static Dictionary<string, Recolour> Recolours() => new Dictionary<string, Recolour> {
            ["Collector Plasma.png"] = new Recolour { From = "Plasma Shot.png", Degrees = 120f },
        };

        /// <summary>Rotate one pixel's hue, keeping saturation, brightness and alpha (so shading survives).</summary>
        public static Color32 ShiftHue(Color32 c, float degrees)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            var shifted = (Color32)Color.HSVToRGB(Mathf.Repeat(h + degrees / 360f, 1f), s, v);
            shifted.a = c.a;
            return shifted;
        }

        static void WriteRecolour(string to, Recolour r)
        {
            // Read the copied PNG's bytes, not the imported asset: the importer settings below
            // have not run yet, and the bytes are the untouched source either way.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadImage(File.ReadAllBytes(Destination + "/" + r.From));
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i] = ShiftHue(px[i], r.Degrees);
                tex.SetPixels32(px);
                File.WriteAllBytes(Destination + "/" + to, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        public static int Import(string source)
        {
            var files = Sources();

            // Validate all inputs before creating anything: a misspelled source folder must
            // not leave a half-updated library that silently changes the running game.
            foreach (var pair in files)
                if (!File.Exists(Path.Combine(source, pair.Value))) throw new FileNotFoundException(pair.Value);
            Directory.CreateDirectory(Destination);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in files) File.Copy(Path.Combine(source, pair.Value), Destination + "/" + pair.Key, true);
                foreach (var pair in Recolours()) WriteRecolour(pair.Key, pair.Value);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var textures = new List<string>(files.Keys);
            textures.AddRange(Recolours().Keys);   // derived sheets need the same pixel-art import
            foreach (var name in textures)
            {
                if (!name.EndsWith(".png")) continue;   // the font keeps Unity's default font import
                var importer = (TextureImporter)AssetImporter.GetAtPath(Destination + "/" + name);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                // Sprite sheets have intentionally non-power-of-two dimensions. Rescaling
                // changes both the frame grid and the proportions of every animation.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = true; // sprite padding checks and sampled floor surfaces
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096; // Necromancer is 2720px wide; never shrink its cells
                importer.SaveAndReimport();
            }
            return files.Count + Recolours().Count;
        }

        [MenuItem("Borrowed Hex/Art/Install World Presentation")]
        public static void Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ProjectBootstrap.ArenaScene || scene.isDirty)
                throw new InvalidOperationException("Install into the saved Arena scene; preserve other unsaved scenes.");
            var root = GameObject.Find("WorldPresentation");
            if (root == null) root = new GameObject("WorldPresentation");
            if (root.GetComponent<WorldPresentation>() == null) root.AddComponent<WorldPresentation>();
            var game = UnityEngine.Object.FindFirstObjectByType<BorrowedHex.Presentation.GameRoot>();
            if (game != null) { game.useWorldArenas = true; EditorUtility.SetDirty(game); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
