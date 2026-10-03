using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The 3 Oct 2026 rework (Docs/REWORK_PLAN.md): hand rule, priming, school resistance,
    /// Overcharge, kill chains, keeping an upgrade. One fixture so the rework's rules are
    /// readable in one place.
    /// </summary>
    public class ReworkTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 AimEast = new Vector2(5f, 0f);
        static PlayerCommand Hold => PlayerCommand.Moving(Vector2.zero).WithAim(AimEast);

        static ArenaSim Sim()
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        static void Shot(ArenaSim sim, Vector2 from, string id = AttackIds.Bolt)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(id), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Hold, Dt);
        }

        /// <summary>Catch one bolt, then let the catch recovery finish so the next press is live.</summary>
        static void CatchOne(ArenaSim sim)
        {
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
        }

        // ---- Rule A: the hand rule (D89) -----------------------------------------------------

        [Test]
        public void HandRule_FullHand_RejectsTheCatch_EvenWithAnEmptyPocket()
        {
            var sim = Sim();
            CatchOne(sim);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
            Assert.IsNull(sim.Packets.InSlot(1), "no automatic banking into the other slot");
        }

        [Test]
        public void HandRule_QPocketsTheHex_ThenTheFreeHandCatches()
        {
            var sim = Sim();
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
            CatchOne(sim);
            Assert.AreSame(first, sim.Packets.InSlot(0), "the pocketed hex is untouched");
            Assert.IsNotNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void HandRule_CycleAndCatchOnOneTick_CatchLandsInTheNewHand()
        {
            // Review Focus 1: cycle runs before catch in TickPlayer, so Q + click together is
            // "pocket, then catch" — the most natural panic input must not be punished.
            var sim = Sim();
            CatchOne(sim);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCycle().WithCatch(), Dt);
            Assert.IsNotNull(sim.Packets.InSlot(1));
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
        }

        [Test]
        public void HandRule_BothSlotsFull_StillReportsSlotsFull()
        {
            var sim = Sim();
            CatchOne(sim);
            TestSims.Pocket(sim);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.SlotsFull, got);
        }

        [Test]
        public void HandRule_ParryStillWorks_WithAFullHand()
        {
            // A parry makes no hex, so a full hand must not stop the catch window opening.
            var sim = Sim();
            CatchOne(sim);
            Assert.IsTrue(sim.Capture.IsReady(sim.Clock.Now));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
        }

        [Test]
        public void Store_Create_UsesOnlyTheSelectedSlot()
        {
            var store = new PacketStore(2);
            Assert.IsNotNull(store.Create(1, 1, 0, 3f, 12));
            Assert.IsNull(store.Create(2, 2, 0, 3f, 12), "selected slot occupied: no fallback");
            store.CycleSelection();
            Assert.AreEqual(1, store.Create(3, 3, 0, 3f, 12).Slot);
        }

        [Test]
        public void Overflow_TriggersOnAFullHand_WithThePocketEmpty()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            CapturedPacket released = null;
            sim.Events.PacketReleased += (p, _) => released = p;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            Assert.AreNotSame(first, sim.Packets.InSlot(0));
            Assert.IsNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void Fusion_NeedsBothSlotsFull_AFullHandAloneIsRejected()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Fusion);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
        }

        // ---- Rule B: priming (D90) ------------------------------------------------------------

        [Test]
        public void Priming_UnprimedRelease_IsRefusedOnce_ThenFiresWhenPrimed()
        {
            // Review Focus 2: the refused press keeps the hex, raises ONE refusal, fires nothing.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            Assert.IsNotNull(p);
            int refused = 0, released = 0;
            sim.Events.ReleaseRefused += _ => refused++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, refused);
            Assert.AreEqual(0, released);
            Assert.AreSame(p, sim.Packets.InSlot(0));
            // 0.4 s after capture it fires.
            while (sim.Clock.Now - p.CapturedAt < 0.4 - 1e-6) sim.Tick(Hold, Dt);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
            Assert.AreEqual(1, refused, "no refusal for the primed press");
        }

        [Test]
        public void Priming_ContinuesWhilePocketed()
        {
            // R1: priming is time since capture, so the juggle (catch, pocket, fire the other,
            // swap back, fire) never pays the 0.4 s twice.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Run(sim, 30);   // 0.5 s pocketed: DecayedTime barely moved
            Assert.Less(p.DecayedTime, 0.1);
            Assert.IsTrue(sim.IsPrimed(p));
            TestSims.Pocket(sim);   // back to slot 0
            int released = 0;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
        }

        [Test]
        public void Priming_OverflowForcedRelease_IgnoresIt()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var first = sim.Packets.InSlot(0);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
            // Still inside 0.4 s? Recovery is 0.65 s, so force freshness for the check:
            first.CapturedAt = sim.Clock.Now;
            Assert.IsFalse(sim.IsPrimed(first));
            CapturedPacket released = null;
            sim.Events.PacketReleased += (pk, _) => released = pk;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released, "Overflow fires an unprimed hex (D91)");
        }

        // ---- School resistance (D92) -----------------------------------------------------------

        static EnemyActor Spawned(ArenaSim sim, ActorCategory cat)
        {
            // The boss has no ordinary-enemy tuning (SpawnEnemy throws on it), so it comes in
            // through its own spawner.
            var e = cat == ActorCategory.Boss ? sim.SpawnBoss() : sim.SpawnEnemy(cat, new Vector2(4f, 0f));
            e.ActiveAt = 0;
            e.Health = e.MaxHealth = 100f;
            return e;
        }

        static AttackSnapshot From(ActorCategory school)
            => new AttackSnapshot { DefinitionId = "test", Kind = AttackKind.Bolt, SourceCategory = school };

        [TestCase(ActorCategory.Acolyte, ActorCategory.Acolyte, 0.75f)]
        [TestCase(ActorCategory.Acolyte, ActorCategory.SiegeFamiliar, 1.33f)]
        [TestCase(ActorCategory.SiegeFamiliar, ActorCategory.SiegeFamiliar, 0.75f)]
        public void School_OwnAttacksResisted_OthersAmplified(ActorCategory victim, ActorCategory school, float factor)
        {
            var sim = Sim();
            var e = Spawned(sim, victim);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(school), 1);
            Assert.AreEqual(100f - 10f * factor, e.Health, 1e-3f);
        }

        [TestCase(DamageCategory.Orbit)]
        [TestCase(DamageCategory.PartingGift)]
        public void School_UpgradeDamageIsNeutral(DamageCategory category)
        {
            // R5 (owner): upgrades get neither the resistance nor the x1.33.
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Acolyte);
            sim.DamageEnemy(e, 10f, category, From(ActorCategory.Player), 0);
            Assert.AreEqual(90f, e.Health, 1e-3f);
        }

        [Test]
        public void School_Riposte_CountsAsAnotherSource()
        {
            // R6 (owner): parry stays the Pursuer counter. The riposte snapshot is built exactly
            // as FireRiposte builds it, so a later change there that tags it with the Pursuer's
            // category fails this test.
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Pursuer);
            var riposte = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Riposte), 999, sim.Ids.Next(), 0f);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, riposte, 1);
            Assert.AreEqual(100f - 10f * 1.33f, e.Health, 1e-3f);
        }

        [Test]
        public void School_BossIgnoresIt()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Boss);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Boss), 1);
            Assert.AreEqual(90f, e.Health, 1e-3f, "R7");
        }

        [Test]
        public void School_DamageEventReportsTheScaledAmount()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Acolyte);
            float got = 0f;
            sim.Events.EnemyDamaged += (_, d) => got = d.Amount;
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(7.5f, got, 1e-4f);
        }

        // ---- Power curve and Overcharge (D93) ---------------------------------------------------

        static PowerCurve Curve => new PowerCurve(1.8f, 2f, 0.35f, 1.5f);

        [TestCase(0.0, 1.0f)]
        [TestCase(2.6499, 1.8f)]  // just before the zone: 2.65 itself is IN the zone (x1.5)
        [TestCase(1.325, 1.2f)]   // halfway through the ramp: 1 + 0.8 * 0.25
        public void Curve_RampIsEasedUpToTheZone(double decayed, float expected)
        {
            Assert.AreEqual(expected, Curve.Evaluate(decayed, 3f), 1e-3f);
        }

        [TestCase(2.649, false)]
        [TestCase(2.65, true)]
        [TestCase(2.99, true)]
        [TestCase(3.0, false)]   // expiry is a backfire, never a crit
        public void Curve_ZoneIsTheLast035Seconds(double decayed, bool zone)
        {
            Assert.AreEqual(zone, Curve.IsOvercharged(decayed, 3f));
        }

        [Test]
        public void Curve_ZoneMultipliesThePeak()
        {
            Assert.AreEqual(2.7f, Curve.Evaluate(2.8, 3f), 1e-3f);
        }

        static CapturedPacket Held(ArenaSim sim, double decayed)
        {
            // Seeded at Now, not Now - 5: decay runs from AdvancedAt, so a back-dated seed would
            // be charged 5 s on the first tick and backfire. Seed already back-dates priming.
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f));
            p.CapacityUsed = 1;
            p.Status = PacketStatus.Stored;
            p.DecayedTime = decayed;
            return p;
        }

        [Test]
        public void Overcharge_ReleaseInTheZone_IsGoldAndRaisesTheEventOnce()
        {
            var sim = Sim();
            var p = Held(sim, 2.75);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            var spawned = new List<ProjectileActor>();
            sim.Events.ProjectileSpawned += s => spawned.Add(s);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, overcharges);
            Assert.AreEqual(1, spawned.Count);
            Assert.IsTrue(spawned[0].Shot.Overcharged);
            Assert.AreEqual(sim.Stats.Power.Evaluate(p.DecayedTime, 3f), spawned[0].PowerMultiplier, 1e-3f);
            Assert.AreEqual(1, sim.Score.Overcharges);
        }

        [Test]
        public void Overcharge_ReleaseBeforeTheZone_IsNot()
        {
            var sim = Sim();
            Held(sim, 1.0);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, overcharges);
        }

        [Test]
        public void Overcharge_TooLate_StillBackfires()
        {
            var sim = Sim();
            Held(sim, 2.995);
            int backfires = 0, released = 0;
            sim.Events.PacketBackfired += _ => backfires++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);   // expiry runs before input (D17)
            Assert.AreEqual(1, backfires);
            Assert.AreEqual(0, released);
        }

        [Test]
        public void Overcharge_PocketedInTheZone_StaysOvercharged()
        {
            // Review Focus 3 / R10: only the selected hex decays, so a hex pocketed inside the
            // zone is a banked crit until it is selected again.
            var sim = Sim();
            var p = Held(sim, 2.7);
            TestSims.Pocket(sim);
            Run(sim, 120);
            Assert.IsTrue(p.IsOvercharged(sim.Stats.Power));
            TestSims.Pocket(sim);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, overcharges);
        }

        [Test]
        public void Overcharge_DamageEventCarriesTheFlag()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            var shot = From(ActorCategory.Acolyte);
            shot.Overcharged = true;
            bool flagged = false;
            sim.Events.EnemyDamaged += (_, d) => flagged = d.Overcharged;
            sim.DamageEnemy(e, 1f, DamageCategory.ReturnedProjectile, shot, 1);
            Assert.IsTrue(flagged);
        }

        // ---- Kill chains (D95) -----------------------------------------------------------------

        [Test]
        public void Chain_BonusEscalates_ThenRepeatsTheLast()
        {
            var c = new KillChain(2.5f, new[] { 0f, 1f, 3f, 5f });
            Assert.AreEqual(0f, c.Register(0.0));
            Assert.AreEqual(1f, c.Register(1.0));
            Assert.AreEqual(3f, c.Register(2.0));
            Assert.AreEqual(5f, c.Register(3.0));
            Assert.AreEqual(5f, c.Register(4.0));
            Assert.AreEqual(5, c.Length);
        }

        [Test]
        public void Chain_BreaksWhenTheWindowPasses()
        {
            var c = new KillChain(2.5f, new[] { 0f, 1f, 3f, 5f });
            c.Register(0.0);
            c.Register(2.5);           // exactly on the edge still chains
            Assert.AreEqual(2, c.Length);
            Assert.AreEqual(0f, c.Register(5.1));
            Assert.AreEqual(1, c.Length);
            Assert.AreEqual(2, c.Best);
        }

        [Test]
        public void Chain_AcolyteKillsGiveThreeFourSix()
        {
            // The owner's example: +3, +4, +6 for consecutive Acolyte kills.
            var sim = Sim();
            sim.DebugSetLife(60.0);
            var gains = new List<float>();
            sim.Events.LifeClockChanged += (d, _) => { if (d > 0) gains.Add(d); };
            for (int i = 0; i < 3; i++)
            {
                var e = Spawned(sim, ActorCategory.Acolyte);
                sim.DamageEnemy(e, 1000f, DamageCategory.ReturnedProjectile, From(ActorCategory.SiegeFamiliar), 1);
                Run(sim, 30);
            }
            CollectionAssert.AreEqual(new[] { 3f, 4f, 6f }, gains);
            Assert.AreEqual(3, sim.Score.BestChain);
        }

        /// <summary>A short run at its first upgrade choice (invulnerable, encounter 1 cleared).</summary>
        static ArenaSim AtFirstChoice()
        {
            var sim = P5.Short(1);
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            return sim;
        }

        /// <summary>
        /// An endless run at its first choice (two 30 s waves, as EndlessTests does). The REAL
        /// starting clock, not EndlessTests' 100000 s one: prices are fractions of life, and a
        /// float near 15000 cannot hold the 1e-3 the price assertions need.
        /// </summary>
        static ArenaSim EndlessAtChoice()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = 1, Mode = GameMode.Endless });
            P5.Invulnerable(sim);
            P5.TickWhile(sim, RunState.Ready);
            P5.TickWhile(sim, RunState.Combat, 40000);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            sim.DebugSetLife(sim.Stats.StartingSeconds);
            return sim;
        }

        // ---- Upgrades for life (D96) -----------------------------------------------------------

        static void NextChoice(ArenaSim sim)
        {
            if (sim.IsEndlessRun)
            {
                P5.TickWhile(sim, RunState.Combat, 40000);
                // The third endless choice is the one deferred until the wave-six boss falls
                // (EndlessTests: "BossIntro w6", then "UpgradeChoice w6"), so kill it as they do.
                if (sim.State == RunState.BossIntro)
                {
                    Assert.IsTrue(sim.CompleteBossIntro());
                    P5.Run(sim, 90);
                    Assert.AreEqual(RunState.BossCombat, sim.State);
                    P5.Kill(sim, sim.Boss);
                    sim.Tick(P5.Still, P5.Dt);
                }
            }
            else P5.ClearEncounter(sim);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            // Refill (Task 7's helper): three paid picks plus the waves between them would
            // otherwise drain a test run to death before the case under test is reached.
            sim.DebugSetLife(sim.Stats.StartingSeconds);
        }

        /// <summary>Index of the first offer that is NOT a rank-up (a new card), or -1.</summary>
        static int NewCard(ArenaSim sim)
        {
            for (int i = 0; i < sim.Offers.Count; i++) if (!sim.IsRankUp(sim.Offers[i])) return i;
            return -1;
        }

        [Test]
        public void Continue_IsFree_AndTakesNothing()
        {
            var sim = AtFirstChoice();
            float life = sim.LifeSeconds;
            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.AreEqual(0, sim.HeldUpgrades.Count);
            Assert.AreEqual(life, sim.LifeSeconds, 1e-4f);
        }

        [TestCase(0, 0.15f)]
        [TestCase(1, 0.25f)]
        [TestCase(2, 0.40f)]
        [TestCase(3, 0.50f)]
        public void Adding_CostsMoreTheMoreYouHold(int held, float fraction)
        {
            var sim = EndlessAtChoice();
            for (int i = 0; i < held; i++) { Assert.IsTrue(sim.ChooseUpgrade(NewCard(sim))); NextChoice(sim); }
            Assert.AreEqual(held, sim.HeldUpgrades.Count);
            float life = sim.LifeSeconds;
            Assert.AreEqual(life * fraction, sim.TakeCost, 1e-3f);
            int hits = sim.Score.DamageTaken;
            Assert.IsTrue(sim.ChooseUpgrade(NewCard(sim)));
            Assert.AreEqual(life * (1f - fraction), sim.LifeSeconds, 1e-3f);
            Assert.AreEqual(held + 1, sim.HeldUpgrades.Count);
            Assert.AreEqual(hits, sim.Score.DamageTaken, "a price, not a hit (R13f)");
        }

        [Test]
        public void Upgrades_NeverExpire_BetweenEncounters()
        {
            var sim = AtFirstChoice();
            var pick = sim.Offers[0];
            Assert.IsTrue(sim.ChooseUpgrade(0));
            NextChoice(sim);
            Assert.IsTrue(sim.Has(pick.Id), "R13a: nothing is discarded");
            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.IsTrue(sim.Has(pick.Id));
        }

        [Test]
        public void Swapping_IsFree_AndReplacesTheCardYouChose()
        {
            // R13d: with two held, the player names the one that goes.
            var sim = EndlessAtChoice();
            sim.ChooseUpgrade(NewCard(sim)); NextChoice(sim);
            sim.ChooseUpgrade(NewCard(sim)); NextChoice(sim);
            var keep = sim.HeldUpgrades[0].Id;
            var drop = sim.HeldUpgrades[1].Id;
            int i = NewCard(sim);
            var incoming = sim.Offers[i].Id;
            float life = sim.LifeSeconds;
            Assert.IsTrue(sim.CanSwap);
            Assert.IsTrue(sim.ChooseUpgrade(i, replace: 1));
            Assert.AreEqual(life, sim.LifeSeconds, 1e-4f, "swaps are free");
            Assert.IsTrue(sim.Has(keep));
            Assert.IsFalse(sim.Has(drop));
            Assert.IsTrue(sim.Has(incoming));
            Assert.AreEqual(2, sim.HeldUpgrades.Count);
        }

        [Test]
        public void RankUp_CostsTheSameAsAdding_AndRaisesOnlyThatCard()
        {
            var sim = Sim();
            sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley);
            // R13b: rank-up is never weaker. Held rank 1, offer rank 1 (cycle): new rank = max(1+1, 1) = 2.
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.PiercingReturn, 2), new UpgradeOffer(UpgradeId.Overflow, 1));
            Assert.IsTrue(sim.IsRankUp(sim.Offers[0]));
            float life = sim.LifeSeconds;
            Assert.AreEqual(life * 0.40f, sim.TakeCost, 1e-3f, "two held: 40% either way");
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(2, sim.RankOf(UpgradeId.PiercingReturn));
            Assert.AreEqual(1, sim.RankOf(UpgradeId.EchoVolley));
            Assert.AreEqual(2, sim.HeldUpgrades.Count, "a rank-up adds no card");
            Assert.AreEqual(life * 0.60f, sim.LifeSeconds, 1e-3f);
        }

        [Test]
        public void FourHeld_OffersOnlyRankUps_AtHalfYourLife()
        {
            // R13c (owner): locked at four, but rank-ups stay on the table for 50%.
            var sim = EndlessAtChoice();
            sim.ContinueFromUpgrade();
            sim.DebugHold(UpgradeId.Overflow, UpgradeId.Fusion, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit);
            Assert.IsTrue(sim.UpgradesLocked);
            NextChoice(sim);
            Assert.IsTrue(sim.Offers.Count > 0);
            Assert.IsTrue(sim.Offers.All(sim.IsRankUp), "no new cards once locked");
            Assert.IsFalse(sim.CanSwap);
            Assert.AreEqual(sim.LifeSeconds * 0.50f, sim.TakeCost, 1e-3f);
            Assert.IsFalse(sim.ChooseUpgrade(0, replace: 0), "no swaps once locked");
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(4, sim.HeldUpgrades.Count);
        }

        [Test]
        public void MaxRankCards_AreNeverOffered()
        {
            var sim = AtFirstChoice();
            sim.ContinueFromUpgrade();
            sim.ForceUpgrade(UpgradeId.FinalSecond, 3);
            for (int k = 0; k < 2; k++)
            {
                NextChoice(sim);
                Assert.IsFalse(sim.Offers.Any(o => o.Id == UpgradeId.FinalSecond), "already at max rank");
                Assert.AreEqual(sim.Config.upgrades.offerCount, sim.Offers.Count);
                sim.ContinueFromUpgrade();
            }
        }

        [Test]
        public void RankUp_IsAlwaysExactlyOneStep()
        {
            // Owner, 3 Oct 2026: a rank-x card is only ever offered as rank x+1, never x+2, even
            // in an endless cycle whose fresh cards are rank 3. x+2 appears only after x+1 is taken.
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.EchoVolley, 1);
            Assert.AreEqual(2, sim.OfferRankFor(UpgradeId.EchoVolley, 3), "held rank 1: offered as rank 2");
            Assert.AreEqual(3, sim.OfferRankFor(UpgradeId.Overflow, 3), "a fresh card keeps the cycle's rank");
            Assert.AreEqual(1, sim.OfferRankFor(UpgradeId.Overflow, 1));

            // Even an offer built with a bigger rank only raises the held card one step.
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.EchoVolley, 3));
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(2, sim.RankOf(UpgradeId.EchoVolley));
            Assert.AreEqual(3, sim.OfferRankFor(UpgradeId.EchoVolley, 3), "rank 3 appears once rank 2 is held");
        }

        [Test]
        public void HeldUpgrades_EachUseTheirOwnRank()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.PiercingReturn, 2);
            sim.DebugHold(UpgradeId.EchoVolley);
            Assert.AreEqual(2, sim.RankOf(UpgradeId.PiercingReturn));
            Assert.AreEqual(1, sim.RankOf(UpgradeId.EchoVolley));
            Assert.AreEqual(0, sim.RankOf(UpgradeId.Overflow));
        }

        [TestCase(10.0, 100)]
        [TestCase(0.1, 1)]
        [TestCase(0.04, 0)]
        [TestCase(180.0, 1800)]
        public void LifeDisplay_ShowsTenPointsPerSecond(double seconds, int points)
            => Assert.AreEqual(points, LifeDisplay.Points(seconds));

        // ---- Lifesteal (D99) -------------------------------------------------------------------

        static ArenaSim WithLifesteal(float perDamage)
        {
            var stats = PlayerStats.FromConfig(TestSims.Config);
            stats.LifePerDamage = perDamage;
            var setup = RunSetup.ForSandbox(1);
            setup.Stats = stats;   // ArenaSim reads Setup.Stats ?? PlayerStats.FromConfig
            var sim = new ArenaSim(TestSims.Config, setup);
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        [Test]
        public void Lifesteal_ReturnsAShareOfTheDamageThatLands()
        {
            var sim = WithLifesteal(0.1f);
            sim.DebugSetLife(60.0);
            float stolen = 0f;
            sim.Events.LifeStolen += (s, _) => stolen += s;
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);   // 100 health in the helper
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            // 10 x 1.33 (another school) = 13.3 damage, x 0.1 = 1.33 s (13 on screen).
            Assert.AreEqual(60f + 1.33f, sim.LifeSeconds, 1e-3f);
            Assert.AreEqual(1.33f, sim.Score.LifeStolen, 1e-3f);
            Assert.AreEqual(1.33f, stolen, 1e-3f, "R17f: the pop is driven by this event");
        }

        [Test]
        public void Lifesteal_IgnoresOverkill()
        {
            // R17c: a 13.3-damage hit on 2 health left steals for 2.
            var sim = WithLifesteal(0.1f);
            sim.DebugSetLife(60.0);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            e.Health = 2f;
            float stolen = 0f;
            sim.Events.LifeStolen += (x, _) => stolen += x;
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            // The hit also KILLS the enemy, and the kill pays its own reward into the same clock,
            // so the clock alone cannot isolate the steal; the event and the score can.
            Assert.AreEqual(0.2f, stolen, 1e-3f);
            Assert.AreEqual(0.2f, sim.Score.LifeStolen, 1e-3f);
            Assert.AreEqual(60.2f + sim.Score.SecondsGained, sim.LifeSeconds, 1e-3f, "the clock is the steal plus the kill reward, nothing else");
        }

        [Test]
        public void Lifesteal_NeverExceedsTheCap()
        {
            var sim = WithLifesteal(10f);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(sim.Stats.StartingSeconds, sim.LifeSeconds, 1e-4f);
        }

        [Test]
        public void Lifesteal_OffByDefault()
        {
            var sim = Sim();
            sim.DebugSetLife(60.0);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(60f, sim.LifeSeconds, 1e-4f);
        }
    }
}
