using System;
using System.Collections.Generic;
using System.IO;
using BorrowedHex.Data;
using BorrowedHex.Presentation.WorldArt;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Bakes the painted floors once, offline: painting 2.7M pixels with several noise octaves
    /// takes seconds, far too slow for runtime. The JPGs are committed (amendment A3), so a
    /// fresh checkout never needs the owner's downloads. The Szadi overlays only add decals; a
    /// missing pack still bakes, minus decals, with a warning.
    /// </summary>
    public static class PaintedFloorBaker
    {
        const string Output = "Assets/Game/Resources/WorldFloors";
        static string Overlays => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "itch_downloads", "OpenWorldandcavedung_1.0", "Open World And Cave Dungeon", "1. OpenWorld");

        [MenuItem("Borrowed Hex/Art/Bake Painted Floors")]
        public static void Bake()
        {
            var decals = LoadDecals();
            Directory.CreateDirectory(Output);
            var written = new List<(string path, bool repeat)>();
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage); string theme = arena.worldTheme;
                int width = Mathf.RoundToInt(arena.bounds.width * PaintedFloorGenerator.PixelsPerUnit);
                int height = Mathf.RoundToInt(arena.bounds.height * PaintedFloorGenerator.PixelsPerUnit);
                written.Add((Write(theme + "Floor", PaintedFloorGenerator.Floor(theme, width, height, decals)), false));
                written.Add((Write(theme + "Outer", PaintedFloorGenerator.Outer(theme, 512)), true));
                written.Add((Write(theme + "Ribbon", PaintedFloorGenerator.Ribbon(theme, 512, 256)), true));
            }
            AssetDatabase.Refresh();
            foreach (var (path, repeat) in written) Configure(path, repeat);
            Debug.Log("Baked " + written.Count + " painted textures into " + Output);
        }

        static Dictionary<string, DecalSheet> LoadDecals()
        {
            var sources = new Dictionary<string, (string file, int minSize)>
            {
                ["grass"] = (Path.Combine(Overlays, "2.Second Layer", "grassF_2.png"), 12),
                ["rocks"] = (Path.Combine(Overlays, "2.Second Layer", "groundrocksA.png"), 8),
                ["vegetation"] = (Path.Combine(Overlays, "3.Third Layer", "vegetationA.png"), 8),
            };
            var sheets = new Dictionary<string, DecalSheet>();
            foreach (var pair in sources)
            {
                if (!File.Exists(pair.Value.file)) { Debug.LogWarning("Decal source missing, baking without it: " + pair.Value.file); continue; }
                var texture = new Texture2D(2, 2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(pair.Value.file));
                    sheets[pair.Key] = new DecalSheet(PaintedImage.FromColors(texture.GetPixels32(), texture.width, texture.height), pair.Value.minSize);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            return sheets;
        }

        static string Write(string name, PaintedImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(image.ToColors()); texture.Apply();
                string path = Output + "/" + name + ".jpg";
                File.WriteAllBytes(path, texture.EncodeToJPG(92));
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static void Configure(string path, bool repeat)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.isReadable = true; importer.filterMode = FilterMode.Bilinear;
            // Clamped floors: the arena image covers the bounds exactly once.
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            // Uncompressed: block compression smears the dark gradients into visible steps.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
