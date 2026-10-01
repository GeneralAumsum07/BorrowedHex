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
            Vector2 direction, int rootReleaseId = 0, bool isEcho = false, float power = 1f, int pierce = 0)
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
            p.PierceRemaining = pierce;
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
                ResolveSegment(p, p.Position, p.Position + p.Velocity * dt);
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
                    // Phase 3 adds the capture sweep here (HitKind.Capture), ahead of the player.
                    // Invulnerable players (dash / post-hit) let hostile shots pass through: a
                    // dash THROUGH a volley is a deliberate, readable skill expression.
                    if (Player.Alive && !Player.IsInvulnerable(Clock.Now)
                        && Geometry2D.SweepCircleVsCircle(start, end, p.Radius, Player.Position, Player.Radius, out float tp))
                        Consider(tp, HitKind.Actor, null);
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

                if (best == HitKind.Wall)
                {
                    EndProjectile(p, ProjectileEndReason.HitWall);
                    return;
                }

                if (p.Faction == AttackFaction.Hostile)
                {
                    DamagePlayer(p.Shot.HostileDamage, p.Shot.SourceActorId);
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

        /// <summary>Returned-shot damage: base x power (upgrades) x perfect bonus (Phase 6).</summary>
        float ReturnedDamageOf(ProjectileActor p)
        {
            float dmg = p.Shot.ReturnedDamage * p.PowerMultiplier;
            if (p.Shot.Perfect) dmg *= 1f + Stats.PerfectBonus;
            return dmg;
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
