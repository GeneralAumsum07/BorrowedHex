using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Draws one ArenaSim. Views are disposable: on restart the whole view root is destroyed
    /// and rebuilt against the new sim, mirroring the sim's own one-instance-per-run rule, so
    /// nothing visual can survive into the next run.
    ///
    /// The view POLLS sim state each frame rather than mirroring it through events; events are
    /// used only for one-shot flourishes (hit flashes). Polling makes it impossible for a
    /// missed event to leave a ghost sprite behind.
    ///
    /// Positions are interpolated between the previous and current fixed step using the
    /// accumulator fraction GameRoot passes in, so a 60 Hz sim looks smooth on any display.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        // Visual height of projectiles above their logical ground position (section 8: visual
        // height is separate from the hit position; the shadow marks where it really is).
        const float ProjectileHeight = 0.55f;

        static readonly Color HostileColor = new Color(1f, 0.38f, 0.28f);
        static readonly Color LanternColor = new Color(1f, 0.78f, 0.35f);
        static readonly Color ReturnedColor = new Color(0.45f, 0.95f, 1f);
        static readonly Color TelegraphColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color RocketColor = new Color(1f, 0.55f, 0.1f);

        ArenaSim sim;
        CharacterView player;
        SpriteRenderer aimMarker;
        Vector2 prevPlayer, currPlayer;
        CharacterView lantern;

        sealed class EnemyView
        {
            public CharacterView Body;
            // One aim line per shot in the volley, so a fan's spread is visible before it fires.
            public SpriteRenderer[] Telegraph;
            // Pursuer: ground disc exactly where the strike will land.
            public SpriteRenderer StrikeMarker;
            public SpriteRenderer WarningRing;
            public bool Seen;
        }

        readonly Dictionary<int, EnemyView> enemies = new Dictionary<int, EnemyView>();
        readonly List<int> scratch = new List<int>();

        sealed class ShotView
        {
            public Transform Root;
            public SpriteRenderer Glow;
            public SpriteRenderer Shadow;
        }

        readonly List<ShotView> shots = new List<ShotView>();
        Transform shotRoot, enemyRoot;

        // Phase 3 capture visuals. The cone is drawn only while the window is open, so its
        // appearance IS the timing feedback; orbit dots stand in for stored shots until real
        // packet art exists; pops are short-lived rings for capture/rejection flourishes.
        static readonly Color ConeColor = new Color(0.55f, 0.95f, 1f, 0.55f);
        static readonly Color RejectColor = new Color(1f, 0.3f, 0.3f);
        SpriteRenderer cone;
        readonly List<SpriteRenderer> orbitDots = new List<SpriteRenderer>();
        Transform orbitRoot;

        sealed class Pop
        {
            public SpriteRenderer Ring;
            public float Age, Life, From, To;
            public Color Color;
        }

        readonly List<Pop> pops = new List<Pop>();
        Camera cam;

        public CharacterView PlayerView => player;

        public static ArenaView Create(ArenaSim sim)
        {
            var go = new GameObject("ArenaView");
            var view = go.AddComponent<ArenaView>();
            view.Bind(sim);
            return view;
        }

        void Bind(ArenaSim s)
        {
            sim = s;
            player = CharacterView.Create(transform, "Player", PixelSprites.Kind.Magician, 1f, 0.45f);
            prevPlayer = currPlayer = sim.Player.Position;

            aimMarker = FlatSprite("AimMarker", transform, PixelSprites.Disc(true), new Color(1f, 0.85f, 0.35f, 0.85f));
            aimMarker.transform.localScale = Vector3.one * 0.32f;

            lantern = CharacterView.Create(transform, "Lantern", PixelSprites.Kind.Lantern, 1.1f, 0.4f);
            lantern.transform.position = Geometry2D.ToWorld(sim.Lantern.Position);

            enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(transform, false);
            shotRoot = new GameObject("Projectiles").transform;
            shotRoot.SetParent(transform, false);

            sim.Events.PlayerHit += (_, __) => player.Flash(0.12f);
            sim.Events.EnemyDamaged += (e, _) => { if (enemies.TryGetValue(e.ActorId, out var v)) v.Body.Flash(0.1f); };
            sim.Events.LanternFired += () => lantern.Flash(0.2f);

            cone = FlatSprite("CaptureCone", transform, PixelSprites.Sector(sim.Stats.CaptureConeAngle * 0.5f), ConeColor);
            cone.sortingOrder = -4;
            orbitRoot = new GameObject("PacketOrbits").transform;
            orbitRoot.SetParent(transform, false);

            // Successful capture: a cyan pinch where the shot was taken. Rejection: a red ring
            // at the shot, so "why did that hit me?" has a visible answer (packet/slots full).
            sim.Events.ShotCaptured += (_, __, at, ___) => SpawnPop(at, ReturnedColor, 0.5f, 0.1f, 0.18f);
            sim.Events.CaptureRejected += (at, _) => SpawnPop(at, RejectColor, 0.15f, 0.7f, 0.3f);
            sim.Events.PacketReleased += (_, __) => SpawnPop(sim.Player.Position, ReturnedColor, 0.4f, 1.4f, 0.25f);
            // Rocket burst drawn at its true damage radius (ring scale == radius), so the
            // player learns how far a returned rocket reaches.
            sim.Events.Explosion += (at, r, f) => SpawnPop(at, f == AttackFaction.Returned ? ReturnedColor : RocketColor, r * 0.3f, r, 0.35f);
        }

        /// <summary>A sprite lying flat on the ground (y slightly above the floor to avoid z-fighting).</summary>
        static SpriteRenderer FlatSprite(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = -5;
            return sr;
        }

        /// <summary>Called by GameRoot immediately before each sim step.</summary>
        public void BeforeStep() => prevPlayer = sim.Player.Position;

        /// <summary>Called by GameRoot immediately after each sim step.</summary>
        public void AfterStep() => currPlayer = sim.Player.Position;

        /// <summary><paramref name="alpha"/> is the leftover accumulator fraction in [0, 1).</summary>
        public void Render(float alpha)
        {
            if (cam == null) cam = Camera.main;
            RenderPlayer(alpha);
            RenderEnemies(alpha);
            RenderProjectiles(alpha);
            RenderCapture(alpha);
            RenderPops();
        }

        void RenderPlayer(float alpha)
        {
            var p = sim.Player;
            Vector2 pos = Vector2.Lerp(prevPlayer, currPlayer, alpha);
            player.transform.position = Geometry2D.ToWorld(pos);
            player.SetFacing(p.AimDirection.x);
            // Post-hit invulnerability blinks; dash i-frames are short enough to skip.
            player.SetBlink(p.Alive && p.InvulnerableUntil > sim.Clock.Now);
            player.gameObject.SetActive(p.Alive);

            aimMarker.enabled = p.Alive;
            aimMarker.transform.position = Geometry2D.ToWorld(pos + p.AimDirection * 1.4f, 0.03f);
        }

        void RenderEnemies(float alpha)
        {
            double now = sim.Clock.Now;
            foreach (var v in enemies.Values) v.Seen = false;

            foreach (var e in sim.Enemies)
            {
                if (!e.Alive) continue;
                if (!enemies.TryGetValue(e.ActorId, out var v))
                {
                    v = CreateEnemyView(e);
                    enemies.Add(e.ActorId, v);
                }
                v.Seen = true;
                Vector2 pos = Vector2.Lerp(e.PrevPosition, e.Position, alpha);
                v.Body.transform.position = Geometry2D.ToWorld(pos);
                v.Body.SetFacing(e.AimDirection.x);

                // Spawn warning: a pulsing ring and a ghosted body, so it reads as "not yet".
                bool warning = now < e.ActiveAt;
                v.WarningRing.enabled = warning;
                if (warning)
                {
                    float k = Mathf.Repeat((float)now * 3f, 1f);
                    v.WarningRing.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, k);
                    v.WarningRing.color = new Color(1f, 0.3f, 0.3f, 0.4f + 0.5f * k);
                }
                v.Body.SetTint(warning ? new Color(1f, 1f, 1f, 0.45f) : Color.white);

                // Telegraph: aim lines that brighten as the attack approaches and turn solid
                // once aim locks, the moment a sidestep starts to count. Pursuers show a
                // ground disc instead: the exact strike circle, so dodging is about leaving it.
                bool tele = e.Phase == EnemyPhase.Telegraph;
                var tune = sim.Config.combat.For(e.Category);
                float remaining = (float)(e.PhaseEndsAt - now);
                float ramp = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, tune.telegraph));
                bool rocket = tune.attackId == Data.AttackIds.Rocket;
                for (int i = 0; i < v.Telegraph.Length; i++)
                {
                    var line = v.Telegraph[i];
                    line.enabled = tele && e.Category != ActorCategory.Pursuer;
                    if (!line.enabled) continue;
                    var c = rocket ? RocketColor : TelegraphColor;
                    c.a = e.AimLocked ? 0.9f : Mathf.Lerp(0.15f, 0.55f, ramp);
                    line.color = c;
                    Vector2 dir = Geometry2D.Rotate(e.AimDirection, tune.volleySpreadDeg[i]);
                    float width = (rocket ? 2f : 1f) * (e.AimLocked ? 0.12f : 0.06f);
                    PlaceGroundLine(line.transform, pos + dir * e.Radius, dir, 7f, width);
                }
                if (v.StrikeMarker != null)
                {
                    v.StrikeMarker.enabled = tele;
                    if (tele)
                    {
                        var c = TelegraphColor;
                        c.a = e.AimLocked ? 0.6f : Mathf.Lerp(0.1f, 0.4f, ramp);
                        v.StrikeMarker.color = c;
                        Vector2 sc = pos + e.AimDirection * tune.strikeReach;
                        v.StrikeMarker.transform.position = Geometry2D.ToWorld(sc, 0.05f);
                        // Disc sprites are 2 units across, so scale == radius.
                        v.StrikeMarker.transform.localScale = Vector3.one * tune.strikeRadius;
                    }
                }
            }

            // Drop views for enemies that died or were removed this frame.
            scratch.Clear();
            foreach (var kv in enemies) if (!kv.Value.Seen) scratch.Add(kv.Key);
            foreach (int id in scratch)
            {
                Destroy(enemies[id].Body.gameObject);
                enemies.Remove(id);
            }
        }

        EnemyView CreateEnemyView(EnemyActor e)
        {
            var kind = e.Category switch
            {
                ActorCategory.Pursuer => PixelSprites.Kind.Pursuer,
                ActorCategory.ScatterCaster => PixelSprites.Kind.ScatterCaster,
                ActorCategory.SiegeFamiliar => PixelSprites.Kind.SiegeFamiliar,
                ActorCategory.Boss => PixelSprites.Kind.Collector,
                _ => PixelSprites.Kind.Acolyte,
            };
            var body = CharacterView.Create(enemyRoot, $"{e.Category}#{e.ActorId}", kind, 1f, e.Radius + 0.1f);
            var v = new EnemyView { Body = body };
            // Telegraph and warning ring live under the body root but must not inherit its
            // position offsets, so they are placed in world space each frame.
            int lines = Mathf.Max(1, sim.Config.combat.For(e.Category).volleySpreadDeg.Length);
            v.Telegraph = new SpriteRenderer[lines];
            for (int i = 0; i < lines; i++)
                v.Telegraph[i] = FlatSprite("Telegraph", body.transform, PixelSprites.Pixel(), TelegraphColor);
            if (e.Category == ActorCategory.Pursuer)
                v.StrikeMarker = FlatSprite("StrikeMarker", body.transform, PixelSprites.Disc(false), TelegraphColor);
            v.WarningRing = FlatSprite("SpawnWarning", body.transform, PixelSprites.Disc(true), Color.red);
            v.WarningRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            return v;
        }

        /// <summary>Lay a pixel sprite on the ground from <paramref name="from"/> along <paramref name="dir"/>.</summary>
        static void PlaceGroundLine(Transform t, Vector2 from, Vector2 dir, float length, float width)
        {
            t.position = Geometry2D.ToWorld(from, 0.05f);
            // Flat sprite: local X maps to world X; yaw around Y turns X toward the direction.
            float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(90f, yaw, 0f);
            t.localScale = new Vector3(length, width, 1f);
        }

        void RenderProjectiles(float alpha)
        {
            int n = 0;
            foreach (var p in sim.Projectiles)
            {
                if (!p.Active) continue;
                var v = n < shots.Count ? shots[n] : AddShotView();
                n++;
                Vector2 pos = Vector2.Lerp(p.PrevPosition, p.Position, alpha);
                v.Root.gameObject.SetActive(true);
                v.Root.position = Geometry2D.ToWorld(pos);
                Color c = p.Faction == AttackFaction.Returned ? ReturnedColor
                    : p.Shot.Kind == AttackKind.Rocket ? RocketColor
                    : p.Shot.DefinitionId == Data.AttackIds.LanternBolt ? LanternColor : HostileColor;
                v.Glow.color = c;
                // Glow size follows the logical radius so what you see is what can hit you
                // (Disc sprites are 2 units across, so scale == radius gives a true-size disc;
                // the 1.3 is a slight glow halo beyond the hitbox).
                v.Glow.transform.localScale = Vector3.one * (p.Radius * 1.3f);
                v.Shadow.transform.localScale = Vector3.one * (p.Radius * 2f);
                if (cam != null) v.Glow.transform.rotation = cam.transform.rotation;
            }
            for (int i = n; i < shots.Count; i++) shots[i].Root.gameObject.SetActive(false);
        }

        ShotView AddShotView()
        {
            var root = new GameObject("Shot").transform;
            root.SetParent(shotRoot, false);
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root, false);
            glowGo.transform.localPosition = new Vector3(0f, ProjectileHeight, 0f);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = PixelSprites.Disc(false);
            glow.sortingOrder = 5;
            var shadow = FlatSprite("Shadow", root, PixelSprites.Blob(), new Color(0f, 0f, 0f, 0.5f));
            shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var v = new ShotView { Root = root, Glow = glow, Shadow = shadow };
            shots.Add(v);
            return v;
        }

        void RenderCapture(float alpha)
        {
            var p = sim.Player;
            double now = sim.Clock.Now;
            Vector2 pos = Vector2.Lerp(prevPlayer, currPlayer, alpha);

            // The cone follows the CURRENT aim, matching the sim, which tests capture against
            // the live aim on every tick of the window (directional, not a snapshot).
            bool open = p.Alive && sim.Capture.IsWindowOpen(now);
            cone.enabled = open;
            if (open)
            {
                cone.transform.position = Geometry2D.ToWorld(pos, 0.04f);
                float yaw = -Mathf.Atan2(p.AimDirection.y, p.AimDirection.x) * Mathf.Rad2Deg;
                cone.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                cone.transform.localScale = Vector3.one * sim.Stats.CaptureRange;
            }

            // Orbit placeholders: one dot per stored shot, each packet on its own ring radius
            // and spin direction so two slots read as two separate bundles. Spin speeds up as
            // expiry nears; the HUD carries the exact countdown, this only says "incoming".
            int used = 0;
            var packets = sim.Packets.Packets;
            for (int k = 0; k < packets.Count; k++)
            {
                var pk = packets[k];
                float left = pk.Remaining(now);
                float urgency = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, pk.Lifetime));
                float radius = 0.75f + 0.3f * k;
                float spin = (float)now * (1.5f + 5f * urgency) * (k % 2 == 0 ? 1f : -1f);
                int count = pk.Payloads.Count;
                for (int i = 0; i < count; i++)
                {
                    var dot = used < orbitDots.Count ? orbitDots[used] : AddOrbitDot();
                    used++;
                    float a = spin + i * Mathf.PI * 2f / count;
                    Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    dot.gameObject.SetActive(true);
                    dot.transform.position = Geometry2D.ToWorld(pos + off, 0.7f);
                    // Blink during the final half second: the release is about to happen.
                    bool blink = left < 0.5f && Mathf.Repeat((float)now * 10f, 1f) < 0.5f;
                    dot.color = blink ? Color.white : ReturnedColor;
                    if (cam != null) dot.transform.rotation = cam.transform.rotation;
                }
            }
            for (int i = used; i < orbitDots.Count; i++) orbitDots[i].gameObject.SetActive(false);
        }

        SpriteRenderer AddOrbitDot()
        {
            var go = new GameObject("OrbitDot");
            go.transform.SetParent(orbitRoot, false);
            go.transform.localScale = Vector3.one * 0.12f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprites.Disc(false);
            sr.sortingOrder = 6;
            orbitDots.Add(sr);
            return sr;
        }

        void SpawnPop(Vector2 at, Color color, float from, float to, float life)
        {
            Pop pop = null;
            foreach (var q in pops) if (!q.Ring.enabled) { pop = q; break; }
            if (pop == null)
            {
                pop = new Pop { Ring = FlatSprite("Pop", transform, PixelSprites.Disc(true), color) };
                pops.Add(pop);
            }
            pop.Ring.enabled = true;
            pop.Ring.transform.position = Geometry2D.ToWorld(at, 0.06f);
            pop.Age = 0f; pop.Life = life; pop.From = from; pop.To = to; pop.Color = color;
        }

        void RenderPops()
        {
            // Unscaled frame time: these are cosmetic flourishes. While paused no new ones
            // spawn, and letting an existing one finish fading is harmless.
            float dt = Time.unscaledDeltaTime;
            foreach (var q in pops)
            {
                if (!q.Ring.enabled) continue;
                q.Age += dt;
                float f = q.Age / q.Life;
                if (f >= 1f) { q.Ring.enabled = false; continue; }
                q.Ring.transform.localScale = Vector3.one * Mathf.Lerp(q.From, q.To, f);
                var c = q.Color; c.a = 1f - f;
                q.Ring.color = c;
            }
        }
    }
}
