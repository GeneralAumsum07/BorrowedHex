using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Solid architecture receives native atlas pixels. Ordinary cover is full-size
    /// cutout artwork; analytic collision stays in ArenaSim. Pixel coverage replaces
    /// growth and shatter animations throughout the live world transformation.
    /// </summary>
    public sealed class WorldGeometry : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Transform root;
        readonly WorldArtLibrary art;
        readonly ArenaLayout layout;
        readonly Dictionary<string, Material> surfaces = new Dictionary<string, Material>();
        readonly List<GameObject> cover = new List<GameObject>(), rubble = new List<GameObject>();
        readonly List<Renderer> boundary = new List<Renderer>(), scenery = new List<Renderer>();
        readonly Dictionary<Renderer, float> sceneryBase = new Dictionary<Renderer, float>();
        readonly Dictionary<GameObject, float> coverBase = new Dictionary<GameObject, float>();
        readonly Dictionary<string, Renderer> backgrounds = new Dictionary<string, Renderer>();
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        bool morphing, retiring;
        float sceneryAmount = 1;
        public string CurrentTheme { get; private set; }
        public Material Stone => surfaces["Wall"];
        public Texture2D FloorTexture => (Texture2D)surfaces["Floor"].GetTexture("_BaseMap");
        public Texture2D FloorFrom => (Texture2D)surfaces["Floor"].GetTexture("_FromMap");

        public WorldGeometry(Transform parent, ArenaLayout arena, WorldArtLibrary library, Material unusedTemplate)
        {
            art = library; layout = arena; CurrentTheme = arena.worldTheme;
            root = Group("Environment", parent).transform;
            foreach (string key in new[] { "Floor", "Wall", "Detail" })
            {
                var texture = WorldPixelSurfaces.Create(art, CurrentTheme, key != "Floor"); owned.Add(texture);
                surfaces[key] = Surface(key, texture);
            }
            var b = arena.bounds;
            Box("Floor", root, new Vector3(b.center.x, -.25f, b.center.y), new Vector3(b.width + 2, .5f, b.height + 2), surfaces["Floor"]);
            surfaces["Floor"].SetTextureScale("_BaseMap", Vector2.one * 2);
            Box("Foundation", root, new Vector3(b.center.x, -.6f, b.center.y), new Vector3(b.width + 2.6f, .35f, b.height + 2.6f), Stone);
            int stage = CurrentTheme == "Courtyard" ? 0 : CurrentTheme == "Graveyard" ? 1 : CurrentTheme == "Cave" ? 2 : 3;
            // Missing segments change the actual enclosing structure, not just its tint.
            for (int side = 0; side < 4; side++) for (int i = 0; i < 12; i++)
            {
                float height = WorldBoundaryPolicy.Height(stage, i + side * 2);
                if (height <= 0) continue;
                if (side == 0) height = Mathf.Min(height, 1.1f);
                bool horizontal = side < 2;
                float length = (horizontal ? b.width : b.height) / 12;
                Vector3 at = horizontal ? new Vector3(b.xMin + length * (i + .5f), height / 2, side == 0 ? b.yMin : b.yMax)
                    : new Vector3(side == 2 ? b.xMin : b.xMax, height / 2, b.yMin + length * (i + .5f));
                var wall = Box("Boundary" + side + "_" + i, root, at, horizontal ? new Vector3(length + .015f, height, .7f) : new Vector3(.7f, height, length + .015f), Stone);
                boundary.Add(wall.GetComponent<Renderer>());
                var coping = Box("Broken coping", root, at + Vector3.up * (height / 2 + .065f), horizontal ? new Vector3(length, .13f, .8f) : new Vector3(.8f, .13f, length), surfaces["Detail"]);
                boundary.Add(coping.GetComponent<Renderer>());
            }
            if (stage == 0 || stage == 3)
                for (float x = b.xMin + 2; x < b.xMax; x += 5) Arch(new Vector3(x, 0, b.yMax + 2), 2.8f, 2.8f);
            for (int i = 0; i < arena.pillars.Count; i++) BuildCover(i);
            // Breaches expose the downloaded backgrounds behind the combat footprint.
            // The downloaded layers are composed for a side view. Sink their base below
            // the horizon so the shallow camera sees the actual skyline, not only the
            // bottom of a giant sprite above its frustum. The terrain apron stops here.
            Background("Sky", "GraveSky", new Vector3(b.center.x, -33, b.yMax + 6), 100);
            Background("Horizon", stage == 2 || stage == 3 ? "Temple" : "GraveSilhouette", new Vector3(b.center.x, -16, b.yMax + 3), 65);
            for (int i = 0; i < 12; i++)
            {
                float sign = i % 2 == 0 ? -1 : 1;
                var at = new Vector3(b.center.x + sign * (b.width / 2 + 3 + i % 3), 0, b.yMin + i * b.height / 12);
                var prop = Prop(stage == 1 ? "Ruin" : stage == 2 ? "RockTree" : "DeadTree", root, at, 3.5f);
                if (prop != null) scenery.Add(prop);
            }
            if (stage == 3)
            {
                var glyph = art.Effect("Vortex", 100);
                if (glyph.Length > 0)
                {
                    var ring = Group("Ritual seal", root); ring.transform.position = Geometry2D.ToWorld(b.center, .025f);
                    ring.transform.rotation = Quaternion.Euler(90, 0, 0); ring.transform.localScale = Vector3.one * 19;
                    var renderer = ring.AddComponent<SpriteRenderer>(); renderer.sprite = glyph[0];
                    renderer.color = new Color(.38f, .55f, .8f, .3f);
                }
            }
        }

        void BuildCover(int index)
        {
            var p = layout.pillars[index];
            var group = Group("CoverTrim" + index, root); group.transform.position = Geometry2D.ToWorld(p.center);
            var kind = index < layout.propKinds.Count ? layout.propKinds[index] : DecayPropKind.Column;
            if (kind == DecayPropKind.Obelisk)
            {
                Box("Base", group.transform, Geometry2D.ToWorld(p.center, .12f), new Vector3(p.width, .24f, p.height), Stone);
                Box("Obelisk", group.transform, Geometry2D.ToWorld(p.center, 1.35f), new Vector3(p.width * .8f, 2.7f, p.height * .8f), Stone);
                Box("Capital", group.transform, Geometry2D.ToWorld(p.center, 2.7f), new Vector3(p.width, .2f, p.height), surfaces["Detail"]);
                var crown = Box("Ritual crown", group.transform, Geometry2D.ToWorld(p.center, 3), new Vector3(.7f, .5f, .7f), surfaces["Detail"]);
                crown.transform.rotation = Quaternion.Euler(0, 45, 0);
                Prop("RunePillar", group.transform, Geometry2D.ToWorld(p.center + new Vector2(0, -.6f), .2f), p.width * .8f);
            }
            else
            {
                string asset = kind == DecayPropKind.Tomb ? "Tomb" : kind == DecayPropKind.Urn ? "Urn"
                    : kind == DecayPropKind.Crystal ? "Crystal" : kind == DecayPropKind.Rock ? "Rock"
                    : kind == DecayPropKind.DeadTree ? "RockTree" : kind == DecayPropKind.Column ? "RunePillar" : "Ruin";
                Prop(asset, group.transform, Geometry2D.ToWorld(p.center), p.width);
            }
            cover.Add(group);
            var debris = Group("CoverRubble" + index, root);
            Prop(kind == DecayPropKind.Tomb ? "TombRubble" : "Rock", debris.transform, Geometry2D.ToWorld(p.center, .01f), p.width * .7f);
            debris.SetActive(false); rubble.Add(debris);
        }

        static GameObject Group(string name, Transform parent)
        { var value = new GameObject(name); value.transform.SetParent(parent, false); return value; }

        Material Surface(string name, Texture2D texture)
        {
            var shader = Shader.Find("BorrowedHex/PixelWorld");
            var material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetTexture("_BaseMap", texture); material.SetTexture("_FromMap", texture);
            material.SetColor("_BaseColor", Color.white); owned.Add(material); return material;
        }

        Renderer Prop(string name, Transform parent, Vector3 at, float width)
        {
            var sprite = art.Prop(name); if (sprite == null) return null;
            var value = Group(name, parent); value.transform.position = at;
            value.transform.rotation = WorldCameraPolicy.Rotation();
            value.transform.localScale = Vector3.one * (width / Mathf.Max(.1f, sprite.bounds.size.x));
            var renderer = value.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = Surface(name, sprite.texture); return renderer;
        }

        void Background(string name, string asset, Vector3 at, float width)
        {
            var renderer = Prop(asset, root, at, width); if (renderer == null) return;
            renderer.name = name; backgrounds[name] = renderer;
            renderer.sharedMaterial.SetFloat("_FogAmount", name == "Sky" ? .25f : .55f);
            if (name == "Sky" && CurrentTheme == "Cave")
            {
                // Keep an opaque sky behind the ruined Temple layer's transparent areas.
                var sky = art.Texture("GraveSky"); var ruins = art.Texture("TempleDark");
                if (sky != null && ruins != null)
                {
                    var pixels = sky.GetPixels();
                    for (int y = 0; y < sky.height; y++) for (int x = 0; x < sky.width; x++)
                    {
                        var ruin = art.Pixels("TempleDark")[y * ruins.height / sky.height * ruins.width + x * ruins.width / sky.width];
                        var dark = pixels[y * sky.width + x] * new Color(.45f, .42f, .62f);
                        pixels[y * sky.width + x] = Color.Lerp(dark, ruin, ruin.a / 255f);
                    }
                    var texture = new Texture2D(sky.width, sky.height, TextureFormat.RGBA32, false) {filterMode = FilterMode.Point};
                    texture.SetPixels(pixels); texture.Apply(); owned.Add(texture);
                    renderer.sharedMaterial.SetTexture("_BaseMap", texture); renderer.sharedMaterial.SetTexture("_FromMap", texture);
                }
            }
        }

        GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            value.transform.SetParent(parent, false); value.transform.position = at; value.transform.localScale = size;
            var collider = value.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            var renderer = value.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var filter = value.GetComponent<MeshFilter>(); var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            for (int i = 0; i < uv.Length && name != "Floor"; i++)
            {
                var p = Vector3.Scale(vertices[i], size);
                uv[i] = Mathf.Abs(normals[i].y) > .5f ? new Vector2(p.x, p.z) / 3
                    : Mathf.Abs(normals[i].x) > .5f ? new Vector2(p.z, p.y) / 3 : new Vector2(p.x, p.y) / 3;
            }
            mesh.uv = uv; filter.sharedMesh = mesh; owned.Add(mesh); return value;
        }

        void Arch(Vector3 at, float width, float height)
        {
            foreach (float sign in new[] { -1f, 1f })
                boundary.Add(Box("Arch pier", root, at + new Vector3(sign * width / 2, height / 2, 0), new Vector3(.55f, height, .85f), Stone).GetComponent<Renderer>());
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI / 6;
                var block = Box("Arch stone", root, at + new Vector3(Mathf.Cos(a) * width / 2, height + Mathf.Sin(a) * width / 2, 0), new Vector3(.72f, .5f, .85f), Stone);
                block.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90); boundary.Add(block.GetComponent<Renderer>());
            }
        }

        public static Vector3 FlameAnchor(ArenaLayout arena, int i)
        {
            if (arena.worldTheme == "Sanctum" && i < arena.pillars.Count) return Geometry2D.ToWorld(arena.pillars[i].center, 3.1f);
            var b = arena.bounds;
            return new Vector3(b.center.x + (i % 2 == 0 ? -1 : 1) * (b.width / 2 + .45f), 1.4f, b.yMin + 1 + i * b.height / 8);
        }

        public void SetTheme(string value) => CurrentTheme = value;

        void Visibility(Renderer renderer, float amount, bool old = false, Color? emission = null)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Visible", Mathf.Clamp01(amount)); properties.SetFloat("_Retiring", old ? 1 : 0);
            if (emission.HasValue) properties.SetColor("_Emission", emission.Value);
            renderer.SetPropertyBlock(properties); properties.Clear();
        }

        void Visibility(GameObject value, float amount, bool old = false)
        { foreach (var renderer in value.GetComponentsInChildren<Renderer>(true)) Visibility(renderer, amount, old); }

        public void RenderCover(ArenaSim sim)
        {
            for (int i = 0; i < cover.Count; i++)
            {
                var state = sim.Pillars[i]; bool broken = state.Crumbled;
                float visible = sim.CoverFormed(i) ? Mathf.Clamp01(1 - (float)(sim.Clock.Now - state.RestoredAt) / (state.MaxDurability * state.DecayInterval)) : sim.CoverFormation(i);
                cover[i].SetActive(!broken); Visibility(cover[i], broken ? 0 : visible);
                coverBase[cover[i]] = broken ? 0 : visible;
                rubble[i].SetActive(broken); // static remains; no shatter/growth animation
            }
        }

        public void RenderRetiring(ArenaSim sim)
        {
            for (int i = 0; i < cover.Count; i++)
            {
                int oldIndex = sim.RetiringCover.FindIndex(value => value.Bounds == layout.pillars[i]);
                float visible = oldIndex < 0 ? 0 : sim.RetiringCoverVisibility(oldIndex);
                // Retirement starts from the displayed state, so partially decayed cover
                // never fills itself back in when the next arena starts replacing it.
                visible *= coverBase.TryGetValue(cover[i], out float baseline) ? baseline : 1;
                cover[i].SetActive(visible > 0); Visibility(cover[i], visible, true);
                rubble[i].SetActive(false);
            }
        }

        Texture2D Snapshot(Material material)
        {
            var target = (Texture2D)material.GetTexture("_BaseMap"); var source = (Texture2D)material.GetTexture("_FromMap");
            float amount = material.GetFloat("_Morph"); var next = target.GetPixels32();
            var old = source != null ? source.GetPixels32() : next;
            int width = target.width, height = target.height;
            for (int i = 0; i < next.Length; i++)
            {
                int x = i % width, y = i / width;
                int j = source != null ? y * source.height / height * source.width + x * source.width / width : i;
                float noise = Mathf.Repeat(Mathf.Sin(Mathf.Floor(x * 18f / width) * 127.1f + Mathf.Floor(y * 18f / height) * 311.7f + 5 * 74.7f) * 43758.5453f, 1);
                next[i] = Color32.Lerp(old[j], next[i], Mathf.Clamp01(amount * 1.65f - noise * .65f));
            }
            var snapshot = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            snapshot.SetPixels32(next); snapshot.Apply(); owned.Add(snapshot); return snapshot;
        }

        public void BeginMorphFrom(WorldGeometry previous)
        {
            morphing = true; previous.retiring = true;
            foreach (var pair in surfaces)
            {
                var snapshot = Snapshot(previous.surfaces[pair.Key]); pair.Value.SetTexture("_FromMap", snapshot); pair.Value.SetFloat("_Morph", 0);
                previous.surfaces[pair.Key].SetTexture("_FromMap", snapshot);
                previous.surfaces[pair.Key].SetTexture("_BaseMap", pair.Value.GetTexture("_BaseMap"));
            }
            foreach (var pair in backgrounds)
                if (previous.backgrounds.TryGetValue(pair.Key, out var before))
                {
                    pair.Value.sharedMaterial.SetTexture("_FromMap", Snapshot(before.sharedMaterial));
                    pair.Value.sharedMaterial.SetFloat("_Morph", 0); before.gameObject.SetActive(false);
                }
            foreach (var renderer in previous.scenery) previous.sceneryBase[renderer] = previous.sceneryAmount;
            previous.root.Find("Floor").gameObject.SetActive(false); previous.root.Find("Foundation").gameObject.SetActive(false);
        }

        public void RenderMorph(float amount, double now, bool reduceFlashes)
        {
            foreach (var pair in surfaces)
            {
                pair.Value.SetFloat("_Morph", morphing || retiring ? amount : 1);
                pair.Value.SetFloat("_GlitchTime", (float)now); pair.Value.SetFloat("_Glitches", reduceFlashes ? 0 : 1);
            }
            foreach (var renderer in backgrounds.Values)
            {
                renderer.sharedMaterial.SetFloat("_Morph", morphing ? amount : 1);
                renderer.sharedMaterial.SetFloat("_GlitchTime", (float)now);
                renderer.sharedMaterial.SetFloat("_Glitches", reduceFlashes ? 0 : 1);
            }
            foreach (var renderer in boundary) Visibility(renderer, retiring ? 1 - amount : morphing ? amount : 1, retiring);
        }

        public void RenderSceneryFormation(float amount)
        {
            sceneryAmount = amount;
            foreach (var renderer in scenery)
                Visibility(renderer, amount * (sceneryBase.TryGetValue(renderer, out float start) ? start : 1), retiring);
        }

        public void RenderReveal(int lit, float opening)
        {
            float room = Mathf.Lerp(.025f + lit * .035f, 1, opening);
            foreach (var material in surfaces.Values) material.SetFloat("_Reveal", room);
            foreach (var renderer in scenery) Visibility(renderer, opening);
            foreach (var renderer in backgrounds.Values)
            {
                renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", room);
                renderer.SetPropertyBlock(properties); properties.Clear();
            }
            for (int i = 0; i < cover.Count; i++)
                foreach (var renderer in cover[i].GetComponentsInChildren<Renderer>())
                {
                    renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", i < lit ? 1 : .015f);
                    properties.SetColor("_Emission", i < lit ? new Color(.035f, .12f, .2f) : Color.black);
                    renderer.SetPropertyBlock(properties); properties.Clear();
                }
        }

        public void FinishReveal()
        {
            foreach (var material in surfaces.Values) material.SetFloat("_Reveal", 1);
            foreach (var group in cover) foreach (var renderer in group.GetComponentsInChildren<Renderer>())
            { renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", 1); renderer.SetPropertyBlock(properties); properties.Clear(); }
            RenderSceneryFormation(1);
            foreach (var renderer in backgrounds.Values)
            { renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", 1); renderer.SetPropertyBlock(properties); properties.Clear(); }
        }

        public void RenderActorOcclusion(Camera camera, Vector3 player, Vector3? boss)
        {
            if (camera == null) return;
            bool Behind(Renderer renderer, Vector3 actor)
            {
                var delta = actor + Vector3.up * 1.2f - camera.transform.position;
                return renderer.bounds.IntersectRay(new Ray(camera.transform.position, delta.normalized), out float distance)
                    && distance < delta.magnitude - .15f;
            }
            foreach (var group in cover)
            {
                var renderers = group.GetComponentsInChildren<Renderer>();
                bool blocks = false;
                foreach (var renderer in renderers)
                    if (Behind(renderer, player) || boss.HasValue && Behind(renderer, boss.Value)) { blocks = true; break; }
                // The low camera can put a retained pillar directly over the Collector
                // or player. Pixel cutout keeps their silhouettes readable while retaining
                // the pillar's geometry and collision; this is unrelated to world decay.
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(properties); properties.SetFloat("_Occlusion", blocks ? .12f : 1);
                    renderer.SetPropertyBlock(properties); properties.Clear();
                }
            }
        }

        public void Dispose()
        { WorldArtLibrary.Release(root.gameObject); foreach (var item in owned) WorldArtLibrary.Release(item); owned.Clear(); }
        public void SetVisible(bool value) => root.gameObject.SetActive(value);
    }
}
