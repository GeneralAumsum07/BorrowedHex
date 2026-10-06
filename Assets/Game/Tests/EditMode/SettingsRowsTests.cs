using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Settings as stepper rows (plan Task 7). A stepper clamps where the old cycle button
    /// wrapped: "›" at 130% jumping to 80% would surprise. Display mode is a closed list, so it
    /// still wraps.
    /// </summary>
    public class SettingsRowsTests
    {
        [Test] public void ScaleStepsUpAndDown() { Assert.AreEqual(1.15f, SettingsPanel.StepScale(1f, +1), 1e-4); Assert.AreEqual(0.9f, SettingsPanel.StepScale(1f, -1), 1e-4); }
        [Test] public void ScaleClampsAtTheEnds() { Assert.AreEqual(1.3f, SettingsPanel.StepScale(1.3f, +1), 1e-4); Assert.AreEqual(0.8f, SettingsPanel.StepScale(0.8f, -1), 1e-4); }
        // A hand-edited save can hold any value; one press lands on a real step.
        [Test] public void AnOffListScaleSnapsToTheNearestStep() => Assert.AreEqual(1.15f, SettingsPanel.StepScale(1.07f, +1), 1e-4);

        // Platform rule kept: windowed only where allowed (desktop). Order: as launched, fullscreen, windowed.
        [Test] public void DisplayCyclesThroughWhatThePlatformAllows()
        {
            Assert.AreEqual(1, SettingsPanel.StepDisplay(-1, +1, true));
            Assert.AreEqual(0, SettingsPanel.StepDisplay(1, +1, true));
            Assert.AreEqual(-1, SettingsPanel.StepDisplay(0, +1, true));
            Assert.AreEqual(-1, SettingsPanel.StepDisplay(1, +1, false), "no windowed on web");
            Assert.AreEqual(0, SettingsPanel.StepDisplay(-1, -1, true));
        }
    }
}
