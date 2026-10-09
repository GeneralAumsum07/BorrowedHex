using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Enemies
{
    /// <summary>
    /// Flat-arena steering shared by every brain (section 4: simple steering, no navmesh until
    /// the arena needs one). All movement goes through PlayerMotor.SweepMove, so enemies use the
    /// same wall-sliding collision as the player and can never tunnel into a pillar.
    /// </summary>
    public static class EnemySteering
    {
        /// <summary>Move along <paramref name="dir"/> (any length; normalised here) at the given speed.</summary>
        public static bool Move(ArenaSim sim, EnemyActor e, Vector2 dir, float speed, float dt)
        {
            if (dir.sqrMagnitude < 1e-8f) return false;
            e.Position = PlayerMotor.SweepMove(e.Position, dir.normalized * (speed * e.MoveScale * dt), e.Radius, sim.Walls, out bool blocked);
            return blocked;
        }

        /// <summary>
        /// Stay inside the [min, max] distance band from the player; strafe while inside it so a
        /// ranged enemy is never a static target. Bumping a wall flips the strafe direction
        /// instead of grinding against it.
        /// </summary>
        public static void KeepBand(ArenaSim sim, EnemyActor e, EnemyTuning t, float dt)
        {
            Vector2 toPlayer = sim.Player.Position - e.Position;
            float dist = toPlayer.magnitude;
            if (dist < 1e-4f) return;
            Vector2 dir = toPlayer / dist;
            Vector2 want;
            float speed = t.moveSpeed;
            if (dist < t.preferredMin) want = -dir;
            // Closing in is the one band move that can be stopped dead by a pillar face (D37),
            // so it routes; backing off and strafing already bounce off walls via StrafeSign.
            else if (dist > t.preferredMax) want = Waypoint(sim, e, sim.Player.Position) - e.Position;
            else { want = new Vector2(-dir.y, dir.x) * e.StrafeSign; speed *= 0.5f; }
            if (Move(sim, e, want, speed, dt)) e.StrafeSign = -e.StrafeSign;
        }

        /// <summary>Head for the player (pursuer), routing around any pillar in the way (D37).</summary>
        public static void Seek(ArenaSim sim, EnemyActor e, float speed, float dt)
            => MoveToward(sim, e, sim.Player.Position, speed, dt);

        /// <summary>Move toward a point, detouring around the pillar that blocks the straight line.</summary>
        public static bool MoveToward(ArenaSim sim, EnemyActor e, Vector2 target, float speed, float dt)
            => Move(sim, e, Waypoint(sim, e, target) - e.Position, speed, dt);

        // Clearance past the body radius at each corner node: comfortably more than
        // SightMargin, so a corner never reads as touching its own pillar.
        const float CornerPad = 0.15f;

        /// <summary>
        /// Where to steer this tick to reach <paramref name="target"/> (D37, owner bug: pursuers
        /// pinned behind pillars). Wall sliding alone fails when the line hits a face square-on:
        /// the push has no sideways component, so the body stops dead.
        ///
        /// Not a navmesh on purpose: a handful of convex pillars in an open box is exactly the
        /// case a corner visibility graph solves. If the straight line is blocked, run a
        /// shortest path over: here, the target, and the padded corners of every pillar, with an
        /// edge wherever a sweep is clear; steer at the first node on that path.
        ///
        /// Why every pillar and not just the one in the way: the far side of the first pillar
        /// can itself be blocked by the next (two pillars share each x column), and a graph
        /// with no route left the acolyte frozen (caught by the routing tests).
        /// Why a path and not "best single corner": rounding a pillar from dead behind takes TWO
        /// corners, and a one-step cost (here→corner + corner→target) rated the corner already
        /// underfoot as cheapest, so the enemy parked on it.
        ///
        /// Edges are only tested lazily inside Dijkstra and the whole thing only runs while the
        /// direct line is blocked; it is re-evaluated each tick, so nothing goes stale when the
        /// player moves.
        /// </summary>
        public static Vector2 Waypoint(ArenaSim sim, EnemyActor e, Vector2 target)
        {
            // Sight tests use the body radius plus a margin, never less: a shrunken test radius
            // let the planner draw a diagonal past a corner that the real body could not fit
            // through, pinning it again (seen in a tick trace: 0.38 test vs 0.40 body).
            // A body already resting on a face is handled by FirstBlocker's touching rule.
            float sight = e.Radius + SightMargin;
            if (FirstBlocker(sim, e.Position, target, sight) < 0) return target;

            // Node 0 = here, 1 = target, then four padded corners per pillar. Boundary walls
            // are skipped: their corners lie outside the arena and could never be reached.
            var b = sim.Arena.bounds;
            float pad = e.Radius + CornerPad;
            int n = 0;
            Ensure(2 + 4 * sim.Walls.Count);
            Nodes[n++] = e.Position;
            Nodes[n++] = target;
            foreach (var w in sim.Walls)
            {
                if (w.xMin < b.xMin || w.xMax > b.xMax || w.yMin < b.yMin || w.yMax > b.yMax) continue;
                Nodes[n++] = new Vector2(w.xMin - pad, w.yMin - pad);
                Nodes[n++] = new Vector2(w.xMax + pad, w.yMin - pad);
                Nodes[n++] = new Vector2(w.xMin - pad, w.yMax + pad);
                Nodes[n++] = new Vector2(w.xMax + pad, w.yMax + pad);
            }

            for (int i = 0; i < n; i++) { Dist[i] = float.MaxValue; Prev[i] = -1; Done[i] = false; }
            Dist[0] = 0f;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!Done[i] && Dist[i] < float.MaxValue && (u < 0 || Dist[i] < Dist[u])) u = i;
                if (u < 0 || u == 1) break; // unreachable rest, or target settled
                Done[u] = true;
                for (int v = 1; v < n; v++)
                {
                    if (Done[v]) continue;
                    float d = Dist[u] + (Nodes[v] - Nodes[u]).magnitude;
                    // Sweep test last: it is the only costly part. A corner hidden by another
                    // pillar, or pressed against the arena edge, simply gets no edges.
                    if (d < Dist[v] && FirstBlocker(sim, Nodes[u], Nodes[v], sight) < 0) { Dist[v] = d; Prev[v] = u; }
                }
            }
            // No route at all: fall back to the straight push (pre-D37 behaviour) rather than
            // freezing; wall sliding still makes progress on anything but a square-on face.
            if (Prev[1] < 0) return target;

            // Walk back to the first hop. Skip a hop that is already underfoot, otherwise an
            // enemy standing on a corner would "steer" at its own position and stop.
            int hop = 1;
            while (Prev[hop] > 0 && (Nodes[Prev[hop]] - e.Position).sqrMagnitude > ArrivedSq) hop = Prev[hop];
            return Nodes[hop];
        }

        // Covers SweepMove's skin and float noise, so "clear" here means the move really fits.
        const float SightMargin = 0.03f;

        // Scratch buffers: the sim is single-threaded and this runs per enemy per tick, so
        // reusing them keeps steering allocation-free (WebGL GC pauses show as hitches).
        static Vector2[] Nodes = new Vector2[0];
        static float[] Dist = new float[0];
        static int[] Prev = new int[0];
        static bool[] Done = new bool[0];

        static void Ensure(int size)
        {
            if (Nodes.Length >= size) return;
            Nodes = new Vector2[size]; Dist = new float[size]; Prev = new int[size]; Done = new bool[size];
        }

        // A corner within ~1 tick of travel counts as reached.
        const float ArrivedSq = 0.08f * 0.08f;

        /// <summary>
        /// Index of the earliest wall a circle of radius r would hit going a→b, or -1.
        ///
        /// Touching rule (mirrors SweepMove): if a already lies within r of a wall, that wall
        /// blocks only a move that heads INTO it. Sound because the walls are convex: if the
        /// first step does not reduce the distance to the nearest point, the distance never
        /// drops along the rest of the straight line. Without this, an enemy flush against a
        /// pillar would see every line as blocked and could never plan its way off the face.
        /// </summary>
        static int FirstBlocker(ArenaSim sim, Vector2 a, Vector2 b, float r)
        {
            int hit = -1;
            float bestT = float.MaxValue;
            Vector2 d = b - a;
            for (int i = 0; i < sim.Walls.Count; i++)
            {
                Rect w = sim.Walls[i];
                if (a.x >= w.xMin - r && a.x <= w.xMax + r && a.y >= w.yMin - r && a.y <= w.yMax + r)
                {
                    Vector2 n = a - new Vector2(Mathf.Clamp(a.x, w.xMin, w.xMax), Mathf.Clamp(a.y, w.yMin, w.yMax));
                    if (Vector2.Dot(n, d) >= 0f) continue;
                    return i; // pushing into a wall already touched: blocked at once
                }
                if (Geometry2D.SweepCircleVsRect(a, b, r, w, out float t) && t < bestT) { bestT = t; hit = i; }
            }
            return hit;
        }

        /// <summary>
        /// Pick a fresh firing spot inside the band at a new angle around the player, so the
        /// next fan comes from a different direction (Scatter Caster's purpose: it "tests
        /// interception angles"). Uses the run's seeded RNG for reproducibility.
        /// </summary>
        public static Vector2 PickRepositionTarget(ArenaSim sim, EnemyActor e, EnemyTuning t)
        {
            Vector2 fromPlayer = e.Position - sim.Player.Position;
            float baseAngle = Mathf.Atan2(fromPlayer.y, fromPlayer.x) * Mathf.Rad2Deg;
            var b = sim.Arena.bounds;
            float margin = e.Radius + 0.6f;
            Vector2 best = e.Position;
            // Never pick a spot the next volley could not reach from: Scatter's band (5.5-8.5)
            // runs past its 8-unit range, and an out-of-reach spot would only make it walk
            // straight back in (RangedCaster gates firing on reach). Min guards a band that
            // sits entirely beyond reach.
            float maxDist = Mathf.Min(t.preferredMax, RangedCaster.EngageDistance(t));
            float minDist = Mathf.Min(t.preferredMin, maxDist);
            for (int i = 0; i < 12; i++)
            {
                // 40-110 degrees around the player, either side: a visible change of angle
                // without crossing the whole arena.
                float swing = sim.Random.Range(40f, 110f) * (sim.Random.NextFloat() < 0.5f ? -1f : 1f);
                float dist = sim.Random.Range(minDist, maxDist);
                Vector2 p = sim.Player.Position + Geometry2D.Rotate(Vector2.right, baseAngle + swing) * dist;
                if (p.x < b.xMin + margin || p.x > b.xMax - margin || p.y < b.yMin + margin || p.y > b.yMax - margin) continue;
                bool clear = true;
                foreach (var w in sim.Walls) if (Geometry2D.CircleOverlapsRect(p, e.Radius + 0.2f, w)) { clear = false; break; }
                if (clear) return p;
            }
            return best;
        }
    }
}
