using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    public enum EnemyPhase { Warning, Idle, Telegraph, Recover }

    /// <summary>
    /// Sim-side enemy state. One class for every enemy kind; behaviour lives in per-kind static
    /// brains keyed by <see cref="Category"/>, which keeps the roster data-driven and lets the
    /// elite variant reuse a base brain with one modifier (section 4).
    /// </summary>
    public sealed class EnemyActor
    {
        public int ActorId;
        public ActorCategory Category;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public float Radius;
        // Float so fractional bonuses (perfect catch, power) accumulate instead of rounding away.
        public float Health;
        public float MaxHealth;
        public bool Alive = true;
        public bool Elite;

        public double SpawnedAt;
        /// <summary>Before this time the enemy is a harmless spawn warning (section 4).</summary>
        public double ActiveAt;

        public EnemyPhase Phase = EnemyPhase.Warning;
        public double PhaseEndsAt;
        /// <summary>Current aim (telegraph direction); locked for the final part of a telegraph.</summary>
        public Vector2 AimDirection = Vector2.down;
        public bool AimLocked;
        /// <summary>+1/-1 strafe preference so a group does not all orbit the same way.</summary>
        public float StrafeSign = 1f;

        public bool IsActive(double now) => Alive && now >= ActiveAt;
    }
}
