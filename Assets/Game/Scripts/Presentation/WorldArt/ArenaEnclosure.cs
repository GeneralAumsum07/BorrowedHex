using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The visual ring around the play rect (spec 1, 3.3): a continuous ribbon wall that
    /// guarantees the no-void property whatever the props do, plus the kit dressing placed
    /// from ArenaDressing. The ribbon never dissolves; dressing renderers are returned as
    /// Scenery so WorldGeometry can form and retire them with the morph.
    /// </summary>
    public sealed class ArenaEnclosure
    {
        public const string RibbonName = "Enclosure ribbon";
        public readonly List<Renderer> Scenery = new List<Renderer>();

        public ArenaEnclosure(Transform root, ArenaLayout arena, WorldModelLibrary models, Material ribbon, List<Object> owned)
        {
            BuildRibbon(root, arena.bounds, ribbon, owned);
            var dressing = new GameObject("Dressing").transform; dressing.SetParent(root, false);
            foreach (var placement in ArenaDressing.Place(arena))
            {
                var model = models.Spawn(placement.Model, dressing, out var size);
                if (model == null) continue; // the library logged it (spec 4.4)
                model.transform.SetPositionAndRotation(placement.Position, Quaternion.Euler(0, placement.Yaw, 0));
                model.transform.localScale = Vector3.one * (placement.Height / Mathf.Max(.01f, size.y));
                Scenery.AddRange(model.GetComponentsInChildren<Renderer>());
            }
        }

        static void BuildRibbon(Transform root, Rect b, Material material, List<Object> owned)
        {
            float t = ArenaDressing.RibbonThickness;
            float west = b.xMin - ArenaDressing.RibbonInset(DressingBand.West), east = b.xMax + ArenaDressing.RibbonInset(DressingBand.East);
            float south = b.yMin - ArenaDressing.RibbonInset(DressingBand.South), north = b.yMax + ArenaDressing.RibbonInset(DressingBand.North);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            // North and south strips run the full outer width, so they close the four corners.
            Strip(new Vector3(west - t, 0, south), new Vector3(east + t, 0, south), Vector3.back, DressingBand.South);
            Strip(new Vector3(west - t, 0, north), new Vector3(east + t, 0, north), Vector3.forward, DressingBand.North);
            Strip(new Vector3(west, 0, south), new Vector3(west, 0, north), Vector3.left, DressingBand.West);
            Strip(new Vector3(east, 0, south), new Vector3(east, 0, north), Vector3.right, DressingBand.East);
            var mesh = new Mesh { name = RibbonName };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            var value = new GameObject(RibbonName); value.transform.SetParent(root, false);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;

            void Strip(Vector3 from, Vector3 to, Vector3 outward, DressingBand side)
            {
                float length = Vector3.Distance(from, to); int steps = Mathf.Max(1, Mathf.CeilToInt(length));
                int start = vertices.Count;
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector3.Lerp(from, to, i / (float)steps);
                    float jitter = Jitter(side, i);
                    // A ragged face and crest read as rock or ruin, not a ruler-straight wall.
                    p -= outward * (jitter * .3f);
                    float h = ArenaDressing.RibbonHeight(b, side, p.z);
                    // The south crest may only dip: it must stay under the 1.2 limit.
                    h += side == DressingBand.South ? Mathf.Min(0, jitter) * .3f : jitter * .8f;
                    float u = i * length / steps / 8; // the ribbon texture spans 8 x 4 units
                    vertices.Add(p); vertices.Add(p + Vector3.up * h); vertices.Add(p + Vector3.up * h + outward * t);
                    uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, h / 4)); uv.Add(new Vector2(u, (h + t) / 4));
                    if (i == 0) continue;
                    int a = start + (i - 1) * 3, c = start + i * 3;
                    Quad(a, c, a + 1, c + 1, -outward);           // the face toward the arena
                    Quad(a + 1, c + 1, a + 2, c + 2, Vector3.up); // the crest
                }
            }

            void Quad(int p00, int p10, int p01, int p11, Vector3 facing) { Tri(p00, p01, p10, facing); Tri(p10, p01, p11, facing); }

            void Tri(int i0, int i1, int i2, Vector3 facing)
            {
                // PaintedWorld culls back faces. Unity draws a triangle whose
                // Cross(v1 - v0, v2 - v0) points at the viewer, so each triangle is ordered
                // against its intended facing rather than trusting the loop's winding.
                var normal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
                if (Vector3.Dot(normal, facing) >= 0) triangles.AddRange(new[] { i0, i1, i2 });
                else triangles.AddRange(new[] { i0, i2, i1 });
            }
        }

        static float Jitter(DressingBand side, int i)
            => Mathf.Repeat(Mathf.Sin(i * 12.9898f + (int)side * 78.233f) * 43758.5453f, 1) - .5f;
    }
}
