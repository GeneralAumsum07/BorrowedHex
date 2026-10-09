using System.Collections;
using System.Linq;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 4 through a real GameRoot: discoveries reach the saved profile at story
    /// boundaries, an abandoned run keeps what it reached, the Story menu lists only unlocked
    /// scenes and replays them without a run or any state change, and excluded modes unlock nothing.
    /// </summary>
    public class StoryMenuTests
    {
        GameObject rootObject, cameraObject;
        GameRoot root;
        MemoryProfileStorage storage;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            storage = new MemoryProfileStorage();
            GameRoot.StorageOverride = storage;
            GameRoot.IgnoreFocus = true;
            cameraObject = new GameObject("StoryTestCamera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            rootObject = new GameObject("StoryTestRoot");
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
            Cheats.Invincible = false;
            yield return null;
        }

        /// <summary>The profile as the NEXT launch would read it.</summary>
        NarrativeProfileState Saved() => ProfileService.Load(storage).Profile.narrative;

        [UnityTest]
        public IEnumerator AnAbandonedRunKeepsWhatItReachedButNotAsSeen()
        {
            root.PlayShort();
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.True);
            root.ShowMainMenu();   // abandoned mid-prologue
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.False);
            Assert.That(Saved().unlocked, Does.Contain(NarrativeCatalog.Prologue), "reached is kept");
            Assert.That(Saved().seen, Is.Empty, "an interruption is not a viewing");
        }

        [UnityTest]
        public IEnumerator ASkippedSceneIsSavedAsSeen()
        {
            root.PlayShort();
            yield return null;
            root.Story.Skip();
            yield return null;
            Assert.That(Saved().seen, Does.Contain(NarrativeCatalog.Prologue));
        }

        [UnityTest]
        public IEnumerator TheStoryMenuListsOnlyUnlockedScenesAndReplaysWithoutARun()
        {
            Assert.That(root.Main.IsEntryEnabled("story"), Is.True);
            root.Main.Press("story");
            yield return null;
            Assert.That(root.StoryMenu.IsOpen, Is.True);
            Assert.That(root.StoryMenu.Listed, Is.Empty, "nothing discovered yet");

            root.Profile.Profile.narrative.Unlock(NarrativeCatalog.Prologue);
            root.Profile.Profile.narrative.Unlock(NarrativeCatalog.LastKindness);
            root.StoryMenu.Refresh();
            Assert.That(root.StoryMenu.Listed, Is.EqualTo(new[] { NarrativeCatalog.Prologue, NarrativeCatalog.LastKindness }));
            Assert.That(root.StoryMenu.Listed, Does.Not.Contain(NarrativeCatalog.OpenWindow), "no ending before victory");

            var before = root.Profile.Profile.narrative.Clone();
            int writes = storage.Writes;
            root.StoryMenu.Choose(NarrativeCatalog.LastKindness);
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.True);
            Assert.That(root.Story.CurrentPage.Text, Does.StartWith("Before he was The Collector"));
            Assert.That(root.InMainMenu, Is.True, "a replay is not a run");
            root.Story.Skip();
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.False);
            Assert.That(root.StoryMenu.IsOpen, Is.True, "back on the Story list");
            var after = root.Profile.Profile.narrative;
            Assert.That(after.seen, Is.EqualTo(before.seen), "a replay changes nothing");
            Assert.That(after.storyCompleted, Is.EqualTo(before.storyCompleted));
            Assert.That(storage.Writes, Is.EqualTo(writes), "and saves nothing");
        }

        [UnityTest]
        public IEnumerator ACheatedRunUnlocksNothing()
        {
            Cheats.Invincible = true;
            root.PlayShort();
            yield return null;
            Assert.That(root.Story.IsPlaying, Is.False, "debug runs carry no story");
            Assert.That(root.Profile.Profile.narrative.unlocked, Is.Empty);
        }

        /// <summary>
        /// Lore plan Task 6: the game looks for optional pictures (Resources/Narrative/&lt;key&gt;)
        /// and, with none shipped, every illustrated page falls back to black.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStoryLooksForOptionalPicturesAndFallsBackToBlack()
        {
            Assert.That(root.Story.Illustrations, Is.Not.Null, "the lookup is wired");
            Assert.That(root.Story.Illustrations("lore/torn_entry"), Is.Null, "no picture shipped");
            root.PlayShort();
            yield return null;
            Assert.That(root.Story.CurrentPage.IllustrationKey, Is.Not.Null, "the prologue asks for one");
            Assert.That(root.Story.Illustration.enabled, Is.False, "and gets black");
            Assert.That(root.Story.Background.color, Is.EqualTo(Color.black));
        }

        [UnityTest]
        public IEnumerator TheFourStorySettingsAreInSettingsAndSave()
        {
            root.Main.Press("settings");
            yield return null;
            var labels = root.Settings.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text).ToList();
            foreach (var name in new[] { "Skip familiar scenes", "Instant story text", "Dialogue sound", "Combat reactions" })
                Assert.That(labels, Does.Contain(name));
            var toggle = root.Settings.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .First(b => b.name == "Toggle" && b.transform.parent.name == "Row_Instant story text");
            toggle.onClick.Invoke();
            Assert.That(Saved().settings.instantText, Is.True, "saved on change");
        }
    }
}
