using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Rebuilds the git-ignored UI art library from the owner's downloads (Downloads/UI_elements).
    /// Same contract as WorldArtImporter: validate every input before touching anything, import
    /// through Unity, never hand-edit .meta YAML. Sprites are NOT sliced here: UiSkin cuts them at
    /// runtime with Sprite.Create (as WorldArtLibrary does), so the rects live in one committed,
    /// unit-tested table and this importer only has to make the textures pixel-exact.
    /// </summary>
    public static class UiArtImporter
    {
        public const string Destination = "Assets/Game/Resources/UiArt";
        const string DarkAges = "DarkAgesUi_v1.0/DarkAgesUi_v1.0/";
        const string Dwellers = "20251126darkDwellers_v_1_0/";

        [MenuItem("Borrowed Hex/Art/Import UI Downloads")]
        public static void ImportDefault() => Import(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads/UI_elements"));

        /// <summary>Destination file name -> path under the downloads folder.</summary>
        public static Dictionary<string, string> Sources() => new Dictionary<string, string>
        {
            // Hypnobius, Dark Ages UI: commercial use and modification allowed, credit optional,
            // no redistribution or resale of the asset itself (LICENSE.txt). Git-ignored for that reason.
            ["DarkAges.png"] = DarkAges + "32x32-Tilesheet.png",
            // Gabriel "tiopalada" Lima, Tiny RPG - Dark Dwellers: CC0 1.0. Copied raw, then gold-mapped.
            ["DwTabRaw.png"] = Dwellers + "20251117darkDwellersTabA1-Sheet.png",
            ["DwPointerRaw.png"] = Dwellers + "20251118darkDwellersHorizontalCursourA1-Sheet.png",
            ["DwCloseRaw.png"] = Dwellers + "20251125closeButton1-Sheet.png",
            ["DwOptionsRaw.png"] = Dwellers + "20251125optionsButton1-Sheet.png",
            ["DwLeftRaw.png"] = Dwellers + "20251125leftArrowButton1-Sheet.png",
            ["DwRightRaw.png"] = Dwellers + "20251125rightArrowButton1-Sheet.png",
            ["DwUpRaw.png"] = Dwellers + "20251125upArrowButton1-Sheet.png",
            ["DwDownRaw.png"] = Dwellers + "20251125downArrowButton1-Sheet.png",
            ["DwPortraitRaw.png"] = Dwellers + "20251125portraitFrameA.png",
            ["DwMouseRaw.png"] = Dwellers + "20251124mouseSmall1-Sheet.png",
            // Hewett Tsoi, alagard. Licence TBD (plan Q1): git-ignored until confirmed.
            ["alagard.ttf"] = "alagard/alagard.ttf",
            // Daniel Linssen, m6x11plus: the UI body face (plan Q2, chosen by the owner over m5x7,
            // which stays the world-callout face). Kept local with the other downloaded fonts.
            ["m6x11plus.ttf"] = "m6x11plus.ttf",
        };

        /// <summary>Gold-mapped sheet -> the copied raw sheet it is made from.</summary>
        public static Dictionary<string, string> GoldMaps() => new Dictionary<string, string>
        {
            ["DwTab.png"] = "DwTabRaw.png", ["DwPointer.png"] = "DwPointerRaw.png",
            ["DwClose.png"] = "DwCloseRaw.png", ["DwOptions.png"] = "DwOptionsRaw.png",
            ["DwLeft.png"] = "DwLeftRaw.png", ["DwRight.png"] = "DwRightRaw.png",
            ["DwUp.png"] = "DwUpRaw.png", ["DwDown.png"] = "DwDownRaw.png",
            ["DwPortrait.png"] = "DwPortraitRaw.png", ["DwMouse.png"] = "DwMouseRaw.png",
        };

        /// <summary>
        /// Ink, Camel, Honey Gold, ivory. The middle two are Dark Ages' own published swatches
        /// (#C19149, #DCC47C) so mapped pieces sit next to its frames without a seam in tone.
        /// </summary>
        public static readonly Color32[] Ramp =
        {
            new Color32(0x15, 0x12, 0x1C, 255), new Color32(0xC1, 0x91, 0x49, 255),
            new Color32(0xDC, 0xC4, 0x7C, 255), new Color32(0xF2, 0xE8, 0xC9, 255),
        };
        // Where Camel and Honey sit on the 0..1 brightness axis. Camel low enough that a mid
        // bevel reads gold rather than mud; tuned by eye on the tab and pointer sheets.
        static readonly float[] Stops = { 0f, 0.45f, 0.75f, 1f };

        static float Luma(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>
        /// Repaint opaque pixels onto <see cref="Ramp"/> by their brightness, normalised over the
        /// sheet's own opaque range (a dark sheet would otherwise map entirely to ink). Alpha is kept.
        /// Why a map and not WorldArtImporter's hue shift: Dark Dwellers is saturated violet, and
        /// rotating a saturated hue only gives another saturated colour, never antique gold. The
        /// map throws the hue away and keeps relative brightness, so bevels and shading survive.
        /// </summary>
        public static void GoldMap(Color32[] px)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in px) if (c.a > 0) { float l = Luma(c); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            float span = hi - lo;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                // A flat sheet has no brightness range to spread: park it mid-ramp, never divide by ~0.
                float t = span < 1e-3f ? 0.5f : (Luma(px[i]) - lo) / span;
                int k = t >= Stops[2] ? 2 : t >= Stops[1] ? 1 : 0;
                float u = (t - Stops[k]) / (Stops[k + 1] - Stops[k]);
                var c = (Color32)Color.Lerp(Ramp[k], Ramp[k + 1], u);
                c.a = px[i].a;
                px[i] = c;
            }
        }

        public static int Import(string source)
        {
            var files = Sources();
            // Validate everything first: a half-imported library is worse than none.
            foreach (var pair in files)
                if (!File.Exists(Path.Combine(source, pair.Value))) throw new FileNotFoundException(pair.Value);
            Directory.CreateDirectory(Destination);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in files) File.Copy(Path.Combine(source, pair.Value), Destination + "/" + pair.Key, true);
                foreach (var pair in GoldMaps())
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        tex.LoadImage(File.ReadAllBytes(Destination + "/" + pair.Value));
                        var px = tex.GetPixels32();
                        GoldMap(px);
                        tex.SetPixels32(px);
                        File.WriteAllBytes(Destination + "/" + pair.Key, tex.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tex); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var pngs = new List<string>(GoldMaps().Keys) { "DarkAges.png" };
            foreach (var name in pngs)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Destination + "/" + name);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;          // pixel art: never bilinear
                importer.mipmapEnabled = false;                  // UI is never minified on purpose
                importer.npotScale = TextureImporterNPOTScale.None; // 384x352 must stay 384x352
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;       // sliced edges must not bleed
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            // Raw Dwellers sheets were only inputs to the gold map; delete them so nothing loads
            // the off-palette originals by mistake.
            foreach (var raw in GoldMaps().Values) AssetDatabase.DeleteAsset(Destination + "/" + raw);
            return files.Count;
        }
    }
}
