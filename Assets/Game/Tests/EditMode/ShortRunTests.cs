using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Section 6 schedule, rosters and cap, and the Collector (owner-revised, D38, D47-D49).
    public class ShortRunTests
    {
        /// <summary>Clear encounters 1–3 with an invulnerable player, recording every spawn by encounter.</summary>
        static List<HashSet<ActorCategory>> RecordRosters(ArenaSim sim)
        {
            var rosters = new List<HashSet<ActorCategory>> { new HashSet<ActorCategory>(), new HashSet<ActorCategory>(), new HashSet<ActorCategory>() };
            sim.Events.EnemySpawned += e => { if (!e.IsBoss) rosters[sim.Encounter].Add(e.Category); };
            P5.Invulnerable(sim);
            for (int i = 0; i < 3; i++)
            {
                P5.ClearEncounter(sim);
                Assert.AreEqual(RunState.UpgradeChoice, sim.State);
                if (i < 2) sim.ContinueFromUpgrade();
            }
            return rosters;
        }

        [Test]
        public void EncounterRosters_GrowEncounterByEncounter()
        {
            var r = RecordRosters(P5.Short(3));
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
            // Encounter 3 plans more than twelve members; with nobody dying there the cap is hit.
            var sim = P5.Short(5);
            P5.Invulnerable(sim);
            for (int i = 0; i < 2; i++) { P5.ClearEncounter(sim); sim.ContinueFromUpgrade(); }
            sim.Tick(P5.Still, P5.Dt);
            Assert.Greater(sim.EnemiesLeftInEncounter(), 12, "fixture: encounter 3 must outnumber the cap");
            int maxAlive = 0, maxQueued = 0;
            for (int i = 0; i < 60 * 60 && sim.State == RunState.Combat; i++)
            {
                sim.Tick(P5.Still, P5.Dt);
                maxAlive = Mathf.Max(maxAlive, sim.AliveOrdinaryCount());
                maxQueued = Mathf.Max(maxQueued, sim.QueuedSpawns);
            }
            Assert.AreEqual(12, maxAlive, "the cap is reached (nobody dies here) and never passed");
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
            var damages = new HashSet<int>();
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.SourceActorId != boss.ActorId) return;
                kinds.Add(p.Shot.Kind);
                allCapturable &= p.Shot.Capturable;
                damages.Add(p.Shot.HostileDamage);
            };
            P5.TickWhile(sim, RunState.BossCombat);
            CollectionAssert.AreEquivalent(new[] { AttackKind.Bolt }, kinds);
            Assert.IsTrue(allCapturable);
            // Owner direction (D51): bosses do two hearts — its bolts included.
            CollectionAssert.AreEquivalent(new[] { sim.Config.collector.hitDamage }, damages);
            Assert.AreEqual(4, sim.Config.collector.hitDamage);
        }

        // ---- Pattern choice by position (D48) -------------------------------------------

        /// <summary>
        /// Choose reads only the two positions, line of sight and the streak, so any enemy
        /// can stand in for the boss's body here; the sandbox arena has the shipped pillars.
        /// </summary>
        static BossPattern ChooseFor(Vector2 boss, Vector2 player, int streak = 0,
            BossPattern? last = null, int sameInARow = 0)
        {
            var sim = P4.Sim();
            var stand = sim.SpawnEnemy(ActorCategory.Pursuer, boss);
            sim.Player.Position = player;
            return CollectorBoss.Choose(sim, stand, sim.Config.collector, streak, last, sameInARow);
        }

        [Test]
        public void Choose_ByDistance_SlamSweepFanStream()
        {
            Vector2 b = new Vector2(0f, 1f);
            Assert.AreEqual(BossPattern.Slam, ChooseFor(b, b + Vector2.down * 1.5f), "point-blank");
            Assert.AreEqual(BossPattern.Sweep, ChooseFor(b, b + Vector2.down * 3f), "close");
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(b, b + Vector2.down * 6f), "mid range");
            Assert.AreEqual(BossPattern.BoltStream, ChooseFor(b, b + Vector2.down * 8.5f), "far");
        }

        [Test]
        public void Choose_PlayerBehindAPillar_GetsTheSlam_WhateverTheDistance()
        {
            // Pillar at x 5.4..6.6, y 2.9..4.1. Boss below it, player above: 5 units apart,
            // which in the open would be a fan volley.
            Vector2 boss = new Vector2(6f, 1f), player = new Vector2(6f, 6f);
            Assert.IsFalse(CollectorBoss.HasLineOfSight(P4.Sim(), boss, player));
            Assert.AreEqual(BossPattern.Slam, ChooseFor(boss, player));
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(new Vector2(0f, 1f), new Vector2(0f, 6f)), "same distance, in sight");
        }

        [Test]
        public void Choose_AfterTwoMeleeInARow_GoesRanged_EvenPointBlankOrHidden()
        {
            Vector2 b = new Vector2(0f, 1f);
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(b, b + Vector2.down * 1.5f, streak: 2));
            Assert.AreEqual(BossPattern.BoltStream, ChooseFor(b, b + Vector2.down * 8.5f, streak: 2));
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(new Vector2(6f, 1f), new Vector2(6f, 6f), streak: 2), "hidden: ammunition first");
            Assert.AreEqual(BossPattern.Slam, ChooseFor(b, b + Vector2.down * 1.5f, streak: 1), "one melee is not yet a streak");
        }

        // ---- No attack more than twice in a row (owner direction, D52) ------------------

        [Test]
        public void Choose_SameAttackTwiceInARow_SwitchesToItsPartner()
        {
            Vector2 b = new Vector2(0f, 1f);
            Vector2 mid = b + Vector2.down * 6f, far = b + Vector2.down * 8.5f;
            // Ranged: fan and stream swap, so ammunition still comes.
            Assert.AreEqual(BossPattern.BoltStream, ChooseFor(b, mid, last: BossPattern.FanVolley, sameInARow: 2));
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(b, far, last: BossPattern.BoltStream, sameInARow: 2));
            // Melee: slam and sweep swap.
            Assert.AreEqual(BossPattern.Sweep, ChooseFor(b, b + Vector2.down * 1.5f, last: BossPattern.Slam, sameInARow: 2));
            Assert.AreEqual(BossPattern.Slam, ChooseFor(b, b + Vector2.down * 3f, last: BossPattern.Sweep, sameInARow: 2));
            // Once is fine; and the partner rule only bites on the SAME pattern.
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(b, mid, last: BossPattern.FanVolley, sameInARow: 1));
            Assert.AreEqual(BossPattern.FanVolley, ChooseFor(b, mid, last: BossPattern.BoltStream, sameInARow: 2));
        }

        [Test]
        public void ThePlayerStandingStill_AtAnyRange_NeverSeesOneAttackThreeTimesInARow(
            [Values(1.5f, 3f, 6f, 9f)] float range)
        {
            var (sim, boss) = BossFight();
            // Teleports re-place the boss; keep the player at a fixed RANGE from it instead of a
            // fixed spot, so each run sits in one band of the choice table — the case where the
            // same pick repeated forever before this rule.
            var seen = new List<BossPattern>();
            int lastStarted = boss.Boss.PatternsStarted;
            for (int i = 0; i < 60 * 40 && sim.State == RunState.BossCombat; i++)
            {
                if (boss.Boss.Stage == BossStage.Reposition || boss.Boss.Stage == BossStage.Recover)
                {
                    Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                    sim.Player.Position = boss.Position + dir * range;
                }
                sim.Tick(P5.Still, P5.Dt);
                if (boss.Boss.PatternsStarted != lastStarted)
                {
                    lastStarted = boss.Boss.PatternsStarted;
                    seen.Add(boss.Boss.Pattern);
                }
            }
            Assert.GreaterOrEqual(seen.Count, 8, "fixture: enough patterns to judge");
            for (int i = 2; i < seen.Count; i++)
                Assert.IsFalse(seen[i] == seen[i - 1] && seen[i] == seen[i - 2],
                    $"three {seen[i]} in a row at range {range}: {string.Join(",", seen)}");
        }

        // ---- Moves around more (owner direction, D52) ------------------------------------

        [Test]
        public void RangedPatterns_StrafeToAFiringSpot_OffTheStraightLine()
        {
            var (sim, boss) = BossFight();
            var t = sim.Config.collector;
            int checkedSpots = 0;
            int lastStarted = boss.Boss.PatternsStarted;
            for (int i = 0; i < 60 * 40 && sim.State == RunState.BossCombat; i++)
            {
                sim.Player.Position = new Vector2(0f, -1f);
                Vector2 bossBefore = boss.Position;
                sim.Tick(P5.Still, P5.Dt);
                var b = boss.Boss;
                if (b.PatternsStarted == lastStarted) continue;
                lastStarted = b.PatternsStarted;
                if (b.Pattern != BossPattern.FanVolley && b.Pattern != BossPattern.BoltStream) continue;
                // The angle between "where the boss was" and "where it goes", seen from the
                // player: walking straight back would be ~0. Clamping at the arena edge can shrink
                // it, so the check uses a margin well under the tuned minimum.
                float turn = Vector2.Angle(bossBefore - sim.Player.Position, b.MoveTarget - sim.Player.Position);
                Assert.GreaterOrEqual(turn, t.strafeMinDeg * 0.5f, $"firing spot {b.MoveTarget} straight behind the boss");
                checkedSpots++;
            }
            Assert.GreaterOrEqual(checkedSpots, 3, "fixture: enough ranged patterns");
        }

        [Test]
        public void Tuning_TeleportFromSixUnits_EveryFiveSeconds_AndLongerRepositioning()
        {
            var t = GameConfig.CreateDefault().collector;
            Assert.AreEqual(6f, t.teleportMinDistance);
            Assert.AreEqual(5f, t.teleportCooldown);
            Assert.AreEqual(2, t.maxSameInARow);
            Assert.Greater(t.repositionMax, 1.0f, "more walking between attacks than before");
        }

        [Test]
        public void Collector_UsesAllFourPatterns_AsThePlayerMoves_AndAmmunitionNeverPausesLong()
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
            // The player walks a loop of four ranges, 5 s each: hugging, close, mid, far.
            float[] ranges = { 1.5f, 3f, 6f, 9f };
            var bounds = sim.Config.arena.bounds;
            double start = sim.Clock.Now;
            while (sim.State == RunState.BossCombat)
            {
                if (boss.Boss.Stage != BossStage.Teleport)
                {
                    float r = ranges[(int)((sim.Clock.Now - start) / 5.0) % ranges.Length];
                    Vector2 dir = (bounds.center - boss.Position).sqrMagnitude > 1e-4f ? (bounds.center - boss.Position).normalized : Vector2.down;
                    Vector2 p = boss.Position + dir * r;
                    p.x = Mathf.Clamp(p.x, bounds.xMin + 0.6f, bounds.xMax - 0.6f);
                    p.y = Mathf.Clamp(p.y, bounds.yMin + 0.6f, bounds.yMax - 0.6f);
                    sim.Player.Position = p;
                }
                sim.Tick(P5.Still, P5.Dt);
                seen.Add(boss.Boss.Pattern);
            }
            CollectionAssert.AreEquivalent(new[] { BossPattern.BoltStream, BossPattern.Sweep, BossPattern.FanVolley, BossPattern.Slam }, seen);
            Assert.LessOrEqual(worstGap, WorstBoltGap(sim.Config.collector), $"worst gap between boss bolts {worstGap:F2} s");
        }

        /// <summary>
        /// Plan: "no extended period with neither targets nor ammunition". The boss is always a
        /// target; the longest bolt drought the rules allow is two full melee patterns (the
        /// cap), a teleport wind-up, then the walk and wind-up of the ranged pattern the cap
        /// forces. About 7.2 s with the shipped numbers.
        /// </summary>
        static double WorstBoltGap(BossTuning t)
        {
            double melee = t.repositionMax + Mathf.Max(t.slamTelegraph, t.sweepTelegraph + t.sweepDuration) + t.recover;
            double ranged = t.repositionMax + Mathf.Max(t.fanTelegraph, t.streamTelegraph);
            return t.maxMeleeInARow * melee + t.teleportTelegraph + ranged + 0.1;
        }

        [Test]
        public void HuggingTheBoss_StillGetsBolts_WithinTwoMeleePatterns()
        {
            var (sim, boss) = BossFight();
            double last = sim.Clock.Now, worstGap = 0;
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.SourceActorId != boss.ActorId) return;
                worstGap = System.Math.Max(worstGap, sim.Clock.Now - last);
                last = sim.Clock.Now;
            };
            for (int i = 0; i < 60 * 40 && sim.State == RunState.BossCombat; i++)
            {
                Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                sim.Player.Position = boss.Position + dir * 1.5f;
                sim.Tick(P5.Still, P5.Dt);
            }
            Assert.LessOrEqual(worstGap, WorstBoltGap(sim.Config.collector), $"worst gap between boss bolts {worstGap:F2} s");
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
        /// Stand <paramref name="chooseAt"/> from the boss while it picks a pattern, then
        /// <paramref name="standAt"/> once a melee wind-up starts (so the pattern is chosen by
        /// one range and tested at another). Counts hits landed, and resolutions seen, per melee
        /// pattern. Invulnerability is cleared every tick so a bolt hit can never hide a melee
        /// hit behind its blink.
        /// </summary>
        static (Dictionary<BossPattern, int> hits, Dictionary<BossPattern, int> swings, List<int> amounts)
            MeleeHitsAt(float chooseAt, float standAt)
        {
            var (sim, boss) = BossFight();
            sim.Player.MaxHealth = sim.Player.Health = 999;
            var hits = new Dictionary<BossPattern, int> { [BossPattern.Sweep] = 0, [BossPattern.Slam] = 0 };
            var swings = new Dictionary<BossPattern, int> { [BossPattern.Sweep] = 0, [BossPattern.Slam] = 0 };
            var amounts = new List<int>();
            sim.Events.PlayerHit += (amount, src) =>
            {
                // Melee damage has no projectile: only count hits while the boss is mid-melee
                // (bolts can only come from ranged patterns).
                var b = boss.Boss;
                if (src == boss.ActorId && CollectorBoss.IsMelee(b.Pattern) && b.Stage == BossStage.Active)
                {
                    hits[b.Pattern]++;
                    amounts.Add(amount);
                }
            };
            sim.Events.EnemyFired += f =>
            {
                if (f == boss && CollectorBoss.IsMelee(boss.Boss.Pattern)) swings[boss.Boss.Pattern]++;
            };
            for (int i = 0; i < 1800 && sim.State == RunState.BossCombat; i++)
            {
                var b = boss.Boss;
                sim.Player.InvulnerableUntil = 0;
                bool winding = CollectorBoss.IsMelee(b.Pattern) && (b.Stage == BossStage.Telegraph || b.Stage == BossStage.Active);
                Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                sim.Player.Position = boss.Position + dir * (winding ? standAt : chooseAt);
                sim.Tick(P5.Still, P5.Dt);
            }
            return (hits, swings, amounts);
        }

        [Test]
        public void Slam_HitsAPlayerPointBlank_ForTwoHearts()
        {
            var (hits, swings, amounts) = MeleeHitsAt(1.6f, 1.6f);
            Assert.Greater(swings[BossPattern.Slam], 0, "fixture: point-blank draws slams");
            Assert.AreEqual(swings[BossPattern.Slam], hits[BossPattern.Slam], "every slam lands");
            CollectionAssert.AreEquivalent(new[] { 4 }, new HashSet<int>(amounts), "2 hearts = 4 half hearts");
        }

        [Test]
        public void Sweep_HitsAPlayerStandingClose()
        {
            var (hits, swings, _) = MeleeHitsAt(2.6f, 2.6f);
            Assert.Greater(swings[BossPattern.Sweep], 0, "fixture: close range draws sweeps");
            Assert.AreEqual(swings[BossPattern.Sweep], hits[BossPattern.Sweep]);
        }

        [Test]
        public void SweepAndSlam_MissAPlayerWhoBacksOutOfReach()
        {
            // sweep 3.0 and slam 3.2, plus the 0.45 body: 4.2 is out of both.
            var slam = MeleeHitsAt(1.6f, 4.2f);
            Assert.Greater(slam.swings[BossPattern.Slam], 0);
            Assert.AreEqual(0, slam.hits[BossPattern.Slam]);
            var sweep = MeleeHitsAt(2.6f, 4.2f);
            Assert.Greater(sweep.swings[BossPattern.Sweep], 0);
            Assert.AreEqual(0, sweep.hits[BossPattern.Sweep]);
        }

        // ---- Parrying the boss (D47) ----------------------------------------------------

        [Test]
        public void SweepParryArc_Geometry_OnlyInsideTheBladesReach()
        {
            // Boss at the origin swinging right. The band (1.15 out from the player, aimed back
            // at the boss) meets the gold arc (radius 1.95, 0.09 wide) when the player stands
            // about 1.95 + 1.15 = 3.1 out — inside the 3.0 reach plus the 0.45 body.
            var t = TestSims.Config.collector;
            float half = 45f, ring = 1.15f, ringW = 0.12f;
            bool Meet(Vector2 player, Vector2 aim) => ParryGeometry.BandMeetsArc(player, aim, half, ring, ringW,
                Vector2.zero, Vector2.right, t.sweepHalfAngle, t.sweepParryArcRadius, t.sweepParryArcWidth);
            Assert.IsTrue(Meet(new Vector2(3.1f, 0f), Vector2.left), "on the arc, facing the boss");
            Assert.IsFalse(Meet(new Vector2(3.1f, 0f), Vector2.right), "facing away");
            Assert.IsFalse(Meet(new Vector2(2.0f, 0f), Vector2.left), "too close: the band is past the arc");
            Assert.IsFalse(Meet(new Vector2(3.8f, 0f), Vector2.left), "too far: the band falls short");
            Assert.IsTrue(Meet(Geometry2D.Rotate(new Vector2(3.1f, 0f), 60f), Geometry2D.Rotate(Vector2.left, 60f)), "inside the 70 deg wedge");
            Assert.IsFalse(Meet(Geometry2D.Rotate(new Vector2(3.1f, 0f), 120f), Geometry2D.Rotate(Vector2.left, 120f)), "outside the wedge: no arc there");
            // The band is itself an arc, so it touches the gold arc over a RANGE of distances
            // (its cone edges reach in closer than its centre). Every one of them must be a spot
            // the blade would hit: a parry is never a free hit from safety.
            float farthest = 0f;
            for (float d = 0.5f; d <= 6f; d += 0.01f)
                if (Meet(new Vector2(d, 0f), Vector2.left)) farthest = d;
            Assert.Greater(farthest, 0f);
            Assert.LessOrEqual(farthest, t.sweepReach + 0.45f, "a parry spot is always in harm's way");
        }

        [Test]
        public void ParryingTheSweep_InItsGoldArcWindow_CancelsIt_AndFiresARiposte()
        {
            var (sim, boss) = BossFight();
            var t = sim.Config.collector;
            float d = t.sweepParryArcRadius + sim.Stats.ParryRingRadius;
            int parries = 0, ripostes = 0, bossHits = 0;
            bool pressed = false;
            sim.Events.StrikeParried += (a, _) => { if (a == boss) parries++; };
            sim.Events.ProjectileSpawned += p => { if (p.Shot.Kind == AttackKind.Riposte) ripostes++; };
            sim.Events.PlayerHit += (_, src) => { if (src == boss.ActorId && boss.Boss.Pattern == BossPattern.Sweep) bossHits++; };
            for (int i = 0; i < 60 * 30 && parries == 0 && sim.State == RunState.BossCombat; i++)
            {
                var b = boss.Boss;
                sim.Player.InvulnerableUntil = 0;
                Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                sim.Player.Position = boss.Position + dir * d;
                var cmd = P5.Still.WithAim(boss.Position);
                // Press exactly once, the first tick the gold arc is up.
                if (!pressed && b.Pattern == BossPattern.Sweep && b.Stage == BossStage.Telegraph && boss.ParryRimOpen(sim.Clock.Now))
                {
                    cmd = cmd.WithCatch();
                    pressed = true;
                }
                sim.Tick(cmd, P5.Dt);
            }
            Assert.IsTrue(pressed, "fixture: a sweep wind-up with its arc up was reached");
            Assert.AreEqual(1, parries);
            Assert.AreEqual(1, boss.Boss.SweepsParried);
            Assert.AreEqual(1, ripostes);
            Assert.AreEqual(BossStage.Recover, boss.Boss.Stage, "the swing is cancelled");
            Assert.AreEqual(0, bossHits);
        }

        [Test]
        public void TheSlam_CanNeverBeParried()
        {
            // Mash catch, aimed at the boss, from every range a slam can be chosen at.
            foreach (float d in new[] { 1.0f, 1.6f })
            {
                var (sim, boss) = BossFight();
                sim.Player.MaxHealth = sim.Player.Health = 999;
                int parries = 0, slams = 0, slamHits = 0;
                sim.Events.StrikeParried += (a, _) => { if (a == boss) parries++; };
                sim.Events.EnemyFired += f => { if (f == boss && boss.Boss.Pattern == BossPattern.Slam) slams++; };
                sim.Events.PlayerHit += (_, src) => { if (src == boss.ActorId && boss.Boss.Pattern == BossPattern.Slam && boss.Boss.Stage == BossStage.Active) slamHits++; };
                for (int i = 0; i < 60 * 20 && sim.State == RunState.BossCombat; i++)
                {
                    sim.Player.InvulnerableUntil = 0;
                    Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                    sim.Player.Position = boss.Position + dir * d;
                    sim.Tick(P5.Still.WithAim(boss.Position).WithCatch(), P5.Dt);
                }
                Assert.Greater(slams, 0, $"fixture at {d}: slams happened");
                Assert.AreEqual(0, parries, $"at {d}");
                Assert.AreEqual(slams, slamHits, $"at {d}: every slam landed");
            }
        }

        // ---- Teleport (D49) ---------------------------------------------------------------

        /// <summary>A boss fight on a private config copy, so tuning can be changed safely.</summary>
        static (ArenaSim sim, EnemyActor boss) BossFight(System.Action<GameConfig> tune)
        {
            var cfg = GameConfig.CreateDefault();
            tune(cfg);
            var sim = new ArenaSim(cfg, new RunSetup { Seed = 1, Mode = GameMode.Short, Sandbox = false });
            P5.ToBossCombat(sim);
            return (sim, sim.Boss);
        }

        [Test]
        public void FarAway_TheBossTeleportsBehindThePlayer_OnACooldown()
        {
            var (sim, boss) = BossFight(c => c.collector.teleportChance = 1f);
            var t = sim.Config.collector;
            // Two far spots; the player always stands at whichever is farther from the boss,
            // aiming up. Behind (-9,5) is open; behind (9,-5) is off the arena, so the
            // widening fallback angles are exercised too.
            Vector2 a = new Vector2(-9f, 5f), c = new Vector2(9f, -5f);
            var starts = new List<double>();
            int seen = 0;
            for (int i = 0; i < 60 * 40 && sim.State == RunState.BossCombat; i++)
            {
                var b = boss.Boss;
                if (b.Stage == BossStage.Teleport)
                {
                    if (starts.Count == 0 || sim.Clock.Now - starts[starts.Count - 1] > t.teleportTelegraph + 0.01)
                        if (b.StageEndsAt - sim.Clock.Now >= t.teleportTelegraph - P5.Dt - 1e-6) starts.Add(sim.Clock.Now);
                }
                else
                {
                    sim.Player.Position = (boss.Position - a).sqrMagnitude > (boss.Position - c).sqrMagnitude ? a : c;
                }
                var player = sim.Player.Position;
                sim.Tick(P5.Still.WithAim(player + Vector2.up * 5f), P5.Dt);
                if (b.Teleports > seen)
                {
                    seen = b.Teleports;
                    Vector2 rel = boss.Position - sim.Player.Position;
                    Assert.AreEqual(t.teleportBehindDistance, rel.magnitude, 1e-3f, "arrives at the set distance");
                    Assert.LessOrEqual(Vector2.Dot(rel, Vector2.up), 1e-3f, "behind or beside, never in front");
                }
            }
            Assert.GreaterOrEqual(seen, 3, "a far, still player keeps getting flanked");
            for (int i = 1; i < starts.Count; i++)
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], t.teleportTelegraph + t.teleportCooldown - 1e-6, "cooldown holds");
        }

        [Test]
        public void Close_TheBossNeverTeleports()
        {
            var (sim, boss) = BossFight(c => c.collector.teleportChance = 1f);
            for (int i = 0; i < 60 * 30 && sim.State == RunState.BossCombat; i++)
            {
                Vector2 dir = boss.AimDirection.sqrMagnitude > 0 ? boss.AimDirection : Vector2.down;
                sim.Player.Position = boss.Position + dir * 3f;
                sim.Tick(P5.Still, P5.Dt);
            }
            Assert.AreEqual(0, boss.Boss.Teleports);
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
            double bossFrom = sim.Clock.Now;
            while (sim.State == RunState.BossCombat) sim.Tick(Bot(sim), P5.Dt);
            TestContext.WriteLine($"end {sim.Summary.Reason} at {sim.Summary.Duration:F1}s, boss hp {boss.Health}, " +
                                  $"released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, damage taken {sim.Score.DamageTaken}");
            Assert.AreEqual(RunEndReason.Victory, sim.Summary.Reason,
                $"boss hp {boss.Health}, released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, " +
                $"kills {sim.Score.Kills}, taken {sim.Score.DamageTaken}");
            // The shared clock (D50) gives the boss whatever the encounters left over; this
            // bot clears them instantly, so the Victory above only proves the boss CAN fall
            // inside 180 s. How long it took is reported, and is the number to weigh against
            // how long a real player needs for the three encounters.
            double took = sim.Summary.Duration - bossFrom;
            Assert.Pass($"won {took:F1}s into the boss fight (run clock {sim.Summary.Duration:F1}s), " +
                        $"released {sim.Score.PacketsReleased}, hit {sim.Score.PacketsHit}, taken {sim.Score.DamageTaken}");
        }
    }
}
