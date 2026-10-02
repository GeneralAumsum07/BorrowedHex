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

        /// <summary>An encounter upgrade was picked at a choice.</summary>
        public event Action<UpgradeOffer> UpgradeChosen;
        /// <summary>Echo Volley fired the echo of this root release.</summary>
        public event Action<int> EchoFired;
        /// <summary>Fusion merged the second packet into the first (the second no longer exists).</summary>
        public event Action<Combat.CapturedPacket, Combat.CapturedPacket> PacketsFused;

        internal void RaiseUpgradeChosen(UpgradeOffer o) => UpgradeChosen?.Invoke(o);
        internal void RaiseEchoFired(int root) => EchoFired?.Invoke(root);
        internal void RaisePacketsFused(Combat.CapturedPacket into, Combat.CapturedPacket from) => PacketsFused?.Invoke(into, from);
        internal void RaiseRunStateChanged(RunState s) => RunStateChanged?.Invoke(s);
        internal void RaiseRunEnded(RunSummary s) => RunEnded?.Invoke(s);
        internal void RaiseLifeClockChanged(float delta, UnityEngine.Vector2 at) => LifeClockChanged?.Invoke(delta, at);
    }
}
