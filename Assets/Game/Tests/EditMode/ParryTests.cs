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
    // Parry (D26, made harder in D36, timed by the rim in D46): early in a catch (the parry
    // window, half the catch window), the player's thin parry band must touch the thin rim of
    // the Pursuer's strike circle on a tick while that rim is SHOWN. The rim appears just after
    // the wind-up starts and is gone before the strike lands, so the strike instant itself can
    // never be parried. A parry cancels the strike and a riposte flies at the attacker.
    //
    // Fixture geometry: the pursuer sits at the origin, wound up and aim-locked to the right,
    // so its strike circle is centred at C = (0.8, 0), radius 0.75, rim band (0.09 wide) centre line 0.705.
    // The player stands d to the right of C, aiming back at it. Along the aim the band (radius
    // 1.15) is |d - 1.15| from C, so d = 0.445 puts it exactly on the rim's centre line.
    public class ParryTests
    {
        const float OnTheRim = 0.445f;
        static readonly Vector2 C = new Vector2(0.8f, 0f);

        static ArenaSim SimWithPlayerAt(float d)
        {
            var sim = P4.Sim();
            sim.Player.Position = C + new Vector2(d, 0f);
            return sim;
        }

        /// <summary>
        /// A pursuer at the origin, mid wind-up, aim locked right, striking in
        /// <paramref name="strikeIn"/> s. Its rim shows from <paramref name="rimOpensIn"/> s
        /// for <paramref name="rimFor"/> s; by default from now until 0.05 s before the strike,
        /// the same gap the real brain leaves.
        /// </summary>
        static EnemyActor WoundUpPursuer(ArenaSim sim, double strikeIn, double rimOpensIn = 0, double rimFor = -1)
        {
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, Vector2.zero);
            e.ActiveAt = 0;
            e.Phase = EnemyPhase.Telegraph;
            e.AimDirection = Vector2.right;
            e.AimLocked = true;
            double now = sim.Clock.Now;
            e.PhaseEndsAt = now + strikeIn;
            e.ParryRimOpensAt = now + rimOpensIn;
            e.ParryRimClosesAt = rimFor >= 0 ? e.ParryRimOpensAt + rimFor : e.PhaseEndsAt - 0.05;
            return e;
        }

        static PlayerCommand Aim(Vector2 dir) => P4.Still.WithAim(dir * 5f);
        static readonly PlayerCommand AtStrike = Aim(Vector2.left);
        static readonly PlayerCommand AwayFromStrike = Aim(Vector2.right);

        /// <summary>Press catch now, then let the strike (and any riposte) resolve.</summary>
        static void CatchAndResolve(ArenaSim sim, PlayerCommand aim, int ticks = 40)
        {
            sim.Tick(aim.WithCatch(), P4.Dt);
            P4.Run(sim, ticks, aim);
        }

        [Test]
        public void BandOnTheRim_EarlyInTheCatch_ParriesAndTheRiposteKillsIt()
        {
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.1);
            var parries = new List<EnemyActor>();
            sim.Events.StrikeParried += (attacker, _) => parries.Add(attacker);
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(0, sim.Score.DamageTaken, "parried strike deals no damage");
            CollectionAssert.AreEqual(new[] { e }, parries, "exactly one parry");
            Assert.IsTrue(e.Killed, "riposte (2 dmg) kills a 2-health pursuer");
        }

        [Test]
        public void ParryWindow_IsHalfTheCatchWindow_FromItsStart()
        {
            var sim = P4.Sim();
            Assert.AreEqual(sim.Stats.CaptureWindow * 0.5f, sim.Stats.ParryWindow, 1e-6f);
            sim.Tick(P4.Still.WithCatch(), P4.Dt);
            Assert.AreEqual(sim.Capture.WindowOpensAt + sim.Stats.ParryWindow, sim.Capture.ParryEndsAt, 1e-6);
        }

        [Test]
        public void PressedBeforeTheRimShows_ParryWindowSpent_StillHurts()
        {
            // The rim appears 0.2 s after the press: the 0.125 s parry window is over by then,
            // even though the catch window (0.25 s) is still open. Too early is a miss.
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.4, rimOpensIn: 0.2, rimFor: 0.15);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.IsFalse(e.Killed);
        }

        [Test]
        public void PressedJustBeforeTheRimShows_WindowsOverlap_Parries()
        {
            // The parry window (0.125 s) is still open when the rim appears 0.08 s later.
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.4, rimOpensIn: 0.08, rimFor: 0.15);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(1, parries);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.IsTrue(e.Killed);
        }

        [Test]
        public void PressedAfterTheRimIsGone_StillHurts()
        {
            // The rim showed and closed before the press; the strike is still to come. A press
            // inside the parry window but after the rim cannot parry: the rim IS the window.
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.3, rimOpensIn: 0, rimFor: 0.05);
            P4.Run(sim, 6, AtStrike); // 0.1 s: the rim is gone
            Assert.IsFalse(e.ParryRimOpen(sim.Clock.Now));
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.IsFalse(e.Killed);
        }

        [Test]
        public void RealPursuer_RimShowsJustAfterTheWindUpStarts_AndIsGoneBeforeTheStrike()
        {
            var sim = P4.Sim();
            var t = sim.Config.combat.pursuer;
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(1.3f, 0f));
            e.ActiveAt = 0; e.Phase = EnemyPhase.Idle; e.PhaseEndsAt = 0;
            sim.Player.InvulnerableUntil = double.MaxValue; // only the timing is under test
            double telegraphAt = -1, firstOpen = -1, lastOpen = -1, strikeAt = -1;
            sim.Events.EnemyTelegraph += _ => telegraphAt = sim.Clock.Now;
            sim.Events.EnemyFired += _ => strikeAt = sim.Clock.Now;
            for (int i = 0; i < 90 && strikeAt < 0; i++)
            {
                sim.Tick(P4.Still, P4.Dt);
                if (e.ParryRimOpen(sim.Clock.Now)) { if (firstOpen < 0) firstOpen = sim.Clock.Now; lastOpen = sim.Clock.Now; }
            }
            Assert.Greater(telegraphAt, -1, "fixture: the wind-up started");
            Assert.Greater(strikeAt, -1, "fixture: the strike landed");
            Assert.AreEqual(telegraphAt + t.parryRimDelay, firstOpen, P4.Dt + 1e-6, "appears just after the wind-up starts");
            Assert.AreEqual(t.parryRimDuration, lastOpen - firstOpen, P4.Dt + 1e-6, "short-lived");
            Assert.LessOrEqual(lastOpen, strikeAt - 0.05 + 1e-6, "gone before the strike lands");
            Assert.IsFalse(e.ParryRimOpen(strikeAt));
        }

        [Test]
        public void InsideTheStrike_BandsApart_TheStrikeLands()
        {
            // Standing on the strike centre: the band (1.15 out) clears the rim (0.705) by far.
            // Facing it, in time, inside the circle — and still hit: the edges must meet.
            var sim = SimWithPlayerAt(0.05f);
            WoundUpPursuer(sim, 0.1);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void FacingAway_TheStrikeStillHurts()
        {
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.1);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AwayFromStrike);
            Assert.AreEqual(0, parries);
            Assert.AreEqual(10, sim.Score.DamageTaken);
            Assert.IsFalse(e.Killed);
        }

        [Test]
        public void NoCatchPressed_TheStrikeLands()
        {
            var sim = SimWithPlayerAt(OnTheRim);
            WoundUpPursuer(sim, 0.1);
            P4.Run(sim, 40, AtStrike);
            Assert.AreEqual(10, sim.Score.DamageTaken);
        }

        [Test]
        public void BandOnTheNearRim_FromJustOutsideTheStrike_StillParries()
        {
            // Pinned ruling (D36): the rule is "the edges meet", not "you were going to be hit".
            // At d = 1.8 the player is outside the 1.1 damage reach, but the band (1.15 out) lies
            // on the rim's near side: a spacing parry. Change here if the owner rules otherwise.
            var sim = SimWithPlayerAt(1.8f);
            var e = WoundUpPursuer(sim, 0.1);
            int parries = 0;
            sim.Events.StrikeParried += (_, __) => parries++;
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(1, parries);
            Assert.IsTrue(e.Killed);
        }

        [Test]
        public void Riposte_IsAReturnedShotAttributedToTheAttacker()
        {
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.1);
            // Copy at spawn: projectiles are pooled, so a held reference may be reused later.
            int count = 0;
            AttackFaction faction = default;
            AttackSnapshot shot = default;
            Vector2 vel = default;
            sim.Events.ProjectileSpawned += p =>
            {
                if (p.Shot.Kind != AttackKind.Riposte) return;
                count++; faction = p.Faction; shot = p.Shot; vel = p.Velocity;
            };
            CatchAndResolve(sim, AtStrike, 10);
            Assert.AreEqual(1, count);
            Assert.AreEqual(AttackFaction.Returned, faction);
            Assert.AreEqual(e.ActorId, shot.SourceActorId, "borrowed from the attacker");
            Assert.IsFalse(shot.Capturable);
            Assert.Greater(Vector2.Dot(vel, Vector2.left), 0f, "flies at the attacker");
        }

        [Test]
        public void ParryWorks_EvenWithBothPacketSlotsFull()
        {
            // Parry must never be blocked by packet bookkeeping (D28): it is the answer to a
            // melee-only wave, so it has to work exactly when the player is holding ammunition.
            var sim = SimWithPlayerAt(OnTheRim);
            var bolt = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), 0f);
            for (int i = 0; i < sim.Stats.PacketSlots; i++)
                sim.Packets.Create(sim.Ids.Next(), 100 + i, 0, 99f, sim.Stats.PacketCapacity).Payloads.Add(bolt);
            var e = WoundUpPursuer(sim, 0.1);
            CatchAndResolve(sim, AtStrike);
            Assert.AreEqual(0, sim.Score.DamageTaken);
            Assert.IsTrue(e.Killed);
        }

        [Test]
        public void Riposte_PiercesIntoASecondEnemyBehind()
        {
            var sim = SimWithPlayerAt(OnTheRim);
            var e = WoundUpPursuer(sim, 0.1);
            var behind = P4.Parked(sim, ActorCategory.Pursuer, new Vector2(-2.1f, 0f));
            CatchAndResolve(sim, AtStrike);
            Assert.IsTrue(e.Killed);
            Assert.IsTrue(behind.Killed, "pierce 1 carries the riposte through");
        }

        [Test]
        public void RunHasNoLanternFallback_AMeleeOnlyArenaFiresNothing()
        {
            var sim = P4.Sim();
            sim.Player.InvulnerableUntil = double.MaxValue;
            var e = sim.SpawnEnemy(ActorCategory.Pursuer, new Vector2(8f, -5f));
            e.ActiveAt = double.MaxValue; // alive, melee-only, harmless
            int shots = 0;
            sim.Events.ProjectileSpawned += _ => shots++;
            P4.Run(sim, 60 * 8);
            Assert.AreEqual(0, shots);
        }
    }

    // The band-contact test on its own (D36), with the shipped widths: band radius 1.15 width
    // 0.12, cone half-angle 45, strike radius 0.75 rim 0.09 → contact when some band point is
    // 0.6..0.81 from the strike centre.
    public class ParryGeometryTests
    {
        const float Half = 45f, Ring = 1.15f, RingW = 0.12f, Strike = 0.75f, Rim = 0.09f;

        static bool Meet(Vector2 player, Vector2 aim, Vector2 c)
            => ParryGeometry.BandsMeet(player, aim, Half, Ring, RingW, c, Strike, Rim);

        [Test]
        public void OnAxis_TheBandMeetsTheRim_OnlyInTheTwoDepthRanges()
        {
            // Along the aim the band is |d - 1.15| from C: contact iff that is in [0.6, 0.81],
            // i.e. d in [0.34, 0.55] (far rim) or [1.75, 1.96] (near rim). Off-axis band points
            // only reach further from C here, so the on-axis numbers are the lower bounds.
            Assert.IsTrue(Meet(new Vector2(0.445f, 0f), Vector2.left, Vector2.zero));
            Assert.IsTrue(Meet(new Vector2(0.36f, 0f), Vector2.left, Vector2.zero));
            Assert.IsFalse(Meet(new Vector2(0.31f, 0f), Vector2.left, Vector2.zero), "inside the old 0.2/0.15 widths, outside the new");
            Assert.IsTrue(Meet(new Vector2(1.8f, 0f), Vector2.left, Vector2.zero));
            Assert.IsFalse(Meet(new Vector2(0.05f, 0f), Vector2.left, Vector2.zero), "on the centre: band clears the rim");
            Assert.IsFalse(Meet(new Vector2(2.6f, 0f), Vector2.left, Vector2.zero), "too far out");
        }

        [Test]
        public void TheBandExistsOnlyInsideTheCone()
        {
            // Same spot as the on-rim case, but facing away or square sideways.
            var p = new Vector2(0.445f, 0f);
            Assert.IsFalse(Meet(p, Vector2.right, Vector2.zero));
            Assert.IsFalse(Meet(p, Vector2.up, Vector2.zero));
        }

        [Test]
        public void ZeroAim_NeverMeets()
        {
            Assert.IsFalse(Meet(new Vector2(0.445f, 0f), Vector2.zero, Vector2.zero));
        }

        [Test]
        public void ClosedForm_AgreesWithDenseSampling()
        {
            // Brute-force check of the interval argument: sample the band's centre arc finely
            // and compare. Cases within 1 cm of the contact boundary are skipped, since there
            // sampling itself is the less exact of the two.
            var rng = new System.Random(7);
            float rimR = Strike - Rim * 0.5f, tol = (RingW + Rim) * 0.5f;
            int compared = 0;
            for (int n = 0; n < 4000; n++)
            {
                var c = new Vector2((float)rng.NextDouble() * 6f - 3f, (float)rng.NextDouble() * 6f - 3f);
                float a = (float)rng.NextDouble() * 360f;
                var aim = Geometry2D.Rotate(Vector2.right, a);
                float best = float.MaxValue;
                for (int i = 0; i <= 900; i++)
                {
                    var q = Geometry2D.Rotate(aim, -Half + 2f * Half * i / 900f) * Ring;
                    best = Mathf.Min(best, Mathf.Abs((q - c).magnitude - rimR));
                }
                if (Mathf.Abs(best - tol) < 0.01f) continue;
                compared++;
                Assert.AreEqual(best <= tol, Meet(Vector2.zero, aim, c), $"C={c} aim={a}");
            }
            Assert.Greater(compared, 3000);
        }
    }

    // Playtest controls: summon one specific kind, toggle the sandbox director, clear the arena.
    public class SandboxControlTests
    {
        [Test]
        public void SummonEnemy_SpawnsExactlyThatKind_AwayFromThePlayer()
        {
            foreach (var kind in new[] { ActorCategory.Acolyte, ActorCategory.Pursuer, ActorCategory.ScatterCaster, ActorCategory.SiegeFamiliar })
            {
                var sim = P4.Sim();
                var e = sim.SummonEnemy(kind);
                Assert.AreEqual(1, sim.Enemies.Count);
                Assert.AreEqual(kind, e.Category);
                Assert.GreaterOrEqual((e.Position - sim.Player.Position).magnitude, sim.Config.combat.minSpawnDistance);
            }
        }

        [Test]
        public void AutoSpawnOff_DirectorStaysQuiet_ButSummonStillWorks()
        {
            var setup = RunSetup.ForSandbox(3);
            setup.SandboxAutoSpawn = true;
            var sim = new ArenaSim(TestSims.Config, setup);
            Assert.Greater(sim.Enemies.Count, 0, "auto-spawn starts with a formation");
            sim.AutoSpawn = false;
            sim.ClearArena();
            P4.Run(sim, 60 * 4);
            Assert.AreEqual(0, sim.AliveEnemyCount(), "director must not refill the arena");
            sim.SummonEnemy(ActorCategory.Pursuer);
            Assert.AreEqual(1, sim.AliveEnemyCount());
        }

        [Test]
        public void AutoSpawnOn_RefillsAnEmptyArena()
        {
            var sim = P4.Sim();
            sim.AutoSpawn = true;
            P4.Run(sim, 60 * 2);
            Assert.Greater(sim.AliveEnemyCount(), 0);
        }

        [Test]
        public void ClearArena_DespawnsWithoutKillsAndRemovesHostileShots()
        {
            var sim = P4.Sim();
            sim.SummonEnemy(ActorCategory.Acolyte);
            sim.SummonEnemy(ActorCategory.Pursuer);
            var bolt = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 77, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(bolt, AttackFaction.Hostile, new Vector2(5f, 5f), Vector2.left);
            int kills = 0;
            sim.Events.EnemyKilled += (_, __) => kills++;
            sim.ClearArena();
            sim.Tick(P4.Still, P4.Dt);
            Assert.AreEqual(0, sim.AliveEnemyCount());
            Assert.AreEqual(0, sim.Projectiles.Count);
            Assert.AreEqual(0, kills);
        }
    }
}
