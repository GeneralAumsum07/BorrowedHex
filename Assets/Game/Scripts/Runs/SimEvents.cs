using System;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Typed events raised by one ArenaSim. Instance-owned (never static), so subscriptions die
    /// with the run and restart cannot accumulate duplicate listeners. Presentation and audio
    /// listeners are optional: combat never depends on anyone listening.
    /// </summary>
    public sealed partial class SimEvents
    {
        public event Action<Vector2, Vector2> Dashed;
        public event Action<int, int> PlayerHit;   // amount, source actor
        public event Action PlayerDied;

        internal void RaiseDash(Vector2 from, Vector2 dir) => Dashed?.Invoke(from, dir);
        internal void RaisePlayerHit(int amount, int source) => PlayerHit?.Invoke(amount, source);
        internal void RaisePlayerDied() => PlayerDied?.Invoke();
    }
}
