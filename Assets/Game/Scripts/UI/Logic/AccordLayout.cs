using System.Collections.Generic;
using BorrowedHex.Progression;
using UnityEngine;

namespace BorrowedHex.UI
{
    public enum NodeState { Owned, CheatActive, Available, Locked }

    /// <summary>
    /// Geometry and state of the Broken Accord (spec 3): the skill tree drawn as a ritual seal,
    /// four branches leaving a torn Ledger scrap on the diagonals. Pure: no Unity objects, so
    /// every rule about where things sit and what they mean is tested without a canvas. Visual
    /// positions are deliberately separate from progression data (spec 4.3).
    ///
    /// Map-local reference pixels, origin at the map's centre, +y up.
    /// </summary>
    public static class AccordLayout
    {
        public static readonly Vector2 MapSize = new Vector2(1040, 800);
        public const float CoreRadius = 88f;
        // Dot pitch: 4 art pixels. The plan's 8-art-pixel pitch left the short tier-1 path two
        // dots, and tiers 2-3 none at all, once the seals were cut out (Task 10 ruling).
        public const float Spacing = 8f;
        // Margin kept clear around each seal, so a path visibly stops short of it.
        const float SealGap = 6f;
        // Sideways push of each path's control point, as a share of the segment length: the
        // branch reads as a drawn working rather than a straight spoke.
        const float Bend = 0.18f;

        // Radii and seal sizes are sized so each path keeps at least four dots between its two
        // seals while the outer seal plus its name still fits in the map's half-height:
        // 424 * cos 45 = 300, + 44 (half the outer seal) + 40 (name) = 384 <= 400.
        public static float Radius(int tier) => tier == 1 ? 176f : tier == 2 ? 296f : 424f;
        public static float SealSize(int tier) => tier == 3 ? 88f : 72f;

        /// <summary>The two broken rings sit halfway between the tiers, so they never cross a seal.</summary>
        public static readonly float[] RingRadii = { (176f + 296f) / 2f, (296f + 424f) / 2f };
        /// <summary>Each ring stops this many degrees short of every branch axis on both sides.</summary>
        public const float RingGapDegrees = 24f;
        public static readonly float[] BranchAxes = { 45f, 135f, 225f, 315f };

        public static Vector2 Direction(SkillBranch b)
        {
            float deg = b switch { SkillBranch.Precision => 135f, SkillBranch.Mobility => 45f, SkillBranch.Resilience => 225f, _ => 315f };
            return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        }

        public static Vector2 NodePosition(SkillNode n) => Direction(n.Branch) * Radius(n.Tier);

        /// <summary>Dotted connection INTO <paramref name="n"/>: from its prerequisite's seal, or from the core.</summary>
        public static List<Vector2> PathDots(SkillNode n)
        {
            var pre = SkillTree.Prerequisite(n);
            var dir = Direction(n.Branch);
            Vector2 a = pre != null ? NodePosition(pre) : dir * CoreRadius;
            float skipA = pre != null ? SealSize(pre.Tier) / 2f + SealGap : SealGap;
            Vector2 b = NodePosition(n);
            float skipB = SealSize(n.Tier) / 2f + SealGap;
            // Control point: the midpoint pushed clockwise (right of travel), so every branch
            // swirls the same way and the seal reads as one rotating figure.
            var mid = (a + b) * 0.5f;
            var side = new Vector2(dir.y, -dir.x);
            var c = mid + side * Vector2.Distance(a, b) * Bend;

            var dots = new List<Vector2>();
            // Walk the curve finely and drop a dot each time the arc length passes Spacing, so
            // the pitch is even along the bend, not just along the chord.
            Vector2 prev = a; float run = 0f;
            for (int i = 1; i <= 400; i++)
            {
                float t = i / 400f;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                run += Vector2.Distance(prev, p); prev = p;
                if (run < Spacing) continue;
                run = 0f;
                if (Vector2.Distance(p, a) < skipA || Vector2.Distance(p, b) < skipB) continue;
                // Snapped to the 2-reference-pixel grid: one art pixel at the UI's PPU.
                dots.Add(new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f));
            }
            return dots;
        }

        public static NodeState State(PlayerProfile p, SkillNode n)
        {
            if (p.ownedNodes.Contains(n.Id)) return NodeState.Owned;
            // IsOwned also answers true for every node under the All-skills cheat (SkillTree.IsOwned),
            // so owned-but-not-in-ownedNodes is exactly "active through cheats".
            if (SkillTree.IsOwned(p, n.Id)) return NodeState.CheatActive;
            return SkillTree.WhyCannotBuy(p, n.Id) == null ? NodeState.Available : NodeState.Locked;
        }
    }
}
