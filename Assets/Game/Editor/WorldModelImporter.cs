using System;
using System.IO;
using BorrowedHex.Presentation.WorldArt;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Copies the catalog's FBX subset of two CC0 kits into Resources. Unlike WorldArt these
    /// are committed (spec 3.1, owner decision), so the arenas render on a fresh checkout.
    /// Validates every source before copying anything, like WorldArtImporter, so a partial
    /// download never leaves a half-imported folder.
    /// </summary>
    public static class WorldModelImporter
    {
        const string Output = "Assets/Game/Resources/WorldModels";
        static string Downloads => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "itch_downloads");

        [MenuItem("Borrowed Hex/Art/Import World Models")]
        public static void Import()
        {
            string kenney = Path.Combine(Downloads, "kenney_graveyard-kit_5.0"), quaternius = Path.Combine(Downloads, "Ultimate Nature Pack by Quaternius");
            string Kfbx(string n) => Path.Combine(kenney, "Models", "FBX format", n + ".fbx");
            string Qfbx(string n) => Path.Combine(quaternius, "FBX", n + ".fbx");
            string colormap = Path.Combine(kenney, "Models", "FBX format", "Textures", "colormap.png");
            foreach (var n in WorldModelCatalog.Kenney) Require(Kfbx(n));
            foreach (var n in WorldModelCatalog.Quaternius) Require(Qfbx(n));
            Require(colormap); Require(Path.Combine(kenney, "License.txt")); Require(Path.Combine(quaternius, "License.txt"));

            AssetDatabase.StartAssetEditing();
            try
            {
                Directory.CreateDirectory(Output + "/Kenney"); Directory.CreateDirectory(Output + "/Quaternius");
                foreach (var n in WorldModelCatalog.Kenney) File.Copy(Kfbx(n), Output + "/Kenney/" + n + ".fbx", true);
                foreach (var n in WorldModelCatalog.Quaternius) File.Copy(Qfbx(n), Output + "/Quaternius/" + n + ".fbx", true);
                File.Copy(Path.Combine(kenney, "License.txt"), Output + "/Kenney/License.txt", true);
                File.Copy(Path.Combine(quaternius, "License.txt"), Output + "/Quaternius/License.txt", true);
                // The FBX importer also wants the original colormap beside the models.
                Directory.CreateDirectory(Output + "/Kenney/Textures");
                File.Copy(colormap, Output + "/Kenney/Textures/colormap.png", true);
                BakeNightColormap(colormap, Output + "/Kenney/colormap_night.png");
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Output, "*.fbx", SearchOption.AllDirectories)) ConfigureModel(path.Replace('\\', '/'));
            var night = (TextureImporter)AssetImporter.GetAtPath(Output + "/Kenney/colormap_night.png");
            // Kenney colormaps are flat swatches: point filter, no mips, or neighbours bleed.
            night.filterMode = FilterMode.Point; night.mipmapEnabled = false;
            night.textureCompression = TextureImporterCompression.Uncompressed; night.SaveAndReimport();
            Debug.Log("Imported " + (WorldModelCatalog.Kenney.Length + WorldModelCatalog.Quaternius.Length) + " world models");
        }

        static void Require(string path) { if (!File.Exists(path)) throw new FileNotFoundException("World model source missing", path); }

        // Daytime kit colours pulled into the night palette once, offline: desaturate 35%
        // then multiply by a cold grey-violet. Per-theme tints finish the job at runtime.
        static void BakeNightColormap(string source, string destination)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(source));
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float lum = pixels[i].r * .3f + pixels[i].g * .59f + pixels[i].b * .11f;
                    var c = Color.Lerp(pixels[i], new Color(lum, lum, lum, pixels[i].a), .35f);
                    pixels[i] = new Color(c.r * .55f, c.g * .55f, c.b * .62f, c.a);
                }
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(destination, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            // Material names survive import so WorldModelLibrary can map Quaternius' flat
            // colours (Wood, LightWood, Rock, Green, Berry) to night colours by name.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.SaveAndReimport();
        }
    }
}
