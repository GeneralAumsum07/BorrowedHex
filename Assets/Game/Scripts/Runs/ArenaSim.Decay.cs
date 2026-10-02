using System.Collections.Generic;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Runs
{
    public sealed partial class ArenaSim
    {
        public readonly List<DecayObstacle> Pillars = new List<DecayObstacle>();
        SeededRandom pillarRandom;

        void InitArenaDecay()
        {
            // A separate stream keeps adding an obstacle from altering formations or AI.
            pillarRandom = new SeededRandom(Setup.Seed ^ 0x50494C4C);
            foreach (var bounds in Config.arena.pillars) Pillars.Add(new DecayObstacle(bounds));
            RestorePillars();
        }

        void RestorePillars()
        {
            Walls.Clear();
            Walls.AddRange(Config.arena.BuildObstacles());
            var tuning = Config.arena;
            foreach (var pillar in Pillars)
                pillar.Restore(Clock.Now, tuning.pillarDurability,
                    pillarRandom.Range(tuning.pillarDecayMinInterval, tuning.pillarDecayMaxInterval));
            // The player may have crossed rubble during the previous encounter. Regrowth
            // must not put their body inside solid cover or let them walk through that cover.
            if (Player != null) MovePlayerOutOfRestoredCover();
        }

        void MovePlayerOutOfRestoredCover()
        {
            foreach (var pillar in Pillars)
            {
                var box = pillar.Bounds;
                var at = Player.Position;
                if (!Geometry2D.CircleOverlapsRect(at, Player.Radius, box)) continue;
                float clearance = Player.Radius + 0.01f;
                var candidates = new[]
                {
                    new Vector2(box.xMin - clearance, Mathf.Clamp(at.y, box.yMin, box.yMax)),
                    new Vector2(box.xMax + clearance, Mathf.Clamp(at.y, box.yMin, box.yMax)),
                    new Vector2(Mathf.Clamp(at.x, box.xMin, box.xMax), box.yMin - clearance),
                    new Vector2(Mathf.Clamp(at.x, box.xMin, box.xMax), box.yMax + clearance),
                };
                float bestDistance = float.PositiveInfinity;
                Vector2 best = at;
                foreach (var candidate in candidates)
                {
                    bool blocked = false;
                    foreach (var wall in Walls)
                        if (Geometry2D.CircleOverlapsRect(candidate, Player.Radius, wall)) { blocked = true; break; }
                    float distance = (candidate - at).sqrMagnitude;
                    if (!blocked && distance < bestDistance) { best = candidate; bestDistance = distance; }
                }
                Player.Position = best;
            }
        }

        void TickArenaDecay(double now)
        {
            foreach (var pillar in Pillars)
            {
                int before = pillar.Durability;
                bool crumbled = pillar.Advance(now);
                int lost = before - pillar.Durability;
                // Report the actual wear even when one long tick spans several intervals;
                // sound and other adapters need ordinary damage as well as the final crumble.
                if (lost > 0) Events.RaisePillarDamaged(pillar, lost);
                if (!crumbled) continue;
                // Every movement, projectile sweep, spawn check and boss sight test reads
                // this one list. Removing cover here makes it disappear for all of them.
                Walls.Remove(pillar.Bounds);
                Events.RaisePillarCrumbled(pillar);
            }
        }
    }
}
