using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Phase 7 encounter upgrades (section 5). One upgrade is held at a time and lasts exactly
    /// one encounter: chosen at a transition, expired when the next encounter is cleared. Every
    /// effect is read at the moment it acts (release, catch, orbit tick), never baked into a
    /// packet, so a packet caught under an upgrade and fired after it expired gets nothing.
    /// </summary>
    public sealed partial class ArenaSim
    {
        /// <summary>The upgrade in force, or null. Rank is 1 in short mode (endless ranks up, Phase 12).</summary>
        public UpgradeOffer? ActiveUpgrade { get; private set; }
        /// <summary>The upgrade that just expired, for the choice panel's "expiring" line.</summary>
        public UpgradeOffer? ExpiredUpgrade { get; private set; }
        /// <summary>The offers on screen while State is UpgradeChoice (empty otherwise).</summary>
        public readonly List<UpgradeOffer> Offers = new List<UpgradeOffer>();

        // Its own stream, like the pillars': drawing offers must not shift the formation picks a
        // seed produces, or every "same seed, same encounter" debugging session would break.
        SeededRandom upgradeRandom;
        UpgradeTuning UT => Config.upgrades;

        public bool Has(UpgradeId id) => ActiveUpgrade.HasValue && ActiveUpgrade.Value.Id == id;
        int ActiveRank => ActiveUpgrade.HasValue ? ActiveUpgrade.Value.Rank : 0;

        void InitUpgrades() => upgradeRandom = new SeededRandom(Setup.Seed ^ 0x55504752);

        /// <summary>
        /// The encounter was cleared: the held upgrade expires now (section 5) and three distinct
        /// offers are drawn. Rank is passed in so endless mode can reuse this unchanged.
        /// </summary>
        void OpenUpgradeChoice(int rank)
        {
            ExpiredUpgrade = ActiveUpgrade;
            ExpireUpgrade();
            Offers.Clear();
            var pool = new List<UpgradeId>(UpgradeInfo.Pool);
            int n = Mathf.Min(UT.offerCount, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int k = upgradeRandom.NextInt(0, pool.Count);
                Offers.Add(new UpgradeOffer(pool[k], rank));
                pool.RemoveAt(k);
            }
        }

        void ExpireUpgrade()
        {
            ActiveUpgrade = null;
            orbitNextHit.Clear();
        }

        /// <summary>
        /// Pick offer <paramref name="index"/> and leave the choice. The pick takes effect for the
        /// encounter (or boss fight) that starts now.
        /// </summary>
        public bool ChooseUpgrade(int index)
        {
            if (State != RunState.UpgradeChoice || index < 0 || index >= Offers.Count) return false;
            var pick = Offers[index];
            if (!ContinueFromUpgrade()) return false;
            ActiveUpgrade = pick;
            Events.RaiseUpgradeChosen(pick);
            return true;
        }

        /// <summary>Sandbox/dev helper: hold an upgrade without a choice screen (tests, practice).</summary>
        public void ForceUpgrade(UpgradeId id, int rank = 1)
        {
            ExpireUpgrade();
            ActiveUpgrade = new UpgradeOffer(id, Mathf.Max(1, rank));
        }

        /// <summary>Sandbox/dev helper: drop the held upgrade.</summary>
        public void ClearUpgrade() => ExpireUpgrade();

        // ---- Release-time modifiers -------------------------------------------------------

        /// <summary>Perfect bonus applied to perfect payloads fired NOW: base + Final Second.</summary>
        internal float PerfectBonusNow =>
            Stats.PerfectBonus + (Has(UpgradeId.FinalSecond) ? UpgradeTuning.ByRank(UT.finalSecondBonus, ActiveRank) : 0f);

        /// <summary>Extra pierce for returned non-explosive payloads (Piercing Return).</summary>
        internal int UpgradePierce => Has(UpgradeId.PiercingReturn) ? UT.piercePerRank * ActiveRank : 0;

        /// <summary>
        /// Called once per real release (never for an echo): schedules the echo and bursts the
        /// parting gift. The volley was already built with this release's modifiers, so the echo
        /// repeats exactly what was fired, at the echo fraction, from where the player is THEN.
        /// </summary>
        internal void AfterRelease(List<ReturnedPayload> volley, int root, float power, float perfectBonus)
        {
            if (Has(UpgradeId.PartingGift)) PartingGift(root);
            if (Has(UpgradeId.EchoVolley))
            {
                float echoPower = power * UpgradeTuning.ByRank(UT.echoFraction, ActiveRank);
                var copy = new List<ReturnedPayload>(volley);
                // Scheduled on the gameplay clock: pause and choices freeze it, death and run end
                // cancel it (Scheduler.CancelAll), so an echo can never fire into the results.
                Scheduler.Schedule(Clock.Now + UT.echoDelay, () =>
                {
                    if (!Player.Alive || Summary != null) return;
                    ReleaseService.SpawnVolley(this, copy, Player.Position, Player.AimDirection, root, true, echoPower, perfectBonus);
                    Events.RaiseEchoFired(root);
                });
            }
        }

        /// <summary>
        /// Parting Gift: a returned burst centred on the player. Root 0 on purpose: it is not a
        /// returned hit, so it builds no combo and does not count as the packet hitting.
        /// </summary>
        void PartingGift(int root)
        {
            float r = UpgradeTuning.ByRank(UT.partingGiftRadius, ActiveRank);
            float dmg = UpgradeTuning.ByRank(UT.partingGiftDamage, ActiveRank);
            Vector2 at = Player.Position;
            Events.RaiseExplosion(at, r, AttackFaction.Returned);
            var shot = new AttackSnapshot { DefinitionId = "parting_gift", Kind = AttackKind.Rocket };
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.IsActive(Clock.Now)) continue;
                float reach = r + e.Radius;
                if ((e.Position - at).sqrMagnitude > reach * reach) continue;
                DamageEnemy(e, dmg, DamageCategory.PartingGift, shot, 0);
            }
        }

        // ---- Heavy Orbit -------------------------------------------------------------------

        // Next time each enemy may take orbit damage: the "per enemy" interval of section 5.
        readonly Dictionary<int, double> orbitNextHit = new Dictionary<int, double>();

        /// <summary>
        /// Step 7: while any packet is held (selected or frozen), enemies whose bodies reach the
        /// orbit radius take damage, at most once per interval each. Ruling: the number of held
        /// packets does not multiply it; "held packets damage" reads as a state, not a count.
        /// Root 0: orbit damage is not a returned hit and never builds combo (section 5).
        /// </summary>
        void TickOrbit(double now)
        {
            if (!Has(UpgradeId.HeavyOrbit) || !Player.Alive || Packets.Packets.Count == 0) return;
            float r = UpgradeTuning.ByRank(UT.orbitRadius, ActiveRank);
            var shot = new AttackSnapshot { DefinitionId = "heavy_orbit", Kind = AttackKind.Bolt };
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.IsActive(now)) continue;
                float reach = r + e.Radius;
                if ((e.Position - Player.Position).sqrMagnitude > reach * reach) continue;
                if (orbitNextHit.TryGetValue(e.ActorId, out double due) && now < due - 1e-9) continue;
                orbitNextHit[e.ActorId] = now + UT.orbitInterval;
                DamageEnemy(e, UT.orbitDamage, DamageCategory.Orbit, shot, 0);
            }
        }

        /// <summary>Orbit radius in force, or 0 (for the view's ring).</summary>
        public float OrbitRadiusNow => Has(UpgradeId.HeavyOrbit) && Packets.Packets.Count > 0
            ? UpgradeTuning.ByRank(UT.orbitRadius, ActiveRank) : 0f;

        // ---- Slots-full catches: Overflow and Fusion ----------------------------------------

        /// <summary>
        /// Before the normal capture: if this catch would be refused because the hand is full
        /// (D91), Overflow fires the held hex to make room — pocket empty or not — and Fusion merges,
        /// but Fusion still needs BOTH slots full since it absorbs the other packet. Used at most
        /// once per activation. Returns the result if it fully handled the shot (Fusion), or null
        /// to continue with the normal path.
        /// </summary>
        CaptureResult? TryFullHandUpgrade(ref AttackSnapshot shot)
        {
            if (Capture.ActivePacket != null || Packets.HandFree || Capture.SlotsFullUpgradeUsed) return null;
            if (shot.EnergyCost > Stats.PacketCapacity) return null;
            var selected = Packets.InSlot(Packets.SelectedSlot);
            if (selected == null) return null;   // selected slot is the locked one: nothing to fire or merge into

            if (Has(UpgradeId.Overflow))
            {
                // Fire the selected packet at its CURRENT power from where the player stands,
                // then let the normal path store the catch in the slot it vacated.
                // Forced release: priming does not apply (D91), like Quick Draw does not (D79).
                Capture.MarkOverflowUsed();
                Packets.Remove(selected);
                Capture.Detach(selected);
                ReleaseService.Release(this, selected, Player.Position, Player.AimDirection, selected.FirePower(Stats.Power));   // R9: the Overcharge zone counts here too
                return null;
            }

            if (Has(UpgradeId.Fusion))
            {
                if (Packets.FreeSlots > 0) return null;   // a free pocket: nothing to merge (D91)
                CapturedPacket other = null;
                foreach (var p in Packets.Packets) if (p != selected) { other = p; break; }
                if (other == null) return null;
                Packets.MergeInto(selected, other);
                // Capacity is ignored for the merge itself; afterwards the packet counts as
                // full, so a later shot in the same window cannot keep piling on.
                selected.PowerScale = UT.fusionPowerScale;
                Capture.AdoptForFusion(selected, shot);
                selected.Capacity = Mathf.Max(selected.Capacity, selected.CapacityUsed);
                Events.RaisePacketsFused(selected, other);
                return CaptureResult.Fused;
            }
            return null;
        }

        /// <summary>
        /// Section 2 perfect catch: the shot's own path would have reached the player's body
        /// within the perfect window. Measured at the moment of capture, from where it is now.
        /// </summary>
        bool IsPerfectCatch(ProjectileActor p)
        {
            if (Stats.PerfectWindow <= 0f) return false;
            Vector2 end = p.Position + p.Velocity * Stats.PerfectWindow;
            return Geometry2D.SweepCircleVsCircle(p.Position, end, p.Radius, Player.Position, Player.Radius, out _);
        }
    }

    /// <summary>One payload ready to fire: the hex-ruled snapshot plus its pierce and range.</summary>
    public struct ReturnedPayload
    {
        public AttackSnapshot Shot;
        public int Pierce;
        public float Range;
    }
}
