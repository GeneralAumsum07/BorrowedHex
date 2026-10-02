using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// The whole combat simulation for one run, in plain C#. One instance == one run: restart
    /// throws the instance away, which is the strongest guarantee that no packet, echo,
    /// subscription or pooled projectile leaks into the next run (section 3).
    ///
    /// Tick order (documented because several rules depend on it):
    ///   1. advance the gameplay clock (nothing happens while paused)
    ///   2. run due scheduled actions (echoes, delayed spawns)
    ///   3. update aim, drain life and selected-packet time, backfire expired packets
    ///   4. player aim, slot cycle, early release, catch activation, dash, movement
    ///   5. enemies think/move/emit (a Pursuer strike may be parried here)
    ///   6. projectiles sweep: walls → capture → actor impact, in travel order
    ///   7. upgrade auras, director
    ///   8. score timers, terminal resolution (death beats boss defeat beats time expiry),
    ///      then the short-mode schedule and encounter director (ArenaSim.Run.cs)
    /// Later phases fill steps 3 and 5–8 in other partial files.
    /// </summary>
    public sealed partial class ArenaSim
    {
        public readonly GameConfig Config;
        public readonly RunSetup Setup;
        public readonly PlayerStats Stats;
        public readonly GameplayClock Clock = new GameplayClock();
        public readonly GameplayScheduler Scheduler = new GameplayScheduler();
        public readonly IdGenerator Ids = new IdGenerator();
        public readonly SimEvents Events = new SimEvents();
        public readonly SeededRandom Random;
        public readonly List<Rect> Walls;
        public readonly PlayerActor Player;
        public readonly string RunId;

        public ArenaSim(GameConfig config, RunSetup setup)
        {
            Config = config;
            Setup = setup ?? RunSetup.ForSandbox(0);
            Stats = Setup.Stats ?? PlayerStats.FromConfig(config);
            Random = new SeededRandom(Setup.Seed);
            RunId = RunIdFactory.Create();
            Walls = config.arena.BuildObstacles();

            Player = new PlayerActor
            {
                ActorId = Ids.Next(),
                Position = config.arena.playerSpawn,
                Radius = Stats.BodyRadius,
            };
            InitCombat();
        }

        partial void InitCombat();
        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt);

        public void Tick(in PlayerCommand cmd, float dt)
        {
            if (Clock.IsPaused) return;
            BeginIfReady();
            double tickStart = Clock.Now;
            Clock.Advance(dt);
            double now = Clock.Now;
            if (now <= tickStart) return; // rejected delta (NaN, negative, zero)

            Scheduler.RunDue(now);
            TickCombat(cmd, tickStart, now, dt);
        }

        /// <summary>Phase 1 player step; combat partials call this at step 4.</summary>
        void TickPlayer(in PlayerCommand cmd, double tickStart, double now, float dt)
        {
            if (!Player.Alive) return;
            PlayerMotor.UpdateAim(Player, cmd);
            // Cycle, then release, then catch: a slot emptied by an early release this tick is
            // usable by a catch pressed on the same tick (same rule as expiry, D17).
            if (cmd.CycleSlot)
            {
                Packets.CycleSelection();
                LastSwapAt = now;
                Events.RaiseSlotSwapped(Packets.SelectedSlot);
            }
            if (cmd.Release) TryReleaseEarly();
            if (cmd.Catch) TryCatch(now);
            if (cmd.Dash) TryDash(cmd.Move, tickStart);
            PlayerMotor.Move(Player, cmd.Move, Stats, now, dt, Walls);
        }

        void TryDash(Vector2 move, double tickStart)
        {
            if (PlayerMotor.TryStartDash(Player, move, Stats, tickStart))
                Events.RaiseDash(Player.Position, Player.DashDirection);
        }

        /// <summary>
        /// The single entry point for player damage, in life-clock seconds. Invulnerability (post-hit
        /// or dash) makes later hits no-ops, and death is raised exactly once.
        /// <paramref name="invulnerability"/> overrides the post-hit window (contact hits use a
        /// shorter one); negative means the normal hit window.
        /// </summary>
        public bool DamagePlayer(int amount, int sourceActorId, float invulnerability = -1f)
            => ApplyPlayerDamage(amount, sourceActorId, invulnerability, false);

        bool ApplyPlayerDamage(int amount, int sourceActorId, float invulnerability, bool bypassInvulnerability)
        {
            double now = Clock.Now;
            if (Summary != null || !Player.Alive || lifeSeconds <= 0 || amount <= 0
                || (!bypassInvulnerability && Player.IsInvulnerable(now))) return false;
            double before = lifeSeconds;
            lifeSeconds = Math.Max(0, lifeSeconds - amount);
            Player.InvulnerableUntil = now + (invulnerability >= 0f ? invulnerability : Stats.HitInvulnerability);
            Events.RaisePlayerHit(amount, sourceActorId);
            Events.RaiseLifeClockChanged((float)(lifeSeconds - before), Player.Position);
            if (lifeSeconds == 0)
            {
                Player.Alive = false;
                Player.Dashing = false;
                Events.RaisePlayerDied();
            }
            return true;
        }
    }
}
