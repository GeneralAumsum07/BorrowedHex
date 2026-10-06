using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    public enum BossPattern { BoltStream, Sweep, FanVolley, Slam, Summon }

    /// <summary>
    /// Where the boss is inside one pattern. Every pattern runs the same four steps, so every
    /// attack has the same readable shape: move, wind up (visible), strike, breathe. Teleport
    /// is a fifth, optional step before a pattern: a visible blink to behind the player.
    /// </summary>
    public enum BossStage { Reposition, Telegraph, Active, Recover, Teleport }

    /// <summary>Boss-only state, hung off the shared EnemyActor so damage, kill events and the
    /// view's body handling stay one code path for every enemy.</summary>
    public sealed class BossState
    {
        public BossPattern Pattern;
        public BossStage Stage;
        public double StageEndsAt;
        public double TelegraphStartedAt;
        /// <summary>Patterns started so far.</summary>
        public int PatternsStarted;
        /// <summary>Melee patterns in a row; capped so ammunition keeps arriving (see Choose).</summary>
        public int MeleeStreak;
        /// <summary>How many times in a row the current <see cref="Pattern"/> has started (D52).</summary>
        public int SameStreak;

        public Vector2 MoveTarget;

        // Bolt stream.
        public int ShotsLeft;
        public double NextShotAt;

        /// <summary>Endless: a repeated boss (Phase 12) fires the wider encore fan.</summary>
        public bool Encore;

        // Sweep: the blade's current angle relative to the locked aim, and where it ends.
        public float BladeDeg;
        public float BladeEndDeg;
        public bool SweepLanded;
        /// <summary>Count of resolved slams/sweeps (for views and tests).</summary>
        public int StrikesResolved;
        /// <summary>Count of parried sweeps (for views and tests).</summary>
        public int SweepsParried;

        // Teleport: where it will reappear, and when it may teleport again.
        public Vector2 TeleportTo;
        public double NextTeleportAt;
        public int Teleports;

        // A separate timer makes Summon independent of player distance and pattern streaks.
        public double NextSummonAt;
        public int SummonsResolved;
        public readonly Vector2[] SummonedPositions = new Vector2[2];
        public readonly ActorCategory[] SummonedKinds = new ActorCategory[2];
    }

    /// <summary>
    /// The Collector (section 4, owner-revised in Phase 5 and again after its first playtest,
    /// D38, D48). Four offensive patterns chosen by player position, plus a timed Summon:
    ///   - player hidden behind a pillar (no line of sight) → ground slam: it ignores line of
    ///     sight, so hiding is answered by the one attack a pillar cannot block;
    ///   - point-blank → slam; close → sweeping melee; mid range → fan volley; far → bolt stream.
    /// Ranged patterns fire ordinary bolts through <see cref="AttackEmitter"/> — the same
    /// payload definition acolytes use — so they are capturable and returnable exactly like any
    /// other bolt. The slam cannot be parried; the sweep can, through a gold arc band (D47).
    ///
    /// Why choice by position needs a cap on melee: a player who hugs the boss (or hides) would
    /// otherwise only ever see melee, and the plan forbids "an extended period with neither
    /// targets nor ammunition". After <c>maxMeleeInARow</c> melee patterns the next one is
    /// ranged whatever the distance, so bolts always come back within a couple of patterns.
    ///
    /// When the player is far away it may also TELEPORT behind them (owner direction): seeded
    /// chance, a cooldown so it cannot chain, and a visible wind-up at both ends, so it is a
    /// surprise in position but never an unreadable hit.
    /// </summary>
    public static class CollectorBoss
    {
        public static bool IsMelee(BossPattern p) => p == BossPattern.Sweep || p == BossPattern.Slam;

        public static void Tick(ArenaSim sim, EnemyActor e, BossTuning t, double now, float dt)
        {
            // Spawn warning: present and visible, but harmless and still (section 4).
            if (now < e.ActiveAt) return;
            var b = e.Boss;
            var player = sim.Player;
            if (!player.Alive) return;

            if (e.Phase == EnemyPhase.Warning)
            {
                e.Phase = EnemyPhase.Idle;
                // No teleport on the very first pattern: the intro shows it where it stands.
                b.NextTeleportAt = now + t.teleportCooldown;
                b.NextSummonAt = now + sim.Random.Range(t.summonIntervalMin, t.summonIntervalMax);
                BeginPattern(sim, e, t, now);
            }

            Vector2 toPlayer = player.Position - e.Position;
            float dist = toPlayer.magnitude;
            Vector2 dirToPlayer = dist > 1e-4f ? toPlayer / dist : e.AimDirection;

            switch (b.Stage)
            {
                case BossStage.Teleport:
                    e.AimDirection = dirToPlayer;
                    if (now >= b.StageEndsAt)
                    {
                        // Snap both positions so the view does not draw a streak across the arena.
                        e.Position = e.PrevPosition = b.TeleportTo;
                        b.Teleports++;
                        BeginPattern(sim, e, t, now, allowTeleport: false, afterTeleport: true);
                    }
                    break;

                case BossStage.Reposition:
                    if (b.Pattern == BossPattern.Summon)
                    {
                        // Reserve the cast at a pattern boundary, then wait for its wind-up
                        // time. An overdue summon starts only after the preceding move and
                        // its complete recovery; the timer never interrupts another pattern.
                        if (now >= b.StageEndsAt) StartTelegraph(sim, e, t, now);
                        break;
                    }
                    e.AimDirection = dirToPlayer;
                    if (ReachedPosition(e, b, t, dist) || now >= b.StageEndsAt)
                    {
                        StartTelegraph(sim, e, t, now);
                        break;
                    }
                    // Melee patterns chase the player (routing round pillars); ranged ones walk
                    // to a firing spot.
                    Vector2 target = IsMelee(b.Pattern) ? player.Position : b.MoveTarget;
                    EnemySteering.MoveToward(sim, e, target, t.moveSpeed, dt);
                    break;

                case BossStage.Telegraph:
                    // Aim tracks the player until it locks, so a late sidestep is a real dodge.
                    if (!e.AimLocked)
                    {
                        e.AimDirection = dirToPlayer;
                        if (now >= b.StageEndsAt - AimLockOf(b.Pattern, t)) e.AimLocked = true;
                    }
                    // Sweep parry (D47): any tick while the gold arc is up; it cancels the swing.
                    if (b.Pattern == BossPattern.Sweep && e.ParryRimOpen(now)
                        && sim.TryParrySweep(e, t.sweepHalfAngle, t.sweepParryArcRadius, t.sweepParryArcWidth))
                    {
                        b.SweepsParried++;
                        Recover(e, t, now, t.parriedRecover);
                        break;
                    }
                    // A fan waits for room in the projectile budget (Phase 12, endless only),
                    // holding its locked aim lines on screen, rather than firing part of a fan.
                    if (now >= b.StageEndsAt && (b.Pattern != BossPattern.FanVolley || sim.HostileRoomFor(FanOf(e, t).Length)))
                        StartActive(sim, e, t, now);
                    break;

                case BossStage.Active:
                    TickActive(sim, e, t, now, dt, dirToPlayer);
                    break;

                case BossStage.Recover:
                    if (now >= b.StageEndsAt) BeginPattern(sim, e, t, now);
                    break;
            }
        }

        static void BeginPattern(ArenaSim sim, EnemyActor e, BossTuning t, double now,
            bool allowTeleport = true, bool afterTeleport = false)
        {
            var b = e.Boss;
            var player = sim.Player;
            float dist = (player.Position - e.Position).magnitude;
            e.Phase = EnemyPhase.Idle;
            e.AimLocked = false;
            e.ClearParryRim();

            // Reserve enough time for a complete cast before the next random deadline.
            // The bound includes a possible teleport + melee and a full bolt stream; otherwise
            // a last-second attack would usually stretch the interval. This is considered
            // only at a pattern boundary after full recovery: if a projectile-budget hold
            // runs late, Summon waits for that attack instead of overriding it.
            float offensiveDuration = t.teleportTelegraph + t.repositionMax + t.recover
                + Mathf.Max(t.streamTelegraph + Mathf.Max(0, t.streamShots - 1) * t.streamInterval,
                    Mathf.Max(t.fanTelegraph, Mathf.Max(t.sweepTelegraph + t.sweepDuration, t.slamTelegraph)));
            if (!afterTeleport && now + offensiveDuration + t.summonTelegraph >= b.NextSummonAt)
            {
                BeginSummon(e, t, now);
                return;
            }

            // Far away: a seeded chance to blink behind the player instead of walking over.
            // The roll happens only when every other condition holds, so the random stream
            // (and with it a seed's replay) does not depend on rolls that could never matter.
            // Not at the melee cap (D53): a teleport is always followed by a melee pattern, so
            // teleporting at the cap would break the ammunition guarantee. It walks instead.
            if (allowTeleport && dist >= t.teleportMinDistance && now >= b.NextTeleportAt
                && b.MeleeStreak < t.maxMeleeInARow
                && TryFindTeleportSpot(sim, e, t, out var spot) && sim.Random.NextFloat() < t.teleportChance)
            {
                b.Stage = BossStage.Teleport;
                b.TeleportTo = spot;
                b.StageEndsAt = now + t.teleportTelegraph;
                b.NextTeleportAt = now + t.teleportTelegraph + t.teleportCooldown;
                e.Phase = EnemyPhase.Telegraph;
                e.PhaseEndsAt = b.StageEndsAt;
                sim.Events.RaiseEnemyTelegraph(e);
                return;
            }

            // The very first pattern has no "previous" one to repeat.
            BossPattern? last = b.PatternsStarted > 0 ? b.Pattern : (BossPattern?)null;
            var next = afterTeleport
                ? ChooseAfterTeleport(sim, t, last, b.SameStreak)
                : Choose(sim, e, t, b.MeleeStreak, last, b.SameStreak);
            // Owner direction (D54): slams were seen only beside pillars. The cause: the melee
            // cap (2) equals the repeat cap (2), so sweep, sweep hits the melee cap first and the
            // sweep→slam swap never fires; and the slam band is thin. So a sweep pick sometimes
            // becomes a slam. Both walk in before striking, so either fits wherever a sweep did.
            // Rolled here, not in Choose, so Choose stays a pure function of position for tests;
            // and only on a sweep pick, so seeds that never pick a sweep replay as before.
            if (!afterTeleport && next == BossPattern.Sweep && sim.Random.NextFloat() < t.slamInsteadOfSweepChance
                && !(last == BossPattern.Slam && b.SameStreak >= t.maxSameInARow))
                next = BossPattern.Slam;
            b.SameStreak = last == next ? b.SameStreak + 1 : 1;
            b.Pattern = next;
            b.MeleeStreak = IsMelee(b.Pattern) ? b.MeleeStreak + 1 : 0;
            b.PatternsStarted++;
            b.Stage = BossStage.Reposition;
            b.StageEndsAt = now + t.repositionMax;
            if (!IsMelee(b.Pattern)) b.MoveTarget = FiringSpot(sim, e, t);
            // Owner direction (D55): after a teleport the melee comes almost at once, so skip
            // the walk-in and start the wind-up on arrival. Safe to skip: the boss lands
            // teleportBehindDistance (2.6) away, inside both the sweep's reach (3.0) and the
            // slam's radius (3.2). The wind-up itself stays full length; it is the player's only
            // warning, and the slam cannot be parried.
            if (afterTeleport) StartTelegraph(sim, e, t, now);
        }

        static void BeginSummon(EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Pattern = BossPattern.Summon;
            b.SameStreak = 1;
            b.PatternsStarted++;
            // Preserve MeleeStreak: two Pursuer arrivals cannot replace the boss's guarantee
            // of fresh ammunition after at most two melee attacks.
            b.Stage = BossStage.Reposition;
            b.StageEndsAt = System.Math.Max(now, b.NextSummonAt - t.summonTelegraph);
            e.Phase = EnemyPhase.Idle;
            e.AimLocked = false;
            e.ClearParryRim();
        }

        /// <summary>
        /// Pattern from the player's position (owner direction, D48), public for tests:
        /// the melee cap first (ammunition guarantee), then line of sight, then distance; and
        /// last, no pattern more than <c>maxSameInARow</c> times running (D52).
        /// </summary>
        public static BossPattern Choose(ArenaSim sim, EnemyActor e, BossTuning t, int meleeStreak,
            BossPattern? last = null, int sameInARow = 0)
        {
            var pick = ChooseByPosition(sim, e, t, meleeStreak);
            // Why a swap and not a re-roll: the position table already says what fits where
            // the player stands, so the partner is the next-best fit for that same spot. Melee
            // swaps with melee and ranged with ranged, which keeps the melee cap's ammunition
            // guarantee intact: a forced ranged pick can only become the other ranged pattern.
            if (pick == last && sameInARow >= t.maxSameInARow) pick = PartnerOf(pick);
            return pick;
        }

        static BossPattern ChooseByPosition(ArenaSim sim, EnemyActor e, BossTuning t, int meleeStreak)
        {
            float dist = (sim.Player.Position - e.Position).magnitude;
            if (meleeStreak >= t.maxMeleeInARow)
                return dist <= t.fanMaxDistance ? BossPattern.FanVolley : BossPattern.BoltStream;
            if (!HasLineOfSight(sim, e.Position, sim.Player.Position)) return BossPattern.Slam;
            if (dist <= t.slamChooseDistance) return BossPattern.Slam;
            if (dist <= t.sweepChooseDistance) return BossPattern.Sweep;
            if (dist <= t.fanMaxDistance) return BossPattern.FanVolley;
            return BossPattern.BoltStream;
        }

        /// <summary>
        /// Owner direction (D53): the attack right after a teleport behind the player is the
        /// sweep or the slam, a seeded coin flip, never ranged. Distance can't decide it: the
        /// boss always arrives at teleportBehindDistance, which would pick the same one every
        /// time. The repeat cap still applies, so a third slam in a row becomes a sweep.
        /// </summary>
        static BossPattern ChooseAfterTeleport(ArenaSim sim, BossTuning t, BossPattern? last, int sameInARow)
        {
            var pick = sim.Random.NextFloat() < 0.5f ? BossPattern.Slam : BossPattern.Sweep;
            if (pick == last && sameInARow >= t.maxSameInARow) pick = PartnerOf(pick);
            return pick;
        }

        static BossPattern PartnerOf(BossPattern p)
        {
            switch (p)
            {
                case BossPattern.Slam: return BossPattern.Sweep;
                case BossPattern.Sweep: return BossPattern.Slam;
                case BossPattern.FanVolley: return BossPattern.BoltStream;
                default: return BossPattern.FanVolley;
            }
        }

        /// <summary>True when no wall or pillar crosses the straight line between the two points.</summary>
        public static bool HasLineOfSight(ArenaSim sim, Vector2 from, Vector2 to)
        {
            foreach (var w in sim.Walls)
                if (Geometry2D.SweepCircleVsRect(from, to, 0f, w, out _)) return false;
            return true;
        }

        /// <summary>
        /// "Behind you" = opposite the player's aim, the way they are not looking. If that spot
        /// is blocked (pillar, wall, arena edge) nearby angles are tried, widening both ways;
        /// if none is clear the boss simply walks this time.
        /// </summary>
        static bool TryFindTeleportSpot(ArenaSim sim, EnemyActor e, BossTuning t, out Vector2 spot)
        {
            var p = sim.Player;
            Vector2 behind = p.AimDirection.sqrMagnitude > 1e-6f ? -p.AimDirection.normalized : Vector2.down;
            var bounds = sim.Arena.bounds;
            float m = e.Radius + 0.4f;
            foreach (float off in TeleportAngles)
            {
                Vector2 c = p.Position + Geometry2D.Rotate(behind, off) * t.teleportBehindDistance;
                if (c.x < bounds.xMin + m || c.x > bounds.xMax - m || c.y < bounds.yMin + m || c.y > bounds.yMax - m) continue;
                bool blocked = false;
                foreach (var w in sim.Walls)
                    if (Geometry2D.CircleOverlapsRect(c, e.Radius + 0.2f, w)) { blocked = true; break; }
                if (blocked) continue;
                spot = c;
                return true;
            }
            spot = default;
            return false;
        }

        static readonly float[] TeleportAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        /// <summary>
        /// A spot about <c>rangedDistance</c> from the player, swung a seeded strafeMin..Max
        /// degrees round them from the boss's current side, pulled inside the arena. From there
        /// a fan's full width crosses the player's area with room to read it.
        /// Why the swing (owner, D52: "move around a little more"): without it the boss backs
        /// straight off along the line to the player and, once at range, barely moves between
        /// volleys. Circling makes it travel every time and changes the angle shots come from.
        /// If the swung spot sits in a pillar the mirror is tried, then the ends of the swing
        /// range both ways (pillars are small; one of those is nearly always clear), and only
        /// then the straight-back spot.
        /// </summary>
        static Vector2 FiringSpot(ArenaSim sim, EnemyActor e, BossTuning t)
        {
            Vector2 away = e.Position - sim.Player.Position;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.up;
            float swing = Mathf.Lerp(t.strafeMinDeg, t.strafeMaxDeg, sim.Random.NextFloat());
            if (sim.Random.NextFloat() < 0.5f) swing = -swing;
            foreach (float deg in new[] { swing, -swing, t.strafeMaxDeg, -t.strafeMaxDeg, t.strafeMinDeg, -t.strafeMinDeg, 0f })
            {
                Vector2 spot = Clamped(sim, e, sim.Player.Position + Geometry2D.Rotate(away.normalized, deg) * t.rangedDistance);
                bool blocked = false;
                foreach (var w in sim.Walls)
                    if (Geometry2D.CircleOverlapsRect(spot, e.Radius + 0.2f, w)) { blocked = true; break; }
                if (!blocked || deg == 0f) return spot;
            }
            return e.Position; // unreachable: the loop always returns on its last entry
        }

        static Vector2 Clamped(ArenaSim sim, EnemyActor e, Vector2 spot)
        {
            var bounds = sim.Arena.bounds;
            float m = e.Radius + 0.6f;
            spot.x = Mathf.Clamp(spot.x, bounds.xMin + m, bounds.xMax - m);
            spot.y = Mathf.Clamp(spot.y, bounds.yMin + m, bounds.yMax - m);
            return spot;
        }

        static bool ReachedPosition(EnemyActor e, BossState b, BossTuning t, float distToPlayer)
        {
            switch (b.Pattern)
            {
                // Close enough that most of the blade/blast covers the player's position.
                case BossPattern.Sweep: return distToPlayer <= t.sweepReach * 0.6f;
                case BossPattern.Slam: return distToPlayer <= t.slamRadius * 0.55f;
                default: return (e.Position - b.MoveTarget).sqrMagnitude <= 0.25f * 0.25f;
            }
        }

        public static float TelegraphOf(BossPattern p, BossTuning t)
        {
            switch (p)
            {
                case BossPattern.BoltStream: return t.streamTelegraph;
                case BossPattern.FanVolley: return t.fanTelegraph;
                case BossPattern.Sweep: return t.sweepTelegraph;
                case BossPattern.Summon: return t.summonTelegraph;
                default: return t.slamTelegraph;
            }
        }

        static float AimLockOf(BossPattern p, BossTuning t)
        {
            switch (p)
            {
                case BossPattern.BoltStream: return t.streamAimLock;
                case BossPattern.FanVolley: return t.fanAimLock;
                case BossPattern.Sweep: return t.sweepAimLock;
                case BossPattern.Summon: return t.summonTelegraph;
                // The slam is centred on the boss, so it has no aim to lock: lock at once.
                default: return t.slamTelegraph;
            }
        }

        static void StartTelegraph(ArenaSim sim, EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Stage = BossStage.Telegraph;
            b.TelegraphStartedAt = now;
            b.StageEndsAt = now + TelegraphOf(b.Pattern, t);
            e.Phase = EnemyPhase.Telegraph;
            e.PhaseEndsAt = b.StageEndsAt;
            e.AimLocked = false;
            if (b.Pattern == BossPattern.Summon)
                // Choose once per cast. Waiting for local space must not reroll the pair
                // every tick or turn a blocked summon into a different enemy composition.
                for (int i = 0; i < b.SummonedKinds.Length; i++)
                    b.SummonedKinds[i] = SummonKinds[sim.Random.NextInt(0, SummonKinds.Length)];
            if (b.Pattern == BossPattern.Sweep)
            {
                // Same shape as the Pursuer's rim (D46): up shortly after the wind-up starts,
                // gone before the blade moves.
                e.ParryRimOpensAt = now + t.sweepParryDelay;
                e.ParryRimClosesAt = System.Math.Min(e.ParryRimOpensAt + t.sweepParryDuration, b.StageEndsAt - 0.05);
            }
            sim.Events.RaiseEnemyTelegraph(e);
        }

        /// <summary>The fan this boss fires: the encore fan once it has been beaten before.</summary>
        public static float[] FanOf(EnemyActor e, BossTuning t) =>
            e.Boss != null && e.Boss.Encore ? t.fanSpreadRepeatDeg : t.fanSpreadDeg;

        static void StartActive(ArenaSim sim, EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            // Plan both local arrivals before spawning either. A crowded cast holds its
            // completed wind-up rather than producing half a pair or spawning across the arena.
            if (b.Pattern == BossPattern.Summon && !FindSummonPair(sim, e, t)) return;
            b.Stage = BossStage.Active;
            e.Phase = EnemyPhase.Idle;
            e.ClearParryRim();
            switch (b.Pattern)
            {
                case BossPattern.BoltStream:
                    b.ShotsLeft = t.streamShots;
                    b.NextShotAt = now; // first bolt on the tick the wind-up ends
                    break;

                case BossPattern.FanVolley:
                    AttackEmitter.FireVolley(sim, AttackIds.Bolt, e.ActorId, e.Position, e.Radius, e.AimDirection,
                        FanOf(e, t), t.hitDamage, range: t.fanRange, speedScale: t.rangedProjectileSpeedScale);
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now, t.recover);
                    return;

                case BossPattern.Summon:
                    for (int i = 0; i < b.SummonedPositions.Length; i++)
                        sim.SpawnEnemy(b.SummonedKinds[i], b.SummonedPositions[i], scaleForCycle: true);
                    b.SummonsResolved++;
                    b.NextSummonAt = now + sim.Random.Range(t.summonIntervalMin, t.summonIntervalMax);
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now, t.recover);
                    return;

                case BossPattern.Sweep:
                    // Swing direction follows the boss's strafe preference so it is not always
                    // the same side, but it is fixed per boss: still learnable.
                    b.BladeDeg = -t.sweepHalfAngle * e.StrafeSign;
                    b.BladeEndDeg = t.sweepHalfAngle * e.StrafeSign;
                    b.SweepLanded = false;
                    // Test the starting edge too, so a player standing exactly on it is not skipped.
                    SweepSegment(sim, e, t, b.BladeDeg, b.BladeDeg);
                    break;

                case BossPattern.Slam:
                    // One overlap test at one instant, like the Pursuer strike: no lingering
                    // zone. No line-of-sight test: a pillar does not shelter you from the ground
                    // shaking — that is the slam's whole job (D48). It cannot be parried.
                    var p = sim.Player;
                    float r = t.slamRadius + p.Radius;
                    if ((p.Position - e.Position).sqrMagnitude <= r * r) sim.DamagePlayer(t.hitDamage, e.ActorId);
                    b.StrikesResolved++;
                    // Hostile burst event: the view draws it at its true radius (visual only;
                    // hostile explosions never damage enemies).
                    sim.Events.RaiseExplosion(e.Position, t.slamRadius, AttackFaction.Hostile);
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now, t.recover);
                    return;
            }
        }

        static void TickActive(ArenaSim sim, EnemyActor e, BossTuning t, double now, float dt, Vector2 dirToPlayer)
        {
            var b = e.Boss;
            if (b.Pattern == BossPattern.BoltStream)
            {
                // The stream follows the player at a limited turn rate: strafing outruns it,
                // standing still does not. That is the dodge it teaches.
                float maxTurn = t.streamTurnRate * dt;
                float want = Vector2.SignedAngle(e.AimDirection, dirToPlayer);
                e.AimDirection = Geometry2D.Rotate(e.AimDirection, Mathf.Clamp(want, -maxTurn, maxTurn)).normalized;
                while (b.ShotsLeft > 0 && now >= b.NextShotAt - 1e-9)
                {
                    // Budget full: the stream pauses (still aiming) and resumes from now,
                    // so the held bolts are delayed, not lost or fired in a burst later.
                    if (!sim.HostileRoomFor(1)) { b.NextShotAt = now; break; }
                    AttackEmitter.FireVolley(sim, AttackIds.Bolt, e.ActorId, e.Position, e.Radius, e.AimDirection,
                        ZeroSpread, t.hitDamage, unlimited: t.streamUnlimited, speedScale: t.rangedProjectileSpeedScale);
                    sim.Events.RaiseEnemyFired(e);
                    b.ShotsLeft--;
                    b.NextShotAt += t.streamInterval;
                }
                if (b.ShotsLeft <= 0) Recover(e, t, now, t.recover);
                return;
            }

            if (b.Pattern == BossPattern.Sweep)
            {
                float speed = 2f * t.sweepHalfAngle / Mathf.Max(0.01f, t.sweepDuration);
                float from = b.BladeDeg;
                float to = Mathf.MoveTowards(from, b.BladeEndDeg, speed * dt);
                b.BladeDeg = to;
                SweepSegment(sim, e, t, from, to);
                if (Mathf.Approximately(to, b.BladeEndDeg))
                {
                    b.StrikesResolved++;
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now, t.recover);
                }
            }
        }

        static readonly float[] ZeroSpread = { 0f };
        static readonly ActorCategory[] SummonKinds = { ActorCategory.Acolyte, ActorCategory.Pursuer,
            ActorCategory.ScatterCaster, ActorCategory.SiegeFamiliar };

        static bool FindSummonPair(ArenaSim sim, EnemyActor e, BossTuning t)
        {
            var b = e.Boss;
            float firstRadius = sim.Config.combat.For(b.SummonedKinds[0]).bodyRadius;
            float secondRadius = sim.Config.combat.For(b.SummonedKinds[1]).bodyRadius;
            // A first legal position can occupy the only place the second body fits.
            // Try several pairs before holding the cast, always keeping the same two kinds.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (!sim.TryFindSpawnPointNear(firstRadius, e.Position, t.summonSpawnMinDistance,
                    t.summonSpawnMaxDistance, out var first)) return false;
                if (!sim.TryFindSpawnPointNear(secondRadius, e.Position, t.summonSpawnMinDistance,
                    t.summonSpawnMaxDistance, out var second, first, firstRadius)) continue;
                b.SummonedPositions[0] = first;
                b.SummonedPositions[1] = second;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The blade swept from <paramref name="fromDeg"/> to <paramref name="toDeg"/> (relative
        /// to the locked aim) this tick. Tested as a swept WEDGE, not the blade's end position,
        /// so a fast swing can never step over the player between ticks. The angular pad is
        /// the player's body as seen from the boss, so the edge of the body counts.
        /// One hit per sweep at most.
        /// </summary>
        static void SweepSegment(ArenaSim sim, EnemyActor e, BossTuning t, float fromDeg, float toDeg)
        {
            var b = e.Boss;
            var p = sim.Player;
            if (b.SweepLanded || !p.Alive) return;
            if (BladeTouches(e.Position, e.AimDirection, fromDeg, toDeg, t.sweepReach, p.Position, p.Radius))
                b.SweepLanded = sim.DamagePlayer(t.hitDamage, e.ActorId);
        }

        /// <summary>Pure geometry for the sweep, public for tests.</summary>
        public static bool BladeTouches(Vector2 origin, Vector2 aim, float fromDeg, float toDeg, float reach,
            Vector2 target, float targetRadius)
        {
            Vector2 v = target - origin;
            float d = v.magnitude;
            if (d > reach + targetRadius) return false;
            if (d <= targetRadius) return true; // standing on the boss: the hilt hits
            float a = Vector2.SignedAngle(aim, v);
            float pad = Mathf.Asin(Mathf.Clamp01(targetRadius / d)) * Mathf.Rad2Deg;
            float lo = Mathf.Min(fromDeg, toDeg) - pad, hi = Mathf.Max(fromDeg, toDeg) + pad;
            return a >= lo && a <= hi;
        }

        static void Recover(EnemyActor e, BossTuning t, double now, float seconds)
        {
            var b = e.Boss;
            b.Stage = BossStage.Recover;
            b.StageEndsAt = now + seconds;
            e.Phase = EnemyPhase.Recover;
            e.AimLocked = false;
            e.ClearParryRim();
        }
    }
}
