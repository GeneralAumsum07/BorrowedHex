using BorrowedHex.Data;

namespace BorrowedHex.Player
{
    /// <summary>
    /// Effective player numbers for one run, resolved ONCE at run start from tuning, capture
    /// style and equipped permanent passives (Phase 8/10). Combat reads only this, never the
    /// config directly, so loadouts cannot leak into a run that has already begun and the
    /// definition assets are never mutated.
    /// </summary>
    public sealed class PlayerStats
    {
        public float MoveSpeed;
        public float StartingSeconds;
        public float HitInvulnerability;
        public float ContactInvulnerability;
        public float DashDistance;
        public float DashDuration;
        public float DashCooldown;
        public float DashInvulnerability;
        public float BodyRadius;

        public float CaptureRange;
        public float CaptureConeAngle;
        public float CaptureWindow;
        public float CaptureRecovery;
        public int PacketCapacity;
        public float PacketLifetime;
        public int PacketSlots;
        /// <summary>Rule B (D90): gameplay seconds after capture before a hex can be fired.</summary>
        public float PrimeSeconds;
        /// <summary>D93: decay-to-power curve with the Overcharge zone (replaces PowerPerSecond).</summary>
        public BorrowedHex.Combat.PowerCurve Power;
        public int BackfireSeconds;
        public float PerfectWindow;
        public float PerfectBonus;
        public float ParryRingRadius;
        public float ParryRingWidth;
        public float ParryWindowScale;
        /// <summary>
        /// Seconds from the start of a catch window during which a parry can land. Derived, so
        /// a future upgrade that lengthens the catch window keeps the owner's 0.5x ratio.
        /// </summary>
        public float ParryWindow => CaptureWindow * ParryWindowScale;

        /// <summary>
        /// Quick Draw (section 7 node): a release fired within <see cref="QuickDrawWindow"/> seconds
        /// of a swap deals 1 + <see cref="QuickDrawBonus"/> times damage. Zero without the node.
        /// </summary>
        public float QuickDrawBonus;
        public float QuickDrawWindow;

        /// <summary>Daredevil: catch performs a capturing dash sharing the dash cooldown.</summary>
        public bool CatchIsDash;
        /// <summary>Daredevil: capture reach around the player's centre along the dash path (D83).</summary>
        public float DashCatchRadius;

        public static PlayerStats FromConfig(GameConfig config)
        {
            var p = config.player;
            var c = config.capture;
            return new PlayerStats
            {
                MoveSpeed = p.moveSpeed,
                StartingSeconds = config.shortMode.runLength,
                HitInvulnerability = p.hitInvulnerability,
                ContactInvulnerability = p.contactInvulnerability,
                DashDistance = p.dashDistance,
                DashDuration = p.dashDuration,
                DashCooldown = p.dashCooldown,
                DashInvulnerability = p.dashInvulnerability,
                BodyRadius = p.bodyRadius,
                CaptureRange = c.range,
                CaptureConeAngle = c.coneAngle,
                CaptureWindow = c.window,
                CaptureRecovery = c.recovery,
                PacketCapacity = c.packetCapacity,
                PacketLifetime = c.packetLifetime,
                PacketSlots = c.packetSlots,
                PrimeSeconds = c.primeSeconds,
                Power = new BorrowedHex.Combat.PowerCurve(c.peakPower, c.powerCurveExponent, c.overchargeWindow, c.overchargeMultiplier),
                BackfireSeconds = c.backfireSeconds,
                PerfectWindow = c.perfectWindow,
                PerfectBonus = c.perfectBonus,
                ParryRingRadius = c.parryRingRadius,
                ParryRingWidth = c.parryRingWidth,
                ParryWindowScale = c.parryWindowScale,
            };
        }

        public PlayerStats Clone() => (PlayerStats)MemberwiseClone();
    }
}
