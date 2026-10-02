using System.Collections.Generic;
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
    // Parry (replaces the lantern, D26): a catch window open while facing a Pursuer when its
    // strike resolves intercepts the strike and redirects it at the attacker as a riposte.
    public class ParryTests
    {
        static readonly PlayerCommand AimRight = P4.Still.WithAim(new Vector2(5f, 0f));
        static readonly PlayerCommand AimLeft = P4.Still.WithAim(new Vector2(-5f, 0f));

        /// <summary>An active pursuer just right of the player, about to start its wind-up.</summary>
        static EnemyActor ReadyPursuer(ArenaSim sim, Vector2 at)
        {
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, at);
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            return e;
        }

        /// <summary>Tick until the strike is <paramref name="before"/> seconds from resolving.</summary>
        static void RunUntilStrikeIn(ArenaSim sim, EnemyActor e, double before, PlayerCommand cmd)
        {
            for (int guard = 0; guard < 600; guard++)
            {
                if (e.Phase == EnemyPhase.Telegraph && e.PhaseEndsAt - sim.Clock.Now <= before) return;
                sim.Tick(cmd, P4.Dt);
            }
            Assert.Fail("pursuer never wound up");
        }

        [Test]
        public void FacingTheStrike_WithWindowOpen_ParriesAndTheRiposteKillsIt()
        {
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            var parries = new List<EnemyActor>();
            sim.Events.StrikeParried += (attacker, _) => parries.Add(attacker);
            RunUntilStrikeIn(sim, e, 0.1, AimRight);
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 30, AimRight);

            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health, "parried strike deals no damage");
            CollectionAssert.AreEqual(new[] { e }, parries, "exactly one parry");
            Assert.IsTrue(e.Killed, "riposte (2 dmg) kills a 2-health pursuer");
        }

        [Test]
        public void Riposte_IsAReturnedShotAttributedToTheAttacker()
        {
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            // Copy at spawn: projectiles are pooled, so a held reference may be reused later.
            int count = 0;
            AttackFaction faction = default;
            AttackSnapshot shot = default;
            Vector2 vel = default;
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.Kind != AttackKind.Riposte) return;
                count++; faction = p.Faction; shot = p.Shot; vel = p.Velocity;
            };
            RunUntilStrikeIn(sim, e, 0.1, AimRight);
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 10, AimRight);
            Assert.AreEqual(1, count);
            Assert.AreEqual(AttackFaction.Returned, faction);
            Assert.AreEqual(e.ActorId, shot.SourceActorId, "borrowed from the attacker");
            Assert.IsFalse(shot.Capturable);
            Assert.Greater(Vector2.Dot(vel, Vector2.right), 0f, "flies at the attacker");
        }

        [Test]
        public void FacingAway_TheStrikeStillHurts()
        {
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            RunUntilStrikeIn(sim, e, 0.1, AimLeft);
            sim.Tick(AimLeft.WithCatch(), P4.Dt);
            P4.Run(sim, 30, AimLeft);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(sim.Stats.MaxHealth - 1, sim.Player.Health);
            Assert.IsFalse(e.Killed);
        }

        [Test]
        public void WindowClosedBeforeTheStrike_StillHurts()
        {
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            // Press far too early: the 0.25 s window has shut by the time the strike lands.
            RunUntilStrikeIn(sim, e, 0.45, AimRight);
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 40, AimRight);
            Assert.AreEqual(sim.Stats.MaxHealth - 1, sim.Player.Health);
        }

        [Test]
        public void ParryWorks_EvenWithBothPacketSlotsFull()
        {
            // Parry must never be blocked by packet bookkeeping: it is the answer to a melee-only
            // wave, so it has to work exactly when the player is holding ammunition too.
            var sim = P4.Sim();
            var bolt = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), 0f);
            for (int i = 0; i < sim.Stats.PacketSlots; i++)
                sim.Packets.Create(sim.Ids.Next(), 100 + i, 0, 99f, sim.Stats.PacketCapacity).Payloads.Add(bolt);
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            RunUntilStrikeIn(sim, e, 0.1, AimRight);
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 30, AimRight);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
            Assert.IsTrue(e.Killed);
        }

        [Test]
        public void Riposte_PiercesIntoASecondEnemyBehind()
        {
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            var behind = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(3.4f, 0f));
            RunUntilStrikeIn(sim, e, 0.1, AimRight);
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 40, AimRight);
            Assert.IsTrue(e.Killed);
            Assert.IsTrue(behind.Killed, "pierce 1 carries the riposte through");
        }

        [Test]
        public void AStrikeThatWouldMiss_IsNotParried()
        {
            // No free ripostes: there must be a real hit to intercept.
            var sim = P4.Sim();
            var e = ReadyPursuer(sim, new Vector2(1.3f, 0f));
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            RunUntilStrikeIn(sim, e, 0.1, AimRight);
            sim.Player.Position = new Vector2(-1.4f, 0f); // out of the locked strike circle
            sim.Tick(AimRight.WithCatch(), P4.Dt);
            P4.Run(sim, 30, AimRight);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(sim.Stats.MaxHealth, sim.Player.Health);
        }

        [Test]
        public void RunHasNoLanternFallback_AMeleeOnlyArenaFiresNothing()
        {
            var sim = P4.Sim();
            sim.Player.InvulnerableUntil = double.MaxValue;
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(8f, -5f));
            e.ActiveAt = double.MaxValue; // alive, melee-only, harmless
            int shots = 0;
            sim.Events.ProjectileSpawned += _ => shots++;
            P4.Run(sim, 60 * 8);
            Assert.AreEqual(0, shots);
        }
    }

    // Playtest controls: summon one specific kind, toggle the sandbox director, clear the arena.
    public class SandboxControlTests
    {
        [Test]
        public void SummonEnemy_SpawnsExactlyThatKind_AwayFromThePlayer()
        {
            foreach (var kind in new[] { ActorCategory.Acolyte, ActorCategory.Pursuer, ActorCategory.ScatterCaster, ActorCategory.SiegeFamiliar })
            {
                var sim = P4.Sim();
                var e = sim.SummonEnemy(kind);
                Assert.AreEqual(1, sim.Enemies.Count);
                Assert.AreEqual(kind, e.Category);
                Assert.GreaterOrEqual((e.Position - sim.Player.Position).magnitude, sim.Config.combat.minSpawnDistance);
            }
        }

        [Test]
        public void AutoSpawnOff_DirectorStaysQuiet_ButSummonStillWorks()
        {
            var setup = RunSetup.ForSandbox(3);
            setup.SandboxAutoSpawn = true;
            var sim = new ArenaSim(TestSims.Config, setup);
            Assert.Greater(sim.Enemies.Count, 0, "auto-spawn starts with a formation");
            sim.AutoSpawn = false;
            sim.ClearArena();
            P4.Run(sim, 60 * 4);
            Assert.AreEqual(0, sim.AliveEnemyCount(), "director must not refill the arena");
            sim.SummonEnemy(ActorCategory.Pursuer);
            Assert.AreEqual(1, sim.AliveEnemyCount());
        }

        [Test]
        public void AutoSpawnOn_RefillsAnEmptyArena()
        {
            var sim = P4.Sim();
            sim.AutoSpawn = true;
            P4.Run(sim, 60 * 2);
            Assert.Greater(sim.AliveEnemyCount(), 0);
        }

        [Test]
        public void ClearArena_DespawnsWithoutKillsAndRemovesHostileShots()
        {
            var sim = P4.Sim();
            sim.SummonEnemy(ActorCategory.Acolyte);
            sim.SummonEnemy(ActorCategory.Pursuer);
            var bolt = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(bolt, AttackFaction.Hostile, new Vector2(5f, 5f), Vector2.left);
            int kills = 0;
            sim.Events.EnemyKilled += (_, __) => kills++;
            sim.ClearArena();
            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(0, sim.AliveEnemyCount());
            Assert.AreEqual(0, sim.Projectiles.Count);
            Assert.AreEqual(0, kills);
        }
    }
}
