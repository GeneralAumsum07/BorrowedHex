using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Narrative;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 1: the script catalog and the director that decides which scene plays
    /// when. Pure C#: no GameRoot, no UI, so every eligibility rule is checked headless.
    /// </summary>
    public class NarrativeTests
    {
        static readonly string[] Order = { "prologue", "last_kindness", "forgotten_answer", "anomaly", "confrontation", "open_window" };

        // ---- Catalog ------------------------------------------------------------------------

        [Test]
        public void CatalogHoldsTheSixScenesInNarrativeOrder()
        {
            Assert.That(NarrativeCatalog.Scenes.Select(s => s.Id), Is.EqualTo(Order));
            foreach (var id in Order) Assert.That(NarrativeCatalog.Get(id), Is.Not.Null, id);
        }

        [Test]
        public void EveryPassageFitsItsWordLimit()
        {
            // Plan section 1: one lore page holds at most 28 words, one dialogue line 18.
            foreach (var scene in NarrativeCatalog.Scenes)
                foreach (var page in scene.Pages)
                {
                    int words = NarrativeCatalog.WordCount(page.Text);
                    int max = page.Kind == PageKind.Dialogue ? NarrativeCatalog.MaxDialogueWords : NarrativeCatalog.MaxLoreWords;
                    Assert.That(words, Is.LessThanOrEqualTo(max), $"{scene.Id}: \"{page.Text}\"");
                }
        }

        [Test]
        public void SpeakersBelongToDialogueAndLabelsToWriting()
        {
            foreach (var scene in NarrativeCatalog.Scenes)
                foreach (var page in scene.Pages)
                {
                    bool spoken = page.Kind == PageKind.Dialogue;
                    Assert.That(page.Speaker != Speaker.None, Is.EqualTo(spoken), $"{scene.Id}: speaker on \"{page.Text}\"");
                    Assert.That(!string.IsNullOrEmpty(page.Label), Is.EqualTo(page.Kind == PageKind.Writing), $"{scene.Id}: label on \"{page.Text}\"");
                }
        }

        [Test]
        public void TheScenesUseThePlannedPresentation()
        {
            Assert.That(NarrativeCatalog.Get("confrontation").Pages.All(p => p.Kind == PageKind.Dialogue), Is.True);
            Assert.That(NarrativeCatalog.Get("anomaly").Pages.All(p => p.Kind == PageKind.Lore), Is.True);
            var answer = NarrativeCatalog.Get("forgotten_answer").Pages;
            Assert.That(answer[1].Label, Is.EqualTo("An unfiled request"));
            Assert.That(answer[2].Label, Is.EqualTo("Mara's correction"));
            // The ending returns to the arena for two spoken lines between its black screens.
            var ending = NarrativeCatalog.Get("open_window").Pages.Select(p => p.Kind).ToArray();
            Assert.That(ending.Count(k => k == PageKind.Dialogue), Is.EqualTo(2));
            Assert.That(ending.First(), Is.EqualTo(PageKind.Lore));
            Assert.That(ending.Last(), Is.EqualTo(PageKind.Lore));
            // "Enough" is crossed out on the last page of the last kindness.
            Assert.That(NarrativeCatalog.Get("last_kindness").Pages.Last().Emphasis, Is.EqualTo("Enough"));
        }

        // ---- Director -----------------------------------------------------------------------

        static NarrativeDirector Fresh(out NarrativeProfileState state, bool eligible = true, string run = "run-1")
        {
            state = new NarrativeProfileState();
            var d = new NarrativeDirector(state);
            d.BeginRun(run, eligible);
            return d;
        }

        /// <summary>Plays a delivery to the end with the given outcome; returns the delivered id or null.</summary>
        static string Play(NarrativeDirector d, NarrativeDelivery delivery, NarrativeEnd end = NarrativeEnd.Completed)
        {
            if (delivery == null) return null;
            d.Start(delivery);
            if (d.IsPlaying) d.Finish(end);
            return delivery.Scene.Id;
        }

        static List<string> PlayWholeRun(NarrativeDirector d)
        {
            var seen = new List<string>
            {
                Play(d, d.OnRunStarted()),
                Play(d, d.OnEncounterCleared(1)),
                Play(d, d.OnEncounterCleared(2)),
                Play(d, d.OnSanctumArrived()),
                Play(d, d.OnFinalUpgradeResolved()),
                Play(d, d.OnVictory()),
            };
            return seen;
        }

        [Test]
        public void AFirstSuccessfulRunReachesAllSixScenesInOrder()
        {
            var d = Fresh(out var state);
            Assert.That(PlayWholeRun(d), Is.EqualTo(Order));
            Assert.That(state.storyCompleted, Is.True);
            Assert.That(state.unlocked, Is.EquivalentTo(Order));
            Assert.That(state.seen, Is.EquivalentTo(Order));
        }

        [Test]
        public void TheThirdClearHasNoSceneOfItsOwn()
        {
            var d = Fresh(out _);
            Assert.That(d.OnEncounterCleared(3), Is.Null, "the third clear leads to the Sanctum, whose arrival carries the anomaly");
        }

        [Test]
        public void ARepeatedNotificationInOneRunDeliversOnce()
        {
            var d = Fresh(out _);
            Assert.That(Play(d, d.OnEncounterCleared(1)), Is.EqualTo("last_kindness"));
            // Pause/resume re-raises the same state change: no second delivery.
            Assert.That(d.OnEncounterCleared(1), Is.Null);
            // A new run is a new delivery.
            d.BeginRun("run-2", true);
            Assert.That(d.OnEncounterCleared(1), Is.Not.Null);
        }

        [Test]
        public void AFamiliarSceneIsSkippedAutomaticallyWhenTheSettingIsOn()
        {
            var d = Fresh(out var state);
            PlayWholeRun(d);
            d.BeginRun("run-2", true);
            NarrativeEnd? end = null;
            d.SceneFinished += (_, e) => end = e;
            var again = d.OnRunStarted();
            Assert.That(again, Is.Not.Null, "the trigger is still reported, so the flow knows to hand on");
            Assert.That(again.AutoSkip, Is.True);
            d.Start(again);
            Assert.That(d.IsPlaying, Is.False);
            Assert.That(end, Is.EqualTo(NarrativeEnd.AutoSkipped));

            state.settings.skipFamiliar = false;
            d.BeginRun("run-3", true);
            var shown = d.OnRunStarted();
            Assert.That(shown.AutoSkip, Is.False);
        }

        [Test]
        public void ExplicitSkipMarksSeenButCancellationDoesNot()
        {
            var d = Fresh(out var state);
            Play(d, d.OnRunStarted(), NarrativeEnd.Skipped);
            Assert.That(state.seen, Does.Contain("prologue"));

            Play(d, d.OnEncounterCleared(1), NarrativeEnd.Cancelled);
            Assert.That(state.unlocked, Does.Contain("last_kindness"), "reaching the trigger unlocks the scene");
            Assert.That(state.seen, Does.Not.Contain("last_kindness"), "a partially viewed scene is not seen");
        }

        [Test]
        public void VictoryCompletesTheStoryEvenWhenTheEndingIsSkipped()
        {
            var d = Fresh(out var state);
            Play(d, d.OnVictory(), NarrativeEnd.Skipped);
            Assert.That(state.storyCompleted, Is.True);
        }

        [Test]
        public void AnIneligibleRunDeliversAndUnlocksNothing()
        {
            var d = Fresh(out var state, eligible: false);
            Assert.That(PlayWholeRun(d).All(id => id == null), Is.True);
            Assert.That(state.unlocked, Is.Empty);
            Assert.That(state.storyCompleted, Is.False);
            Assert.That(d.HasPendingBossScene, Is.False, "practice and endless never wait for a story");
        }

        [Test]
        public void TheBossSceneStaysPendingUntilTheConfrontationEnds()
        {
            var d = Fresh(out _);
            Assert.That(d.HasPendingBossScene, Is.True);
            Play(d, d.OnSanctumArrived());
            Assert.That(d.HasPendingBossScene, Is.True, "the anomaly is not the confrontation");
            var confrontation = d.OnFinalUpgradeResolved();
            d.Start(confrontation);
            Assert.That(d.HasPendingBossScene && d.BlocksFlow, Is.True);
            d.Finish(NarrativeEnd.Skipped);
            Assert.That(d.HasPendingBossScene, Is.False);
            Assert.That(d.BlocksFlow, Is.False);
        }

        [Test]
        public void CancelEndsThePlayingSceneWithoutMarkingIt()
        {
            var d = Fresh(out var state);
            d.Start(d.OnRunStarted());
            Assert.That(d.IsPlaying, Is.True);
            d.Cancel();
            Assert.That(d.IsPlaying, Is.False);
            Assert.That(state.seen, Is.Empty);
        }

        [Test]
        public void ReplayListsOnlyUnlockedScenesInOrderAndChangesNothing()
        {
            var d = Fresh(out var state);
            Play(d, d.OnEncounterCleared(1));
            Play(d, d.OnRunStarted());
            Assert.That(d.Replayable().Select(s => s.Id), Is.EqualTo(new[] { "prologue", "last_kindness" }));
            Assert.That(d.Replay("open_window"), Is.Null, "the ending cannot be previewed before victory");

            var before = string.Join(",", state.seen) + "|" + state.storyCompleted;
            var replay = d.Replay("last_kindness");
            Assert.That(replay, Is.Not.Null);
            Assert.That(replay.AutoSkip, Is.False, "a replay is asked for: it always plays");
            d.Start(replay);
            d.Finish(NarrativeEnd.Completed);
            Assert.That(string.Join(",", state.seen) + "|" + state.storyCompleted, Is.EqualTo(before));
        }
    

        // ---- Task 4: persistence ---------------------------------------------------------

        [Test]
        public void ALegacySaveWithoutTheNarrativeGroupLoadsWithDefaults()
        {
            var json = Progression.ProfileService.ToJson(Progression.PlayerProfile.CreateDefault());
            // An older build's file: no "narrative" key at all.
            int at = json.IndexOf(",\"narrative\":", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "fixture: the profile now writes a narrative group");
            int end = json.IndexOf('}', json.IndexOf("\"settings\":", at, System.StringComparison.Ordinal)) + 1;
            end = json.IndexOf('}', end) + 1;   // closes the narrative object itself
            var legacy = json.Remove(at, end - at);
            Assert.That(legacy, Does.Not.Contain("narrative"));

            Assert.That(Progression.ProfileService.TryParse(legacy, out var p, out var why), Is.True, why);
            Assert.That(p.narrative, Is.Not.Null);
            Assert.That(p.narrative.unlocked, Is.Empty);
            Assert.That(p.narrative.seen, Is.Empty);
            Assert.That(p.narrative.storyCompleted, Is.False);
            Assert.That(p.narrative.settings.skipFamiliar, Is.True);
            Assert.That(p.narrative.settings.instantText, Is.False);
            Assert.That(p.narrative.settings.dialogueVolume, Is.EqualTo(0.35f));
            Assert.That(p.narrative.settings.combatReactions, Is.True);
            Assert.That(p.version, Is.EqualTo(Progression.PlayerProfile.CurrentVersion), "additive: no version bump");
        }

        [Test]
        public void DiscoveriesAndSettingsSurviveASaveAndLoad()
        {
            var p = Progression.PlayerProfile.CreateDefault();
            p.narrative.Unlock(NarrativeCatalog.Prologue);
            p.narrative.MarkSeen(NarrativeCatalog.Prologue);
            p.narrative.Unlock(NarrativeCatalog.LastKindness);
            p.narrative.settings.dialogueVolume = 0.6f;
            p.narrative.settings.instantText = true;
            Assert.That(Progression.ProfileService.TryParse(Progression.ProfileService.ToJson(p), out var back, out var why), Is.True, why);
            Assert.That(back.narrative.unlocked, Is.EqualTo(new[] { NarrativeCatalog.Prologue, NarrativeCatalog.LastKindness }));
            Assert.That(back.narrative.seen, Is.EqualTo(new[] { NarrativeCatalog.Prologue }));
            Assert.That(back.narrative.settings.dialogueVolume, Is.EqualTo(0.6f));
            Assert.That(back.narrative.settings.instantText, Is.True);
        }

        [Test]
        public void ADamagedNarrativeGroupRejectsTheSnapshot()
        {
            // Same rule as every other field (D73): out of range means damaged, use the backup.
            var p = Progression.PlayerProfile.CreateDefault();
            p.narrative.settings.dialogueVolume = 7f;
            Assert.That(Progression.ProfileService.TryParse(Progression.ProfileService.ToJson(p), out _, out _), Is.False);
            p = Progression.PlayerProfile.CreateDefault();
            p.narrative.unlocked.Add("not_a_scene");
            Assert.That(Progression.ProfileService.TryParse(Progression.ProfileService.ToJson(p), out _, out _), Is.False);
            p = Progression.PlayerProfile.CreateDefault();
            p.narrative.seen.Add(NarrativeCatalog.Prologue);   // seen but never unlocked
            Assert.That(Progression.ProfileService.TryParse(Progression.ProfileService.ToJson(p), out _, out _), Is.False);
        }
    }
}
