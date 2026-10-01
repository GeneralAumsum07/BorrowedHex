using System.Collections.Generic;

namespace BorrowedHex.Core
{
    public interface IGameplayClock
    {
        double Now { get; }
        bool IsPaused { get; }
        void Advance(float deltaSeconds);
        void SetPaused(bool paused);
    }

    /// <summary>
    /// The single gameplay clock (section 3). Movement cooldowns, projectile lifetime,
    /// packet expiry, telegraphs, upgrades and run duration all read this clock, so
    /// freezing it is the one switch that freezes combat. UI animation uses unscaled time.
    /// </summary>
    public sealed class GameplayClock : IGameplayClock
    {
        // Double precision: a short run lasts 180 s and endless can run far longer; float
        // would start dropping sub-millisecond steps and make expiry boundaries flaky.
        double now;
        readonly HashSet<PauseReason> reasons = new HashSet<PauseReason>();

        public double Now => now;
        public bool IsPaused => reasons.Count > 0;

        public void Advance(float deltaSeconds)
        {
            // Zero, negative, NaN and infinite deltas come from focus loss or editor stalls;
            // none of them may move gameplay time.
            if (IsPaused) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f) return;
            now += deltaSeconds;
        }

        public void SetPaused(bool paused) => SetPauseReason(PauseReason.Manual, paused);

        public void SetPauseReason(PauseReason reason, bool paused)
        {
            if (paused) reasons.Add(reason);
            else reasons.Remove(reason);
        }

        public bool HasPauseReason(PauseReason reason) => reasons.Contains(reason);

        /// <summary>Start a fresh run timeline. Clears every pause reason too.</summary>
        public void Reset()
        {
            now = 0;
            reasons.Clear();
        }
    }
}
