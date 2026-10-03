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
    static class P4
    {
        public const float Dt = 1f / 60f;
        public static PlayerCommand Still => PlayerCommand.Moving(Vector2.zero);

        public static ArenaSim Sim(int seed = 1)
        {
            var sim = TestSims.Sandbox(seed);
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        /// <summary>Spawn an enemy already past its warning and parked (it will not act).</summary>
        public static EnemyActor Parked(ArenaSim sim, ActorCategory c, Vector2 at)
        {
            var e = sim.SpawnEnemy(c, at);
            e.ActiveAt = 0;
            e.Phase = EnemyPhase.Recover; // no brain transitions out of Recover except the pursuer's timer
            e.PhaseEndsAt = double.MaxValue;
            return e;
        }

        public static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Still, Dt);
        }

        public static ProjectileActor Returned(ArenaSim sim, string attackId, Vector2 from, Vector2 dir, int source = 900)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(attackId), source, sim.Ids.Next(), 0f);
            return sim.SpawnProjectile(s, AttackFaction.Returned, from, dir);
        }
    }

    // Phase 4: rockets, explosions, fans, attribution.
    public class AttackPayloadTests
    {
        [Test]
        public void ReturnedRocket_DamagesEnemiesInRadius_ButNotThePlayer()
        {
            var sim = P4.Sim();
            var a = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            var b = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4.2f, 1.3f));
            sim.Player.Position = new Vector2(2.6f, 1.4f); // inside the blast radius, off the rocket's path
            P4.Returned(sim, AttackIds.Rocket, new Vector2(0.5f, 0f), Vector2.right);
            P4.Run(sim, 60);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            // D92: the test rocket carries the default (Player) school, so a Siege takes x1.33.
            float hit = 5f * sim.Config.combat.otherSchoolDamage;
            Assert.AreEqual(a.MaxHealth - hit, a.Health, 1e-4f, "8 - 5 x other-school");
            Assert.AreEqual(b.MaxHealth - hit, b.Health, 1e-4f, "neighbour inside the radius");
        }

        [Test]
        public void Explosion_DamagesEachActorExactlyOnce_IncludingTheDirectHitTarget()
        {
            var sim = P4.Sim();
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4f, 0f));
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(4.6f, 0.9f));
            P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(9f, 0f)); // outside
            var hits = new Dictionary<int, int>();
            sim.Events.EnemyDamaged += (e, d) => { hits.TryGetValue(e.ActorId, out int n); hits[e.ActorId] = n + 1; Assert.AreEqual(DamageCategory.Explosion, d.Category); };
            int bursts = 0;
            sim.Events.Explosion += (_, __, ___) => bursts++;
            P4.Returned(sim, AttackIds.Rocket, new Vector2(0.5f, 0f), Vector2.right);
            P4.Run(sim, 90);
            Assert.AreEqual(1, bursts);
            Assert.AreEqual(2, hits.Count);
            foreach (var n in hits.Values) Assert.AreEqual(1, n);
        }

        [Test]
        public void ReturnedRocket_BurstsOnAPillar()
        {
            var sim = P4.Sim();
            var w = sim.Config.arena.pillars[1]; // (5.4, 2.9) 1.2 x 1.2
            var e = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(w.xMin - 0.2f, w.yMax + 0.9f));
            sim.Player.Position = new Vector2(1f, -3f);
            P4.Returned(sim, AttackIds.Rocket, new Vector2(1f, w.center.y), Vector2.right);
            ProjectileEndReason why = ProjectileEndReason.Cleared;
            sim.Events.ProjectileEnded += (p, r) => why = r;
            P4.Run(sim, 90);
            Assert.AreEqual(ProjectileEndReason.HitWall, why);
            Assert.AreEqual(e.MaxHealth - 5f * sim.Config.combat.otherSchoolDamage, e.Health, 1e-4f, "D92: Player-school rocket on a Siege");
        }

        [Test]
        public void HostileRocket_DealsOnePlayerHit_AndNoAreaDamageToEnemies()
        {
            var sim = P4.Sim();
            var bystander = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(0f, 1.1f));
            var siege = P4.Parked(sim, ActorCategory.SiegeFamiliar, new Vector2(6f, 0f));
            AttackEmitter.FireVolley(sim, AttackIds.Rocket, siege.ActorId, siege.Position, siege.Radius, Vector2.left, new[] { 0f });
            P4.Run(sim, 120);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.AreEqual(bystander.MaxHealth, bystander.Health);
        }

        [Test]
        public void KillingAShooterWithItsOwnShot_KeepsSourceAttribution()
        {
            var sim = P4.Sim();
            var acolyte = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(4f, 0f));
            acolyte.Health = 1f;
            DamageEvent kill = default;
            int kills = 0;
            sim.Events.EnemyKilled += (e, d) => { kills++; kill = d; };
            P4.Returned(sim, AttackIds.Bolt, new Vector2(1f, 0f), Vector2.right, source: acolyte.ActorId);
            P4.Run(sim, 60);
            Assert.AreEqual(1, kills);
            Assert.AreEqual(acolyte.ActorId, kill.TargetActorId);
            Assert.AreEqual(acolyte.ActorId, kill.SourceActorId, "its own snapshot");
            Assert.IsTrue(acolyte.Killed);
        }

        [Test]
        public void ReturnedFan_KeepsItsFiveShotSpread()
        {
            var sim = P4.Sim();
            var spreads = sim.Config.combat.scatter.volleySpreadDeg;
            double now = sim.Clock.Now;
            Assert.IsTrue(sim.Capture.TryActivate(now, sim.Stats));
            foreach (var off in spreads)
            {
                var snap = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), off);
                sim.Capture.TryCapture(snap, AttackFaction.Hostile, false, now, sim.Packets, sim.Stats, sim.Ids);
            }
            Assert.AreEqual(5, sim.Packets.Packets[0].Payloads.Count);

            Vector2 aim = new Vector2(0.6f, 0.8f);
            ReleaseService.Release(sim, sim.Packets.Packets[0], Vector2.zero, aim, 1f);
            var angles = new List<float>();
            foreach (var p in sim.Projectiles) angles.Add(Vector2.SignedAngle(aim, p.Velocity));
            angles.Sort();
            Assert.AreEqual(spreads.Length, angles.Count);
            for (int i = 0; i < spreads.Length; i++) Assert.AreEqual(spreads[i], angles[i], 0.01f);
        }

        [Test]
        public void CapturedRocket_ReturnsAsARocket()
        {
            var sim = P4.Sim();
            double now = sim.Clock.Now;
            sim.Capture.TryActivate(now, sim.Stats);
            var snap = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Rocket), 77, sim.Ids.Next(), 0f);
            Assert.AreEqual(CaptureResult.CreatedPacket, sim.Capture.TryCapture(snap, AttackFaction.Hostile, false, now, sim.Packets, sim.Stats, sim.Ids));
            Assert.AreEqual(4, sim.Packets.Packets[0].CapacityUsed);
            ReleaseService.Release(sim, sim.Packets.Packets[0], Vector2.zero, Vector2.right, 1f);
            Assert.AreEqual(AttackKind.Rocket, sim.Projectiles[0].Shot.Kind);
            Assert.AreEqual(AttackFaction.Returned, sim.Projectiles[0].Faction);
        }
    }

    // Phase 4: enemy behaviour, spawning, kill bookkeeping.
    public class EnemyEncounterTests
    {
        [Test]
        public void EnemiesNeverSpawnOnThePlayer()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var sim = P4.Sim(seed);
                var b = sim.Config.arena.bounds;
                sim.Player.Position = new Vector2(sim.Random.Range(b.xMin + 1, b.xMax - 1), sim.Random.Range(b.yMin + 1, b.yMax - 1));
                foreach (var f in EnemySpawnService.Formations) EnemySpawnService.Spawn(sim, f);
                foreach (var e in sim.Enemies)
                    Assert.GreaterOrEqual((e.Position - sim.Player.Position).magnitude, sim.Config.combat.minSpawnDistance,
                        $"seed {seed} {e.Category}");
            }
        }

        [Test]
        public void SpawnWarning_IsHarmlessAndImmune()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(0.9f, 0f));
            Assert.IsFalse(sim.DamageEnemy(e, 5f, DamageCategory.ReturnedProjectile, default, 0));
            P4.Run(sim, Mathf.FloorToInt(sim.Config.combat.spawnWarning / P4.Dt) - 2);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.AreEqual(e.MaxHealth, e.Health);
        }

        [Test]
        public void Pursuer_StrikeHitsAPlayerWhoStays_Once()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(1.3f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            int telegraphs = 0;
            sim.Events.EnemyTelegraph += _ => telegraphs++;
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.pursuer.telegraph / P4.Dt) + 3);
            Assert.AreEqual(1, telegraphs);
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void Pursuer_StrikeMissesAPlayerWhoLeavesTheMarker()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(1.3f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(EnemyPhase.Telegraph, e.Phase);
            sim.Player.Position = new Vector2(-3.5f, 0f); // step well out of the strike circle
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.pursuer.telegraph / P4.Dt) + 2);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.AreEqual(EnemyPhase.Recover, e.Phase);
        }

        [Test]
        public void ScatterCasterFiresAFiveShotFan_SiegeFiresOneRocket()
        {
            var sim = P4.Sim();
            var s = sim.SpawnEnemy(ActorCategory.ScatterCaster, new Vector2(-6f, 0f));
            s.ActiveAt = 0; s.Phase = EnemyPhase.Idle; s.PhaseEndsAt = 0;
            var f = sim.SpawnEnemy(ActorCategory.SiegeFamiliar, new Vector2(7f, 0f));
            f.ActiveAt = 0; f.Phase = EnemyPhase.Idle; f.PhaseEndsAt = 0;
            var fired = new Dictionary<int, List<AttackKind>>();
            sim.Events.ProjectileSpawned += p =>
            {
                if (!fired.TryGetValue(p.Shot.SourceActorId, out var l)) fired[p.Shot.SourceActorId] = l = new List<AttackKind>();
                l.Add(p.Shot.Kind);
            };
            // Long enough for the slower siege telegraph, short enough for one volley each.
            P4.Run(sim, Mathf.CeilToInt(sim.Config.combat.siege.telegraph / P4.Dt) + 2);
            Assert.AreEqual(5, fired[s.ActorId].Count);
            CollectionAssert.AreEqual(new[] { AttackKind.Rocket }, fired[f.ActorId]);
        }

        [Test]
        public void Pursuer_RoutesAroundAPillar_InsteadOfStickingBehindIt()
        {
            // Owner bug: a pursuer with a pillar dead between it and the player pressed into the
            // pillar face and stayed there. Pillar at x -6.6..-5.4, y 2.9..4.1; player below it,
            // pursuer directly above, so straight-line seeking hits the face square-on.
            var sim = P4.Sim();
            sim.Player.Position = new Vector2(-6f, 1.0f);
            sim.Player.InvulnerableUntil = double.MaxValue;
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(-6f, 6.2f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            bool reached = false;
            for (int i = 0; i < 60 * 5 && !reached; i++)
            {
                sim.Tick(P4.Still, P4.Dt);
                reached = e.Phase == EnemyPhase.Telegraph;
            }
            Assert.IsTrue(reached, $"pursuer stuck at {e.Position}");
        }

        [Test]
        public void Pursuer_ReachesThePlayer_FromSquareBehindEveryPillarFace()
        {
            // Every pillar, every face: the pursuer starts 1.2 beyond the face centre and the
            // player stands 1.6 beyond the opposite face, so the straight line is the worst case.
            var dirs = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            foreach (var pillar in TestSims.Config.arena.pillars)
            foreach (var d in dirs)
            {
                var sim = P4.Sim();
                Vector2 half = pillar.size * 0.5f;
                float reach = Mathf.Abs(Vector2.Dot(half, d));
                sim.Player.Position = pillar.center - d * (reach + 1.6f);
                sim.Player.InvulnerableUntil = double.MaxValue;
                var e = sim.SpawnEnemy(ActorCategory.Pursuer, pillar.center + d * (reach + 1.2f));
                e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
                bool reached = false;
                for (int i = 0; i < 60 * 6 && !reached; i++)
                {
                    sim.Tick(P4.Still, P4.Dt);
                    reached = e.Phase == EnemyPhase.Telegraph;
                }
                Assert.IsTrue(reached, $"pillar {pillar.center} side {d}: stuck at {e.Position}");
            }
        }

        [Test]
        public void RangedEnemy_TooFar_RoutesAroundAPillarToo()
        {
            var sim = P4.Sim();
            sim.Player.Position = new Vector2(-6f, -7f);
            sim.Player.InvulnerableUntil = double.MaxValue;
            var e = P4.Parked(sim, ActorCategory.Acolyte, new Vector2(-6f, 6.5f));
            e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = double.MaxValue; // moves, never fires
            P4.Run(sim, 60 * 6);
            Assert.LessOrEqual((e.Position - sim.Player.Position).magnitude, sim.Config.combat.acolyte.preferredMax + 0.5f,
                $"acolyte stuck at {e.Position}");
        }

        [Test]
        public void Despawn_IsNotAKill_AndOverkillRaisesOneKill()
        {
            var sim = P4.Sim();
            var gone = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(6f, 0f));
            var dead = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(-6f, 0f));
            int kills = 0, despawns = 0;
            sim.Events.EnemyKilled += (_, __) => kills++;
            sim.Events.EnemyDespawned += _ => despawns++;
            sim.DespawnEnemy(gone);
            Assert.IsTrue(sim.DamageEnemy(dead, 5f, DamageCategory.Explosion, default, 0));
            Assert.IsFalse(sim.DamageEnemy(dead, 5f, DamageCategory.Explosion, default, 0));
            Assert.AreEqual(1, kills);
            Assert.AreEqual(1, despawns);
            Assert.IsFalse(gone.Killed);
            Assert.IsTrue(dead.Killed);
            Assert.AreEqual(10, dead.KillValue);
        }

        [Test]
        public void Formations_AllSpawn_AndOnlyTheDeliberateOneIsMeleeOnly()
        {
            int meleeOnly = 0;
            foreach (var f in EnemySpawnService.Formations)
            {
                var sim = P4.Sim();
                Assert.DoesNotThrow(() => EnemySpawnService.Spawn(sim, f), f.Name);
                Assert.AreEqual(f.Members.Length, sim.Enemies.Count);
                if (!f.HasRanged) meleeOnly++;
            }
            Assert.AreEqual(1, meleeOnly);
        }

        [Test]
        public void EliteKillValue_IsOneAndAHalfTimesBase()
        {
            var sim = P4.Sim();
            Assert.AreEqual(30, sim.SpawnEnemy(ActorCategory.ScatterCaster, new Vector2(6, 0), elite: true).KillValue);
            Assert.AreEqual(38, sim.SpawnEnemy(ActorCategory.SiegeFamiliar, new Vector2(-6, 0), elite: true).KillValue);
        }
    }

    // Body contact (owner direction, D51): touching an enemy costs half a heart (the boss: one
    // heart) and grants a SHORTER invulnerability than a real hit, so contact is a nudge to
    // move, not a free shield to stand inside a crowd with.
    public class ContactDamageTests
    {
        [Test]
        public void TouchingAnEnemy_CostsHalfAHeart_ThenABriefInvulnerability()
        {
            var sim = P4.Sim();
            var e = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(0.5f, 0f));

            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(5, sim.Score.DamageTaken, "half a heart");
            Assert.AreEqual(sim.Stats.ContactInvulnerability, sim.Player.InvulnerableUntil - sim.Clock.Now, P4.Dt + 1e-4,
                "the contact blink, not the longer hit blink");
            Assert.Less(sim.Stats.ContactInvulnerability, sim.Stats.HitInvulnerability);
            // Still touching: nothing more until the blink ends...
            P4.Run(sim, Mathf.FloorToInt(sim.Stats.ContactInvulnerability / P4.Dt) - 2);
            Assert.AreEqual(5, sim.Score.DamageTaken);
            // ...then the next half heart.
            P4.Run(sim, 4);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.IsTrue(e.Alive);
        }

        [Test]
        public void TwoBodiesOnOneTick_CostOneContactHit()
        {
            var sim = P4.Sim();
            P4.Parked(sim, ActorCategory.Pursuer, new Vector2(0.5f, 0f));
            P4.Parked(sim, ActorCategory.Pursuer, new Vector2(-0.5f, 0f));
            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(5, sim.Score.DamageTaken);
        }

        [Test]
        public void AnEnemyInItsSpawnWarning_DoesNotHurtByContact()
        {
            var sim = P4.Sim();
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(0.5f, 0f));
            e.ActiveAt = double.MaxValue;
            P4.Run(sim, 30);
            Assert.AreEqual(0, sim.Score.DamageTaken);
        }

        [Test]
        public void NoContact_OneHairApart()
        {
            var sim = P4.Sim();
            var e = P4.Parked(sim, ActorCategory.Pursuer, Vector2.zero);
            e.Position = e.PrevPosition = new Vector2(e.Radius + sim.Player.Radius + 0.01f, 0f);
            P4.Run(sim, 30);
            Assert.AreEqual(0, sim.Score.DamageTaken);
        }

        [Test]
        public void ADuringDashOrHitBlink_ContactDoesNothing()
        {
            var sim = P4.Sim();
            sim.Player.InvulnerableUntil = 1.0;
            P4.Parked(sim, ActorCategory.Pursuer, new Vector2(0.5f, 0f));
            P4.Run(sim, 30);
            Assert.AreEqual(0, sim.Score.DamageTaken);
        }

        [Test]
        public void TouchingTheBoss_CostsOneHeart()
        {
            var sim = P5.Short();
            P5.ToBossCombat(sim);
            var boss = sim.Boss;
            int amount = -1;
            sim.Events.PlayerHit += (a, src) => { if (src == boss.ActorId && amount < 0) amount = a; };
            // Wait for a moment the boss is only walking (no attack resolving), then touch it.
            for (int i = 0; i < 60 * 20 && !(boss.IsActive(sim.Clock.Now) && boss.Boss.Stage == BossStage.Reposition); i++)
                sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(BossStage.Reposition, boss.Boss.Stage, "fixture");
            sim.Player.InvulnerableUntil = 0;
            sim.Player.Position = boss.Position + Vector2.right * (boss.Radius + sim.Player.Radius - 0.2f);
            sim.Tick(P5.Still, P5.Dt);
            Assert.AreEqual(10, amount, "boss body contact costs ten seconds");
        }
    }

    // Damage amounts in half hearts (D51): five hearts, ordinary hits one heart.
    public class LifeClockTuningTests
    {
        [Test]
        public void OrdinaryHitsAndBossContactsUseLifeSeconds()
        {
            var sim = P4.Sim();
            Assert.AreEqual(180f, sim.LifeSeconds);
            Assert.AreEqual(10, sim.Config.combat.enemyHitDamage);
            Assert.AreEqual(10, sim.Attacks.Get(AttackIds.Bolt).HostileDamage);
            Assert.AreEqual(5, sim.Config.combat.enemyContactDamage);
            Assert.AreEqual(10, sim.Config.collector.contactDamage);
        }

        [Test]
        public void TheShippedConfigAsset_UsesTheLifeClock()
        {
            // The asset serializes the player section, so a changed code default alone would
            // not reach the game. Guard the asset itself.
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Game/Data/GameConfig.asset");
            Assert.NotNull(cfg);
            var sim = new ArenaSim(cfg, RunSetup.ForSandbox(0));
            Assert.AreEqual(180f, sim.LifeSeconds);
            Assert.AreEqual(10, sim.Attacks.Get(AttackIds.Bolt).HostileDamage);
        }
    }

    // Owner direction (D56): pursuers 1.4x as fast (3.0 → 4.2). Run length 300 → 180 later (D65).
    public class D56TuningTests
    {
        [Test]
        public void ThreeMinuteLife_AndFasterPursuers()
        {
            var cfg = GameConfig.CreateDefault();
            Assert.AreEqual(180f, cfg.shortMode.runLength);
            Assert.AreEqual(3.0f * 1.4f, cfg.combat.pursuer.moveSpeed, 1e-5f);
            Assert.Less(cfg.combat.pursuer.moveSpeed, cfg.player.moveSpeed, "the player can still outrun them");
        }
    }
}
