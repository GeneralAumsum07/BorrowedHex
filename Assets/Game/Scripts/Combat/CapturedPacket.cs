using System.Collections.Generic;
using BorrowedHex.Core;

namespace BorrowedHex.Combat
{
    // Merged: its payloads were folded into another packet by Fusion (Phase 7); it no longer exists.
    public enum PacketStatus { Collecting, Stored, Released, Backfired, Cancelled, Merged }

    /// <summary>
    /// One stored packet (section 3). Holds snapshot DATA only — never references to the
    /// projectiles or enemies it came from — so a shooter dying mid-carry changes nothing.
    /// </summary>
    public sealed class CapturedPacket
    {
        public int PacketId;
        /// <summary>
        /// The fixed slot this packet occupies until it is released (D31). A packet never moves
        /// slot, so the HUD panel and the Q selection always point at the same bundle.
        /// </summary>
        public int Slot;
        /// <summary>The catch activation that created it; only that activation may append.</summary>
        public int ActivationId;
        public double CapturedAt;
        /// <summary>Only time spent selected counts. Appending never renews this budget.</summary>
        public float Lifetime;
        public double DecayedTime;
        internal double AdvancedAt;
        public int Capacity;
        public int CapacityUsed;
        public PacketStatus Status = PacketStatus.Collecting;
        public readonly List<AttackSnapshot> Payloads = new List<AttackSnapshot>();

        public bool IsFull => CapacityUsed >= Capacity;
        public bool Fits(int cost) => CapacityUsed + cost <= Capacity;
        public float Remaining(double now) => (float)System.Math.Max(0.0, Lifetime - DecayedTime);
        /// <summary>D93: decay power from the eased curve, including the Overcharge step.</summary>
        public float Power(PowerCurve curve) => curve.Evaluate(DecayedTime, Lifetime);
        /// <summary>D93: inside the last OverchargeWindow seconds of its own (selected) decay.</summary>
        public bool IsOvercharged(PowerCurve curve) => curve.IsOvercharged(DecayedTime, Lifetime);
        /// <summary>Extra power multiplier (Fusion's +25%, section 5). 1 for an ordinary packet.</summary>
        public float PowerScale = 1f;
        /// <summary>The multiplier a release uses: decay power times any fusion scale.</summary>
        public float FirePower(PowerCurve curve) => Power(curve) * PowerScale;

        /// <summary>The dominant kind for HUD icons: the most expensive payload carried.</summary>
        public AttackKind DominantKind
        {
            get
            {
                AttackKind k = AttackKind.Bolt;
                int best = -1;
                foreach (var p in Payloads)
                    if (p.EnergyCost > best) { best = p.EnergyCost; k = p.Kind; }
                return k;
            }
        }
    }

    /// <summary>
    /// The packet slots (D31): each packet owns one fixed slot index from capture until it is
    /// released, and a new packet takes ONLY the selected slot, and only if it is empty (D89). A held slot is locked: nothing
    /// is ever appended to it after its catch window, so a later catch of any kind always lands
    /// in another slot. <see cref="SelectedSlot"/> is the player's Q selection for early release.
    ///
    /// Expiry is checked with a tiny tolerance so floating accumulation of
    /// 60 Hz steps still backfire at exactly three selected seconds rather than one tick late.
    /// </summary>
    public sealed class PacketStore
    {
        public const double ExpiryEpsilon = 1e-9;
        readonly List<CapturedPacket> packets = new List<CapturedPacket>();
        readonly List<CapturedPacket> expired = new List<CapturedPacket>();

        public int SlotCount { get; private set; }
        public IReadOnlyList<CapturedPacket> Packets => packets;
        // A slot locked by Fusion holds no packet but cannot take one either (section 5).
        public int FreeSlots => SlotCount - packets.Count - (LockedSlot >= 0 ? 1 : 0);

        /// <summary>
        /// Fusion lock (Phase 7): the slot the merged-away packet left, or -1. It stays locked
        /// until the packet that absorbed it fires, backfires or is cancelled, whichever comes first.
        /// </summary>
        public int LockedSlot { get; private set; } = -1;
        int lockOwnerId;

        public bool IsLocked(int slot) => slot == LockedSlot;

        /// <summary>Fusion: <paramref name="absorbed"/> leaves its slot, which stays locked to <paramref name="owner"/>.</summary>
        public void MergeInto(CapturedPacket owner, CapturedPacket absorbed)
        {
            owner.Payloads.AddRange(absorbed.Payloads);
            owner.CapacityUsed += absorbed.CapacityUsed;
            absorbed.Status = PacketStatus.Merged;
            packets.Remove(absorbed);
            LockedSlot = absorbed.Slot;
            lockOwnerId = owner.PacketId;
        }

        // Every way a packet leaves the store goes through here, so the lock can never outlive
        // the packet it waits for (a backfire, a death cancel and a release all unlock).
        void Left(CapturedPacket p)
        {
            if (LockedSlot >= 0 && p.PacketId == lockOwnerId) LockedSlot = -1;
        }

        /// <summary>Slot the right-mouse release fires first. Persists when that slot empties.</summary>
        public int SelectedSlot { get; private set; }

        public PacketStore(int slots) => SlotCount = slots;

        /// <summary>The packet in slot <paramref name="slot"/>, or null if that slot is free.</summary>
        public CapturedPacket InSlot(int slot)
        {
            foreach (var p in packets) if (p.Slot == slot) return p;
            return null;
        }

        public void CycleSelection() => SelectedSlot = SlotCount > 0 ? (SelectedSlot + 1) % SlotCount : 0;

        /// <summary>
        /// The packet an early release fires: the selected slot's, and ONLY that one (D33 revised,
        /// owner direction). An empty selected slot means right mouse does nothing. A fallback to
        /// "whatever else is held" made Q look cosmetic: the player could never be sure which
        /// bundle RMB would throw, which defeats the point of choosing one.
        /// </summary>
        public CapturedPacket ReleaseCandidate() => InSlot(SelectedSlot);

        /// <summary>Take a packet out of its slot (early release). The slot is free immediately.</summary>
        public bool Remove(CapturedPacket p)
        {
            if (!packets.Remove(p)) return false;
            Left(p);
            return true;
        }

        /// <summary>
        /// Rule A (D89): the selected slot is the hand. A catch lands there or nowhere — there is
        /// no fallback to the other slot any more, because banking a hex is the player's Q
        /// decision, not the store's. Before D89 the store auto-banked, which made one slot enough.
        /// </summary>
        public bool HandFree => InSlot(SelectedSlot) == null && !IsLocked(SelectedSlot);

        public CapturedPacket Create(int packetId, int activationId, double now, float lifetime, int capacity)
            => HandFree ? CreateInSlot(SelectedSlot, packetId, activationId, now, lifetime, capacity) : null;

        /// <summary>
        /// Put a packet in a specific slot. Gameplay goes through <see cref="Create"/>; this exists
        /// for tests and sandbox seeding that need "two hexes held" without playing two catches.
        /// </summary>
        public CapturedPacket CreateInSlot(int slot, int packetId, int activationId, double now, float lifetime, int capacity)
        {
            if (slot < 0 || slot >= SlotCount || InSlot(slot) != null || IsLocked(slot)) return null;
            var p = new CapturedPacket
            {
                PacketId = packetId,
                Slot = slot,
                ActivationId = activationId,
                CapturedAt = now,
                Lifetime = lifetime,
                AdvancedAt = now,
                Capacity = capacity,
            };
            packets.Add(p);
            return p;
        }

        /// <summary>
        /// Remove and return packets whose lifetime has ended. The returned list is reused;
        /// callers must consume it before the next call. Slots are free immediately.
        /// </summary>
        public List<CapturedPacket> Advance(double now)
        {
            expired.Clear();
            for (int i = 0; i < packets.Count; i++)
            {
                var packet = packets[i];
                // Advance the timestamp even on frozen packets: selecting one later must not
                // charge it for the time it spent banked. New catches start at their own time.
                double elapsed = System.Math.Max(0.0, now - packet.AdvancedAt);
                packet.AdvancedAt = System.Math.Max(now, packet.AdvancedAt);
                if (packet.Slot == SelectedSlot)
                    packet.DecayedTime = System.Math.Min(packet.Lifetime, packet.DecayedTime + elapsed);
                if (packet.DecayedTime >= packet.Lifetime - ExpiryEpsilon)
                {
                    expired.Add(packets[i]);
                    packets.RemoveAt(i--);
                    Left(packet);
                }
            }
            return expired;
        }

        /// <summary>Death/restart: drop every packet without releasing it.</summary>
        public void CancelAll()
        {
            foreach (var p in packets) p.Status = PacketStatus.Cancelled;
            packets.Clear();
            LockedSlot = -1;
        }

        public int TotalStoredShots()
        {
            int n = 0;
            foreach (var p in packets) n += p.Payloads.Count;
            return n;
        }
    }
}
