using static BorrowedHex.Tests.ClockFixtures;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Phase 1 checks (section 9): movement, dash, aim, health.
    public class PlayerTests
    {
        const float Dt = 1f / 60f;

        static ArenaSim NewSim(Vector2? spawn = null)
        {
            var sim = TestSims.Sandbox();
            if (spawn.HasValue) sim.Player.Position = spawn.Value;
            return sim;
        }

        [Test]
        public void DiagonalSpeed_EqualsStraightSpeed()
        {
            var straight = NewSim(Vector2.zero);
            var diagonal = NewSim(Vector2.zero);
            for (int i = 0; i < 30; i++)
            {
                straight.Tick(PlayerCommand.Moving(new Vector2(1, 0)), Dt);
                diagonal.Tick(PlayerCommand.Moving(new Vector2(1, 1)), Dt);
            }
            float expected = straight.Stats.MoveSpeed * 30 * Dt;
            Assert.AreEqual(expected, straight.Player.Position.magnitude, 1e-3f);
            Assert.AreEqual(expected, diagonal.Player.Position.magnitude, 1e-3f);
        }

        [Test]
        public void ZeroInput_ProducesNoMovementOrInvalidValues()
        {
            var sim = NewSim(Vector2.zero);
            for (int i = 0; i < 10; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            Assert.AreEqual(Vector2.zero, sim.Player.Position);
            Assert.IsFalse(float.IsNaN(sim.Player.AimDirection.x));
        }

        [Test]
        public void Dash_TravelsConfiguredDistanceInOpenSpace()
        {
            var sim = NewSim(Vector2.zero);
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithDash(), Dt);
            for (int i = 0; i < 30; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            // No movement input → dash follows aim (default +Z).
            Assert.AreEqual(sim.Stats.DashDistance, sim.Player.Position.y, 0.02f);
            Assert.AreEqual(0f, sim.Player.Position.x, 1e-4f);
        }

        [Test]
        public void Dash_StopsAtWall()
        {
            var b = TestSims.Config.arena.bounds;
            // Start one unit from the east wall and dash east.
            var sim = NewSim(new Vector2(b.xMax - 1f, 0f));
            sim.Tick(PlayerCommand.Moving(new Vector2(1, 0)).WithDash(), Dt);
            for (int i = 0; i < 30; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            float maxX = b.xMax - sim.Stats.BodyRadius;
            Assert.LessOrEqual(sim.Player.Position.x, maxX + 1e-3f);
            Assert.Greater(sim.Player.Position.x, maxX - 0.05f, "dash should reach the wall, not stop early");
        }

        [Test]
        public void Dash_RespectsCooldown()
        {
            var sim = NewSim(Vector2.zero);
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithDash(), Dt);
            for (int i = 0; i < 20; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            float after = sim.Player.Position.y;
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithDash(), Dt); // still cooling down
            for (int i = 0; i < 20; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            Assert.AreEqual(after, sim.Player.Position.y, 1e-4f);
        }

        [Test]
        public void MissingAimProjection_PreservesLastValidDirection()
        {
            var sim = NewSim(Vector2.zero);
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(-3, 0)), Dt);
            Assert.AreEqual(new Vector2(-1, 0), sim.Player.AimDirection);
            sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt); // no aim this frame (cursor off-window)
            Assert.AreEqual(new Vector2(-1, 0), sim.Player.AimDirection);
            // Aim point on top of the player is degenerate and must also be ignored.
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithAim(sim.Player.Position), Dt);
            Assert.AreEqual(new Vector2(-1, 0), sim.Player.AimDirection);
        }

        [Test]
        public void RepeatedHitsDuringInvulnerability_CostOneHealth()
        {
            var sim = NewSim();
            float before = sim.LifeSeconds;
            Assert.IsTrue(sim.DamagePlayer(1, sourceActorId: 0));
            Assert.IsFalse(sim.DamagePlayer(1, 0));
            sim.Tick(PlayerCommand.Moving(Vector2.zero), 0.3f);
            Assert.IsFalse(sim.DamagePlayer(1, 0));
            Assert.AreEqual(before - 1.3f, sim.LifeSeconds, 1e-4f);
            for (int i = 0; i < 30; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt);
            Assert.IsTrue(sim.DamagePlayer(1, 0), "invulnerability should have expired after 0.65 s");
        }

        [Test]
        public void TwoLethalCallbacks_EmitOneDeath()
        {
            var sim = NewSim();
            int deaths = 0;
            sim.Events.PlayerDied += () => deaths++;
            LeaveOneSecond(sim);
            sim.DamagePlayer(1, 0);
            sim.DamagePlayer(1, 0);
            sim.Player.ClearInvulnerability();
            sim.DamagePlayer(5, 0);
            Assert.AreEqual(1, deaths);
            Assert.IsFalse(sim.Player.Alive);
        }

        [Test]
        public void DashInvulnerability_CoversOnlyTheFirstWindow()
        {
            var sim = NewSim(Vector2.zero);
            sim.Tick(PlayerCommand.Moving(Vector2.zero).WithDash(), Dt);
            Assert.IsFalse(sim.DamagePlayer(1, 0), "dash start is invulnerable");
            for (int i = 0; i < 10; i++) sim.Tick(PlayerCommand.Moving(Vector2.zero), Dt); // ~0.18 s
            Assert.IsTrue(sim.DamagePlayer(1, 0), "after 0.12 s the dash no longer protects");
        }

        [Test]
        public void PausedSim_DoesNotMoveThePlayer()
        {
            var sim = NewSim(Vector2.zero);
            sim.Clock.SetPauseReason(PauseReason.FocusLost, true);
            for (int i = 0; i < 10; i++) sim.Tick(PlayerCommand.Moving(new Vector2(1, 0)), Dt);
            Assert.AreEqual(Vector2.zero, sim.Player.Position);
        }
    }

    /// <summary>Shared factory: a sim with code-default tuning and no encounter director.</summary>
    public static class TestSims
    {
        static GameConfig config;
        public static GameConfig Config => config != null ? config : (config = GameConfig.CreateDefault());

        public static ArenaSim Sandbox(int seed = 1) => new ArenaSim(Config, RunSetup.ForSandbox(seed));

        /// <summary>
        /// Seed a packet bypassing the hand rule (D89): the selected slot if it is free, else the
        /// first free slot — exactly what PacketStore.Create did before D89, so tests written
        /// against the old auto-banking keep their meaning. Tests that need "two packets held"
        /// use this; tests about WHICH slot a catch fills must go through a real catch (or
        /// PacketStore.Create) instead.
        /// </summary>
        public static CapturedPacket Seed(PacketStore store, int packetId, int activationId, double now, float lifetime, int capacity)
        {
            var inHand = store.CreateInSlot(store.SelectedSlot, packetId, activationId, now, lifetime, capacity);
            if (inHand != null) return inHand;
            for (int slot = 0; slot < store.SlotCount; slot++)
            {
                var p = store.CreateInSlot(slot, packetId, activationId, now, lifetime, capacity);
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>Rule A (D89): one tick with Q pressed, pocketing the held hex so the hand is free.</summary>
        public static void Pocket(ArenaSim sim) => sim.Tick(PlayerCommand.Moving(Vector2.zero).WithCycle(), 1f / 60f);
    }
}
