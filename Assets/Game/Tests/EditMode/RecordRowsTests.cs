using System.Linq;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class RecordRowsTests
    {
        static RunRecord Rec() => new RunRecord { mode = "Short", styleId = "collector", kind = Records.BestScore, score = 4210,
            duration = 312f, kills = 57, reason = "Victory", seed = 99, buildVersion = "0.9", masteryLevel = 3,
            passives = { SkillTree.PrecisionAngle, "unknown_future_node" } };

        [Test]
        public void TheRowShowsModeStyleValueAndTimeOnly()
        {
            var r = RecordRows.Row(Rec());
            Assert.AreEqual("Short", r.Mode); Assert.AreEqual("Collector", r.Style);
            StringAssert.Contains("4210", r.Value); Assert.AreEqual("5:12", r.Duration);
            foreach (var s in new[] { r.Mode, r.Style, r.Value, r.Duration })
            { StringAssert.DoesNotContain("seed", s); StringAssert.DoesNotContain("0.9", s); }
        }

        [Test]
        public void DetailsUseSkillNamesAndKeepUnknownIds()
        {
            var d = RecordRows.Row(Rec()).Details;
            StringAssert.Contains("Wide Grasp", d);
            StringAssert.DoesNotContain(SkillTree.PrecisionAngle, d);
            StringAssert.Contains("unknown_future_node", d, "an id with no name is still shown, not dropped");
            StringAssert.Contains("99", d); StringAssert.Contains("0.9", d);
        }

        [Test]
        public void SurvivalRecordsShowTheTime()
        {
            var rec = Rec(); rec.kind = Records.LongestRun;
            StringAssert.Contains("5:12", RecordRows.Row(rec).Value);
        }

        [Test]
        public void FiltersPartitionTheAchievements()
        {
            var p = new PlayerProfile();
            p.achievements.Add(new AchievementEntry { id = Achievements.All[0].Id });
            int all = RecordRows.Achievements(p, AchievementFilter.All).Count;
            int got = RecordRows.Achievements(p, AchievementFilter.Earned).Count;
            int left = RecordRows.Achievements(p, AchievementFilter.Locked).Count;
            Assert.AreEqual(Achievements.All.Count, all);
            Assert.AreEqual(1, got); Assert.AreEqual(all - 1, left);
            Assert.AreEqual($"1 / {all}", RecordRows.Completion(p));
        }

        // An old save may hold a record with no style id: it predates styles, so it was a Snatcher run.
        [Test] public void ARecordWithNoStyleIsASnatcherRun() =>
            Assert.AreEqual("Snatcher", RecordRows.Row(new RunRecord { mode = "Short", styleId = null }).Style);

        [Test] public void HoursFormatWithAnHourField() => Assert.AreEqual("1:02:03", RecordRows.FormatTime(3723f));
    }
}
