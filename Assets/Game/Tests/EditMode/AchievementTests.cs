using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Phase 10: section 7 achievements and section 6 personal records.</summary>
    public class AchievementTests
    {
        const float Dt = 1f / 60f;

        static void Damage(ArenaSim sim, EnemyActor e, float amount, int source, int root = 7,
            DamageCategory cat = DamageCategory.ReturnedProjectile, AttackKind kind = AttackKind.Bolt, bool perfect = false, int shotId = 0)
        {
            var shot = new AttackSnapshot { Kind = kind, SourceActorId = source, ShotId = shotId != 0 ? shotId : sim.Ids.Next(), Perfect = perfect };
            sim.DamageEnemy(e, amount, cat, shot, root);
        }

        static EnemyActor Enemy(ArenaSim sim, float x) => P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(x, 2f));

        /// <summary>End a short run by death and return its summary.</summary>
        static RunSummary Die(ArenaSim sim)
        {
            sim.Player.InvulnerableUntil = 0;
            sim.Player.DashInvulnerableUntil = 0;
            sim.DamagePlayer(100000, 0);
            sim.Tick(P5.Still, Dt);
            Assert.IsNotNull(sim.Summary);
            return sim.Summary;
        }

        static ArenaSim LiveShort(int seed = 1)
        {
            var sim = P5.Short(seed);
            sim.Tick(P5.Still, Dt);
            return sim;
        }

        // ---- Combat facts ------------------------------------------------------------------

        [Test]
        public void ReturnPolicy_NeedsTheOriginalCaster_AndNeverARiposte()
        {
            var sim = P4.Sim();
            var a = Enemy(sim, 3f);
            var b = Enemy(sim, 5f);
            Damage(sim, a, 100f, source: b.ActorId);   // killed by someone else's bolt
            Assert.IsTrue(sim.Score.FirstBorrow);
            Assert.IsFalse(sim.Score.ReturnPolicy);
            Damage(sim, b, 100f, source: b.ActorId, kind: AttackKind.Riposte);
            Assert.IsFalse(sim.Score.ReturnPolicy, "a riposte was never borrowed");

            sim = P4.Sim();
            var c = Enemy(sim, 3f);
            Damage(sim, c, 100f, source: c.ActorId, cat: DamageCategory.Echo);
            Assert.IsTrue(sim.Score.ReturnPolicy, "its own shot's echo still came from it");
        }

        [Test]
        public void FirstBorrow_IgnoresOrbitAndPartingGift()
        {
            var sim = P4.Sim();
            var a = Enemy(sim, 3f);
            Damage(sim, a, 0.5f, 0, root: 0, cat: DamageCategory.Orbit);
            Damage(sim, a, 0.5f, 0, root: 0, cat: DamageCategory.PartingGift);
            Assert.IsFalse(sim.Score.FirstBorrow);
        }

        [Test]
        public void CrowdControl_FiveDistinctKillsFromOneRoot_PierceAndEchoNeverCountAVictimTwice()
        {
            var sim = P4.Sim();
            var es = new EnemyActor[5];
            for (int i = 0; i < 5; i++) es[i] = Enemy(sim, -6f + 3f * i);
            for (int i = 0; i < 4; i++) Damage(sim, es[i], 100f, 900, root: 7);
            // The echo and a pierced shot reach the same (already dead) victims again.
            for (int i = 0; i < 4; i++) Damage(sim, es[i], 100f, 900, root: 7, cat: DamageCategory.Echo);
            Assert.AreEqual(4, sim.Score.BestVolleyKills);
            Damage(sim, es[4], 100f, 900, root: 8);   // a different release
            Assert.AreEqual(4, sim.Score.BestVolleyKills, "two releases do not add up");

            sim = P4.Sim();
            for (int i = 0; i < 5; i++) es[i] = Enemy(sim, -6f + 3f * i);
            for (int i = 0; i < 3; i++) Damage(sim, es[i], 100f, 900, root: 7);
            for (int i = 3; i < 5; i++) Damage(sim, es[i], 100f, 900, root: 7, cat: DamageCategory.Echo);
            Assert.AreEqual(5, sim.Score.BestVolleyKills, "a release together with its echo");
        }

        [Test]
        public void Untouchable_ClearWithoutAHit_Counts_AHitOrABackfireSpoilsIt_PauseDoesNot()
        {
            int Untouched(System.Action<ArenaSim> during)
            {
                var sim = P5.Short(2);
                sim.Tick(P5.Still, Dt);
                during(sim);
                P5.Invulnerable(sim);   // the rest of the clear is safe
                P5.ClearEncounter(sim);
                Assert.AreEqual(RunState.UpgradeChoice, sim.State);
                // Pausing and resuming over the choice is not a second clear.
                sim.SetPause(PauseReason.Manual, true);
                sim.SetPause(PauseReason.Manual, false);
                return sim.Score.UntouchableEncounters;
            }
            Assert.AreEqual(1, Untouched(_ => { }));
            Assert.AreEqual(0, Untouched(sim => sim.DamagePlayer(10, 0)));
            Assert.AreEqual(0, Untouched(sim =>
            {
                // A hit, then a pause and resume mid-encounter: the hit still counts.
                sim.DamagePlayer(10, 0);
                sim.SetPause(PauseReason.Manual, true);
                sim.SetPause(PauseReason.Manual, false);
            }));
            Assert.AreEqual(0, Untouched(sim =>
            {
                // A packet left to decay backfires: costs life, so the encounter is not untouchable.
                var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
                p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(Data.AttackIds.Bolt), 42, sim.Ids.Next(), 0));
                p.Status = PacketStatus.Stored;
                P5.Invulnerable(sim);   // backfires bypass immunity; ordinary hits do not count here
                P4.Run(sim, 200);
                Assert.Greater(sim.Score.Backfires, 0);
            }));
        }

        // ---- Evaluation and award ---------------------------------------------------------

        [Test]
        public void ARunsFacts_MapToTheirAchievements()
        {
            var sim = LiveShort();
            var a = Enemy(sim, 3f);
            var b = Enemy(sim, 6f);
            Damage(sim, a, 100f, a.ActorId, kind: AttackKind.HeavyShot);
            Damage(sim, b, 100f, 900, kind: AttackKind.Rocket, cat: DamageCategory.Explosion);
            for (int i = 0; i < 5; i++)
            {
                var e = Enemy(sim, -6f + i);
                Damage(sim, e, 100f, 900, root: 20 + i, perfect: true);
            }
            var s = Die(sim);
            var met = Achievements.MetBy(s, new MasteryState { level = 5 });
            CollectionAssert.IsSubsetOf(new[]
            {
                Achievements.ReturnPolicy, Achievements.FirstBorrow, Achievements.MixedBag,
                Achievements.PerfectTiming, Achievements.PersistentStudent,
            }, met);
            CollectionAssert.DoesNotContain(met, Achievements.FinalNotice, "a death is not a win");
            CollectionAssert.DoesNotContain(met, Achievements.FullyTrained);
            CollectionAssert.DoesNotContain(met, Achievements.SecondEncore, "endless only");
            CollectionAssert.DoesNotContain(met, Achievements.CrowdControl);
        }

        [Test]
        public void WinningShortMode_IsFinalNotice()
        {
            var sim = P5.Short(4);
            P5.ToBossCombat(sim);
            P4.Run(sim, 120);
            P5.Kill(sim, sim.Boss);
            sim.Tick(P5.Still, Dt);
            Assert.AreEqual(RunEndReason.Victory, sim.Summary.Reason);
            CollectionAssert.Contains(Achievements.MetBy(sim.Summary, new MasteryState()), Achievements.FinalNotice);
        }

        [Test]
        public void Awarding_IsOncePerAchievement_AndALostRunKeepsWhatItEarned()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            var sim = LiveShort(1);
            var a = Enemy(sim, 3f);
            Damage(sim, a, 1f, 900);
            var r1 = service.FinalizeRun(Die(sim), sim.Setup);
            CollectionAssert.Contains(r1.NewAchievements, Achievements.FirstBorrow, "earned in a run that was lost");
            int count = service.Profile.achievements.Count;

            sim = LiveShort(2);
            a = Enemy(sim, 3f);
            Damage(sim, a, 1f, 900);
            var r2 = service.FinalizeRun(Die(sim), sim.Setup);
            Assert.IsEmpty(r2.NewAchievements, "repeat completion gives nothing more");
            Assert.AreEqual(count, service.Profile.achievements.Count);
        }

        [Test]
        public void MasteryAchievements_CountTheLevelReachedThisRun()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            var m = service.Profile.mastery;
            m.level = 4;
            m.points = 3;
            m.xp = Mastery.CostToAdvance(4) - 1;
            var sim = LiveShort();
            P5.Kill(sim, Enemy(sim, 3f));   // 2 XP crosses the line
            var r = service.FinalizeRun(Die(sim), sim.Setup);
            Assert.AreEqual(5, r.LevelAfter);
            CollectionAssert.Contains(r.NewAchievements, Achievements.PersistentStudent);
        }

        // ---- Records -----------------------------------------------------------------------

        [Test]
        public void Records_FirstThenBetterReplaces_WorseAndTiesKeepTheBest_WithMetadata()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            RunSummary Run(int kills, int seed)
            {
                var sim = LiveShort(seed);
                sim.Setup.PassiveIds.Add("precision_angle");
                for (int i = 0; i < kills; i++) P5.Kill(sim, Enemy(sim, -6f + 2f * i));
                return Die(sim);
            }
            var s1 = Run(1, 11);
            var o1 = service.FinalizeRun(s1, P5.Short(11).Setup).Records[0];
            Assert.IsTrue(o1.IsNewBest);
            Assert.IsNull(o1.Previous);

            var s2 = Run(3, 12);
            var setup2 = new RunSetup { Seed = 12, Mode = GameMode.Short, MasteryLevel = 3, BuildVersion = "test-build" };
            setup2.PassiveIds.Add("mobility_speed");
            var o2 = service.FinalizeRun(s2, setup2).Records[0];
            Assert.IsTrue(o2.IsNewBest);
            Assert.AreEqual(s1.Score, o2.Previous.score);

            var o3 = service.FinalizeRun(Run(0, 13), null).Records[0];
            Assert.IsFalse(o3.IsNewBest);
            Assert.AreEqual(s2.Score, o3.Previous.score);

            Assert.AreEqual(1, service.Profile.records.Count, "one record per mode, style and kind");
            var rec = service.Profile.records[0];
            Assert.AreEqual(s2.Score, rec.score);
            Assert.AreEqual("Short", rec.mode);
            Assert.AreEqual("snatcher", rec.styleId);
            Assert.AreEqual(12, rec.seed);
            Assert.AreEqual("test-build", rec.buildVersion);
            Assert.AreEqual(3, rec.masteryLevel);
            CollectionAssert.AreEqual(new[] { "mobility_speed" }, rec.passives);
            Assert.AreEqual(Records.BestScore, rec.kind);
        }

        [TestCase("{\"version\":1,\"achievements\":[{\"id\":\"made_up\"}]}")]
        [TestCase("{\"version\":1,\"achievements\":[{\"id\":\"first_borrow\"},{\"id\":\"first_borrow\"}]}")]
        [TestCase("{\"version\":1,\"records\":[{\"mode\":\"Short\",\"styleId\":\"snatcher\",\"kind\":\"fastest\"}]}")]
        [TestCase("{\"version\":1,\"records\":[{\"mode\":\"Short\",\"styleId\":\"snatcher\"},{\"mode\":\"Short\",\"styleId\":\"snatcher\"}]}")]
        public void ProfilesWithImpossibleAchievementsOrRecords_AreRejected(string json)
        {
            Assert.IsFalse(ProfileService.TryParse(json, out _, out var why));
            Assert.IsNotNull(why);
        }

        [Test]
        public void AnOlderRecordWithoutAKind_IsABestScore()
        {
            Assert.IsTrue(ProfileService.TryParse("{\"version\":1,\"records\":[{\"mode\":\"Short\",\"styleId\":\"snatcher\",\"score\":5}]}",
                out var p, out var why), why);
            Assert.AreEqual(Records.BestScore, p.records[0].kind);
            Assert.IsNotNull(Records.Find(p, "Short", "snatcher", Records.BestScore));
        }
    }
}
