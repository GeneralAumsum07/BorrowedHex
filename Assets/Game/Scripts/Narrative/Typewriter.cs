using System;
using System.Globalization;

namespace BorrowedHex.Narrative
{
    /// <summary>What a press did to the page.</summary>
    public enum TypewriterPress { Ignored, RevealedRest, Advance }

    /// <summary>
    /// Lore plan Tasks 2 and 5: the typing rules for lore passages and dialogue lines, without
    /// any UI. NarrativePanel feeds it unscaled frame time and presses; it answers how much of
    /// the passage shows, whether this frame ticks, and what a press means.
    ///
    /// Units are TEXT ELEMENTS (StringInfo), not chars: a surrogate pair or a letter plus its
    /// combining accent appears whole, never as half a character.
    /// </summary>
    public sealed class Typewriter
    {
        public const float CharsPerSecond = 35f;
        public const float TickGap = 0.06f;
        /// <summary>
        /// Longest frame of typing accepted. A hitch, or a return from a pause, must not
        /// fast-forward the passage, and so cannot produce a burst of reveals or ticks.
        /// </summary>
        public const float MaxFrame = 0.1f;

        string text = "";
        int[] starts = Array.Empty<int>();   // char index where each text element begins
        float progress;                      // elements revealed, fractional while typing
        int revealed;
        bool ticks;
        float typingTime;                    // seconds of actual typing, for the tick gap
        float lastTickAt = float.NegativeInfinity;
        int lastPressFrame;

        public string Text => text;
        /// <summary>Text elements shown so far.</summary>
        public int Revealed => revealed;
        public int Elements => starts.Length;
        public bool Complete => revealed >= starts.Length;
        /// <summary>Chars shown so far: always a text-element boundary.</summary>
        public int VisibleChars => revealed >= starts.Length ? text.Length : starts[revealed];

        /// <summary>
        /// Start a page. <paramref name="frame"/> is the frame it opened on: a press in that same
        /// frame is the one that opened it (an upgrade click, the previous page's advance) and is ignored.
        /// </summary>
        public void Begin(string passage, int frame, bool tickSound, bool instant)
        {
            text = passage ?? "";
            starts = StringInfo.ParseCombiningCharacters(text);
            ticks = tickSound;
            progress = 0f;
            revealed = instant ? starts.Length : 0;
            typingTime = 0f;
            lastTickAt = float.NegativeInfinity;
            lastPressFrame = frame;
        }

        /// <summary>Advance typing by one (unpaused) frame. True when this frame should tick.</summary>
        public bool Tick(float dt)
        {
            if (Complete) return false;
            dt = Math.Max(0f, Math.Min(dt, MaxFrame));
            typingTime += dt;
            progress += dt * CharsPerSecond;
            int next = Math.Min(starts.Length, (int)Math.Floor(progress));
            if (next <= revealed) return false;
            int from = starts[revealed];
            int to = next >= starts.Length ? text.Length : starts[next];
            revealed = next;
            if (!ticks || typingTime - lastTickAt < TickGap) return false;
            // Only letters and digits tick: spaces and punctuation read as natural rests.
            for (int i = from; i < to; i++)
                if (char.IsLetterOrDigit(text, i))
                {
                    lastTickAt = typingTime;
                    return true;
                }
            return false;
        }

        /// <summary>
        /// A fresh advance press on <paramref name="frame"/>. While typing it reveals the rest
        /// (silently: no tick, no burst) and stays on the page; once complete it advances.
        /// At most one action per frame, and never on the frame the page opened.
        /// </summary>
        public TypewriterPress Press(int frame)
        {
            if (frame == lastPressFrame) return TypewriterPress.Ignored;
            lastPressFrame = frame;
            if (!Complete)
            {
                revealed = starts.Length;
                progress = revealed;
                return TypewriterPress.RevealedRest;
            }
            return TypewriterPress.Advance;
        }

        /// <summary>
        /// Show the whole passage now, silently, without spending a press: Instant story text
        /// switched on mid-page (from the pause menu) applies to the page already open.
        /// </summary>
        public void RevealAll()
        {
            revealed = starts.Length;
            progress = revealed;
        }

        /// <summary>
        /// Rich text for the label: the whole passage is always laid out, with the unrevealed
        /// suffix transparent, so wrapping and alignment never shift as letters appear.
        /// </summary>
        public string Markup()
        {
            int n = VisibleChars;
            return n >= text.Length ? text : text.Substring(0, n) + "<color=#00000000>" + text.Substring(n) + "</color>";
        }
    }
}
