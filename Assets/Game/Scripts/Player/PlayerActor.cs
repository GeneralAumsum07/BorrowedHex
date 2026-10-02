using UnityEngine;

namespace BorrowedHex.Player
{
    /// <summary>
    /// One simulation tick's worth of player intent, already resolved to the gameplay plane.
    /// Button presses are edge-triggered and latched by the input reader until a tick consumes
    /// them, so a press between two fixed steps is never lost or repeated.
    /// </summary>
    public struct PlayerCommand
    {
        public Vector2 Move;       // camera-relative, in world XZ; magnitude may exceed 1 (clamped in sim)
        public bool HasAim;        // false when the cursor ray missed the plane or left the window
        public Vector2 AimPoint;   // world XZ point under the cursor
        public bool Catch;
        public bool Dash;
        /// <summary>Right mouse: fire the selected packet now instead of waiting out its timer.</summary>
        public bool Release;
        /// <summary>Q: move the slot selection to the next slot (wraps).</summary>
        public bool CycleSlot;

        public static PlayerCommand Moving(Vector2 move) => new PlayerCommand { Move = move };
        // Builders return a modified COPY and never touch the receiver: a mutating builder on
        // a struct held in a local variable would leave e.g. Catch stuck on for every later
        // tick that reuses it (found while scripting play mode, D35).
        public PlayerCommand WithAim(Vector2 point) { var c = this; c.HasAim = true; c.AimPoint = point; return c; }
        public PlayerCommand WithDash() { var c = this; c.Dash = true; return c; }
        public PlayerCommand WithCatch() { var c = this; c.Catch = true; return c; }
        public PlayerCommand WithRelease() { var c = this; c.Release = true; return c; }
        public PlayerCommand WithCycle() { var c = this; c.CycleSlot = true; return c; }
    }

    /// <summary>Mutable player state for one run. Rules live in ArenaSim/PlayerMotor.</summary>
    public sealed class PlayerActor
    {
        public int ActorId;
        public Vector2 Position;
        public float Radius;
        public Vector2 AimDirection = Vector2.up; // +Z: toward the far wall, away from camera
        public Vector2 FacingMove;                 // last non-zero move, for presentation only
        public bool Alive = true;

        public double InvulnerableUntil = double.NegativeInfinity;

        public bool Dashing;
        public double DashEndsAt;
        public double DashInvulnerableUntil = double.NegativeInfinity;
        public double DashReadyAt;
        public Vector2 DashDirection;
        public float DashSpeed;
        /// <summary>Start of the current/last dash; Daredevil sweeps capture along it.</summary>
        public Vector2 DashOrigin;

        public bool IsInvulnerable(double now) => now < InvulnerableUntil || now < DashInvulnerableUntil;

        /// <summary>Test/dev helper: drop all invulnerability windows.</summary>
        public void ClearInvulnerability()
        {
            InvulnerableUntil = double.NegativeInfinity;
            DashInvulnerableUntil = double.NegativeInfinity;
        }
    }
}
