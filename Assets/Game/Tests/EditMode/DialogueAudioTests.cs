using System;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation.Audio;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 5: the generated dialogue tick (its exact spec) and the silent combat
    /// reactions' once-per-run and shared-cooldown rules. Throttling, whitespace and instant
    /// completion are the Typewriter's (TypewriterTests); here is what is left.
    /// </summary>
    public class DialogueAudioTests
    {
        [Test]
        public void TheTickIsA25msSoft640HzSineAt22050()
        {
            var s = DialogueTextAudio.TickSamples();
            Assert.That(DialogueTextAudio.SampleRate, Is.EqualTo(22050));
            Assert.That(s.Length, Is.EqualTo((int)Math.Round(22050 * 0.025)));
            float peak = 0f;
            foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
            Assert.That(peak, Is.EqualTo(0.08f).Within(0.002f), "0.08 peak");
            // 5 ms ramps: silent at both ends, never a click.
            Assert.That(Math.Abs(s[0]), Is.LessThan(1e-4f));
            Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(0.002f));
            int ramp = (int)(22050 * 0.005);
            for (int i = 0; i < ramp / 2; i++) Assert.That(Math.Abs(s[i]), Is.LessThan(0.08f * 0.6f), $"attack too fast at {i}");
            // 640 Hz over 25 ms: 16 cycles, so 32 sign changes give or take one at the edges.
            int crossings = 0;
            for (int i = 1; i < s.Length; i++) if ((s[i - 1] < 0) != (s[i] < 0) && s[i] != 0 && s[i - 1] != 0) crossings++;
            Assert.That(crossings, Is.InRange(30, 33));
        }

        [Test]
        public void EachReactionShowsAtMostOncePerRun()
        {
            var r = new CombatReactions();
            Assert.That(r.Offer(CombatReaction.FirstCapture, 0f, canShow: true), Is.True);
            Assert.That(r.Offer(CombatReaction.FirstCapture, 100f, canShow: true), Is.False, "the first capture only");
        }

        [Test]
        public void ReactionsShareATwentySecondCooldown()
        {
            var r = new CombatReactions();
            Assert.That(r.Offer(CombatReaction.FirstCapture, 10f, true), Is.True);
            Assert.That(r.Offer(CombatReaction.FirstBackfire, 29f, true), Is.False, "inside the shared cooldown");
            // Discarded, not queued: the first backfire has happened, so it is gone for the run.
            Assert.That(r.Offer(CombatReaction.FirstBackfire, 40f, true), Is.False);
            Assert.That(r.Offer(CombatReaction.FirstPaidUpgrade, 30f, true), Is.True, "20 s later");
        }

        [Test]
        public void AReactionThatCannotAppearPromptlyIsDiscarded()
        {
            var r = new CombatReactions();
            Assert.That(r.Offer(CombatReaction.FirstPaidUpgrade, 0f, canShow: false), Is.False, "a story is blocking");
            Assert.That(r.Offer(CombatReaction.FirstPaidUpgrade, 1f, canShow: true), Is.False, "not shown later");
            Assert.That(r.Offer(CombatReaction.FirstCapture, 1f, canShow: true), Is.True, "a discard starts no cooldown");
        }

        [Test]
        public void TheLinesAreTheScripts()
        {
            Assert.That(CombatReactions.Line(CombatReaction.FirstCapture), Is.EqualTo("Was that meant for me?"));
            Assert.That(CombatReactions.Line(CombatReaction.FirstBackfire), Is.EqualTo("Held on too long."));
            Assert.That(CombatReactions.Line(CombatReaction.FirstPaidUpgrade), Is.EqualTo("I'll need less time if this works."));
            Assert.That(NarrativeCatalog.RecallCaption, Is.EqualTo("The unfinished renewal recalls you. Another advance waits to be stolen."));
        }
    }
}
