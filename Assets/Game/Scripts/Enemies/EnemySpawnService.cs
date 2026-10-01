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
    /// enemy; that one exists so the lantern's starvation rule is exercised in normal play.
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
            // Melee-only on purpose: the lantern must step in (section 3).
            new Formation("Pack", P, P, P),
            new Formation("Bombardment", F, S, P),
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
