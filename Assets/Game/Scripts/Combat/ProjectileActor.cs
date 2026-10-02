using System.Collections.Generic;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// Live projectile state inside the sim. Pooled, so <see cref="Reset"/> must clear EVERY
    /// mutable field — a stale faction, hit set or provenance on a reused object would let a
    /// fresh enemy shot behave like an old returned one (Phase 2 check).
    /// </summary>
    public sealed class ProjectileActor
    {
        public bool Active;
        /// <summary>Unique per spawn within the run; a reused object gets a new one.</summary>
        public int ProjectileId;
        public AttackSnapshot Shot;
        public AttackFaction Faction;
        public Vector2 Position;
        /// <summary>Position at the start of the last tick, for view interpolation only.</summary>
        public Vector2 PrevPosition;
        public Vector2 Velocity;
        public double SpawnedAt;
        public double ExpireAt;

        /// <summary>The player release that launched it (0 for enemy shots). Echoes share it.</summary>
        public int RootReleaseId;
        public bool IsEcho;
        public float PowerMultiplier = 1f;
        /// <summary>Additional actors it may pass through after a hit (Piercing upgrade).</summary>
        public int PierceRemaining;
        /// <summary>Travel budget, independent of speed: shotgun pellets stop at their range.</summary>
        public float DistanceRemaining = float.PositiveInfinity;
        /// <summary>Actors already damaged, so piercing never hits the same target twice.</summary>
        public readonly HashSet<int> HitActors = new HashSet<int>();
        /// <summary>Catch activation that already rejected this shot (packet/slots full).</summary>
        public int CaptureRejectedActivation;

        public float Radius => Shot.Radius;
        public Vector2 Direction => Velocity.sqrMagnitude > 1e-12f ? Velocity.normalized : Vector2.up;

        public void Reset()
        {
            Active = false;
            ProjectileId = 0;
            Shot = default;
            Faction = AttackFaction.Hostile;
            Position = PrevPosition = Velocity = Vector2.zero;
            SpawnedAt = ExpireAt = 0;
            RootReleaseId = 0;
            IsEcho = false;
            PowerMultiplier = 1f;
            PierceRemaining = 0;
            DistanceRemaining = float.PositiveInfinity;
            HitActors.Clear();
            CaptureRejectedActivation = 0;
        }
    }

    /// <summary>Simple free-list pool. Rent always returns a fully reset object.</summary>
    public sealed class ProjectilePool
    {
        readonly Stack<ProjectileActor> free = new Stack<ProjectileActor>();
        public int FreeCount => free.Count;
        public int Created { get; private set; }

        public ProjectileActor Rent()
        {
            var p = free.Count > 0 ? free.Pop() : NewOne();
            p.Reset();
            return p;
        }

        ProjectileActor NewOne()
        {
            Created++;
            return new ProjectileActor();
        }

        public void Return(ProjectileActor p)
        {
            // Reset on the way in too, so a pooled object never holds references to old ids.
            p.Reset();
            free.Push(p);
        }
    }
}
