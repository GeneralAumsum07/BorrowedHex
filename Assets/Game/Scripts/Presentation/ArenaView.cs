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

        ArenaSim sim;
        CharacterView player;
        SpriteRenderer aimMarker;
        Vector2 prevPlayer, currPlayer;
        CharacterView lantern;

        sealed class EnemyView
        {
            public CharacterView Body;
            public SpriteRenderer Telegraph;
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

                // Telegraph: an aim line that brightens as the shot approaches and turns solid
                // once aim locks — the moment a sidestep starts to count.
                bool tele = e.Phase == EnemyPhase.Telegraph;
                v.Telegraph.enabled = tele;
                if (tele)
                {
                    var tune = sim.Config.combat.acolyte;
                    float remaining = (float)(e.PhaseEndsAt - now);
                    float k = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, tune.telegraph));
                    var c = TelegraphColor;
                    c.a = e.AimLocked ? 0.9f : Mathf.Lerp(0.15f, 0.55f, k);
                    v.Telegraph.color = c;
                    PlaceGroundLine(v.Telegraph.transform, pos + e.AimDirection * e.Radius, e.AimDirection, 7f,
                        e.AimLocked ? 0.12f : 0.06f);
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
            v.Telegraph = FlatSprite("Telegraph", body.transform, PixelSprites.Pixel(), TelegraphColor);
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
    }
}
