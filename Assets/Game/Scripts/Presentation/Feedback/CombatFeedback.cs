using System;
using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// The feedback director (D7). It listens to one run's sim, asks FeedbackPolicy what each
    /// moment looks like, and executes the answer: sheets, shake and hit-stop (through the
    /// host), callouts, recoil and impact frames. It never decides a look itself, so retuning
    /// stays in the policy table. It is owned and disposed by ArenaView, so like the view it
    /// dies with its run and nothing can leak into the next one.
    /// </summary>
    public sealed class CombatFeedback : IDisposable
    {
        const float AirHeight = 0.55f;     // ArenaView's ProjectileHeight: sparks sit where shots fly
        const float GroundHeight = 0.04f;

        readonly ArenaSim sim;
        readonly IFeedbackHost host;
        readonly Func<int, CharacterView> enemyView;
        readonly CharacterView player;
        readonly WorldArtLibrary art;
        readonly Transform root;
        readonly WorldEffects effects;
        readonly CalloutText callouts;
        readonly ImpactFrame impact;
        readonly PierceTracker pierce = new PierceTracker();
        // Every handler this director added to the sim's events, as its own removal. Disposing
        // runs them all, so a disposed director can never react to a sim that outlives it
        // (final review minor: Dispose used to leave every handler attached).
        readonly List<Action> unsubscribe = new List<Action>();

        // Parting Gift raises PartingGiftBurst, then its own Explosion: swallow exactly that one.
        bool skipNextExplosion;
        // KillChainChanged fires inside the sim's own EnemyKilled handler, which was subscribed
        // first, so the length arrives just before our EnemyKilled, which knows the position.
        int pendingChain;
        // Where the latest blast went off. Explosions are raised BEFORE their damage
        // (ExplosionResolver), so the enemies it hits can recoil away from its centre.
        Vector2? lastBlastAt;

        public int LiveEffects => effects.ActiveCount;
        public CalloutText Callouts => callouts;
        public ImpactFrame Impact => impact;
        public int PierceKeys => pierce.Count;

        // ---- Passive cues (D3): only while their action is happening, never permanent.
        public const float GhostSeconds = 0.25f;
        public const int DashGhosts = 3;
        // Grace windows are fractions of a second. Anything this long is god mode or a test
        // rig, and a permanent shimmer would be noise, not information.
        public const double ShimmerHorizon = 5.0;
        // Sim seconds between the sweep's dirt kicks: ~6 along a typical swing, enough to draw
        // the blade's path without flooding the 64-effect pool.
        public const double SweepDustInterval = 0.06;

        sealed class Ghost { public SpriteRenderer Renderer; public float Age, Life, Alpha; public Vector2 Dir; }
        readonly List<Ghost> ghosts = new List<Ghost>();
        SpriteRenderer shimmer, glint;
        Material spriteMaterial;   // the default sprite material, for ghosts that are not silhouettes
        double lastNow;
        // Dash afterimages are dropped DURING the dash at the player's real position, so they
        // trail behind. (The first pass laid all three out along the dash at its start, which
        // put copies ahead of the player, where it had not been yet.)
        double nextDashGhostAt = double.PositiveInfinity;
        int dashGhostsLeft;
        // The boss sweep being dressed, by actor id (-1: none), and when its next dirt kick is due.
        int sweepingBoss = -1;
        double nextSweepDustAt;

        public int LiveGhosts { get { int n = 0; foreach (var g in ghosts) if (g.Renderer.enabled) n++; return n; } }
        internal IEnumerable<SpriteRenderer> LiveGhostRenderers { get { foreach (var g in ghosts) if (g.Renderer.enabled) yield return g.Renderer; } }
        public bool ShimmerVisible => shimmer.enabled;
        public bool GlintVisible => glint.enabled;

        /// <summary>Dash recovery: true only on the frame the cooldown ends (never at run start).</summary>
        public static bool DashReadyCrossed(double before, double now, double readyAt) => before < readyAt && now >= readyAt;

        public static bool ShowsShimmer(PlayerActor p, double now) =>
            p.IsInvulnerable(now) && Math.Max(p.InvulnerableUntil, p.DashInvulnerableUntil) - now < ShimmerHorizon;

        /// <summary>Quick Draw's post-swap window is open (the same test the sim's multiplier uses).</summary>
        public static bool QuickDrawOpen(ArenaSim sim) =>
            sim.Stats.QuickDrawBonus > 0f && sim.Clock.Now - sim.LastSwapAt <= sim.Stats.QuickDrawWindow + 1e-6;

        public CombatFeedback(ArenaSim sim, Transform parent, WorldArtLibrary art, IFeedbackHost host,
            Func<int, CharacterView> enemyView, CharacterView player)
        {
            this.sim = sim; this.art = art; this.host = host; this.enemyView = enemyView; this.player = player;
            root = new GameObject("CombatFeedback").transform;
            root.SetParent(parent, false);
            effects = new WorldEffects(root, art, "FeedbackEffects");
            callouts = new CalloutText(root);
            impact = new ImpactFrame();
            Subscribe();
            lastNow = sim.Clock.Now;
            shimmer = Overlay("GraceShimmer", 7);
            glint = Overlay("QuickDrawGlint", 8);
            spriteMaterial = shimmer.sharedMaterial;
        }

        /// <summary>Attach a handler and remember how to detach it.</summary>
        void Hook<T>(Action<T> add, Action<T> remove, T handler) where T : Delegate
        {
            add(handler);
            unsubscribe.Add(() => remove(handler));
        }

        void Subscribe()
        {
            var ev = sim.Events;
            Hook<Action<ProjectileActor, ProjectileEndReason>>(h => ev.ProjectileEnded += h, h => ev.ProjectileEnded -= h, OnShotEnded);
            Hook<Action<EnemyActor>>(h => ev.EnemyFired += h, h => ev.EnemyFired -= h,
                e => { if (!e.IsBoss) Play(CueEvent.Muzzle, e.Position); });
            Hook<Action<EnemyActor, DamageEvent>>(h => ev.EnemyDamaged += h, h => ev.EnemyDamaged -= h, OnEnemyDamaged);
            Hook<Action<EnemyActor, DamageEvent>>(h => ev.EnemyKilled += h, h => ev.EnemyKilled -= h, OnEnemyKilled);
            Hook<Action<int, int>>(h => ev.PlayerHit += h, h => ev.PlayerHit -= h,
                (_, __) => Play(CueEvent.PlayerHit, sim.Player.Position));
            Hook<Action<Vector2, float, AttackFaction>>(h => ev.Explosion += h, h => ev.Explosion -= h, OnExplosion);
            Hook<Action<Vector2, float>>(h => ev.PartingGiftBurst += h, h => ev.PartingGiftBurst -= h, (at, r) =>
            {
                skipNextExplosion = true;
                lastBlastAt = at;
                Play(CueEvent.PartingGift, at, new CueContext { Radius = r });
            });
            Hook<Action<EnemyActor, Vector2>>(h => ev.StrikeParried += h, h => ev.StrikeParried -= h,
                (e, at) => Play(CueEvent.Parry, at, default, Enemy(e), player));
            Hook<Action<CapturedPacket, AttackSnapshot, Vector2, CaptureResult>>(h => ev.ShotCaptured += h, h => ev.ShotCaptured -= h,
                (_, shot, at, __) =>
                {
                    if (shot.Perfect) Play(CueEvent.PerfectCatch, at, new CueContext { FinalSecond = sim.Has(UpgradeId.FinalSecond) }, player);
                });
            Hook<Action<CapturedPacket, int>>(h => ev.PacketOvercharged += h, h => ev.PacketOvercharged -= h, (pk, _) =>
                Play(CueEvent.Overcharge, sim.Player.Position, new CueContext { Power = pk.FirePower(sim.Stats.Power) }, player));
            Hook<Action<CapturedPacket>>(h => ev.PacketBackfired += h, h => ev.PacketBackfired -= h,
                _ => Play(CueEvent.Backfire, sim.Player.Position));
            Hook<Action<CapturedPacket, CapturedPacket>>(h => ev.PacketsFused += h, h => ev.PacketsFused -= h,
                (_, __) => Play(CueEvent.Fusion, sim.Player.Position));
            Hook<Action<int, float>>(h => ev.KillChainChanged += h, h => ev.KillChainChanged -= h, (length, _) => pendingChain = length);
            Hook<Action<Vector2>>(h => ev.OverflowFired += h, h => ev.OverflowFired -= h, at => Play(CueEvent.Overflow, at));
            // The spark sits at the muzzle, half a unit along the aim.
            Hook<Action<Vector2>>(h => ev.QuickDrawFired += h, h => ev.QuickDrawFired -= h,
                at => Play(CueEvent.QuickDraw, at + sim.Player.AimDirection * 0.5f));
            Hook<Action<float, Vector2>>(h => ev.LifeStolen += h, h => ev.LifeStolen -= h,
                (_, __) => Play(CueEvent.LifeStolen, sim.Player.Position));
            Hook<Action<Vector2, Vector2>>(h => ev.Dashed += h, h => ev.Dashed -= h, OnDashed);
            Hook<Action<int>>(h => ev.EchoFired += h, h => ev.EchoFired -= h, OnEchoFired);
        }

        CharacterView Enemy(EnemyActor e) => e != null ? enemyView(e.ActorId) : null;

        void OnShotEnded(ProjectileActor p, ProjectileEndReason why)
        {
            pierce.Forget(p.RootReleaseId, p.Shot.ShotId, p.IsEcho);
            var c = new CueContext { StateColor = ProjectileSkins.State(p.Faction, p.Shot.Kind, p.Shot.Overcharged) };
            switch (why)
            {
                case ProjectileEndReason.Expired: Play(CueEvent.ShotExpired, p.Position, c); break;
                case ProjectileEndReason.HitWall: Play(CueEvent.ShotHitWall, p.Position, c); break;
                case ProjectileEndReason.Captured: Play(CueEvent.ShotCaptured, p.Position, c); break;
                // HitActor: the hit's own cue covers it. Cleared: the arena reset, nothing to say.
            }
        }

        void OnEnemyDamaged(EnemyActor e, DamageEvent d)
        {
            // The boss's spark and hurt pose already live in WorldPresentation; add only the weight.
            if (e.IsBoss) { Play(CueEvent.BossHit, e.Position); return; }
            var context = new CueContext { School = d.SourceCategory };
            Cue cue;
            Vector2? arcAt = null;
            if (d.Category == DamageCategory.Orbit) cue = FeedbackPolicy.For(CueEvent.OrbitHit, context);
            else if (pierce.Hit(d, e.Position, out var previous))
            {
                cue = FeedbackPolicy.For(CueEvent.PierceHit, context);
                arcAt = (previous + e.Position) * 0.5f;
                // The arc spans the two enemies (a uniform-scale sheet, so this is a first pass).
                cue.Size2 = Mathf.Clamp(Vector2.Distance(previous, e.Position), 0.6f, 3f);
            }
            else cue = FeedbackPolicy.For(CueEvent.EnemyHit, context);
            Play(cue, e.Position, arcAt);
            var view = Enemy(e);
            if (cue.Recoil && view != null) view.Recoil(RecoilDirection(e, d));
        }

        /// <summary>
        /// Which way a hit pushes the sprite. DamageEvent carries no direction, so it is found:
        /// a blast pushes away from its centre, a shot pushes along its flight (final review
        /// minor: the first pass always pushed away from the player, which is wrong for a shot
        /// curving in from the side or a blast behind the enemy). Away from the player remains
        /// the fallback, e.g. when the shot ended on this very hit and is no longer listed.
        /// </summary>
        internal Vector2 RecoilDirection(EnemyActor e, DamageEvent d)
        {
            if ((d.Category == DamageCategory.Explosion || d.Category == DamageCategory.PartingGift) && lastBlastAt.HasValue)
            {
                var away = e.Position - lastBlastAt.Value;
                if (away.sqrMagnitude > 1e-6f) return away;   // at the very centre: no side to push to
            }
            if (d.ShotId != 0)
                foreach (var p in sim.Projectiles)
                    if (p.Active && p.Shot.ShotId == d.ShotId) return p.Direction;
            return e.Position - sim.Player.Position;
        }

        void OnEnemyKilled(EnemyActor e, DamageEvent d)
        {
            if (e.IsBoss) { Play(CueEvent.BossKill, e.Position, default, Enemy(e)); pendingChain = 0; return; }
            Play(CueEvent.Kill, e.Position);
            if (pendingChain >= 3) Play(CueEvent.Chain, e.Position, new CueContext { ChainLength = pendingChain });
            pendingChain = 0;
        }

        void OnExplosion(Vector2 at, float radius, AttackFaction faction)
        {
            lastBlastAt = at;
            if (skipNextExplosion) { skipNextExplosion = false; return; }
            // Playtest: the Collector's slam reuses the hostile Explosion event, so it was drawn
            // as a fiery Blast, and a slam is a blow to the ground, not an explosion. Recognised
            // here (the boss mid-slam, centred on the burst) and given its own dust-and-dirt cue.
            if (faction == AttackFaction.Hostile && SlammingBossAt(at))
            {
                Play(CueEvent.BossSlam, at, new CueContext { Radius = radius });
                return;
            }
            Play(CueEvent.Explosion, at, new CueContext { Radius = radius });
        }

        bool SlammingBossAt(Vector2 at)
        {
            foreach (var e in sim.Enemies)
                if (e.IsBoss && e.Boss != null && e.Boss.Pattern == BossPattern.Slam && (e.Position - at).sqrMagnitude < 0.01f)
                    return true;
            return false;
        }

        void OnEchoFired(int releaseId)
        {
            // Echo: a faint copy of each echoed shot, in its own shape, where it starts, so the
            // echo reads as "that shot, again" (final review minor: the first pass drew one
            // Arcane Orb at the player whatever the shot was). The echo projectiles are already
            // spawned when the event is raised.
            foreach (var p in sim.Projectiles)
            {
                if (!p.Active || !p.IsEcho || p.RootReleaseId != releaseId) continue;
                string sheet = ProjectileSkins.Sheet(p.Shot.SourceCategory, p.Shot.Kind, p.Shot.SourceSpreadHalfAngle);
                var frames = art.Effect(sheet);
                if (frames.Length == 0) continue;
                AddGhost(frames[0], Geometry2D.ToWorld(p.Position, AirHeight), p.Radius * ProjectileSkins.SpriteScaleFor(sheet),
                    FeedbackColors.Returned, 0.5f, 0.2f, null, false, p.Direction);
            }
        }

        public void Play(CueEvent e, Vector2 at, CueContext context = default, params CharacterView[] actors) =>
            Play(FeedbackPolicy.For(e, context), at, null, actors);

        public void Play(Cue cue, Vector2 at, Vector2? secondAt, params CharacterView[] actors)
        {
            // One place applies Reduce flashes, so no output can forget it.
            cue = FeedbackPolicy.Filter(cue, DisplayOptions.ReduceFlashes);
            double now = sim.Clock.Now;
            float y = cue.Ground ? GroundHeight : AirHeight;
            if (cue.Sheet != null)
                effects.Spawn(cue.Sheet, Geometry2D.ToWorld(at, y), cue.Size, now, cue.Color, cue.Ground, false, 12f, cue.Cell > 0 ? cue.Cell : 32);
            if (cue.Sheet2 != null)
                effects.Spawn(cue.Sheet2, Geometry2D.ToWorld(secondAt ?? at, y), cue.Size2, now, cue.Color2, cue.Ground, false, 12f);
            if (cue.ShakeAmp > 0f) host?.Shake(cue.ShakeAmp, cue.ShakeDuration);
            if (cue.HitStop > 0f) host?.HitStop(cue.HitStop);
            if (cue.Callout != null) callouts.Show(cue.Callout, cue.CalloutColor, Geometry2D.ToWorld(at));
            if (cue.ImpactFrame) impact.Begin(cue.Color, actors);
        }

        /// <summary>Once per rendered frame, after the view has drawn the actors.</summary>
        public void Render(Camera cam, float unscaledDt)
        {
            // Sheets run on the sim clock, so they hold still during hit-stop, as the freeze intends.
            effects.Render(sim.Clock.Now, cam);
            callouts.Tick(unscaledDt, cam);
            impact.Tick();
            RenderPassives(cam, unscaledDt);
        }

        SpriteRenderer Overlay(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        void OnDashed(Vector2 from, Vector2 dir)
        {
            // Only arm the trail here; RenderPassives drops the copies as the dash happens. The
            // first copy is due at once, so even a dash shorter than a frame leaves one.
            nextDashGhostAt = sim.Clock.Now;
            dashGhostsLeft = DashGhosts;
        }

        void DropDashGhost(PlayerActor p, int index)
        {
            var sprite = player != null ? player.CurrentSprite : null;
            if (sprite == null) return;
            // A white-silhouette copy tinted cyan, facing the way the player faces, so it reads
            // as the player's own outline left behind. Older copies start fainter.
            AddGhost(sprite, Geometry2D.ToWorld(p.Position), player.Scale, new Color(0.6f, 0.9f, 1f), 0.5f - 0.12f * index,
                GhostSeconds, CharacterView.SilhouetteMaterial, player.FlipX, Vector2.zero);
        }

        void AddGhost(Sprite sprite, Vector3 at, float scale, Color color, float alpha, float life,
            Material material, bool flipX, Vector2 dir)
        {
            Ghost ghost = null;
            foreach (var g in ghosts) if (!g.Renderer.enabled) { ghost = g; break; }
            if (ghost == null)
            {
                if (ghosts.Count >= 16) return;   // a bounded pool, like WorldEffects
                ghost = new Ghost { Renderer = Overlay("Ghost", 0) };
                ghosts.Add(ghost);
            }
            // Set every pooled property each time: a reused ghost must not keep the last one's
            // material or flip (a silhouette echo orb, a mirrored dash copy).
            ghost.Renderer.sharedMaterial = material != null ? material : spriteMaterial;
            ghost.Renderer.flipX = flipX;
            ghost.Renderer.sprite = sprite;
            ghost.Renderer.transform.position = at;
            ghost.Renderer.transform.localScale = Vector3.one * scale;
            ghost.Age = 0f; ghost.Life = life; ghost.Alpha = alpha; ghost.Dir = dir;
            color.a = alpha;
            ghost.Renderer.color = color;
            ghost.Renderer.enabled = true;
        }

        void RenderPassives(Camera cam, float dt)
        {
            double now = sim.Clock.Now;
            var p = sim.Player;
            if (DashReadyCrossed(lastNow, now, p.DashReadyAt)) Play(CueEvent.DashReady, p.Position);
            lastNow = now;

            // Dash trail: one copy per DashDuration / DashGhosts of sim time while the dash runs,
            // at most one per frame so a long frame cannot stack copies on one spot.
            if (dashGhostsLeft > 0 && now >= nextDashGhostAt && (p.Dashing || dashGhostsLeft == DashGhosts))
            {
                DropDashGhost(p, DashGhosts - dashGhostsLeft);
                dashGhostsLeft--;
                nextDashGhostAt += Mathf.Max(0.01f, sim.Stats.DashDuration) / DashGhosts;
            }
            if (!p.Dashing && dashGhostsLeft < DashGhosts) dashGhostsLeft = 0;   // dash over: no stragglers

            RenderSweep(now);

            Loop(shimmer, ShowsShimmer(p, now), "Shield Bubble", Geometry2D.ToWorld(p.Position, 0.6f), 1.4f, new Color(0.6f, 0.9f, 1f, 0.35f), now, cam);
            Loop(glint, QuickDrawOpen(sim), "Charge Up", Geometry2D.ToWorld(p.Position + p.AimDirection * 0.4f, 0.7f), 0.6f, FeedbackColors.Riposte, now, cam);
            foreach (var g in ghosts)
            {
                if (!g.Renderer.enabled) continue;
                g.Age += dt;   // real time: ghosts are a trace of motion, gone even if the game pauses
                if (g.Age >= g.Life) { g.Renderer.enabled = false; continue; }
                var c = g.Renderer.color; c.a = g.Alpha * (1f - g.Age / g.Life); g.Renderer.color = c;
                if (cam != null)
                    g.Renderer.transform.rotation = g.Dir == Vector2.zero
                        ? cam.transform.rotation
                        : cam.transform.rotation * Quaternion.Euler(0f, 0f, ScreenAngle(cam, g.Renderer.transform.position, g.Dir));
            }
        }

        /// <summary>
        /// Playtest: the Collector's sweep had no effect of its own while the blade moved (the
        /// Wide Cleave plays when it ends). Now the swing starts with a small shake and the blade
        /// tip kicks up dirt along its arc, so the path it cut stays visible for a moment.
        /// Driven by the boss's own sweep state, so it follows the true blade, and on sim time,
        /// so it pauses with hit-stop.
        /// </summary>
        void RenderSweep(double now)
        {
            EnemyActor boss = null;
            foreach (var e in sim.Enemies)
                if (e.IsBoss && e.Alive && e.Boss != null && e.Boss.Stage == BossStage.Active && e.Boss.Pattern == BossPattern.Sweep) { boss = e; break; }
            if (boss == null) { sweepingBoss = -1; return; }
            if (sweepingBoss != boss.ActorId)
            {
                sweepingBoss = boss.ActorId;
                nextSweepDustAt = now;
                Play(CueEvent.BossSweep, boss.Position);
            }
            float reach = sim.Config.collector.sweepReach;
            Vector2 blade = Geometry2D.Rotate(boss.AimDirection, boss.Boss.BladeDeg);
            int guard = 0;   // a long hitch must not dump a burst; the pool is shared
            while (now >= nextSweepDustAt && guard++ < 3)
            {
                Play(CueEvent.SweepDust, boss.Position + blade * reach);
                nextSweepDustAt += SweepDustInterval;
            }
            if (now >= nextSweepDustAt) nextSweepDustAt = now + SweepDustInterval;
        }

        static float ScreenAngle(Camera cam, Vector3 world, Vector2 dir)
        {
            Vector3 a = cam.WorldToScreenPoint(world), b = cam.WorldToScreenPoint(world + Geometry2D.ToWorld(dir));
            return Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        }

        void Loop(SpriteRenderer sr, bool on, string sheet, Vector3 at, float scale, Color color, double now, Camera cam)
        {
            var frames = on ? art.Effect(sheet) : null;
            sr.enabled = on && frames.Length > 0;
            if (!sr.enabled) return;
            sr.sprite = frames[WorldArtPolicy.Frame(now, frames.Length, 12f, true)];
            sr.transform.position = at;
            sr.transform.localScale = Vector3.one * scale;
            sr.color = color;
            if (cam != null) sr.transform.rotation = cam.transform.rotation;
        }

        public void Dispose()
        {
            foreach (var off in unsubscribe) off();
            unsubscribe.Clear();
            impact.Dispose();
            callouts.Dispose();
            effects.Dispose();
            if (root != null) WorldArtLibrary.Release(root.gameObject);
        }
    }
}
