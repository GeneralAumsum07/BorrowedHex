using System.Linq;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>The Broken Accord's geometry and node states (plan Task 10), without a canvas.</summary>
    public class AccordLayoutTests
    {
        [Test]
        public void BranchesSitInTheirQuadrants()
        {
            foreach (var n in SkillTree.Nodes)
            {
                var p = AccordLayout.NodePosition(n);
                bool left = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Resilience;
                bool up = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Mobility;
                Assert.AreEqual(left, p.x < 0, n.Id); Assert.AreEqual(up, p.y > 0, n.Id);
            }
        }

        [Test]
        public void OuterTiersAreFurtherOut()
        {
            foreach (var n in SkillTree.Nodes)
                Assert.AreEqual(AccordLayout.Radius(n.Tier), AccordLayout.NodePosition(n).magnitude, 0.5f, n.Id);
            Assert.Less(AccordLayout.Radius(1), AccordLayout.Radius(2));
            Assert.Less(AccordLayout.Radius(2), AccordLayout.Radius(3));
        }

        [Test]
        public void SealsNeverTouchEachOtherAndStayOnTheMap()
        {
            var nodes = SkillTree.Nodes;
            foreach (var a in nodes)
            {
                var pa = AccordLayout.NodePosition(a); float ra = AccordLayout.SealSize(a.Tier) / 2f;
                Assert.LessOrEqual(Mathf.Abs(pa.x) + ra, AccordLayout.MapSize.x / 2f, a.Id);
                Assert.LessOrEqual(Mathf.Abs(pa.y) + ra + 40f, AccordLayout.MapSize.y / 2f, a.Id + " (+40 for the name under it)");
                foreach (var b in nodes)
                {
                    if (a == b) continue;
                    float rb = AccordLayout.SealSize(b.Tier) / 2f;
                    Assert.Greater(Vector2.Distance(pa, AccordLayout.NodePosition(b)), ra + rb + 16f, $"{a.Id}/{b.Id}");
                }
            }
        }

        // Spec: "decorative rings never resemble connections between unrelated branches"; the same
        // goes for paths: no dot of one branch's path comes near another branch's seal.
        [Test]
        public void PathsOnlyTouchTheirOwnChain()
        {
            foreach (var n in SkillTree.Nodes)
                foreach (var dot in AccordLayout.PathDots(n))
                    foreach (var other in SkillTree.Nodes.Where(o => o.Branch != n.Branch))
                        Assert.Greater(Vector2.Distance(dot, AccordLayout.NodePosition(other)), AccordLayout.SealSize(other.Tier) / 2f + 24f, $"{n.Id} path near {other.Id}");
        }

        [Test]
        public void DotsAreOnThePixelGridAndOutsideSeals()
        {
            foreach (var n in SkillTree.Nodes)
            {
                var dots = AccordLayout.PathDots(n);
                Assert.Greater(dots.Count, 3, n.Id);
                foreach (var d in dots)
                {
                    Assert.AreEqual(0f, d.x % 2f, 1e-4, n.Id); Assert.AreEqual(0f, d.y % 2f, 1e-4, n.Id);
                    Assert.Greater(Vector2.Distance(d, AccordLayout.NodePosition(n)), AccordLayout.SealSize(n.Tier) / 2f, n.Id);
                }
            }
        }

        [Test]
        public void StatesFollowOwnershipCheatsAndGates()
        {
            var p = new PlayerProfile(); p.mastery.level = 2; p.mastery.points = 1;
            var wide = SkillTree.Find(SkillTree.PrecisionAngle);
            var deep = SkillTree.Find(SkillTree.PrecisionCapacity);
            Assert.AreEqual(NodeState.Available, AccordLayout.State(p, wide));
            Assert.AreEqual(NodeState.Locked, AccordLayout.State(p, deep), "needs Wide Grasp and mastery 4");
            p.ownedNodes.Add(wide.Id);
            Assert.AreEqual(NodeState.Owned, AccordLayout.State(p, wide));
            try
            {
                Cheats.SetUnlockAllNodes(true);
                Assert.AreEqual(NodeState.CheatActive, AccordLayout.State(p, deep), "distinct from earned ownership");
                Assert.AreEqual(NodeState.Owned, AccordLayout.State(p, wide));
            }
            finally { Cheats.SetUnlockAllNodes(false); }
        }
    }
}
