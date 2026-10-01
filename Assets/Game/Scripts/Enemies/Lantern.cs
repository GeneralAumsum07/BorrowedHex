using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// The arcane lantern (section 3): a fixed, visible hostile emitter of slow capturable bolts.
    /// It is the ammunition safety net: if enemies remain but nothing can supply shots for
    /// <c>starvationDelay</c> seconds, it fires a pair every <c>interval</c> seconds until the
    /// condition ends, so a melee-only remainder never makes a run unwinnable.
    /// </summary>
    public sealed class Lantern
    {
        public int ActorId;
        public Vector2 Position;
        public double NextFireAt;
        /// <summary>When the current starvation began, or -1 while ammunition is available.</summary>
        public double StarvedSince = -1;
        public const float BodyRadius = 0.4f;
    }
}
