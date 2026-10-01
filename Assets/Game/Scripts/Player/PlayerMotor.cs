using System.Collections.Generic;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Player
{
    /// <summary>
    /// Movement, aim and dash rules. Static and side-effect free apart from the actor it is
    /// handed, so the sim owns ordering and tests can drive it tick by tick.
    /// </summary>
    public static class PlayerMotor
    {
        /// <summary>Update aim from the command; an invalid projection keeps the last direction.</summary>
        public static void UpdateAim(PlayerActor p, in PlayerCommand cmd)
        {
            if (!cmd.HasAim) return;
            Vector2 to = cmd.AimPoint - p.Position;
            // A cursor exactly over the player gives no usable direction.
            if (to.sqrMagnitude < 1e-4f || float.IsNaN(to.x) || float.IsNaN(to.y)) return;
            p.AimDirection = to.normalized;
        }

        /// <summary>
        /// Start a dash if off cooldown. Direction is the movement input, or the aim when the
        /// player is not pressing a direction (section 2).
        /// </summary>
        public static bool TryStartDash(PlayerActor p, Vector2 move, PlayerStats s, double now)
        {
            if (p.Dashing || now < p.DashReadyAt) return false;
            Vector2 dir = move.sqrMagnitude > 0.01f ? move.normalized : p.AimDirection;
            if (dir.sqrMagnitude < 1e-6f) return false;
            p.Dashing = true;
            p.DashOrigin = p.Position;
            p.DashDirection = dir.normalized;
            p.DashSpeed = s.DashDistance / Mathf.Max(0.01f, s.DashDuration);
            p.DashEndsAt = now + s.DashDuration;
            p.DashReadyAt = now + s.DashCooldown;
            // Invulnerability never outlasts the dash itself (Resilience 3 cap, section 7).
            p.DashInvulnerableUntil = now + Mathf.Min(s.DashInvulnerability, s.DashDuration);
            return true;
        }

        /// <summary>
        /// Advance position for a tick of length dt ending at time `now`. Walking slides along
        /// walls; a dash sweeps straight and stops at the first wall it meets.
        /// </summary>
        public static void Move(PlayerActor p, Vector2 move, PlayerStats s, double now, float dt, IReadOnlyList<Rect> walls)
        {
            if (p.Dashing)
            {
                // Only travel for the part of this tick that was still inside the dash, so the
                // distance is exact regardless of how the fixed step lines up with the dash end.
                double start = now - dt;
                float active = (float)System.Math.Max(0.0, System.Math.Min(now, p.DashEndsAt) - start);
                Vector2 delta = p.DashDirection * (p.DashSpeed * active);
                p.Position = SweepMove(p.Position, delta, p.Radius, walls, out bool hit);
                if (hit || now >= p.DashEndsAt - 1e-9) p.Dashing = false;
                return;
            }

            // Clamp, don't normalize: analog-style partial input stays partial, while a
            // diagonal key pair (magnitude √2) is brought back to full speed, not 41% over.
            Vector2 input = Vector2.ClampMagnitude(move, 1f);
            if (input.sqrMagnitude < 1e-6f) return;
            p.FacingMove = input;
            Vector2 step = input * (s.MoveSpeed * dt);
            // Axis-separated moves give wall sliding for free.
            p.Position = SweepMove(p.Position, new Vector2(step.x, 0f), p.Radius, walls, out _);
            p.Position = SweepMove(p.Position, new Vector2(0f, step.y), p.Radius, walls, out _);
        }

        const float Skin = 0.002f;

        /// <summary>Move a circle by delta, stopping just short of the first wall contact.</summary>
        public static Vector2 SweepMove(Vector2 pos, Vector2 delta, float radius, IReadOnlyList<Rect> walls, out bool blocked)
        {
            blocked = false;
            float len = delta.magnitude;
            if (len < 1e-7f) return pos;
            float best = 1f;
            for (int i = 0; i < walls.Count; i++)
            {
                Rect w = walls[i];
                if (Geometry2D.CircleOverlapsRect(pos, radius, w))
                {
                    // Already touching: allow motion that does not push deeper, otherwise a
                    // body resting against a wall could never slide along or leave it.
                    Vector2 closest = new Vector2(Mathf.Clamp(pos.x, w.xMin, w.xMax), Mathf.Clamp(pos.y, w.yMin, w.yMax));
                    Vector2 n = pos - closest;
                    if (Vector2.Dot(n, delta) >= 0f) continue;
                    best = 0f;
                    blocked = true;
                    break;
                }
                if (Geometry2D.SweepCircleVsRect(pos, pos + delta, radius, w, out float t) && t < best)
                {
                    best = t;
                    blocked = true;
                }
            }
            if (!blocked) return pos + delta;
            float travel = Mathf.Max(0f, best * len - Skin);
            return pos + delta / len * travel;
        }
    }
}
