using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Phase 8: the player profile, the main menu and settings. Kept apart from the frame loop
    /// in GameRoot.cs: this half decides WHICH run starts and what happens when one ends.
    /// </summary>
    public sealed partial class GameRoot
    {
        /// <summary>
        /// Tests set this before creating a GameRoot so they never touch the real save
        /// (section 8). Null in the game: file storage on desktop, PlayerPrefs on Web.
        /// </summary>
        public static IProfileStorage StorageOverride;

        public ProfileService Profile { get; private set; }
        public MainMenu Main { get; private set; }
        public SettingsPanel Settings { get; private set; }
        public bool InMainMenu => kind == RunKind.Backdrop;
        /// <summary>The last finalization (null until a counted run ends), for the results screen.</summary>
        public FinalizeResult LastFinalize { get; private set; }

        void BuildMenus()
        {
            var storage = StorageOverride ?? (IsWeb
                ? new PlayerPrefsProfileStorage()
                : (IProfileStorage)new FileProfileStorage(Application.persistentDataPath));
            Profile = ProfileService.Load(storage);

            Main = MainMenu.Create(canvas);
            Main.AddEntry("play_short", "Play", PlayShort);
            // Visibly disabled until their phase lands (Phase 8 checklist).
            Main.AddEntry("endless", "Endless", null, "later build");
            Main.AddEntry("practice", "Practice sandbox", PlaySandbox);
            Main.AddEntry("mastery", "Mastery & skills", null, "later build");
            Main.AddEntry("style", "Capture style", null, "later build");
            Main.AddEntry("records", "Achievements & records", null, "later build");
            Main.AddEntry("settings", "Settings", OpenSettingsFromMain);
            if (!IsWeb) Main.AddEntry("quit", "Quit", Application.Quit);
            Settings = SettingsPanel.Create(canvas, allowWindowed: !IsWeb);
            BuildProgressionMenus();
            ApplySettings();
        }

        // Later phases (tree, styles, records, endless) switch on their menu entries here.
        partial void BuildProgressionMenus();
        // ...and close their own panels here (Esc on the main menu, or returning to it).
        partial void CloseSubMenus();
        // Later phases fill the run's style, passives and stats from the profile here.
        partial void ApplyLoadoutExtra(RunSetup setup);

        /// <summary>The profile is read once to build a run (RunSetup's contract) and not again until finalization.</summary>
        void ApplyLoadout(RunSetup setup)
        {
            setup.MasteryLevel = Profile.Profile.mastery.level;
            setup.BuildVersion = Application.version;
            ApplyLoadoutExtra(setup);
        }

        public GameConfig Config => config;

        public void PlayShort() => StartRun(RunKind.Short);
        public void PlaySandbox() => StartRun(RunKind.Sandbox);

        void StartRun(RunKind k)
        {
            kind = k;
            Main.Show(false);
            Settings.Hide();
            BeginRun();
        }

        /// <summary>
        /// Back to the launch screen. Leaving mid-run ABANDONS it: nothing is finalized and no
        /// XP is given (D75); only the endings section 7 lists award progression.
        /// </summary>
        public void ShowMainMenu()
        {
            Menu.Show(false);
            Settings.Hide();
            CloseSubMenus();
            kind = RunKind.Backdrop;
            BeginRun();
            RefreshMainMenu();
            Main.Show(true);
            // Above the HUD, flow panels and pause menu, below settings.
            Main.transform.SetAsLastSibling();
            Settings.transform.SetAsLastSibling();
            SyncGameplayInput();
        }

        void RefreshMainMenu()
        {
            Main.SetProfileLine(ProfileLine());
            Main.SetWarning(Profile.Warning);
        }

        // Replaced by a mastery line in Phase 9.
        string ProfileLine()
        {
            var st = Profile.Profile.stats;
            string extra = null;
            ProfileLineExtra(ref extra);
            return extra ?? (st.runs == 0 ? "" : $"Runs {st.runs}   Victories {st.victories}");
        }
        partial void ProfileLineExtra(ref string line);

        void OnRunEnded(RunSummary summary)
        {
            // The sim raised this for ITS run; Sim may already have been replaced if a restart
            // landed in the same frame, so finalize with the summary's own setup.
            var setup = summary.RunId == Sim.RunId ? Sim.Setup : null;
            LastFinalize = Profile.FinalizeRun(summary, setup);
            Flow.SetProgress(FinalizeText(LastFinalize));
        }

        /// <summary>The results screen's profile lines. Later phases add XP, achievements and records.</summary>
        string FinalizeText(FinalizeResult r)
        {
            if (r == null || !r.Applied) return null;
            var sb = new System.Text.StringBuilder();
            FinalizeTextExtra(r, sb);
            if (!r.Saved) sb.Append(sb.Length > 0 ? "\n" : "").Append("<color=#FF8C73>Not saved: ").Append(Profile.Warning).Append("</color>");
            return sb.ToString();
        }
        partial void FinalizeTextExtra(FinalizeResult r, System.Text.StringBuilder sb);

        void OpenSettingsFromMain() => Settings.Show(Profile.Profile.settings, SettingsChanged, CloseSettings);
        void OpenSettingsFromPause() => Settings.Show(Profile.Profile.settings, SettingsChanged, CloseSettings);

        void CloseSettings()
        {
            Settings.Hide();
            // Return focus to whichever menu opened it.
            if (InMainMenu) Main.Show(true);
            else if (Menu.IsOpen) Menu.Show(true);
        }

        void SettingsChanged()
        {
            ApplySettings();
            Profile.Save();
            if (InMainMenu) RefreshMainMenu();   // a failed save shows its warning at once
        }

        void ApplySettings()
        {
            var s = Profile.Profile.settings;
            // Interface scale: the canvas is laid out against 1080p, so a smaller reference
            // resolution makes every element proportionally larger.
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Clamp(s.uiScale, ProfileSettings.MinUiScale, ProfileSettings.MaxUiScale);
            DisplayOptions.ReduceFlashes = s.reduceFlashes;
            DisplayOptions.ShowHints = s.showHints;
            // -1 leaves the display alone, so a fresh profile never fights the launch settings.
            if (s.displayMode == 1 && !Screen.fullScreen)
            {
                if (IsWeb) Screen.fullScreen = true;
                else Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            }
            else if (s.displayMode == 0 && Screen.fullScreen && !IsWeb)
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }
    }
}
