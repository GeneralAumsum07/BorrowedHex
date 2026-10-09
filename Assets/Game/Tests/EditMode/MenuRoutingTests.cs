using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The three-destination menu (plan Task 6) still answers to every Phase 8 key: GameRoot
    /// partials and the PlayMode tests press keys, never buttons, so each key needs a home.
    /// </summary>
    public class MenuRoutingTests
    {
        [TestCase("play_short", MenuSlot.Mode)]
        [TestCase("endless", MenuSlot.Mode)]
        [TestCase("mastery", MenuSlot.Character)]
        [TestCase("style", MenuSlot.Hidden)]
        [TestCase("records", MenuSlot.Records)]
        [TestCase("story", MenuSlot.Story)]
        [TestCase("tutorial", MenuSlot.Training)]
        [TestCase("practice", MenuSlot.Training)]
        [TestCase("settings", MenuSlot.Settings)]
        [TestCase("cheats", MenuSlot.Cheats)]
        [TestCase("quit", MenuSlot.Quit)]
        public void EveryLegacyKeyHasAHome(string key, MenuSlot slot) => Assert.AreEqual(slot, MenuRouting.SlotFor(key));

        [Test]
        public void UnknownKeysAreHiddenNotLost() => Assert.AreEqual(MenuSlot.Hidden, MenuRouting.SlotFor("future_mode"));

        [Test]
        public void ShortRunIsTheFirstMode() => Assert.AreEqual("play_short", MenuRouting.ModeKeys[0]);
    }
}
