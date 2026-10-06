using BorrowedHex.Presentation.Feedback;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Spec 2's overcharge table as one pure rule (plan Task 14): selection is an ivory marker,
    /// overcharge is a gold border, and the two never stand in for each other.
    /// </summary>
    public class SlotCueTests
    {
        static SlotInput Packet(bool selected, bool over, double now = 10, double since = 10) => new SlotInput
        { HasPacket = true, Selected = selected, Primed = true, Overcharged = over, Now = now, OverchargeSince = since };

        [Test] public void OrdinarySelectionIsIvoryWithNoGold()
        {
            var c = SlotCue.Evaluate(Packet(true, false));
            Assert.IsTrue(c.Marker); Assert.AreEqual(UiPalette.Ivory, c.MarkerColor); Assert.IsFalse(c.Border);
        }

        [Test] public void AnEmptySelectedSlotStillShowsTheIvoryMarker()
        {
            var c = SlotCue.Evaluate(new SlotInput { Selected = true });
            Assert.IsTrue(c.Marker); Assert.AreEqual(UiPalette.Ivory, c.MarkerColor); Assert.IsFalse(c.Border);
        }

        [Test] public void SelectedOverchargeUsesTheSharedGoldExactlyAndStartsBright()
        {
            var c = SlotCue.Evaluate(Packet(true, true));
            Assert.IsTrue(c.Border);
            var g = FeedbackColors.Overcharge;
            Assert.AreEqual(g.r, c.BorderColor.r); Assert.AreEqual(g.g, c.BorderColor.g); Assert.AreEqual(g.b, c.BorderColor.b);
            Assert.AreEqual(1f, c.BorderColor.a, 1e-4);
            Assert.IsTrue(c.CountdownGold);
        }

        [Test] public void ThePulseRunsBetween45And100PercentAt2Hz()
        {
            Assert.AreEqual(SlotCue.PulseMin, SlotCue.Evaluate(Packet(true, true, 10.25, 10)).BorderColor.a, 1e-4, "trough a quarter second in");
            Assert.AreEqual(1f, SlotCue.Evaluate(Packet(true, true, 10.5, 10)).BorderColor.a, 1e-4, "peak again at half a second");
        }

        [Test] public void FrozenOverchargeIsSteadyAndSaysFrozen()
        {
            var c = SlotCue.Evaluate(Packet(false, true, 10.25, 10));
            Assert.IsTrue(c.Border); Assert.AreEqual(1f, c.BorderColor.a, 1e-4);
            Assert.IsFalse(c.Marker);
            CollectionAssert.Contains(c.States, "state.frozen");
            CollectionAssert.Contains(c.States, "state.overcharge");
        }

        [Test] public void ReduceFlashesHoldsTheBorderSteady()
        {
            var i = Packet(true, true, 10.25, 10); i.ReduceFlashes = true;
            Assert.AreEqual(1f, SlotCue.Evaluate(i).BorderColor.a, 1e-4);
        }

        [Test] public void HandFullTurnsTheMarkerRedButKeepsTheGoldBorder()
        {
            var i = Packet(true, true); i.HandFull = true;
            var c = SlotCue.Evaluate(i);
            Assert.AreEqual(SlotCue.RejectRed, c.MarkerColor); Assert.IsTrue(c.Border);
        }

        [Test] public void AnUnstablePacketNeverSaysFire()
        {
            var i = Packet(true, true); i.Primed = false;
            var c = SlotCue.Evaluate(i);
            CollectionAssert.Contains(c.States, "state.unstable");
            StringAssert.DoesNotContain("Fire", c.Label);
        }

        [TestCase(false, true)] [TestCase(false, false)]
        public void EmptyAndLockedSlotsHaveNoBorder(bool selected, bool locked)
        {
            var c = SlotCue.Evaluate(new SlotInput { Selected = selected, Locked = locked, Overcharged = true });
            Assert.IsFalse(c.Border, "a stale overcharge never outlives its packet");
            if (locked) CollectionAssert.Contains(c.States, "state.locked");
        }

        [Test] public void TwoChargedPacketsBothShowGoldButOnlyTheSelectedOnePulses()
        {
            var sel = SlotCue.Evaluate(Packet(true, true, 10.25, 10));
            var frozen = SlotCue.Evaluate(Packet(false, true, 10.25, 10));
            Assert.IsTrue(sel.Border && frozen.Border);
            Assert.Less(sel.BorderColor.a, 1f); Assert.AreEqual(1f, frozen.BorderColor.a, 1e-4);
        }

        [Test] public void TrackStartsBrightOnEntryAndOnANewPacket()
        {
            var clock = new PulseClock(); var a = new object(); var b = new object();
            Assert.AreEqual(5.0, SlotCue.Track(ref clock, a, true, 5.0));
            Assert.AreEqual(5.0, SlotCue.Track(ref clock, a, true, 7.0), "same packet keeps pulsing from its start");
            Assert.AreEqual(8.0, SlotCue.Track(ref clock, b, true, 8.0), "a swapped-in packet starts bright");
            SlotCue.Track(ref clock, b, false, 9.0);
            Assert.AreEqual(9.5, SlotCue.Track(ref clock, b, true, 9.5), "re-selecting a frozen overcharge starts bright");
        }

        [Test] public void APausedClockFreezesThePulse()
        {
            var x = SlotCue.Evaluate(Packet(true, true, 10.1, 10)).BorderColor.a;
            var y = SlotCue.Evaluate(Packet(true, true, 10.1, 10)).BorderColor.a;
            Assert.AreEqual(x, y, "same gameplay time, same frame of the pulse");
        }
    }
}
