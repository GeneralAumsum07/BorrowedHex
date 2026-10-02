using System.Collections.Generic;
using BorrowedHex.Data;

namespace BorrowedHex.Progression
{
    public enum SkillBranch { Precision, Mobility, Resilience }

    /// <summary>One section 7 node. Its effect lives in <see cref="Loadout"/>; this is identity and gating only.</summary>
    public sealed class SkillNode
    {
        public readonly string Id;
        public readonly string Name;
        public readonly SkillBranch Branch;
        public readonly int Tier;

        public SkillNode(string id, string name, SkillBranch branch, int tier)
        {
            Id = id;
            Name = name;
            Branch = branch;
            Tier = tier;
        }
    }

    /// <summary>
    /// The nine-node tree and every rule for buying, equipping and respeccing (section 7).
    /// Domain logic only: the menu calls these and shows the reason string on a refusal, so the
    /// UI can never allow something the profile validator would later reject.
    /// </summary>
    public static class SkillTree
    {
        public const string PrecisionAngle = "precision_angle";
        public const string PrecisionCapacity = "precision_capacity";
        public const string QuickDraw = "quick_draw";
        public const string MobilitySpeed = "mobility_speed";
        public const string MobilityDashRecovery = "mobility_dash_recovery";
        public const string MobilityDashDistance = "mobility_dash_distance";
        public const string ResilienceGrace = "resilience_grace";
        public const string ResilienceTime = "resilience_time";
        public const string ResilienceDashGrace = "resilience_dash_grace";

        /// <summary>Mastery level each tier needs (index = tier).</summary>
        static readonly int[] TierLevel = { 0, 2, 4, 7 };

        public static readonly IReadOnlyList<SkillNode> Nodes = new[]
        {
            new SkillNode(PrecisionAngle, "Wide Grasp", SkillBranch.Precision, 1),
            new SkillNode(PrecisionCapacity, "Deep Pockets", SkillBranch.Precision, 2),
            new SkillNode(QuickDraw, "Quick Draw", SkillBranch.Precision, 3),
            new SkillNode(MobilitySpeed, "Light Feet", SkillBranch.Mobility, 1),
            new SkillNode(MobilityDashRecovery, "Quick Recovery", SkillBranch.Mobility, 2),
            new SkillNode(MobilityDashDistance, "Long Stride", SkillBranch.Mobility, 3),
            new SkillNode(ResilienceGrace, "Steady Nerves", SkillBranch.Resilience, 1),
            new SkillNode(ResilienceTime, "Borrowed Hours", SkillBranch.Resilience, 2),
            new SkillNode(ResilienceDashGrace, "Slippery", SkillBranch.Resilience, 3),
        };

        public static SkillNode Find(string id)
        {
            foreach (var n in Nodes) if (n.Id == id) return n;
            return null;
        }

        public static int LevelForTier(int tier) => TierLevel[tier];

        /// <summary>The same branch's node one tier down, or null for tier one.</summary>
        public static SkillNode Prerequisite(SkillNode n)
        {
            if (n.Tier <= 1) return null;
            foreach (var o in Nodes) if (o.Branch == n.Branch && o.Tier == n.Tier - 1) return o;
            return null;
        }

        /// <summary>The player-facing effect, built from live tuning so the text cannot drift from the rule.</summary>
        public static string Describe(SkillNode n, ProgressionTuning t)
        {
            switch (n.Id)
            {
                case PrecisionAngle: return $"+{t.precisionAngle:0.#}° catch cone";
                case PrecisionCapacity: return $"+{t.precisionCapacity} energy per packet";
                case QuickDraw: return $"Fire within {t.quickDrawWindow:0.0#} s of a swap: +{t.quickDrawBonus * 100f:0}% damage";
                case MobilitySpeed: return $"+{t.mobilitySpeed * 100f:0.#}% movement speed";
                case MobilityDashRecovery: return $"-{t.mobilityDashRecovery:0.0#} s dash cooldown";
                case MobilityDashDistance: return $"+{t.mobilityDashDistance:0.0#} dash distance";
                case ResilienceGrace: return $"+{t.resilienceGrace:0.0#} s invulnerability after a hit";
                case ResilienceTime: return $"+{t.resilienceTime:0} s starting life and cap";
                case ResilienceDashGrace: return $"+{t.resilienceDashGrace:0.0#} s dash invulnerability";
                default: return "";
            }
        }

        /// <summary>Why <paramref name="id"/> cannot be bought right now, or null if it can.</summary>
        public static string WhyCannotBuy(PlayerProfile p, string id)
        {
            var n = Find(id);
            if (n == null) return "unknown node";
            if (p.ownedNodes.Contains(id)) return "already owned";
            if (p.mastery.level < TierLevel[n.Tier]) return $"needs mastery {TierLevel[n.Tier]}";
            var pre = Prerequisite(n);
            // Owned, not equipped, is enough (section 7).
            if (pre != null && !p.ownedNodes.Contains(pre.Id)) return $"needs {pre.Name}";
            if (p.mastery.points < 1) return "no points";
            return null;
        }

        public static bool TryBuy(PlayerProfile p, string id, out string why)
        {
            why = WhyCannotBuy(p, id);
            if (why != null) return false;
            p.mastery.points--;
            p.ownedNodes.Add(id);
            return true;
        }

        public static string WhyCannotEquip(PlayerProfile p, string id)
        {
            if (Find(id) == null) return "unknown node";
            if (!p.ownedNodes.Contains(id)) return "not owned";
            if (p.equippedNodes.Contains(id)) return "already equipped";
            if (p.equippedNodes.Count >= Mastery.MaxEquipped) return $"{Mastery.MaxEquipped} already equipped";
            return null;
        }

        public static bool TryEquip(PlayerProfile p, string id, out string why)
        {
            why = WhyCannotEquip(p, id);
            if (why != null) return false;
            p.equippedNodes.Add(id);
            return true;
        }

        public static bool Unequip(PlayerProfile p, string id) => p.equippedNodes.Remove(id);

        /// <summary>Free respec: every owned node is refunded and unequipped. Returns the points refunded.</summary>
        public static int Respec(PlayerProfile p)
        {
            int refund = p.ownedNodes.Count;
            p.mastery.points += refund;
            p.ownedNodes.Clear();
            p.equippedNodes.Clear();
            return refund;
        }

        /// <summary>
        /// Profile validation for the tree (wired into ProfileService.Validate): every node is
        /// known, and every owned node's level gate and prerequisite hold. A file that breaks
        /// these was edited or damaged; the strict rule (D73) rejects it.
        /// </summary>
        public static string Validate(PlayerProfile p)
        {
            foreach (var id in p.ownedNodes)
            {
                var n = Find(id);
                if (n == null) return $"unknown node {id}";
                if (p.mastery.level < TierLevel[n.Tier]) return $"{id} above mastery";
                var pre = Prerequisite(n);
                if (pre != null && !p.ownedNodes.Contains(pre.Id)) return $"{id} without {pre.Id}";
            }
            if (new HashSet<string>(p.equippedNodes).Count != p.equippedNodes.Count) return "duplicate equipped";
            return null;
        }
    }
}
