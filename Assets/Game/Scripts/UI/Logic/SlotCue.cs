using System.Collections.Generic;
using BorrowedHex.Presentation.Feedback;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>What one hex slot needs to know this frame; filled by PacketIndicator from the sim.</summary>
    public struct SlotInput
    {
        public bool HasPacket, Locked, Selected, Primed, Overcharged, Fused, HandFull, ReduceFlashes;
        /// <summary>Gameplay time (sim clock): it stops while paused, so the pulse freezes with it.</summary>
        public double Now, OverchargeSince;
    }

    /// <summary>Per-slot memory for <see cref="SlotCue.Track"/>: which packet pulsed, since when.</summary>
    public struct PulseClock { public object Packet; public double Since; public bool Was; }

    /// <summary>
    /// Spec 2, "Explicit overcharge feedback for both hex slots", as one pure rule. Selection and
    /// overcharge are separate channels: an ivory marker says "this is the hand you fire from",
    /// a gold border says "this one is charged". Hand-full only touches the marker, so a rejected
    /// catch can never hide that the selected packet is charged.
    /// </summary>
    public struct SlotCue
    {
        public const float PulseHz = 2f, PulseMin = 0.45f;
        // Kept apart from the overcharge gold and the ivory marker: red only ever means "refused".
        public static readonly Color RejectRed = new Color(1f, 0.32f, 0.3f);

        public bool Marker; public Color MarkerColor;
        public bool Border; public Color BorderColor;
        public bool CountdownGold;
        /// <summary>UiGlyphs ids, in reading order.</summary>
        public string[] States;
        /// <summary>The same states as words (plus "Hand full" / "Decaying", which have no glyph).</summary>
        public string Label;

        public static SlotCue Evaluate(in SlotInput i)
        {
            var c = new SlotCue { Marker = i.Selected, MarkerColor = i.HandFull ? RejectRed : UiPalette.Ivory };
            var states = new List<string>(4);
            var words = new List<string>(4);
            void Add(string glyph, string word) { states.Add(glyph); words.Add(word); }

            if (!i.HasPacket)
            {
                if (i.Locked) Add("state.locked", "Locked");
                c.States = states.ToArray(); c.Label = string.Join("  ", words);
                return c;   // no packet, no border: nothing stale survives a release or expiry
            }

            // In this game an unselected packet IS a frozen one: Q swaps which hand decays.
            bool frozen = !i.Selected;
            if (i.HandFull) words.Add("Hand full");
            // Unstable is never dropped for a louder state: it is the one that says "you can't fire
            // yet", and the label never says "Fire" at all (the gold border carries "now").
            if (!i.Primed) Add("state.unstable", "Unstable");
            if (frozen) Add("state.frozen", "Frozen");
            if (i.Overcharged) Add("state.overcharge", "Overcharge");
            if (i.Fused) Add("state.fused", "Fused");
            if (words.Count == 0) words.Add("Decaying");

            if (i.Overcharged)
            {
                c.Border = true; c.CountdownGold = true;
                float a = 1f;
                // Only the hand you fire from pulses; a frozen charge is banked, so it holds still.
                if (!frozen && !i.ReduceFlashes)
                {
                    // cos starts at its peak, so entry is bright; mid 0.725 +- 0.275 spans 0.45..1.
                    double t = i.Now - i.OverchargeSince;
                    a = (1f + PulseMin) / 2f + (1f - PulseMin) / 2f * Mathf.Cos((float)(2.0 * Mathf.PI * PulseHz * t));
                }
                // The shared feedback gold, not a local copy: the world cue and the slot must match.
                var g = FeedbackColors.Overcharge;
                c.BorderColor = new Color(g.r, g.g, g.b, a);
            }
            c.States = states.ToArray();
            c.Label = string.Join("  ", words);
            return c;
        }

        /// <summary>When the selected-overcharge pulse began. Resets on entry and on a new packet,
        /// so a swapped-in or re-selected charge always starts bright (spec).</summary>
        public static double Track(ref PulseClock clock, object packet, bool pulsing, double now)
        {
            if (pulsing && (!clock.Was || !ReferenceEquals(clock.Packet, packet))) clock.Since = now;
            clock.Was = pulsing; clock.Packet = packet;
            return clock.Since;
        }
    }
}
