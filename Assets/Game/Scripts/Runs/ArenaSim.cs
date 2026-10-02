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
    ///   3. expire packets and release them   ← before captures, so a freed slot is usable
    ///   4. player aim, catch activation, dash, movement
    ///   5. enemies think/move/emit (a Pursuer strike may be parried here)
    ///   6. projectiles sweep: walls → capture → actor impact, in travel order
    ///   7. upgrade auras, director
    ///   8. terminal resolution (death beats boss defeat beats time expiry)
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
                MaxHealth = Stats.MaxHealth,
                Health = Stats.MaxHealth,
            };
            InitCombat();
        }

        partial void InitCombat();
        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt);

        public void Tick(in PlayerCommand cmd, float dt)
        {
            if (Clock.IsPaused) return;
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
        /// The single entry point for player damage. Invulnerability (post-hit or dash) makes
        /// later hits no-ops, and death is raised exactly once.
        /// </summary>
        public bool DamagePlayer(int amount, int sourceActorId)
        {
            double now = Clock.Now;
            if (!Player.Alive || amount <= 0 || Player.IsInvulnerable(now)) return false;
            Player.Health = Math.Max(0, Player.Health - amount);
            Player.InvulnerableUntil = now + Stats.HitInvulnerability;
            Events.RaisePlayerHit(amount, sourceActorId);
            if (Player.Health == 0)
            {
                Player.Alive = false;
                Player.Dashing = false;
                Events.RaisePlayerDied();
            }
            return true;
        }
    }
}
