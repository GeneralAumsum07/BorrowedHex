using System.Collections.Generic;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Runs
{
    public sealed partial class ArenaSim
    {
        public readonly List<DecayObstacle> Pillars = new List<DecayObstacle>();
        SeededRandom pillarRandom;
        public readonly List<DecayObstacle> RetiringCover = new List<DecayObstacle>();
        readonly List<double> retirementTimes = new List<double>(), formationTimes = new List<double>();
        readonly List<bool> formedCover = new List<bool>();
        public const float WorldMorphDuration = 24;
        public double WorldMorphStartedAt { get; private set; }
        public bool WorldMorphing { get; private set; }
        public float WorldMorphProgress => WorldMorphing ? Mathf.Clamp01((float)(Clock.Now - WorldMorphStartedAt) / WorldMorphDuration) : 1;
        public bool CoverFormed(int index) => index >= formedCover.Count || formedCover[index];
        public float CoverFormation(int index) => CoverFormed(index) ? 1 : Mathf.Clamp01((float)(Clock.Now - (formationTimes[index] - 4)) / 4) * .85f;
        public float RetiringCoverVisibility(int index) => RetiringCover[index].Crumbled ? 0
            : Mathf.Clamp01((float)((retirementTimes[index] - Clock.Now) / (retirementTimes[index] - WorldMorphStartedAt)));

        void InitArenaDecay()
        {
            // A separate stream keeps adding an obstacle from altering formations or AI.
            pillarRandom = new SeededRandom(Setup.Seed ^ 0x50494C4C);
            foreach (var bounds in Arena.pillars) Pillars.Add(new DecayObstacle(bounds));
            RestorePillars();
        }

        void RestorePillars()
        {
            Walls.Clear();
            var boxes = Arena.BuildObstacles();
            for (int i = 0; i < 4; i++) Walls.Add(boxes[i]);
            var tuning = Arena;
            for (int i = 0; i < Pillars.Count; i++)
            {
                var pillar = Pillars[i];
                pillar.Restore(Clock.Now, tuning.pillarDurability,
                    pillarRandom.Range(tuning.pillarDecayMinInterval, tuning.pillarDecayMaxInterval));
                if (CoverFormed(i)) Walls.Add(pillar.Bounds);
            }
            foreach (var old in RetiringCover) if (!old.Crumbled) Walls.Add(old.Bounds);
            // The player may have crossed rubble during the previous encounter. Regrowth
            // must not put their body inside solid cover or let them walk through that cover.
            if (Player != null) MovePlayerOutOfRestoredCover();
        }

        bool SelectWorldArena(int stage)
        {
            if (!Setup.WorldArenas || Setup.Tutorial || ArenaStage == stage) return false;
            // A fast clear/debug skip finishes older replacement before starting another;
            // invisible collision must never accumulate from several abandoned layouts.
            foreach (var old in RetiringCover)
                if (!old.Crumbled) { old.Crumble(); Walls.Remove(old.Bounds); Events.RaisePillarCrumbled(old); }
            RetiringCover.Clear(); retirementTimes.Clear();
            bool morph = stage < 3 && ArenaStage < 3;
            if (morph)
                foreach (var old in Pillars)
                    if (!old.Crumbled && Walls.Contains(old.Bounds)) RetiringCover.Add(old);
            WorldMorphing = morph; WorldMorphStartedAt = Clock.Now;
            for (int i = 0; i < RetiringCover.Count; i++)
                retirementTimes.Add(Clock.Now + 3 + i * 12f / Mathf.Max(1, RetiringCover.Count));
            ArenaStage = stage;
            Arena = Data.WorldArenaLayouts.Create(stage);
            Pillars.Clear();
            formedCover.Clear(); formationTimes.Clear();
            for (int i = 0; i < Arena.pillars.Count; i++)
            {
                Pillars.Add(new DecayObstacle(Arena.pillars[i])); formedCover.Add(!morph);
                formationTimes.Add(Clock.Now + 7 + i * 15f / Mathf.Max(1, Arena.pillars.Count));
            }
            // Only Sanctum entry relocates the player. Ordinary morphing preserves input,
            // position, dash, packets, aim and combat time throughout the transformation.
            if (!morph) { Player.Position = Arena.playerSpawn; Player.Dashing = false; }
            ArenaRevision++;
            return true;
        }

        void MovePlayerOutOfRestoredCover()
        {
            for (int i = 0; i < Pillars.Count; i++)
            {
                // Incoming fragments are still non-solid. Only existing collision can
                // require recovery after an encounter; formation itself waits for actors.
                if (!CoverFormed(i) || Pillars[i].Crumbled) continue;
                var pillar = Pillars[i];
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
            for (int i = 0; i < RetiringCover.Count; i++)
            {
                var old = RetiringCover[i]; if (old.Crumbled) continue;
                bool crumbled = old.Advance(now);
                if (now >= retirementTimes[i]) { old.Crumble(); crumbled = true; }
                if (crumbled) { Walls.Remove(old.Bounds); Events.RaisePillarCrumbled(old); }
            }
            for (int i = 0; i < Pillars.Count; i++)
            {
                var pillar = Pillars[i];
                if (!CoverFormed(i))
                {
                    if (now < formationTimes[i] || CoverOccupied(pillar.Bounds)) continue;
                    formedCover[i] = true;
                    pillar.Restore(now, Arena.pillarDurability, pillarRandom.Range(Arena.pillarDecayMinInterval, Arena.pillarDecayMaxInterval));
                    Walls.Add(pillar.Bounds);
                }
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

        bool CoverOccupied(Rect box)
        {
            if (Geometry2D.CircleOverlapsRect(Player.Position, Player.Radius + .08f, box)) return true;
            foreach (var enemy in Enemies)
                if (enemy.Alive && Geometry2D.CircleOverlapsRect(enemy.Position, enemy.Radius + .08f, box)) return true;
            foreach (var old in RetiringCover) if (!old.Crumbled && old.Bounds.Overlaps(box)) return true;
            return false;
        }
    }
}
