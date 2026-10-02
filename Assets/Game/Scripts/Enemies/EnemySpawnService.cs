using BorrowedHex.Core;
using BorrowedHex.Runs;

namespace BorrowedHex.Enemies
{
    /// <summary>A small authored group of enemies that arrive together.</summary>
    public sealed class Formation
    {
        public readonly string Name;
        public readonly ActorCategory[] Members;

        public Formation(string name, params ActorCategory[] members)
        {
            Name = name;
            Members = members;
        }

        public bool HasRanged
        {
            get
            {
                foreach (var m in Members) if (ArenaSim.IsRanged(m)) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Phase 4: "author small mixed formations instead of adding more enemy types". Each group
    /// pairs a pressure source with an ammunition source, so the answer to the threat is
    /// always on screen. Every formation except the deliberate melee-only one carries a ranged
    /// enemy; that one exists so the parry (D26), the answer to a melee-only wave, gets
    /// exercised in normal play.
    /// The Phase 5 encounter director draws from this list.
    /// </summary>
    public static class EnemySpawnService
    {
        const ActorCategory A = ActorCategory.Acolyte;
        const ActorCategory P = ActorCategory.Pursuer;
        const ActorCategory S = ActorCategory.ScatterCaster;
        const ActorCategory F = ActorCategory.SiegeFamiliar;

        public static readonly Formation[] Formations =
        {
            new Formation("Acolyte pair", A, A),
            // Pursuers push you around while the acolyte feeds you the bolts to answer them.
            new Formation("Hounds and handler", P, P, A),
            // A fan from a moving angle: tests interception, gives fuller packets.
            new Formation("Scatter screen", S, P),
            // Rocket ammunition with bodies to spend it on.
            new Formation("Siege escort", F, P, P),
            new Formation("Crossfire", S, A),
            // Melee-only on purpose: no bolts to catch, so the player must parry (D26).
            new Formation("Pack", P, P, P),
            new Formation("Bombardment", F, S, P),
        };

        // Short-mode rosters (section 6). Index 0 of each pool is the encounter's SIGNATURE,
        // spawned first so the new enemy kind is on screen the moment the encounter starts:
        //   1: acolytes and a few pursuers
        //   2: adds scatter casters
        //   3: adds siege familiars and mixed formations
        // A new two-member group, "Picket", keeps encounter one from leaning on pairs only.
        static readonly Formation Picket = new Formation("Picket", A, P);

        public static readonly Formation[][] EncounterPools =
        {
            new[] { Formations[1], Formations[0], Picket },
            new[] { Formations[2], Formations[4], Formations[1], Formations[0] },
            new[] { Formations[3], Formations[6], Formations[5], Formations[4], Formations[1] },
        };

        public static void Spawn(ArenaSim sim, Formation f)
        {
            // Spawn points are drawn one at a time so each member avoids the ones already
            // placed (FindSpawnPoint checks live enemies) and keeps the minimum player distance.
            foreach (var m in f.Members)
                sim.SpawnEnemy(m, sim.FindSpawnPoint(sim.Config.combat.For(m).bodyRadius));
        }
    }
}
