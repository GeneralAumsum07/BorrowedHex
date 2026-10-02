using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// Pursuer (section 4): runs at the player and performs a telegraphed close strike. It
    /// fires nothing capturable; instead its strike can be PARRIED (D26, D36): touch the strike
    /// circle's rim with the parry band early in a catch and it comes back as a riposte. So it pressures positioning, is a crowd target
    /// for returned shots and rockets, and is its own answer when no caster is left.
    ///
    /// Rhythm: Idle (seek) → Telegraph (stop; a ground marker shows exactly where the strike
    /// lands; aim tracks then locks; the gold rim, the parry chance, shows for part of it) →
    /// strike resolves ONCE at the end of the wind-up against the strike circle → Recover
    /// (brief pause so it can be punished). Touching its body costs half a heart (contact
    /// damage, owner direction), handled for every enemy in ArenaSim.ApplyContactDamage.
    /// </summary>
    public static class Pursuer
    {
        public static Vector2 StrikeCentre(EnemyActor e, EnemyTuning t) => e.Position + e.AimDirection * t.strikeReach;

        public static void Tick(ArenaSim sim, EnemyActor e, EnemyTuning t, double now, float dt)
        {
            var player = sim.Player;
            Vector2 toPlayer = player.Position - e.Position;
            float dist = toPlayer.magnitude;

            switch (e.Phase)
            {
                case EnemyPhase.Idle:
                    if (!player.Alive) break;
                    if (dist > 1e-4f) e.AimDirection = toPlayer / dist;
                    // Stop short of the player so bodies do not overlap while it winds up.
                    if (dist > t.strikeTrigger * 0.75f) EnemySteering.Seek(sim, e, t.moveSpeed, dt);
                    if (dist <= t.strikeTrigger && now >= e.PhaseEndsAt)
                    {
                        e.Phase = EnemyPhase.Telegraph;
                        e.PhaseEndsAt = now + t.telegraph;
                        e.AimLocked = false;
                        // Owner direction: the rim shows just after the wind-up starts and is
                        // gone before the strike lands. Clamped so a short wind-up still leaves
                        // a gap before the hit.
                        e.ParryRimOpensAt = now + t.parryRimDelay;
                        e.ParryRimClosesAt = System.Math.Min(e.ParryRimOpensAt + t.parryRimDuration, e.PhaseEndsAt - 0.05);
                        sim.Events.RaiseEnemyTelegraph(e);
                    }
                    break;

                case EnemyPhase.Telegraph:
                    if (!e.AimLocked)
                    {
                        if (dist > 1e-4f) e.AimDirection = toPlayer / dist;
                        if (now >= e.PhaseEndsAt - t.aimLock) e.AimLocked = true;
                    }
                    // Parry: possible on any tick while the rim is up (D46). It cancels the
                    // strike outright; the parry band and rim must touch on that tick (D36).
                    if (e.ParryRimOpen(now) && sim.TryParry(e, StrikeCentre(e, t), t.strikeRadius, t.strikeEdgeWidth))
                    {
                        e.ClearParryRim();
                        e.Phase = EnemyPhase.Recover;
                        e.AimLocked = false;
                        // A parried attacker that survives the riposte (elite tuning, future
                        // health buffs) is staggered for a full cooldown: the follow-up window
                        // is part of the reward.
                        e.PhaseEndsAt = now + t.cooldown;
                        break;
                    }
                    if (now >= e.PhaseEndsAt)
                    {
                        // One overlap test at one instant: no lingering hitbox, no double hits.
                        // DamagePlayer itself respects dash / post-hit invulnerability. The rim
                        // closed earlier, so nothing can be parried at this instant.
                        Vector2 c = StrikeCentre(e, t);
                        float r = t.strikeRadius + player.Radius;
                        if (player.Alive && (player.Position - c).sqrMagnitude <= r * r)
                            sim.DamagePlayer(sim.Config.combat.enemyHitDamage, e.ActorId);
                        sim.Events.RaiseEnemyFired(e);
                        e.ClearParryRim();
                        e.Phase = EnemyPhase.Recover;
                        e.AimLocked = false;
                        e.PhaseEndsAt = now + t.cooldown * 0.5f;
                    }
                    break;

                case EnemyPhase.Recover:
                    if (now >= e.PhaseEndsAt)
                    {
                        e.Phase = EnemyPhase.Idle;
                        e.PhaseEndsAt = now + t.cooldown * 0.5f;
                    }
                    break;
            }
        }
    }
}
