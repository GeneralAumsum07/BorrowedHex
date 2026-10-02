using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Player;

namespace BorrowedHex.Runs
{
    // Combat tick pipeline. Steps are filled in phase by phase; the order is documented on
    // ArenaSim and must not be rearranged without updating the tests that pin it.
    public sealed partial class ArenaSim
    {
        /// <summary>Attack definitions frozen at run start (config edits never reach a live run).</summary>
        public AttackCatalog Attacks { get; private set; }

        partial void InitCombat()
        {
            Attacks = new AttackCatalog(Config.combat);
            InitArenaDecay();
            InitCapture();
            InitRun();
            Events.PlayerDied += OnPlayerDied;
            AutoSpawn = Setup.SandboxAutoSpawn;
            if (AutoSpawn)
                SpawnNextSandboxFormation();
        }

        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt)
        {
            // Aim first (no side effects) so a release this tick uses the freshest valid aim.
            if (Player.Alive) PlayerMotor.UpdateAim(Player, cmd);
            TickLifeClock(dt);
            TickWaveClock(dt);                            //   endless wave countdown (Combat only)
            BackfireExpiredPackets(now);                  // 3
            if (!Player.Alive) { TickRunFlow(now); return; }
            TickArenaDecay(now);
            TickPlayer(cmd, tickStart, now, dt);          // 4 (catch, dash, move)
            TickEnemies(now, dt);                         // 5 (melee strikes resolve / are parried)
            TickProjectiles(now, dt);                     // 6
            Capture.Tick(now);                            //   close the window's packet
            TickOrbit(now);                               // 7 (Heavy Orbit aura)
            TickSandboxDirector(now);                     // 7 (sandbox director)
            RemoveDeadEnemies();
            TickRunFlow(now);                             // 8 (terminal, schedule, short-mode director)
        }

        /// <summary>
        /// Section 3: death cancels every pending action. Nothing scheduled may fire into the
        /// results screen, and live hostile shots stop so the death frame stays readable.
        /// </summary>
        void OnPlayerDied()
        {
            Scheduler.CancelAll();
            CancelCapture();
            ClearProjectiles();
        }
    }
}
