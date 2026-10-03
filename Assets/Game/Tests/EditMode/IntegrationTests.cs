using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Phase 13 integration soak: whole runs, start to results, played by a simple bot in every
    /// mode with every capture style, with and without passives, through random pauses and focus
    /// losses, picking every kind of upgrade. Each tick re-checks the invariants that the
    /// focused suites prove one at a time; the end checks the summary and that it finalizes
    /// into a profile exactly once. This is a crash-and-invariant net, not a balance verdict:
    /// the bot is far weaker than a person, so how long it lasts says nothing about tuning.
    /// </summary>
    public class IntegrationTests
    {
        const float Dt = 1f / 60f;

        static readonly string[] Advanced = { SkillTree.PrecisionAngle, SkillTree.MobilityDashRecovery, SkillTree.ResilienceTime };

        /// <summary>
        /// Plays like a careless person: walks around the arena centre, catches the nearest hostile
        /// shot when it can, fires at the nearest enemy, swaps slots now and then, and dashes
        /// (a capturing dash, for Daredevil) when something is close. Seeded, so a failure replays.
        /// </summary>
        sealed class Bot
        {
            readonly System.Random rng;
            public Bot(int seed) { rng = new System.Random(seed); }

            public PlayerCommand Next(ArenaSim sim)
            {
                var p = sim.Player;
                double now = sim.Clock.Now;
                Vector2 target = p.Position + Vector2.up;
                float bestEnemy = float.MaxValue;
                foreach (var e in sim.Enemies)
                {
                    if (!e.Alive) continue;
                    float d = (e.Position - p.Position).sqrMagnitude;
                    if (d < bestEnemy) { bestEnemy = d; target = e.Position; }
                }
                // Drift around the centre so the bot neither camps a wall nor stands still.
                Vector2 centre = sim.Config.arena.bounds.center;
                Vector2 orbit = new Vector2(Mathf.Cos((float)now * 0.7f), Mathf.Sin((float)now * 0.9f)) * 4f;
                var cmd = PlayerCommand.Moving((centre + orbit - p.Position).normalized);

                ProjectileActor incoming = null;
                float best = 3f;
                foreach (var pr in sim.Projectiles)
                {
                    if (!pr.Active || pr.Faction != AttackFaction.Hostile) continue;
                    float dd = (pr.Position - p.Position).magnitude;
                    if (dd < best) { best = dd; incoming = pr; }
                }
                if (incoming != null && sim.Capture.IsReady(now))
                {
                    // D89: a full hand cannot catch — pocket first (Q and catch on one tick is
                    // "pocket, then catch", the cycle runs first).
                    cmd = cmd.WithAim(incoming.Position).WithCatch();
                    if (!sim.Packets.HandFree && sim.Packets.FreeSlots > 0) cmd = cmd.WithCycle();
                    return rng.NextDouble() < 0.5 ? cmd.WithDash() : cmd;
                }
                if (rng.NextDouble() < 0.01) cmd = cmd.WithCycle();
                if (bestEnemy < 4f && rng.NextDouble() < 0.05) cmd = cmd.WithDash();
                var held = sim.Packets.ReleaseCandidate();
                if (held != null && !sim.IsPrimed(held))
                {
                    // D90: too fresh to fire — swap to the pocketed hex if that one is ready.
                    int other = (sim.Packets.SelectedSlot + 1) % sim.Packets.SlotCount;
                    var pocket = sim.Packets.InSlot(other);
                    if (pocket != null && sim.IsPrimed(pocket)) return cmd.WithAim(target).WithCycle();
                    return cmd.WithAim(target);
                }
                return cmd.WithAim(target).WithRelease();
            }
        }

        static IEnumerable<TestCaseData> Cases()
        {
            int seed = 100;
            foreach (var mode in new[] { GameMode.Short, GameMode.Endless })
                foreach (var style in CaptureStyles.All)
                    foreach (var kind in new[] { "fresh", "advanced", "assisted" })
                        yield return new TestCaseData(mode, style.Id, kind, seed++)
                            .SetName($"Soak_{mode}_{style.Id}_{kind}");
        }

        /// <summary>Kills the first active ordinary enemy and takes a twentieth off the boss, both via
        /// DamageEnemy with a returned-projectile hit, the same path a caught bolt takes.</summary>
        static void Assist(ArenaSim sim)
        {
            if (sim.State != RunState.Combat && sim.State != RunState.BossCombat) return;
            var hit = new AttackSnapshot { Kind = AttackKind.Bolt, SourceActorId = 900 };
            foreach (var e in sim.Enemies)
                if (e.Alive && !e.IsBoss && sim.Clock.Now >= e.ActiveAt)
                {
                    sim.DamageEnemy(e, e.Health, DamageCategory.ReturnedProjectile, hit, 0);
                    break;
                }
            var boss = sim.Boss;
            if (boss != null && boss.Alive && sim.State == RunState.BossCombat)
                sim.DamageEnemy(boss, boss.MaxHealth * 0.05f, DamageCategory.ReturnedProjectile, hit, 0);
        }

        [TestCaseSource(nameof(Cases))]
        public void AWholeRun_KeepsEveryInvariant_AndFinalizesOnce(GameMode mode, string styleId, string kind, int seed)
        {
            // Unassisted, the bot dies or runs dry before any boss (measured: 37-194 s, at most six
            // waves), so "assisted" runs make it invulnerable and land a real kill every half second
            // through the normal damage path. Those reach the boss, a short-mode victory, several
            // endless cycles and a retirement, which the bot alone never does. Nothing about them is
            // debug: they finalize like any run.
            bool advanced = kind != "fresh", assisted = kind == "assisted";
            var config = TestSims.Config;
            var passives = advanced ? new List<string>(Advanced) : new List<string>();
            var setup = new RunSetup
            {
                Mode = mode, Seed = seed, StyleId = styleId, PassiveIds = passives,
                Stats = Loadout.Resolve(config, passives, styleId),
            };
            var sim = new ArenaSim(config, setup);
            if (assisted) sim.Player.InvulnerableUntil = 1e9;
            var bot = new Bot(seed);
            var rng = new System.Random(seed * 31);
            int cap = mode == GameMode.Endless ? config.endless.maxOrdinaryEnemies : config.shortMode.maxOrdinaryEnemies;
            var upgradesSeen = new HashSet<UpgradeId>();
            int choices = 0, pausedTicks = 0;
            // 20 minutes of ticks at most; an endless run the bot survives that long is retired.
            for (int i = 0; i < 20 * 60 * 60 && sim.State != RunState.Results; i++)
            {
                switch (sim.State)
                {
                    case RunState.UpgradeChoice:
                        choices++;
                        // Rotate through offer slots so different upgrades get held.
                        var pick = sim.Offers[choices % sim.Offers.Count];
                        Assert.IsTrue(sim.ChooseUpgrade(choices % sim.Offers.Count));
                        upgradesSeen.Add(pick.Id);
                        Assert.AreEqual(pick.Id, sim.ActiveUpgrade.Value.Id);
                        continue;
                    case RunState.BossIntro:
                        Assert.IsTrue(sim.CompleteBossIntro());
                        continue;
                }

                // Now and then the window loses focus or the menu opens: nothing may move.
                if (rng.NextDouble() < 0.002)
                {
                    var reason = rng.NextDouble() < 0.5 ? PauseReason.Menu : PauseReason.FocusLost;
                    var before = sim.State;
                    double now = sim.Clock.Now;
                    float life = sim.LifeSeconds;
                    sim.SetPause(reason, true);
                    for (int k = 0; k < 30; k++) { sim.Tick(bot.Next(sim), Dt); pausedTicks++; }
                    Assert.AreEqual(now, sim.Clock.Now, "paused: the clock holds");
                    Assert.AreEqual(life, sim.LifeSeconds, "paused: life holds");
                    sim.SetPause(reason, false);
                    Assert.AreEqual(before, sim.State, "resume returns to the exact state");
                }

                if (assisted && i % 30 == 0) Assist(sim);
                sim.Tick(bot.Next(sim), Dt);

                Assert.LessOrEqual(sim.AliveOrdinaryCount(), cap, "enemy cap");
                Assert.GreaterOrEqual(sim.LifeSeconds, 0f);
                Assert.LessOrEqual(sim.LifeSeconds, sim.Stats.StartingSeconds + 1e-3f, "life never exceeds the cap");
                if (mode == GameMode.Endless)
                    Assert.LessOrEqual(sim.HostileProjectileCount(), config.endless.maxHostileProjectiles, "projectile budget");
                if (sim.State == RunState.Results) break;
            }
            if (sim.State == RunState.UpgradeChoice) Assert.IsTrue(sim.RetireRun());
            if (sim.State != RunState.Results)
            {
                // Still alive after 20 minutes (endless only): end it at the next choice.
                Assert.AreEqual(GameMode.Endless, mode, "a short run cannot outlast its clock");
                while (sim.State != RunState.UpgradeChoice && sim.State != RunState.Results)
                {
                    if (sim.State == RunState.BossIntro) sim.CompleteBossIntro();
                    sim.Tick(bot.Next(sim), Dt);
                }
                if (sim.State == RunState.UpgradeChoice) Assert.IsTrue(sim.RetireRun());
            }

            var s = sim.Summary;
            Assert.IsNotNull(s);
            Assert.AreEqual(mode, s.Mode);
            Assert.AreEqual(styleId, s.StyleId);
            Assert.Greater(s.Duration, 0f);
            Assert.GreaterOrEqual(s.Kills, 0);
            Assert.GreaterOrEqual(s.HitRate, 0f);
            Assert.LessOrEqual(s.HitRate, 1f);
            Assert.AreEqual(0, sim.HostileProjectileCount(), "nothing flies into the results");
            if (mode == GameMode.Short) Assert.AreNotEqual(RunEndReason.Retired, s.Reason);
            else Assert.AreNotEqual(RunEndReason.Victory, s.Reason, "endless has no victory");

            if (assisted)
            {
                // The point of assistance: prove the far end of each mode was actually reached.
                if (mode == GameMode.Short) Assert.AreEqual(RunEndReason.Victory, s.Reason);
                else
                {
                    Assert.AreEqual(RunEndReason.Retired, s.Reason);
                    Assert.GreaterOrEqual(s.BossesDefeated, 2, "the repeated boss was beaten too");
                }
            }

            var service = ProfileService.Load(new MemoryProfileStorage());
            var first = service.FinalizeRun(s, setup);
            Assert.IsTrue(first.Applied, first.SkippedBecause);
            Assert.AreEqual("already finalized", service.FinalizeRun(s, setup).SkippedBecause, "never twice");
            Assert.AreEqual(1, service.Profile.stats.runs);
            Assert.IsTrue(ProfileService.Validate(service.Profile, out var why), "the profile stays valid: " + why);
            TestContext.WriteLine($"{mode}/{styleId}/{kind}: {s.Reason} at {s.Duration:F0}s, " +
                                  $"kills {s.Kills}, waves {s.WavesCompleted}, bosses {s.BossesDefeated}, choices {choices}, " +
                                  $"upgrades {upgradesSeen.Count}, paused ticks {pausedTicks}, xp {first.Xp?.Total}");
        }
    }
}
