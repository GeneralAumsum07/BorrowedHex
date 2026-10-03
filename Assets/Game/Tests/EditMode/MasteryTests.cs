using System.Linq;
using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Phase 9: mastery XP and levels, the twelve-node tree, and loadout resolution (section 7).</summary>
    public class MasteryTests
    {
        const float Dt = 1f / 60f;

        static PlayerProfile Profile(int level, int points, params string[] owned)
        {
            var p = PlayerProfile.CreateDefault();
            p.mastery.level = level;
            p.mastery.points = points;
            p.ownedNodes.AddRange(owned);
            return p;
        }

        // ---- XP and levels -------------------------------------------------------------------

        [Test]
        public void NinetyNineXp_StaysLevelOne_HundredReachesLevelTwoWithOnePoint()
        {
            var m = new MasteryState();
            Assert.AreEqual(0, Mastery.Grant(m, 99));
            Assert.AreEqual(1, m.level);
            Assert.AreEqual(99, m.xp);
            Assert.AreEqual(0, m.points);
            Assert.AreEqual(1, Mastery.Grant(m, 1));
            Assert.AreEqual(2, m.level);
            Assert.AreEqual(0, m.xp);
            Assert.AreEqual(1, m.points);
        }

        [Test]
        public void OneRun_CanGainSeveralLevels_AndExcessCarries()
        {
            var m = new MasteryState();
            // 100 (1→2) + 150 (2→3) + 200 (3→4) = 450, plus 30 carried.
            Assert.AreEqual(3, Mastery.Grant(m, 480));
            Assert.AreEqual(4, m.level);
            Assert.AreEqual(30, m.xp);
            Assert.AreEqual(3, m.points);
            Assert.AreEqual(480, m.totalXp);
        }

        [Test]
        public void MaxLevel_IsTheCap_OnePointPerLevelGained_StatisticsKeepCounting()
        {
            var m = new MasteryState();
            int cost = 0;
            for (int l = 1; l < Mastery.MaxLevel; l++) cost += Mastery.CostToAdvance(l);
            Assert.AreEqual(Mastery.MaxLevel - 1, Mastery.Grant(m, cost + 5000));
            Assert.AreEqual(Mastery.MaxLevel, m.level);
            Assert.AreEqual(Mastery.MaxLevel - 1, m.points);
            Assert.AreEqual(0, m.xp, "the bar reads full at the cap");
            Assert.AreEqual(0, Mastery.Grant(m, 1000));
            Assert.AreEqual(Mastery.MaxLevel - 1, m.points, "no point beyond the cap");
            Assert.AreEqual(cost + 6000, m.totalXp);
        }

        [Test]
        public void RunXp_FollowsTheFormula_PerfectTermCappedAtTwenty()
        {
            var x = Mastery.RunXp(normalKills: 10, overstayedKills: 2, bossKills: 1, encounters: 3, perfectHits: 50);
            Assert.AreEqual(2 * 10 + 5 * 2 + 35 + 5 * 3 + 20, x.Total);
            Assert.AreEqual(20, x.PerfectHits);
        }

        [Test]
        public void ALostRun_StillGrantsXp_ForItsKillsAndEncounters()
        {
            var sim = P5.Short(3);
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            int kills = sim.Score.Kills;
            Assert.Greater(kills, 0);
            Assert.IsTrue(sim.ContinueFromUpgrade());
            sim.Tick(P5.Still, Dt);
            sim.Player.InvulnerableUntil = 0;
            sim.DamagePlayer(100000, 0);
            sim.Tick(P5.Still, Dt);
            Assert.IsNotNull(sim.Summary);
            Assert.AreEqual(RunEndReason.Death, sim.Summary.Reason);

            var storage = new MemoryProfileStorage();
            var service = ProfileService.Load(storage);
            var r = service.FinalizeRun(sim.Summary, sim.Setup);
            int expected = 2 * kills + 5 * 1;   // no overstays or perfect shots in this scripted clear
            Assert.AreEqual(expected, r.Xp.Total);
            Assert.AreEqual(expected, service.Profile.mastery.xp);
            // And only once.
            service.FinalizeRun(sim.Summary, sim.Setup);
            Assert.AreEqual(expected, service.Profile.mastery.totalXp);
        }

        [Test]
        public void OverstayedKills_AreTheirOwnXpTerm_NotAlsoNormalKills()
        {
            var sim = P5.Short(5);
            P5.Invulnerable(sim);
            sim.Tick(P5.Still, Dt);
            // Wait for something to arrive, overstay it by force, then kill everything.
            P4.Run(sim, 200);
            EnemyActor target = null;
            foreach (var e in sim.Enemies) if (e.Alive && !e.IsBoss) { target = e; break; }
            Assert.IsNotNull(target);
            target.Overstayed = true;
            P5.Kill(sim, target);
            Assert.AreEqual(1, sim.Score.OverstayedKills);
            Assert.AreEqual(1, sim.Score.Kills);
        }

        [Test]
        public void PerfectHits_CountDistinctShots_NotPierceVictims()
        {
            var sim = P4.Sim();
            var a = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(3f, 0f));
            var b = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            var shot = new AttackSnapshot { Kind = AttackKind.Bolt, SourceActorId = 900, ShotId = sim.Ids.Next(), Perfect = true };
            sim.DamageEnemy(a, 1f, DamageCategory.ReturnedProjectile, shot, 7);
            sim.DamageEnemy(b, 1f, DamageCategory.ReturnedProjectile, shot, 7);
            Assert.AreEqual(1, sim.Score.PerfectHits);
            var echo = shot;
            echo.ShotId = sim.Ids.Next();
            sim.DamageEnemy(a, 1f, DamageCategory.Echo, echo, 7);
            Assert.AreEqual(1, sim.Score.PerfectHits, "an echo was never caught, so it is not a perfect shot");
        }

        // ---- The tree -----------------------------------------------------------------------

        [Test]
        public void TierOne_NeedsMasteryTwo_AndAPoint()
        {
            var p = Profile(1, 0);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out var why));
            StringAssert.Contains("mastery 2", why);
            p = Profile(2, 0);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out why));
            Assert.AreEqual("no points", why);
            p = Profile(2, 1);
            Assert.IsTrue(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out _));
            Assert.AreEqual(0, p.mastery.points);
            CollectionAssert.Contains(p.ownedNodes, SkillTree.PrecisionAngle);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out why), "never bought twice");
        }

        [Test]
        public void HigherTiers_NeedTheirLevel_AndTheBranchNodeBelow()
        {
            var p = Profile(4, 2);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.PrecisionCapacity, out var why));
            StringAssert.Contains("needs", why);
            Assert.IsTrue(SkillTree.TryBuy(p, SkillTree.PrecisionAngle, out _));
            Assert.IsTrue(SkillTree.TryBuy(p, SkillTree.PrecisionCapacity, out _), "owned prerequisite suffices");
            p = Profile(6, 1, SkillTree.MobilitySpeed, SkillTree.MobilityDashRecovery);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.MobilityDashDistance, out why));
            StringAssert.Contains("mastery 7", why);
            Assert.IsFalse(SkillTree.TryBuy(p, "made_up_node", out why));
            Assert.AreEqual("unknown node", why);
        }

        [Test]
        public void EveryNode_CanBeOwned_AndOwnedNodesAreActive()
        {
            // D101 (owner): no equip step and no cap. Level 13 = 12 points = all 12 nodes.
            Assert.AreEqual(13, Mastery.MaxLevel);
            Assert.AreEqual(12, SkillTree.Nodes.Count);
            var p = Profile(Mastery.MaxLevel, Mastery.MaxLevel - 1);
            // Buy in tier order so every prerequisite is already owned.
            foreach (var n in SkillTree.Nodes.OrderBy(n => n.Tier))
                Assert.IsTrue(SkillTree.TryBuy(p, n.Id, out var why), why);
            Assert.AreEqual(0, p.mastery.points);
            Assert.IsTrue(ProfileService.Validate(p, out var bad), bad);
            var s = Loadout.Resolve(TestSims.Config, p.ownedNodes);
            Assert.Greater(s.LifePerDamage, 0f, "owned = active");
        }

        [Test]
        public void BloodPrice_NodesStack_AndGateLikeTheOtherBranches()
        {
            var t = TestSims.Config.progression;
            var s = Loadout.Resolve(TestSims.Config, new[] { SkillTree.BloodLeech, SkillTree.BloodSiphon, SkillTree.BloodDebt });
            Assert.AreEqual(t.bloodLeech + t.bloodSiphon + t.bloodDebt, s.LifePerDamage, 1e-6f);
            var p = Profile(6, 1, SkillTree.BloodLeech, SkillTree.BloodSiphon);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.BloodDebt, out var why));
            StringAssert.Contains("mastery 7", why);
        }

        [Test]
        public void Respec_RefundsEveryOwnedNode()
        {
            var p = Profile(5, 1, SkillTree.PrecisionAngle, SkillTree.PrecisionCapacity, SkillTree.MobilitySpeed);
            Assert.AreEqual(3, SkillTree.Respec(p));
            Assert.AreEqual(4, p.mastery.points, "one held + three refunded = levels gained");
            Assert.IsEmpty(p.ownedNodes);
            Assert.IsTrue(ProfileService.Validate(p, out var why), why);
        }

        [TestCase("{\"version\":1,\"mastery\":{\"level\":5},\"ownedNodes\":[\"made_up\"]}")]
        [TestCase("{\"version\":1,\"mastery\":{\"level\":5},\"ownedNodes\":[\"precision_capacity\"]}")]
        [TestCase("{\"version\":1,\"mastery\":{\"level\":3},\"ownedNodes\":[\"precision_angle\",\"precision_capacity\"]}")]
        public void ProfilesWithImpossibleTrees_AreRejected(string json)
        {
            Assert.IsFalse(ProfileService.TryParse(json, out _, out var why));
            Assert.IsNotNull(why);
        }

        [Test]
        public void ALegalTree_SurvivesTheRoundTrip()
        {
            var p = Profile(7, 0, SkillTree.ResilienceGrace, SkillTree.ResilienceTime, SkillTree.ResilienceDashGrace);
            p.mastery.points = 3;
            Assert.IsTrue(ProfileService.TryParse(ProfileService.ToJson(p), out var back, out var why), why);
            CollectionAssert.AreEqual(p.ownedNodes, back.ownedNodes);
        }

        // ---- Loadout -----------------------------------------------------------------------

        [Test]
        public void EachPassive_ChangesItsOwnStat_AndNothingElse()
        {
            var cfg = TestSims.Config;
            var t = cfg.progression;
            var b = PlayerStats.FromConfig(cfg);
            PlayerStats With(string id) => Loadout.Resolve(cfg, new[] { id });
            Assert.AreEqual(b.CaptureConeAngle + t.precisionAngle, With(SkillTree.PrecisionAngle).CaptureConeAngle, 1e-4f);
            Assert.AreEqual(b.PacketCapacity + t.precisionCapacity, With(SkillTree.PrecisionCapacity).PacketCapacity);
            Assert.AreEqual(t.quickDrawBonus, With(SkillTree.QuickDraw).QuickDrawBonus, 1e-6f);
            Assert.AreEqual(b.MoveSpeed * (1f + t.mobilitySpeed), With(SkillTree.MobilitySpeed).MoveSpeed, 1e-4f);
            Assert.AreEqual(b.DashCooldown - t.mobilityDashRecovery, With(SkillTree.MobilityDashRecovery).DashCooldown, 1e-4f);
            var dd = With(SkillTree.MobilityDashDistance);
            Assert.AreEqual(b.DashDistance + t.mobilityDashDistance, dd.DashDistance, 1e-4f);
            Assert.AreEqual(b.DashDuration, dd.DashDuration, 1e-6f, "same duration");
            Assert.AreEqual(b.HitInvulnerability + t.resilienceGrace, With(SkillTree.ResilienceGrace).HitInvulnerability, 1e-4f);
            Assert.AreEqual(b.StartingSeconds + t.resilienceTime, With(SkillTree.ResilienceTime).StartingSeconds, 1e-4f);
            Assert.AreEqual(Mathf.Min(b.DashDuration, b.DashInvulnerability + t.resilienceDashGrace),
                With(SkillTree.ResilienceDashGrace).DashInvulnerability, 1e-4f);
            // No passives: identical to the config.
            var none = Loadout.Resolve(cfg, new List<string>());
            Assert.AreEqual(b.CaptureConeAngle, none.CaptureConeAngle);
            Assert.AreEqual(0f, none.QuickDrawBonus);
        }

        [Test]
        public void BorrowedHours_RaisesTheStartAndTheCap()
        {
            var cfg = TestSims.Config;
            var setup = new RunSetup { Seed = 1, Mode = GameMode.Short };
            setup.PassiveIds.Add(SkillTree.ResilienceTime);
            setup.Stats = Loadout.Resolve(cfg, setup.PassiveIds);
            var sim = new ArenaSim(cfg, setup);
            sim.Tick(P5.Still, Dt);
            float start = cfg.shortMode.runLength + cfg.progression.resilienceTime;
            Assert.AreEqual(start, (float)sim.LifeSeconds, 0.05f);
            var e = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(5f, 0f));
            P5.Kill(sim, e);
            Assert.LessOrEqual(sim.LifeSeconds, start + 1e-3, "kill time is capped at the raised cap");
        }

        [Test]
        public void QuickDraw_FiringWithinPointThreeSecondsOfASwap_DealsThirtyPercentMore()
        {
            float PowerAfter(bool node, int ticksAfterSwap)
            {
                var setup = RunSetup.ForSandbox(1);
                setup.Stats = Loadout.Resolve(TestSims.Config, node ? new[] { SkillTree.QuickDraw } : new string[0]);
                var sim = new ArenaSim(TestSims.Config, setup);
                var aim = PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(8f, 0f));
                // A packet in slot 1, selected by the swap.
                var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
                p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 42, sim.Ids.Next(), 0));
                p.Status = PacketStatus.Stored;
                sim.Tick(aim, Dt);
                sim.Tick(aim.WithCycle(), Dt);   // packet now in the unselected slot
                sim.Tick(aim.WithCycle(), Dt);   // swap back: this is the swap Quick Draw measures
                for (int i = 0; i < ticksAfterSwap; i++) sim.Tick(aim, Dt);
                float power = -1f;
                sim.Events.ProjectileSpawned += s => { if (s.Faction == AttackFaction.Returned) power = s.PowerMultiplier; };
                sim.Tick(aim.WithRelease(), Dt);
                Assert.Greater(power, 0f, "the packet fired");
                return power;
            }
            float baseline = PowerAfter(false, 3);
            Assert.AreEqual(baseline * 1.3f, PowerAfter(true, 3), 1e-4f, "0.067 s after the swap");
            // 0.3 s = 18 ticks; the release tick is one more, so 19 waiting ticks is past the window.
            Assert.AreEqual(PowerAfter(false, 19), PowerAfter(true, 19), 1e-4f, "too late for the bonus");
        }
    }
}
