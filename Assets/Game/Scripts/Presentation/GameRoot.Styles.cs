using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>Phase 11: the capture style panel and applying the selected style to a run.</summary>
    public sealed partial class GameRoot
    {
        public StylePanel Styles { get; private set; }

        partial void BuildModeMenus()
        {
            Styles = StylePanel.Create(canvas);
            Main.EnableEntry("style", OpenStyles);
            BuildEndlessMenus();
        }

        // Phase 12 (endless).
        partial void BuildEndlessMenus();

        void OpenStyles()
        {
            Main.Show(false);
            Styles.Show(Profile.Profile, Config, () => Profile.Save(), CloseStyles);
        }

        void CloseStyles()
        {
            Styles.Hide();
            RefreshMainMenu();
            Main.Show(true);
        }

        partial void CloseModeSubMenus()
        {
            if (Styles != null && Styles.IsOpen) CloseStyles();
            CloseEndlessSubMenus();
        }

        partial void CloseEndlessSubMenus();

        /// <summary>
        /// Section 7: the style sets the base catch, then the equipped passives apply on top, in one
        /// Loadout call (D83). An unknown saved id resolves to Snatcher here as well as in
        /// validation, so a run never starts with a style the sim does not know.
        /// </summary>
        partial void ApplyStyleExtra(RunSetup setup)
        {
            setup.StyleId = CaptureStyles.Resolve(Profile.Profile.styleId).Id;
            setup.Stats = Loadout.Resolve(Config, setup.PassiveIds, setup.StyleId);
        }
    }
}
