using System;
using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    public sealed partial class ArenaSim
    {
        public readonly List<DecayObstacle> Pillars = new List<DecayObstacle>();
        SeededRandom pillarRandom, morphRandom;
        public readonly List<DecayObstacle> RetiringCover = new List<DecayObstacle>();
        readonly List<int> retiringPatch = new List<int>(), coverPatch = new List<int>();
        readonly List<bool> formedCover = new List<bool>(), formedLate = new List<bool>();
        readonly List<double> formedAt = new List<double>();

        // Reality-glitch morph tuning (owner-approved design, 2026-10-04).
        // KillShare: the part of the field the current encounter's kills turn over; the
        // rest finishes over TailSeconds into the next encounter. TimedSeconds drives
        // morphs with no encounter kills behind them (endless waves, sandbox stage edges).
        public const float KillShare = .8f, TailSeconds = 4, TimedSeconds = 6;
        // A multi-kill ripples instead of popping several patches in the same frame.
        public const float TriggerGap = .25f;
        // A spot older than the last few kills no longer reads as "where that enemy died".
        const int KillSpotMemory = 4;

        /// <summary>The live patch field; null when no morph is running.</summary>
        public MorphField Morph { get; private set; }
        public bool WorldMorphing => Morph != null;
        public float WorldMorphProgress => Morph == null ? 1 : Morph.Progress(Clock.Now);

        bool morphByKills;
        int morphEncounter, tailFrom, encounterKills;
        double tailStartedAt, lastTriggerAt = double.NegativeInfinity;
        float tailSeconds = TimedSeconds;
        readonly List<Vector2> killSpots = new List<Vector2>();

        public bool CoverFormed(int index) => index >= formedCover.Count || formedCover[index];
        /// <summary>Patch owning incoming cover `index`; -1 when it arrived without a morph.</summary>
        public int CoverPatch(int index) => index < coverPatch.Count ? coverPatch[index] : -1;
        public int RetiringPatch(int index) => retiringPatch[index];
        public double CoverFormedAt(int index) => index < formedAt.Count ? formedAt[index] : 0;
        /// <summary>True when the cover had to wait for someone to leave its footprint.</summary>
        public bool CoverFormedLate(int index) => index < formedLate.Count && formedLate[index];

        // The Sanctum's obelisks are the boss fight's fixed landmarks: they never wear down.
        bool PermanentCover => Setup.WorldArenas && ArenaStage == 3;

        /// <summary>Displayed wear, 1 = intact. Continuous, so the dissolve never steps.</summary>
        public float CoverIntegrity(DecayObstacle cover)
        {
            if (PermanentCover || cover.MaxDurability == 0) return 1;
            return Mathf.Clamp01(1 - (float)(Clock.Now - cover.RestoredAt) / (cover.MaxDurability * cover.DecayInterval));
        }

        void InitArenaDecay()
        {
            // Separate streams keep adding an obstacle or a morph from altering formations or AI.
            pillarRandom = new SeededRandom(Setup.Seed ^ 0x50494C4C);
            morphRandom = new SeededRandom(Setup.Seed ^ 0x4D4F5250);
            foreach (var bounds in Arena.pillars) Pillars.Add(NewCover(bounds));
            Events.EnemyKilled += CountMorphKill;
            RebuildWalls();
        }

        DecayObstacle NewCover(Rect bounds)
        {
            var cover = new DecayObstacle(bounds);
            cover.Restore(Clock.Now, Arena.pillarDurability, pillarRandom.Range(Arena.pillarDecayMinInterval, Arena.pillarDecayMaxInterval));
            return cover;
        }

        /// <summary>
        /// Rebuilds the one collision list from what currently stands. Crumbled cover is NOT
        /// restored (owner ruling, 2026-10-04): it stays gone and the morph brings new cover.
        /// </summary>
        void RebuildWalls()
        {
            Walls.Clear();
            var boxes = Arena.BuildObstacles();
            for (int i = 0; i < 4; i++) Walls.Add(boxes[i]);
            for (int i = 0; i < Pillars.Count; i++)
                if (CoverFormed(i) && !Pillars[i].Crumbled) Walls.Add(Pillars[i].Bounds);
            foreach (var old in RetiringCover) if (!old.Crumbled) Walls.Add(old.Bounds);
            if (Player != null) MovePlayerOutOfRestoredCover();
        }

        /// <summary>
        /// The classic single arena (world arenas off, and the tutorial) has no morph to bring
        /// new cover, so it keeps the original rule: every choice edge regrows all of it. The
        /// "no in-place rebuild" ruling applies to world arenas only.
        /// </summary>
        void RestoreClassicCover()
        {
            if (Setup.WorldArenas && !Setup.Tutorial) return;
            foreach (var pillar in Pillars)
                pillar.Restore(Clock.Now, Arena.pillarDurability, pillarRandom.Range(Arena.pillarDecayMinInterval, Arena.pillarDecayMaxInterval));
            // RebuildWalls also moves a player who crossed the rubble out of the regrown cover.
            RebuildWalls();
        }

        void CountMorphKill(EnemyActor enemy, DamageEvent damage)
        {
            if (!IsShortRun || enemy == Boss) return;
            encounterKills++;
            killSpots.Add(enemy.Position);
            if (killSpots.Count > KillSpotMemory) killSpots.RemoveAt(0);
        }

        /// <summary>
        /// Encounter edge (ContinueFromUpgrade): a kill-driven morph switches to its timed tail,
        /// and a stage the kills never reached (a skip) is caught up by a timed morph.
        /// </summary>
        void AdvanceWorldMorph()
        {
            encounterKills = 0; killSpots.Clear();
            if (!Setup.WorldArenas || Setup.Tutorial) return;
            if (Morph != null && morphByKills) BeginTail(TailSeconds);
            int desired = Mathf.Clamp(Encounter, 0, 2);
            if (ArenaStage != desired) SelectWorldArena(desired);
        }

        void BeginTail(float seconds)
        {
            morphByKills = false; tailStartedAt = Clock.Now; tailFrom = Morph.Triggered; tailSeconds = seconds;
        }

        // Short runs only: encounter e turns stage e into stage e+1 from its first kill. The
        // last encounter is left alone; the Sanctum entry keeps its own transition.
        bool ShouldBeginKillMorph()
            => Setup.WorldArenas && !Setup.Tutorial && IsShortRun && State == RunState.Combat
               && encounterKills > 0 && ArenaStage == Encounter && Encounter + 1 < Config.shortMode.encounterCount
               && Encounter + 1 < 3;

        bool SelectWorldArena(int stage, bool byKills = false)
        {
            if (!Setup.WorldArenas || Setup.Tutorial || ArenaStage == stage) return false;
            // A fast clear/debug skip finishes the running morph before starting another;
            // invisible collision must never accumulate from several abandoned layouts.
            Morph?.CompleteAll(Clock.Now);
            foreach (var old in RetiringCover)
                if (!old.Crumbled) { old.Crumble(); Walls.Remove(old.Bounds); Events.RaisePillarCrumbled(old); }
            RetiringCover.Clear(); retiringPatch.Clear();
            bool morph = stage < 3 && ArenaStage < 3;
            ArenaStage = stage;
            Arena = Data.WorldArenaLayouts.Create(stage);
            Morph = morph ? new MorphField(Arena.bounds, morphRandom) : null;
            lastTriggerAt = double.NegativeInfinity;
            if (morph)
            {
                morphByKills = byKills; morphEncounter = Encounter;
                if (!byKills) BeginTail(TimedSeconds);
                // Standing cover from the outgoing layout stays solid until its patch turns over.
                for (int i = 0; i < Pillars.Count; i++)
                {
                    var old = Pillars[i];
                    if (!CoverFormed(i) || old.Crumbled || !Walls.Contains(old.Bounds)) continue;
                    RetiringCover.Add(old); retiringPatch.Add(Morph.PatchAt(old.Bounds.center));
                }
            }
            Pillars.Clear(); formedCover.Clear(); coverPatch.Clear(); formedAt.Clear(); formedLate.Clear();
            for (int i = 0; i < Arena.pillars.Count; i++)
            {
                var bounds = Arena.pillars[i];
                // Incoming morph cover is restored only when it forms, so its wear starts then.
                Pillars.Add(morph ? new DecayObstacle(bounds) : NewCover(bounds));
                formedCover.Add(!morph); formedLate.Add(false); formedAt.Add(Clock.Now);
                coverPatch.Add(morph ? Morph.PatchAt(bounds.center) : -1);
            }
            // Only Sanctum entry relocates the player. Ordinary morphing preserves input,
            // position, dash, packets, aim and combat time throughout the transformation.
            if (!morph) { Player.Position = Arena.playerSpawn; Player.Dashing = false; }
            ArenaRevision++;
            RebuildWalls();
            return true;
        }

        void MovePlayerOutOfRestoredCover()
        {
            for (int i = 0; i < Pillars.Count; i++)
            {
                // Incoming fragments are still non-solid. Only existing collision can
                // require recovery; formation itself waits for actors.
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

        /// <summary>
        /// Starts the kill-driven morph when due, then triggers patches up to the current
        /// target. Trigger times are back-dated inside this tick (never before tickStart) so a
        /// long tick or a test's single big step still spaces patches TriggerGap apart.
        /// </summary>
        void TickMorph(double tickStart, double now)
        {
            if (Morph == null)
            {
                if (!ShouldBeginKillMorph()) return;
                SelectWorldArena(Encounter + 1, byKills: true);
            }
            int target = morphByKills && Encounter == morphEncounter
                ? MorphField.KillTarget(encounterKills, EnemiesLeftInEncounter(), KillShare)
                : MorphField.TimedTarget(tailFrom, now - tailStartedAt, tailSeconds);
            while (Morph.Triggered < target)
            {
                double at = Math.Max(tickStart, lastTriggerAt + TriggerGap);
                if (at > now) break;
                Morph.Trigger(PickPatch(), at); lastTriggerAt = at;
            }
        }

        // The newest kill spot pulls the patch nearest it; with none left, any patch at random.
        int PickPatch()
        {
            while (killSpots.Count > 0)
            {
                var spot = killSpots[killSpots.Count - 1]; killSpots.RemoveAt(killSpots.Count - 1);
                int near = Morph.NearestUntriggered(spot);
                if (near >= 0) return near;
            }
            return Morph.RandomUntriggered(morphRandom);
        }

        bool PatchDone(int patch, double now) => Morph == null || patch < 0 || Morph.Complete(patch, now);

        void TickArenaDecay(double tickStart, double now)
        {
            TickMorph(tickStart, now);
            for (int i = 0; i < RetiringCover.Count; i++)
            {
                var old = RetiringCover[i]; if (old.Crumbled) continue;
                bool crumbled = old.Advance(now);
                // Outgoing cover stops being solid the moment its patch has turned over.
                if (!crumbled && PatchDone(retiringPatch[i], now)) { old.Crumble(); crumbled = true; }
                if (crumbled) { Walls.Remove(old.Bounds); Events.RaisePillarCrumbled(old); }
            }
            for (int i = 0; i < Pillars.Count; i++)
            {
                var pillar = Pillars[i];
                if (!CoverFormed(i))
                {
                    if (!PatchDone(coverPatch[i], now)) continue;
                    // Held back while anyone stands in the footprint; the art shows a ghost.
                    if (CoverOccupied(pillar.Bounds)) { formedLate[i] = true; continue; }
                    formedCover[i] = true; formedAt[i] = now;
                    pillar.Restore(now, Arena.pillarDurability, pillarRandom.Range(Arena.pillarDecayMinInterval, Arena.pillarDecayMaxInterval));
                    Walls.Add(pillar.Bounds);
                }
                if (PermanentCover) continue;
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
            // Released once every patch has turned and any blocked cover's fades have settled;
            // late cover still forms afterwards (PatchDone is true with no field).
            if (Morph != null && Morph.AllComplete(now - MorphField.Settle)) Morph = null;
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
