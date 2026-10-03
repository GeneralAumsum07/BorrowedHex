using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Data;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>EditMode coverage for the arena redesign (spec 2026-10-04).</summary>
    public class ArenaRedesignTests
    {
        static readonly string[] Themes = { "Courtyard", "Graveyard", "Cave", "Sanctum" };

        [Test]
        public void PaintedWorldCompilesAndKeepsThePixelWorldInterface()
        {
            var shader = Shader.Find(PaintedMaterials.ShaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "PaintedWorld has compile errors");
            // WorldGeometry's effect code drives these by name; losing one silently breaks
            // decay, morph, reveal or occlusion with no compile error anywhere.
            foreach (var name in new[] { "_BaseMap", "_FromMap", "_BaseColor", "_Morph", "_Visible", "_Retiring",
                         "_GlitchTime", "_Glitches", "_Reveal", "_Emission", "_FogAmount", "_Occlusion" })
                Assert.That(shader.FindPropertyIndex(name), Is.GreaterThanOrEqualTo(0), name);
            Assert.That(shader.FindPropertyIndex("_NorthLimit"), Is.EqualTo(-1), "The apron clip is retired");
            var material = PaintedMaterials.Create("probe", Texture2D.whiteTexture, Color.white);
            try
            {
                foreach (var pass in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                    Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0), pass);
                Assert.That(material.GetTexture("_FromMap"), Is.SameAs(Texture2D.whiteTexture));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void FloorGenerationIsDeterministic()
        {
            var a = PaintedFloorGenerator.Floor("Graveyard", 96, 72, null);
            var b = PaintedFloorGenerator.Floor("Graveyard", 96, 72, null);
            Assert.That(a.R, Is.EqualTo(b.R)); Assert.That(a.G, Is.EqualTo(b.G)); Assert.That(a.B, Is.EqualTo(b.B));
        }

        [Test]
        public void EveryThemeFloorIsNightDark()
        {
            // Real lighting brightens later; the albedo itself must stay in the nightmare range
            // or the moonlit arena reads as daytime.
            foreach (var theme in Themes)
                Assert.That(PaintedFloorGenerator.Floor(theme, 160, 120, null).MeanLuminance(), Is.InRange(.02f, .30f), theme);
        }

        [Test]
        public void TiledNoiseWrapsExactlyOnItsPeriod()
        {
            for (int i = 0; i < 20; i++)
            {
                float u = i * .37f, v = i * .53f;
                float at = PaintedFloorGenerator.FbmTiled(u, v, 9, 4, 8, 4);
                Assert.That(PaintedFloorGenerator.FbmTiled(u + 8, v, 9, 4, 8, 4), Is.EqualTo(at).Within(1e-5));
                Assert.That(PaintedFloorGenerator.FbmTiled(u, v + 4, 9, 4, 8, 4), Is.EqualTo(at).Within(1e-5));
            }
        }

        [Test]
        public void OuterAndRibbonTexturesTileWithoutVisibleSeams()
        {
            foreach (var theme in Themes)
                foreach (var image in new[] { PaintedFloorGenerator.Outer(theme, 64), PaintedFloorGenerator.Ribbon(theme, 64, 32) })
                {
                    // The wrap pair (last column, first column) must differ no more than an
                    // ordinary neighbouring pair does, or the repeat shows as a line.
                    float seam = 0, interior = 0;
                    for (int y = 0; y < image.Height; y++)
                    {
                        seam += Mathf.Abs(image.Luminance(image.Width - 1, y) - image.Luminance(0, y));
                        interior += Mathf.Abs(image.Luminance(image.Width / 2, y) - image.Luminance(image.Width / 2 - 1, y));
                    }
                    Assert.That(seam, Is.LessThanOrEqualTo(interior * 3 + .05f * image.Height), theme);
                }
        }

        [Test]
        public void DecalsAreFoundAsBlobsAndScatterStaysInsideTheImage()
        {
            var sheet = new PaintedImage(64, 64);
            for (int i = 0; i < sheet.A.Length; i++) sheet.A[i] = 0;
            foreach (var (x0, y0) in new[] { (4, 4), (40, 36) })
                for (int y = y0; y < y0 + 16; y++) for (int x = x0; x < x0 + 16; x++)
                { int i = y * 64 + x; sheet.A[i] = 1; sheet.R[i] = sheet.G[i] = sheet.B[i] = 1; }
            var decals = new DecalSheet(sheet, 8);
            Assert.That(decals.Sprites.Count, Is.EqualTo(2));
            var floor = new PaintedImage(20, 20);
            // Stamps that start off-image must clip, not throw.
            PaintedFloorGenerator.Scatter(floor, decals, 50, 1, 1f, Color.white, 0, 1, 64);
            Assert.That(floor.R.Any(v => v > .5f), Is.True);
        }

        [Test]
        public void BakedFloorsExistAtFortyPixelsPerUnitAndAreReadable()
        {
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage); string theme = arena.worldTheme;
                var floor = Resources.Load<Texture2D>("WorldFloors/" + theme + "Floor");
                Assert.That(floor, Is.Not.Null, theme + "Floor");
                Assert.That(floor.width, Is.EqualTo(Mathf.RoundToInt(arena.bounds.width * PaintedFloorGenerator.PixelsPerUnit)));
                Assert.That(floor.height, Is.EqualTo(Mathf.RoundToInt(arena.bounds.height * PaintedFloorGenerator.PixelsPerUnit)));
                // The interrupted-morph snapshot samples pixels on the CPU.
                Assert.That(floor.isReadable, Is.True);
                Assert.That(floor.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                foreach (var suffix in new[] { "Outer", "Ribbon" })
                {
                    var tile = Resources.Load<Texture2D>("WorldFloors/" + theme + suffix);
                    Assert.That(tile, Is.Not.Null, theme + suffix);
                    Assert.That(tile.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                    Assert.That(tile.isReadable, Is.True);
                }
            }
        }

        [Test]
        public void EveryCatalogModelLoadsFromResources()
        {
            foreach (var name in WorldModelCatalog.Kenney.Concat(WorldModelCatalog.Quaternius))
                Assert.That(Resources.Load<GameObject>(WorldModelCatalog.ResourcePath(name)), Is.Not.Null, name);
            Assert.That(Resources.Load<Texture2D>("WorldModels/Kenney/colormap_night"), Is.Not.Null);
            Assert.That(WorldModelCatalog.Exists(WorldModelCatalog.Crystal), Is.True, "Built in code, never loaded");
        }

        [Test]
        public void SpawnedModelsUsePaintedMaterialsWithAGroundedPivot()
        {
            var parent = new GameObject("probe").transform;
            using (var library = new WorldModelLibrary(Color.white))
                try
                {
                    foreach (var name in new[] { "crypt-small", "Rock_Moss_1", WorldModelCatalog.Crystal })
                    {
                        var model = library.Spawn(name, parent, out var size);
                        Assert.That(model, Is.Not.Null, name);
                        Assert.That(size.y, Is.GreaterThan(.05f), name);
                        var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                        // Bottom-centre pivot: recipes place models ON the ground at a point.
                        Assert.That(bounds.min.y, Is.EqualTo(0).Within(.01f), name);
                        Assert.That(bounds.center.x, Is.EqualTo(0).Within(.05f * Mathf.Max(1, size.x)), name);
                        Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, name);
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                            foreach (var material in renderer.sharedMaterials)
                                Assert.That(material.shader.name, Is.EqualTo(PaintedMaterials.ShaderName), name);
                    }
                }
                finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test]
        public void MissingModelLogsAndSkips()
        {
            var parent = new GameObject("probe").transform;
            using (var library = new WorldModelLibrary(Color.white))
                try
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no-such-model"));
                    Assert.That(library.Spawn("no-such-model", parent, out _), Is.Null);
                }
                finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test]
        public void QuaterniusMaterialNamesMapToNightColours()
        {
            Assert.That(WorldModelLibrary.NightColor("Wood").maxColorComponent, Is.LessThan(.15f));
            Assert.That(WorldModelLibrary.NightColor("Rock (Instance)"), Is.EqualTo(WorldModelLibrary.NightColor("Rock")));
            Assert.That(WorldModelLibrary.NightColor("Something"), Is.EqualTo(new Color(.12f, .12f, .14f)));
        }
    }
}
