using BorrowedHex.Progression;
using BorrowedHex.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>Phase 10: the achievements and records panel, and their lines on the results screen.</summary>
    public sealed partial class GameRoot
    {
        public RecordsPanel RecordsView { get; private set; }

        partial void BuildLaterMenus()
        {
            RecordsView = RecordsPanel.Create(canvas);
            Main.EnableEntry("records", OpenRecords);
            BuildModeMenus();
        }

        // Phases 11-12 (styles, endless).
        partial void BuildModeMenus();

        void OpenRecords()
        {
            RecordsView.Show(Profile.Profile, CloseRecords);
            Screens.Push(RecordsView.gameObject, () => RecordsView.DefaultFocus, CloseRecords);
        }

        void CloseRecords()
        {
            if (Screens.Top == RecordsView.gameObject) Screens.Pop();
        }
        // Task 16: the achievement and record lines moved to ResultsCopy.FromFinalize.
    }
}
