using BorrowedHex.Core;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// Copied values of one shot (section 8 contract). A value type with no object references:
    /// a packet holding snapshots stays valid after the projectile is pooled and the enemy
    /// that fired it is destroyed.
    /// </summary>
    public struct AttackSnapshot
    {
        public string DefinitionId;
        public AttackKind Kind;
        public float Speed;
        public float Radius;
        public int ReturnedDamage;
        public int HostileDamage;
        public int EnergyCost;
        public float Lifetime;
        public bool Capturable;
        public float ExplosionRadius;
        public int ExplosionDamage;

        /// <summary>Angle relative to the volley's centre line; preserved on return (section 3).</summary>
        public float SpreadOffsetDeg;
        /// <summary>The enemy that originally fired this shot; kept through capture and return.</summary>
        public int SourceActorId;
        public int ShotId;
        public bool Perfect;

        public static AttackSnapshot From(AttackDefinition d, int sourceActorId, int shotId, float spreadOffsetDeg)
        {
            return new AttackSnapshot
            {
                DefinitionId = d.Id,
                Kind = d.Kind,
                Speed = d.Speed,
                Radius = d.Radius,
                ReturnedDamage = d.ReturnedDamage,
                HostileDamage = d.HostileDamage,
                EnergyCost = d.EnergyCost,
                Lifetime = d.Lifetime,
                Capturable = d.Capturable,
                ExplosionRadius = d.ExplosionRadius,
                ExplosionDamage = d.ExplosionDamage,
                SpreadOffsetDeg = spreadOffsetDeg,
                SourceActorId = sourceActorId,
                ShotId = shotId,
            };
        }

        public bool Explodes => ExplosionRadius > 0f && ExplosionDamage > 0;
    }

    /// <summary>
    /// One application of damage (section 8 contract). Every hit gets its own DamageId so
    /// stats/achievements can count hits idempotently; provenance fields survive capture.
    /// </summary>
    public struct DamageEvent
    {
        public int DamageId;
        public int TargetActorId;
        public int SourceActorId;
        public int ShotId;
        public int RootReleaseId;
        public AttackKind Kind;
        // Float: perfect (+15%) and power bonuses must not round away on 1-damage bolts.
        public float Amount;
        public DamageCategory Category;
        public bool Perfect;
    }
}
