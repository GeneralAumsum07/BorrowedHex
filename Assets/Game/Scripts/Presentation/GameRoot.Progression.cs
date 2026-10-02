using System.Collections.Generic;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>Phase 9: mastery on the menu and results, the tree panel, and the run loadout.</summary>
    public sealed partial class GameRoot
    {
        public SkillTreePanel Tree { get; private set; }

        partial void BuildProgressionMenus()
        {
            Tree = SkillTreePanel.Create(canvas);
            Main.EnableEntry("mastery", OpenTree);
            BuildLaterMenus();
        }

        // Phases 10-12 switch on their own entries here.
        partial void BuildLaterMenus();

        void OpenTree()
        {
            Main.Show(false);
            Tree.Show(Profile.Profile, Config.progression, () => Profile.Save(), CloseTree);
        }

        partial void CloseSubMenus()
        {
            if (Tree != null && Tree.IsOpen) CloseTree();
            CloseLaterSubMenus();
        }

        partial void CloseLaterSubMenus();

        void CloseTree()
        {
            Tree.Hide();
            RefreshMainMenu();
            Main.Show(true);
        }

        /// <summary>
        /// Section 7: style plus equipped passives are resolved ONCE here, into the setup the sim
        /// copies at construction. The tree can change between runs without touching a live one.
        /// </summary>
        partial void ApplyLoadoutExtra(RunSetup setup)
        {
            setup.PassiveIds = new List<string>(Profile.Profile.equippedNodes);
            setup.Stats = Loadout.Resolve(Config, setup.PassiveIds);
            ApplyStyleExtra(setup);
        }

        // Phase 11 applies the capture style on top of the passives here.
        partial void ApplyStyleExtra(RunSetup setup);

        partial void ProfileLineExtra(ref string line)
        {
            var m = Profile.Profile.mastery;
            string xp = m.level >= Mastery.MaxLevel ? "max level" : $"{m.xp}/{Mastery.CostToAdvance(m.level)} XP";
            string pts = m.points > 0 ? $"   ·   {m.points} unspent point{(m.points == 1 ? "" : "s")}" : "";
            // The style is shown here too, so the choice is visible right next to Play.
            line = $"Mastery {m.level}   ·   {xp}{pts}   ·   {CaptureStyles.Resolve(Profile.Profile.styleId).Name}";
        }

        partial void FinalizeTextExtra(FinalizeResult r, System.Text.StringBuilder sb)
        {
            if (r.Xp == null) return;
            var x = r.Xp;
            // Only the non-zero terms, so a short loss reads "+4 XP (kills 4)" rather than a wall of zeros.
            var parts = new List<string>();
            if (x.NormalKills > 0) parts.Add($"kills {x.NormalKills * Mastery.XpPerNormalKill}");
            if (x.OverstayedKills > 0) parts.Add($"overstayed {x.OverstayedKills * Mastery.XpPerOverstayedKill}");
            if (x.BossKills > 0) parts.Add($"boss {x.BossKills * Mastery.XpPerBossKill}");
            if (x.Encounters > 0) parts.Add($"encounters {x.Encounters * Mastery.XpPerEncounter}");
            if (x.PerfectHits > 0) parts.Add($"perfect {x.PerfectHits}");
            sb.Append($"+{x.Total} XP");
            if (parts.Count > 0) sb.Append("  (").Append(string.Join(", ", parts)).Append(')');
            var m = Profile.Profile.mastery;
            if (r.LevelsGained > 0)
                sb.Append($"\n<color=#FAD150>Mastery {r.LevelBefore} → {r.LevelAfter}: +{r.LevelsGained} point{(r.LevelsGained == 1 ? "" : "s")}</color>");
            else if (m.level < Mastery.MaxLevel)
                sb.Append($"\nMastery {m.level}: {m.xp}/{Mastery.CostToAdvance(m.level)} XP");
            FinalizeTextMore(r, sb);
        }

        // Phase 10 adds achievements and records lines.
        partial void FinalizeTextMore(FinalizeResult r, System.Text.StringBuilder sb);
    }
}
