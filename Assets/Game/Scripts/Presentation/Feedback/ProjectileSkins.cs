using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// How a shot looks (spec 3, D2). The SHAPE says who made it, and it survives capture and
    /// return, so a returned ice shard is still an ice shard. The COLOUR (halo and trail) says
    /// what it will do to you now. The halo stays the honest hitbox at the old disc size; the
    /// sprite is decoration on top.
    /// </summary>
    public static class ProjectileSkins
    {
        public const float HaloAlpha = 0.45f;
        public const float HaloScale = 1.3f;     // x radius: the old glow disc, the true hitbox plus a hint
        public const float SpriteScale = 2.6f;   // x radius: a 32-pixel cell spans 1 unit at scale 1
        public const float EchoAlpha = 0.6f;
        public const float Fps = 12f;

        public static string Sheet(ActorCategory school, AttackKind kind)
        {
            if (kind == AttackKind.Riposte) return "Dark Slash";
            switch (school)
            {
                case ActorCategory.Acolyte: return "Magic Missile";
                case ActorCategory.ScatterCaster: return "Ice Shard Shot";  // thin and pointed: a fan reads as spread
                case ActorCategory.SiegeFamiliar: return "Fireball Shot";
                case ActorCategory.Boss: return "Homing Orb";
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
    }
}
