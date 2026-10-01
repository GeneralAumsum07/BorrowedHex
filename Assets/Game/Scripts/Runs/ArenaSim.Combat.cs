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
            InitCapture();
            Lantern = new Lantern { ActorId = Ids.Next(), Position = Config.arena.lanternPosition };
            Events.PlayerDied += OnPlayerDied;
            if (Setup.SandboxAutoSpawn)
                SpawnNextSandboxFormation();
        }

        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt)
        {
            // Aim first (no side effects) so a release this tick uses the freshest valid aim.
            if (Player.Alive) PlayerMotor.UpdateAim(Player, cmd);
            ReleaseExpiredPackets(now);                   // 3
            TickPlayer(cmd, tickStart, now, dt);          // 4 (catch, dash, move)
            TickEnemies(now, dt);                         // 5
            TickProjectiles(now, dt);                     // 6
            Capture.Tick(now);                            //   close the window's packet
            TickLantern(now);                             //   ammunition starvation (section 3)
            TickSandboxDirector(now);                     // 7 (director)
            RemoveDeadEnemies();
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
