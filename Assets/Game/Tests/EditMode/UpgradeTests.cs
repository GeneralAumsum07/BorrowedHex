using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Phase 7: encounter upgrades (section 5), perfect catches, offers and lifetimes.</summary>
    public class UpgradeTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 East = new Vector2(8f, 0f);
        static PlayerCommand Hold => PlayerCommand.Moving(Vector2.zero).WithAim(East);
        static PlayerCommand Fire => Hold.WithRelease();

        /// <summary>A held packet built directly (no catch), in the selected slot first.</summary>
        static CapturedPacket Packet(ArenaSim sim, string attack = AttackIds.Bolt, bool perfect = false,
            ActorCategory source = ActorCategory.Pursuer, int count = 1)
        {
            var p = sim.Packets.Create(sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            for (int i = 0; i < count; i++)
            {
                var s = AttackSnapshot.From(sim.Attacks.Get(attack), 42, sim.Ids.Next(), 0);
                s.Perfect = perfect;
                s.SourceCategory = source;
                p.Payloads.Add(s);
                p.CapacityUsed += s.EnergyCost;
            }
            p.Status = PacketStatus.Stored;
            return p;
        }

        static List<ProjectileActor> CaptureSpawns(ArenaSim sim)
        {
            var list = new List<ProjectileActor>();
            // Copy values we need at spawn time: the actor is pooled and reused later.
            sim.Events.ProjectileSpawned += p => { if (p.Faction == AttackFaction.Returned) list.Add(p); };
            return list;
        }

        static void HostileShot(ArenaSim sim, Vector2 from, string id = AttackIds.Bolt)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(id), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        // ---- Offers and lifetime ----------------------------------------------------------

        [Test]
        public void ClearingAnEncounter_OffersThreeDistinctUpgrades_ReproducedBySeed()
        {
            List<UpgradeOffer> Offers(int seed)
            {
                var sim = P5.Short(seed);
                P5.Invulnerable(sim);
                P5.ClearEncounter(sim);
                Assert.AreEqual(RunState.UpgradeChoice, sim.State);
                return new List<UpgradeOffer>(sim.Offers);
            }
            var a = Offers(7);
            var b = Offers(7);
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(3, new HashSet<UpgradeId>(a.ConvertAll(o => o.Id)).Count, "distinct");
            CollectionAssert.AreEqual(a.ConvertAll(o => o.Id), b.ConvertAll(o => o.Id), "same seed, same offers");
            Assert.IsTrue(a.TrueForAll(o => o.Rank == 1), "short mode offers rank 1");
        }

        [Test]
        public void DrawingOffers_DoesNotShiftTheSeededEncounters()
        {
            // Offers use their own RNG stream: the second encounter's spawns for a seed must be
            // the same whichever upgrade was picked.
            List<ActorCategory> SecondEncounter(int pick)
            {
                var sim = P5.Short(3);
                P5.Invulnerable(sim);
                P5.ClearEncounter(sim);
                var seen = new List<ActorCategory>();
                sim.Events.EnemySpawned += e => seen.Add(e.Category);
                Assert.IsTrue(sim.ChooseUpgrade(pick));
                P5.ClearEncounter(sim);
                return seen;
            }
            CollectionAssert.AreEqual(SecondEncounter(0), SecondEncounter(2));
        }

        [Test]
        public void ChosenUpgrade_LastsOneEncounter_ThenExpiresAtTheNextClear()
        {
            var sim = P5.Short(5);
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            var pick = sim.Offers[1];
            Assert.IsTrue(sim.ChooseUpgrade(1));
            Assert.AreEqual(RunState.Combat, sim.State);
            Assert.AreEqual(pick.Id, sim.ActiveUpgrade.Value.Id);
            Assert.AreEqual(0, sim.Offers.Count, "offers are gone once chosen");

            P5.ClearEncounter(sim);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            Assert.IsFalse(sim.ActiveUpgrade.HasValue, "expired when its encounter was cleared");
            Assert.AreEqual(pick.Id, sim.ExpiredUpgrade.Value.Id, "the panel can say what expired");
        }

        [Test]
        public void UpgradePickedAfterEncounterThree_AppliesToTheBossFight()
        {
            var sim = P5.Short(2);
            P5.Invulnerable(sim);
            for (int i = 0; i < 2; i++) { P5.ClearEncounter(sim); sim.ContinueFromUpgrade(); }
            P5.ClearEncounter(sim);
            var pick = sim.Offers[0];
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(RunState.BossIntro, sim.State);
            Assert.IsTrue(sim.CompleteBossIntro());
            Assert.AreEqual(pick.Id, sim.ActiveUpgrade.Value.Id);
        }

        [Test]
        public void ChooseUpgrade_RejectsBadIndexAndWrongState()
        {
            var sim = P5.Short();
            Assert.IsFalse(sim.ChooseUpgrade(0), "not at a choice");
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            Assert.IsFalse(sim.ChooseUpgrade(3));
            Assert.IsFalse(sim.ChooseUpgrade(-1));
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
        }

        [Test]
        public void EveryUpgrade_HasNameKeyAndExactDescription()
        {
            var t = new UpgradeTuning();
            foreach (var id in UpgradeInfo.Pool)
            {
                Assert.IsNotEmpty(UpgradeInfo.Name(id));
                Assert.IsNotEmpty(UpgradeInfo.Describe(id, 1, t));
            }
            StringAssert.Contains("25%", UpgradeInfo.Describe(UpgradeId.EchoVolley, 1, t));
            StringAssert.Contains("55%", UpgradeInfo.Describe(UpgradeId.EchoVolley, 3, t));
            StringAssert.Contains("+20%", UpgradeInfo.Describe(UpgradeId.FinalSecond, 1, t));
        }

        // ---- Perfect catches ---------------------------------------------------------------

        [Test]
        public void CatchJustBeforeImpact_IsPerfect_CatchFarOut_IsNot()
        {
            bool CaughtPerfect(float distance)
            {
                var sim = P4.Sim();
                CapturedPacket got = null;
                sim.Events.ShotCaptured += (p, s, at, r) => got = p;
                HostileShot(sim, new Vector2(distance, 0f));
                sim.Tick(Hold.WithCatch(), Dt);
                Assert.IsNotNull(got, $"caught at {distance}");
                return got.Payloads[0].Perfect;
            }
            // Bolt speed 9: within 0.10 s it covers 0.9 units; the body reach is 0.35 + bolt radius.
            Assert.IsTrue(CaughtPerfect(1.1f));
            Assert.IsFalse(CaughtPerfect(2.5f));
        }

        [Test]
        public void PerfectPayload_DealsBaseBonus_AndFinalSecondAddsTwentyPoints()
        {
            float Multiplier(bool finalSecond)
            {
                var sim = P4.Sim();
                if (finalSecond) sim.ForceUpgrade(UpgradeId.FinalSecond);
                Packet(sim, perfect: true);
                var spawns = CaptureSpawns(sim);
                sim.Tick(Fire, Dt);
                Assert.AreEqual(1, spawns.Count);
                return spawns[0].PerfectMultiplier;
            }
            Assert.AreEqual(1.15f, Multiplier(false), 1e-5f);
            Assert.AreEqual(1.35f, Multiplier(true), 1e-5f);
        }

        // ---- Piercing Return ----------------------------------------------------------------

        [Test]
        public void PiercingReturn_AddsOnePierce_StacksWithAcolyte_NeverOnRockets()
        {
            int PierceOf(string attack, ActorCategory source, bool upgrade)
            {
                var sim = P4.Sim();
                if (upgrade) sim.ForceUpgrade(UpgradeId.PiercingReturn);
                Packet(sim, attack, source: source);
                var spawns = CaptureSpawns(sim);
                sim.Tick(Fire, Dt);
                return spawns[0].PierceRemaining;
            }
            Assert.AreEqual(0, PierceOf(AttackIds.Bolt, ActorCategory.Pursuer, false));
            Assert.AreEqual(1, PierceOf(AttackIds.Bolt, ActorCategory.Pursuer, true));
            int acolyte = TestSims.Config.combat.acolyteReturnPierce;
            Assert.AreEqual(acolyte + 1, PierceOf(AttackIds.Bolt, ActorCategory.Acolyte, true));
            Assert.AreEqual(0, PierceOf(AttackIds.Rocket, ActorCategory.SiegeFamiliar, true));
        }

        [Test]
        public void PiercingShot_NeverHitsTheSameEnemyTwice()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.PiercingReturn, 3);
            var e = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(3f, 0f));
            int hits = 0;
            sim.Events.EnemyDamaged += (x, d) => { if (x == e) hits++; };
            Packet(sim);
            sim.Tick(Fire, Dt);
            P4.Run(sim, 60, Hold);
            Assert.AreEqual(1, hits, "pierce passes through; it never re-hits the same body");
        }

        // ---- Echo Volley -------------------------------------------------------------------

        [Test]
        public void EchoVolley_RepeatsOnceAfterPointTwoSeconds_AtAQuarter_SameRoot()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.EchoVolley);
            Packet(sim);
            var shots = new List<(bool echo, int root, float power, double at)>();
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Faction == AttackFaction.Returned) shots.Add((p.IsEcho, p.RootReleaseId, p.PowerMultiplier, sim.Clock.Now));
            };
            sim.Tick(Fire, Dt);
            P4.Run(sim, 120, Hold);
            Assert.AreEqual(2, shots.Count, "one release, one echo, never an echo of the echo");
            Assert.IsFalse(shots[0].echo);
            Assert.IsTrue(shots[1].echo);
            Assert.AreEqual(shots[0].root, shots[1].root);
            Assert.AreEqual(shots[0].power * 0.25f, shots[1].power, 1e-5f);
            Assert.AreEqual(0.20, shots[1].at - shots[0].at, Dt + 1e-6);
        }

        [Test]
        public void PacketCaughtUnderEcho_FiredAfterItExpires_GetsNoEcho()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.EchoVolley);
            Packet(sim);
            sim.ForceUpgrade(UpgradeId.PiercingReturn);   // the echo upgrade is gone before firing
            var spawns = CaptureSpawns(sim);
            sim.Tick(Fire, Dt);
            P4.Run(sim, 60, Hold);
            Assert.AreEqual(1, spawns.Count);
        }

        [Test]
        public void EchoPendingAtDeath_NeverFires()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.EchoVolley);
            Packet(sim);
            var spawns = CaptureSpawns(sim);
            sim.Tick(Fire, Dt);
            Assert.IsTrue(sim.DamagePlayer(1000, 0));
            P4.Run(sim, 60, Hold);
            Assert.AreEqual(1, spawns.Count);
        }

        // ---- Parting Gift ------------------------------------------------------------------

        [Test]
        public void PartingGift_BurstsAroundThePlayer_WithoutBuildingCombo()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.PartingGift);
            var near = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(0f, 1.6f));
            var far = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(0f, 4f));
            float nearHp = near.Health, farHp = far.Health;
            Packet(sim);
            sim.Tick(Fire, Dt);   // fired east, away from both
            Assert.AreEqual(nearHp - 1f, near.Health, 1e-4f, "radius 1.5 reaches a body at 1.6");
            Assert.AreEqual(farHp, far.Health, 1e-4f);
            Assert.AreEqual(1f, sim.Score.Multiplier, "the gift is not a returned hit");
        }

        // ---- Heavy Orbit -------------------------------------------------------------------

        [Test]
        public void HeavyOrbit_DamagesNearbyEnemiesOncePerInterval_OnlyWhileHolding()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.HeavyOrbit);
            var e = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(1.2f, 0f));
            e.Radius = 0.3f; // body at 0.9 from the player's centre, inside 1.0
            float hp = e.Health;
            P4.Run(sim, 60, Hold);
            Assert.AreEqual(hp, e.Health, 1e-4f, "no packet held: no orbit");

            Packet(sim);
            // 0.35 s interval over 1 s: hits at 0, 0.35, 0.70 -> 3 hits (60 ticks).
            P4.Run(sim, 60, Hold);
            Assert.AreEqual(hp - 3f, e.Health, 1e-4f);
            Assert.AreEqual(1f, sim.Score.Multiplier, "orbit damage never builds combo");
        }

        [Test]
        public void OrbitKill_IsNotCountedAsAHexKill()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.HeavyOrbit);
            var e = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(0.9f, 0f));
            e.Health = 1f;
            Packet(sim);
            P4.Run(sim, 2, Hold);
            Assert.IsTrue(e.Killed);
            Assert.AreEqual(1, sim.Score.Kills);
            Assert.AreEqual(0, sim.Score.KillsByKind.Count);
        }

        // ---- Overflow ----------------------------------------------------------------------

        [Test]
        public void Overflow_FiresTheSelectedPacket_AndTheCatchTakesItsSlot()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            var first = Packet(sim);                       // slot 0 (selected)
            var second = Packet(sim, AttackIds.Rocket);    // slot 1
            CapturedPacket released = null;
            sim.Events.PacketReleased += (p, root) => released = p;
            HostileShot(sim, new Vector2(2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released);
            var now0 = sim.Packets.InSlot(0);
            Assert.IsNotNull(now0);
            Assert.AreNotSame(first, now0);
            Assert.AreSame(second, sim.Packets.InSlot(1), "the frozen packet is untouched");
        }

        [Test]
        public void WithoutOverflowOrFusion_FullSlotsRejectTheCatch()
        {
            var sim = P4.Sim();
            Packet(sim);
            Packet(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (at, r) => got = r;
            HostileShot(sim, new Vector2(2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.SlotsFull, got);
        }

        // ---- Fusion ------------------------------------------------------------------------

        [Test]
        public void Fusion_MergesIntoSelected_LocksTheOtherSlot_UntilTheMergedPacketFires()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.Fusion);
            var selected = Packet(sim, count: 2);            // slot 0
            var other = Packet(sim, AttackIds.Rocket);       // slot 1
            HostileShot(sim, new Vector2(2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);

            Assert.AreEqual(4, selected.Payloads.Count, "2 own + 1 frozen + the new catch");
            Assert.AreEqual(PacketStatus.Merged, other.Status);
            Assert.AreEqual(1.25f, selected.PowerScale, 1e-6f);
            Assert.IsTrue(sim.Packets.IsLocked(1));
            Assert.IsNull(sim.Packets.InSlot(1));
            Assert.AreEqual(0, sim.Packets.FreeSlots, "the locked slot cannot take a catch");

            // Let the window close, then fire the merged packet: the lock lifts.
            P4.Run(sim, 30, Hold);
            float power = selected.Power(sim.Stats.PowerPerSecond) * 1.25f;
            var spawns = CaptureSpawns(sim);
            sim.Tick(Fire, Dt);
            Assert.AreEqual(4, spawns.Count);
            Assert.AreEqual(power, spawns[0].PowerMultiplier, 0.02f);
            Assert.IsFalse(sim.Packets.IsLocked(1));
            Assert.AreEqual(2, sim.Packets.FreeSlots);
        }

        [Test]
        public void FusedPacket_BackfiresOnce_AndReleasesTheLock()
        {
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.Fusion);
            Packet(sim);
            Packet(sim);
            HostileShot(sim, new Vector2(2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            int backfires = 0;
            sim.Events.PacketBackfired += _ => backfires++;
            sim.Player.InvulnerableUntil = 0;
            P4.Run(sim, 60 * 4, Hold);
            Assert.AreEqual(1, backfires);
            Assert.IsFalse(sim.Packets.IsLocked(1));
            Assert.AreEqual(2, sim.Packets.FreeSlots);
        }

        [Test]
        public void SlotsFullUpgrade_UsedAtMostOncePerActivation()
        {
            // Overflow, then a second shot in the same window: it appends to the new packet
            // instead of firing again (one Overflow per activation, section 5).
            var sim = P4.Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            Packet(sim);
            Packet(sim);
            int releases = 0;
            sim.Events.PacketReleased += (p, r) => releases++;
            HostileShot(sim, new Vector2(2f, 0.1f));
            HostileShot(sim, new Vector2(2.4f, -0.1f));
            sim.Tick(Hold.WithCatch(), Dt);
            P4.Run(sim, 10, Hold);
            Assert.AreEqual(1, releases);
            Assert.IsTrue(sim.Capture.SlotsFullUpgradeUsed);
        }
    }
}
