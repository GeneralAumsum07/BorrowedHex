using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Player;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Everything needed to start a run: mode, seed, and the loadout snapshot. The profile is
    /// read once to build this and is not touched again until finalization.
    /// </summary>
    public sealed class RunSetup
    {
        public GameMode Mode = GameMode.Short;
        public int Seed;
        /// <summary>No encounter director: an empty arena for tests and the dev sandbox.</summary>
        public bool Sandbox;
        /// <summary>Debug-assisted runs never submit profile rewards or records (Phase 11).</summary>
        public bool Debug;
        public string StyleId = "snatcher";
        public List<string> PassiveIds = new List<string>();
        public int MasteryLevel = 1;
        public string BuildVersion = "dev";
        /// <summary>Null → derive from config (no passives, Snatcher).</summary>
        public PlayerStats Stats;

        /// <summary>
        /// Sandbox only: start with the formation director on (ArenaSim.AutoSpawn). Off by default so unit tests get an
        /// empty arena and control every actor themselves.
        /// </summary>
        public bool SandboxAutoSpawn;

        /// <summary>
        /// Phase 14: the scripted tutorial (D86). Always a sandbox too, so it inherits "never
        /// saved, no encounter director"; on top of that the life clock is frozen.
        /// </summary>
        public bool Tutorial;
        /// <summary>Opt-in larger stage layouts. Tutorial and old headless fixtures keep their authored arena.</summary>
        public bool WorldArenas;

        public static RunSetup ForSandbox(int seed) => new RunSetup { Seed = seed, Sandbox = true };
        public static RunSetup ForTutorial(int seed) => new RunSetup { Seed = seed, Sandbox = true, Tutorial = true };
    }
}
