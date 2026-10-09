using System.Collections.Generic;

namespace BorrowedHex.Narrative
{
    /// <summary>The three first-time moments the rogue remarks on (lore plan section G).</summary>
    public enum CombatReaction { FirstCapture, FirstBackfire, FirstPaidUpgrade }

    /// <summary>
    /// Lore plan Task 5: the rules for the silent combat captions, kept pure so they are tested
    /// without a scene. One instance per run (GameRoot builds a fresh one in BindNarrative).
    ///
    ///  - Each reaction is offered once per run: the FIRST capture is the moment, never a later one.
    ///  - All three share one 20 s cooldown, so two firsts close together never stack captions.
    ///  - A reaction that cannot appear now (a story is up, the cooldown is running) is DISCARDED,
    ///    not queued: the moment has passed, and a line arriving late would be about nothing.
    ///    A discard therefore consumes the reaction but starts no cooldown.
    /// </summary>
    public sealed class CombatReactions
    {
        public const float Cooldown = 20f;

        readonly HashSet<CombatReaction> offered = new HashSet<CombatReaction>();
        // Minus infinity: the first shown reaction is never inside a cooldown.
        float lastShownAt = float.NegativeInfinity;

        /// <summary>
        /// The moment <paramref name="r"/> happened at gameplay time <paramref name="now"/>; true
        /// when its caption should be shown now. <paramref name="canShow"/> is the caller's "could
        /// a caption appear promptly" (no blocking story, no menu, live play).
        /// </summary>
        public bool Offer(CombatReaction r, float now, bool canShow)
        {
            // Add first: whatever happens below, this reaction's moment has now occurred.
            if (!offered.Add(r)) return false;
            if (!canShow || now - lastShownAt < Cooldown) return false;
            lastShownAt = now;
            return true;
        }

        /// <summary>The script's lines, verbatim (plan section G).</summary>
        public static string Line(CombatReaction r) => r switch
        {
            CombatReaction.FirstCapture => "Was that meant for me?",
            CombatReaction.FirstBackfire => "Held on too long.",
            CombatReaction.FirstPaidUpgrade => "I'll need less time if this works.",
            _ => null,
        };
    }
}
