using BorrowedHex.Player;

namespace BorrowedHex.Runs
{
    // Combat tick pipeline. Steps are filled in phase by phase; the order is documented on
    // ArenaSim and must not be rearranged without updating the tests that pin it.
    public sealed partial class ArenaSim
    {
        partial void InitCombat()
        {
        }

        partial void TickCombat(in PlayerCommand cmd, double tickStart, double now, float dt)
        {
            TickPlayer(cmd, tickStart, now, dt);
        }
    }
}
