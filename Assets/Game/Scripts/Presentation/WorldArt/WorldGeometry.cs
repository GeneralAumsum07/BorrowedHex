using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Orchestrates one arena's world art and owns its effect API (spec 4.1/4.2): the painted
    /// floor and outer ground (PaintedFloor), the backing ribbon and kit dressing
    /// (ArenaEnclosure) and kit-model cover fitted to the collision rects (CoverModels).
    /// Analytic collision stays in ArenaSim; nothing here has a collider. Coverage
    /// dissolves (_Visible/_Retiring) replace growth and shatter animations, as before.
    /// </summary>
    public sealed class WorldGeometry : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Transform root;
        readonly WorldArtLibrary art;
        readonly WorldModelLibrary models;
        readonly ArenaLayout layout;
        // Floor, Outer and Wall (the ribbon) cross-fade through _Morph and never dissolve,
        // so a morph can never open a hole onto the void (spec 3.3).
        readonly Dictionary<string, Material> surfaces = new Dictionary<string, Material>();
        readonly List<GameObject> cover = new List<GameObject>(), rubble = new List<GameObject>();
        readonly List<Renderer> scenery = new List<Renderer>();
        readonly Dictionary<Renderer, float> sceneryBase = new Dictionary<Renderer, float>();
        readonly Dictionary<GameObject, float> coverBase = new Dictionary<GameObject, float>();
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
            models = new WorldModelLibrary(ArenaDressing.KitTint(CurrentTheme));
            var floor = new PaintedFloor(root, arena, CurrentTheme, owned);
            surfaces["Floor"] = floor.Floor; surfaces["Outer"] = floor.Outer;
            surfaces["Wall"] = PaintedMaterials.Create("Wall", PaintedFloor.Texture(CurrentTheme, "Ribbon"), Color.white);
            owned.Add(surfaces["Wall"]);
            scenery.AddRange(new ArenaEnclosure(root, arena, models, surfaces["Wall"], owned).Scenery);
            for (int i = 0; i < arena.pillars.Count; i++) BuildCover(i);
            if (CurrentTheme == "Sanctum")
            {
                // The ritual seal stays on top of the painted basalt (spec 2.2).
                var b = arena.bounds; var glyph = art.Effect("Vortex", 100);
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
            var kind = index < layout.propKinds.Count ? layout.propKinds[index] : DecayPropKind.Column;
            var group = Group("CoverTrim" + index, root); group.transform.position = Geometry2D.ToWorld(p.center);
            float height = CoverModels.Height(kind);
            CoverModels.Build(models, group.transform, kind.ToString(), CoverModels.Model(kind, index), p.size, height);
            if (kind == DecayPropKind.Obelisk)
            {
                // The rune crown is the Sanctum reveal's landmark; spec 3.4 keeps it.
                var crown = Box("Ritual crown", group.transform, Geometry2D.ToWorld(p.center, height + .25f), new Vector3(.7f, .5f, .7f), Stone);
                crown.transform.rotation = Quaternion.Euler(0, 45, 0);
            }
            cover.Add(group);
            var debris = Group("CoverRubble" + index, root); debris.transform.position = Geometry2D.ToWorld(p.center);
            CoverModels.Build(models, debris.transform, "Rubble", CoverModels.Rubble(kind, index), p.size * .7f, .45f);
            debris.SetActive(false); rubble.Add(debris);
        }

        static GameObject Group(string name, Transform parent)
        { var value = new GameObject(name); value.transform.SetParent(parent, false); return value; }

        static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            // Disabled first: in play mode Release defers destruction to the end of the frame.
            var collider = value.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            value.transform.SetParent(parent, false); value.transform.position = at; value.transform.localScale = size;
            value.GetComponent<Renderer>().sharedMaterial = material; return value;
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
            var snapshot = PaintedFloor.Snapshot(material, PaintedFloor.SnapshotCap, out bool baked);
            // An un-baked snapshot is a shared Resources texture: releasing it would destroy the asset.
            if (baked) owned.Add(snapshot);
            return snapshot;
        }

        public void BeginMorphFrom(WorldGeometry previous)
        {
            morphing = true; previous.retiring = true;
            foreach (var pair in surfaces)
            {
                if (!previous.surfaces.TryGetValue(pair.Key, out var before)) continue;
                var snapshot = Snapshot(before); pair.Value.SetTexture("_FromMap", snapshot); pair.Value.SetFloat("_Morph", 0);
                before.SetTexture("_FromMap", snapshot); before.SetTexture("_BaseMap", pair.Value.GetTexture("_BaseMap"));
            }
            foreach (var renderer in previous.scenery) previous.sceneryBase[renderer] = previous.sceneryAmount;
            // Stages 0-2 share one footprint: the incoming floor, ground and ribbon take over
            // in place, so the outgoing copies are hidden rather than dissolved (no z-fighting).
            foreach (string name in new[] { "Floor", "Outer ground", ArenaEnclosure.RibbonName })
            {
                var old = previous.root.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }
        }

        public void RenderMorph(float amount, double now, bool reduceFlashes)
        {
            foreach (var material in surfaces.Values)
            {
                material.SetFloat("_Morph", morphing || retiring ? amount : 1);
                material.SetFloat("_GlitchTime", (float)now); material.SetFloat("_Glitches", reduceFlashes ? 0 : 1);
            }
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
                // A retained pillar can sit directly over the Collector or the player. The
                // cutout keeps their silhouettes readable while keeping the pillar's geometry
                // and collision; this is unrelated to world decay.
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(properties); properties.SetFloat("_Occlusion", blocks ? .12f : 1);
                    renderer.SetPropertyBlock(properties); properties.Clear();
                }
            }
        }

        public void Dispose()
        {
            WorldArtLibrary.Release(root.gameObject);
            foreach (var item in owned) WorldArtLibrary.Release(item); owned.Clear();
            models.Dispose();
        }

        public void SetVisible(bool value) => root.gameObject.SetActive(value);
    }
}
