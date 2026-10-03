using BorrowedHex.Data;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// The seven encounter upgrades of section 5. Order here is the offer pool order; the seeded
    /// draw picks distinct entries from it, so adding one changes which offers a seed produces.
    /// </summary>
    public enum UpgradeId
    {
        PiercingReturn,
        EchoVolley,
        HeavyOrbit,
        PartingGift,
        FinalSecond,
        Overflow,
        Fusion,
    }

    /// <summary>One upgrade offered at a choice, with the rank it would be held at.</summary>
    public readonly struct UpgradeOffer
    {
        public readonly UpgradeId Id;
        public readonly int Rank;
        public UpgradeOffer(UpgradeId id, int rank) { Id = id; Rank = rank; }
        public override string ToString() => $"{Id} r{Rank}";
    }

    /// <summary>
    /// Player-facing names and EXACT effect text (Phase 7: "the choice panel shows exact effects").
    /// The text is generated from the same tuning the sim reads, so a retune cannot leave the
    /// card describing a number the game no longer uses.
    /// </summary>
    public static class UpgradeInfo
    {
        public static readonly UpgradeId[] Pool =
        {
            UpgradeId.PiercingReturn, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit, UpgradeId.PartingGift,
            UpgradeId.FinalSecond, UpgradeId.Overflow, UpgradeId.Fusion,
        };

        /// <summary>Stable save/achievement IDs from section 5.</summary>
        public static string Key(UpgradeId id) => id switch
        {
            UpgradeId.PiercingReturn => "piercing_return",
            UpgradeId.EchoVolley => "echo_volley",
            UpgradeId.HeavyOrbit => "heavy_orbit",
            UpgradeId.PartingGift => "parting_gift",
            UpgradeId.FinalSecond => "final_second",
            UpgradeId.Overflow => "overflow",
            UpgradeId.Fusion => "fusion",
            _ => id.ToString(),
        };

        public static string Name(UpgradeId id) => id switch
        {
            UpgradeId.PiercingReturn => "Piercing Return",
            UpgradeId.EchoVolley => "Echo Volley",
            UpgradeId.HeavyOrbit => "Heavy Orbit",
            UpgradeId.PartingGift => "Parting Gift",
            UpgradeId.FinalSecond => "Final Second",
            UpgradeId.Overflow => "Overflow",
            UpgradeId.Fusion => "Fusion",
            _ => id.ToString(),
        };

        public static string Describe(UpgradeId id, int rank, UpgradeTuning t)
        {
            switch (id)
            {
                case UpgradeId.PiercingReturn:
                    int n = t.piercePerRank * rank;
                    return $"Returned shots (not rockets) pass through {n} extra enem{(n == 1 ? "y" : "ies")}.";
                case UpgradeId.EchoVolley:
                    return $"Every release fires again after {t.echoDelay:0.##} s at {Pct(UpgradeTuning.ByRank(t.echoFraction, rank))} damage. Echoes never echo.";
                case UpgradeId.HeavyOrbit:
                    return $"While you hold a packet, enemies within {UpgradeTuning.ByRank(t.orbitRadius, rank):0.##} take {t.orbitDamage:0.#} damage every {t.orbitInterval:0.##} s.";
                case UpgradeId.PartingGift:
                    return $"Every release also bursts around you: {UpgradeTuning.ByRank(t.partingGiftDamage, rank):0.#} damage, radius {UpgradeTuning.ByRank(t.partingGiftRadius, rank):0.##}.";
                case UpgradeId.FinalSecond:
                    return $"Perfect catches (caught just before impact) deal +{Pct(UpgradeTuning.ByRank(t.finalSecondBonus, rank))} more damage.";
                case UpgradeId.Overflow:
                    return "Catching with your hand full fires the hex you hold at once and catches the new shot in its place.";
                case UpgradeId.Fusion:
                    return $"Catching with both slots full merges the catch and your other packet into the selected one (+{Pct(t.fusionPowerScale - 1f)} power). The other slot stays locked until it fires.";
            }
            return "";
        }

        static string Pct(float f) => $"{UnityEngine.Mathf.RoundToInt(f * 100f)}%";
    }
}
