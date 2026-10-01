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
            e.Position = PlayerMotor.SweepMove(e.Position, dir.normalized * (speed * dt), e.Radius, sim.Walls, out bool blocked);
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
            else if (dist > t.preferredMax) want = dir;
            else { want = new Vector2(-dir.y, dir.x) * e.StrafeSign; speed *= 0.5f; }
            if (Move(sim, e, want, speed, dt)) e.StrafeSign = -e.StrafeSign;
        }

        /// <summary>Head straight for the player (pursuer). Wall sliding handles pillar corners.</summary>
        public static void Seek(ArenaSim sim, EnemyActor e, float speed, float dt)
            => Move(sim, e, sim.Player.Position - e.Position, speed, dt);

        /// <summary>
        /// Pick a fresh firing spot inside the band at a new angle around the player, so the
        /// next fan comes from a different direction (Scatter Caster's purpose: it "tests
        /// interception angles"). Uses the run's seeded RNG for reproducibility.
        /// </summary>
        public static Vector2 PickRepositionTarget(ArenaSim sim, EnemyActor e, EnemyTuning t)
        {
            Vector2 fromPlayer = e.Position - sim.Player.Position;
            float baseAngle = Mathf.Atan2(fromPlayer.y, fromPlayer.x) * Mathf.Rad2Deg;
            var b = sim.Config.arena.bounds;
            float margin = e.Radius + 0.6f;
            Vector2 best = e.Position;
            for (int i = 0; i < 12; i++)
            {
                // 40-110 degrees around the player, either side: a visible change of angle
                // without crossing the whole arena.
                float swing = sim.Random.Range(40f, 110f) * (sim.Random.NextFloat() < 0.5f ? -1f : 1f);
                float dist = sim.Random.Range(t.preferredMin, t.preferredMax);
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
