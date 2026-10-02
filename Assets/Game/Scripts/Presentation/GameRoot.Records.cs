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
            Main.Show(false);
            RecordsView.Show(Profile.Profile, CloseRecords);
        }

        void CloseRecords()
        {
            RecordsView.Hide();
            Main.Show(true);
        }

        partial void CloseLaterSubMenus()
        {
            if (RecordsView != null && RecordsView.IsOpen) CloseRecords();
            CloseModeSubMenus();
        }

        partial void CloseModeSubMenus();

        partial void FinalizeTextMore(FinalizeResult r, System.Text.StringBuilder sb)
        {
            foreach (var id in r.NewAchievements)
            {
                var a = Achievements.Find(id);
                sb.Append($"\n<color=#FAD150>Achievement: {a?.Name ?? id}</color>");
            }
            foreach (var o in r.Records)
            {
                bool time = o.Kind == Records.LongestRun;
                string label = time ? "Longest run" : "Best score";
                string Fmt(float v) => time ? $"{(int)v / 60}:{(int)v % 60:00}" : ((int)v).ToString();
                if (o.Previous == null)
                    sb.Append($"\n{label}: {Fmt(o.ThisValue)} (first record)");
                else
                {
                    float prev = time ? o.Previous.duration : o.Previous.score;
                    sb.Append(o.IsNewBest
                        ? $"\n<color=#8CF0A8>New {label.ToLowerInvariant()}: {Fmt(o.ThisValue)} (was {Fmt(prev)})</color>"
                        : $"\n{label}: {Fmt(prev)} (this run {Fmt(o.ThisValue)})");
                }
            }
        }
    }
}
