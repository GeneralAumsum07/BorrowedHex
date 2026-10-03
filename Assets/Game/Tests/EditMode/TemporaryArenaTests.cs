using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class TemporaryArenaTests
    {
        [Test]
        public void PillarWearReportsActualLossBeforeCrumbleAndStaysQuietWhilePaused()
        {
            var cfg = Data.GameConfig.CreateDefault();
            cfg.arena.pillarDecayMinInterval = cfg.arena.pillarDecayMaxInterval = 6;
            var sim = new ArenaSim(cfg, RunSetup.ForSandbox(1));
            var pillar = sim.Pillars[0];
            int lost = 0, crumbles = 0;
            sim.Events.PillarDamaged += (p, amount) => { if (p == pillar) lost += amount; };
            sim.Events.PillarCrumbled += p =>
            {
                if (p != pillar) return;
                Assert.AreEqual(12, lost, "final wear is reported before crumble");
                crumbles++;
            };
            sim.Tick(P5.Still, 24);
            Assert.AreEqual(4, lost, "batched ticks report actual durability lost");
            sim.SetPause(PauseReason.Menu, true);
            sim.Tick(P5.Still, 60);
            Assert.AreEqual(4, lost);
            sim.SetPause(PauseReason.Menu, false);
            sim.Tick(P5.Still, 48);
            sim.Tick(P5.Still, 6);
            Assert.AreEqual(12, lost, "crumbled pillars emit no further wear");
            Assert.AreEqual(1, crumbles);
        }

        [Test]
        public void SixSecondPillarIntervalCrumblesAtSeventyTwoAndIgnoresProjectileHits()
        {
            var cfg = Data.GameConfig.CreateDefault();
            cfg.arena.pillarDecayMinInterval = cfg.arena.pillarDecayMaxInterval = 6;
            var sim = new ArenaSim(cfg, RunSetup.ForSandbox(1));
            sim.Tick(P5.Still, 66f);
            var pillar = sim.Pillars[0];
            Assert.AreEqual(1, pillar.Durability);
            var from = new Vector2(pillar.Bounds.center.x, pillar.Bounds.yMin - 1);
            var shot = AttackSnapshot.From(sim.Attacks.Get(Data.AttackIds.Bolt), 1, 1, 0);
            sim.SpawnProjectile(shot, AttackFaction.Returned, from, Vector2.up);
            sim.Tick(P5.Still, 0.2f);
            Assert.AreEqual(0, sim.Projectiles.Count, "the pillar stops a returned bolt");
            Assert.AreEqual(1, pillar.Durability, "impacts never damage cover");
            sim.SetPause(PauseReason.Menu, true);
            sim.Tick(P5.Still, 30);
            Assert.AreEqual(1, pillar.Durability);
            sim.SetPause(PauseReason.Menu, false);
            sim.Tick(P5.Still, 5.79f);
            Assert.IsFalse(pillar.Crumbled);
            sim.Tick(P5.Still, 0.01f);
            Assert.IsTrue(pillar.Crumbled);
            Assert.AreEqual(4, sim.Walls.Count);
            Assert.AreEqual(4, sim.Score.PillarsCrumbled);
            sim.Tick(P5.Still, 1);
            Assert.AreEqual(4, sim.Score.PillarsCrumbled, "each event fires once");
        }

        [Test]
        public void PillarsRestoreAfterEachChoiceIncludingTheBossTransition()
        {
            var cfg = Data.GameConfig.CreateDefault();
            cfg.arena.pillarDecayMinInterval = cfg.arena.pillarDecayMaxInterval = 0.1f;
            var sim = new ArenaSim(cfg, new RunSetup { Mode = GameMode.Short, Seed = 1 });
            P5.Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                P5.ClearEncounter(sim);
                Assert.AreEqual(4, sim.Walls.Count);
                sim.ContinueFromUpgrade();
                Assert.AreEqual(8, sim.Walls.Count);
                foreach (var pillar in sim.Pillars) Assert.AreEqual(12, pillar.Durability);
            }
            Assert.AreEqual(RunState.BossIntro, sim.State);
        }

        [Test]
        public void RestoringAPillarUnderThePlayerLeavesThemOutsideSolidCover()
        {
            var cfg = Data.GameConfig.CreateDefault();
            cfg.arena.pillarDecayMinInterval = cfg.arena.pillarDecayMaxInterval = 0.1f;
            var sim = new ArenaSim(cfg, new RunSetup { Mode = GameMode.Short, Seed = 1 });
            P5.Invulnerable(sim);
            P5.ClearEncounter(sim);
            var pillar = sim.Pillars[0];
            Assert.IsTrue(pillar.Crumbled);
            sim.Player.Position = pillar.Bounds.center;
            sim.ContinueFromUpgrade();
            Assert.IsFalse(Geometry2D.CircleOverlapsRect(sim.Player.Position, sim.Player.Radius, pillar.Bounds),
                "regrowing cover must not trap a player who crossed the rubble");
        }

        [Test]
        public void AcolyteReturnDamagesTwoDistinctTargetsInALine()
        {
            var sim = TestSims.Sandbox();
            var packet = EmittedPacket(sim, ActorCategory.Acolyte, 0);
            var first = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(2, 0));
            var second = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(4, 0));
            foreach (var e in new[] { first, second })
            {
                e.ActiveAt = 0;
                e.Phase = EnemyPhase.Recover;
                e.PhaseEndsAt = double.MaxValue;
            }
            ReleaseService.Release(sim, packet, Vector2.zero, Vector2.right, 1);
            sim.Tick(P5.Still, 0.5f);
            // D92: an Acolyte's own bolt returned onto Acolytes is resisted (x0.75).
            float own = sim.Config.combat.ownSchoolDamage;
            Assert.AreEqual(first.MaxHealth - own, first.Health, 1e-4f);
            Assert.AreEqual(second.MaxHealth - own, second.Health, 1e-4f);
            Assert.AreEqual(0, sim.Projectiles.Count);
        }

        [Test]
        public void AnOverstayedKillRestoresEliteTimeAndBossNeverOverstays()
        {
            var sim = TestSims.Sandbox();
            var e = sim.SpawnEnemy(ActorCategory.ScatterCaster, new Vector2(8, 0));
            var boss = sim.SpawnBoss();
            P5.Invulnerable(sim);
            sim.Tick(P5.Still, 26);
            Assert.IsTrue(e.Overstayed);
            Assert.AreEqual(1.15f, e.MoveScale);
            Assert.AreEqual(0.75f, e.CooldownScale);
            Assert.AreEqual(30, e.KillValue);
            Assert.IsFalse(boss.Overstayed);
            float before = sim.LifeSeconds;
            P5.Kill(sim, e);
            Assert.AreEqual(before + 7.5f, sim.LifeSeconds, 1e-4f);
            Assert.AreEqual(1, sim.Score.EnemiesOverstayed);
        }

        [Test]
        public void UntouchedPillarsEventuallyStopBlocking()
        {
            var sim = TestSims.Sandbox();
            sim.Tick(P5.Still, 96f);
            Assert.AreEqual(4, sim.Walls.Count, "only the border walls remain");
        }

        [Test]
        public void OverstayRestoresOneAndAHalfHealthOnceAfterTheSpawnWarning()
        {
            var sim = TestSims.Sandbox();
            var e = sim.SpawnEnemy(ActorCategory.Acolyte, new Vector2(8, 0));
            e.Health = 1;
            sim.Tick(P5.Still, 25f);
            Assert.IsFalse(e.Elite, "the warning's 0.8 seconds do not count");
            sim.Tick(P5.Still, 0.8f);
            Assert.IsTrue(e.Elite);
            Assert.AreEqual(4.5f, e.Health, 1e-5f);
            e.Health = 1;
            sim.Tick(P5.Still, 25f);
            Assert.AreEqual(1f, e.Health, "an overstayed enemy never evolves a second time");
        }

        static CapturedPacket EmittedPacket(ArenaSim sim, ActorCategory category, params float[] spread)
        {
            var e = sim.SpawnEnemy(category, new Vector2(9, 0));
            AttackEmitter.FireVolley(sim, Data.AttackIds.Bolt, e.ActorId, e.Position, e.Radius, Vector2.left, spread);
            var packet = new CapturedPacket();
            foreach (var shot in sim.Projectiles) packet.Payloads.Add(shot.Shot);
            sim.ClearProjectiles();
            sim.DespawnEnemy(e); // identities must survive the source disappearing
            return packet;
        }

        [Test]
        public void AcolyteHexPiercesOneExtraEnemyAfterItsSourceDisappears()
        {
            var sim = TestSims.Sandbox();
            var packet = EmittedPacket(sim, ActorCategory.Acolyte, 0);
            ReleaseService.Release(sim, packet, Vector2.zero, Vector2.right, 1);
            Assert.AreEqual(1, sim.Projectiles[0].PierceRemaining);
        }

        [Test]
        public void ScatterHexCompressesTheFanAndDealsOneAndAHalfPerPellet()
        {
            var sim = TestSims.Sandbox();
            var packet = EmittedPacket(sim, ActorCategory.ScatterCaster, -24, -12, 0, 12, 24);
            ReleaseService.Release(sim, packet, Vector2.zero, Vector2.right, 1);
            Assert.AreEqual(5, sim.Projectiles.Count);
            foreach (var shot in sim.Projectiles)
            {
                Assert.AreEqual(1.5f, shot.Shot.ReturnedDamage);
                Assert.LessOrEqual(Mathf.Abs(Vector2.SignedAngle(Vector2.right, shot.Direction)), 12.001f);
            }
            sim.Tick(P5.Still, 1);
            Assert.AreEqual(0, sim.Projectiles.Count, "the pellets cannot fly farther than six units");
        }

        [Test]
        public void BossHexReturnsAsAHeavyTwoDamageBolt()
        {
            var sim = TestSims.Sandbox();
            var e = sim.SpawnBoss();
            AttackEmitter.FireVolley(sim, Data.AttackIds.Bolt, e.ActorId, e.Position, e.Radius, Vector2.down, new[] { 0f });
            var packet = new CapturedPacket();
            packet.Payloads.Add(sim.Projectiles[0].Shot);
            sim.ClearProjectiles();
            sim.DespawnEnemy(e);
            ReleaseService.Release(sim, packet, Vector2.zero, Vector2.right, 1);
            Assert.AreEqual(AttackKind.HeavyShot, sim.Projectiles[0].Shot.Kind);
            Assert.AreEqual(2f, sim.Projectiles[0].Shot.ReturnedDamage);
        }
    }
}
