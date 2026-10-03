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
    // Section 6 score and statistics. Sandbox sims: score works the same there, and an empty
    // arena lets each test place exactly the enemies it needs.
    public class ScoreTests
    {
        static ArenaSim Sim()
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        /// <summary>Active (damageable) but parked: it never attacks, so no stray hit resets a combo.</summary>
        static EnemyActor Enemy(ArenaSim sim, ActorCategory c, float x, bool elite = false)
        {
            var e = sim.SpawnEnemy(c, new Vector2(x, 5f), elite);
            e.ActiveAt = 0;
            e.Phase = EnemyPhase.Recover;
            e.PhaseEndsAt = double.MaxValue;
            return e;
        }

        /// <summary>
        /// Score from kill values alone. These kills follow each other inside one chain window, so
        /// since D102 each also scores a chain bonus; that has its own test (ReworkTests).
        /// </summary>
        static int KillScore(ArenaSim sim) => sim.Score.Score - sim.Score.ScoreFromChains;

        [Test]
        public void KillValues_MatchSection6()
        {
            var sim = Sim();
            P5.Kill(sim, Enemy(sim, ActorCategory.Pursuer, -6f));
            Assert.AreEqual(10, KillScore(sim));
            P5.Kill(sim, Enemy(sim, ActorCategory.Acolyte, -3f));
            Assert.AreEqual(20, KillScore(sim));
            P5.Kill(sim, Enemy(sim, ActorCategory.ScatterCaster, 0f));
            Assert.AreEqual(40, KillScore(sim));
            P5.Kill(sim, Enemy(sim, ActorCategory.SiegeFamiliar, 3f));
            Assert.AreEqual(65, KillScore(sim));
            P5.Kill(sim, Enemy(sim, ActorCategory.Pursuer, 6f, elite: true));
            Assert.AreEqual(80, KillScore(sim), "elite = 1.5x");
            var boss = sim.SpawnBoss();
            boss.ActiveAt = 0;
            P5.Kill(sim, boss);
            Assert.AreEqual(330, KillScore(sim));
            Assert.AreEqual(1, sim.Score.BossesDefeated);
        }

        [Test]
        public void FirstReturnedHitOfARelease_RaisesTheCombo_OnlyOnce()
        {
            var sim = Sim();
            var tank = Enemy(sim, ActorCategory.SiegeFamiliar, 0f);
            P5.Hit(sim, tank, 1f, root: 5);
            Assert.AreEqual(1.25f, sim.Score.Multiplier, 1e-5f);
            P5.Hit(sim, tank, 1f, root: 5); // pierce / echo / explosion of the same release
            Assert.AreEqual(1.25f, sim.Score.Multiplier, 1e-5f);
            P5.Hit(sim, tank, 1f, root: 6);
            Assert.AreEqual(1.5f, sim.Score.Multiplier, 1e-5f);
        }

        [Test]
        public void Combo_CapsAtThree()
        {
            var sim = Sim();
            var e = sim.SpawnBoss();
            e.ActiveAt = 0;
            for (int r = 1; r <= 12; r++) P5.Hit(sim, e, 0.1f, root: r);
            Assert.AreEqual(3f, sim.Score.Multiplier, 1e-5f);
        }

        [Test]
        public void Kills_AreScoredAtTheCurrentMultiplier_IncludingTheKillingHitsOwnStep()
        {
            var sim = Sim();
            var tank = Enemy(sim, ActorCategory.SiegeFamiliar, 0f);
            P5.Hit(sim, tank, 1f, root: 1);                 // 1.25
            P5.Kill(sim, Enemy(sim, ActorCategory.Pursuer, 4f), root: 2); // first hit of root 2 → 1.5, then the kill
            Assert.AreEqual(15, sim.Score.Score);
        }

        [Test]
        public void Combo_ExpiresAfterFiveGameplaySeconds_AndIsFrozenWhilePaused()
        {
            var sim = Sim();
            var tank = Enemy(sim, ActorCategory.SiegeFamiliar, 0f);
            P5.Hit(sim, tank, 1f, root: 1);
            P5.Run(sim, 299);
            Assert.AreEqual(1.25f, sim.Score.Multiplier, 1e-5f);
            sim.SetPause(PauseReason.Menu, true);
            P5.Run(sim, 1000);
            sim.SetPause(PauseReason.Menu, false);
            Assert.AreEqual(1.25f, sim.Score.Multiplier, 1e-5f, "menu time does not count");
            P5.Run(sim, 2);
            Assert.AreEqual(1f, sim.Score.Multiplier, 1e-5f);
        }

        [Test]
        public void TakingDamage_ResetsTheCombo()
        {
            var sim = Sim();
            var tank = Enemy(sim, ActorCategory.SiegeFamiliar, 0f);
            P5.Hit(sim, tank, 1f, root: 1);
            P5.Hit(sim, tank, 1f, root: 2);
            sim.DamagePlayer(1, tank.ActorId);
            Assert.AreEqual(1f, sim.Score.Multiplier);
            Assert.AreEqual(1, sim.Score.DamageTaken);
        }

        [Test]
        public void Despawning_GivesNoScoreAndNoKill()
        {
            var sim = Sim();
            var e = Enemy(sim, ActorCategory.ScatterCaster, 0f);
            sim.DespawnEnemy(e);
            sim.ClearArena();
            Assert.AreEqual(0, sim.Score.Score);
            Assert.AreEqual(0, sim.Score.Kills);
        }

        [Test]
        public void Catching_GivesNoScore()
        {
            var sim = Sim();
            sim.Player.AimDirection = Vector2.right;
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Hostile, new Vector2(2f, 0f), Vector2.left);
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(3f, 0f)).WithCatch(), P5.Dt);
            P5.Run(sim, 20);
            Assert.AreEqual(1, sim.Packets.TotalStoredShots(), "fixture: the bolt was caught");
            Assert.AreEqual(0, sim.Score.Score);
            Assert.AreEqual(1f, sim.Score.Multiplier);
        }

        [Test]
        public void BestVolley_CountsDistinctKillsPerRootRelease()
        {
            var sim = Sim();
            for (int i = 0; i < 3; i++) P5.Kill(sim, Enemy(sim, ActorCategory.Pursuer, -6f + 3f * i), root: 40);
            P5.Kill(sim, Enemy(sim, ActorCategory.Pursuer, 6f), root: 41);
            Assert.AreEqual(3, sim.Score.BestVolleyKills);
            Assert.AreEqual(4, sim.Score.KillsByKind[AttackKind.Bolt]);
        }

        [Test]
        public void ReleasesAndHits_GiveTheHitRate()
        {
            var sim = Sim();
            var target = sim.SpawnEnemy(ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            target.ActiveAt = 0;
            target.Phase = EnemyPhase.Recover;
            target.PhaseEndsAt = double.MaxValue;
            CapturedPacket Packet()
            {
                var p = new CapturedPacket { PacketId = sim.Ids.Next(), Capacity = 12 };
                p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f));
                return p;
            }
            ReleaseService.Release(sim, Packet(), sim.Player.Position, Vector2.right, 1f);  // hits
            ReleaseService.Release(sim, Packet(), sim.Player.Position, Vector2.down, 1f);   // misses
            P5.Run(sim, 120);
            Assert.AreEqual(2, sim.Score.PacketsReleased);
            Assert.AreEqual(1, sim.Score.PacketsHit);
            Assert.AreEqual(0.5f, sim.Score.HitRate, 1e-5f);
        }
    }
}
