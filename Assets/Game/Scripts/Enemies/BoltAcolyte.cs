using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// Bolt Acolyte brain (section 4): keeps its distance, stops to telegraph with a visible aim
    /// line, locks aim for the last moment, then fires a three-bolt volley. It is the teaching
    /// enemy for capture, so it is deliberately predictable: fixed rhythm, no feints.
    /// </summary>
    public static class BoltAcolyte
    {
        public static void Tick(ArenaSim sim, EnemyActor e, double now, float dt)
        {
            var t = sim.Config.combat.acolyte;
            var player = sim.Player;
            Vector2 toPlayer = player.Position - e.Position;

            switch (e.Phase)
            {
                case EnemyPhase.Idle:
                    Steer(sim, e, toPlayer, t, dt);
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
                    // Stands still while aiming: a moving shooter plus a moving aim line is
                    // too much to read for the enemy that teaches the core mechanic.
                    if (!e.AimLocked)
                    {
                        if (toPlayer.sqrMagnitude > 1e-6f) e.AimDirection = toPlayer.normalized;
                        if (now >= e.PhaseEndsAt - t.aimLock) e.AimLocked = true;
                    }
                    if (now >= e.PhaseEndsAt)
                    {
                        AttackEmitter.FireVolley(sim, AttackIds.Bolt, e.ActorId, e.Position, e.Radius,
                            e.AimDirection, t.volleySpreadDeg);
                        sim.Events.RaiseEnemyFired(e);
                        e.Phase = EnemyPhase.Idle;
                        e.AimLocked = false;
                        e.PhaseEndsAt = now + t.cooldown;
                    }
                    break;
            }
        }

        /// <summary>Stay inside a distance band; strafe inside it so it is not a static target.</summary>
        static void Steer(ArenaSim sim, EnemyActor e, Vector2 toPlayer, AcolyteTuning t, float dt)
        {
            float dist = toPlayer.magnitude;
            if (dist < 1e-4f) return;
            Vector2 dir = toPlayer / dist;
            Vector2 want;
            if (dist < t.preferredMin) want = -dir;
            else if (dist > t.preferredMax) want = dir;
            else want = new Vector2(-dir.y, dir.x) * (0.5f * e.StrafeSign);

            var next = PlayerMotor.SweepMove(e.Position, want * (t.moveSpeed * dt), e.Radius, sim.Walls, out bool blocked);
            // Bumping a wall while strafing flips direction instead of grinding against it.
            if (blocked) e.StrafeSign = -e.StrafeSign;
            e.Position = next;
        }
    }

    /// <summary>
    /// The arcane lantern (section 3): a fixed, visible hostile emitter of slow capturable
    /// bolts. Phase 2 fires it on demand; Phase 4 adds the ammunition-starvation trigger.
    /// </summary>
    public sealed class Lantern
    {
        public int ActorId;
        public Vector2 Position;
        public double NextFireAt;
        public double StarvedSince = -1;
        public const float BodyRadius = 0.4f;
    }
}
