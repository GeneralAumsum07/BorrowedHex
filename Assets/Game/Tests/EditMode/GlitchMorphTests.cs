using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Reality-glitch morph (owner-approved design, 2026-10-04): a patch field the sim owns,
    // kill-driven in short runs, timed elsewhere, with collision gated by each patch.
    public class GlitchMorphTests
    {
        static readonly Rect Footprint = new Rect(-24, -18, 48, 36);

        static void Change(ArenaSim sim, int stage)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ArenaSim).GetMethod("SelectWorldArena", flags).Invoke(sim, new object[] { stage, false });
        }

        static ArenaSim WorldSandbox()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = 7, Sandbox = true, WorldArenas = true });
            sim.AutoSpawn = false;
            return sim;
        }

        [Test]
        public void SeedsStayInsideTheirCellsSoTheShadersThreeByThreeSearchIsExact()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var field = new MorphField(Footprint, new SeededRandom(seed));
                for (int i = 0; i < MorphField.Count; i++)
                {
                    var cell = (field.Seed(i) - field.Origin) / field.Cell;
                    Assert.That(Mathf.FloorToInt(cell.x), Is.EqualTo(i % MorphField.Columns));
                    Assert.That(Mathf.FloorToInt(cell.y), Is.EqualTo(i / MorphField.Columns));
                }
                // The shader only looks at the 3x3 cells around a point; the C# answer (every
                // seed) must agree everywhere, or cover would turn over under the wrong pixels.
                for (float x = Footprint.xMin; x <= Footprint.xMax; x += .7f)
                    for (float y = Footprint.yMin; y <= Footprint.yMax; y += .7f)
                    {
                        var p = new Vector2(x, y);
                        int cx = Mathf.Clamp(Mathf.FloorToInt((x - field.Origin.x) / field.Cell.x), 0, MorphField.Columns - 1);
                        int cy = Mathf.Clamp(Mathf.FloorToInt((y - field.Origin.y) / field.Cell.y), 0, MorphField.Rows - 1);
                        int best = -1; float bestDistance = float.MaxValue;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= MorphField.Columns || ny >= MorphField.Rows) continue;
                            int i = ny * MorphField.Columns + nx;
                            float d = (field.Seed(i) - p).sqrMagnitude;
                            if (d < bestDistance) { bestDistance = d; best = i; }
                        }
                        Assert.That((field.Seed(best) - p).sqrMagnitude, Is.EqualTo((field.Seed(field.PatchAt(p)) - p).sqrMagnitude).Within(1e-4f), $"seed {seed} at {p}");
                    }
            }
        }

        [Test]
        public void KillsTurnOverEightyPercentByTheLastKillAndTheFirstKillCracksAtLeastOnePatch()
        {
            Assert.That(MorphField.KillTarget(0, 20, ArenaSim.KillShare), Is.Zero);
            Assert.That(MorphField.KillTarget(1, 40, ArenaSim.KillShare), Is.EqualTo(1));
            Assert.That(MorphField.KillTarget(20, 0, ArenaSim.KillShare), Is.EqualTo(Mathf.FloorToInt(.8f * MorphField.Count)));
            int last = 0;
            for (int kills = 0; kills <= 30; kills++)
            {
                int target = MorphField.KillTarget(kills, 30 - kills, ArenaSim.KillShare);
                Assert.That(target, Is.GreaterThanOrEqualTo(last)); last = target;
            }
            Assert.That(MorphField.TimedTarget(19, 0, ArenaSim.TailSeconds), Is.EqualTo(19));
            Assert.That(MorphField.TimedTarget(19, ArenaSim.TailSeconds, ArenaSim.TailSeconds), Is.EqualTo(MorphField.Count));
            Assert.That(MorphField.TimedTarget(0, 99, ArenaSim.TimedSeconds), Is.EqualTo(MorphField.Count));
        }

        [Test]
        public void OneLongTickStillSpacesPatchTriggersApart()
        {
            var sim = WorldSandbox();
            Change(sim, 1);
            var field = sim.Morph;
            sim.Tick(default, 3);
            var starts = new List<double>();
            for (int i = 0; i < MorphField.Count; i++) if (field.IsTriggered(i)) starts.Add(field.StartedAt(i));
            starts.Sort();
            Assert.That(starts.Count, Is.GreaterThan(4));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.GreaterThanOrEqualTo(ArenaSim.TriggerGap - 1e-9));
        }

        [Test]
        public void CoverLeavesAndJoinsCollisionOnlyWhenItsOwnPatchHasTurnedOver()
        {
            var sim = WorldSandbox();
            sim.Player.Position = new Vector2(-23, -17); // a corner, clear of every footprint
            Change(sim, 1);
            var field = sim.Morph;
            Assert.That(sim.RetiringCover.Count, Is.GreaterThan(0));
            var retiring = sim.RetiringCover.ToArray();
            int formedSeen = 0;
            for (int step = 0; step < 60 * 9 && sim.Morph != null; step++)
            {
                sim.Tick(default, 1f / 60);
                double now = sim.Clock.Now;
                for (int i = 0; i < retiring.Length; i++)
                    if (!field.Complete(sim.RetiringPatch(i), now) && !retiring[i].Crumbled)
                        CollectionAssert.Contains(sim.Walls, retiring[i].Bounds, "Old cover stays solid until its patch turns.");
                for (int i = 0; i < sim.Pillars.Count; i++)
                {
                    if (!sim.CoverFormed(i)) { CollectionAssert.DoesNotContain(sim.Walls, sim.Pillars[i].Bounds); continue; }
                    Assert.That(field.Complete(sim.CoverPatch(i), now), Is.True, "New cover never collides ahead of its patch.");
                    formedSeen++;
                }
            }
            Assert.That(sim.Morph, Is.Null, "A timed morph finishes and releases its field.");
            Assert.That(formedSeen, Is.GreaterThan(0));
            foreach (var old in retiring) { Assert.That(old.Crumbled, Is.True); CollectionAssert.DoesNotContain(sim.Walls, old.Bounds); }
        }

        [Test]
        public void ShortRunKillsDriveTheMorphAndItFinishesEarlyInTheNextEncounterWithoutRebuildingCover()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = 3, Mode = GameMode.Short, WorldArenas = true });
            P5.Invulnerable(sim);
            Assert.That(sim.ArenaStage, Is.Zero);
            P5.ClearEncounter(sim);
            Assert.That(sim.State, Is.EqualTo(RunState.UpgradeChoice));
            Assert.That(sim.ArenaStage, Is.EqualTo(1), "The first kill starts the morph into the next arena.");
            Assert.That(sim.Morph, Is.Not.Null, "The tail belongs to the next encounter.");
            // Up to the kill share; a burst of last kills can leave some owed, and those simply
            // join the timed tail (triggers stay TriggerGap apart, never several in one frame).
            Assert.That(sim.Morph.Triggered, Is.InRange(1, Mathf.FloorToInt(ArenaSim.KillShare * MorphField.Count)));
            // Wear one formed piece out by hand: crossing the encounter edge must not rebuild it.
            int broken = sim.Pillars.FindIndex(p => !p.Crumbled && sim.Walls.Contains(p.Bounds));
            Assert.That(broken, Is.GreaterThanOrEqualTo(0));
            sim.Pillars[broken].Advance(1e9); sim.Walls.Remove(sim.Pillars[broken].Bounds);
            Assert.That(sim.ContinueFromUpgrade(), Is.True);
            Assert.That(sim.Pillars[broken].Crumbled, Is.True);
            CollectionAssert.DoesNotContain(sim.Walls, sim.Pillars[broken].Bounds);
            double start = sim.Clock.Now;
            while (sim.Morph != null && sim.Clock.Now - start < 30) sim.Tick(P5.Still, P5.Dt);
            Assert.That(sim.Clock.Now - start, Is.LessThan(ArenaSim.TailSeconds + MorphField.Duration + MorphField.Settle + .5f),
                "The morph completes a few seconds into the next encounter.");
            Assert.That(sim.ArenaStage, Is.EqualTo(1));
            Assert.That(sim.Pillars[broken].Crumbled, Is.True);
        }

        [Test]
        public void SanctumPillarsNeverWearDown()
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = 4, Mode = GameMode.Short, WorldArenas = true });
            P5.ToBossCombat(sim);
            Assert.That(sim.ArenaStage, Is.EqualTo(3));
            P5.Run(sim, 60 * 120);
            foreach (var pillar in sim.Pillars)
            {
                Assert.That(pillar.Crumbled, Is.False);
                CollectionAssert.Contains(sim.Walls, pillar.Bounds);
                Assert.That(sim.CoverIntegrity(pillar), Is.EqualTo(1));
            }
        }

        // Every glitch output must be continuous in time: a jump between frames is exactly
        // the "cheap" pop the owner rejected. Sampled at 1 ms across a whole patch timeline.
        static void AssertContinuous(System.Func<double, GlitchState> at, string what)
        {
            var previous = at(-.5);
            for (double t = -.5 + .001; t < 4; t += .001)
            {
                var state = at(t);
                Assert.That(Mathf.Abs(state.Fill - previous.Fill), Is.LessThan(.01f), $"{what} fill at {t:F3}");
                Assert.That(Mathf.Abs(state.Tear - previous.Tear), Is.LessThan(.01f), $"{what} tear at {t:F3}");
                Assert.That(Mathf.Abs(state.Ghost - previous.Ghost), Is.LessThan(.01f), $"{what} ghost at {t:F3}");
                previous = state;
            }
        }

        [Test]
        public void GlitchStatesNeverJumpBetweenFrames()
        {
            var field = new MorphField(Footprint, new SeededRandom(1));
            field.Trigger(5, 0);
            AssertContinuous(t => GlitchMorphPolicy.Forming(field, 5, t), "dressing");
            AssertContinuous(t => GlitchMorphPolicy.Retiring(field, 5, t), "retiring");
            AssertContinuous(t => GlitchMorphPolicy.FormingCover(field, 5, false, false, 0, t), "blocked cover");
            // Cover freed during, and after, its fade to the ghost.
            foreach (double freed in new[] { MorphField.Duration + .1, MorphField.Duration + 1 })
                AssertContinuous(t => t < freed ? GlitchMorphPolicy.FormingCover(field, 5, false, false, 0, t)
                    : GlitchMorphPolicy.FormingCover(field, 5, true, true, freed, t), $"late cover freed at {freed}");
            Assert.That(GlitchMorphPolicy.Forming(field, 5, 9).Fill, Is.EqualTo(1));
            Assert.That(GlitchMorphPolicy.Retiring(field, 5, 9).Fill, Is.Zero);
            Assert.That(GlitchMorphPolicy.FormingCover(field, 5, false, false, 0, MorphField.Duration - .01).Fill, Is.LessThan(1),
                "Unformed cover never looks solid ahead of its collision.");
        }
    }
}
