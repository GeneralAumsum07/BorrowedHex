using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>Upgrade-choice copy (plan Task 15): the price in Life, said once, exactly.</summary>
    public class UpgradeCopyTests
    {
        [Test]
        public void ThePreviewSubtractsTheExactCost()
        {
            // A real price: the sim charges TakeCostFraction of CURRENT Life (ArenaSim.TakeCost),
            // so 10% of 142 s is 14.2 s, not 18 (the brief's figure assumed a cap-based price).
            var c = UpgradeCopy.LifePreview(142f, 180f, 14.2f);
            Assert.AreEqual(UpgradeCopy.Points(142f), c.Now);
            Assert.AreEqual(UpgradeCopy.Points(142f - 14.2f), c.After);
            Assert.AreEqual(c.Now - c.After, c.Cost, "the bar and the button never disagree");
            Assert.AreEqual(10, c.Percent, "the share of current Life, as the sim charges it");
            Assert.Less(c.FracAfter, c.FracNow);
        }

        [Test]
        public void ThePercentIsOfCurrentLifeNotOfTheCap()
        {
            // At half Life a 10% price is 10%, never 5%: the panel must say what the sim takes.
            Assert.AreEqual(10, UpgradeCopy.LifePreview(90f, 180f, 9f).Percent);
        }

        [Test] public void TheLastOpenSlotIsWarnedOnce() { Assert.AreEqual("Held 2/4", UpgradeCopy.HeldLine(2, 4)); StringAssert.Contains("last open slot", UpgradeCopy.HeldLine(3, 4)); }
        [Test] public void AFullSetSaysRankUpsOnly() => StringAssert.Contains("rank-ups only", UpgradeCopy.HeldLine(4, 4));
        [Test] public void ActionsKeepTheExactCostVisible()
        {
            Assert.AreEqual("Take  ·  18 Life", UpgradeCopy.Action(false, 0, 1, 18));
            Assert.AreEqual("Add  ·  18 Life", UpgradeCopy.Action(false, 2, 1, 18));
            Assert.AreEqual("Rank up to 2  ·  18 Life", UpgradeCopy.Action(true, 2, 2, 18));
        }
    }
}
