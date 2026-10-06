using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>Task 17: the compact tutorial prompt, the completion card and the boss name.</summary>
    public class TutorialCopyTests
    {
        [Test] public void ReduceFlashesKeepsThePromptSteady() =>
            Assert.AreEqual(TutorialPanel.PromptColour(0.5f, false), TutorialPanel.PromptColour(0f, true));

        [Test] public void APromptChangeFlashesWithoutTheSetting() =>
            Assert.AreNotEqual(TutorialPanel.PromptColour(0f, false), TutorialPanel.PromptColour(0.5f, false));

        [Test] public void TheReminderIsTheSpecSentence() =>
            Assert.AreEqual("Your life drains during runs. Defeat enemies to reclaim it.", TutorialPanel.CompletionReminder);

        [Test] public void TheBannerSaysTheNameOnly() =>
            Assert.AreEqual("The Collector", RunFlowPanels.BannerText("The Collector"));
    }
}
