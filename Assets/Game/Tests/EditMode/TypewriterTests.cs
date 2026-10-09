using BorrowedHex.Narrative;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 2/5: the typing rules shared by lore pages and dialogue lines, kept in a
    /// pure class so rate, fresh-press and tick behaviour are checked without a canvas.
    /// </summary>
    public class TypewriterTests
    {
        const float Frame = 1f / 60f;

        static Typewriter Typing(string text, bool ticks = true, bool instant = false, int frame = 0)
        {
            var t = new Typewriter();
            t.Begin(text, frame, ticks, instant);
            return t;
        }

        [Test]
        public void RevealsThirtyFiveTextElementsPerSecond()
        {
            var t = Typing(new string('a', 100));
            for (int i = 0; i < 60; i++) t.Tick(Frame);
            Assert.That(t.Revealed, Is.EqualTo(35).Within(1));
            Assert.That(t.Complete, Is.False);
        }

        [Test]
        public void ALongHitchCannotFastForwardThePassage()
        {
            var t = Typing(new string('a', 100));
            t.Tick(5f); // focus regained after five seconds away
            Assert.That(t.Revealed, Is.LessThanOrEqualTo(4), "at most one capped frame (0.1 s) of typing");
        }

        [Test]
        public void NeverRevealsHalfAUnicodeCharacter()
        {
            // "a" + a surrogate pair + a combining accent: three text elements, five chars.
            string text = "a\U0001F56Fé";
            var t = Typing(text);
            for (int i = 0; i < 200 && !t.Complete; i++)
            {
                t.Tick(0.01f);
                int cut = t.VisibleChars;
                Assert.That(cut == 0 || cut == text.Length || !char.IsHighSurrogate(text[cut - 1]), $"split a surrogate at {cut}");
                Assert.That(cut == text.Length || !char.IsLowSurrogate(text[cut]) , $"split before a low surrogate at {cut}");
                Assert.That(cut == text.Length || text[cut] != '́', $"split a combining mark at {cut}");
            }
            Assert.That(t.Complete, Is.True);
        }

        [Test]
        public void TheLayoutStaysFixedWithATransparentSuffix()
        {
            var t = Typing("Hello world");
            t.Tick(0.1f); // 3.5 elements → "Hel"
            Assert.That(t.Markup(), Is.EqualTo("Hel<color=#00000000>lo world</color>"));
            t.Press(1);
            Assert.That(t.Markup(), Is.EqualTo("Hello world"));
        }

        [Test]
        public void TheFirstPressRevealsTheRestAndASecondFreshPressAdvances()
        {
            var t = Typing("A passage still typing", frame: 10);
            t.Tick(Frame);
            Assert.That(t.Press(10), Is.EqualTo(TypewriterPress.Ignored), "the press that opened the page");
            Assert.That(t.Press(11), Is.EqualTo(TypewriterPress.RevealedRest));
            Assert.That(t.Complete, Is.True);
            Assert.That(t.Press(11), Is.EqualTo(TypewriterPress.Ignored), "one press is one action");
            Assert.That(t.Press(12), Is.EqualTo(TypewriterPress.Advance));
        }

        [Test]
        public void InstantTextShowsTheWholeLineAndStaysSilent()
        {
            var t = Typing("Every word at once", instant: true);
            Assert.That(t.Complete, Is.True);
            Assert.That(t.Tick(Frame), Is.False);
            Assert.That(t.Press(1), Is.EqualTo(TypewriterPress.Advance));
        }

        [Test]
        public void TicksOnLettersOnlyAndNoCloserThanTheGap()
        {
            var t = Typing(new string('a', 200));
            int ticks = 0;
            for (int i = 0; i < 60; i++) if (t.Tick(Frame)) ticks++;
            // The gap is a minimum: at most 1/0.06 ≈ 16 ticks a second, never one per letter (35).
            // Ticks land only on frames that reveal a letter, so the real rhythm is a little slower.
            Assert.That(ticks, Is.InRange(10, 17));

            var spaces = Typing("    ....    ,,,,");
            bool any = false;
            for (int i = 0; i < 60; i++) any |= spaces.Tick(Frame);
            Assert.That(any, Is.False, "spaces and punctuation are silent");
        }

        [Test]
        public void SilentPagesNeverTickAndRevealingTheRestIsSilent()
        {
            var lore = Typing(new string('a', 50), ticks: false);
            bool any = false;
            for (int i = 0; i < 60; i++) any |= lore.Tick(Frame);
            Assert.That(any, Is.False, "lore pages type silently");

            var line = Typing(new string('a', 50));
            line.Tick(Frame);
            line.Press(1);
            Assert.That(line.Tick(Frame), Is.False, "completing a line early emits no burst");
        }
    }
}
