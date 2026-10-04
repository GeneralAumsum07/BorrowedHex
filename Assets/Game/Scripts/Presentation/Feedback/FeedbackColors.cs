using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// The one home of the feedback colours (spec 3.2, 4.1). State colours are the values
    /// ArenaView already used, so the halo players learned keeps meaning the same thing;
    /// ArenaView now aliases these instead of keeping its own copies.
    /// School colours are a first pass (spec 9): each must read apart from the others on the
    /// painted floor, and from the state colours, because a hit spark shows both at once.
    /// </summary>
    public static class FeedbackColors
    {
        public static readonly Color Hostile = new Color(1f, 0.38f, 0.28f);
        public static readonly Color Returned = new Color(0.45f, 0.95f, 1f);
        public static readonly Color Riposte = new Color(1f, 0.85f, 0.35f);
        public static readonly Color Rocket = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Overcharge = new Color(1f, 0.84f, 0.2f);
        public static readonly Color Danger = new Color(1f, 0.3f, 0.3f);
        public static readonly Color Fusion = new Color(0.75f, 0.45f, 1f);
        public static readonly Color Chain = new Color(1f, 0.6f, 0.2f);
        public static readonly Color Smoke = new Color(0.75f, 0.75f, 0.8f);
        public static readonly Color LifeSteal = new Color(1f, 0.35f, 0.4f);

        public static Color School(ActorCategory school)
        {
            switch (school)
            {
                case ActorCategory.Acolyte: return new Color(0.85f, 0.5f, 1f);        // violet missiles
                case ActorCategory.ScatterCaster: return new Color(0.6f, 0.85f, 1f);  // ice
                case ActorCategory.SiegeFamiliar: return Rocket;                      // fire
                case ActorCategory.Boss: return new Color(0.55f, 1f, 0.6f);           // the Collector's green
                default: return Returned;                                             // the player's own
            }
        }
    }
}
