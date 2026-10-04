using BorrowedHex.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Spec 4.2.4: at most 3 live; a repeat refreshes; rise ~.6, pop in over .08 s, gone by .6 s.
    public class CalloutTextTests
    {
        GameObject parent;
        CalloutText callouts;

        [SetUp] public void SetUp() { parent = new GameObject("CalloutTest"); callouts = new CalloutText(parent.transform); }
        [TearDown] public void TearDown() { callouts.Dispose(); Object.DestroyImmediate(parent); }

        [Test]
        public void NeverMoreThanThreeAndTheNewestAlwaysShows()
        {
            foreach (var word in new[] { "A!", "B!", "C!", "D!", "E!" }) callouts.Show(word, Color.white, Vector3.zero);
            Assert.AreEqual(3, callouts.LiveCount);
            CollectionAssert.Contains(callouts.LiveTexts, "E!", "the oldest gives way, never the newest");
            CollectionAssert.DoesNotContain(callouts.LiveTexts, "A!");
        }

        // Final review minor: two words at one moment printed on top of each other.
        [Test]
        public void TwoWordsAtOneSpotStackInsteadOfOverlapping()
        {
            callouts.Show("Parry!", Color.white, Vector3.zero);
            callouts.Show("Perfect!", Color.white, Vector3.zero);
            var o = callouts.LiveOrigins;
            Assert.GreaterOrEqual(Mathf.Abs(o[0].y - o[1].y), CalloutText.StackStep - 1e-4f);
            callouts.Show("Far away", Color.white, new Vector3(10f, 0f, 0f));
            Assert.AreEqual(o[0].y, callouts.LiveOrigins[2].y, 1e-4, "a word elsewhere keeps its own height");
        }

        [Test]
        public void ARepeatRefreshesInsteadOfStacking()
        {
            callouts.Show("Parry!", Color.yellow, Vector3.zero);
            callouts.Tick(0.5f, null);
            callouts.Show("Parry!", Color.yellow, Vector3.zero);
            Assert.AreEqual(1, callouts.LiveCount);
            callouts.Tick(0.3f, null);   // 0.8 s after the first; only 0.3 s after the refresh
            Assert.AreEqual(1, callouts.LiveCount, "the refresh restarted its life");
        }

        [Test]
        public void GoneByPointSix()
        {
            callouts.Show("Fusion!", Color.white, Vector3.zero);
            callouts.Tick(0.59f, null);
            Assert.AreEqual(1, callouts.LiveCount);
            callouts.Tick(0.02f, null);
            Assert.AreEqual(0, callouts.LiveCount);
        }

        [Test]
        public void PopsInThenRisesAndFades()
        {
            CalloutText.Pose(0f, out var rise0, out var scale0, out var alpha0);
            Assert.Greater(scale0, 1f, "starts big");
            Assert.AreEqual(0f, rise0, 1e-6); Assert.AreEqual(1f, alpha0, 1e-6);
            CalloutText.Pose(CalloutText.PopIn, out _, out var scalePop, out _);
            Assert.AreEqual(1f, scalePop, 1e-6, "settled by .08 s");
            CalloutText.Pose(CalloutText.Life, out var riseEnd, out _, out var alphaEnd);
            Assert.AreEqual(CalloutText.Rise, riseEnd, 1e-4);
            Assert.AreEqual(0f, alphaEnd, 1e-4);
        }
    }
}
