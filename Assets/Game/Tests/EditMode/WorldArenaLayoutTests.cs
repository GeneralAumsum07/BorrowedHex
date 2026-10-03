using System;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class WorldArenaLayoutTests
    {
        static ArenaLayout Layout(int stage)
        {
            var type = typeof(ArenaSim).Assembly.GetType("BorrowedHex.Data.WorldArenaLayouts");
            Assert.That(type, Is.Not.Null, "Stages need distinct authored world layouts.");
            return (ArenaLayout)type.GetMethod("Create").Invoke(null, new object[] { stage });
        }

        [Test]
        public void FourStagesHaveDistinctLargerBoundsAndMoreThanFourCoverObjects()
        {
            var shapes = new System.Collections.Generic.HashSet<string>();
            for (int stage = 0; stage < 4; stage++)
            {
                var layout = Layout(stage);
                Assert.That(layout.bounds.width, Is.GreaterThan(24));
                Assert.That(layout.bounds.height, Is.GreaterThan(16));
                Assert.That(shapes.Add(string.Join("|", layout.pillars)), Is.True);
                Assert.That(layout.pillars.Count, Is.GreaterThan(4));
                foreach (var box in layout.pillars)
                {
                    Assert.That(layout.bounds.Contains(box.min), Is.True);
                    Assert.That(layout.bounds.Contains(box.max), Is.True);
                    Assert.That(Geometry2D.CircleOverlapsRect(layout.playerSpawn, .5f, box), Is.False);
                }
            }
        }

        [Test]
        public void FollowingCameraLooksDownAtFiftyDegreesAndCannotSeeOutsideTheWorldTerrain()
        {
            var type = typeof(ArenaSim).Assembly.GetType("BorrowedHex.Presentation.WorldArt.WorldCameraPolicy");
            Assert.That(type, Is.Not.Null);
            var rotation = (Quaternion)type.GetMethod("Rotation").Invoke(null, null);
            // Spec 2026-10-04 raised the pitch to 50 so the enclosure, not a clip plane, hides the void.
            Assert.That(rotation.eulerAngles.x, Is.EqualTo(50f).Within(.01f));
            var centre = (Vector2)type.GetMethod("ClampFocus").Invoke(null, new object[] { new Vector2(1000, 1000), Layout(0).bounds });
            Assert.That(Layout(0).bounds.Contains(centre), Is.True);
        }

        [Test]
        public void RealStageFlowReplacesBoundsAndCoverWithoutMutatingTheSourceConfig()
        {
            var config = TestSims.Config;
            var original = config.arena.bounds;
            var sim = new ArenaSim(config, new RunSetup { Seed = 12, WorldArenas = true });
            P5.Invulnerable(sim);
            for (int stage = 1; stage < 4; stage++)
            {
                P5.ClearEncounter(sim);
                Assert.That(sim.ContinueFromUpgrade(), Is.True);
                Assert.That(sim.ArenaStage, Is.EqualTo(stage));
                Assert.That(sim.Arena.bounds, Is.EqualTo(Layout(stage).bounds));
                Assert.That(sim.Arena.bounds.Contains(sim.Player.Position), Is.True);
                Assert.That(sim.Pillars.Count, Is.EqualTo(Layout(stage).pillars.Count));
                var borders = sim.Arena.BuildObstacles();
                for (int i = 0; i < 4; i++) CollectionAssert.Contains(sim.Walls, borders[i]);
            }
            Assert.That(sim.Boss, Is.Not.Null);
            Assert.That(sim.Arena.bounds.Contains(sim.Boss.Position), Is.True);
            Assert.That(config.arena.bounds, Is.EqualTo(original));
        }

        [Test]
        public void TravelPauseCostsNoLifeAndTutorialKeepsItsExistingLayout()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Sandbox = true, WorldArenas = true });
            float life = sim.LifeSeconds;
            sim.Clock.SetPauseReason(PauseReason.WorldTransition, true);
            sim.Tick(Player.PlayerCommand.Moving(Vector2.right), 2);
            Assert.That(sim.Clock.Now, Is.Zero);
            Assert.That(sim.LifeSeconds, Is.EqualTo(life));
            var setup = RunSetup.ForTutorial(1); setup.WorldArenas = true;
            var tutorial = new ArenaSim(TestSims.Config, setup);
            Assert.That(tutorial.Arena, Is.SameAs(TestSims.Config.arena));
        }

        // Drive the same stage edge as the director, without depending on enemy clear
        // speed. These checks cover occupied footprints and overlapping generations.
        static void Change(ArenaSim sim, int stage)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ArenaSim).GetMethod("SelectWorldArena", flags).Invoke(sim, new object[] { stage });
            typeof(ArenaSim).GetMethod("RestorePillars", flags).Invoke(sim, null);
        }

        [Test]
        public void LiveMorphPreservesPositionAndDefersOccupiedCoverUntilThePlayerLeaves()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Sandbox = true, WorldArenas = true });
            sim.AutoSpawn = false;
            var incoming = Layout(1).pillars[0];
            sim.Player.Position = incoming.center;
            Change(sim, 1);
            Assert.That(sim.Player.Position, Is.EqualTo(incoming.center), "Pending cover must never eject the player.");
            Assert.That(sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.False);
            Assert.That(sim.CoverFormed(0), Is.False);
            CollectionAssert.DoesNotContain(sim.Walls, incoming);
            sim.Tick(default, 24);
            Assert.That(sim.Clock.Now, Is.EqualTo(24));
            Assert.That(sim.CoverFormed(0), Is.False, "Formation waits for an occupied footprint.");
            sim.Player.Position = Vector2.zero;
            sim.Tick(default, .1f);
            Assert.That(sim.CoverFormed(0), Is.True);
            CollectionAssert.Contains(sim.Walls, incoming);
        }

        [Test]
        public void PausingFreezesMorphAndRapidStageChangesRemoveAbandonedCollision()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Sandbox = true, WorldArenas = true });
            sim.AutoSpawn = false;
            Change(sim, 1);
            Assert.That(sim.RetiringCover.Count, Is.GreaterThan(0));
            sim.Clock.SetPauseReason(PauseReason.Manual, true);
            sim.Tick(default, 12);
            Assert.That(sim.WorldMorphProgress, Is.Zero);
            sim.Clock.SetPauseReason(PauseReason.Manual, false);
            var abandoned = sim.RetiringCover.ToArray();
            Change(sim, 2);
            foreach (var old in abandoned)
            {
                Assert.That(old.Crumbled, Is.True);
                CollectionAssert.DoesNotContain(sim.Walls, old.Bounds);
            }
            sim.Tick(default, 24);
            Assert.That(sim.WorldMorphProgress, Is.EqualTo(1));
            foreach (var old in sim.RetiringCover) Assert.That(old.Crumbled, Is.True);
            foreach (var cover in sim.Pillars) CollectionAssert.Contains(sim.Walls, cover.Bounds);
        }
    }
}
