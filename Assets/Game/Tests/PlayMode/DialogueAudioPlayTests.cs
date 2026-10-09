using System.Collections;
using System.Reflection;
using BorrowedHex.Core;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 5 through a real GameRoot: the dialogue tick follows the saved volume and
    /// stops under a pause without replaying what it missed; the silent combat captions appear
    /// once, hide under a story, and respect the setting; the recall line sits on death results
    /// without delaying a restart. The tick's spec and the reaction rules are DialogueAudioTests'.
    /// </summary>
    public class DialogueAudioPlayTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject rootObject, cameraObject;
        GameRoot root;

        // A long dialogue line, so typing outlasts every wait below.
        static readonly NarrativeScene Line = new NarrativeScene("test_line", "Test",
            new NarrativePage(PageKind.Dialogue, Speaker.Rogue, null,
                "Every letter of this deliberately long line gives the typewriter something more to tick about."));

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameRoot.StorageOverride = new MemoryProfileStorage();
            GameRoot.IgnoreFocus = true;
            cameraObject = new GameObject("AudioTestCamera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            rootObject = new GameObject("AudioTestRoot");
            root = rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(rootObject);
            Object.Destroy(cameraObject);
            GameRoot.StorageOverride = null;
            GameRoot.IgnoreFocus = false;
            yield return null;
        }

        NarrativeSettings Settings => root.Profile.Profile.narrative.settings;

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>A short run past its prologue, in live combat.</summary>
        IEnumerator IntoCombat()
        {
            root.PlayShort();
            yield return null;
            // The prologue, unless an earlier run in this test made it familiar (auto-skipped).
            if (root.Story.IsPlaying) root.Story.Skip();
            for (int i = 0; i < 600 && root.Sim.State != RunState.Combat; i++) yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.Combat));
        }

        void React(CombatReaction r) =>
            typeof(GameRoot).GetMethod("React", Private).Invoke(root, new object[] { r });

        [UnityTest]
        public IEnumerator DialogueTicksAtTheSavedVolumeAndVolumeZeroIsSilent()
        {
            root.Story.Play(Line, _ => { });
            yield return Wait(0.6f);
            int played = root.TickAudio.Played;
            Assert.That(played, Is.GreaterThan(0), "dialogue ticks");
            // Throttled: never more than one per 0.06 s of typing (0.6 s here, after the fade).
            Assert.That(played, Is.LessThanOrEqualTo(Mathf.CeilToInt(0.6f / Typewriter.TickGap) + 1));
            root.Story.Cancel();

            Settings.dialogueVolume = 0f;
            root.Story.Play(Line, _ => { });
            yield return Wait(0.6f);
            Assert.That(root.TickAudio.Played, Is.EqualTo(played), "muted: no tick at all");
            Assert.That(root.Story.RevealedElements, Is.GreaterThan(0), "and the story types on regardless");
            root.Story.Cancel();
        }

        [UnityTest]
        public IEnumerator APauseStopsTheTickAndMissedTicksAreNotReplayed()
        {
            yield return IntoCombat();
            root.Story.Play(Line, _ => { });
            yield return Wait(0.4f);
            Assert.That(root.TickAudio.Played, Is.GreaterThan(0));

            root.SetMenuOpen(true);
            yield return null;
            int atPause = root.TickAudio.Played;
            int revealed = root.Story.RevealedElements;
            Assert.That(root.TickAudio.IsSounding, Is.False, "the sounding tick is cut");
            yield return Wait(0.5f);
            Assert.That(root.TickAudio.Played, Is.EqualTo(atPause), "nothing ticks while paused");
            Assert.That(root.Story.RevealedElements, Is.EqualTo(revealed));

            root.SetMenuOpen(false);
            yield return null;
            yield return null;
            // Resuming plays the next letters' ticks, not a burst of the 0.5 s missed.
            Assert.That(root.TickAudio.Played - atPause, Is.LessThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator ACombatReactionShowsOnceAndHidesUnderAStory()
        {
            yield return IntoCombat();
            React(CombatReaction.FirstCapture);
            yield return null;
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.True);
            Assert.That(root.ReactionCaption.text, Is.EqualTo("Was that meant for me?"));
            Assert.That(root.Sim.Clock.IsPaused, Is.False, "a reaction never pauses combat");

            // A story over it hides it; it comes back after.
            root.Story.Play(Line, _ => { });
            yield return null;
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.False);
            root.Story.Cancel();
            yield return null;
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.True);

            // And it leaves on its own after about three seconds.
            yield return Wait(3.3f);
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.False);
            React(CombatReaction.FirstCapture);
            yield return null;
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.False, "once per run");
        }

        [UnityTest]
        public IEnumerator ReactionsRespectTheSettingAndNewRunsStartFresh()
        {
            Settings.combatReactions = false;
            yield return IntoCombat();
            React(CombatReaction.FirstBackfire);
            yield return null;
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.False, "setting off");

            Settings.combatReactions = true;
            yield return IntoCombat();   // a new run: a new allowance
            React(CombatReaction.FirstBackfire);
            yield return null;
            Assert.That(root.ReactionCaption.text, Is.EqualTo("Held on too long."));
            Assert.That(root.ReactionCaption.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator DeathShowsTheRecallLineAndRestartsAtOnce()
        {
            yield return IntoCombat();
            typeof(ArenaSim).GetMethod("EndRun", Private).Invoke(root.Sim, new object[] { RunEndReason.Death });
            yield return null;
            yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.Results));
            Assert.That(root.Story.IsPlaying, Is.False, "no scene on a death");
            Assert.That(root.Flow.RecallLabel.gameObject.activeInHierarchy, Is.True);
            Assert.That(root.Flow.RecallLabel.text, Is.EqualTo(NarrativeCatalog.RecallCaption));

            var dead = root.Sim;
            Assert.That(root.HandleRestartKey(true), Is.True, "restart is immediate");
            Assert.That(root.Sim, Is.Not.SameAs(dead));
            Assert.That(root.Flow.Recall, Is.Null, "the next run's results start clean");
        }

        [UnityTest]
        public IEnumerator AnEndlessDeathHasNoRecallLine()
        {
            root.PlayEndless();
            yield return null;
            typeof(ArenaSim).GetMethod("EndRun", Private).Invoke(root.Sim, new object[] { RunEndReason.Death });
            yield return null;
            yield return null;
            Assert.That(root.Sim.State, Is.EqualTo(RunState.Results));
            Assert.That(root.Flow.RecallLabel.gameObject.activeSelf, Is.False, "the story is the short run's");
        }
    }
}
