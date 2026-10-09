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
        // Owner playtest: shooters opened fire from wherever they stood, so a volley launched
        // from beyond its own range fizzled in mid-air and read as a bug, not a threat. A
        // shooter now only begins a telegraph from inside this fraction of its range. The
        // 10% slack covers the shot spawning a body-radius ahead, the player's own radius and
        // a step of player drift during the wind-up, so a volley that starts "in range" can
        // actually arrive. Deliberately NOT re-checked once the telegraph starts: backing out
        // of reach mid-wind-up is a legitimate dodge (see the Scatter range note in CombatTuning).
        const float EngageFraction = 0.9f;

        /// <summary>Farthest the player may be for this shooter to start a volley.</summary>
        public static float EngageDistance(EnemyTuning t) => t.range * EngageFraction;

        public static void Tick(ArenaSim sim, EnemyActor e, EnemyTuning t, double now, float dt)
        {
            var player = sim.Player;
            Vector2 toPlayer = player.Position - e.Position;
            // Squared compare: this runs per shooter per tick, and it is the only distance test.
            float engage = EngageDistance(t);
            bool inReach = toPlayer.sqrMagnitude <= engage * engage;

            switch (e.Phase)
            {
                case EnemyPhase.Idle:
                    if (!inReach)
                    {
                        // Out of reach: walk in (routed around pillars, D37) before anything
                        // else. Overrides both the band and the reposition spot, either of which
                        // could otherwise park the shooter outside its range indefinitely.
                        EnemySteering.MoveToward(sim, e, player.Position, t.moveSpeed, dt);
                        // A reposition spot chosen from the old position is stale once we have
                        // closed in; pick a fresh one when back in reach.
                        e.HasMoveTarget = false;
                    }
                    else if (t.repositions)
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
                        // A cooldown that ends while out of reach simply waits: the shooter fires
                        // on the first tick it has closed in, rather than wasting the volley.
                        if (now >= e.PhaseEndsAt && inReach)
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
                            e.AimDirection, t.volleySpreadDeg, range: t.range);
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
