using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The arena floor (one painted quad covering the bounds once, amendment A1) and the
    /// darker outer ground that runs 40 units past it under the enclosure, the spec's
    /// safety net: any gap between props reads as dark ground, never as void.
    /// </summary>
    public sealed class PaintedFloor
    {
        // 80, not 40: at the 30-degree pitch the travel camera's 2-unit rise lets the top
        // corners of a 21:9 view clear the side walls near the north corners, and those rays
        // only meet the ground ~60-70 units past the bounds. The quad is one draw either way.
        public const float OuterMargin = 80, OuterTile = 8;
        public Material Floor { get; }
        public Material Outer { get; }

        public PaintedFloor(Transform root, ArenaLayout arena, string theme, List<Object> owned)
        {
            var b = arena.bounds;
            Floor = PaintedMaterials.Create("Floor", Texture(theme, "Floor"), Color.white); owned.Add(Floor);
            Quad("Floor", root, new Vector3(b.center.x, 0, b.center.y), b.size, Floor);
            var outerSize = b.size + Vector2.one * OuterMargin * 2;
            Outer = PaintedMaterials.Create("Outer ground", Texture(theme, "Outer"), Color.white); owned.Add(Outer);
            // _FromMap shares the _BaseMap UVs in the shader, so the morph tiles both alike.
            Outer.SetTextureScale("_BaseMap", outerSize / OuterTile);
            // 2 cm under the floor: no z-fighting at the seam, invisible at this camera height.
            Quad("Outer ground", root, new Vector3(b.center.x, -.02f, b.center.y), outerSize, Outer);
        }

        public static Texture2D Texture(string theme, string suffix)
        {
            string path = "WorldFloors/" + ArenaDressing.Known(theme) + suffix;
            var texture = Resources.Load<Texture2D>(path);
            if (texture != null) return texture;
            Debug.LogError("Painted texture missing: Resources/" + path + " (run Borrowed Hex/Art/Bake Painted Floors)");
            return Texture2D.grayTexture;
        }

        static void Quad(string name, Transform root, Vector3 at, Vector2 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Quad); value.name = name;
            var collider = value.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            value.transform.SetParent(root, false);
            // Lying flat and facing up: texture row 0 (the baker's bottom row) lands on the south edge.
            value.transform.SetPositionAndRotation(at, Quaternion.Euler(90, 0, 0));
            value.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = value.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }
    }
}
