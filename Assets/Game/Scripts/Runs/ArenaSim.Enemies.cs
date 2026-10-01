using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>Enemy lifecycle: spawning (with warning), per-kind brains, damage and death.</summary>
    public sealed partial class ArenaSim
    {
        public readonly List<EnemyActor> Enemies = new List<EnemyActor>();
        public Lantern Lantern { get; private set; }
        bool sandboxSpawnPending;

        public EnemyActor SpawnEnemy(ActorCategory category, Vector2 position, bool elite = false)
        {
            var c = Config.combat;
            var e = new EnemyActor
            {
                ActorId = Ids.Next(),
                Category = category,
                Position = position,
                PrevPosition = position,
                Elite = elite,
                SpawnedAt = Clock.Now,
                ActiveAt = Clock.Now + c.spawnWarning,
                Phase = EnemyPhase.Warning,
                StrafeSign = Random.NextFloat() < 0.5f ? -1f : 1f,
            };
            // For() throws on a category with no ordinary-enemy tuning (boss, player, lantern):
            // fail loudly rather than spawn something with no brain.
            var t = c.For(category);
            e.Radius = t.bodyRadius;
            e.MaxHealth = e.Health = t.health;
            // Section 6: elite kill value is 1.5x the base. Elite behaviour modifiers arrive
            // with endless mode (Phase 11); the flag and value exist now so scoring is stable.
            e.KillValue = elite ? Mathf.RoundToInt(t.killValue * 1.5f) : t.killValue;
            Enemies.Add(e);
            Events.RaiseEnemySpawned(e);
            return e;
        }

        void TickEnemies(double now, float dt)
        {
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                e.PrevPosition = e.Position;
                var t = Config.combat.For(e.Category);
                if (e.Phase == EnemyPhase.Warning)
                {
                    if (now < e.ActiveAt) continue;
                    e.Phase = EnemyPhase.Idle;
                    e.PhaseEndsAt = now + t.firstShotDelay;
                }
                if (e.Category == ActorCategory.Pursuer) Pursuer.Tick(this, e, t, now, dt);
                else RangedCaster.Tick(this, e, t, now, dt);
            }
            SeparateEnemies();
        }

        /// <summary>True for kinds that supply capturable ammunition (section 3 starvation rule).</summary>
        public static bool IsRanged(ActorCategory c) =>
            c == ActorCategory.Acolyte || c == ActorCategory.ScatterCaster || c == ActorCategory.SiegeFamiliar;

        /// <summary>
        /// Section 3 ammunition starvation. Starving = enemies remain, but no ranged enemy, no
        /// hostile shot in flight, and no stored packet. After <c>starvationDelay</c> seconds of
        /// that, the lantern fires a pair every <c>interval</c> seconds until it ends.
        ///
        /// The lantern's OWN bolts do not count as "a hostile shot in flight": otherwise each
        /// pair would reset the condition and the cadence would stretch to flight time + delay.
        /// Once the player catches them they become a stored packet, which does end starvation
        /// until it is released, exactly as the rule reads.
        /// </summary>
        void TickLantern(double now)
        {
            bool starving = Player.Alive && AliveEnemyCount() > 0;
            if (starving)
                foreach (var e in Enemies)
                    if (e.Alive && IsRanged(e.Category)) { starving = false; break; }
            if (starving)
                foreach (var p in Projectiles)
                    if (p.Active && p.Faction == AttackFaction.Hostile && p.Shot.SourceActorId != Lantern.ActorId) { starving = false; break; }
            if (starving && Packets.Packets.Count > 0) starving = false;

            if (!starving)
            {
                Lantern.StarvedSince = -1;
                return;
            }
            if (Lantern.StarvedSince < 0)
            {
                Lantern.StarvedSince = now;
                Lantern.NextFireAt = now + Config.combat.lantern.starvationDelay;
            }
            if (now >= Lantern.NextFireAt - 1e-9)
            {
                FireLantern();
                Lantern.NextFireAt = now + Config.combat.lantern.interval;
            }
        }

        /// <summary>
        /// Cheap pairwise push-apart so enemies never stack into one unreadable sprite.
        /// O(n²) is fine for the dozen or so bodies an arena holds.
        /// </summary>
        void SeparateEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var a = Enemies[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < Enemies.Count; j++)
                {
                    var b = Enemies[j];
                    if (!b.Alive) continue;
                    Vector2 d = b.Position - a.Position;
                    float min = a.Radius + b.Radius;
                    float sq = d.sqrMagnitude;
                    if (sq >= min * min) continue;
                    float dist = Mathf.Sqrt(sq);
                    Vector2 n = dist > 1e-5f ? d / dist : Vector2.right;
                    float push = (min - dist) * 0.5f;
                    a.Position = PlayerMotor.SweepMove(a.Position, -n * push, a.Radius, Walls, out _);
                    b.Position = PlayerMotor.SweepMove(b.Position, n * push, b.Radius, Walls, out _);
                }
            }
        }

        /// <summary>
        /// The single entry point for enemy damage. Ignores dead or still-warning enemies, so a
        /// spawn warning is genuinely harmless in both directions.
        /// </summary>
        public bool DamageEnemy(EnemyActor e, float amount, DamageCategory category, in AttackSnapshot shot, int rootReleaseId)
        {
            if (e == null || !e.IsActive(Clock.Now) || amount <= 0f) return false;
            var ev = new DamageEvent
            {
                DamageId = Ids.Next(),
                TargetActorId = e.ActorId,
                SourceActorId = shot.SourceActorId,
                ShotId = shot.ShotId,
                RootReleaseId = rootReleaseId,
                Kind = shot.Kind,
                Amount = amount,
                Category = category,
                Perfect = shot.Perfect,
            };
            e.Health = Mathf.Max(0f, e.Health - amount);
            Events.RaiseEnemyDamaged(e, ev);
            if (e.Health <= 1e-4f)
            {
                // Alive is cleared before raising, and DamageEnemy rejects dead targets, so the
                // kill event fires exactly once per enemy however many hits land this tick.
                e.Health = 0f;
                e.Alive = false;
                e.Killed = true;
                Events.RaiseEnemyKilled(e, ev);
            }
            return true;
        }

        /// <summary>
        /// Remove an enemy WITHOUT killing it (formation cleanup, run end). Raises no kill
        /// event and leaves Killed false: section 6, despawning never counts as a kill.
        /// </summary>
        public void DespawnEnemy(EnemyActor e)
        {
            if (e == null || !e.Alive) return;
            e.Alive = false;
            Events.RaiseEnemyDespawned(e);
        }

        public int AliveEnemyCount()
        {
            int n = 0;
            foreach (var e in Enemies) if (e.Alive) n++;
            return n;
        }

        void RemoveDeadEnemies() => Enemies.RemoveAll(e => !e.Alive);

        /// <summary>
        /// Random legal spawn point: inside the arena, clear of pillars and other enemies, and at
        /// least <c>minSpawnDistance</c> from the player (section 4: never spawn on the player).
        /// Uses the run's seeded RNG so a seed reproduces the same encounters.
        /// </summary>
        public Vector2 FindSpawnPoint(float radius)
        {
            var b = Config.arena.bounds;
            float margin = radius + 0.8f;
            float minDist = Config.combat.minSpawnDistance;
            Vector2 best = new Vector2(b.center.x, b.yMax - margin);
            float bestScore = -1f;
            for (int i = 0; i < 48; i++)
            {
                var p = new Vector2(Random.Range(b.xMin + margin, b.xMax - margin), Random.Range(b.yMin + margin, b.yMax - margin));
                bool blocked = false;
                foreach (var w in Walls) if (Geometry2D.CircleOverlapsRect(p, radius + 0.3f, w)) { blocked = true; break; }
                if (blocked) continue;
                foreach (var e in Enemies) if (e.Alive && (e.Position - p).sqrMagnitude < (e.Radius + radius + 0.6f) * (e.Radius + radius + 0.6f)) { blocked = true; break; }
                if (blocked) continue;
                float d = (p - Player.Position).magnitude;
                if (d >= minDist) return p;
                if (d > bestScore) { bestScore = d; best = p; }   // fallback: farthest legal point seen
            }
            return best;
        }

        int sandboxFormation;

        /// <summary>
        /// Sandbox director: when the arena is clear, bring in the next authored formation after
        /// a short breather, cycling through the list so practice covers every enemy kind.
        /// The scheduled spawn dies with the run (death cancels the scheduler).
        /// </summary>
        void TickSandboxDirector(double now)
        {
            if (!Setup.SandboxAutoSpawn || sandboxSpawnPending || !Player.Alive) return;
            if (AliveEnemyCount() > 0) return;
            sandboxSpawnPending = true;
            Scheduler.Schedule(now + 1.5, () =>
            {
                sandboxSpawnPending = false;
                if (Player.Alive) SpawnNextSandboxFormation();
            });
        }

        /// <summary>Spawn the next formation in the sandbox rotation (also the dev button).</summary>
        public Formation SpawnNextSandboxFormation()
        {
            var f = EnemySpawnService.Formations[sandboxFormation % EnemySpawnService.Formations.Length];
            sandboxFormation++;
            EnemySpawnService.Spawn(this, f);
            return f;
        }

        /// <summary>Fire the lantern's pair of slow bolts at the player now.</summary>
        public void FireLantern()
        {
            var aim = Player.Position - Lantern.Position;
            AttackEmitter.FireVolley(this, AttackIds.LanternBolt, Lantern.ActorId, Lantern.Position, Lantern.BodyRadius,
                aim, Config.combat.lantern.pairSpreadDeg);
            Events.RaiseLanternFired();
        }
    }
}
