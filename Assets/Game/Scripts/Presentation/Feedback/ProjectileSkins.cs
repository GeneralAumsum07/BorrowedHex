using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// How a shot looks (spec 3, D2). The SHAPE says who made it, and it survives capture and
    /// return, so a returned ice shard is still an ice shard. The COLOUR (halo and trail) says
    /// what it will do to you now. The halo stays the honest hitbox, drawn as a faint outline
    /// just outside it; the sprite is decoration on top.
    /// </summary>
    public static class ProjectileSkins
    {
        // Playtest: even at .3 a FILLED halo was too prominent, so the halo is now a thin ring
        // (PixelSprites.Disc(true)). A ring has a fraction of a disc's area, so it can take a
        // higher alpha and still read as faint; .45 keeps it visible on the dark floor.
        public const float HaloAlpha = 0.45f;
        // x radius: the ring's outer edge sits just outside the true hitbox, so the outline
        // hugs what can actually hit you instead of hinting past it like the old 1.3 disc.
        public const float HaloScale = 1.15f;
        // x radius: a 32-pixel cell spans 1 unit at scale 1. The sheets draw in about 40% of
        // their cell. 2.6 (first pass) was a third of the halo; 5 (capture) still read a little
        // small in the playtest, so 6.
        public const float SpriteScale = 6f;
        // The Homing Orb draws in most of its cell, unlike the other shot sheets, so at the
        // shared scale it overshot its outline by about 30% (final review minor). Scaled to fit.
        const float OrbScale = SpriteScale / 1.3f;
        public const float EchoAlpha = 0.6f;
        public const float Fps = 12f;

        /// <param name="spreadHalfAngle">
        /// The volley's half-spread (AttackSnapshot.SourceSpreadHalfAngle). Only the Collector
        /// reads it: its bolt stream and its wide fan both fire the Bolt definition, and the
        /// playtest asked for them to look different. The stream fires single bolts (0), the
        /// fan fires a spread, so presentation can tell them apart without any sim change.
        /// </param>
        public static string Sheet(ActorCategory school, AttackKind kind, float spreadHalfAngle)
        {
            if (kind == AttackKind.Riposte) return "Dark Slash";
            switch (school)
            {
                case ActorCategory.Acolyte: return "Magic Missile";
                case ActorCategory.ScatterCaster: return "Ice Shard Shot";  // thin and pointed: a fan reads as spread
                case ActorCategory.SiegeFamiliar: return "Fireball Shot";
                // The stream is a hue-shifted Plasma Shot in the Collector's magenta (see
                // WorldArtImporter.Recolours); the fan keeps the Homing Orb.
                case ActorCategory.Boss: return spreadHalfAngle > 0f ? "Homing Orb" : "Collector Plasma";
                default: return "Arcane Orb";
            }
        }

        /// <summary>The same priority ArenaView always used, so no shot changes colour meaning.</summary>
        public static Color State(AttackFaction faction, AttackKind kind, bool overcharged) =>
            overcharged ? FeedbackColors.Overcharge
            : kind == AttackKind.Riposte ? FeedbackColors.Riposte
            : faction == AttackFaction.Returned ? FeedbackColors.Returned
            : kind == AttackKind.Rocket ? FeedbackColors.Rocket
            : FeedbackColors.Hostile;

        public static Color Halo(AttackFaction faction, AttackKind kind, bool overcharged)
        {
            var c = State(faction, kind, overcharged);
            c.a = HaloAlpha;
            return c;
        }

        // Returned trails are a little longer: the player's own shot is the one to admire.
        public static float TrailSeconds(AttackFaction faction) => faction == AttackFaction.Returned ? 0.12f : 0.08f;

        /// <summary>Extra puffs behind a shot, or null. Overcharge is a state, and the state wins.</summary>
        public static string Puffs(AttackKind kind, bool overcharged) =>
            overcharged ? "Spark Trail" : kind == AttackKind.Rocket ? "Fire Trail" : null;

        public static float SpriteAlpha(bool echo) => echo ? EchoAlpha : 1f;

        /// <summary>The sprite scale (x hitbox radius) for a sheet, corrected for how much of its cell it fills.</summary>
        public static float SpriteScaleFor(string sheet) => sheet == "Homing Orb" ? OrbScale : SpriteScale;
    }
}
