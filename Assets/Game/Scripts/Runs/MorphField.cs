using System;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// The reality-glitch morph's patch field (owner-approved design, 2026-10-04). The shared
    /// 48x36 footprint is cut into Columns x Rows irregular patches; each patch, once
    /// triggered, runs a fixed Unstable + Grow timeline on gameplay time and then belongs to
    /// the incoming arena. The sim owns this so collision (cover leaving and joining Walls)
    /// and the shader read exactly the same patch timings.
    /// </summary>
    public sealed class MorphField
    {
        public const int Columns = 6, Rows = 4, Count = Columns * Rows;
        // The unstable shimmer, then the outward growth. Fixed per patch, independent of kills,
        // so the motion is always smooth even when the kill rate is not.
        public const float Unstable = .6f, Grow = 1.2f, Duration = Unstable + Grow;
        // Cover blocked by an occupant fades from almost-solid to a ghost over WaitFade, and
        // once free fills in over LateFill. The field is held Settle past its last patch so
        // both fades read the patch timings they started from and never jump.
        public const float WaitFade = .3f, LateFill = .5f, Settle = WaitFade + LateFill;

        public readonly Vector2 Origin, Cell;
        readonly Vector2[] seeds = new Vector2[Count];
        readonly double[] startedAt = new double[Count];
        public int Triggered { get; private set; }

        public MorphField(Rect bounds, SeededRandom random)
        {
            Origin = bounds.min;
            Cell = new Vector2(bounds.width / Columns, bounds.height / Rows);
            for (int i = 0; i < Count; i++)
            {
                int column = i % Columns, row = i / Columns;
                // Jittered inside the middle 70% of its grid cell: irregular enough to hide the
                // grid, and it keeps the shader's 3x3-cell nearest-seed search exact.
                seeds[i] = Origin + Vector2.Scale(Cell, new Vector2(
                    column + .15f + .7f * random.NextFloat(), row + .15f + .7f * random.NextFloat()));
                startedAt[i] = double.PositiveInfinity;
            }
        }

        public Vector2 Seed(int index) => seeds[index];
        public double StartedAt(int index) => startedAt[index];
        public bool IsTriggered(int index) => !double.IsPositiveInfinity(startedAt[index]);
        public bool Complete(int index, double now) => IsTriggered(index) && now - startedAt[index] >= Duration - 1e-6;

        /// <summary>0 untriggered, 1 complete; linear in time between.</summary>
        public float Local(int index, double now)
            => IsTriggered(index) ? Mathf.Clamp01((float)((now - startedAt[index]) / Duration)) : 0;

        /// <summary>Mean patch progress; drives the lighting blend and when the old art is released.</summary>
        public float Progress(double now)
        {
            float sum = 0;
            for (int i = 0; i < Count; i++) sum += Local(i, now);
            return sum / Count;
        }

        public bool AllComplete(double now)
        {
            for (int i = 0; i < Count; i++) if (!Complete(i, now)) return false;
            return true;
        }

        /// <summary>The patch a world point belongs to: plain nearest seed (the shader's
        /// noise-warped outline only moves the visible border, not cover ownership).</summary>
        public int PatchAt(Vector2 point)
        {
            int best = 0; float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                float distance = (seeds[i] - point).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        public int NearestUntriggered(Vector2 point)
        {
            int best = -1; float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                if (IsTriggered(i)) continue;
                float distance = (seeds[i] - point).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        public int RandomUntriggered(SeededRandom random)
        {
            int left = Count - Triggered;
            if (left <= 0) return -1;
            int pick = random.NextInt(0, left);
            for (int i = 0; i < Count; i++)
                if (!IsTriggered(i) && pick-- == 0) return i;
            return -1;
        }

        public void Trigger(int index, double at)
        {
            if (index < 0 || IsTriggered(index)) return;
            startedAt[index] = at; Triggered++;
        }

        /// <summary>Finish everything immediately (a stage change interrupting this morph).</summary>
        public void CompleteAll(double now)
        {
            for (int i = 0; i < Count; i++)
                if (!Complete(i, now)) startedAt[i] = now - Duration;
            Triggered = Count;
        }

        /// <summary>
        /// How many patches should have been triggered while the encounter's kills drive the
        /// morph: KillShare of the field by the last kill, and at least one from the first, so
        /// the opening kill visibly cracks the world.
        /// </summary>
        public static int KillTarget(int kills, int left, float killShare)
        {
            if (kills <= 0) return 0;
            float share = kills / (float)Math.Max(1, kills + left);
            return Mathf.Clamp(Mathf.FloorToInt(share * killShare * Count + 1e-4f), 1, Count);
        }

        /// <summary>The time-driven remainder: from `from` triggered to all of them over `seconds`.</summary>
        public static int TimedTarget(int from, double elapsed, float seconds)
        {
            float t = Mathf.Clamp01((float)(elapsed / Math.Max(.001f, seconds)));
            return Mathf.Clamp(from + Mathf.CeilToInt((Count - from) * t - 1e-4f), from, Count);
        }
    }
}
