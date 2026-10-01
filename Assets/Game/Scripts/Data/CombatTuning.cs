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

    /// <summary>
    /// Section 4 roster numbers. One shape for every ordinary enemy: the brains differ in
    /// behaviour, not in what they need to be tuned by, so a designer edits the same fields
    /// for each kind. Fields a brain does not use are simply ignored (e.g. strike* for casters).
    /// </summary>
    [Serializable]
    public class EnemyTuning
    {
        public int health = 3;
        public float bodyRadius = 0.45f;
        public float moveSpeed = 2.2f;
        [Tooltip("Ranged kinds keep between these distances from the player.")]
        public float preferredMin = 5f;
        public float preferredMax = 9f;
        [Tooltip("Seconds of wind-up (aim line / strike marker) before the attack.")]
        public float telegraph = 0.75f;
        [Tooltip("Final part of the telegraph where aim is locked, so a sidestep is a real dodge.")]
        public float aimLock = 0.25f;
        public float cooldown = 1.8f;
        [Tooltip("First attack after spawning waits this long beyond the spawn warning.")]
        public float firstShotDelay = 0.6f;
        [Tooltip("Attack fired by ranged kinds; empty for melee-only kinds.")]
        public string attackId = AttackIds.Bolt;
        public float[] volleySpreadDeg = { -8f, 0f, 8f };
        [Tooltip("Scatter Caster: moves to a fresh firing spot between volleys.")]
        public bool repositions;
        [Tooltip("Melee: starts a wind-up when the player is this close (centre to centre).")]
        public float strikeTrigger = 1.6f;
        [Tooltip("Melee: the strike circle sits this far ahead of the body.")]
        public float strikeReach = 0.8f;
        public float strikeRadius = 0.75f;
        [Tooltip("Score for a kill (section 6). Elite = 1.5x.")]
        public int killValue = 10;
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

        public EnemyTuning acolyte = new EnemyTuning();

        // Section 4: approaches and performs a telegraphed close strike; supplies no ammunition.
        public EnemyTuning pursuer = new EnemyTuning
        {
            health = 2, bodyRadius = 0.4f, moveSpeed = 3.0f, telegraph = 0.55f, aimLock = 0.2f,
            cooldown = 1.1f, firstShotDelay = 0.2f, attackId = "", volleySpreadDeg = new float[0],
            killValue = 10,
        };

        // Five-shot fan; repositions between volleys so its angle of attack keeps changing.
        public EnemyTuning scatter = new EnemyTuning
        {
            health = 5, bodyRadius = 0.5f, moveSpeed = 2.6f, preferredMin = 5.5f, preferredMax = 8.5f,
            telegraph = 0.85f, aimLock = 0.3f, cooldown = 2.4f, firstShotDelay = 0.7f,
            volleySpreadDeg = new[] { -24f, -12f, 0f, 12f, 24f }, repositions = true, killValue = 20,
        };

        // Slow, long, clearly telegraphed single rocket: the crowd-clearing ammunition source.
        public EnemyTuning siege = new EnemyTuning
        {
            health = 8, bodyRadius = 0.6f, moveSpeed = 1.1f, preferredMin = 6f, preferredMax = 10f,
            telegraph = 1.3f, aimLock = 0.35f, cooldown = 3.4f, firstShotDelay = 0.8f,
            attackId = AttackIds.Rocket, volleySpreadDeg = new[] { 0f }, killValue = 25,
        };

        public EnemyTuning For(ActorCategory c)
        {
            switch (c)
            {
                case ActorCategory.Acolyte: return acolyte;
                case ActorCategory.Pursuer: return pursuer;
                case ActorCategory.ScatterCaster: return scatter;
                case ActorCategory.SiegeFamiliar: return siege;
                default: throw new ArgumentOutOfRangeException(nameof(c), c, "No ordinary-enemy tuning");
            }
        }
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
