using System.Collections.Generic;
using BorrowedHex.Core;

namespace BorrowedHex.Combat
{
    public enum PacketStatus { Collecting, Stored, Released, Cancelled }

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
        /// <summary>Fixed at creation: appending more shots never extends the lifetime.</summary>
        public double ExpiresAt;
        public int Capacity;
        public int CapacityUsed;
        public PacketStatus Status = PacketStatus.Collecting;
        public readonly List<AttackSnapshot> Payloads = new List<AttackSnapshot>();

        public bool IsFull => CapacityUsed >= Capacity;
        public bool Fits(int cost) => CapacityUsed + cost <= Capacity;
        public float Remaining(double now) => (float)System.Math.Max(0.0, ExpiresAt - now);
        public float Lifetime => (float)(ExpiresAt - CapturedAt);

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
    /// released, and a new packet takes the LOWEST free index. A held slot is locked: nothing
    /// is ever appended to it after its catch window, so a later catch of any kind always lands
    /// in another slot. <see cref="SelectedSlot"/> is the player's Q selection for early release.
    ///
    /// Expiry is checked with a tiny tolerance so floating accumulation of
    /// 60 Hz steps (e.g. 0.999999 + 3.0) still releases exactly on the 4.0 boundary tick
    /// rather than one tick late (Phase 3 boundary vector).
    /// </summary>
    public sealed class PacketStore
    {
        public const double ExpiryEpsilon = 1e-9;
        readonly List<CapturedPacket> packets = new List<CapturedPacket>();
        readonly List<CapturedPacket> expired = new List<CapturedPacket>();

        public int SlotCount { get; private set; }
        public IReadOnlyList<CapturedPacket> Packets => packets;
        public int FreeSlots => SlotCount - packets.Count;

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
        public bool Remove(CapturedPacket p) => packets.Remove(p);

        public CapturedPacket Create(int packetId, int activationId, double now, float lifetime, int capacity)
        {
            if (FreeSlots <= 0) return null;
            int slot = 0;
            while (InSlot(slot) != null) slot++;
            var p = new CapturedPacket
            {
                PacketId = packetId,
                Slot = slot,
                ActivationId = activationId,
                CapturedAt = now,
                ExpiresAt = now + lifetime,
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
                if (now >= packets[i].ExpiresAt - ExpiryEpsilon)
                {
                    expired.Add(packets[i]);
                    packets.RemoveAt(i--);
                }
            }
            return expired;
        }

        /// <summary>Death/restart: drop every packet without releasing it.</summary>
        public void CancelAll()
        {
            foreach (var p in packets) p.Status = PacketStatus.Cancelled;
            packets.Clear();
        }

        public int TotalStoredShots()
        {
            int n = 0;
            foreach (var p in packets) n += p.Payloads.Count;
            return n;
        }
    }
}
