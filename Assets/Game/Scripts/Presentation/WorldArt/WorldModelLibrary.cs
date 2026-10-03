using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Instantiates kit models with PaintedWorld materials. Materials are cached per source
    /// (one for all Kenney models, one per Quaternius colour) so a few hundred props share a
    /// handful of materials; WorldGeometry's property blocks still vary them per renderer.
    /// Everything created here is owned and released on Dispose, as WorldGeometry does today.
    /// </summary>
    public sealed class WorldModelLibrary : IDisposable
    {
        readonly Color kitTint;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Mesh crystal;

        public WorldModelLibrary(Color kitTint) => this.kitTint = kitTint;

        // Quaternius models carry flat colours named by material; these are their night
        // versions. Prefix match because Unity may append " (Instance)" or a numeric suffix.
        static readonly (string name, Color color)[] Night =
        {
            ("LightWood", new Color(.13f, .09f, .08f)), ("Wood", new Color(.09f, .06f, .06f)),
            ("Rock", new Color(.17f, .17f, .21f)), ("DarkGreen", new Color(.04f, .06f, .04f)),
            ("Green", new Color(.07f, .10f, .06f)), ("Black", new Color(.03f, .03f, .03f)),
            ("White", new Color(.35f, .36f, .34f)), ("Leaves", new Color(.10f, .08f, .06f)),
            ("Berry", new Color(.16f, .04f, .06f)),
        };

        public static Color NightColor(string materialName)
        {
            foreach (var (name, color) in Night)
                if (materialName.StartsWith(name, StringComparison.Ordinal)) return color;
            return new Color(.12f, .12f, .14f);
        }

        public GameObject Spawn(string model, Transform parent, out Vector3 size)
        {
            size = Vector3.zero;
            GameObject instance;
            if (model == WorldModelCatalog.Crystal) instance = BuildCrystal();
            else
            {
                var prefab = Resources.Load<GameObject>(WorldModelCatalog.ResourcePath(model));
                if (prefab == null)
                {
                    // Spec 4.4: name it loudly, skip the placement, never throw mid-build.
                    Debug.LogError("World model missing from Resources/" + WorldModelCatalog.ResourcePath(model) + ": " + model);
                    return null;
                }
                instance = UnityEngine.Object.Instantiate(prefab);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) WorldArtLibrary.Release(collider);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) Paint(renderer, model);
            }
            instance.name = "Mesh";
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }

            // Measure at the identity transform, then offset the mesh inside a wrapper so the
            // wrapper's origin is the bottom-centre of the model (recipes place on the ground).
            var wrapper = new GameObject(model);
            instance.transform.SetParent(wrapper.transform, false);
            var bounds = Bounds(instance);
            instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            size = bounds.size;
            wrapper.transform.SetParent(parent, false);
            return wrapper;
        }

        static Bounds Bounds(GameObject value)
        {
            var renderers = value.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(value.transform.position, Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        void Paint(Renderer renderer, string model)
        {
            var source = renderer.sharedMaterials; var painted = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (WorldModelCatalog.IsKenney(model)) painted[i] = Cached("Kenney", () =>
                    PaintedMaterials.Create("Kenney night", Resources.Load<Texture2D>("WorldModels/Kenney/colormap_night"), kitTint));
                else
                {
                    var color = NightColor(source[i] != null ? source[i].name : "");
                    painted[i] = Cached("Q" + ColorUtility.ToHtmlStringRGB(color), () =>
                        PaintedMaterials.Create("Quaternius night", Texture2D.whiteTexture, color));
                }
            }
            renderer.sharedMaterials = painted;
        }

        Material Cached(string key, Func<Material> create)
        {
            if (materials.TryGetValue(key, out var material)) return material;
            material = create(); owned.Add(material);
            return materials[key] = material;
        }

        // Neither kit has a crystal (spec 3.4): five tapered hexagonal prisms leaning outward,
        // emissive cold blue so the Cave keeps its glow under very dark ambient.
        GameObject BuildCrystal()
        {
            if (crystal == null)
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                var random = new System.Random(17);
                for (int shard = 0; shard < 5; shard++)
                {
                    float height = .9f + (float)random.NextDouble() * .9f, radius = .14f + (float)random.NextDouble() * .1f;
                    var tilt = Quaternion.Euler((float)random.NextDouble() * 30 - 15, shard * 72, (float)random.NextDouble() * 30 - 15);
                    var foot = new Vector3(Mathf.Cos(shard * 1.26f), 0, Mathf.Sin(shard * 1.26f)) * (shard == 0 ? 0 : .25f);
                    int start = vertices.Count;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3;
                        vertices.Add(foot + tilt * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                        vertices.Add(foot + tilt * new Vector3(Mathf.Cos(a) * radius * .55f, height * .8f, Mathf.Sin(a) * radius * .55f));
                    }
                    vertices.Add(foot + tilt * new Vector3(0, height, 0));
                    for (int i = 0; i < 6; i++)
                    {
                        int b0 = start + i * 2, t0 = b0 + 1, b1 = start + (i + 1) % 6 * 2, t1 = b1 + 1, tip = start + 12;
                        triangles.AddRange(new[] { b0, t0, b1, b1, t0, t1, t0, tip, t1 });
                    }
                }
                crystal = new Mesh { name = "Crystal cluster" };
                crystal.SetVertices(vertices); crystal.SetTriangles(triangles, 0); crystal.RecalculateNormals(); crystal.RecalculateBounds();
                owned.Add(crystal);
            }
            var value = new GameObject("Crystal");
            value.AddComponent<MeshFilter>().sharedMesh = crystal;
            var material = Cached("Crystal", () =>
            {
                var m = PaintedMaterials.Create("Crystal", Texture2D.whiteTexture, new Color(.35f, .55f, .9f));
                m.SetColor("_Emission", new Color(.15f, .3f, .6f)); return m;
            });
            value.AddComponent<MeshRenderer>().sharedMaterial = material;
            return value;
        }

        public void Dispose() { foreach (var item in owned) WorldArtLibrary.Release(item); owned.Clear(); materials.Clear(); crystal = null; }
    }
}
