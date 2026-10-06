using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Plan Task 12: the dev buttons fold into a drawer, grouped by their label so GameRoot's
    /// existing AddDevButton calls need no change. The auto-spawn caption is the live one
    /// ("Auto-spawn: ON" / "Auto-spawn: OFF"), so the group matches its prefix.
    /// </summary>
    public class PracticeDrawerTests
    {
        [TestCase("+ Formation", "Spawn")]
        [TestCase("+ Collector", "Spawn")]
        [TestCase("Clear arena", "Arena")]
        [TestCase("Auto-spawn: OFF", "Arena")]
        [TestCase("Auto-spawn: ON", "Arena")]
        [TestCase("Upgrade: none", "Run tools")]
        [TestCase("Endless (debug)", "Run tools")]
        [TestCase("Skip wave", "Run tools")]
        [TestCase("Reset", "Run tools")]
        public void ToolsLandInTheirGroup(string label, string group) => Assert.AreEqual(group, PracticeDrawer.Group(label));
    }
}
