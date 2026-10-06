using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Phase 11: capture styles (section 7) and the Phase 11 checks in section 9.
    public class StyleTests
    {
        const float Dt = 1f / 60f;
        static GameConfig Cfg => TestSims.Config;
        static readonly Vector2 East = Vector2.right;

        static ArenaSim Sim(string style)
        {
            var setup = RunSetup.ForSandbox(1);
            setup.StyleId = style;
            setup.Stats = Loadout.Resolve(Cfg, new string[0], style);
            var sim = new ArenaSim(Cfg, setup);
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        // Aim is a world point, so keep it ahead of the player wherever the dash has taken them.
        static PlayerCommand Hold(ArenaSim sim) => PlayerCommand.Moving(Vector2.zero).WithAim(sim.Player.Position + East * 5f);
        static PlayerCommand MoveEast(ArenaSim sim) => PlayerCommand.Moving(East).WithAim(sim.Player.Position + East * 5f);

        static void Run(ArenaSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(Hold(sim), Dt);
        }

        static ProjectileActor Shot(ArenaSim sim, Vector2 from, Vector2 dir, float speed = -1f)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            if (speed > 0) s.Speed = speed;
            return sim.SpawnProjectile(s, AttackFaction.Hostile, from, dir);
        }

        // ---- Stats ----

        [Test]
        public void Snatcher_IsTheBaselineCatch_Unchanged()
        {
            var b = PlayerStats.FromConfig(Cfg);
            var s = Loadout.Resolve(Cfg, null, CaptureStyles.Snatcher);
            Assert.AreEqual(b.CaptureConeAngle, s.CaptureConeAngle);
            Assert.AreEqual(b.CaptureRange, s.CaptureRange);
            Assert.AreEqual(b.CaptureWindow, s.CaptureWindow);
            Assert.AreEqual(b.CaptureRecovery, s.CaptureRecovery);
            Assert.IsFalse(s.CatchIsDash);
        }

        [Test]
        public void Collector_Is140Degrees_040Window_100Recovery_SameRange()
        {
            var s = Loadout.Resolve(Cfg, null, CaptureStyles.Collector);
            Assert.AreEqual(140f, s.CaptureConeAngle, 1e-4f);
            Assert.AreEqual(0.40f, s.CaptureWindow, 1e-4f);
            Assert.AreEqual(1.00f, s.CaptureRecovery, 1e-4f);
            Assert.AreEqual(Cfg.capture.range, s.CaptureRange, 1e-4f);
        }

        [Test]
        public void PassivesApplyOnTopOfTheStyle()
        {
            var s = Loadout.Resolve(Cfg, new[] { SkillTree.PrecisionAngle }, CaptureStyles.Collector);
            Assert.AreEqual(140f + Cfg.progression.precisionAngle, s.CaptureConeAngle, 1e-4f);
            var d = Loadout.Resolve(Cfg, new[] { SkillTree.MobilityDashRecovery }, CaptureStyles.Daredevil);
            Assert.AreEqual(Cfg.player.dashCooldown - Cfg.progression.mobilityDashRecovery, d.DashCooldown, 1e-4f,
                "Daredevil's catch shares the improved dash cooldown");
        }

        [Test]
        public void EveryStyle_KeepsSlotsLifetimeAndBackfire()
        {
            var b = PlayerStats.FromConfig(Cfg);
            foreach (var style in CaptureStyles.All)
            {
                var s = Loadout.Resolve(Cfg, null, style.Id);
                Assert.AreEqual(2, s.PacketSlots, style.Id);
                Assert.AreEqual(3f, s.PacketLifetime, 1e-6f, style.Id);
                Assert.AreEqual(b.BackfireSeconds, s.BackfireSeconds, style.Id);
                Assert.AreEqual(b.Power.Evaluate(1.0, 3f), s.Power.Evaluate(1.0, 3f), 1e-6f, style.Id);
            }
        }

        [Test]
        public void UnknownStyleIds_FallBackToSnatcher_AndKeepTheProfileValid()
        {
            Assert.AreEqual(CaptureStyles.Snatcher, CaptureStyles.Resolve("laser").Id);
            Assert.AreEqual(CaptureStyles.Snatcher, CaptureStyles.Resolve(null).Id);
            var s = Loadout.Resolve(Cfg, null, "laser");
            Assert.AreEqual(Cfg.capture.coneAngle, s.CaptureConeAngle);
            Assert.IsFalse(s.CatchIsDash);

            var p = new PlayerProfile { styleId = "laser" };
            Assert.IsTrue(ProfileService.Validate(p, out string why), why);
            Assert.AreEqual(CaptureStyles.Snatcher, p.styleId, "repaired, not rejected");
            var q = new PlayerProfile { styleId = CaptureStyles.Daredevil };
            Assert.IsTrue(ProfileService.Validate(q, out why), why);
            Assert.AreEqual(CaptureStyles.Daredevil, q.styleId);
        }

        [Test]
        public void StyleCards_ShowEffectiveNumbers()
        {
            var p = new PlayerProfile();
            p.ownedNodes.Add(SkillTree.PrecisionAngle);   // D101: owned = active
            string collector = StylePanel.CardBody(CaptureStyles.Resolve(CaptureStyles.Collector), p, Cfg, false);
            StringAssert.Contains($"{140f + Cfg.progression.precisionAngle:0}°", collector, "the card includes passives");
            StringAssert.Contains("1.00 s", collector);
            string dare = StylePanel.CardBody(CaptureStyles.Resolve(CaptureStyles.Daredevil), p, Cfg, true);
            StringAssert.Contains("dash", dare);
            StringAssert.Contains("Selected", dare);
        }

        // ---- Collector in play ----

        [Test]
        public void Collector_CatchesAShotAt65Degrees_SnatcherDoesNot()
        {
            foreach (var (style, caught) in new[] { (CaptureStyles.Collector, 1), (CaptureStyles.Snatcher, 0) })
            {
                var sim = Sim(style);
                // 65 degrees off the aim: outside Snatcher's 45 half-angle, inside Collector's 70.
                Vector2 from = (Vector2)(Quaternion.Euler(0, 0, 65f) * Vector3.right) * 2f;
                Shot(sim, from, -from);
                sim.Tick(Hold(sim).WithCatch(), Dt);
                Run(sim, 20);
                Assert.AreEqual(caught, sim.Packets.Packets.Count, style);
            }
        }

        // ---- Daredevil ----

        [Test]
        public void Daredevil_CatchAndDashOnOneTick_IsOneAction()
        {
            var sim = Sim(CaptureStyles.Daredevil);
            int dashes = 0, catches = 0;
            sim.Events.Dashed += (_, __) => dashes++;
            sim.Events.CatchActivated += _ => catches++;
            sim.Tick(MoveEast(sim).WithCatch().WithDash(), Dt);
            Assert.AreEqual(1, dashes, "one dash");
            Assert.AreEqual(1, catches, "one catch window: the dash IS the catch");
            Assert.IsTrue(sim.Player.Dashing);
            // Shared cooldown: neither input does anything until the dash is ready again.
            int cooldownTicks = Mathf.CeilToInt(sim.Stats.DashCooldown / Dt);
            for (int i = 0; i < cooldownTicks - 3; i++)
                sim.Tick(Hold(sim).WithCatch().WithDash(), Dt);
            Assert.AreEqual(1, dashes);
            Assert.AreEqual(1, catches);
            Run(sim, 5);
            sim.Tick(Hold(sim).WithCatch(), Dt);
            Assert.AreEqual(2, dashes, "ready again after the shared cooldown");
            Assert.AreEqual(2, catches);
        }

        /// <summary>
        /// Owner direction (D88): the capturing dash goes where the player AIMS, so a Daredevil
        /// can run north while lunging east at a shot. The plain dash still follows movement.
        /// </summary>
        [Test]
        public void Daredevil_CatchingDash_FollowsTheAim_PlainDash_FollowsMovement()
        {
            var north = PlayerCommand.Moving(Vector2.up);

            var sim = Sim(CaptureStyles.Daredevil);
            sim.Tick(north.WithAim(sim.Player.Position + East * 5f).WithCatch(), Dt);
            Assert.IsTrue(sim.Player.Dashing);
            Assert.AreEqual(East.x, sim.Player.DashDirection.x, 1e-4f, "catching dash goes toward the aim...");
            Assert.AreEqual(0f, sim.Player.DashDirection.y, 1e-4f, "...not toward the movement key");
            Assert.Greater(sim.Player.Position.x, 0f);

            // Catch + dash on one tick is still the capturing dash (D83), so it aims too.
            var both = Sim(CaptureStyles.Daredevil);
            both.Tick(north.WithAim(both.Player.Position + East * 5f).WithCatch().WithDash(), Dt);
            Assert.AreEqual(East.x, both.Player.DashDirection.x, 1e-4f);

            var plain = Sim(CaptureStyles.Daredevil);
            plain.Tick(north.WithAim(plain.Player.Position + East * 5f).WithDash(), Dt);
            Assert.AreEqual(1f, plain.Player.DashDirection.y, 1e-4f, "the plain dash is unchanged: movement first");

            // Other styles' dash never looked at the catch and still follows movement.
            var snatcher = Sim(CaptureStyles.Snatcher);
            snatcher.Tick(north.WithAim(snatcher.Player.Position + East * 5f).WithCatch().WithDash(), Dt);
            Assert.AreEqual(1f, snatcher.Player.DashDirection.y, 1e-4f);
        }

        [Test]
        public void Daredevil_PlainDash_OpensNoWindow_AndUsesTheSharedCooldown()
        {
            var sim = Sim(CaptureStyles.Daredevil);
            int catches = 0;
            sim.Events.CatchActivated += _ => catches++;
            sim.Tick(MoveEast(sim).WithDash(), Dt);
            Assert.IsTrue(sim.Player.Dashing);
            Assert.IsFalse(sim.Capture.IsWindowOpen(sim.Clock.Now));
            sim.Tick(Hold(sim).WithCatch(), Dt);
            Assert.AreEqual(0, catches, "the catch waits for the dash it shares a cooldown with");
        }

        [Test]
        public void Daredevil_CatchesAShotBesideItsPath_EvenOneMovingAway()
        {
            var sim = Sim(CaptureStyles.Daredevil);
            // Slow shot 0.8 north of the dash line, drifting further north: never near the
            // start position, never approaching; only the dash path reaches it.
            Shot(sim, new Vector2(2f, 0.8f), Vector2.up, speed: 0.5f);
            sim.Tick(MoveEast(sim).WithCatch(), Dt);
            Run(sim, 20);
            Assert.AreEqual(1, sim.Packets.Packets.Count);
            Assert.AreEqual(0, sim.Score.DamageTaken);
        }

        [Test]
        public void Daredevil_DoesNotCatchThroughAWallOrStandingPillar()
        {
            var sim = Sim(CaptureStyles.Daredevil);
            // A thin solid box between the dash line and the same shot. Pillars live in the
            // same Walls list (a crumbled one is removed from it), so this is the pillar path.
            sim.Walls.Add(new Rect(1.0f, 0.45f, 2.0f, 0.1f));
            Shot(sim, new Vector2(2f, 0.8f), Vector2.up, speed: 0.5f);
            sim.Tick(MoveEast(sim).WithCatch(), Dt);
            Run(sim, 20);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
            Assert.Greater(sim.Player.Position.x, 2f, "the dash itself was not blocked");
        }

        [Test]
        public void Daredevil_DoesNotCatchAfterTheDashEnds()
        {
            var sim = Sim(CaptureStyles.Daredevil);
            sim.Tick(MoveEast(sim).WithCatch(), Dt);
            Run(sim, 15); // dash over (0.18 s)
            Assert.IsFalse(sim.Player.Dashing);
            Shot(sim, sim.Player.Position + new Vector2(0f, 0.8f), Vector2.up, speed: 0.5f);
            Run(sim, 5);
            Assert.AreEqual(0, sim.Packets.Packets.Count);
        }

        // ---- Every style keeps the frozen-slot and backfire rules ----

        static void CatchOneHeadOn(ArenaSim sim)
        {
            Shot(sim, sim.Player.Position + new Vector2(2f, 0f), Vector2.left);
            var cmd = sim.Stats.CatchIsDash ? MoveEast(sim) : Hold(sim);
            sim.Tick(cmd.WithCatch(), Dt);
            Run(sim, 20);
        }

        [Test]
        public void EveryStyle_SelectedSlotBackfires_OtherSlotStaysFrozen()
        {
            foreach (var style in CaptureStyles.All)
            {
                var sim = Sim(style.Id);
                CatchOneHeadOn(sim);
                Assert.AreEqual(1, sim.Packets.Packets.Count, style.Id);
                float ready = Mathf.Max(sim.Stats.CaptureRecovery, sim.Stats.CatchIsDash ? sim.Stats.DashCooldown : 0f);
                Run(sim, Mathf.CeilToInt(ready / Dt) + 2);
                TestSims.Pocket(sim);   // D89: the second catch needs a free hand
                CatchOneHeadOn(sim);
                Assert.AreEqual(2, sim.Packets.Packets.Count, style.Id);
                var second = sim.Packets.InSlot(1);
                Assert.IsNotNull(second, style.Id);
                // Select the first hex again. The second decayed while it was the hand, so it
                // freezes at what it has left now, not at a full 3 s (D89).
                TestSims.Pocket(sim);
                float frozenAt = second.Remaining(sim.Clock.Now);

                float lifeBefore = sim.LifeSeconds;
                // The first hex was frozen while the second was caught, so it expires later than
                // first + 3 s: run until it backfires (bounded), then check the second.
                for (int i = 0; i < 300 && sim.Score.Backfires == 0; i++) Run(sim, 1);
                Assert.AreEqual(1, sim.Score.Backfires, style.Id);
                Assert.AreEqual(1, sim.Packets.Packets.Count, style.Id);
                Assert.Less(sim.LifeSeconds, lifeBefore - sim.Stats.BackfireSeconds + 1f, style.Id + ": the backfire cost the clock");
                Assert.AreEqual(frozenAt, second.Remaining(sim.Clock.Now), 1e-6f, style.Id + ": unselected slot frozen");
            }
        }
    }
}
