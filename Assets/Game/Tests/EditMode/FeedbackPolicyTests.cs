using System;
using System.Linq;
using BorrowedHex.Core;
using BorrowedHex.Presentation.Feedback;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    // The cue table and the tier rules (spec 4.1, 4.2). Pure: no Unity objects are created.
    public class FeedbackPolicyTests
    {
        static readonly CueEvent[] All = (CueEvent[])Enum.GetValues(typeof(CueEvent));
        // A context that makes every conditional cue fire (a chain long enough, a real radius).
        static CueContext Full => new CueContext { ChainLength = 5, Radius = 1.6f, Power = 1.8f, School = ActorCategory.Acolyte };

        [Test]
        public void TierZeroNeverShakesFreezesCallsOutOrFlashes()
        {
            var tierZero = new[] { CueEvent.EnemyHit, CueEvent.PierceHit, CueEvent.OrbitHit, CueEvent.PartingGift,
                CueEvent.Overflow, CueEvent.QuickDraw, CueEvent.LifeStolen, CueEvent.ShotExpired,
                CueEvent.ShotHitWall, CueEvent.ShotCaptured, CueEvent.Muzzle, CueEvent.DashReady };
            foreach (var e in tierZero) Assert.AreEqual(0, FeedbackPolicy.For(e, Full).Tier, e.ToString());
            foreach (var e in All)
            {
                var cue = FeedbackPolicy.For(e, Full);
                if (cue.Tier != 0) continue;
                Assert.AreEqual(0f, cue.ShakeAmp, e.ToString());
                Assert.AreEqual(0f, cue.HitStop, e.ToString());
                Assert.IsNull(cue.Callout, e.ToString());
                Assert.IsFalse(cue.ImpactFrame, e.ToString());
            }
        }

        [TestCase(CueEvent.Parry, "Parry!")]
        [TestCase(CueEvent.PerfectCatch, "Perfect!")]
        [TestCase(CueEvent.Backfire, "Backfire!")]
        [TestCase(CueEvent.Fusion, "Fusion!")]
        public void EachTierTwoMomentCallsOutItsOwnWord(CueEvent e, string word)
        {
            var cue = FeedbackPolicy.For(e, Full);
            Assert.AreEqual(2, cue.Tier);
            Assert.AreEqual(word, cue.Callout);
        }

        [Test]
        public void FinalSecondSaysLastSecondInsteadOfPerfect()
        {
            var c = Full; c.FinalSecond = true;
            Assert.AreEqual("Last Second!", FeedbackPolicy.For(CueEvent.PerfectCatch, c).Callout);
        }

        [Test]
        public void OverchargeKeepsTheMultiplierInItsCallout()
        {
            var c = Full; c.Power = 1.8f;
            Assert.AreEqual("Overcharge! x1.8", FeedbackPolicy.For(CueEvent.Overcharge, c).Callout);
        }

        [Test]
        public void ChainsShowFromThreeAndCallTheirLength()
        {
            var c = Full;
            c.ChainLength = 2;
            var two = FeedbackPolicy.For(CueEvent.Chain, c);
            Assert.IsNull(two.Callout); Assert.IsNull(two.Sheet);
            c.ChainLength = 3;
            Assert.AreEqual("Chain x3!", FeedbackPolicy.For(CueEvent.Chain, c).Callout);
        }

        [Test]
        public void HitStopExtendsToItsOwnLengthNeverSumsAndCaps()
        {
            double now = 10;
            Assert.AreEqual(now + .09, FeedbackPolicy.ExtendHitStop(now + .05, now, .09f), 1e-6, "extends to .09");
            Assert.AreEqual(now + .09, FeedbackPolicy.ExtendHitStop(now + .09, now, .05f), 1e-6, "a shorter request never shortens");
            Assert.AreEqual(now + .15, FeedbackPolicy.ExtendHitStop(0, now, .5f), 1e-6, "capped at .15");
            Assert.AreEqual(now + .15, FeedbackPolicy.ExtendHitStop(now + .15, now, .15f), 1e-6, "never sums past the cap");
        }

        [Test]
        public void ReduceFlashesDropsMotionButKeepsSpritesAndWords()
        {
            var cue = FeedbackPolicy.Filter(FeedbackPolicy.For(CueEvent.Parry, Full), true);
            Assert.AreEqual(0f, cue.ShakeAmp); Assert.AreEqual(0f, cue.HitStop); Assert.IsFalse(cue.ImpactFrame);
            Assert.AreEqual("Parry!", cue.Callout);
            Assert.AreEqual("Parry Flash", cue.Sheet);
            var kept = FeedbackPolicy.Filter(FeedbackPolicy.For(CueEvent.Parry, Full), false);
            Assert.Greater(kept.ShakeAmp, 0f); Assert.Greater(kept.HitStop, 0f); Assert.IsTrue(kept.ImpactFrame);
        }

        [Test]
        public void TheSpecsShakeAndFreezeValues()
        {
            void Check(CueEvent e, float amp, float dur, float stop)
            {
                var cue = FeedbackPolicy.For(e, Full);
                Assert.AreEqual(amp, cue.ShakeAmp, 1e-6, e + " amp");
                Assert.AreEqual(dur, cue.ShakeDuration, 1e-6, e + " dur");
                Assert.AreEqual(stop, cue.HitStop, 1e-6, e + " hit-stop");
            }
            Check(CueEvent.Kill, .05f, .10f, 0f);
            Check(CueEvent.PlayerHit, .12f, .20f, 0f);
            Check(CueEvent.Explosion, .10f, .20f, 0f);
            Check(CueEvent.BossHit, .06f, .10f, 0f);
            Check(CueEvent.Parry, .15f, .20f, .09f);
            Check(CueEvent.PerfectCatch, 0f, 0f, .05f);
            Check(CueEvent.Overcharge, .18f, .25f, .07f);
            Check(CueEvent.Backfire, .20f, .25f, 0f);
            Check(CueEvent.BossKill, .35f, .50f, .15f);
        }

        [Test]
        public void ImpactFramesOnlyOnParryPerfectOverchargeAndBossKill()
        {
            var expected = new[] { CueEvent.Parry, CueEvent.PerfectCatch, CueEvent.Overcharge, CueEvent.BossKill };
            var actual = All.Where(e => FeedbackPolicy.For(e, Full).ImpactFrame).ToArray();
            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void EveryCueNamesAnImportedSheet()
        {
            foreach (var e in All)
            {
                var cue = FeedbackPolicy.For(e, Full);
                if (cue.Sheet != null) CollectionAssert.Contains(FeedbackAssetTests.Sheets, cue.Sheet, e.ToString());
                if (cue.Sheet2 != null) CollectionAssert.Contains(FeedbackAssetTests.Sheets, cue.Sheet2, e.ToString());
            }
        }

        // Playtest: the slam read as an explosion. It is dust and dirt at its true reach, with
        // the heaviest Tier 1 shake, and never the Blast sheet or a freeze.
        [Test]
        public void TheSlamIsDustNotABlast()
        {
            var c = Full; c.Radius = 2.5f;
            var slam = FeedbackPolicy.For(CueEvent.BossSlam, c);
            Assert.AreEqual("Landing Dust", slam.Sheet);
            Assert.AreEqual(5f, slam.Size, 1e-5, "the dust reaches as far as the slam does");
            Assert.AreNotEqual("Blast", slam.Sheet2);
            Assert.AreEqual(1, slam.Tier);
            Assert.Zero(slam.HitStop);
            Assert.Greater(slam.ShakeAmp, FeedbackPolicy.For(CueEvent.Explosion, c).ShakeAmp, "the floor itself is hit");
        }

        [Test]
        public void HitSparksTakeTheSchoolsColour()
        {
            var c = Full; c.School = ActorCategory.ScatterCaster;
            Assert.AreEqual(FeedbackColors.School(ActorCategory.ScatterCaster), FeedbackPolicy.For(CueEvent.EnemyHit, c).Color);
            Assert.AreNotEqual(FeedbackColors.School(ActorCategory.Acolyte), FeedbackColors.School(ActorCategory.ScatterCaster));
        }
    }
}
