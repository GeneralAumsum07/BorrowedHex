using BorrowedHex.Data;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The compact capture-style cards (plan Task 8): three aligned numbers per card so the
    /// styles compare at a glance, with the long text moved to Details.
    /// </summary>
    public class StyleCardTests
    {
        // Verbatim from StyleTests.Cfg, so both suites read the same tuning.
        static GameConfig Cfg => TestSims.Config;

        [Test]
        public void EveryCardComparesTheSameThreeThingsInTheSameOrder()
        {
            var p = new PlayerProfile();
            string[] first = null;
            foreach (var s in CaptureStyles.All)
            {
                var sum = StylePanel.CardSummary(s, p, Cfg);
                Assert.AreEqual(3, sum.Compare.Length, s.Id);
                var labels = System.Array.ConvertAll(sum.Compare, c => c.label);
                if (first == null) first = labels; else CollectionAssert.AreEqual(first, labels, "aligned rows");
                Assert.IsFalse(string.IsNullOrEmpty(sum.TradeOff));
            }
        }

        [Test]
        public void TheCardNoLongerSaysClickToSelect()
        {
            foreach (var s in CaptureStyles.All)
                StringAssert.DoesNotContain("Click to select", StylePanel.CardBody(s, new PlayerProfile(), Cfg, false));
        }

        [Test]
        public void CardValuesFollowOwnedSkills()
        {
            var p = new PlayerProfile();
            var before = StylePanel.CardSummary(CaptureStyles.Resolve(CaptureStyles.Collector), p, Cfg).Compare[0].value;
            p.ownedNodes.Add(SkillTree.PrecisionAngle);
            var after = StylePanel.CardSummary(CaptureStyles.Resolve(CaptureStyles.Collector), p, Cfg).Compare[0].value;
            Assert.AreNotEqual(before, after, "Wide Grasp widens the cone on the card");
        }
    }
}
