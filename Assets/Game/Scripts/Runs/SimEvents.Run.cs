using System;
using BorrowedHex.Core;
using UnityEngine;

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
        /// <summary>D95: a kill extended or started a chain: its length and the bonus seconds it earned.</summary>
        public event Action<int, float> KillChainChanged;

        internal void RaiseKillChainChanged(int length, float bonus) => KillChainChanged?.Invoke(length, bonus);

        /// <summary>D96: life was paid for an upgrade (an add or a rank-up): the card and the seconds paid.</summary>
        public event Action<UpgradeOffer, float> UpgradePaid;
        internal void RaiseUpgradePaid(UpgradeOffer offer, float seconds) => UpgradePaid?.Invoke(offer, seconds);

        /// <summary>D99: lifesteal returned this many life seconds (after the cap) at this position.</summary>
        public event Action<float, Vector2> LifeStolen;
        internal void RaiseLifeStolen(float seconds, Vector2 at) => LifeStolen?.Invoke(seconds, at);

        // VFX pass (spec 2026-10-04 section 2): three moments the sim already decides but never
        // announced. Separate events, not flags on PacketReleased/Explosion, so every existing
        // subscriber of those events sees exactly what it saw before.

        /// <summary>A release earned the Quick Draw bonus; the player's position at the release.</summary>
        public event Action<Vector2> QuickDrawFired;
        internal void RaiseQuickDrawFired(Vector2 at) => QuickDrawFired?.Invoke(at);

        /// <summary>Overflow force-fired the held hex to make room for a catch; the player's position.</summary>
        public event Action<Vector2> OverflowFired;
        internal void RaiseOverflowFired(Vector2 at) => OverflowFired?.Invoke(at);

        /// <summary>
        /// Parting Gift burst around the player (every release). Raised IMMEDIATELY before the
        /// gift's own Explosion, so presentation can tell that explosion from a rocket's.
        /// </summary>
        public event Action<Vector2, float> PartingGiftBurst;
        internal void RaisePartingGiftBurst(Vector2 at, float radius) => PartingGiftBurst?.Invoke(at, radius);

        internal void RaiseUpgradeChosen(UpgradeOffer o) => UpgradeChosen?.Invoke(o);
        internal void RaiseEchoFired(int root) => EchoFired?.Invoke(root);
        internal void RaisePacketsFused(Combat.CapturedPacket into, Combat.CapturedPacket from) => PacketsFused?.Invoke(into, from);
        internal void RaiseRunStateChanged(RunState s) => RunStateChanged?.Invoke(s);
        internal void RaiseRunEnded(RunSummary s) => RunEnded?.Invoke(s);
        internal void RaiseLifeClockChanged(float delta, UnityEngine.Vector2 at) => LifeClockChanged?.Invoke(delta, at);
    }
}
