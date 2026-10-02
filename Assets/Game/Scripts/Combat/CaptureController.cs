using BorrowedHex.Core;
using BorrowedHex.Player;
using UnityEngine;

namespace BorrowedHex.Combat
{
    public enum CaptureResult
    {
        CreatedPacket,
        Appended,
        NotEligible,      // returned/echo/non-capturable/not approaching
        WindowClosed,
        PacketFull,       // this activation's packet cannot fit the shot
        SlotsFull,        // no packet yet this activation and both slots are occupied
    }

    /// <summary>
    /// Capture region test (section 3): inside the aim cone and range, AND moving toward the
    /// player. The "approaching" rule is what stops a shot that already flew past from being
    /// scooped up from behind.
    /// </summary>
    public static class CaptureGeometry
    {
        public static bool InRegion(Vector2 playerPos, Vector2 aim, float halfAngleDeg, float range,
            Vector2 shotPos, Vector2 shotVel, float shotRadius)
        {
            Vector2 toPlayer = playerPos - shotPos;
            if (Vector2.Dot(shotVel, toPlayer) <= 0f) return false;
            // No "touching the body counts" shortcut: section 2 requires the shot to be inside
            // the cone, so a side or rear impact during an open window must still hurt. The
            // coincident-impact tie is settled by the resolver's ordering, not by widening here.
            return Geometry2D.InCone(playerPos, aim, halfAngleDeg, range + shotRadius, shotPos);
        }

        /// <summary>
        /// Earliest fraction t∈[0,1] of this tick's travel at which the shot is inside the
        /// region, by sampling the segment no coarser than 0.1 units (plus any caller-supplied
        /// extra sample, e.g. the exact player-impact time so capture can win that tie).
        /// </summary>
        public static bool EarliestEntry(Vector2 playerPos, Vector2 aim, float halfAngleDeg, float range,
            Vector2 start, Vector2 end, Vector2 vel, float radius, float extraSample, out float t)
        {
            float len = (end - start).magnitude;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.1f), 1, 64);
            bool extraPending = extraSample >= 0f && extraSample <= 1f;
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n;
                // Test the extra sample in time order, so it can only win if nothing earlier did.
                if (extraPending && extraSample < s)
                {
                    extraPending = false;
                    if (Sample(extraSample)) { t = extraSample; return true; }
                }
                if (Sample(s)) { t = s; return true; }
            }
            if (extraPending && Sample(extraSample)) { t = extraSample; return true; }
            t = 0f;
            return false;

            bool Sample(float f) => InRegion(playerPos, aim, halfAngleDeg, range, Vector2.Lerp(start, end, f), vel, radius);
        }
    }

    /// <summary>
    /// Catch activations (section 3): one short window, then recovery. A packet is created
    /// only on the FIRST success of an activation; later successes in the same window append
    /// to it. An empty activation still costs recovery but occupies no slot.
    /// </summary>
    public sealed class CaptureController
    {
        public int ActivationId { get; private set; }
        public double WindowOpensAt { get; private set; } = double.NegativeInfinity;
        public double WindowEndsAt { get; private set; } = double.NegativeInfinity;
        public double RecoveryEndsAt { get; private set; } = double.NegativeInfinity;
        /// <summary>The packet created by the current activation, if any.</summary>
        public CapturedPacket ActivePacket { get; private set; }
        /// <summary>Overflow upgrade (Phase 6): may create one packet beyond the slots per activation.</summary>
        public bool OverflowAvailable;
        public bool OverflowUsedThisActivation { get; private set; }

        public bool IsWindowOpen(double now) => now >= WindowOpensAt && now <= WindowEndsAt + 1e-9;
        public bool IsReady(double now) => now >= RecoveryEndsAt - 1e-9;

        public bool TryActivate(double now, PlayerStats s)
        {
            if (!IsReady(now)) return false;
            ActivationId++;
            WindowOpensAt = now;
            WindowEndsAt = now + s.CaptureWindow;
            RecoveryEndsAt = now + Mathf.Max(s.CaptureRecovery, s.CaptureWindow);
            ActivePacket = null;
            OverflowUsedThisActivation = false;
            return true;
        }

        /// <summary>
        /// Decide whether one intercepted shot is stored. Pure bookkeeping — the caller has
        /// already established the shot is in the capture region this tick.
        /// </summary>
        public CaptureResult TryCapture(in AttackSnapshot shot, AttackFaction faction, bool isEcho, double now,
            PacketStore store, PlayerStats s, IdGenerator ids)
        {
            if (faction != AttackFaction.Hostile || isEcho || !shot.Capturable) return CaptureResult.NotEligible;
            if (!IsWindowOpen(now)) return CaptureResult.WindowClosed;

            if (ActivePacket != null)
            {
                // Same window: append without touching the timer. A full packet never spills
                // into a second packet within one activation.
                if (!ActivePacket.Fits(shot.EnergyCost)) return CaptureResult.PacketFull;
                Store(ActivePacket, shot);
                return CaptureResult.Appended;
            }

            if (shot.EnergyCost > s.PacketCapacity) return CaptureResult.PacketFull;
            if (store.FreeSlots <= 0) return CaptureResult.SlotsFull;   // Overflow handled in Phase 6

            ActivePacket = store.Create(ids.Next(), ActivationId, now, s.PacketLifetime, s.PacketCapacity);
            Store(ActivePacket, shot);
            return CaptureResult.CreatedPacket;
        }

        static void Store(CapturedPacket p, in AttackSnapshot shot)
        {
            p.Payloads.Add(shot);
            p.CapacityUsed += shot.EnergyCost;
        }

        /// <summary>Called each tick; closes the window's packet to further appends.</summary>
        public void Tick(double now)
        {
            if (ActivePacket != null && !IsWindowOpen(now))
            {
                if (ActivePacket.Status == PacketStatus.Collecting) ActivePacket.Status = PacketStatus.Stored;
                ActivePacket = null;
            }
        }

        /// <summary>
        /// Stop appending to <paramref name="p"/> (it was released early) WITHOUT closing the
        /// window: a later catch in the same window starts a fresh packet in a free slot.
        /// </summary>
        public void Detach(CapturedPacket p)
        {
            if (ActivePacket == p) ActivePacket = null;
        }

        public void Cancel()
        {
            ActivePacket = null;
            WindowEndsAt = double.NegativeInfinity;
        }
    }
}
