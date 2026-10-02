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
    /// lands; aim tracks then locks) → strike resolves ONCE at the end of the wind-up against
    /// the strike circle → Recover (brief pause so it can be punished). Touching its body does
    /// nothing; only the telegraphed strike hurts, so every hit is readable and avoidable.
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
                        sim.Events.RaiseEnemyTelegraph(e);
                    }
                    break;

                case EnemyPhase.Telegraph:
                    if (!e.AimLocked)
                    {
                        if (dist > 1e-4f) e.AimDirection = toPlayer / dist;
                        if (now >= e.PhaseEndsAt - t.aimLock) e.AimLocked = true;
                    }
                    if (now >= e.PhaseEndsAt)
                    {
                        // One overlap test at one instant: no lingering hitbox, no double hits.
                        // DamagePlayer itself respects dash / post-hit invulnerability.
                        Vector2 c = StrikeCentre(e, t);
                        float r = t.strikeRadius + player.Radius;
                        bool wouldHit = player.Alive && (player.Position - c).sqrMagnitude <= r * r;
                        // Parry is checked first and wins: if the bands meet in time the strike
                        // is redirected instead of landing (D36), even if the player is also
                        // dash-invulnerable. Otherwise being inside the circle is a hit.
                        bool parried = sim.TryParry(e, c, t.strikeRadius, t.strikeEdgeWidth);
                        if (wouldHit && !parried) sim.DamagePlayer(1, e.ActorId);
                        sim.Events.RaiseEnemyFired(e);
                        e.Phase = EnemyPhase.Recover;
                        e.AimLocked = false;
                        // A parried attacker that survives the riposte (elite tuning, future
                        // health buffs) is staggered for a full cooldown: the follow-up window
                        // is part of the reward.
                        e.PhaseEndsAt = now + t.cooldown * (parried ? 1f : 0.5f);
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
