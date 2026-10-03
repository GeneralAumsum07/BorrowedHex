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
        public const float OuterMargin = 40, OuterTile = 8;
        public const int SnapshotCap = 512;
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

        /// The shader's morph patch hash, Hash(float3(floor(uv*18), 5)), on the CPU.
        public static float PatchNoise(float u, float v)
            => Mathf.Repeat(Mathf.Sin(Mathf.Floor(u * 18) * 127.1f + Mathf.Floor(v * 18) * 311.7f + 5 * 74.7f) * 43758.5453f, 1);

        /// <summary>
        /// What the material shows right now, as a texture the next morph can start from.
        /// A finished morph (the usual case) is just its _BaseMap: no CPU work and no new
        /// texture (spec 2.2). Only an interrupted morph bakes, capped at `cap` px wide.
        /// When `baked` is false the result is a shared asset the caller must NOT release.
        /// </summary>
        public static Texture2D Snapshot(Material material, int cap, out bool baked)
        {
            baked = false;
            var target = material.GetTexture("_BaseMap") as Texture2D; var source = material.GetTexture("_FromMap") as Texture2D;
            float amount = material.GetFloat("_Morph");
            if (amount >= 1 || source == null || source == target || target == null) return target;
            int width = Mathf.Min(cap, target.width), height = Mathf.Max(1, Mathf.RoundToInt(width * (float)target.height / target.width));
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width, v = (y + .5f) / height;
                float mix = Mathf.Clamp01(amount * 1.65f - PatchNoise(u, v) * .65f);
                pixels[y * width + x] = Color.Lerp(source.GetPixelBilinear(u, v), target.GetPixelBilinear(u, v), mix);
            }
            var snapshot = new Texture2D(width, height, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = target.wrapMode };
            snapshot.SetPixels32(pixels); snapshot.Apply();
            baked = true; return snapshot;
        }
    }
}
