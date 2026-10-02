using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Tests
{
    static class ClockFixtures
    {
        // Exercise the real damage entry point rather than installing test-only setters on
        // the simulation. Ceil leaves at most one second even after fractional ticking.
        public static void LeaveOneSecond(ArenaSim sim)
        {
            sim.Player.ClearInvulnerability();
            sim.DamagePlayer(Mathf.CeilToInt(sim.LifeSeconds) - 1, 0);
            sim.Player.ClearInvulnerability();
        }
    }
}
