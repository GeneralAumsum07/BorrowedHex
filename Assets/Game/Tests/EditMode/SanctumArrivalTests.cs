using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 3: the short run's last transition is clear → Sanctum (arena + boss,
    /// prepared once) → SanctumArrival → final upgrades → BossIntro → fight. The final choice
    /// must never re-select the arena, respawn the boss or redraw the offers.
    /// </summary>
    public class SanctumArrivalTests
    {
        static ArenaSim World(bool hold, int seed = 3)
        {
            var sim = new ArenaSim(TestSims.Config, new RunSetup { Seed = seed, Mode = GameMode.Short, Sandbox = false, WorldArenas = true });
            sim.HoldSanctumArrival = hold;
            P5.Invulnerable(sim);
            return sim;
        }

        /// <summary>Clear encounters one and two (taking nothing), then the third.</summary>
        static void ClearAll(ArenaSim sim)
        {
            for (int i = 0; i < 2; i++) { P5.ClearEncounter(sim); sim.ContinueFromUpgrade(); }
            P5.ClearEncounter(sim);
        }

        [Test]
        public void TheThirdClearPreparesTheSanctumAndBossThenWaitsForArrival()
        {
            var sim = World(hold: true);
            var states = new List<RunState>();
            int bosses = 0;
            sim.Events.RunStateChanged += s => states.Add(s);
            sim.Events.EnemySpawned += e => { if (e.IsBoss) bosses++; };
            ClearAll(sim);

            Assert.That(sim.State, Is.EqualTo(RunState.SanctumArrival));
            Assert.That(sim.ArenaStage, Is.EqualTo(3), "the Sanctum is selected before the upgrades");
            Assert.That(sim.Boss, Is.Not.Null);
            Assert.That(bosses, Is.EqualTo(1));
            Assert.That(sim.Offers.Count, Is.GreaterThan(0), "offers are drawn at the clear, as before");
            Assert.That(sim.Clock.IsPaused, Is.True, "nothing moves while arriving");
            Assert.That(states.Last(), Is.EqualTo(RunState.SanctumArrival));
            Assert.That(states.Count(s => s == RunState.UpgradeChoice), Is.EqualTo(2), "the final choice is not open yet");
        }

        [Test]
        public void CompletingArrivalOpensTheSameOffersOnceAndTheChoiceStartsTheIntroWithoutRespawning()
        {
            var sim = World(hold: true);
            int bosses = 0, revisions;
            sim.Events.EnemySpawned += e => { if (e.IsBoss) bosses++; };
            ClearAll(sim);
            var offers = sim.Offers.Select(o => o.Id).ToList();
            var boss = sim.Boss;
            revisions = sim.ArenaRevision;

            Assert.That(sim.CompleteSanctumArrival(), Is.True);
            Assert.That(sim.CompleteSanctumArrival(), Is.False, "guarded: one arrival");
            Assert.That(sim.State, Is.EqualTo(RunState.UpgradeChoice));
            Assert.That(sim.Offers.Select(o => o.Id), Is.EqualTo(offers));

            Assert.That(sim.ChooseUpgrade(0), Is.True);
            Assert.That(sim.State, Is.EqualTo(RunState.BossIntro));
            Assert.That(sim.Encounter, Is.EqualTo(3), "the encounter counter advances once");
            Assert.That(sim.Boss, Is.SameAs(boss));
            Assert.That(bosses, Is.EqualTo(1), "no second boss");
            Assert.That(sim.ArenaRevision, Is.EqualTo(revisions), "no second arena selection, so no second pull");
            Assert.That(sim.CompleteBossIntro(), Is.True);
            Assert.That(sim.State, Is.EqualTo(RunState.BossCombat));
        }

        [Test]
        public void APauseDuringArrivalResumesOntoArrival()
        {
            var sim = World(hold: true);
            ClearAll(sim);
            sim.SetPause(PauseReason.Menu, true);
            Assert.That(sim.State, Is.EqualTo(RunState.Paused));
            Assert.That(sim.CompleteSanctumArrival(), Is.False, "never under the pause menu");
            sim.SetPause(PauseReason.Menu, false);
            Assert.That(sim.State, Is.EqualTo(RunState.SanctumArrival));
            for (int i = 0; i < 120; i++) sim.Tick(P5.Still, P5.Dt);
            Assert.That(sim.State, Is.EqualTo(RunState.SanctumArrival), "ticking never leaves arrival on its own");
        }

        [Test]
        public void WithoutAnArrivalObserverTheSimPassesStraightToTheChoice()
        {
            // Headless sims (tests, tools) have nobody to report the pull: arrival completes at once.
            var sim = World(hold: false);
            var states = new List<RunState>();
            sim.Events.RunStateChanged += s => states.Add(s);
            ClearAll(sim);
            Assert.That(sim.State, Is.EqualTo(RunState.UpgradeChoice));
            Assert.That(states.Skip(states.Count - 2), Is.EqualTo(new[] { RunState.SanctumArrival, RunState.UpgradeChoice }));
            Assert.That(sim.Boss, Is.Not.Null);
        }

        [Test]
        public void TheThirdEncounterStillCountsAsUntouchable()
        {
            var sim = World(hold: true);
            ClearAll(sim);
            Assert.That(sim.Score.UntouchableEncounters, Is.EqualTo(3));
        }
    }
}
