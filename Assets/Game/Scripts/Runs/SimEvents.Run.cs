using System;
using BorrowedHex.Core;

namespace BorrowedHex.Runs
{
    /// <summary>Phase 5 run-flow events (state changes, the frozen end of a run).</summary>
    public sealed partial class SimEvents
    {
        public event Action<RunState> RunStateChanged;
        /// <summary>Raised exactly once per run, after the summary is frozen.</summary>
        public event Action<RunSummary> RunEnded;
        /// <summary>Actual capped gain/loss, with a world position for the floating feedback.</summary>
        public event Action<float, UnityEngine.Vector2> LifeClockChanged;

        internal void RaiseRunStateChanged(RunState s) => RunStateChanged?.Invoke(s);
        internal void RaiseRunEnded(RunSummary s) => RunEnded?.Invoke(s);
        internal void RaiseLifeClockChanged(float delta, UnityEngine.Vector2 at) => LifeClockChanged?.Invoke(delta, at);
    }
}
