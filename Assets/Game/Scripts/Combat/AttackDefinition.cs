using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// Immutable attack type shared by enemy emission and player return (section 4: returning
    /// an attack does not create a different weapon). Faction is NOT part of the definition;
    /// it is context on the projectile, so the same definition hurts whoever it should.
    /// </summary>
    public sealed class AttackDefinition
    {
        public readonly string Id;
        public readonly AttackKind Kind;
        public readonly float Speed;
        public readonly float Radius;
        public readonly int ReturnedDamage;
        public readonly int HostileDamage;
        public readonly int EnergyCost;
        public readonly float Lifetime;
        public readonly bool Capturable;
        public readonly float ExplosionRadius;
        public readonly int ExplosionDamage;

        public AttackDefinition(AttackTuning t)
        {
            Id = t.id;
            Kind = t.kind;
            Speed = t.speed;
            Radius = t.radius;
            ReturnedDamage = t.returnedDamage;
            HostileDamage = t.hostileDamage;
            EnergyCost = t.energyCost;
            Lifetime = t.lifetime;
            Capturable = t.capturable;
            ExplosionRadius = t.explosionRadius;
            ExplosionDamage = t.explosionDamage;
        }

        public bool Explodes => ExplosionRadius > 0f && ExplosionDamage > 0;
    }

    /// <summary>Per-run lookup of definitions, frozen from the config at run start.</summary>
    public sealed class AttackCatalog
    {
        readonly Dictionary<string, AttackDefinition> byId = new Dictionary<string, AttackDefinition>();

        public AttackCatalog(CombatTuning tuning)
        {
            foreach (var t in tuning.attacks)
                if (!string.IsNullOrEmpty(t.id)) byId[t.id] = new AttackDefinition(t);
        }

        public AttackDefinition Get(string id) =>
            byId.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException("Unknown attack id: " + id);
    }
}
