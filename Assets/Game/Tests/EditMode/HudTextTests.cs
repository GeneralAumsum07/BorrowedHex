using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>Plan Task 13: the HUD's words, short and in sentence case, tested without a sim.</summary>
    public class HudTextTests
    {
        static ObjectiveInfo Info(ObjectiveKind k) => new ObjectiveInfo
        { Kind = k, Encounter = 1, EncounterCount = 3, EnemiesLeft = 6, Wave = 1, WavesPerCycle = 6, Cycle = 1, WaveSecondsLeft = 44.2f, BossName = "The Collector" };

        [Test] public void EncounterLineIsShort() => Assert.AreEqual("Encounter 2/3  ·  6 remaining", HudText.Objective(Info(ObjectiveKind.Encounter)));
        [Test] public void OneRemainingIsSingular() { var i = Info(ObjectiveKind.Encounter); i.EnemiesLeft = 1; StringAssert.EndsWith("1 remaining", HudText.Objective(i)); }
        // The countdown rounds up, as the old EndlessObjective did: 44.2 s left shows 0:45.
        [Test] public void EndlessShowsWaveCycleAndCountdown() => Assert.AreEqual("Wave 1/6  ·  Cycle 1  ·  0:45", HudText.Objective(Info(ObjectiveKind.EndlessWave)));
        [Test] public void BossLinesNameTheBossOnce() => StringAssert.AreEqualIgnoringCase("Defeat The Collector", HudText.Objective(Info(ObjectiveKind.ShortBoss)));
        [Test] public void EndlessBossAddsTheCycle() => Assert.AreEqual("Defeat The Collector  ·  Cycle 1", HudText.Objective(Info(ObjectiveKind.EndlessBoss)));
        [Test] public void TheTutorialLineIsEmptyBecauseThePromptOwnsTheLessonNumber() => Assert.AreEqual("", HudText.Objective(Info(ObjectiveKind.Tutorial)));
        [Test] public void SandboxSaysPractice() => Assert.AreEqual("Practice", HudText.Objective(Info(ObjectiveKind.Sandbox)));
        [Test] public void ScoreShowsTheMultiplierOnlyAboveOne() { Assert.AreEqual("4210", HudText.Score(4210, 1f)); Assert.AreEqual("4210  x1.25", HudText.Score(4210, 1.25f)); }
        [Test] public void AChainOfOneIsNotAChain() { Assert.AreEqual("", HudText.Chain(1, 2f)); Assert.AreEqual("Chain x3  +2 next", HudText.Chain(3, 2f)); }
    }
}
