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
    }
}
