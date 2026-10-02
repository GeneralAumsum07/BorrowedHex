using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>Section 2 "Initial player tuning". Starting defaults, not playtested values.</summary>
    [Serializable]
    public class PlayerTuning
    {
        public float moveSpeed = 6f;
        // Health is counted in HALF HEARTS (owner direction: 5 hearts, half-heart contact
        // damage). Integers keep damage exact and deterministic; the HUD divides by two.
        [Tooltip("In half hearts: 10 = 5 hearts.")]
        public int maxHealth = 10;
        public float hitInvulnerability = 0.65f;
        [Tooltip("Invulnerability after a CONTACT hit (bumping an enemy body). Shorter than a "
                 + "hit's, so contact is a brief shove of danger rather than a free dash-through.")]
        public float contactInvulnerability = 0.5f;
        public float dashDistance = 2.9f;
        public float dashDuration = 0.18f;
        public float dashCooldown = 1.2f;
        public float dashInvulnerability = 0.12f;
        [Tooltip("Logical body radius on the gameplay plane; independent of sprite size.")]
        public float bodyRadius = 0.35f;
    }

    /// <summary>Section 2/3 capture defaults (the Snatcher baseline).</summary>
    [Serializable]
    public class CaptureTuning
    {
        public float range = 2.8f;
        [Tooltip("Total cone angle in degrees (half on each side of aim).")]
        public float coneAngle = 90f;
        public float window = 0.25f;
        public float recovery = 0.65f;
        public int packetCapacity = 12;
        public float packetLifetime = 3f;
        public int packetSlots = 2;
        [Tooltip("A shot is perfect if its path would reach the player's hitbox within this many seconds.")]
        public float perfectWindow = 0.10f;
        public float perfectBonus = 0.15f;

        // Parry band (D36, owner direction): a thin arc INSIDE the catch cone, not the whole
        // cone. It sits at 1.15 rather than at the cone's outer rim because a Pursuer strike
        // circle (radius 0.75, 0.8 ahead of its body) is only reachable by a 2.8 rim while the
        // player stands well outside the strike — a parry must mean standing in harm's way.
        // At 1.15 the band crosses the strike's far rim when the player is ~0.34-0.55 from the
        // strike centre, i.e. squarely inside the damage zone. Tunable without code changes.
        [Tooltip("Parry band: distance of the band's centre line from the player.")]
        public float parryRingRadius = 1.15f;
        [Tooltip("Parry band: full width of the band.")]
        public float parryRingWidth = 0.12f;
        [Tooltip("Parry is live only for this fraction of the catch window, from its start.")]
        public float parryWindowScale = 0.5f;
    }

    public partial class GameConfig
    {
        public PlayerTuning player = new PlayerTuning();
        public CaptureTuning capture = new CaptureTuning();
    }
}
