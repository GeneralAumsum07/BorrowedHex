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

        /// <summary>Scatter Caster's chosen firing spot for the current reposition.</summary>
        public Vector2 MoveTarget;
        public bool HasMoveTarget;

        /// <summary>Score for killing it (section 6), resolved at spawn including the elite factor.</summary>
        public int KillValue;
        /// <summary>
        /// True only when damage took it to zero health. A despawned enemy is also not Alive but
        /// stays Killed == false: section 6 says despawning never counts as a kill, so reward
        /// code must test this (or the kill event), never just "no longer alive".
        /// </summary>
        public bool Killed;

        public bool IsActive(double now) => Alive && now >= ActiveAt;
    }
}
