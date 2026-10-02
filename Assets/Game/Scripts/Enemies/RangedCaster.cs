using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// One brain for every ranged ordinary enemy (Bolt Acolyte, Scatter Caster, Siege Familiar).
    /// They share the same readable rhythm (move, stop and telegraph with a tracking aim line,
    /// lock aim, fire) and differ only by tuning: payload, spread, telegraph length, speed and
    /// whether they reposition between volleys. Keeping one brain means a fix to the telegraph
    /// or aim-lock rules lands for all three at once, and "author formations instead of more
    /// enemy types" (Phase 4) stays cheap.
    /// </summary>
    public static class RangedCaster
    {
        public static void Tick(ArenaSim sim, EnemyActor e, EnemyTuning t, double now, float dt)
        {
            var player = sim.Player;
            Vector2 toPlayer = player.Position - e.Position;

            switch (e.Phase)
            {
                case EnemyPhase.Idle:
                    if (t.repositions)
                    {
                        // Scatter Caster: walk to a fresh spot first, then hold the band.
                        if (!e.HasMoveTarget)
                        {
                            e.MoveTarget = EnemySteering.PickRepositionTarget(sim, e, t);
                            e.HasMoveTarget = true;
                        }
                        Vector2 to = e.MoveTarget - e.Position;
                        // Routed (D37): a reposition spot on the far side of a pillar used to
                        // leave the caster grinding against it until its next volley.
                        if (to.sqrMagnitude > 0.04f) EnemySteering.MoveToward(sim, e, e.MoveTarget, t.moveSpeed, dt);
                    }
                    else EnemySteering.KeepBand(sim, e, t, dt);

                    if (player.Alive)
                    {
                        // Face the player while idle so the sprite reads as "watching you".
                        if (toPlayer.sqrMagnitude > 1e-6f) e.AimDirection = toPlayer.normalized;
                        if (now >= e.PhaseEndsAt)
                        {
                            e.Phase = EnemyPhase.Telegraph;
                            e.PhaseEndsAt = now + t.telegraph;
                            e.AimLocked = false;
                            sim.Events.RaiseEnemyTelegraph(e);
                        }
                    }
                    break;

                case EnemyPhase.Telegraph:
                    // Stands still while aiming: a moving shooter plus a moving aim line is too
                    // much to read, and the stop itself is part of the telegraph.
                    if (!e.AimLocked)
                    {
                        if (toPlayer.sqrMagnitude > 1e-6f) e.AimDirection = toPlayer.normalized;
                        if (now >= e.PhaseEndsAt - t.aimLock) e.AimLocked = true;
                    }
                    // Phase 12: with the hostile projectile budget full, the shooter holds its
                    // locked telegraph (a visible delay) and fires on the first tick with room.
                    if (now >= e.PhaseEndsAt && sim.HostileRoomFor(t.volleySpreadDeg.Length))
                    {
                        AttackEmitter.FireVolley(sim, t.attackId, e.ActorId, e.Position, e.Radius,
                            e.AimDirection, t.volleySpreadDeg);
                        sim.Events.RaiseEnemyFired(e);
                        e.Phase = EnemyPhase.Idle;
                        e.AimLocked = false;
                        e.HasMoveTarget = false;   // next idle picks a new spot
                        e.PhaseEndsAt = now + t.cooldown * e.CooldownScale;
                    }
                    break;
            }
        }
    }
}
