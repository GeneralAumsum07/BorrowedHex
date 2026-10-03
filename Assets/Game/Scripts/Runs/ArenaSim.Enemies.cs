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
        bool sandboxSpawnPending;

        /// <summary>
        /// Sandbox director switch. Starts from <c>Setup.SandboxAutoSpawn</c> but can be flipped
        /// mid-run from the dev panel, so a playtester can freeze the arena on one summoned enemy
        /// without the director topping it up with a formation.
        /// </summary>
        public bool AutoSpawn;

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
            // For() throws on a category with no ordinary-enemy tuning (boss, player):
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

        /// <summary>
        /// Bring in the Collector (Phase 5). It gets its own tuning (BossTuning) because its
        /// patterns share nothing with the ordinary-enemy roster beyond the body and damage
        /// rules. Placed on the far side of the arena from the player, on the centre line,
        /// which is clear of all four pillars.
        /// </summary>
        /// <param name="completedCycles">Endless only (Phase 12): each completed cycle adds
        /// boss health, and a REPEATED boss (any cycle after the first) gains its one predefined
        /// variation, the wider fan. Set before the spawn event so views see the final boss.</param>
        public EnemyActor SpawnBoss(int completedCycles = 0)
        {
            if (SelectWorldArena(3)) { ClearArena(); RestorePillars(); }
            var t = Config.collector;
            var b = Arena.bounds;
            float y = Player.Position.y > b.center.y ? b.yMin + 3.5f : b.yMax - 3.5f;
            var pos = new Vector2(b.center.x, y);
            var e = new EnemyActor
            {
                ActorId = Ids.Next(),
                Category = ActorCategory.Boss,
                Position = pos,
                PrevPosition = pos,
                SpawnedAt = Clock.Now,
                ActiveAt = Clock.Now + t.spawnWarning,
                Phase = EnemyPhase.Warning,
                AimDirection = (Player.Position - pos).normalized,
                StrafeSign = Random.NextFloat() < 0.5f ? -1f : 1f,
                Radius = t.bodyRadius,
                MaxHealth = t.health,
                Health = t.health,
                KillValue = t.killValue,
                Boss = new BossState { Encore = completedCycles > 0 },
            };
            if (completedCycles > 0)
                e.MaxHealth = e.Health = t.health * (1f + Config.endless.bossHealthPerCycle * completedCycles);
            Enemies.Add(e);
            Events.RaiseEnemySpawned(e);
            return e;
        }

        /// <summary>True while any boss is alive (ordinary-enemy counts and caps exclude it).</summary>
        public EnemyActor LivingBoss()
        {
            foreach (var e in Enemies) if (e.Alive && e.IsBoss) return e;
            return null;
        }

        /// <summary>
        /// The overstay evolution (D63). Normally reached only from TickEnemies when an enemy's
        /// timer runs out; public so the tutorial's evolution lesson (D87) can show it on the spot
        /// instead of making the player wait 25 seconds. Bosses never evolve.
        /// </summary>
        public void EvolveEnemy(EnemyActor e, double now)
        {
            // One-way evolution, guarded by Overstayed so it can never compound or heal twice.
            // It multiplies on top of the spawn-time values, which are the authored base in
            // short mode and the endless cycle scaling in endless (Phase 12): an overstayer is
            // always stronger than its own cycle.
            if (e.Overstayed || e.IsBoss || !e.Alive) return;
            var t = Config.combat.For(e.Category);
            e.Overstayed = e.Elite = true;
            e.MaxHealth = e.Health = e.MaxHealth * Config.combat.overstayHealthScale;
            e.MoveScale *= Config.combat.overstayMoveScale;
            e.CooldownScale *= Config.combat.overstayCooldownScale;
            e.KillValue = Mathf.RoundToInt(t.killValue * 1.5f);
            if (e.Phase == EnemyPhase.Idle || e.Phase == EnemyPhase.Recover)
                e.PhaseEndsAt = now + System.Math.Max(0, e.PhaseEndsAt - now) * e.CooldownScale;
            Events.RaiseEnemyOverstayed(e);
        }

        void TickEnemies(double now, float dt)
        {
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                e.PrevPosition = e.Position;
                if (e.IsBoss)
                {
                    CollectorBoss.Tick(this, e, Config.collector, now, dt);
                    continue;
                }
                var t = Config.combat.For(e.Category);
                if (!e.Overstayed && now >= e.ActiveAt + OverstaySeconds - 1e-6) EvolveEnemy(e, now);
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
            ApplyContactDamage(now);
        }

        /// <summary>
        /// Owner direction (supersedes D25's "body contact is harmless"): touching an enemy's
        /// body costs half a heart (a boss's, a whole heart), followed by a SHORT invulnerability
        /// window, so contact punishes walking into bodies without chaining hits every tick.
        /// Checked after enemies have moved and been separated, so it tests where bodies
        /// actually ended this tick. Spawn warnings are harmless (IsActive), and a dash's
        /// i-frames pass through bodies freely (DamagePlayer respects them).
        /// </summary>
        void ApplyContactDamage(double now)
        {
            var p = Player;
            if (!p.Alive || p.IsInvulnerable(now)) return;
            foreach (var e in Enemies)
            {
                if (!e.IsActive(now)) continue;
                float r = e.Radius + p.Radius;
                if ((e.Position - p.Position).sqrMagnitude > r * r) continue;
                int dmg = e.IsBoss ? Config.collector.contactDamage : Config.combat.enemyContactDamage;
                // One contact hit per tick at most: the first body found wins, and the
                // invulnerability it grants covers any other body touching on the same tick.
                if (DamagePlayer(dmg, e.ActorId, Stats.ContactInvulnerability)) return;
            }
        }

        /// <summary>True for kinds that fire capturable ammunition (formation authoring, director).</summary>
        public static bool IsRanged(ActorCategory c) =>
            c == ActorCategory.Acolyte || c == ActorCategory.ScatterCaster || c == ActorCategory.SiegeFamiliar;

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
        /// School resistance (D92, owner rule): an enemy shrugs off its own school's magic (x0.75)
        /// and takes x1.33 from any other school. "School" is the attack's SourceCategory, which a
        /// returned payload keeps from its caster, so a bolt caught from ANY Acolyte is Acolyte
        /// school. A riposte keeps the default Player category, so it is "another source" and
        /// parry stays the Pursuer counter (R6, owner). Upgrade damage (Orbit, Parting Gift) is
        /// neutral: the rule is about turning enemy magic on enemies, and upgrades are not enemy
        /// magic (R5, owner). The boss is exempt (R7): in its fight its own returned shots are
        /// nearly the only damage there is, so resistance would only lengthen it.
        /// Applied in DamageEnemy, the single entry point, so the DamageEvent, kill check and
        /// score all see the same scaled number.
        /// </summary>
        internal float SchoolMultiplier(EnemyActor victim, DamageCategory category, in AttackSnapshot shot)
        {
            if (victim.IsBoss || category == DamageCategory.Orbit || category == DamageCategory.PartingGift) return 1f;
            var c = Config.combat;
            return shot.SourceCategory == victim.Category ? c.ownSchoolDamage : c.otherSchoolDamage;
        }

        /// <summary>
        /// The single entry point for enemy damage. Ignores dead or still-warning enemies, so a
        /// spawn warning is genuinely harmless in both directions.
        /// </summary>
        public bool DamageEnemy(EnemyActor e, float amount, DamageCategory category, in AttackSnapshot shot, int rootReleaseId)
        {
            if (Summary != null || e == null || !e.IsActive(Clock.Now) || amount <= 0f) return false;
            amount *= SchoolMultiplier(e, category, shot);
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
                Overcharged = shot.Overcharged,
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

        /// <summary>Living non-boss enemies: what the section 6 cap of 12 counts.</summary>
        public int AliveOrdinaryCount()
        {
            int n = 0;
            foreach (var e in Enemies) if (e.Alive && !e.IsBoss) n++;
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
            var b = Arena.bounds;
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
            if (!AutoSpawn || sandboxSpawnPending || !Player.Alive) return;
            if (AliveEnemyCount() > 0) return;
            sandboxSpawnPending = true;
            Scheduler.Schedule(now + 1.5, () =>
            {
                sandboxSpawnPending = false;
                // Re-check the switch: it may have been turned off during the breather.
                if (Player.Alive && AutoSpawn) SpawnNextSandboxFormation();
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

        /// <summary>
        /// Playtest control: one enemy of exactly this kind at a legal spawn point (same rules
        /// as a formation member: spawn warning, minimum player distance, clear of pillars).
        /// </summary>
        public EnemyActor SummonEnemy(ActorCategory category) =>
            SpawnEnemy(category, FindSpawnPoint(Config.combat.For(category).bodyRadius));

        /// <summary>
        /// Playtest control: despawn every enemy (never counted as kills) and remove hostile
        /// shots in flight, so a fresh matchup starts without leftovers. Stored packets and
        /// the player's own returned shots are kept — they belong to the player.
        /// </summary>
        public void ClearArena()
        {
            foreach (var e in Enemies) DespawnEnemy(e);
            foreach (var p in Projectiles)
                if (p.Active && p.Faction == AttackFaction.Hostile) EndProjectile(p, ProjectileEndReason.Cleared);
            RemoveDeadEnemies();
            CompactProjectiles();
        }
    }
}
