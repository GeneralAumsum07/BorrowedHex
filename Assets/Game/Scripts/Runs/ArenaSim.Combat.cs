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
            Lantern = new Lantern { ActorId = Ids.Next(), Position = Config.arena.lanternPosition };
            Events.PlayerDied += OnPlayerDied;
            if (Setup.SandboxAutoSpawn)
                SpawnEnemy(ActorCategory.Acolyte, FindSpawnPoint(Config.combat.acolyte.bodyRadius));
        }

        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt)
        {
            // 3. packet expiry/release — Phase 3
            TickPlayer(cmd, tickStart, now, dt);          // 4
            TickEnemies(now, dt);                         // 5
            TickProjectiles(now, dt);                     // 6
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
            ClearProjectiles();
        }
    }
}
