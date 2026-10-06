using System.Collections;
using System.Reflection;
using BorrowedHex.Core;
using BorrowedHex.Presentation;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore screens end to end through a real GameRoot: every story beat of a short run opens
    /// at its trigger, freezes the clock while it is up, and hands control back on Skip. The
    /// encounter clears and the victory are forced through the sim's own private flow methods
    /// (reflection), so the test exercises the real state changes without playing three fights.
    /// </summary>
    public class NarrativeFlowTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject rootObject, cameraObject, artObject;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameRoot.StorageOverride = new MemoryProfileStorage();
            GameRoot.IgnoreFocus = true;
            cameraObject = new GameObject("NarrativeTestCamera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            rootObject = new GameObject("NarrativeTestRoot");
            root = rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        // Re-enter UpgradeChoice the way TickRunFlow does after an encounter clear.
        void ForceUpgradeChoice(int transitions)
        {
            typeof(ArenaSim).GetProperty("TransitionsReached").SetValue(root.Sim, transitions);
            root.Sim.Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
            typeof(ArenaSim).GetMethod("SetState", Private).Invoke(root.Sim, new object[] { RunState.UpgradeChoice });
        }

        string Passage => root.Story.transform.Find("Passage").GetComponent<UnityEngine.UI.Text>().text;

        IEnumerator Frames(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        [UnityTest]
        public IEnumerator EveryStoryBeatPlaysFreezesTheRunAndHandsBack()
        {
            root.PlayShort();
            root.Sim.Player.InvulnerableUntil = double.MaxValue;
            yield return null;

            // Prologue: up before the first tick, clock frozen, text typing out.
            Assert.That(root.Story.IsPlaying, Is.True, "prologue did not open");
            float life = root.Sim.LifeSeconds;
            yield return Frames(1f);
            Assert.That(root.Sim.Clock.Now, Is.EqualTo(0).Within(1e-9), "clock ran under the prologue");
            Assert.That(root.Sim.LifeSeconds, Is.EqualTo(life), "life spent while reading");
            Assert.That(Passage.StartsWith("The Collector"), Is.True, $"text did not type out: {Passage}");
            Assert.That(Passage.Contains("<color=#00000000>"), Is.True, "35 chars/s should still be mid-passage after 1 s");
            root.Story.Skip();
            yield return Frames(0.5f);
            Assert.That(root.Story.IsPlaying, Is.False);
            Assert.That(root.Sim.Clock.Now, Is.GreaterThan(0), "run did not resume after the prologue");

            // Encounter 1 and 2 clears: scene over the upgrade choice, choice intact after.
            ForceUpgradeChoice(1);
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.True, "last kindness did not open");
            Assert.That(Passage, Does.Contain("Avel Sere").Or.Contain("<color"));
            root.Story.Skip();
            yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.UpgradeChoice));
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.Narrative), Is.False);
            root.Sim.ContinueFromUpgrade();

            ForceUpgradeChoice(2);
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.True, "forgotten answer did not open");
            root.Story.Skip();
            yield return null;
            root.Sim.ContinueFromUpgrade();

            // Third clear: no scene over the upgrades; the Sanctum scene holds the boss intro.
            ForceUpgradeChoice(3);
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.False, "nothing should play over the third choice");
            root.Sim.ContinueFromUpgrade();
            yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.BossIntro));
            Assert.That(root.Story.IsPlaying, Is.True, "anomaly/confrontation did not open");
            yield return Frames(3f);
            Assert.That(root.Sim.State, Is.EqualTo(RunState.BossIntro), "boss fight started under the story");
            root.Story.Skip();
            yield return Frames(3f);
            Assert.That(root.Sim.State, Is.EqualTo(RunState.BossCombat), "boss fight never started after the story");

            // Victory: ending holds the results; R cannot restart past it.
            typeof(ArenaSim).GetMethod("EndRun", Private).Invoke(root.Sim, new object[] { RunEndReason.Victory });
            yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.Results));
            Assert.That(root.Story.IsPlaying, Is.True, "ending did not open");
            Assert.That(root.HandleRestartKey(true), Is.False, "restart key skipped the ending");
            var sim = root.Sim;
            root.Story.Skip();
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.False);
            Assert.That(root.Sim, Is.SameAs(sim));
        }

        [UnityTest]
        public IEnumerator SanctumSceneWaitsForThePullThenStartsTheFight()
        {
            artObject = new GameObject("NarrativeWorldArt");
            artObject.AddComponent<WorldPresentation>().BindRoot(root);
            root.useWorldArenas = true;
            root.PlayShort();
            root.Sim.Player.InvulnerableUntil = double.MaxValue;
            yield return null;
            root.Story.Skip();
            yield return null;
            typeof(ArenaSim).GetMethod("BeginBossIntro", Private).Invoke(root.Sim, new object[] { 0 });

            // During the pull the story must not cover the travel; once it lands, it plays.
            bool sawTravel = false;
            float until = Time.unscaledTime + 12f;
            while (Time.unscaledTime < until && !root.Story.IsPlaying)
            {
                if (root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition)) sawTravel = true;
                yield return null;
            }
            Assert.That(sawTravel, Is.True, "no Sanctum pull happened");
            Assert.That(root.Story.IsPlaying, Is.True, "Sanctum scene never opened");
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.False, "scene opened over the pull");
            Assert.That(root.Sim.State, Is.EqualTo(RunState.BossIntro), "the pull started the fight past the story");
            root.Story.Skip();
            yield return Frames(3f);
            Assert.That(root.Sim.State, Is.EqualTo(RunState.BossCombat));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (artObject != null) Object.Destroy(artObject);
            Object.Destroy(rootObject);
            Object.Destroy(cameraObject);
            GameRoot.StorageOverride = null;
            GameRoot.IgnoreFocus = false;
            yield return null;
        }
    }
}
