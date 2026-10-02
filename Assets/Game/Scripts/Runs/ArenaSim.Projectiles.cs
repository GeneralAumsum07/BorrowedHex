using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    public enum ProjectileEndReason { Expired, HitWall, HitActor, Captured, Cleared }

    /// <summary>
    /// The single authoritative projectile-resolution path (section 3). Each tick a projectile
    /// sweeps its whole travelled segment and the EARLIEST contact wins, so:
    ///   - a fast shot that would tunnel through the player in one step still hits;
    ///   - a wall in front of an actor blocks the later actor impact;
    ///   - at equal time, capture beats wall beats actor (capture-wins rule, Phase 3).
    /// A projectile is removed from play the instant it ends, so it can never damage twice.
    /// </summary>
    public sealed partial class ArenaSim
    {
        public readonly List<ProjectileActor> Projectiles = new List<ProjectileActor>();
        public readonly ProjectilePool ProjectilePool = new ProjectilePool();

        enum HitKind { None = -1, Capture = 0, Wall = 1, Actor = 2 }

        public ProjectileActor SpawnProjectile(in AttackSnapshot shot, AttackFaction faction, Vector2 position,
            Vector2 direction, int rootReleaseId = 0, bool isEcho = false, float power = 1f, int pierce = 0,
            float maxDistance = float.PositiveInfinity, float perfectMultiplier = 1f)
        {
            var p = ProjectilePool.Rent();
            p.Active = true;
            p.ProjectileId = Ids.Next();
            p.Shot = shot;
            p.Faction = faction;
            p.Position = p.PrevPosition = position;
            if (direction.sqrMagnitude < 1e-8f) direction = Vector2.up;
            p.Velocity = direction.normalized * shot.Speed;
            p.SpawnedAt = Clock.Now;
            p.ExpireAt = Clock.Now + shot.Lifetime;
            p.RootReleaseId = rootReleaseId;
            p.IsEcho = isEcho;
            p.PowerMultiplier = power;
            p.PerfectMultiplier = perfectMultiplier;
            p.PierceRemaining = pierce;
            p.DistanceRemaining = maxDistance;
            Projectiles.Add(p);
            Events.RaiseProjectileSpawned(p);
            return p;
        }

        void TickProjectiles(double now, float dt)
        {
            // Index loop: shots spawned during resolution (none yet; echoes are scheduled)
            // would simply be processed this tick too.
            for (int i = 0; i < Projectiles.Count; i++)
            {
                var p = Projectiles[i];
                if (!p.Active) continue;
                p.PrevPosition = p.Position;
                if (now >= p.ExpireAt)
                {
                    EndProjectile(p, ProjectileEndReason.Expired);
                    continue;
                }
                Vector2 travel = p.Velocity * dt;
                float length = travel.magnitude;
                float allowed = Mathf.Min(length, p.DistanceRemaining);
                if (length > 1e-8f) travel *= allowed / length;
                ResolveSegment(p, p.Position, p.Position + travel);
                p.DistanceRemaining -= allowed;
                if (p.Active && p.DistanceRemaining <= 1e-6f) EndProjectile(p, ProjectileEndReason.Expired);
            }
            CompactProjectiles();
        }

        void ResolveSegment(ProjectileActor p, Vector2 start, Vector2 end)
        {
            // Bounded loop: each iteration either ends the projectile or consumes one pierce.
            for (int guard = 0; guard < 16; guard++)
            {
                float bestT = float.MaxValue;
                HitKind best = HitKind.None;
                EnemyActor bestEnemy = null;

                void Consider(float t, HitKind kind, EnemyActor enemy)
                {
                    // Earliest wins; on an exact tie the lower HitKind (capture < wall < actor) wins.
                    if (t < bestT - 1e-6f || (Mathf.Abs(t - bestT) <= 1e-6f && kind < best))
                    {
                        bestT = t;
                        best = kind;
                        bestEnemy = enemy;
                    }
                }

                foreach (var w in Walls)
                    if (Geometry2D.SweepCircleVsRect(start, end, p.Radius, w, out float tw)) Consider(tw, HitKind.Wall, null);

                if (p.Faction == AttackFaction.Hostile)
                {
                    // Invulnerable players (dash / post-hit) let hostile shots pass through: a
                    // dash THROUGH a volley is a deliberate, readable skill expression.
                    float tp = -1f;
                    if (Player.Alive && !Player.IsInvulnerable(Clock.Now)
                        && Geometry2D.SweepCircleVsCircle(start, end, p.Radius, Player.Position, Player.Radius, out tp))
                        Consider(tp, HitKind.Actor, null);
                    else tp = -1f;

                    // Capture is tested along the same segment, including the exact impact
                    // time, so an interception that coincides with a hit wins (section 3).
                    // Daredevil swaps the cone for its dash path (D83); everything after the
                    // region test (packets, slots, Overflow/Fusion, rejection) is shared.
                    float tc = 0f;
                    if (CanAttemptCapture(p)
                        && (Stats.CatchIsDash
                            ? DashSweepEntry(start, end, p.Radius, tp, out tc)
                            : CaptureGeometry.EarliestEntry(Player.Position, Player.AimDirection, Stats.CaptureConeAngle * 0.5f,
                                Stats.CaptureRange, start, end, p.Velocity, p.Radius, tp, out tc)))
                        Consider(tc, HitKind.Capture, null);
                }
                else
                {
                    foreach (var e in Enemies)
                    {
                        if (!e.IsActive(Clock.Now) || p.HitActors.Contains(e.ActorId)) continue;
                        if (Geometry2D.SweepCircleVsCircle(start, end, p.Radius, e.Position, e.Radius, out float te))
                            Consider(te, HitKind.Actor, e);
                    }
                }

                if (best == HitKind.None)
                {
                    p.Position = end;
                    return;
                }

                Vector2 hitPoint = Vector2.Lerp(start, end, bestT);
                p.Position = hitPoint;

                if (best == HitKind.Capture)
                {
                    if (TryCaptureProjectile(p)) return;
                    // Rejected: it keeps flying. Re-resolve the same segment without capture
                    // (CanAttemptCapture is now false for this projectile and activation).
                    p.Position = start;
                    continue;
                }

                if (best == HitKind.Wall)
                {
                    // Rockets burst on the first thing they touch, walls included, so a returned
                    // rocket aimed at a pillar still clears the crowd around it.
                    if (p.Shot.Explodes) ExplosionResolver.Explode(this, p, hitPoint);
                    EndProjectile(p, ProjectileEndReason.HitWall);
                    return;
                }

                if (p.Faction == AttackFaction.Hostile)
                {
                    // Section 4: incoming versions deal one player hit, rockets included. The
                    // hostile burst is visual only (see ExplosionResolver).
                    DamagePlayer(p.Shot.HostileDamage, p.Shot.SourceActorId);
                    if (p.Shot.Explodes) ExplosionResolver.Explode(this, p, hitPoint);
                    EndProjectile(p, ProjectileEndReason.HitActor);
                    return;
                }

                if (p.Shot.Explodes)
                {
                    // A returned rocket's damage is ALL in the burst (direct damage is 0), and it
                    // never pierces: one impact, one explosion, each enemy damaged at most once.
                    ExplosionResolver.Explode(this, p, hitPoint);
                    EndProjectile(p, ProjectileEndReason.HitActor);
                    return;
                }

                p.HitActors.Add(bestEnemy.ActorId);
                DamageEnemy(bestEnemy, ReturnedDamageOf(p), p.IsEcho ? DamageCategory.Echo : DamageCategory.ReturnedProjectile,
                    p.Shot, p.RootReleaseId);
                if (p.PierceRemaining > 0)
                {
                    // Carry on from the hit point with the rest of this tick's travel.
                    p.PierceRemaining--;
                    start = hitPoint;
                    continue;
                }
                EndProjectile(p, ProjectileEndReason.HitActor);
                return;
            }
        }

        /// <summary>Returned-shot damage: base x perfect bonus x power (power includes any echo fraction).</summary>
        float ReturnedDamageOf(ProjectileActor p) => ScaledReturnedDamage(p, p.Shot.ReturnedDamage);

        /// <summary>Apply the projectile's power and per-payload perfect bonus to a base amount.</summary>
        internal float ScaledReturnedDamage(ProjectileActor p, float baseAmount)
        {
            // The perfect multiplier was fixed at release, so Final Second expiring mid-flight
            // cannot change a shot already fired.
            return baseAmount * p.PerfectMultiplier * p.PowerMultiplier;
        }

        void EndProjectile(ProjectileActor p, ProjectileEndReason reason)
        {
            if (!p.Active) return;
            p.Active = false;
            Events.RaiseProjectileEnded(p, reason);
        }

        /// <summary>Remove ended projectiles and return them to the pool (order kept stable).</summary>
        void CompactProjectiles()
        {
            int w = 0;
            for (int r = 0; r < Projectiles.Count; r++)
            {
                var p = Projectiles[r];
                if (p.Active) Projectiles[w++] = p;
                else ProjectilePool.Return(p);
            }
            Projectiles.RemoveRange(w, Projectiles.Count - w);
        }

        /// <summary>End every live projectile (death, restart, boss transition).</summary>
        public void ClearProjectiles()
        {
            foreach (var p in Projectiles) EndProjectile(p, ProjectileEndReason.Cleared);
            CompactProjectiles();
        }

        public int CountProjectiles(AttackFaction faction)
        {
            int n = 0;
            foreach (var p in Projectiles) if (p.Active && p.Faction == faction) n++;
            return n;
        }
    }
}
