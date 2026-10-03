using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Player;
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
            InitUpgrades();
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
        /// Rule B (D90): a fresh hex is unstable for PrimeSeconds after its capture. Measured on
        /// the gameplay clock since capture, NOT selected time (R1): a pocketed hex keeps priming,
        /// so catching, pocketing, firing the other hex and swapping back is a fluid rhythm.
        /// The clock freezes in menus and choices, so priming does too.
        /// </summary>
        public bool IsPrimed(CapturedPacket p) => p != null && Clock.Now - p.CapturedAt >= Stats.PrimeSeconds - 1e-6;

        /// <summary>
        /// Right mouse (D32): fire the selected packet now, from the current position along the
        /// current aim at its accumulated power. Selection stays put when the slot empties.
        /// </summary>
        bool TryReleaseEarly()
        {
            var packet = Packets.ReleaseCandidate();
            if (packet == null) return false;
            // An unstable hex refuses the fire command and stays in the hand: with Rule A that
            // means a second incoming shot can only be answered by Q (D89/D90). Overflow's
            // forced release does not come through here, so it ignores priming (D91).
            if (!IsPrimed(packet)) { Events.RaiseReleaseRefused(packet); return false; }
            Packets.Remove(packet);
            Capture.Detach(packet);
            ReleaseService.Release(this, packet, Player.Position, Player.AimDirection,
                packet.FirePower(Stats.PowerPerSecond) * QuickDrawMultiplier(Clock.Now));
            return true;
        }

        /// <summary>When the selection last changed (gameplay time); -inf before the first swap.</summary>
        public double LastSwapAt { get; private set; } = double.NegativeInfinity;

        /// <summary>
        /// Quick Draw (section 7). Applies to the player's own fire command only: the swap-then-
        /// fire rhythm is what it rewards. A swap and fire on the same tick counts (swap runs
        /// first in TickPlayer). Overflow's forced release is a catch side effect, so it does not
        /// get the bonus (my ruling, D79).
        /// </summary>
        float QuickDrawMultiplier(double now)
        {
            if (Stats.QuickDrawBonus <= 0f) return 1f;
            return now - LastSwapAt <= Stats.QuickDrawWindow + 1e-6 ? 1f + Stats.QuickDrawBonus : 1f;
        }

        void TryCatch(double now)
        {
            if (Stats.CatchIsDash) return;   // Daredevil routes the catch through the dash (TryDashCatch)
            if (Capture.TryActivate(now, Stats)) Events.RaiseCatchActivated(Capture.ActivationId);
        }

        /// <summary>Daredevil: whether the body was dashing during this tick's move, and where it went.</summary>
        internal bool DashSweepActive;
        internal Vector2 DashSweepFrom, DashSweepTo;

        /// <summary>
        /// Daredevil's catch (section 7): a dash whose whole path is the capture region. Both
        /// gates must be open, so a catch press never produces half an action (a dash with no
        /// window, or a window that stays put). The window opens at the tick's START, the same
        /// instant the dash starts, and lasts exactly the dash.
        /// </summary>
        void TryDashCatch(double tickStart)
        {
            if (!Capture.IsReady(tickStart)) return;
            // The capturing dash lunges toward the AIM, not the movement keys (D88): the catch
            // is aimed at a shot, so the body goes where the cursor says the shot is, and the
            // player can keep strafing one way while lunging another. The plain dash (TryDash)
            // still follows movement. Aim was already updated from this tick's command.
            if (!PlayerMotor.TryStartDash(Player, Player.AimDirection, Stats, tickStart)) return;
            Events.RaiseDash(Player.Position, Player.DashDirection);
            if (Capture.TryActivate(tickStart, Stats)) Events.RaiseCatchActivated(Capture.ActivationId);
        }

        /// <summary>
        /// Earliest fraction of a shot's travel this tick at which it meets the dash path. Both
        /// move during the tick, so each sample pairs the shot with the body at the same moment.
        /// A wall or standing pillar between the body and the shot blocks the catch: a dash past
        /// a pillar does not reach round it (Phase 11 check). A crumbled pillar has already left
        /// Walls, so it no longer blocks. There is no "approaching" rule here, unlike the cone:
        /// running a shot down from behind is what this style is for (D83).
        /// </summary>
        bool DashSweepEntry(Vector2 start, Vector2 end, float shotRadius, float extraSample, out float t)
        {
            t = 0f;
            if (!DashSweepActive) return false;
            float reach = Stats.DashCatchRadius + shotRadius;
            Vector2 from = DashSweepFrom, to = DashSweepTo;
            float len = Mathf.Max((end - start).magnitude, (to - from).magnitude);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 0.1f), 1, 64);
            bool extraPending = extraSample >= 0f && extraSample <= 1f;
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n;
                // The exact impact time is tested in order, so capture can win that tie (as the cone does).
                if (extraPending && extraSample < s)
                {
                    extraPending = false;
                    if (Sample(extraSample)) { t = extraSample; return true; }
                }
                if (Sample(s)) { t = s; return true; }
            }
            if (extraPending && Sample(extraSample)) { t = extraSample; return true; }
            return false;

            bool Sample(float f)
            {
                Vector2 body = Vector2.Lerp(from, to, f);
                Vector2 shot = Vector2.Lerp(start, end, f);
                return (shot - body).sqrMagnitude <= reach * reach && ClearLine(body, shot);
            }
        }

        /// <summary>True when no solid box (border wall or standing pillar) lies between two points.</summary>
        bool ClearLine(Vector2 a, Vector2 b)
        {
            foreach (var w in Walls)
                if (Geometry2D.SweepCircleVsRect(a, b, 0f, w, out _)) return false;
            return true;
        }

        /// <summary>Called by the projectile resolver when capture is the earliest contact.</summary>
        bool TryCaptureProjectile(ProjectileActor p)
        {
            // The stored copy records whether this was a perfect catch (Phase 7); the live
            // projectile's own snapshot is left alone.
            var shot = p.Shot;
            if (p.Faction == AttackFaction.Hostile && !p.IsEcho && shot.Capturable) shot.Perfect = IsPerfectCatch(p);
            var special = TryFullHandUpgrade(ref shot);
            var result = special ?? Capture.TryCapture(shot, p.Faction, p.IsEcho, Clock.Now, Packets, Stats, Ids);
            if (result == CaptureResult.CreatedPacket || result == CaptureResult.Appended || result == CaptureResult.Fused)
            {
                Events.RaiseShotCaptured(Capture.ActivePacket, shot, p.Position, result);
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
    ///
    /// Modifier order (section 5): copied base payload -> per-enemy hex rule -> perfect bonus ->
    /// power (decay, Overflow's current power, Fusion's scale) -> echo fraction. The first two
    /// shape the snapshot here; the rest are per-projectile multipliers, so damage is computed
    /// once per payload at impact and an attack definition is never mutated.
    /// </summary>
    public static class ReleaseService
    {
        /// <returns>The root release ID shared by every shot (and later echo) of this release.</returns>
        public static int Release(ArenaSim sim, CapturedPacket packet, Vector2 origin, Vector2 aim, float power)
        {
            if (!sim.Player.Alive || packet.Status == PacketStatus.Released || packet.Status == PacketStatus.Backfired
                || packet.Status == PacketStatus.Cancelled || packet.Status == PacketStatus.Merged) return 0;
            int root = sim.Ids.Next();
            var volley = BuildVolley(sim, packet.Payloads);
            // Read the perfect bonus NOW: Final Second applies to packets fired while it is held,
            // not to packets that happened to be caught under it (section 5).
            float perfectBonus = sim.PerfectBonusNow;
            SpawnVolley(sim, volley, origin, aim, root, false, power, perfectBonus);
            packet.Status = PacketStatus.Released;
            sim.Events.RaisePacketReleased(packet, root);
            sim.AfterRelease(volley, root, power, perfectBonus);
            return root;
        }

        /// <summary>Apply the per-enemy hex rules (D61) and any upgrade pierce to copies of the payloads.</summary>
        static List<ReturnedPayload> BuildVolley(ArenaSim sim, List<AttackSnapshot> payloads)
        {
            var tuning = sim.Config.combat;
            int extraPierce = sim.UpgradePierce;
            var list = new List<ReturnedPayload>(payloads.Count);
            foreach (var payload in payloads)
            {
                // Work on a copy: a packet snapshot is never rewritten, including mixed
                // sources and future echoes. Identity does not require a surviving caster.
                var returned = payload;
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
                    // Piercing Return stacks with the acolyte's own pierce (section 5); rockets
                    // never pierce: their damage is the burst.
                    pierce += extraPierce;
                }
                list.Add(new ReturnedPayload { Shot = returned, Pierce = pierce, Range = range });
            }
            return list;
        }

        /// <summary>
        /// Spawn one volley. Used for the release and, unchanged, for its echo: an echo shares the
        /// root (one release for combo and Crowd Control), is flagged IsEcho (never recapturable,
        /// never echoes again) and carries the echo fraction inside its power.
        /// </summary>
        internal static void SpawnVolley(ArenaSim sim, List<ReturnedPayload> volley, Vector2 origin, Vector2 aim,
            int root, bool isEcho, float power, float perfectBonus)
        {
            if (aim.sqrMagnitude < 1e-8f) aim = Vector2.up;
            aim.Normalize();
            foreach (var r in volley)
            {
                Vector2 dir = Geometry2D.Rotate(aim, r.Shot.SpreadOffsetDeg);
                Vector2 muzzle = origin + dir * (sim.Player.Radius + r.Shot.Radius + 0.05f);
                sim.SpawnProjectile(r.Shot, AttackFaction.Returned, muzzle, dir, root, isEcho,
                    power: power, pierce: r.Pierce, maxDistance: r.Range,
                    perfectMultiplier: r.Shot.Perfect ? 1f + perfectBonus : 1f);
            }
        }
    }
}
