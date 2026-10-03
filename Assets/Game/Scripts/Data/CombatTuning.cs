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
        [Tooltip("Life seconds lost when it hits the player as a hostile shot. "
                 + "A boss's shots use the boss's own damage instead (BossTuning.hitDamage).")]
        public int hostileDamage = 10;
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
        // Per shooter, not per attack: Acolyte, Scatter and the Collector all fire the one
        // "bolt" definition, whose 4 s lifetime at speed 9 (36 units) outlasts any arena, so
        // before this every hostile shot simply flew until it met a wall (owner report).
        [Tooltip("Ranged kinds: units a shot travels before it expires (owner values: Scatter 10, Acolyte 15, Siege 22).")]
        public float range = 15f;
        [Tooltip("Scatter Caster: moves to a fresh firing spot between volleys.")]
        public bool repositions;
        [Tooltip("Melee: starts a wind-up when the player is this close (centre to centre).")]
        public float strikeTrigger = 1.6f;
        [Tooltip("Melee: the strike circle sits this far ahead of the body.")]
        public float strikeReach = 0.8f;
        public float strikeRadius = 0.75f;
        [Tooltip("Melee: width of the strike circle's outer rim band, the part a parry band must touch (D36).")]
        public float strikeEdgeWidth = 0.09f;
        [Tooltip("Melee: the rim (and with it the chance to parry) appears this long after the wind-up starts.")]
        public float parryRimDelay = 0.1f;
        [Tooltip("Melee: the rim stays up this long, then vanishes BEFORE the strike lands (owner direction).")]
        public float parryRimDuration = 0.25f;
        [Tooltip("Score for a kill (section 6). Elite = 1.5x.")]
        public int killValue = 10;
        [Tooltip("Seconds restored by a kill, capped by the run's starting life. Elite = 1.5x.")]
        public float killSeconds = 3f;
    }

    [Serializable]
    public class CombatTuning
    {
        [Header("Borrowed hex identities")]
        public int acolyteReturnPierce = 1;
        public float scatterReturnDamage = 1.5f;
        public float scatterReturnHalfAngle = 12f;
        public float scatterReturnRange = 6f;
        public float bossReturnDamage = 2f;

        [Header("Overstaying")]
        public float overstaySeconds = 25f;
        public float overstayWarningSeconds = 5f;
        public float overstayHealthScale = 1.5f;
        public float overstayCooldownScale = 0.75f;
        public float overstayMoveScale = 1.15f;
        [Tooltip("Seconds an enemy is visible as a harmless spawn warning before it acts.")]
        public float spawnWarning = 0.8f;
        [Tooltip("Enemies never spawn closer than this to the player.")]
        public float minSpawnDistance = 5f;

        // Player damage spends life seconds; enemy health remains ordinary damage units.
        [Tooltip("Life seconds lost to an ordinary enemy's melee strike.")]
        public int enemyHitDamage = 10;
        [Tooltip("Life seconds lost on touching an ordinary enemy's body.")]
        public int enemyContactDamage = 5;

        public List<AttackTuning> attacks = new List<AttackTuning>
        {
            new AttackTuning { id = AttackIds.Bolt },
            // The parried strike (D26). Fast and short-lived so it reads as a slash flung back
            // rather than a bolt; 2 damage kills an ordinary Pursuer outright, which is what
            // makes parry the answer to a melee-only wave now that the lantern is gone.
            // Not capturable and costs no packet energy: it never enters the packet economy.
            new AttackTuning
            {
                id = AttackIds.Riposte, kind = AttackKind.Riposte, speed = 18f, radius = 0.3f,
                returnedDamage = 2, hostileDamage = 0, energyCost = 0, lifetime = 0.5f, capturable = false,
            },
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
            // moveSpeed 3.0 → 4.2 (owner, D56: 1.4x); still under the player's 6.
            health = 2, bodyRadius = 0.4f, moveSpeed = 4.2f, telegraph = 0.55f, aimLock = 0.2f,
            cooldown = 1.1f, firstShotDelay = 0.2f, attackId = "", volleySpreadDeg = new float[0],
            killValue = 10,
        };

        // Five-shot fan; repositions between volleys so its angle of attack keeps changing.
        public EnemyTuning scatter = new EnemyTuning
        {
            health = 5, bodyRadius = 0.5f, moveSpeed = 2.6f, preferredMin = 5.5f, preferredMax = 8.5f,
            telegraph = 0.85f, aimLock = 0.3f, cooldown = 2.4f, firstShotDelay = 0.7f,
            volleySpreadDeg = new[] { -24f, -12f, 0f, 12f, 24f }, repositions = true, killValue = 20, killSeconds = 5f,
            range = 10f, // mid range: just past its 8.5 standing distance, so backing off escapes the fan
        };

        // Slow, long, clearly telegraphed single rocket: the crowd-clearing ammunition source.
        public EnemyTuning siege = new EnemyTuning
        {
            health = 8, bodyRadius = 0.6f, moveSpeed = 1.1f, preferredMin = 6f, preferredMax = 10f,
            telegraph = 1.3f, aimLock = 0.35f, cooldown = 3.4f, firstShotDelay = 0.8f,
            attackId = AttackIds.Rocket, volleySpreadDeg = new[] { 0f }, killValue = 25, killSeconds = 6f,
            range = 22f, // the longest ordinary reach; 22 / speed 6 = 3.7 s, inside the rocket's 5 s lifetime
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

        [Tooltip("Parry: extra enemies the riposte passes through after the attacker (D26).")]
        public int ripostePierce = 1;

        [Tooltip("D92: damage multiplier when an enemy is hit by its own school's attack (owner value 0.75).")]
        public float ownSchoolDamage = 0.75f;
        [Tooltip("D92: damage multiplier from any other school, ripostes included; upgrade damage is neutral (owner value 1.33).")]
        public float otherSchoolDamage = 1.33f;
        [Tooltip("D95: seconds after a kill during which the next kill extends the chain (placeholder).")]
        public float chainWindow = 2.5f;
        [Tooltip("D95: extra seconds for the 1st, 2nd, 3rd... kill of a chain; the last entry repeats (placeholder).")]
        public float[] chainBonusSeconds = { 0f, 1f, 3f, 5f };
    }

    /// <summary>String IDs shared by data, snapshots, stats and save files.</summary>
    public static class AttackIds
    {
        public const string Bolt = "bolt";
        public const string Riposte = "riposte";
        public const string Rocket = "rocket";
    }

    public partial class GameConfig
    {
        public CombatTuning combat = new CombatTuning();
    }
}
