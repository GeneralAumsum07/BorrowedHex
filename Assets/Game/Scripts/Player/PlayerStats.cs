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
        public int MaxHealth;
        public float HitInvulnerability;
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
        public float PerfectWindow;
        public float PerfectBonus;

        /// <summary>Daredevil: catch performs a capturing dash sharing the dash cooldown.</summary>
        public bool CatchIsDash;

        public static PlayerStats FromConfig(GameConfig config)
        {
            var p = config.player;
            var c = config.capture;
            return new PlayerStats
            {
                MoveSpeed = p.moveSpeed,
                MaxHealth = p.maxHealth,
                HitInvulnerability = p.hitInvulnerability,
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
                PerfectWindow = c.perfectWindow,
                PerfectBonus = c.perfectBonus,
            };
        }

        public PlayerStats Clone() => (PlayerStats)MemberwiseClone();
    }
}
