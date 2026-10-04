using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Presentation;
using BorrowedHex.Presentation.Feedback;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Event -> cue -> outputs, against a real sim with events raised directly (internal
    // Raise* via InternalsVisibleTo). The host is a recorder, so shake and hit-stop are seen
    // as requests, exactly what GameRoot would receive.
    public class CombatFeedbackTests
    {
        sealed class Recorder : IFeedbackHost
        {
            public readonly List<float> Stops = new List<float>(), Shakes = new List<float>();
            public void HitStop(float s) => Stops.Add(s);
            public void Shake(float amp, float dur) => Shakes.Add(amp);
        }

        ArenaSim sim; GameObject parent; WorldArtLibrary art; Recorder host; CharacterView player; CombatFeedback fx;
        bool reduceFlashes;

        [SetUp]
        public void SetUp()
        {
            reduceFlashes = DisplayOptions.ReduceFlashes;
            DisplayOptions.ReduceFlashes = false;
            sim = TestSims.Sandbox();
            parent = new GameObject("FeedbackTest");
            art = new WorldArtLibrary();
            host = new Recorder();
            player = CharacterView.Create(parent.transform, "Player", PixelSprites.Kind.Magician, 1f, .45f);
            fx = new CombatFeedback(sim, parent.transform, art, host, _ => null, player);
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose(); art.Dispose(); Object.DestroyImmediate(parent);
            DisplayOptions.ReduceFlashes = reduceFlashes;
        }

        EnemyActor Enemy(Vector2 at) => P4.Parked(sim, ActorCategory.Acolyte, at);

        // Effect sheets are licensed local art (git-ignored). Without them WorldEffects.Spawn
        // correctly draws nothing, so effect counts are only checked where the sheets exist;
        // every other assertion (callouts, shake, hit-stop, pierce keys) still runs. HasBoss is
        // the repo's existing "local packs present" probe: the packs arrive together.
        bool Sheets => art.HasBoss;

        [Test]
        public void AParryCallsOutShakesFreezesAndFlashes()
        {
            sim.Events.RaiseStrikeParried(Enemy(new Vector2(2, 0)), new Vector2(1, 0));
            CollectionAssert.Contains(fx.Callouts.LiveTexts, "Parry!");
            CollectionAssert.Contains(host.Stops, .09f);
            CollectionAssert.Contains(host.Shakes, .15f);
            if (Sheets) Assert.Greater(fx.LiveEffects, 0);
            Assert.IsTrue(fx.Impact.Active); Assert.IsTrue(player.Silhouette);
            for (int i = 0; i < 3; i++) fx.Render(null, 1f / 60);
            Assert.IsFalse(player.Silhouette, "the silhouette lasts two frames");
        }

        [Test]
        public void ReduceFlashesKeepsTheWordButNotTheMotion()
        {
            DisplayOptions.ReduceFlashes = true;
            sim.Events.RaiseStrikeParried(Enemy(new Vector2(2, 0)), new Vector2(1, 0));
            CollectionAssert.Contains(fx.Callouts.LiveTexts, "Parry!");
            Assert.IsEmpty(host.Stops); Assert.IsEmpty(host.Shakes);
            Assert.IsFalse(fx.Impact.Active);
        }

        [Test]
        public void AGiftsExplosionNeverShakesButTheNextRocketDoes()
        {
            sim.Events.RaisePartingGiftBurst(Vector2.zero, 2f);
            sim.Events.RaiseExplosion(Vector2.zero, 2f, AttackFaction.Returned);
            Assert.IsEmpty(host.Shakes, "the gift bursts on every release; no shake");
            sim.Events.RaiseExplosion(new Vector2(3, 0), 1.6f, AttackFaction.Hostile);
            CollectionAssert.AreEqual(new[] { .1f }, host.Shakes, "only the gift's own explosion was swallowed");
        }

        [Test]
        public void ChainsCallOutFromThreeOnTheKill()
        {
            // The real chain, not an injected length: the sim's own EnemyKilled handler raises
            // KillChainChanged with Chain.Length, so a faked length would be overwritten. The
            // clock never moves here, so every kill lands inside the chain window.
            var e = Enemy(new Vector2(2, 0));
            sim.Events.RaiseEnemyKilled(e, default);
            sim.Events.RaiseEnemyKilled(e, default);
            Assert.IsEmpty(fx.Callouts.LiveTexts, "chains of 1 and 2 stay silent");
            CollectionAssert.Contains(host.Shakes, .05f, "an ordinary kill still shakes a little");
            sim.Events.RaiseEnemyKilled(e, default);
            CollectionAssert.Contains(fx.Callouts.LiveTexts, "Chain x3!");
        }

        [Test]
        public void ASecondEnemyOfOneShotDrawsThePierceArc()
        {
            var hit = new DamageEvent { RootReleaseId = 4, ShotId = 9, Category = DamageCategory.ReturnedProjectile };
            sim.Events.RaiseEnemyDamaged(Enemy(new Vector2(2, 0)), hit);
            int afterFirst = fx.LiveEffects;
            sim.Events.RaiseEnemyDamaged(Enemy(new Vector2(4, 0)), hit);
            if (Sheets) Assert.AreEqual(afterFirst + 2, fx.LiveEffects, "pierce spark plus the chain arc");
            Assert.IsEmpty(host.Shakes, "hits are Tier 0");
        }

        [Test]
        public void APerfectCatchWithFinalSecondSaysLastSecond()
        {
            sim.ForceUpgrade(UpgradeId.FinalSecond);
            var shot = new AttackSnapshot { Perfect = true };
            sim.Events.RaiseShotCaptured(null, shot, Vector2.zero, CaptureResult.CreatedPacket);
            CollectionAssert.Contains(fx.Callouts.LiveTexts, "Last Second!");
            CollectionAssert.DoesNotContain(fx.Callouts.LiveTexts, "Perfect!");
            CollectionAssert.Contains(host.Stops, .05f);
        }

        [Test]
        public void OverchargeKeepsItsFreezeAndShake()
        {
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now, 3f, 12);
            sim.Events.RaisePacketOvercharged(p, 1);
            CollectionAssert.Contains(host.Stops, .07f);
            CollectionAssert.Contains(host.Shakes, .18f);
            Assert.That(fx.Callouts.LiveTexts[0], Does.StartWith("Overcharge! x"));
        }

        [Test]
        public void EndedShotsShowTheirEndingAndForgetTheirPierceKey()
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            var p = sim.SpawnProjectile(s, AttackFaction.Hostile, new Vector2(3, 3), Vector2.right);
            sim.Events.RaiseProjectileEnded(p, ProjectileEndReason.Expired);
            int fizzle = Sheets ? 1 : 0;
            Assert.AreEqual(fizzle, fx.LiveEffects, "a fizzle where it ran out");
            sim.Events.RaiseProjectileEnded(p, ProjectileEndReason.Cleared);
            Assert.AreEqual(fizzle, fx.LiveEffects, "a cleared arena shows nothing");
            Assert.AreEqual(0, fx.PierceKeys);
        }

        [Test]
        public void FiveMomentsInARowNeverShowMoreThanThreeWords()
        {
            var e = Enemy(new Vector2(2, 0));
            sim.Events.RaiseStrikeParried(e, Vector2.zero);
            sim.Events.RaisePacketBackfired(null);
            sim.Events.RaisePacketsFused(null, null);
            for (int i = 0; i < 3; i++) sim.Events.RaiseEnemyKilled(e, default);   // a real chain of 3
            sim.Events.RaiseShotCaptured(null, new AttackSnapshot { Perfect = true }, Vector2.zero, CaptureResult.CreatedPacket);
            Assert.LessOrEqual(fx.Callouts.LiveCount, 3);
            CollectionAssert.Contains(fx.Callouts.LiveTexts, "Perfect!", "the newest always shows");
        }
    }
}
