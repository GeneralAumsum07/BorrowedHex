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

        /// <summary>Expiry precedes input: at exactly 3 s the hex backfires, even if fire was pressed.</summary>
        void BackfireExpiredPackets(double now)
        {
            var expired = Packets.Advance(now);
            if (expired.Count == 0) return;
            foreach (var packet in expired)
            {
                // If the window that created it is somehow still open, stop appending to it.
                if (Capture.ActivePacket == packet) Capture.Cancel();
                packet.Status = PacketStatus.Backfired;
                Events.RaisePacketBackfired(packet);
                // It is in the player's hands, so dash and post-hit immunity cannot save it.
                ApplyPlayerDamage(Stats.BackfireSeconds, 0, Stats.HitInvulnerability, bypassInvulnerability: true);
            }
        }

        /// <summary>
        /// Right mouse (D32): fire the selected packet now, from the current position along the
        /// current aim at its accumulated power. Selection stays put when the slot empties.
        /// </summary>
        bool TryReleaseEarly()
        {
            var packet = Packets.ReleaseCandidate();
            if (packet == null) return false;
            Packets.Remove(packet);
            Capture.Detach(packet);
            ReleaseService.Release(this, packet, Player.Position, Player.AimDirection, packet.Power(Stats.PowerPerSecond));
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
            if (!sim.Player.Alive || packet.Status == PacketStatus.Released || packet.Status == PacketStatus.Backfired
                || packet.Status == PacketStatus.Cancelled) return 0;
            int root = sim.Ids.Next();
            if (aim.sqrMagnitude < 1e-8f) aim = Vector2.up;
            aim.Normalize();
            foreach (var payload in packet.Payloads)
            {
                // Work on a copy: a packet snapshot is never rewritten, including mixed
                // sources and future echoes. Identity does not require a surviving caster.
                var returned = payload;
                var tuning = sim.Config.combat;
                int pierce = 0;
                float range = float.PositiveInfinity;
                if (!payload.Explodes && payload.Kind != AttackKind.Riposte)
                {
                    switch (payload.SourceCategory)
                    {
                        case ActorCategory.Acolyte: pierce = tuning.acolyteReturnPierce; break;
                        case ActorCategory.ScatterCaster:
                            returned.ReturnedDamage = tuning.scatterReturnDamage;
                            float ratio = payload.SourceSpreadHalfAngle > 0
                                ? Mathf.Min(1, tuning.scatterReturnHalfAngle / payload.SourceSpreadHalfAngle) : 1;
                            returned.SpreadOffsetDeg *= ratio;
                            range = tuning.scatterReturnRange;
                            break;
                        case ActorCategory.Boss:
                            returned.Kind = AttackKind.HeavyShot;
                            returned.ReturnedDamage = tuning.bossReturnDamage;
                            break;
                    }
                }
                Vector2 dir = Geometry2D.Rotate(aim, returned.SpreadOffsetDeg);
                Vector2 muzzle = origin + dir * (sim.Player.Radius + returned.Radius + 0.05f);
                sim.SpawnProjectile(returned, AttackFaction.Returned, muzzle, dir, root, isEcho: false,
                    power: power, pierce: pierce, maxDistance: range);
            }
            packet.Status = PacketStatus.Released;
            sim.Events.RaisePacketReleased(packet, root);
            return root;
        }
    }
}
