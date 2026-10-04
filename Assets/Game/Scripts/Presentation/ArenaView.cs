using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Presentation.Feedback;
using BorrowedHex.Presentation.WorldArt;
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

        // State colours live in FeedbackColors (one source of truth for halo, trail and feedback).
        static readonly Color HostileColor = FeedbackColors.Hostile;
        // Riposte: gold, so a parried strike reads as different from a packet release.
        static readonly Color RiposteColor = FeedbackColors.Riposte;
        static readonly Color ReturnedColor = FeedbackColors.Returned;
        static readonly Color TelegraphColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color TeleportColor = new Color(0.75f, 0.45f, 1f);
        static readonly Color RocketColor = FeedbackColors.Rocket;
        // D94: the perfect-release colour, shared with the hand HUD's Overcharge fill.
        static readonly Color OverchargeGold = FeedbackColors.Overcharge;

        ArenaSim sim;
        CharacterView player;
        SpriteRenderer aimMarker;
        Vector2 prevPlayer, currPlayer;

        sealed class EnemyView
        {
            public CharacterView Body;
            // One aim line per shot in the volley, so a fan's spread is visible before it fires.
            public SpriteRenderer[] Telegraph;
            // Pursuer: ground disc exactly where the strike will land.
            public SpriteRenderer StrikeMarker;
            // Pursuer: the strike circle's thin rim band, gold like the player's parry band,
            // because touching THIS edge with THAT band is the parry (D36).
            public SpriteRenderer StrikeRim;
            public SpriteRenderer WarningRing;
            // Collector only: the sweep's wedge and moving blade, and the slam's charge disc
            // inside a ring drawn at the slam's true radius.
            public SpriteRenderer SweepWedge, SweepBlade, SlamFill, SlamRing;
            // Collector only: the sweep's gold parry arc (D47) and the teleport arrival marker.
            public SpriteRenderer SweepParry, TeleportMarker;
            public bool Seen;
            public bool Evolved;
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
        static readonly Color RejectColor = FeedbackColors.Danger;
        SpriteRenderer cone;
        // D36: the parry band, drawn inside the cone only while the parry window is open.
        // When it disappears the cone is exactly the plain catch cone again, so the band's
        // vanishing IS the "parry over, still catching" cue.
        SpriteRenderer parryBand;
        float parryBandOuter;
        readonly List<SpriteRenderer> orbitDots = new List<SpriteRenderer>();
        Transform orbitRoot;

        sealed class Pop
        {
            public SpriteRenderer Ring;
            public float Age, Life, From, To;
            public Color Color;
        }

        readonly List<Pop> pops = new List<Pop>();
        sealed class TimeNumber
        {
            public TextMesh Text;
            public Vector3 Origin;
            public float Age;
        }
        readonly List<TimeNumber> timeNumbers = new List<TimeNumber>();
        Camera cam;

        public CharacterView PlayerView => player;

        // The VFX pass: this view owns the run's art library and feedback director, so both die
        // with the run exactly like every other visual here.
        WorldArtLibrary art;
        CombatFeedback feedback;
        public CombatFeedback Feedback => feedback;
        public WorldArtLibrary Art => art;
        SpriteRenderer heavyOrbitRing;

        /// <param name="host">Owner of the frame loop and camera (GameRoot). Null in tests and
        /// tools: feedback then skips shake and hit-stop and still draws everything else.</param>
        public static ArenaView Create(ArenaSim sim, IFeedbackHost host = null)
        {
            var go = new GameObject("ArenaView");
            var view = go.AddComponent<ArenaView>();
            view.Bind(sim, host);
            return view;
        }

        void Bind(ArenaSim s, IFeedbackHost host)
        {
            sim = s;
            art = new WorldArtLibrary();
            player = CharacterView.Create(transform, "Player", PixelSprites.Kind.Magician, 1f, 0.45f);
            prevPlayer = currPlayer = sim.Player.Position;

            aimMarker = FlatSprite("AimMarker", transform, PixelSprites.Disc(true), new Color(1f, 0.85f, 0.35f, 0.85f));
            aimMarker.transform.localScale = Vector3.one * 0.32f;
            // Phase 14: the movement lesson's target, a gold ring at the true "close enough"
            // radius (ring sprite: scale == radius), so standing inside it is exactly what counts.
            if (sim.Tutorial != null)
            {
                tutorialMarker = FlatSprite("TutorialMarker", transform, PixelSprites.Disc(true), new Color(1f, 0.85f, 0.35f, 0.9f));
                tutorialMarker.enabled = false;
            }

            enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(transform, false);
            shotRoot = new GameObject("Projectiles").transform;
            shotRoot.SetParent(transform, false);

            sim.Events.PlayerHit += (_, __) => player.Flash(0.12f);
            sim.Events.LifeClockChanged += SpawnTimeNumber;
            sim.Events.LifeStolen += SpawnStolenNumber;
            sim.Events.PacketBackfired += _ => SpawnPop(sim.Player.Position, RejectColor, 0.1f, 2f, 0.35f);
            sim.Events.EnemyDamaged += (e, _) => { if (enemies.TryGetValue(e.ActorId, out var v)) v.Body.Flash(0.1f); };

            // Daredevil's region is a disc around the dashing body (D83): a full 360-degree
            // sector, so it shares the cone's sprite path, colour and sorting.
            float coneHalf = sim.Stats.CatchIsDash ? 180f : sim.Stats.CaptureConeAngle * 0.5f;
            cone = FlatSprite("CaptureCone", transform, PixelSprites.Sector(coneHalf), ConeColor);
            cone.sortingOrder = -4;
            var st = sim.Stats;
            parryBandOuter = st.ParryRingRadius + st.ParryRingWidth * 0.5f;
            float bandInner = (st.ParryRingRadius - st.ParryRingWidth * 0.5f) / parryBandOuter;
            parryBand = FlatSprite("ParryBand", transform, PixelSprites.ArcBand(st.CaptureConeAngle * 0.5f, bandInner), RiposteColor);
            parryBand.sortingOrder = -3; // above the cone fill
            // Heavy Orbit (Phase 7): a ring at the true damage reach, shown only while it is live
            // (upgrade held AND a packet held), so the player sees when holding is a weapon.
            heavyOrbitRing = FlatSprite("HeavyOrbit", transform, PixelSprites.Disc(true), new Color(0.45f, 0.95f, 1f, 0.35f));
            heavyOrbitRing.enabled = false;
            // Echo Volley: a fainter release pop where the echo leaves the player.
            sim.Events.EchoFired += _ => SpawnPop(sim.Player.Position, ReturnedColor, 0.3f, 1.0f, 0.2f);
            orbitRoot = new GameObject("PacketOrbits").transform;
            orbitRoot.SetParent(transform, false);

            // Successful capture: a cyan pinch where the shot was taken. Rejection: a red ring
            // at the shot, so "why did that hit me?" has a visible answer (packet/slots full).
            sim.Events.ShotCaptured += (_, __, at, ___) => SpawnPop(at, ReturnedColor, 0.5f, 0.1f, 0.18f);
            sim.Events.CaptureRejected += (at, _) => SpawnPop(at, RejectColor, 0.15f, 0.7f, 0.3f);
            // D90: a refused fire gets a small grey fizzle at the player, so "I clicked and
            // nothing happened" reads as "too fresh", not as a dropped input.
            sim.Events.ReleaseRefused += _ => SpawnPop(sim.Player.Position, new Color(0.7f, 0.7f, 0.75f), 0.2f, 0.6f, 0.15f);
            sim.Events.PacketReleased += (_, __) => SpawnPop(sim.Player.Position, ReturnedColor, 0.4f, 1.4f, 0.25f);
            // D94: the perfect release - a wide gold ring; the callout carries the multiplier now.
            sim.Events.PacketOvercharged += (pk, _) => SpawnPop(sim.Player.Position, OverchargeGold, 0.4f, 2.2f, 0.3f);
            // R11: gold damage numbers, only for overcharged hits (the game shows no others).
            sim.Events.EnemyDamaged += (e, d) => { if (d.Overcharged) SpawnNumber($"{d.Amount:0.#}", OverchargeGold, e.Position); };
            // Rocket burst drawn at its true damage radius (ring scale == radius), so the
            // player learns how far a returned rocket reaches.
            sim.Events.Explosion += (at, r, f) => SpawnPop(at, f == AttackFaction.Returned ? ReturnedColor : RocketColor, r * 0.3f, r, 0.35f);
            // Parry: a gold ring snaps shut on the strike circle (the hit that didn't land) and
            // the attacker flashes (not the player: a player flash already means "you were hit").
            sim.Events.StrikeParried += (e, at) =>
            {
                SpawnPop(at, RiposteColor, sim.Config.combat.pursuer.strikeRadius * 1.4f, 0.15f, 0.22f);
                if (enemies.TryGetValue(e.ActorId, out var v)) v.Body.Flash(0.15f);
            };
            // Subscribed last, so every handler above has drawn its part of a moment first.
            feedback = new CombatFeedback(sim, transform, art, host,
                id => enemies.TryGetValue(id, out var v) ? v.Body : null, player);
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
            RenderTimeNumbers();
            RenderTutorialMarker();
            feedback.Render(cam, Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            // The feedback director first: its effects borrow textures from the art library.
            feedback?.Dispose();
            art?.Dispose();
        }

        SpriteRenderer tutorialMarker;

        void RenderTutorialMarker()
        {
            if (tutorialMarker == null) return;
            var m = sim.Tutorial.Marker;
            tutorialMarker.enabled = m.HasValue;
            if (!m.HasValue) return;
            tutorialMarker.transform.position = Geometry2D.ToWorld(m.Value, 0.04f);
            // A slow breathing pulse (unscaled: decoration, keeps moving while paused) that never
            // shrinks below the real radius, so the visible ring never promises less than it gives.
            float pulse = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            tutorialMarker.transform.localScale = Vector3.one * (BorrowedHex.Runs.TutorialDirector.MarkerRadius * pulse);
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
            float orbit = sim.OrbitRadiusNow;
            heavyOrbitRing.enabled = p.Alive && orbit > 0f;
            if (heavyOrbitRing.enabled)
            {
                heavyOrbitRing.transform.position = Geometry2D.ToWorld(pos, 0.05f);
                heavyOrbitRing.transform.localScale = Vector3.one * orbit;   // ring sprite: scale == radius
            }

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
                if (e.Overstayed && !v.Evolved)
                {
                    v.Evolved = true;
                    var kind = e.Category switch
                    {
                        ActorCategory.Pursuer => PixelSprites.Kind.Pursuer,
                        ActorCategory.ScatterCaster => PixelSprites.Kind.ScatterCaster,
                        ActorCategory.SiegeFamiliar => PixelSprites.Kind.SiegeFamiliar,
                        _ => PixelSprites.Kind.Acolyte,
                    };
                    var art = sim.Config.OverstayedSprite(e.Category);
                    v.Body.SetSprite(art != null ? art : PixelSprites.Overstayed(kind));
                    v.Body.SetVisualScale(1.2f);
                    v.Body.Flash(0.2f);
                }

                // Spawn warning: a pulsing ring and a ghosted body, so it reads as "not yet".
                bool warning = now < e.ActiveAt;
                double overstayLeft = e.ActiveAt + sim.OverstaySeconds - now;
                bool overstayWarning = !e.IsBoss && !e.Overstayed && !warning
                    && overstayLeft <= sim.Config.combat.overstayWarningSeconds;
                v.WarningRing.enabled = warning || overstayWarning;
                if (warning)
                {
                    float k = Mathf.Repeat((float)now * 3f, 1f);
                    v.WarningRing.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 0.8f, k);
                    v.WarningRing.color = new Color(1f, 0.3f, 0.3f, 0.4f + 0.5f * k);
                }
                else if (overstayWarning)
                {
                    float left = Mathf.Clamp01((float)overstayLeft / Mathf.Max(0.01f, sim.Config.combat.overstayWarningSeconds));
                    v.WarningRing.transform.localScale = Vector3.one * Mathf.Lerp(e.Radius * 2f, 2.8f, left);
                    v.WarningRing.color = new Color(0.95f, 0.2f, 0.75f, 0.9f);
                }
                v.Body.SetTint(warning ? new Color(1f, 1f, 1f, 0.45f) : Color.white);
                if (e.IsBoss)
                {
                    RenderBoss(e, v, pos, now);
                    continue;
                }

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
                        // Rim slightly above the disc so it is never z-fighting with it; its
                        // alpha follows the same ramp so the whole marker reads as one shape.
                        v.StrikeRim.transform.position = Geometry2D.ToWorld(sc, 0.055f);
                        v.StrikeRim.transform.localScale = Vector3.one * tune.strikeRadius;
                        var rc = RiposteColor;
                        rc.a = e.AimLocked ? 0.95f : Mathf.Lerp(0.25f, 0.7f, ramp);
                        v.StrikeRim.color = rc;
                    }
                }
                // The gold rim is the parry chance, and it only exists in its window (D46).
                if (v.StrikeRim != null) v.StrikeRim.enabled = tele && e.ParryRimOpen(now);
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
            int lines = e.IsBoss ? CollectorBoss.FanOf(e, sim.Config.collector).Length
                : Mathf.Max(1, sim.Config.combat.For(e.Category).volleySpreadDeg.Length);
            v.Telegraph = new SpriteRenderer[lines];
            for (int i = 0; i < lines; i++)
                v.Telegraph[i] = FlatSprite("Telegraph", body.transform, PixelSprites.Pixel(), TelegraphColor);
            if (e.Category == ActorCategory.Pursuer)
            {
                v.StrikeMarker = FlatSprite("StrikeMarker", body.transform, PixelSprites.Disc(false), TelegraphColor);
                var pt = sim.Config.combat.pursuer;
                v.StrikeRim = FlatSprite("StrikeRim", body.transform,
                    PixelSprites.Annulus((pt.strikeRadius - pt.strikeEdgeWidth) / pt.strikeRadius), RiposteColor);
            }
            if (e.IsBoss)
            {
                var bt = sim.Config.collector;
                v.SweepWedge = FlatSprite("SweepWedge", body.transform, PixelSprites.Sector(bt.sweepHalfAngle), TelegraphColor);
                v.SweepBlade = FlatSprite("SweepBlade", body.transform, PixelSprites.Pixel(), Color.white);
                v.SlamFill = FlatSprite("SlamFill", body.transform, PixelSprites.Disc(false), TelegraphColor);
                v.SlamRing = FlatSprite("SlamRing", body.transform, PixelSprites.Disc(true), TelegraphColor);
                float outer = bt.sweepParryArcRadius + bt.sweepParryArcWidth * 0.5f;
                v.SweepParry = FlatSprite("SweepParry", body.transform,
                    PixelSprites.ArcBand(bt.sweepHalfAngle, (bt.sweepParryArcRadius - bt.sweepParryArcWidth * 0.5f) / outer), RiposteColor);
                v.TeleportMarker = FlatSprite("TeleportMarker", body.transform, PixelSprites.Disc(true), TeleportColor);
            }
            v.WarningRing = FlatSprite("SpawnWarning", body.transform, PixelSprites.Disc(true), Color.red);
            v.WarningRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            return v;
        }

        /// <summary>
        /// The Collector's telegraphs. Every pattern shows WHERE it will land before it lands:
        ///   - bolt stream / fan: aim lines (one per bolt for the fan), solid once aim locks;
        ///   - sweep: the whole wedge it will cover, then the blade crossing it;
        ///   - slam: a ring at the slam's true radius that fills as the slam charges.
        /// Red = it hurts; the boss has no gold (parryable) markings, because its melee cannot
        /// be parried (D38).
        /// </summary>
        void RenderBoss(EnemyActor e, EnemyView v, Vector2 pos, double now)
        {
            var t = sim.Config.collector;
            var b = e.Boss;
            bool tele = b.Stage == BossStage.Telegraph;
            bool active = b.Stage == BossStage.Active;
            float ramp = tele ? 1f - Mathf.Clamp01((float)(b.StageEndsAt - now) / Mathf.Max(0.01f, CollectorBoss.TelegraphOf(b.Pattern, t))) : 0f;
            float yaw = -Mathf.Atan2(e.AimDirection.y, e.AimDirection.x) * Mathf.Rad2Deg;

            // Aim lines: the stream shows one (it keeps tracking while firing), the fan all.
            var fan = CollectorBoss.FanOf(e, t);
            int shown = b.Pattern == BossPattern.FanVolley && tele ? fan.Length
                : b.Pattern == BossPattern.BoltStream && (tele || active) ? 1 : 0;
            for (int i = 0; i < v.Telegraph.Length; i++)
            {
                var line = v.Telegraph[i];
                line.enabled = i < shown;
                if (!line.enabled) continue;
                float off = b.Pattern == BossPattern.FanVolley ? fan[i] : 0f;
                var c = TelegraphColor;
                c.a = active ? 0.5f : e.AimLocked ? 0.9f : Mathf.Lerp(0.15f, 0.6f, ramp);
                line.color = c;
                Vector2 dir = Geometry2D.Rotate(e.AimDirection, off);
                PlaceGroundLine(line.transform, pos + dir * e.Radius, dir, 8f, e.AimLocked || active ? 0.12f : 0.06f);
            }

            // Teleport: the body fades out where it stands while a ring pulses where it will
            // appear, so the blink is surprising in position but never unannounced (D49).
            bool porting = b.Stage == BossStage.Teleport;
            v.TeleportMarker.enabled = porting;
            if (porting)
            {
                float k = 1f - Mathf.Clamp01((float)(b.StageEndsAt - now) / Mathf.Max(0.01f, t.teleportTelegraph));
                v.Body.SetTint(new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.25f, k)));
                v.TeleportMarker.transform.position = Geometry2D.ToWorld(b.TeleportTo, 0.05f);
                v.TeleportMarker.transform.localScale = Vector3.one * (e.Radius * Mathf.Lerp(1.8f, 1f, k));
                var tc = TeleportColor;
                tc.a = Mathf.Lerp(0.3f, 0.95f, k);
                v.TeleportMarker.color = tc;
            }

            bool sweep = b.Pattern == BossPattern.Sweep && (tele || active);
            // Gold parry arc: only inside its window, like a Pursuer's rim.
            bool sweepParry = sweep && tele && e.ParryRimOpen(now);
            v.SweepParry.enabled = sweepParry;
            if (sweepParry)
            {
                v.SweepParry.transform.position = Geometry2D.ToWorld(pos, 0.055f);
                v.SweepParry.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                v.SweepParry.transform.localScale = Vector3.one * (t.sweepParryArcRadius + t.sweepParryArcWidth * 0.5f);
                var gc = RiposteColor;
                gc.a = 0.95f;
                v.SweepParry.color = gc;
            }
            v.SweepWedge.enabled = sweep;
            v.SweepBlade.enabled = sweep && active;
            if (sweep)
            {
                v.SweepWedge.transform.position = Geometry2D.ToWorld(pos, 0.05f);
                v.SweepWedge.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                v.SweepWedge.transform.localScale = Vector3.one * t.sweepReach;
                var c = TelegraphColor;
                c.a = active ? 0.25f : e.AimLocked ? 0.6f : Mathf.Lerp(0.12f, 0.45f, ramp);
                v.SweepWedge.color = c;
                if (active)
                {
                    Vector2 bladeDir = Geometry2D.Rotate(e.AimDirection, b.BladeDeg);
                    PlaceGroundLine(v.SweepBlade.transform, pos, bladeDir, t.sweepReach, 0.22f);
                    v.SweepBlade.color = new Color(1f, 0.95f, 0.85f, 0.95f);
                }
            }

            bool slam = b.Pattern == BossPattern.Slam && tele;
            v.SlamFill.enabled = v.SlamRing.enabled = slam;
            if (slam)
            {
                v.SlamRing.transform.position = Geometry2D.ToWorld(pos, 0.05f);
                v.SlamRing.transform.localScale = Vector3.one * t.slamRadius;
                var rc = TelegraphColor;
                rc.a = Mathf.Lerp(0.35f, 0.9f, ramp);
                v.SlamRing.color = rc;
                // The fill grows to the ring: when it touches the edge, the slam lands.
                v.SlamFill.transform.position = Geometry2D.ToWorld(pos, 0.045f);
                v.SlamFill.transform.localScale = Vector3.one * (t.slamRadius * Mathf.Max(0.05f, ramp));
                var fc = TelegraphColor;
                fc.a = 0.3f;
                v.SlamFill.color = fc;
            }
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
                Color c = p.Shot.Overcharged ? OverchargeGold
                    : p.Shot.Kind == AttackKind.Riposte ? RiposteColor
                    : p.Faction == AttackFaction.Returned ? ReturnedColor
                    : p.Shot.Kind == AttackKind.Rocket ? RocketColor : HostileColor;
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
                cone.transform.localScale = Vector3.one * (sim.Stats.CatchIsDash ? sim.Stats.DashCatchRadius : sim.Stats.CaptureRange);
            }
            bool parry = open && sim.Capture.IsParryOpen(now);
            parryBand.enabled = parry;
            if (parry)
            {
                parryBand.transform.position = Geometry2D.ToWorld(pos, 0.045f);
                parryBand.transform.rotation = cone.transform.rotation;
                parryBand.transform.localScale = Vector3.one * parryBandOuter;
            }

            // Orbit placeholders: one dot per stored shot, each SLOT on its own ring radius and
            // spin direction so two slots read as two separate bundles (keyed by the fixed slot
            // index, so a bundle keeps its ring when the other one fires). The selected slot's
            // dots are larger: that is what right mouse will throw. Spin speeds up as
            // expiry nears; the HUD carries the exact countdown, this only says "incoming".
            int used = 0;
            var packets = sim.Packets.Packets;
            for (int k = 0; k < packets.Count; k++)
            {
                var pk = packets[k];
                float left = pk.Remaining(now);
                float urgency = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, pk.Lifetime));
                int slot = pk.Slot;
                bool selected = slot == sim.Packets.SelectedSlot;
                float radius = 0.75f + 0.3f * slot;
                float spin = (float)pk.DecayedTime * (1.5f + 5f * urgency) * (slot % 2 == 0 ? 1f : -1f);
                int count = pk.Payloads.Count;
                for (int i = 0; i < count; i++)
                {
                    var dot = used < orbitDots.Count ? orbitDots[used] : AddOrbitDot();
                    used++;
                    float a = spin + i * Mathf.PI * 2f / count;
                    Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    dot.gameObject.SetActive(true);
                    dot.transform.localScale = Vector3.one * (selected ? 0.17f : 0.12f);
                    dot.transform.position = Geometry2D.ToWorld(pos + off, 0.7f);
                    // Blink during the final half second: the release is about to happen.
                    bool blink = selected && left < 0.5f && Mathf.Repeat((float)now * 10f, 1f) < 0.5f;
                    // D94: inside the Overcharge zone the dots go solid gold instead of the red
                    // blink, matching the HUD: there it means "fire now", not "about to lose it".
                    bool zone = pk.IsOvercharged(sim.Stats.Power);
                    dot.color = zone ? OverchargeGold : blink ? RejectColor : selected ? ReturnedColor : new Color(0.4f, 0.55f, 0.7f);
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

        void SpawnTimeNumber(float delta, Vector2 at)
        {
            // No "s" suffix (D66), shown x10 (D100): life reads as a health bar, not a timer, so
            // the number is whole health points. The sim still counts it in seconds.
            // A change that rounds to 0 points is skipped, so tiny gains never leave a "0" behind.
            if (LifeDisplay.Points(delta) == 0) return;
            SpawnNumber(LifeDisplay.Signed(delta), delta < 0 ? RejectColor : ReturnedColor, at);
        }

        // D99/R17f: lifesteal gets its own muted red so a heal from hitting reads differently
        // from a kill's reward; shown x10 (D100), and skipped when it rounds to nothing.
        static readonly Color StolenColor = new Color(1f, 0.45f, 0.5f);
        void SpawnStolenNumber(float seconds, Vector2 at)
        {
            if (LifeDisplay.Points(seconds) <= 0) return;
            SpawnNumber(LifeDisplay.Signed(seconds), StolenColor, at);
        }

        /// <summary>A floating world-space number/label that rises and fades (life changes, D94 Overcharge).</summary>
        void SpawnNumber(string label, Color color, Vector2 at)
        {
            var go = new GameObject("TimeChange");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMesh>();
            text.font = UI.Ui.Font;
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 48;
            text.characterSize = 0.055f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = label;
            timeNumbers.Add(new TimeNumber { Text = text, Origin = Geometry2D.ToWorld(at, 1.5f) });
        }

        void RenderTimeNumbers()
        {
            // Feedback uses real time while gameplay pauses, but belongs to this disposable
            // view: restarting can never leave a previous run's loss hovering over the player.
            for (int i = timeNumbers.Count - 1; i >= 0; i--)
            {
                var number = timeNumbers[i];
                number.Age += Time.unscaledDeltaTime;
                if (number.Age >= 0.8f)
                {
                    Destroy(number.Text.gameObject);
                    timeNumbers.RemoveAt(i);
                    continue;
                }
                number.Text.transform.position = number.Origin + Vector3.up * number.Age;
                if (cam != null) number.Text.transform.rotation = cam.transform.rotation;
                var color = number.Text.color;
                color.a = 1f - number.Age / 0.8f;
                number.Text.color = color;
            }
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
