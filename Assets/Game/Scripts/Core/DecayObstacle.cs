using System;
using UnityEngine;

namespace BorrowedHex.Core
{
    /// <summary>
    /// A temporary solid obstacle. Time is its only damage source, so projectile impacts
    /// cannot accidentally wear cover down. The same state can serve future decaying props.
    /// </summary>
    public sealed class DecayObstacle
    {
        public readonly Rect Bounds;
        public int Durability { get; private set; }
        public int MaxDurability { get; private set; }
        public double RestoredAt { get; private set; }
        public float DecayInterval { get; private set; }
        public bool Crumbled => Durability == 0;
        // Stage replacement is time-driven too; projectile impacts still never wear cover.
        internal void Crumble() => Durability = 0;

        public DecayObstacle(Rect bounds) => Bounds = bounds;

        public void Restore(double now, int durability, float interval)
        {
            RestoredAt = now;
            MaxDurability = Durability = Math.Max(1, durability);
            DecayInterval = Mathf.Max(0.001f, interval);
        }

        /// <returns>True only on the tick it crumbles, for one collision removal/event.</returns>
        public bool Advance(double now)
        {
            if (Crumbled) return false;
            // Derive wear from gameplay time rather than accumulating a float timer; both
            // a hitch and 4320 small ticks hit the exact 72 s boundary for a six-second rate.
            int wear = (int)Math.Floor(Math.Max(0, now - RestoredAt) / DecayInterval + 1e-6);
            Durability = Math.Max(0, MaxDurability - wear);
            return Crumbled;
        }
    }
}
