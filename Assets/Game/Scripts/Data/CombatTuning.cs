using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// Serialized, editable form of one attack type. Converted ONCE per run into an immutable
    /// <c>AttackDefinition</c>, so tweaking the asset in the inspector mid-run can never change
    /// a projectile that is already flying or a payload already stored in a packet.
    /// </summary>
    [Serializable]
    public class AttackTuning
    {
        public string id = "bolt";
        public AttackKind kind = AttackKind.Bolt;
        public float speed = 9f;
        [Tooltip("Logical collision radius on the gameplay plane.")]
        public float radius = 0.18f;
        [Tooltip("Damage when returned by the player against enemies.")]
        public int returnedDamage = 1;
        [Tooltip("Damage when it hits the player as a hostile shot (section 4: one player hit).")]
        public int hostileDamage = 1;
        [Tooltip("Packet capacity units (section 3: bullet 1, heavy 3, rocket 4).")]
        public int energyCost = 1;
        public float lifetime = 4f;
        public bool capturable = true;
        [Tooltip("0 = no explosion.")]
        public float explosionRadius;
        public int explosionDamage;
    }

    /// <summary>Section 4 roster numbers for the Bolt Acolyte. Starting defaults.</summary>
    [Serializable]
    public class AcolyteTuning
    {
        public int health = 3;
        public float bodyRadius = 0.45f;
        public float moveSpeed = 2.2f;
        [Tooltip("Keeps between these distances from the player.")]
        public float preferredMin = 5f;
        public float preferredMax = 9f;
        [Tooltip("Seconds the aim line tracks the player before firing.")]
        public float telegraph = 0.75f;
        [Tooltip("Final part of the telegraph where aim is locked, so a sidestep is a real dodge.")]
        public float aimLock = 0.25f;
        public float cooldown = 1.8f;
        [Tooltip("First shot after spawning waits this long beyond the spawn warning.")]
        public float firstShotDelay = 0.6f;
        public float[] volleySpreadDeg = { -8f, 0f, 8f };
    }

    /// <summary>Section 3 arcane lantern: pair of slow capturable bolts.</summary>
    [Serializable]
    public class LanternTuning
    {
        public float interval = 2f;
        public float starvationDelay = 2f;
        public float[] pairSpreadDeg = { -10f, 10f };
    }

    [Serializable]
    public class CombatTuning
    {
        [Tooltip("Seconds an enemy is visible as a harmless spawn warning before it acts.")]
        public float spawnWarning = 0.8f;
        [Tooltip("Enemies never spawn closer than this to the player.")]
        public float minSpawnDistance = 5f;

        public List<AttackTuning> attacks = new List<AttackTuning>
        {
            new AttackTuning { id = AttackIds.Bolt },
            new AttackTuning { id = AttackIds.LanternBolt, speed = 4.5f, radius = 0.2f, lifetime = 6f },
            new AttackTuning
            {
                id = AttackIds.Rocket, kind = AttackKind.Rocket, speed = 6f, radius = 0.28f,
                returnedDamage = 0, energyCost = 4, lifetime = 5f, explosionRadius = 1.6f, explosionDamage = 5,
            },
        };

        public AcolyteTuning acolyte = new AcolyteTuning();
        public LanternTuning lantern = new LanternTuning();
    }

    /// <summary>String IDs shared by data, snapshots, stats and save files.</summary>
    public static class AttackIds
    {
        public const string Bolt = "bolt";
        public const string LanternBolt = "lantern_bolt";
        public const string Rocket = "rocket";
    }

    public partial class GameConfig
    {
        public CombatTuning combat = new CombatTuning();
    }
}
