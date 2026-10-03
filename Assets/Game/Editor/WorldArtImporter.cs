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

        public static int Import(string source)
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
                "Slashes/Wide Cleave", "Slashes/Dark Slash", "Hit Sparks/Parry Flash", "Hit Sparks/Heavy Hit", "Projectiles/Magic Missile" })
                files[Path.GetFileName(pair)] = Essentials + pair + ".png";

            // Validate all inputs before creating anything: a misspelled source folder must
            // not leave a half-updated library that silently changes the running game.
            foreach (var pair in files)
                if (!File.Exists(Path.Combine(source, pair.Value))) throw new FileNotFoundException(pair.Value);
            Directory.CreateDirectory(Destination);
            AssetDatabase.StartAssetEditing();
            try { foreach (var pair in files) File.Copy(Path.Combine(source, pair.Value), Destination + "/" + pair.Key + ".png", true); }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var pair in files)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Destination + "/" + pair.Key + ".png");
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
            return files.Count;
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
