using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Phase 14 tutorial (D86): the frozen clock, each lesson's goal and its "cannot fail"
    /// respawn rule, and a full playthrough performed with the real verbs (catch, fire, swap,
    /// parry) through PlayerCommand, exactly the path keyboard and mouse input takes.
    /// </summary>
    public class TutorialTests
    {
        const float Dt = 1f / 60f;
        static readonly PlayerCommand Still = PlayerCommand.Moving(Vector2.zero);

        static ArenaSim Tutorial(int seed = 3) => new ArenaSim(TestSims.Config, RunSetup.ForTutorial(seed));

        static void Run(ArenaSim sim, int ticks, PlayerCommand cmd)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd, Dt);
        }

        /// <summary>Tick through the quiet beat between lessons until the next one has started.</summary>
        static void SkipBeat(ArenaSim sim)
        {
            for (int i = 0; i < 600 && sim.Tutorial.Celebrating; i++) sim.Tick(Still, Dt);
            sim.Tick(Still, Dt);
        }

        static void WalkTheMarkers(ArenaSim sim)
        {
            foreach (var m in TutorialDirector.MarkerPositions)
            {
                for (int i = 0; i < 60 * 10 && sim.Tutorial.Step == TutorialStep.Move && sim.Tutorial.Marker == m; i++)
                {
                    Vector2 to = m - sim.Player.Position;
                    sim.Tick(PlayerCommand.Moving(to.sqrMagnitude > 0.01f ? to.normalized : Vector2.zero), Dt);
                }
            }
        }

        static void DashThrice(ArenaSim sim)
        {
            for (int i = 0; i < 60 * 10 && sim.Tutorial.Step == TutorialStep.Dash && !sim.Tutorial.Celebrating; i++)
                sim.Tick(PlayerCommand.Moving(i % 120 < 60 ? Vector2.right : Vector2.left).WithDash(), Dt);
        }

        [Test]
        public void ATutorialRun_IsASandbox_WithAFrozenClock_AndHitsCostNoTime()
        {
            var sim = Tutorial();
            Assert.IsTrue(sim.Setup.Sandbox, "never saved, no encounter director");
            Assert.IsNotNull(sim.Tutorial);
            float start = sim.LifeSeconds;
            Run(sim, 60 * 30, Still);
            Assert.AreEqual(start, sim.LifeSeconds, "30 s pass, the clock does not move");
            int hits = 0;
            sim.Events.PlayerHit += (_, __) => hits++;
            Assert.IsTrue(sim.DamagePlayer(50, 0), "the hit still lands...");
            Assert.AreEqual(1, hits);
            Assert.AreEqual(start, sim.LifeSeconds, "...but costs nothing");
            Assert.IsTrue(sim.Player.Alive);
        }

        [Test]
        public void OtherSandboxRuns_StillDrainTheClock()
        {
            var sim = TestSims.Sandbox();
            Assert.IsNull(sim.Tutorial);
            float start = sim.LifeSeconds;
            Run(sim, 60, Still);
            Assert.Less(sim.LifeSeconds, start);
        }

        [Test]
        public void Movement_NeedsEveryMarker_InOrder_ThenDashNeedsThreeDashes()
        {
            var sim = Tutorial();
            Assert.AreEqual(TutorialStep.Move, sim.Tutorial.Step);
            Assert.AreEqual(TutorialDirector.MarkerPositions[0], sim.Tutorial.Marker);
            // Standing on a LATER marker does nothing: the lesson walks the player around.
            sim.Player.Position = TutorialDirector.MarkerPositions[2];
            Run(sim, 5, Still);
            Assert.AreEqual("0/3", sim.Tutorial.Progress);
            WalkTheMarkers(sim);
            Assert.IsTrue(sim.Tutorial.Celebrating);
            Assert.AreEqual("Nice!", sim.Tutorial.Prompt);
            Assert.IsNull(sim.Tutorial.Marker, "no marker during the beat");
            SkipBeat(sim);
            Assert.AreEqual(TutorialStep.Dash, sim.Tutorial.Step);
            Assert.AreEqual(0, sim.Enemies.Count, "nothing to fight while learning to move");

            int dashes = 0;
            sim.Events.Dashed += (_, __) => dashes++;
            DashThrice(sim);
            Assert.AreEqual(TutorialDirector.DashesNeeded, dashes);
            SkipBeat(sim);
            Assert.AreEqual(TutorialStep.Capture, sim.Tutorial.Step);
            Assert.AreEqual(1, sim.Tutorial.Roster.Count);
            Assert.AreEqual(ActorCategory.Acolyte, sim.Tutorial.Roster[0].Category);
        }

        static ArenaSim AtLesson(TutorialStep step)
        {
            var sim = Tutorial();
            WalkTheMarkers(sim);
            SkipBeat(sim);
            if (step == TutorialStep.Dash) return sim;
            DashThrice(sim);
            SkipBeat(sim);
            return sim;
        }

        static void Kill(ArenaSim sim, EnemyActor e) =>
            sim.DamageEnemy(e, e.Health, DamageCategory.ReturnedProjectile,
                new AttackSnapshot { Kind = AttackKind.Bolt, SourceActorId = 900 }, 0);

        [Test]
        public void Capture_KillingTheAcolyteWithoutCatching_BringsItBack()
        {
            var sim = AtLesson(TutorialStep.Capture);
            var first = sim.Tutorial.Roster[0];
            Run(sim, 60, Still);
            Kill(sim, first);
            Run(sim, 60, Still);
            Assert.AreEqual(TutorialStep.Capture, sim.Tutorial.Step, "the goal is to catch, not to kill");
            Assert.AreEqual(1, sim.Tutorial.Roster.Count);
            Assert.AreNotSame(first, sim.Tutorial.Roster[0], "a fresh Acolyte was summoned");
            Assert.IsTrue(sim.Tutorial.Roster[0].Alive);
        }

        [Test]
        public void Parry_ARiposteKillWithOneParry_SummonsAnotherPursuer_UntilTwoParries()
        {
            var sim = AtLesson(TutorialStep.Capture);
            // Jump straight to the parry lesson by playing the earlier ones with the full bot.
            var bot = new TutorialBot();
            for (int i = 0; i < 60 * 240 && sim.Tutorial.Step != TutorialStep.Parry; i++) sim.Tick(bot.Next(sim), Dt);
            Assert.AreEqual(TutorialStep.Parry, sim.Tutorial.Step);
            var first = sim.Tutorial.Roster[0];
            Assert.AreEqual(ActorCategory.Pursuer, first.Category);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            for (int i = 0; i < 60 * 120 && parries == 0; i++) sim.Tick(bot.Next(sim), Dt);
            Assert.AreEqual(1, parries, "the bot landed a parry");
            Run(sim, 60, Still);
            if (!first.Alive)
            {
                Assert.AreEqual(TutorialStep.Parry, sim.Tutorial.Step, "one parry is not the lesson");
                Assert.IsTrue(sim.Tutorial.Roster[0].Alive, "a fresh Pursuer for the second parry");
            }
            Assert.AreEqual("parries 1/2", sim.Tutorial.Progress);
        }

        /// <summary>
        /// Plays the tutorial the way a person would be told to: walk to the marker, dash, catch
        /// an incoming bolt by aiming at it, fire at the nearest enemy, swap when told, and parry
        /// a Pursuer by pressing catch as its strike rim appears. It follows the director's own
        /// step and prompt state, so it doubles as a check that each prompt is achievable.
        /// </summary>
        sealed class TutorialBot
        {
            double lastCatch = -10, lastRelease = -10, lastSwap = -10;

            public PlayerCommand Next(ArenaSim sim)
            {
                var t = sim.Tutorial;
                var p = sim.Player;
                double now = sim.Clock.Now;
                if (t.Celebrating || t.IsComplete) return Still;
                switch (t.Step)
                {
                    case TutorialStep.Move:
                    {
                        Vector2 to = t.Marker.Value - p.Position;
                        return PlayerCommand.Moving(to.normalized);
                    }
                    case TutorialStep.Dash:
                        return PlayerCommand.Moving(Mathf.Sin((float)now) > 0 ? Vector2.right : Vector2.left).WithDash();
                }

                EnemyActor target = null;
                float best = float.MaxValue;
                foreach (var e in sim.Enemies)
                {
                    if (!e.Alive || now < e.ActiveAt) continue;
                    float d = (e.Position - p.Position).sqrMagnitude;
                    if (d < best) { best = d; target = e; }
                }

                // Parry: press as the strike rim is about to show (the parry window is half the
                // catch window, so pressing a hair early still overlaps the rim).
                if (target != null && target.Category == ActorCategory.Pursuer
                    && now >= target.ParryRimOpensAt - 0.03 && now <= target.ParryRimClosesAt && sim.Capture.IsReady(now))
                {
                    lastCatch = now;
                    return Still.WithAim(target.Position).WithCatch();
                }

                // Keep some distance from the Pursuer until it commits, so its strike is a clean one.
                Vector2 move = Vector2.zero;
                if (t.Step != TutorialStep.Parry && t.Step != TutorialStep.Evolve && target != null && best < 9f) move = (p.Position - target.Position).normalized;

                // Catch the nearest hostile shot heading in.
                ProjectileActor incoming = null;
                float near = 2.2f;
                foreach (var pr in sim.Projectiles)
                {
                    if (!pr.Active || pr.Faction != AttackFaction.Hostile) continue;
                    float d = (pr.Position - p.Position).magnitude;
                    if (d < near) { near = d; incoming = pr; }
                }
                var slots = sim.Packets;
                bool holding = slots.Packets.Count > 0;
                bool wantCatch = t.Step == TutorialStep.Slots ? slots.FreeSlots > 0 : !holding;
                if (incoming != null && wantCatch && sim.Capture.IsReady(now) && now - lastCatch > 0.3)
                {
                    lastCatch = now;
                    var c = PlayerCommand.Moving(move).WithAim(incoming.Position).WithCatch();
                    // D89: a full hand cannot catch; Q on the same tick pockets first.
                    return slots.HandFree ? c : c.WithCycle();
                }

                var cmd = PlayerCommand.Moving(move).WithAim(target != null ? target.Position : p.Position + Vector2.up);
                if (t.Step == TutorialStep.Slots)
                {
                    // Fill both, then swap once, then fire from each slot with a swap in between.
                    bool full = slots.Packets.Count >= 2;
                    // "Q to swap", not "Press Q": the fill prompt also says "Press Q" (to pocket),
                    // and cycling there would only shuffle the hand instead of catching.
                    if (t.Prompt.Contains("Q to swap") && now - lastSwap > 0.3) { lastSwap = now; return cmd.WithCycle(); }
                    if (t.Prompt.Contains("fire from both") && holding && now - lastRelease > 0.4)
                    {
                        if (slots.InSlot(slots.SelectedSlot) == null) { lastSwap = now; return cmd.WithCycle(); }
                        if (!sim.IsPrimed(slots.InSlot(slots.SelectedSlot))) return cmd;   // D90
                        lastRelease = now;
                        return cmd.WithRelease();
                    }
                    if (!full && !t.Prompt.Contains("Q to swap") && !t.Prompt.Contains("fire from both") && t.Prompt.Contains("defeat") && holding && now - lastRelease > 0.4)
                    {
                        if (slots.InSlot(slots.SelectedSlot) == null) return cmd.WithCycle();
                        if (!sim.IsPrimed(slots.InSlot(slots.SelectedSlot))) return cmd;   // D90
                        lastRelease = now;
                        return cmd.WithRelease();
                    }
                    return cmd;
                }
                if (holding && target != null && now - lastRelease > 0.4 && now - lastCatch > 0.35
                    && sim.IsPrimed(slots.ReleaseCandidate()))
                {
                    lastRelease = now;
                    return cmd.WithRelease();
                }
                return cmd;
            }
        }

        [Test]
        public void TutorialEnemies_NeverEvolveOnTheirOwn_AndShowNoOverstayCountdown()
        {
            var sim = AtLesson(TutorialStep.Capture);
            var acolyte = sim.Tutorial.Roster[0];
            Assert.IsTrue(float.IsPositiveInfinity(sim.OverstaySeconds), "the view's warning ring reads this too");
            // Keep the player out of the way and alive-ish; 40 s is well past the 25 s overstay.
            for (int i = 0; i < 60 * 40; i++) sim.Tick(Still, Dt);
            Assert.IsTrue(acolyte.Alive);
            Assert.IsFalse(acolyte.Overstayed, "no evolution in the tutorial's early lessons");
        }

        [Test]
        public void ARealRun_StillEvolvesItsEnemies()
        {
            var sim = TestSims.Sandbox();
            var e = sim.SummonEnemy(ActorCategory.Acolyte);
            sim.Player.Position = new Vector2(-11f, -7f);
            for (int i = 0; i < 60 * 30 && !e.Overstayed && e.Alive; i++) sim.Tick(Still, Dt);
            Assert.IsTrue(e.Overstayed, "the refactor into EvolveEnemy kept the normal timer");
        }

        [Test]
        public void EvolutionLesson_EvolvesBothAtOnce_AndOnlyEvolvedKillsCount()
        {
            var sim = Tutorial(5);
            var bot = new TutorialBot();
            for (int i = 0; i < 60 * 400 && sim.Tutorial.Step != TutorialStep.Evolve; i++) sim.Tick(bot.Next(sim), Dt);
            Assert.AreEqual(TutorialStep.Evolve, sim.Tutorial.Step);
            Assert.AreEqual(6, sim.Tutorial.LessonNumber);
            var roster = new List<EnemyActor>(sim.Tutorial.Roster);
            CollectionAssert.AreEquivalent(new[] { ActorCategory.Acolyte, ActorCategory.Pursuer }, roster.ConvertAll(e => e.Category));
            Assert.IsFalse(roster.Exists(e => e.Overstayed), "seen in their normal form first");
            StringAssert.Contains("EVOLVE", sim.Tutorial.Prompt);
            double activeAt = Mathf.Max((float)roster[0].ActiveAt, (float)roster[1].ActiveAt);
            float baseHealth = roster[0].MaxHealth;
            int evolved = 0;
            sim.Events.EnemyOverstayed += _ => evolved++;
            while (sim.Clock.Now < activeAt + TutorialDirector.EvolveAfter + 0.1) sim.Tick(Still, Dt);
            Assert.AreEqual(2, evolved, "both evolved, about a second after they could fight, not 25");
            Assert.IsTrue(roster.TrueForAll(e => e.Overstayed && e.Elite));
            Assert.Greater(roster[0].MaxHealth, baseHealth);
            StringAssert.Contains("Take both down", sim.Tutorial.Prompt);
            Assert.AreEqual("evolved defeated 0/2", sim.Tutorial.Progress);

            foreach (var e in roster) Kill(sim, e);
            for (int i = 0; i < 5; i++) sim.Tick(Still, Dt);
            Assert.IsTrue(sim.Tutorial.Celebrating || sim.Tutorial.IsComplete);
            SkipBeat(sim);
            Assert.IsTrue(sim.Tutorial.IsComplete, "two evolved kills finish the tutorial");
        }

        [Test]
        public void AScriptedPlayer_FinishesEveryLesson_UsingTheRealVerbs()
        {
            var sim = Tutorial(11);
            var bot = new TutorialBot();
            var reached = new List<TutorialStep>();
            int captures = 0, releases = 0, swaps = 0, parries = 0;
            sim.Events.ShotCaptured += (_, __, ___, ____) => captures++;
            sim.Events.PacketReleased += (_, __) => releases++;
            sim.Events.SlotSwapped += _ => swaps++;
            sim.Events.StrikeParried += (_, __) => parries++;
            float life = sim.LifeSeconds;
            for (int i = 0; i < 60 * 900 && !sim.Tutorial.IsComplete; i++)
            {
                if (reached.Count == 0 || reached[reached.Count - 1] != sim.Tutorial.Step) reached.Add(sim.Tutorial.Step);
                sim.Tick(bot.Next(sim), Dt);
            }
            TestContext.WriteLine($"t={sim.Clock.Now:F0}s captures {captures} releases {releases} swaps {swaps} parries {parries}; at {sim.Tutorial.Step}: {sim.Tutorial.Prompt} {sim.Tutorial.Progress}");
            Assert.IsTrue(sim.Tutorial.IsComplete, $"stuck at {sim.Tutorial.Step}: '{sim.Tutorial.Prompt}' {sim.Tutorial.Progress}");
            reached.Add(sim.Tutorial.Step);
            CollectionAssert.AreEqual(new[] { TutorialStep.Move, TutorialStep.Dash, TutorialStep.Capture,
                TutorialStep.Slots, TutorialStep.Parry, TutorialStep.Evolve, TutorialStep.Complete }, reached, "lessons in order, none skipped");
            Assert.GreaterOrEqual(captures, 3, "one catch for the Acolyte, two to fill both slots");
            Assert.GreaterOrEqual(swaps, 1);
            Assert.GreaterOrEqual(parries, TutorialDirector.ParriesNeeded);
            Assert.AreEqual(life, sim.LifeSeconds, "the clock never moved");
            Assert.AreEqual(RunState.Combat, sim.State, "a tutorial never reaches Results");
            Assert.AreEqual(0, sim.Enemies.Count, "the arena is quiet at the end");
        }

        [Test]
        public void SlotsLesson_TeachesPocketingWithQ()
        {
            var sim = new ArenaSim(TestSims.Config, RunSetup.ForTutorial(1));
            var bot = new TutorialBot();
            for (int i = 0; i < 60 * 240 && sim.Tutorial.Step != TutorialStep.Slots; i++) sim.Tick(bot.Next(sim), Dt);
            Assert.AreEqual(TutorialStep.Slots, sim.Tutorial.Step);
            StringAssert.Contains("Q", sim.Tutorial.Prompt);
            StringAssert.Contains("pocket", sim.Tutorial.Prompt.ToLowerInvariant());
        }
    }
}
