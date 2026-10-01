using System;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>Section 2 "Initial player tuning". Starting defaults, not playtested values.</summary>
    [Serializable]
    public class PlayerTuning
    {
        public float moveSpeed = 6f;
        public int maxHealth = 3;
        public float hitInvulnerability = 0.65f;
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
    }

    public partial class GameConfig
    {
        public PlayerTuning player = new PlayerTuning();
        public CaptureTuning capture = new CaptureTuning();
    }
}
