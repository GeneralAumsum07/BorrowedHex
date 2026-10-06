using BorrowedHex.Core;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>Task 16: the results screen leads with outcome, XP and rewards; the rest is Details.</summary>
    public class ResultsCopyTests
    {
        static FinalizeResult Applied() => new FinalizeResult
        {
            Applied = true, Saved = true, Xp = new XpBreakdown { NormalKills = 10, Total = 46 },
            LevelBefore = 3, LevelAfter = 3,
        };

        [Test] public void XpComesFirstAndTheBreakdownGoesToDetails()
        {
            var o = ResultsCopy.FromFinalize(Applied(), new MasteryState { level = 3, xp = 120 }, null);
            Assert.AreEqual("+46 XP", o.XpLine);
            Assert.AreEqual(120f / Mastery.CostToAdvance(3), o.XpFrac, 1e-4);
            Assert.IsTrue(o.Details.Exists(d => d.Contains("kills")));
            Assert.IsNull(o.LevelUp);
        }

        [Test] public void ALevelUpIsAReward()
        {
            var r = Applied(); r.LevelAfter = 4; r.LevelsGained = 1;
            StringAssert.Contains("Mastery 3 → 4", ResultsCopy.FromFinalize(r, new MasteryState { level = 4 }, null).LevelUp);
        }

        [Test] public void NewBestsAndAchievementsAreRewardsAndUnchangedRecordsAreDetails()
        {
            var r = Applied();
            r.NewAchievements.Add(Achievements.All[0].Id);
            r.Records.Add(new RecordOutcome { Kind = Records.BestScore, IsNewBest = true, ThisValue = 900, Previous = new RunRecord { score = 700 } });
            r.Records.Add(new RecordOutcome { Kind = Records.LongestRun, IsNewBest = false, ThisValue = 200, Previous = new RunRecord { duration = 300 } });
            var o = ResultsCopy.FromFinalize(r, new MasteryState { level = 3 }, null);
            Assert.IsTrue(o.Rewards.Exists(x => x.Contains(Achievements.All[0].Name)));
            Assert.IsTrue(o.Rewards.Exists(x => x.Contains("New best score")));
            Assert.IsFalse(o.Rewards.Exists(x => x.Contains("Longest")), "unchanged comparisons are not rewards");
            Assert.IsTrue(o.Details.Exists(x => x.Contains("Longest run")));
        }

        [Test] public void SkippedRunsSayWhyOnce()
        {
            var o = ResultsCopy.FromFinalize(new FinalizeResult { Applied = false, SkippedBecause = "sandbox" }, new MasteryState(), null);
            StringAssert.Contains("not recorded", o.NotRecorded);
            Assert.IsNull(o.XpLine);
            // A replayed finalize says nothing new.
            Assert.IsNull(ResultsCopy.FromFinalize(new FinalizeResult { SkippedBecause = "already finalized" }, new MasteryState(), null).NotRecorded);
        }

        [Test] public void ASaveFailureIsItsOwnLine()
        {
            var r = Applied(); r.Saved = false;
            Assert.AreEqual("Not saved: disk full", ResultsCopy.FromFinalize(r, new MasteryState { level = 3 }, "disk full").Warning);
        }

        [Test] public void TheSubtitleNeverRepeatsTheTitle()
        {
            // "Defeated" then "You fell" was the spec's example of redundancy.
            Assert.IsNull(ResultsCopy.Subtitle(RunEndReason.Death, GameMode.Short, 1, 0, 0, 0));
            Assert.AreEqual("Time bonus +250", ResultsCopy.Subtitle(RunEndReason.Victory, GameMode.Short, 1, 0, 0, 250));
            Assert.AreEqual("Cycle 2  ·  8 waves  ·  1 boss", ResultsCopy.Subtitle(RunEndReason.Retired, GameMode.Endless, 2, 8, 1, 0));
        }

        [Test] public void TitlesAreSentenceCase() =>
            Assert.AreEqual("Out of time", ResultsCopy.Title(RunEndReason.TimeExpired).title);
    }
}
