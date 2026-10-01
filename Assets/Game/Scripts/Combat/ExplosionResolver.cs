using BorrowedHex.Core;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// Rocket bursts (section 4: five damage within a 1.6-unit radius).
    ///
    /// Resolved instantly at the impact point rather than scheduled, so there is no pending
    /// explosion that death or restart would need to cancel, and the burst happens on the same
    /// tick the rocket ends: it cannot be triggered twice.
    ///
    /// Faction rules:
    ///   - Returned rockets damage every ACTIVE enemy whose body overlaps the radius, once each
    ///     (one pass over the list, with the rocket's hit set as a guard), and never the player.
    ///   - Hostile rockets deal their single player hit through the normal impact path; their
    ///     burst is purely visual. Area damage on the player would make an incoming rocket
    ///     worth more than "one player hit", against section 4.
    /// </summary>
    public static class ExplosionResolver
    {
        public static int Explode(ArenaSim sim, ProjectileActor p, Vector2 at)
        {
            float radius = p.Shot.ExplosionRadius;
            sim.Events.RaiseExplosion(at, radius, p.Faction);
            if (p.Faction != AttackFaction.Returned) return 0;

            float amount = sim.ScaledReturnedDamage(p, p.Shot.ExplosionDamage);
            var category = p.IsEcho ? DamageCategory.Echo : DamageCategory.Explosion;
            int hit = 0;
            // Iterate a snapshot by index: DamageEnemy never adds or removes list entries (dead
            // enemies are compacted at the end of the tick), so indices stay stable.
            var enemies = sim.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.IsActive(sim.Clock.Now) || p.HitActors.Contains(e.ActorId)) continue;
                float reach = radius + e.Radius;
                if ((e.Position - at).sqrMagnitude > reach * reach) continue;
                p.HitActors.Add(e.ActorId);
                if (sim.DamageEnemy(e, amount, category, p.Shot, p.RootReleaseId)) hit++;
            }
            return hit;
        }
    }
}
