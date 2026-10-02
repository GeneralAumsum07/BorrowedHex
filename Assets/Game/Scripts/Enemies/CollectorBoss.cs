using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    public enum BossPattern { BoltStream, Sweep, FanVolley, Slam }

    /// <summary>
    /// Where the boss is inside one pattern. Every pattern runs the same four steps, so every
    /// attack has the same readable shape: move, wind up (visible), strike, breathe.
    /// </summary>
    public enum BossStage { Reposition, Telegraph, Active, Recover }

    /// <summary>Boss-only state, hung off the shared EnemyActor so damage, kill events and the
    /// view's body handling stay one code path for every enemy.</summary>
    public sealed class BossState
    {
        public BossPattern Pattern;
        public BossStage Stage;
        public double StageEndsAt;
        /// <summary>Patterns started so far; the cycle position is this modulo the cycle length.</summary>
        public int PatternsStarted;

        public Vector2 MoveTarget;

        // Bolt stream.
        public int ShotsLeft;
        public double NextShotAt;

        // Sweep: the blade's current angle relative to the locked aim, and where it ends.
        public float BladeDeg;
        public float BladeEndDeg;
        public bool SweepLanded;
        /// <summary>True once the slam/sweep has resolved (for views and tests).</summary>
        public int StrikesResolved;
    }

    /// <summary>
    /// The Collector (section 4, owner-revised for Phase 5, D38). A fixed cycle:
    ///   bolt stream → sweeping melee → fan volley → ground slam → (repeat)
    /// Ranged patterns fire ordinary bolts through <see cref="AttackEmitter"/> — the same
    /// payload definition acolytes use — so they are capturable and returnable exactly like any
    /// other bolt (section 4: "through existing payload definitions"). The melee patterns are
    /// avoidable hazards only and cannot be parried (D38 ruling: section 4 says melee attacks
    /// are "not automatically stealable"; the Pursuer parry is the owner's explicit exception).
    ///
    /// Why a fixed cycle rather than random picks: a player learning a boss in a 60-second
    /// window needs to predict it, and alternating ranged/melee guarantees ammunition never
    /// goes more than one pattern without arriving (plan: no extended period with neither
    /// targets nor ammunition).
    /// </summary>
    public static class CollectorBoss
    {
        public static readonly BossPattern[] Cycle =
            { BossPattern.BoltStream, BossPattern.Sweep, BossPattern.FanVolley, BossPattern.Slam };

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
                BeginPattern(sim, e, t, now);
            }

            Vector2 toPlayer = player.Position - e.Position;
            float dist = toPlayer.magnitude;
            Vector2 dirToPlayer = dist > 1e-4f ? toPlayer / dist : e.AimDirection;

            switch (b.Stage)
            {
                case BossStage.Reposition:
                    e.AimDirection = dirToPlayer;
                    if (ReachedPosition(e, b, t, dist) || now >= b.StageEndsAt)
                    {
                        StartTelegraph(sim, e, t, now);
                        break;
                    }
                    // Melee patterns chase the player; ranged ones walk to a firing spot.
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
                    if (now >= b.StageEndsAt) StartActive(sim, e, t, now);
                    break;

                case BossStage.Active:
                    TickActive(sim, e, t, now, dt, dirToPlayer);
                    break;

                case BossStage.Recover:
                    if (now >= b.StageEndsAt) BeginPattern(sim, e, t, now);
                    break;
            }
        }

        static void BeginPattern(ArenaSim sim, EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Pattern = Cycle[b.PatternsStarted % Cycle.Length];
            b.PatternsStarted++;
            b.Stage = BossStage.Reposition;
            b.StageEndsAt = now + t.repositionMax;
            e.Phase = EnemyPhase.Idle;
            e.AimLocked = false;
            if (!IsMelee(b.Pattern)) b.MoveTarget = FiringSpot(sim, e, t);
        }

        /// <summary>
        /// A spot about <c>rangedDistance</c> from the player on the boss's side, pulled inside
        /// the arena. From there a fan's full width crosses the player's area with room to read it.
        /// </summary>
        static Vector2 FiringSpot(ArenaSim sim, EnemyActor e, BossTuning t)
        {
            Vector2 away = e.Position - sim.Player.Position;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.up;
            Vector2 spot = sim.Player.Position + away.normalized * t.rangedDistance;
            var bounds = sim.Config.arena.bounds;
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
                // The slam is centred on the boss, so it has no aim to lock: lock at once.
                default: return t.slamTelegraph;
            }
        }

        static void StartTelegraph(ArenaSim sim, EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Stage = BossStage.Telegraph;
            b.StageEndsAt = now + TelegraphOf(b.Pattern, t);
            e.Phase = EnemyPhase.Telegraph;
            e.PhaseEndsAt = b.StageEndsAt;
            e.AimLocked = false;
            sim.Events.RaiseEnemyTelegraph(e);
        }

        static void StartActive(ArenaSim sim, EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Stage = BossStage.Active;
            e.Phase = EnemyPhase.Idle;
            switch (b.Pattern)
            {
                case BossPattern.BoltStream:
                    b.ShotsLeft = t.streamShots;
                    b.NextShotAt = now; // first bolt on the tick the wind-up ends
                    break;

                case BossPattern.FanVolley:
                    AttackEmitter.FireVolley(sim, AttackIds.Bolt, e.ActorId, e.Position, e.Radius, e.AimDirection, t.fanSpreadDeg);
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now);
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
                    // One overlap test at one instant, like the Pursuer strike: no lingering zone.
                    var p = sim.Player;
                    float r = t.slamRadius + p.Radius;
                    if ((p.Position - e.Position).sqrMagnitude <= r * r) sim.DamagePlayer(1, e.ActorId);
                    b.StrikesResolved++;
                    // Hostile burst event: the view draws it at its true radius (visual only;
                    // hostile explosions never damage enemies).
                    sim.Events.RaiseExplosion(e.Position, t.slamRadius, Core.AttackFaction.Hostile);
                    sim.Events.RaiseEnemyFired(e);
                    Recover(e, t, now);
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
                e.AimDirection = Core.Geometry2D.Rotate(e.AimDirection, Mathf.Clamp(want, -maxTurn, maxTurn)).normalized;
                while (b.ShotsLeft > 0 && now >= b.NextShotAt - 1e-9)
                {
                    AttackEmitter.FireVolley(sim, AttackIds.Bolt, e.ActorId, e.Position, e.Radius, e.AimDirection, ZeroSpread);
                    sim.Events.RaiseEnemyFired(e);
                    b.ShotsLeft--;
                    b.NextShotAt += t.streamInterval;
                }
                if (b.ShotsLeft <= 0) Recover(e, t, now);
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
                    Recover(e, t, now);
                }
            }
        }

        static readonly float[] ZeroSpread = { 0f };

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
                b.SweepLanded = sim.DamagePlayer(1, e.ActorId);
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

        static void Recover(EnemyActor e, BossTuning t, double now)
        {
            var b = e.Boss;
            b.Stage = BossStage.Recover;
            b.StageEndsAt = now + t.recover;
            e.Phase = EnemyPhase.Recover;
            e.AimLocked = false;
        }
    }
}
