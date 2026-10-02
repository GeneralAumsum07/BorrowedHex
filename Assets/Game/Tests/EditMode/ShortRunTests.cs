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
    // Section 6 schedule, rosters and cap, and the Collector (owner-revised, D38).
    public class ShortRunTests
    {
        /// <summary>Run encounters 1–3 with an invulnerable player, recording every spawn by encounter.</summary>
        static List<HashSet<ActorCategory>> RecordRosters(ArenaSim sim, System.Action perTick = null, bool killAtTransitions = false)
        {
            var rosters = new List<HashSet<ActorCategory>> { new HashSet<ActorCategory>(), new HashSet<ActorCategory>(), new HashSet<ActorCategory>() };
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) rosters[sim.Encounter].Add(e.Category); };
            P5.Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                int guard = 0;
                while (sim.State != RunState.UpgradeChoice && guard++ < 3000)
                {
                    sim.Tick(P5.Still, P5.Dt);
                    perTick?.Invoke();
                }
                Assert.AreEqual(RunState.UpgradeChoice, sim.State);
                // A player who clears the arena: otherwise the cap (nobody dies here) keeps
                // encounter-one enemies on the field and the next roster never gets room.
                if (killAtTransitions) foreach (var e in sim.Enemies) if (e.Alive) P5.Kill(sim, e);
                if (i < 2) sim.ContinueFromUpgrade();
            }
            return rosters;
        }

        [Test]
        public void EncounterRosters_GrowEncounterByEncounter()
        {
            var r = RecordRosters(P5.Short(3), killAtTransitions: true);
            CollectionAssert.IsSubsetOf(r[0], new[] { ActorCategory.Acolyte, ActorCategory.Pursuer });
            CollectionAssert.Contains(r[0], ActorCategory.Acolyte);
            CollectionAssert.Contains(r[0], ActorCategory.Pursuer);
            CollectionAssert.Contains(r[1], ActorCategory.ScatterCaster);
            CollectionAssert.DoesNotContain(r[1], ActorCategory.SiegeFamiliar);
            CollectionAssert.Contains(r[2], ActorCategory.SiegeFamiliar);
        }

        [Test]
        public void ActiveOrdinaryEnemies_NeverExceedTwelve_AndExtraSpawnsWait()
        {
            var sim = P5.Short(5);
            int maxAlive = 0, maxQueued = 0;
            RecordRosters(sim, () =>
            {
                maxAlive = Mathf.Max(maxAlive, sim.AliveOrdinaryCount());
                maxQueued = Mathf.Max(maxQueued, sim.QueuedSpawns);
            });
            Assert.AreEqual(12, maxAlive, "the cap is reached (nobody dies in this run) and never passed");
            Assert.Greater(maxQueued, 0, "a formation did have to wait");
            Assert.LessOrEqual(maxQueued, 3, "waiting never stockpiles more than one formation");
        }

        [Test]
        public void TheSameSeed_ReproducesTheSameSpawns()
        {
            List<string> Spawns(int seed)
            {
                var sim = P5.Short(seed);
                var log = new List<string>();
                sim.Events.EnemySpawned += e => log.Add($"{sim.Clock.Now:F3} {e.Category} {e.Position.x:F3},{e.Position.y:F3}");
                P5.Invulnerable(sim);
                P5.Run(sim, 2400);
                return log;
            }
            var a = Spawns(11);
            CollectionAssert.AreEqual(a, Spawns(11));
            CollectionAssert.AreNotEqual(a, Spawns(12));
        }

        // ---- The Collector -------------------------------------------------------------

        static (ArenaSim sim, EnemyActor boss) BossFight()
        {
            var sim = P5.Short();
            P5.ToBossCombat(sim);
            return (sim, sim.Boss);
        }

        [Test]
        public void Collector_Has50Health_AndOnlyFiresCapturableBolts_NoRockets()
        {
            var (sim, boss) = BossFight();
            Assert.AreEqual(50f, boss.Health);
            var kinds = new HashSet<AttackKind>();
            bool allCapturable = true;
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.SourceActorId != boss.ActorId) return;
                kinds.Add(p.Shot.Kind);
                allCapturable &= p.Shot.Capturable;
            };
            P5.TickWhile(sim, RunState.BossCombat);
            CollectionAssert.AreEquivalent(new[] { AttackKind.Bolt }, kinds);
            Assert.IsTrue(allCapturable);
        }

        [Test]
        public void Collector_RunsAllFourPatterns_AndAmmunitionNeverPausesLong()
        {
            var (sim, boss) = BossFight();
            var seen = new HashSet<BossPattern>();
            double last = sim.Clock.Now, worstGap = 0;
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.SourceActorId != boss.ActorId) return;
                worstGap = System.Math.Max(worstGap, sim.Clock.Now - last);
                last = sim.Clock.Now;
            };
            while (sim.State == RunState.BossCombat)
            {
                sim.Tick(P5.Still, P5.Dt);
                seen.Add(boss.Boss.Pattern);
            }
            CollectionAssert.AreEquivalent(new[] { BossPattern.BoltStream, BossPattern.Sweep, BossPattern.FanVolley, BossPattern.Slam }, seen);
            // Plan: "no extended period with neither targets nor ammunition". The boss is always
            // a target; bolts arrive at least every 6.5 s (including its 1 s spawn warning).
            Assert.LessOrEqual(worstGap, 6.5, $"worst gap between boss bolts {worstGap:F2} s");
        }

        [Test]
        public void SweepGeometry_HitsInsideTheSweptWedgeOnly()
        {
            Vector2 o = Vector2.zero, aim = Vector2.right;
            Assert.IsTrue(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, new Vector2(2f, 0f), 0.45f));
            Assert.IsFalse(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, new Vector2(-2f, 0f), 0.45f), "behind");
            Assert.IsFalse(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, new Vector2(3.6f, 0f), 0.45f), "beyond reach");
            Assert.IsTrue(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, new Vector2(3.4f, 0f), 0.45f), "body edge in reach");
            // 20° off the swept edge at distance 2: the body (0.45) subtends ~13°, so it misses...
            Assert.IsFalse(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, Geometry2D.Rotate(new Vector2(2f, 0f), 30f), 0.45f));
            // ...and at 20° the edge of the body is grazed.
            Assert.IsTrue(CollectorBoss.BladeTouches(o, aim, -10f, 10f, 3f, Geometry2D.Rotate(new Vector2(2f, 0f), 20f), 0.45f));
        }

        /// <summary>
        /// Keep the player at a fixed offset from the boss and count hits landed while each
        /// melee pattern is running. Invulnerability is cleared every tick so a bolt hit can
        /// never hide a melee hit behind its blink.
        /// </summary>
        static Dictionary<BossPattern, int> MeleeHitsAt(float distance)
        {
            var (sim, boss) = BossFight();
            sim.Player.MaxHealth = sim.Player.Health = 999;
            var hits = new Dictionary<BossPattern, int> { [BossPattern.Sweep] = 0, [BossPattern.Slam] = 0 };
            sim.Events.PlayerHit += (_, src) =>
            {
                // Melee damage has no projectile: only count hits while the boss is mid-melee
                // and no boss bolt is in flight (bolts can only come from ranged patterns).
                var b = boss.Boss;
                if (src == boss.ActorId && CollectorBoss.IsMelee(b.Pattern) && b.Stage == BossStage.Active)
                    hits[b.Pattern]++;
            };
            for (int i = 0; i < 1800 && sim.State == RunState.BossCombat; i++)
            {
                sim.Player.InvulnerableUntil = 0;
                Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                sim.Player.Position = boss.Position + dir * distance;
                sim.Tick(P5.Still, P5.Dt);
            }
            return hits;
        }

        [Test]
        public void SweepAndSlam_HitAPlayerStandingClose()
        {
            var hits = MeleeHitsAt(1.6f);
            Assert.Greater(hits[BossPattern.Sweep], 0);
            Assert.Greater(hits[BossPattern.Slam], 0);
        }

        [Test]
        public void SweepAndSlam_MissAPlayerOutsideTheirReach()
        {
            var hits = MeleeHitsAt(4.2f); // sweep 3.0 and slam 3.2, plus the 0.45 body: out of both
            Assert.AreEqual(0, hits[BossPattern.Sweep]);
            Assert.AreEqual(0, hits[BossPattern.Slam]);
        }

        /// <summary>
        /// A deliberately simple player: catch the nearest incoming shot when the catch is ready,
        /// otherwise fire whatever is stored at the boss; keep out of melee reach. No dash, no
        /// upgrades, no passives.
        /// </summary>
        static PlayerCommand Bot(ArenaSim sim)
        {
            var p = sim.Player;
            var boss = sim.Boss;
            double now = sim.Clock.Now;
            Vector2 toBoss = boss.Position - p.Position;
            float d = toBoss.magnitude;
            float want = CollectorBoss.IsMelee(boss.Boss.Pattern) ? 4.6f : 5.5f;
            Vector2 move = d < want ? -toBoss.normalized : d > want + 1.5f ? toBoss.normalized : Vector2.zero;
            var cmd = PlayerCommand.Moving(move);

            ProjectileActor incoming = null;
            float best = 3.2f;
            foreach (var pr in sim.Projectiles)
            {
                if (!pr.Active || pr.Faction != AttackFaction.Hostile) continue;
                float dd = (pr.Position - p.Position).magnitude;
                if (dd < best) { best = dd; incoming = pr; }
            }
            if (incoming != null && sim.Capture.IsWindowOpen(now)) return cmd.WithAim(incoming.Position);
            if (incoming != null && sim.Capture.IsReady(now)) return cmd.WithAim(incoming.Position).WithCatch();
            if (sim.Packets.ReleaseCandidate() == null && sim.Packets.Packets.Count > 0) cmd = cmd.WithCycle();
            return cmd.WithAim(boss.Position).WithRelease();
        }

        [Test]
        public void AZeroPassiveCharacter_CanDefeatTheCollectorInsideTheBossWindow()
        {
            var (sim, boss) = BossFight();
            // Health raised so this measures OFFENCE only: can baseline catching and returning
            // deal 50 damage inside 60 s? Dodging is a skill question the human playtest answers.
            sim.Player.InvulnerableUntil = 0;
            sim.Player.MaxHealth = sim.Player.Health = 999;
            while (sim.State == RunState.BossCombat) sim.Tick(Bot(sim), P5.Dt);
            TestContext.WriteLine($"end {sim.Summary.Reason} at {sim.Summary.Duration:F1}s, boss hp {boss.Health}, " +
                                  $"released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, damage taken {sim.Score.DamageTaken}");
            Assert.AreEqual(RunEndReason.Victory, sim.Summary.Reason,
                $"boss hp {boss.Health}, released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, " +
                $"kills {sim.Score.Kills}, taken {sim.Score.DamageTaken}");
            // Report the margin in the passing result: how much of the 60 s window it needed.
            Assert.Pass($"won at {sim.Summary.Duration:F1}s ({sim.Summary.Duration - 120f:F1}s into the boss window), " +
                        $"released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, taken {sim.Score.DamageTaken}");
        }
    }
}
