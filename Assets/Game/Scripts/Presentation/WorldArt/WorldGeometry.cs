using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Mesh scenery follows ArenaLayout but owns no collision or gameplay randomness.
    /// High silhouettes live beyond the north wall; the near parapet stays low so actors
    /// and telegraphs remain visible. One owner releases every generated material/texture.
    /// </summary>
    public sealed class WorldGeometry : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<GameObject> trim = new List<GameObject>(), rubble = new List<GameObject>();
        readonly List<GameObject> cracks = new List<GameObject>();
        readonly Dictionary<string, GameObject> themes = new Dictionary<string, GameObject>();
        readonly WorldArtLibrary art;
        readonly Transform root;
        readonly Material stone, basalt, floor, earth;
        readonly List<Rect> coverBounds;
        readonly Dictionary<Renderer, Vector3> sceneryScale = new Dictionary<Renderer, Vector3>();
        readonly Dictionary<SpriteRenderer, Color> sceneryColor = new Dictionary<SpriteRenderer, Color>();
        Texture2D morphTexture, destinationFloor;
        Color32[] morphSource, morphDestination, morphPixels;
        float[] morphThreshold;
        int morphTick = -1;
        Color morphColorFrom, morphColorTo;
        public string CurrentTheme { get; private set; }
        public Material Stone => stone;

        public WorldGeometry(Transform parent, ArenaLayout layout, WorldArtLibrary library, Material template)
        {
            art = library;
            coverBounds = layout.pillars;
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            stone = Surface("Weathered masonry", template, new Color(.48f, .47f, .44f));
            basalt = Surface("Basalt", template, new Color(.24f, .25f, .28f));
            floor = Surface("Broken cobbles", template, new Color(.58f, .57f, .54f));
            earth = Surface("Ash earth", template, new Color(.38f, .35f, .32f));
            var cobbles = Cobbles();
            floor.mainTexture = WornFloor(art.Texture("CaveGround"), layout.worldTheme);
            destinationFloor = (Texture2D)floor.mainTexture;
            stone.mainTexture = cobbles;
            earth.mainTexture = GroundPatch(art.Texture("CaveGround"));
            var b = layout.bounds;
            Box("Floor", root, new Vector3(b.center.x, -.25f, b.center.y), new Vector3(b.width + 2, .5f, b.height + 2), floor);
            Box("Foundation", root, new Vector3(b.center.x, -.6f, b.center.y), new Vector3(b.width + 2.6f, .35f, b.height + 2.6f), basalt);
            foreach (float side in new[] { -1f, 1f })
                Box("Earth border", root, new Vector3(b.center.x + side * (b.width / 2 - 1), -.031f, b.center.y), new Vector3(2, .08f, b.height), earth);
            var boundaries = layout.BuildObstacles();
            for (int i = 0; i < 4; i++)
            {
                var w = boundaries[i];
                float h = i == 0 ? .55f : i == 1 ? 2.2f : 1f;
                Box("Boundary" + i, root, new Vector3(w.center.x, h / 2, w.center.y), new Vector3(w.width, h, w.height), stone);
                Box("Coping" + i, root, new Vector3(w.center.x, h + .08f, w.center.y), new Vector3(w.width + .12f, .16f, w.height + .12f), basalt);
            }
            for (float x = b.xMin + 1.6f; x < b.xMax; x += 4.2f)
            {
                Arch(new Vector3(x, 0, b.yMax + 2.2f), 2.8f, 3.0f);
                Box("Buttress", root, new Vector3(x + 1.4f, 1.1f, b.yMax + 3), new Vector3(.7f, 2.2f, 2), basalt);
            }
            for (int i = 0; i < 3; i++)
                Box("Outer stair", root, new Vector3(b.center.x, -.12f - i * .18f, b.yMin - 1.65f - i * .45f), new Vector3(7 + i * .5f, .22f, .5f), stone);
            for (int i = 0; i < layout.pillars.Count; i++)
            {
                var p = layout.pillars[i];
                var group = Group("CoverTrim" + i, root);
                group.transform.position = Core.Geometry2D.ToWorld(p.center);
                var kind = i < layout.propKinds.Count ? layout.propKinds[i] : DecayPropKind.Column;
                float height = kind == DecayPropKind.Column ? 2.1f : kind == DecayPropKind.Obelisk ? 2.7f
                    : kind == DecayPropKind.DeadTree ? 1.8f : kind == DecayPropKind.Rock ? 1.3f : 1.15f;
                Box("Base", group.transform, new Vector3(p.center.x, .12f, p.center.y), new Vector3(p.width, .24f, p.height), basalt);
                if (kind == DecayPropKind.RuinedWall)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float h = height + (k == 1 ? -.3f : .25f);
                        Box("Wall fragment", group.transform, new Vector3(p.xMin + p.width * (k + .5f) / 3, h / 2, p.center.y),
                            new Vector3(p.width / 3 - .02f, h, p.height), stone);
                    }
                }
                else if (kind == DecayPropKind.Rock)
                {
                    var rock = Box("Boulder", group.transform, new Vector3(p.center.x, height / 2, p.center.y), new Vector3(p.width * .85f, height, p.height * .85f), basalt);
                    rock.transform.rotation = Quaternion.Euler(8, i * 23, 8);
                    Prop("Rock", group.transform, Core.Geometry2D.ToWorld(p.center, .5f), .8f);
                }
                else if (kind == DecayPropKind.DeadTree)
                {
                    Box("Stump", group.transform, new Vector3(p.center.x, .5f, p.center.y), new Vector3(p.width * .7f, 1, p.height * .7f), earth);
                    Prop("DeadTree", group.transform, Core.Geometry2D.ToWorld(p.center), .28f);
                }
                else
                {
                    Box(kind == DecayPropKind.Tomb ? "Sarcophagus" : "Column", group.transform, new Vector3(p.center.x, height / 2, p.center.y),
                        new Vector3(p.width * .8f, height, p.height * .8f), kind == DecayPropKind.Obelisk ? basalt : stone);
                    Box("Capital", group.transform, new Vector3(p.center.x, height, p.center.y), new Vector3(p.width, .2f, p.height), stone);
                    if (kind == DecayPropKind.Tomb)
                        Prop("Tomb", group.transform, Core.Geometry2D.ToWorld(p.center, height), .6f);
                    if (kind == DecayPropKind.Obelisk)
                    {
                        var crown = Box("Ritual crown", group.transform, Core.Geometry2D.ToWorld(p.center, height + .3f), new Vector3(.7f, .5f, .7f), basalt);
                        crown.transform.rotation = Quaternion.Euler(0, 45, 0);
                    }
                }
                var wear = Group("Fractures", group.transform);
                var fractureMaterial = Surface("Cover fractures", template, new Color(.08f, .07f, .1f));
                for (int k = 0; k < 3; k++)
                {
                    var line = Box("Fracture", wear.transform, new Vector3(p.center.x + (k - 1) * p.width * .16f, height * (.3f + k * .18f), p.yMin - .015f),
                        new Vector3(.035f, height * .35f, .015f), fractureMaterial);
                    line.transform.rotation = Quaternion.Euler(0, 0, k % 2 == 0 ? 24 : -28);
                }
                wear.SetActive(false); cracks.Add(wear);
                trim.Add(group);
                var debris = Group("CoverRubble" + i, root);
                debris.transform.position = Core.Geometry2D.ToWorld(p.center);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.9f;
                    var piece = Box("Fragment", debris.transform, new Vector3(p.center.x + Mathf.Cos(a) * p.width * .36f, .09f, p.center.y + Mathf.Sin(a) * p.height * .36f), new Vector3(.38f, .18f, .3f), k % 2 == 0 ? stone : basalt);
                    piece.transform.rotation = Quaternion.Euler(0, k * 47, 8);
                }
                debris.SetActive(false);
                rubble.Add(debris);
            }
            foreach (string name in new[] { "Courtyard", "Graveyard", "Cave", "Sanctum" })
            {
                var group = Group(name, root);
                themes[name] = group;
                for (int i = 0; i < 10; i++)
                {
                    float side = i % 2 == 0 ? -1 : 1;
                    float x = b.center.x + side * (b.width / 2 + 2.4f + i % 3);
                    float z = b.yMin + 1 + i * b.height / 10;
                    if (name == "Cave")
                    {
                        var rock = Box("RockMass", group.transform, new Vector3(x, 1.2f, z), new Vector3(2.5f, 2.5f + i % 3, 2), basalt);
                        rock.transform.rotation = Quaternion.Euler(12, i * 37, 15);
                        Prop("Rock", group.transform, new Vector3(x * .9f, 0, z), 1.4f);
                    }
                    else if (name == "Graveyard")
                    {
                        Box("Gravestone", group.transform, new Vector3(x, .7f, z), new Vector3(.7f, 1.4f, .3f), stone);
                        Prop("Tomb", group.transform, new Vector3(x * .92f, 0, z + .8f), 1.4f);
                    }
                    else if (name == "Sanctum")
                    {
                        Box("ObeliskBase", group.transform, new Vector3(x, .6f, z), new Vector3(1.2f, 1.2f, 1.2f), basalt);
                        Prop("Obelisk", group.transform, new Vector3(x, 1.2f, z), 1.4f);
                    }
                    else Prop("DeadTree", group.transform, new Vector3(x, 0, z), .8f);
                }
                group.SetActive(false);
            }
            Prop("GraveSky", themes["Graveyard"].transform, new Vector3(b.center.x, 1, b.yMax + 13), 1.7f, new Color(.7f, .75f, .9f));
            Prop("Temple", themes["Sanctum"].transform, new Vector3(b.center.x, 0, b.yMax + 12), .85f, new Color(.58f, .6f, .72f));
            // Physical braziers anchor the sprite flames to visible geometry.
            for (int i = 0; i < 8; i++)
            {
                var at = FlameAnchor(layout, i);
                Box("Brazier stem", root, at - Vector3.up * .65f, new Vector3(.18f, 1.3f, .18f), basalt);
                Box("Brazier bowl", root, at - Vector3.up * .12f, new Vector3(.5f, .2f, .5f), stone);
            }
        }

        public static Vector3 FlameAnchor(ArenaLayout layout, int i)
        {
            var b = layout.bounds;
            return new Vector3(b.center.x + (i % 2 == 0 ? -1 : 1) * (b.width / 2 + .45f), 1.4f, b.yMin + 1 + i * b.height / 8);
        }

        static GameObject Group(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }

        void Arch(Vector3 at, float width, float height)
        {
            foreach (float side in new[] { -1f, 1f })
                Box("Arch pier", root, at + new Vector3(side * width / 2, height / 2, 0), new Vector3(.55f, height, .85f), stone);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI / 6;
                var block = Box("Arch stone", root, at + new Vector3(Mathf.Cos(a) * width / 2, height + Mathf.Sin(a) * width / 2, 0), new Vector3(.72f, .5f, .85f), i % 2 == 0 ? stone : basalt);
                block.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90);
            }
        }

        public void SetTheme(string next)
        {
            if (CurrentTheme == next) return;
            CurrentTheme = next;
            foreach (var pair in themes) pair.Value.SetActive(pair.Key == next);
            floor.color = next == "Sanctum" ? new Color(.43f, .41f, .5f) : next == "Cave" ? new Color(.38f, .43f, .47f) : next == "Graveyard" ? new Color(.48f, .53f, .55f) : new Color(.58f, .55f, .49f);
        }

        public void RenderCover(ArenaSim sim)
        {
            for (int i = 0; i < trim.Count; i++)
            {
                bool broken = i < sim.Pillars.Count && sim.Pillars[i].Crumbled;
                float formation = sim.CoverFormation(i);
                trim[i].SetActive(!broken && formation > 0); rubble[i].SetActive(broken);
                float coverWear = i < sim.Pillars.Count ? 1f - (float)sim.Pillars[i].Durability / sim.Pillars[i].MaxDurability : 0;
                cracks[i].SetActive(!broken && coverWear >= .3f);
                trim[i].transform.localScale = new Vector3(1, formation * Mathf.Lerp(1, .82f, coverWear), 1);
            }
            // Time wear changes visual soil colour, not walkability or damage rules.
            float wear = sim.Pillars.Count == 0 ? 0 : Mathf.Clamp01((float)(sim.Clock.Now - sim.Pillars[0].RestoredAt) / 90);
            earth.color = Color.Lerp(new Color(.38f, .35f, .32f), new Color(.24f, .23f, .26f), wear);
        }

        public void RenderRetiring(ArenaSim sim)
        {
            for (int i = 0; i < coverBounds.Count; i++)
            {
                Core.DecayObstacle state = null;
                foreach (var old in sim.RetiringCover) if (old.Bounds == coverBounds[i]) { state = old; break; }
                bool broken = state == null || state.Crumbled;
                trim[i].SetActive(!broken); rubble[i].SetActive(broken); cracks[i].SetActive(!broken);
                if (!broken) trim[i].transform.localScale = new Vector3(1, Mathf.Lerp(1, .3f, sim.WorldMorphProgress), 1);
            }
        }

        public void BeginMorphFrom(WorldGeometry previous)
        {
            morphColorFrom = previous.floor.color; morphColorTo = floor.color;
            previous.CaptureSceneryForRetirement();
            morphSource = ((Texture2D)previous.floor.mainTexture).GetPixels32();
            morphDestination = destinationFloor.GetPixels32(); morphPixels = new Color32[morphDestination.Length];
            // Cache the spatial pattern once. Re-evaluating Perlin noise per pixel at
            // every upload would add needless CPU spikes while combat is running.
            morphThreshold = new float[morphPixels.Length];
            for (int i = 0; i < morphThreshold.Length; i++)
                morphThreshold[i] = Mathf.PerlinNoise(i % 512 / 54f + 5, i / 512 / 69f + 8) * .7f;
            morphTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            owned.Add(morphTexture); floor.mainTexture = morphTexture;
            RenderMorph(0, 0, false);
            // Identical outer structures never double-render and flicker. Retiring cover
            // and themed scenery remain visible, fracture and collapse in place.
            foreach (Transform child in previous.root)
                if (!child.name.StartsWith("Cover", StringComparison.Ordinal) && !previous.themes.ContainsKey(child.name))
                    child.gameObject.SetActive(false);
        }

        public void RenderMorph(float progress, double now, bool reduceFlashes)
        {
            if (morphTexture == null) return;
            floor.color = Color.Lerp(morphColorFrom, morphColorTo, progress);
            int tick = (int)(now * 6);
            if (tick == morphTick && progress < 1) return; morphTick = tick;
            if (progress >= 1) { floor.mainTexture = destinationFloor; return; }
            for (int i = 0; i < morphPixels.Length; i++)
            {
                int x = i % 512, y = i / 512;
                float blend = Mathf.Clamp01(progress * 1.7f - morphThreshold[i]);
                var target = morphDestination[i];
                // Narrow, sparse bands offset the incoming texture: a decaying-world glitch
                // without a fullscreen flash. Accessibility can remove the band entirely.
                if (!reduceFlashes && progress > .05f && ((y / 5 + tick * 3) % 67 == 0))
                    target = morphDestination[y * 512 + (x + 13 + tick % 7) % 512];
                morphPixels[i] = Color32.Lerp(morphSource[i], target, blend);
            }
            morphTexture.SetPixels32(morphPixels); morphTexture.Apply();
        }

        public void RenderSceneryFormation(float amount)
        {
            foreach (var pair in themes)
                if (pair.Value.activeSelf)
                    foreach (var renderer in pair.Value.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!sceneryScale.ContainsKey(renderer)) sceneryScale[renderer] = renderer.transform.localScale;
                        if (renderer is SpriteRenderer sprite)
                        {
                            if (!sceneryColor.ContainsKey(sprite)) sceneryColor[sprite] = sprite.color;
                            var color = sceneryColor[sprite]; color.a *= amount; sprite.color = color;
                        }
                        else
                        {
                            var scale = sceneryScale[renderer]; scale.y *= Mathf.Max(.02f, amount); renderer.transform.localScale = scale;
                        }
                    }
        }

        void CaptureSceneryForRetirement()
        {
            // A very fast encounter can interrupt formation. Preserve what is currently
            // visible rather than resurrecting the whole previous theme at full strength.
            sceneryScale.Clear(); sceneryColor.Clear();
            foreach (var pair in themes)
                if (pair.Value.activeSelf)
                    foreach (var renderer in pair.Value.GetComponentsInChildren<Renderer>(true))
                    {
                        sceneryScale[renderer] = renderer.transform.localScale;
                        if (renderer is SpriteRenderer sprite) sceneryColor[sprite] = sprite.color;
                    }
        }

        GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false); go.transform.position = at; go.transform.localScale = size;
            var collider = go.GetComponent<Collider>(); collider.enabled = false;
            WorldArtLibrary.Release(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (material == stone)
            {
                // World-size UVs stop long walls stretching a single brick into a beam.
                var filter = go.GetComponent<MeshFilter>();
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < uv.Length; i++)
                {
                    var p = Vector3.Scale(vertices[i], size);
                    uv[i] = Mathf.Abs(normals[i].y) > .5f ? new Vector2(p.x, p.z) / 3
                        : Mathf.Abs(normals[i].x) > .5f ? new Vector2(p.z, p.y) / 3 : new Vector2(p.x, p.y) / 3;
                }
                mesh.uv = uv; filter.sharedMesh = mesh; owned.Add(mesh);
            }
            return go;
        }

        void Prop(string name, Transform parent, Vector3 at, float scale, Color? color = null)
        {
            var sprite = art.Prop(name); if (sprite == null) return;
            var go = Group(name, parent);
            go.transform.position = at; go.transform.localScale = Vector3.one * scale;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color ?? new Color(.65f, .65f, .67f);
        }

        Material Surface(string name, Material template, Color color)
        {
            var value = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            value.name = name; value.color = color;
            value.SetFloat("_ReceiveShadows", 0); value.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            owned.Add(value); return value;
        }

        Texture2D Cobbles()
        {
            // Nearest-filtered low-resolution masonry fits arbitrary 3D faces. Side-view
            // wall sprites cannot wrap those faces without baking incorrect perspective.
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                int sx = (x + (y / 8 % 2) * 8) % 16;
                bool seam = y % 8 == 0 || sx == 0;
                float value = seam ? .31f : .68f + ((x * 13 + y * 7) % 11) / 110f;
                if (!seam && ((x / 16 * 17 + y / 8 * 31) % 9 == 0) && sx == y % 8 + 3) value -= .24f;
                texture.SetPixel(x, y, new Color(value, value * .96f, value * .91f));
            }
            texture.Apply(); owned.Add(texture); return texture;
        }

        Texture2D GroundPatch(Texture2D source)
        {
            if (source == null || source.width < 512 || source.height < 512) return null;
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            // Sample an interior atlas patch away from white margins and empty quadrants.
            var pixels = source.GetPixels(128, source.height - 320, 128, 128);
            for (int i = 0; i < pixels.Length; i++) { float v = pixels[i].grayscale; pixels[i] = new Color(v * 1.5f, v * 1.45f, v * 1.4f, 1); }
            texture.SetPixels(pixels); texture.Apply(); owned.Add(texture); return texture;
        }

        Texture2D WornFloor(Texture2D source, string theme)
        {
            // The floor uses a single authored-size texture: varied pavers and patches of
            // the downloaded cracked earth avoid an endless repeated brick carpet. All
            // variation is deterministic and never consumes the combat random generator.
            const int n = 512;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[n * n];
            var ground = source != null ? source.GetPixels32() : null;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int row = y / 16, offsetX = x + (row % 2) * 8, col = offsetX / 16;
                int sx = offsetX % 16, sy = y % 16;
                int hash = (col * 73 + row * 137 + col * row * 17) % 101;
                float shade = .66f + hash / 480f;
                if (sx <= 1 || sy <= 1) shade = .25f;
                else if (sx == 2 || sy == 2) shade += .06f;
                else if (sx == 15 || sy == 15) shade -= .1f;
                if (hash < 15 && sx == (sy * 3 / 5 + hash) % 16) shade *= .6f;
                float age = theme == "Graveyard" ? 7 : theme == "Cave" ? 16 : theme == "Sanctum" ? 25 : 0;
                float weather = Mathf.PerlinNoise(x / 83f + 12 + age, y / 91f + 4);
                var color = new Color(shade, shade * .96f, shade * .89f);
                if (ground != null && source.width >= 1024 && source.height >= 1024)
                {
                    var sample = ground[(source.height - 40 - y * 3 / 2) * source.width + 40 + x * 3 / 2];
                    float v = ((Color)sample).grayscale * 1.5f;
                    color = Color.Lerp(color, new Color(v * .7f, v * .75f, v * .73f), Mathf.Clamp01((weather - .47f) * 3.4f));
                }
                pixels[y * n + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(); owned.Add(texture); return texture;
        }

        public void Dispose()
        {
            if (root != null) WorldArtLibrary.Release(root.gameObject);
            foreach (var value in owned) WorldArtLibrary.Release(value);
            owned.Clear();
        }
        public void SetVisible(bool value) => root.gameObject.SetActive(value);
    }
}
