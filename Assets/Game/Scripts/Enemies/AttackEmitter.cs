using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// The one way anything hostile fires: enemies and (Phase 5) the boss both
    /// emit through here, so every shot gets a unique ShotId, a snapshot and the same spawn
    /// rules. The player's returns use ReleaseService (Phase 3) with the same projectile code.
    /// </summary>
    public static class AttackEmitter
    {
        /// <summary>
        /// Fire one shot per spread angle around <paramref name="aim"/>. The muzzle sits just
        /// outside the shooter's body so a shot never starts inside its own source.
        /// </summary>
        /// <param name="hostileDamage">Overrides the attack's damage to the player (half hearts);
        /// the boss uses it so its bolts hit harder than an acolyte's while staying ordinary,
        /// capturable bolts. Negative keeps the attack's own value. Returned damage is untouched.</param>
        public static int FireVolley(ArenaSim sim, string attackId, int sourceActorId, Vector2 origin,
            float sourceRadius, Vector2 aim, float[] spreadDeg, int hostileDamage = -1)
        {
            var def = sim.Attacks.Get(attackId);
            ActorCategory sourceCategory = ActorCategory.Player; // unknown synthetic shots keep the base rule
            foreach (var e in sim.Enemies)
                if (e.ActorId == sourceActorId) { sourceCategory = e.Category; break; }
            float halfSpread = 0;
            foreach (float offset in spreadDeg) halfSpread = Mathf.Max(halfSpread, Mathf.Abs(offset));
            if (aim.sqrMagnitude < 1e-8f) aim = Vector2.down;
            aim.Normalize();
            int fired = 0;
            foreach (float offset in spreadDeg)
            {
                Vector2 dir = Geometry2D.Rotate(aim, offset);
                Vector2 muzzle = origin + dir * (sourceRadius + def.Radius + 0.05f);
                var shot = AttackSnapshot.From(def, sourceActorId, sim.Ids.Next(), offset);
                shot.SourceCategory = sourceCategory;
                shot.SourceSpreadHalfAngle = halfSpread;
                if (hostileDamage >= 0) shot.HostileDamage = hostileDamage;
                sim.SpawnProjectile(shot, AttackFaction.Hostile, muzzle, dir);
                fired++;
            }
            return fired;
        }
    }
}
