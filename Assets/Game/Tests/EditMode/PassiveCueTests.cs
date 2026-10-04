using BorrowedHex.Combat;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Presentation;
using BorrowedHex.Presentation.Feedback;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // D3: passives get cues only while their action is happening (spec 5.2, 5.3).
    public class PassiveCueTests
    {
        [Test]
        public void TheDashPingFiresOnlyOnTheFrameRecoveryEnds()
        {
            Assert.IsTrue(CombatFeedback.DashReadyCrossed(1.0, 1.02, 1.01));
            Assert.IsFalse(CombatFeedback.DashReadyCrossed(1.02, 1.04, 1.01), "only once");
            Assert.IsFalse(CombatFeedback.DashReadyCrossed(0.0, 0.016, 0.0), "no ping at the start of a run");
        }

        [Test]
        public void OnlyAFiniteGraceShimmers()
        {
            var p = new PlayerActor();
            Assert.IsFalse(CombatFeedback.ShowsShimmer(p, 10), "not invulnerable");
            p.InvulnerableUntil = 10.5;
            Assert.IsTrue(CombatFeedback.ShowsShimmer(p, 10), "post-hit grace");
            p.InvulnerableUntil = double.PositiveInfinity;
            Assert.IsFalse(CombatFeedback.ShowsShimmer(p, 10), "god mode / test invulnerability is not a grace window");
            p.InvulnerableUntil = double.NegativeInfinity; p.DashInvulnerableUntil = 10.1;
            Assert.IsTrue(CombatFeedback.ShowsShimmer(p, 10), "dash grace");
        }

        [Test]
        public void ADashLeavesThreeGhostsAlongItsPathThatFade()
        {
            var sim = TestSims.Sandbox();
            var parent = new GameObject("PassiveTest");
            var art = new WorldArtLibrary();
            var player = CharacterView.Create(parent.transform, "P", PixelSprites.Kind.Magician, 1f, .45f);
            var fx = new CombatFeedback(sim, parent.transform, art, null, _ => null, player);
            try
            {
                // A real dash through the sim: the copies are dropped while it runs, at the
                // player's position at the time, so they trail behind instead of appearing ahead.
                // Rendered with dt 0 so no copy ages out while the dash is still laying them.
                var go = BorrowedHex.Player.PlayerCommand.Moving(Vector2.right);
                sim.Tick(go.WithDash(), 1f / 60);
                fx.Render(null, 0f);
                Assert.AreEqual(1, fx.LiveGhosts, "the first copy at once");
                var xs = new System.Collections.Generic.List<float>();
                for (int i = 0; i < 30; i++)
                {
                    int before = fx.LiveGhosts;
                    sim.Tick(go, 1f / 60);
                    fx.Render(null, 0f);
                    if (fx.LiveGhosts > before) xs.Add(sim.Player.Position.x);
                }
                Assert.AreEqual(CombatFeedback.DashGhosts, fx.LiveGhosts);
                Assert.AreEqual(CombatFeedback.DashGhosts - 1, xs.Count);
                Assert.Less(xs[0], xs[1], "each later copy is further along the dash");
                fx.Render(null, CombatFeedback.GhostSeconds + .01f);
                Assert.AreEqual(0, fx.LiveGhosts);
            }
            finally { fx.Dispose(); art.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void TheQuickDrawGlintShowsOnlyInsideThePostSwapWindow()
        {
            const float Dt = 1f / 60f;
            var setup = RunSetup.ForSandbox(1);
            setup.Stats = Loadout.Resolve(TestSims.Config, new[] { SkillTree.QuickDraw });
            var sim = new ArenaSim(TestSims.Config, setup);
            var aim = PlayerCommand.Moving(Vector2.zero).WithAim(new Vector2(8f, 0f));
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 42, sim.Ids.Next(), 0));
            p.Status = PacketStatus.Stored;
            Assert.IsFalse(CombatFeedback.QuickDrawOpen(sim), "no swap yet");
            sim.Tick(aim.WithCycle(), Dt);
            Assert.IsTrue(CombatFeedback.QuickDrawOpen(sim));
            for (int i = 0; i < 30; i++) sim.Tick(aim, Dt);
            Assert.IsFalse(CombatFeedback.QuickDrawOpen(sim), "0.5 s later the window has closed");
        }
    }
}
