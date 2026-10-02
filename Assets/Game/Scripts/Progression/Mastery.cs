using BorrowedHex.Runs;

namespace BorrowedHex.Progression
{
    /// <summary>What one run earned, term by term, so the results screen can show the sum honestly.</summary>
    public sealed class XpBreakdown
    {
        public int NormalKills, OverstayedKills, BossKills, Encounters, PerfectHits;
        public int Total;
    }

    /// <summary>
    /// Section 7 mastery rules. Fixed in code, not in the config asset: a saved profile's
    /// level, XP and points are validated against them, so they must not drift with tuning.
    /// </summary>
    public static partial class Mastery
    {
        public const int XpPerNormalKill = 2;
        public const int XpPerOverstayedKill = 5;
        public const int XpPerBossKill = 35;
        public const int XpPerEncounter = 5;
        public const int PerfectHitXpCap = 20;

        /// <summary>XP needed to advance FROM <paramref name="level"/> to the next: 100 + 50 (L - 1).</summary>
        public static int CostToAdvance(int level) => 100 + 50 * (level - 1);

        /// <summary>The section 7 formula on raw counts (the summary overload feeds it).</summary>
        public static XpBreakdown RunXp(int normalKills, int overstayedKills, int bossKills, int encounters, int perfectHits)
        {
            var b = new XpBreakdown
            {
                NormalKills = normalKills,
                OverstayedKills = overstayedKills,
                BossKills = bossKills,
                Encounters = encounters,
                // The cap is on the XP term, which is 1 XP per perfect hit, so capping the count is the same thing.
                PerfectHits = System.Math.Min(PerfectHitXpCap, perfectHits),
            };
            b.Total = XpPerNormalKill * normalKills + XpPerOverstayedKill * overstayedKills + XpPerBossKill * bossKills
                      + XpPerEncounter * encounters + b.PerfectHits;
            return b;
        }

        /// <summary>
        /// Every ending earns XP (section 7), losses included. "Normal" kills are the ones that
        /// are neither the boss nor an overstayed enemy, so no kill is counted under two terms.
        /// </summary>
        public static XpBreakdown RunXp(RunSummary s)
        {
            int normal = System.Math.Max(0, s.Kills - s.BossesDefeated - s.OverstayedKills);
            return RunXp(normal, s.OverstayedKills, s.BossesDefeated, s.EncountersCompleted, s.PerfectHits);
        }

        /// <summary>
        /// Add XP; excess carries into the next level; one point per level gained. At the cap the
        /// running XP stays at 0 (the bar reads full) while totalXp keeps counting, and no new
        /// points are given (section 7: statistics keep counting without new points).
        /// Returns the number of levels gained.
        /// </summary>
        public static int Grant(MasteryState m, int xp)
        {
            if (xp <= 0) return 0;
            m.totalXp += xp;
            if (m.level >= MaxLevel) { m.xp = 0; return 0; }
            int gained = 0;
            m.xp += xp;
            while (m.level < MaxLevel && m.xp >= CostToAdvance(m.level))
            {
                m.xp -= CostToAdvance(m.level);
                m.level++;
                m.points++;
                gained++;
            }
            if (m.level >= MaxLevel) m.xp = 0;
            return gained;
        }
    }
}
