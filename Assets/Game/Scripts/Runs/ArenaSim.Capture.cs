using BorrowedHex.Combat;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Capture → carry → return (section 3). The controller and store are plain objects owned
    /// by this run, so restart discards every packet and pending release with the sim.
    /// </summary>
    public sealed partial class ArenaSim
    {
        public CaptureController Capture { get; private set; }
        public PacketStore Packets { get; private set; }

        void InitCapture()
        {
            Capture = new CaptureController();
            Packets = new PacketStore(Stats.PacketSlots);
        }

        /// <summary>Step 3: release expired packets BEFORE this tick's captures (freed slot usable now).</summary>
        void ReleaseExpiredPackets(double now)
        {
            var expired = Packets.Advance(now);
            if (expired.Count == 0) return;
            foreach (var packet in expired)
            {
                // If the window that created it is somehow still open, stop appending to it.
                if (Capture.ActivePacket == packet) Capture.Cancel();
                ReleaseService.Release(this, packet, Player.Position, Player.AimDirection, 1f);
            }
        }

        /// <summary>
        /// Right mouse (D32): fire the selected packet now, from the current position along the
        /// current aim, exactly as an expiry would. No damage penalty: holding to the timer
        /// buys nothing but positioning, so firing early is the fast-paced default.
        /// </summary>
        bool TryReleaseEarly()
        {
            var packet = Packets.ReleaseCandidate();
            if (packet == null) return false;
            Packets.Remove(packet);
            Capture.Detach(packet);
            ReleaseService.Release(this, packet, Player.Position, Player.AimDirection, 1f);
            return true;
        }

        void TryCatch(double now)
        {
            if (Stats.CatchIsDash) return;   // Daredevil style routes catch through the dash (Phase 10)
            if (Capture.TryActivate(now, Stats)) Events.RaiseCatchActivated(Capture.ActivationId);
        }

        /// <summary>Called by the projectile resolver when capture is the earliest contact.</summary>
        bool TryCaptureProjectile(ProjectileActor p)
        {
            var result = Capture.TryCapture(p.Shot, p.Faction, p.IsEcho, Clock.Now, Packets, Stats, Ids);
            if (result == CaptureResult.CreatedPacket || result == CaptureResult.Appended)
            {
                Events.RaiseShotCaptured(Capture.ActivePacket, p.Shot, p.Position, result);
                EndProjectile(p, ProjectileEndReason.Captured);
                return true;
            }
            // Rejected (full packet / full slots): it keeps flying and can still hurt. Mark it
            // so this activation does not re-test it every sub-step.
            p.CaptureRejectedActivation = Capture.ActivationId;
            Events.RaiseCaptureRejected(p.Position, result);
            return false;
        }

        bool CanAttemptCapture(ProjectileActor p) =>
            p.Faction == AttackFaction.Hostile && !p.IsEcho && p.Shot.Capturable
            && Player.Alive && Capture.IsWindowOpen(Clock.Now)
            && p.CaptureRejectedActivation != Capture.ActivationId;

        void CancelCapture()
        {
            Capture.Cancel();
            Packets.CancelAll();
        }
    }

    /// <summary>
    /// Fires a packet's payloads as returned shots from the player's CURRENT position toward
    /// the current aim, keeping each payload's spread offset (section 3: preserve relative
    /// spread rather than reversing historic trajectories).
    /// </summary>
    public static class ReleaseService
    {
        /// <returns>The root release ID shared by every shot (and later echo) of this release.</returns>
        public static int Release(ArenaSim sim, CapturedPacket packet, Vector2 origin, Vector2 aim, float power)
        {
            if (packet.Status == PacketStatus.Released || packet.Status == PacketStatus.Cancelled) return 0;
            int root = sim.Ids.Next();
            if (aim.sqrMagnitude < 1e-8f) aim = Vector2.up;
            aim.Normalize();
            foreach (var payload in packet.Payloads)
            {
                Vector2 dir = Geometry2D.Rotate(aim, payload.SpreadOffsetDeg);
                Vector2 muzzle = origin + dir * (sim.Player.Radius + payload.Radius + 0.05f);
                sim.SpawnProjectile(payload, AttackFaction.Returned, muzzle, dir, root, isEcho: false, power: power);
            }
            packet.Status = PacketStatus.Released;
            sim.Events.RaisePacketReleased(packet, root);
            return root;
        }
    }
}
