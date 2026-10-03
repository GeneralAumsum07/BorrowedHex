using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Encounter upgrades (section 5, reworked by D96). Upgrades are never discarded: at each
    /// choice the player may continue for free, swap a new card in for free, or pay a share of
    /// current life to add a card or rank one up. Four held locks the set; after that only
    /// rank-ups are offered. Every effect is read at the moment it acts (release, catch, orbit
    /// tick), never baked into a packet.
    /// </summary>
    public sealed partial class ArenaSim
    {
        /// <summary>Every upgrade in force, oldest first (0..maxHeld, distinct ids).</summary>
        public IReadOnlyList<UpgradeOffer> HeldUpgrades => held;
        readonly List<UpgradeOffer> held = new List<UpgradeOffer>(4);

        /// <summary>The offers on screen while State is UpgradeChoice (empty otherwise).</summary>
        public readonly List<UpgradeOffer> Offers = new List<UpgradeOffer>();

        // Its own stream, like the pillars': drawing offers must not shift the formation picks a
        // seed produces, or every "same seed, same encounter" debugging session would break.
        SeededRandom upgradeRandom;
        UpgradeTuning UT => Config.upgrades;

        /// <summary>D96: at maxHeld no new card can join; only rank-ups are offered (owner).</summary>
        // Max(1, ...) so a zeroed tuning value cannot lock the set before anything is held.
        public bool UpgradesLocked => held.Count >= Mathf.Max(1, UT.maxHeld);

        // Each effect reads the rank of ITS OWN upgrade (R13g), so a rank-3 endless pick and a
        // rank-1 older upgrade never borrow each other's rank.
        public bool Has(UpgradeId id) => RankOf(id) > 0;

        /// <summary>Rank of a held upgrade, or 0 when it is not held.</summary>
        public int RankOf(UpgradeId id)
        {
            // A linear scan over at most four entries: cheaper than keeping a dictionary in sync.
            for (int i = 0; i < held.Count; i++) if (held[i].Id == id) return held[i].Rank;
            return 0;
        }

        /// <summary>An offer of something already held is a rank-up card (R13b).</summary>
        public bool IsRankUp(UpgradeOffer offer) => Has(offer.Id);

        /// <summary>Swaps need something to replace, and stop once the set is locked (owner).</summary>
        public bool CanSwap => State == RunState.UpgradeChoice && held.Count > 0 && !UpgradesLocked;

        /// <summary>
        /// D96: "You rely on borrowed power, and it comes with a price." What an add or a
        /// rank-up costs right now, as a share of CURRENT life set by how many you hold. The
        /// clock is paused during a choice, so the price on the panel is the price paid (R13e).
        /// </summary>
        public float TakeCostFraction
        {
            get
            {
                var t = UT.takeCostByHeld;
                // Index = held count, capped at the last entry: four held still costs the 3+ price.
                return t == null || t.Length == 0 ? 0f : t[Mathf.Min(held.Count, t.Length - 1)];
            }
        }

        /// <summary>Seconds of life any paid pick costs right now.</summary>
        public float TakeCost => (float)(lifeSeconds * TakeCostFraction);

        void InitUpgrades() => upgradeRandom = new SeededRandom(Setup.Seed ^ 0x55504752);

        /// <summary>
        /// The rank an offer of <paramref name="id"/> carries. A held card is offered one step up,
        /// never more (owner, 3 Oct 2026: "only rank x+1 of that card can appear, not x+2"), even
        /// in an endless cycle whose fresh cards are higher; capped at maxRank. A card not held
        /// carries <paramref name="cycleRank"/> (1 in short mode, the cycle's rank in endless).
        /// </summary>
        public int OfferRankFor(UpgradeId id, int cycleRank)
        {
            int r = RankOf(id);
            return r > 0 ? Mathf.Min(Mathf.Max(1, UT.maxRank), r + 1) : Mathf.Max(1, cycleRank);
        }

        /// <summary>
        /// The encounter was cleared: distinct offers are drawn from upgrades that can still
        /// improve. Nothing expires (R13a). Rank is passed in so endless can offer its cycle's rank.
        /// </summary>
        void OpenUpgradeChoice(int rank)
        {
            Offers.Clear();
            // Built in UpgradeInfo.Pool order and filtered, so with nothing held the pool (and so
            // a seed's first offers) is exactly what it was before D96.
            var pool = new List<UpgradeId>();
            foreach (var id in UpgradeInfo.Pool)
            {
                int r = RankOf(id);
                if (r >= UT.maxRank) continue;           // R13b: nothing left to rank up
                if (r == 0 && UpgradesLocked) continue;  // R13c: locked = rank-ups only
                pool.Add(id);
            }
            int n = Mathf.Min(UT.offerCount, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int k = upgradeRandom.NextInt(0, pool.Count);
                var id = pool[k];
                Offers.Add(new UpgradeOffer(id, OfferRankFor(id, rank)));
                pool.RemoveAt(k);
            }
        }

        /// <summary>
        /// D96. Take offer <paramref name="index"/> and leave the choice:
        /// - a held upgrade's card ranks it up and costs TakeCost;
        /// - a new card with <paramref name="replace"/> >= 0 swaps out that held upgrade, free;
        /// - a new card otherwise joins the set and costs TakeCost (refused once locked).
        /// ContinueFromUpgrade is the free "take nothing".
        /// </summary>
        public bool ChooseUpgrade(int index, int replace = -1)
        {
            if (State != RunState.UpgradeChoice || index < 0 || index >= Offers.Count) return false;
            var pick = Offers[index];
            bool rankUp = IsRankUp(pick);
            // Only a NEW card can be swapped in. A replace index on a rank-up card is refused
            // rather than silently ignored, so a caller that meant "swap" never pays by accident
            // (and once locked, where every card is a rank-up, no swap call can succeed).
            if (rankUp && replace >= 0) return false;
            bool swap = !rankUp && replace >= 0;
            if (swap && (!CanSwap || replace >= held.Count)) return false;
            if (!rankUp && !swap && UpgradesLocked) return false;
            // Read BEFORE the set changes: the price is set by how many you held when you chose.
            float cost = swap ? 0f : TakeCost;
            if (!ContinueFromUpgrade()) return false;

            if (rankUp)
            {
                // Resolved here, not trusted from the offer: a rank-up is ALWAYS exactly one step
                // (owner), however the offer was built (tests, sandbox offers).
                int k = held.FindIndex(o => o.Id == pick.Id);
                pick = new UpgradeOffer(pick.Id, OfferRankFor(pick.Id, pick.Rank));
                held[k] = pick;
            }
            else if (swap)
            {
                // The orbit's per-enemy cooldowns belong to the orbit; drop them with it.
                if (held[replace].Id == UpgradeId.HeavyOrbit) orbitNextHit.Clear();
                held[replace] = pick;
            }
            else held.Add(pick);

            if (cost > 0f)
            {
                // A price, not a hit (R13f): no invulnerability, no combo reset, not health lost
                // to hits. At most half of a positive number, so it can never kill.
                double before = lifeSeconds;
                lifeSeconds = System.Math.Max(0, lifeSeconds - cost);
                float paid = (float)(before - lifeSeconds);
                Score.RecordUpgradePaid(paid);
                Events.RaiseUpgradePaid(pick, paid);
                // Reuses the life-change event so the HUD bar flashes red: the sacrifice should be felt.
                Events.RaiseLifeClockChanged(-paid, Player.Position);
            }
            Score.RecordHeld(held.Count);
            Events.RaiseUpgradeChosen(pick);
            return true;
        }

        /// <summary>Sandbox/dev helper: hold exactly one upgrade without a choice screen (tests, practice).</summary>
        public void ForceUpgrade(UpgradeId id, int rank = 1)
        {
            held.Clear();
            orbitNextHit.Clear();
            held.Add(new UpgradeOffer(id, Mathf.Max(1, rank)));
        }

        /// <summary>Sandbox/dev helper: drop every held upgrade.</summary>
        public void ClearUpgrade() { held.Clear(); orbitNextHit.Clear(); }

        /// <summary>Sandbox/test helper: add rank-1 upgrades on top of what is held, up to maxHeld.</summary>
        public void DebugHold(params UpgradeId[] ids)
        {
            foreach (var id in ids)
                if (!Has(id) && !UpgradesLocked) held.Add(new UpgradeOffer(id, 1));
        }

        /// <summary>Test helper: open a choice with exactly these offers.</summary>
        public void DebugOpenChoice(params UpgradeOffer[] offers)
        {
            Offers.Clear();
            Offers.AddRange(offers);
            Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
            SetState(RunState.UpgradeChoice);
        }

        // ---- Release-time modifiers -------------------------------------------------------

        /// <summary>Perfect bonus applied to perfect payloads fired NOW: base + Final Second.</summary>
        internal float PerfectBonusNow =>
            Stats.PerfectBonus + (Has(UpgradeId.FinalSecond) ? UpgradeTuning.ByRank(UT.finalSecondBonus, RankOf(UpgradeId.FinalSecond)) : 0f);

        /// <summary>Extra pierce for returned non-explosive payloads (Piercing Return).</summary>
        internal int UpgradePierce => UT.piercePerRank * RankOf(UpgradeId.PiercingReturn);

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
                float echoPower = power * UpgradeTuning.ByRank(UT.echoFraction, RankOf(UpgradeId.EchoVolley));
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
            float r = UpgradeTuning.ByRank(UT.partingGiftRadius, RankOf(UpgradeId.PartingGift));
            float dmg = UpgradeTuning.ByRank(UT.partingGiftDamage, RankOf(UpgradeId.PartingGift));
            Vector2 at = Player.Position;
            // Announce the gift first: its Explosion follows on every release, and presentation
            // must not give it the rocket's camera shake (spec section 5.1).
            Events.RaisePartingGiftBurst(at, r);
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
            float r = UpgradeTuning.ByRank(UT.orbitRadius, RankOf(UpgradeId.HeavyOrbit));
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
            ? UpgradeTuning.ByRank(UT.orbitRadius, RankOf(UpgradeId.HeavyOrbit)) : 0f;

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

            // R13h: with both held, Overflow wins; it frees the hand, so Fusion has nothing to merge.
            if (Has(UpgradeId.Overflow))
            {
                // Fire the selected packet at its CURRENT power from where the player stands,
                // then let the normal path store the catch in the slot it vacated.
                // Forced release: priming does not apply (D91), like Quick Draw does not (D79).
                Capture.MarkOverflowUsed();
                Packets.Remove(selected);
                Capture.Detach(selected);
                ReleaseService.Release(this, selected, Player.Position, Player.AimDirection, selected.FirePower(Stats.Power));   // R9: the Overcharge zone counts here too
                // Overflow's release otherwise looks like any release (PacketReleased) and the
                // catch after it like any catch; this is the only signal that names it.
                Events.RaiseOverflowFired(Player.Position);
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
