using UnityEngine;

namespace BorrowedHex.Data
{
    public enum DecayPropKind { Column, RuinedWall, Tomb, DeadTree, Rock, Obelisk, Urn, Crystal }

    /// <summary>
    /// Four spaces in one continuous world. Rectangles remain authoritative for collision;
    /// visual props never decide whether a projectile or actor can pass through cover.
    /// Fresh layout instances prevent a run from modifying the shared configuration asset.
    /// </summary>
    public static class WorldArenaLayouts
    {
        public static ArenaLayout Create(int stage)
        {
            stage = Mathf.Clamp(stage, 0, 3);
            // Ordinary stages morph in place while combat continues. Their stable outer
            // footprint prevents changing boundaries from crushing actors or snapping aim.
            Vector2 centre = stage < 3 ? Vector2.zero : new Vector2(96, 64);
            var size = stage < 3 ? new Vector2(48, 36) : new Vector2(40, 34);
            var arena = new ArenaLayout {
                bounds = new Rect(centre - size * .5f, size), playerSpawn = centre + new Vector2(0, -7),
                wallHeight = 1.2f, pillarDurability = 12, pillarDecayMinInterval = 4, pillarDecayMaxInterval = 7,
                worldTheme = new[] { "Courtyard", "Graveyard", "Cave", "Sanctum" }[stage]
            };
            arena.pillars.Clear();
            void Cover(float x, float z, float width, float depth, DecayPropKind kind)
            {
                arena.pillars.Add(new Rect(centre + new Vector2(x - width / 2, z - depth / 2), new Vector2(width, depth)));
                arena.propKinds.Add(kind);
            }
            if (stage == 0)
            {
                // A broad processional route with staggered ruined garden walls.
                foreach (float x in new[] { -11f, 11f })
                {
                    Cover(x, -6, 3.2f, 1, DecayPropKind.RuinedWall);
                    Cover(x, 6, 1, 3.2f, DecayPropKind.RuinedWall);
                    Cover(x * 1.5f, 0, 1.4f, 1.4f, DecayPropKind.DeadTree);
                    Cover(x * .5f, 10, 1.5f, 1.5f, DecayPropKind.Column);
                }
            }
            else if (stage == 1)
            {
                // Offset tomb rows create several lanes rather than four symmetric pillars.
                for (int i = 0; i < 12; i++)
                    Cover((i % 2 == 0 ? -1 : 1) * (6 + i % 3 * 3), -11 + i / 2 * 4.5f,
                        i % 3 == 2 ? 1.3f : 2.2f, 1.3f,
                        i % 3 == 0 ? DecayPropKind.Tomb : i % 3 == 1 ? DecayPropKind.DeadTree : DecayPropKind.Urn);
            }
            else if (stage == 2)
            {
                // Uneven rock islands leave an open winding route through the cave.
                for (int i = 0; i < 10; i++)
                    Cover((i % 2 == 0 ? -1 : 1) * (7 + i % 3 * 4), -11 + i / 2 * 5, 1.8f + i % 3 * .4f, 1.5f,
                        i % 3 == 0 ? DecayPropKind.Rock : i % 3 == 1 ? DecayPropKind.Crystal : DecayPropKind.DeadTree);
            }
            else
            {
                // A clear central boss space, surrounded by degradable ritual obelisks.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    Cover(Mathf.Cos(a) * 12, Mathf.Sin(a) * 11, 1.4f, 1.4f, DecayPropKind.Obelisk);
                }
            }
            return arena;
        }
    }
}
