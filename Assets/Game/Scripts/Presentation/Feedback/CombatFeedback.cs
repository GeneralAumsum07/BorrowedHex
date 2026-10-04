using System;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
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

        // Parting Gift raises PartingGiftBurst, then its own Explosion: swallow exactly that one.
        bool skipNextExplosion;
        // KillChainChanged fires inside the sim's own EnemyKilled handler, which was subscribed
        // first, so the length arrives just before our EnemyKilled, which knows the position.
        int pendingChain;

        public int LiveEffects => effects.ActiveCount;
        public CalloutText Callouts => callouts;
        public ImpactFrame Impact => impact;
        public int PierceKeys => pierce.Count;

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
        }

        void Subscribe()
        {
            var ev = sim.Events;
            ev.ProjectileEnded += OnShotEnded;
            ev.EnemyFired += e => { if (!e.IsBoss) Play(CueEvent.Muzzle, e.Position); };
            ev.EnemyDamaged += OnEnemyDamaged;
            ev.EnemyKilled += OnEnemyKilled;
            ev.PlayerHit += (_, __) => Play(CueEvent.PlayerHit, sim.Player.Position);
            ev.Explosion += OnExplosion;
            ev.PartingGiftBurst += (at, r) => { skipNextExplosion = true; Play(CueEvent.PartingGift, at, new CueContext { Radius = r }); };
            ev.StrikeParried += (e, at) => Play(CueEvent.Parry, at, default, Enemy(e), player);
            ev.ShotCaptured += (_, shot, at, __) =>
            {
                if (shot.Perfect) Play(CueEvent.PerfectCatch, at, new CueContext { FinalSecond = sim.Has(UpgradeId.FinalSecond) }, player);
            };
            ev.PacketOvercharged += (pk, _) =>
                Play(CueEvent.Overcharge, sim.Player.Position, new CueContext { Power = pk.FirePower(sim.Stats.Power) }, player);
            ev.PacketBackfired += _ => Play(CueEvent.Backfire, sim.Player.Position);
            ev.PacketsFused += (_, __) => Play(CueEvent.Fusion, sim.Player.Position);
            ev.KillChainChanged += (length, _) => pendingChain = length;
            ev.OverflowFired += at => Play(CueEvent.Overflow, at);
            // The spark sits at the muzzle, half a unit along the aim.
            ev.QuickDrawFired += at => Play(CueEvent.QuickDraw, at + sim.Player.AimDirection * 0.5f);
            ev.LifeStolen += (_, __) => Play(CueEvent.LifeStolen, sim.Player.Position);
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
            // DamageEvent carries no direction. Returned fire comes from the player's side, so
            // "away from the player" is the push it reads as (inferred; checked in the capture).
            if (cue.Recoil && view != null) view.Recoil(e.Position - sim.Player.Position);
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
            if (skipNextExplosion) { skipNextExplosion = false; return; }
            Play(CueEvent.Explosion, at, new CueContext { Radius = radius });
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
        }

        public void Dispose()
        {
            impact.Dispose();
            callouts.Dispose();
            effects.Dispose();
            if (root != null) WorldArtLibrary.Release(root.gameObject);
        }
    }
}
