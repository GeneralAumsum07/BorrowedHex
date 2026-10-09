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
            // Phase 14: right under Play, where a first-time player looks first (D86).
            Main.AddEntry("tutorial", "Tutorial", PlayTutorial);
            // GameRoot.Endless switches this on; until then its tab is greyed beside Short run.
            Main.AddEntry("endless", "Endless", null);
            // Visibly disabled until their phase lands (Phase 8 checklist).
            Main.AddEntry("practice", "Practice sandbox", PlaySandbox);
            Main.AddEntry("mastery", "Mastery & skills", null, "later build");
            Main.AddEntry("style", "Capture style", null, "later build");
            Main.AddEntry("records", "Achievements & records", null, "later build");
            Main.AddEntry("settings", "Settings", OpenSettingsFromMain);
            // Owner request (3 Oct 2026); GameRoot.Cheats.cs. Above Quit so Quit stays last.
            Main.AddEntry("cheats", "Cheats", OpenCheats);
            if (!IsWeb) Main.AddEntry("quit", "Quit", Application.Quit);
            Settings = SettingsPanel.Create(canvas, allowWindowed: !IsWeb);
            CheatPanel = CheatsPanel.Create(canvas);
            BuildProgressionMenus();
            // Training is a sub-screen like any other: the stack hides the menu under it, and Esc
            // or Back returns with focus restored. Starting a run from it clears the whole stack.
            Main.OpenTraining += () => Screens.Push(Main.Training.gameObject, () => Main.Training.DefaultFocus, null);
            Main.Training.Back += () => { if (Screens.Top == Main.Training.gameObject) Screens.Pop(); };
            ApplySettings();
        }

        // Later phases (tree, styles, records, endless) switch on their menu entries here.
        partial void BuildProgressionMenus();
        // Later phases fill the run's style, passives and stats from the profile here.
        partial void ApplyLoadoutExtra(RunSetup setup);

        /// <summary>The profile is read once to build a run (RunSetup's contract) and not again until finalization.</summary>
        void ApplyLoadout(RunSetup setup)
        {
            setup.MasteryLevel = Profile.Profile.mastery.level;
            setup.BuildVersion = Application.version;
            ApplyLoadoutExtra(setup);
            // Last, so a cheat's Debug mark covers the passives the tree just resolved.
            Cheats.ApplyTo(setup);
        }

        public GameConfig Config => config;

        public void PlayShort() => StartRun(RunKind.Short);
        public void PlaySandbox() => StartRun(RunKind.Sandbox);

        void StartRun(RunKind k)
        {
            kind = k;
            Screens.Clear();
            BeginRun();
        }

        /// <summary>
        /// Back to the launch screen. Leaving mid-run ABANDONS it: nothing is finalized and no
        /// XP is given (D75); only the endings section 7 lists award progression.
        /// </summary>
        public void ShowMainMenu()
        {
            // One call closes every sub-screen (the old per-partial CloseSubMenus chain).
            Screens.Clear();
            Menu.Show(false);
            kind = RunKind.Backdrop;
            BeginRun();
            RefreshMainMenu();
            Main.Show(true);
            // The main menu is the stack's bottom entry: that is what hides it while Character,
            // Records or Settings is up. Esc on it does nothing; it never starts or resumes a run.
            // Push also raises it above the HUD and flow panels.
            Screens.Push(Main.gameObject, () => Main.DefaultFocus, () => { });
            SyncGameplayInput();
        }

        void RefreshMainMenu()
        {
            var m = Profile.Profile.mastery;
            Main.SetStatus(new MenuStatus
            {
                Mastery = m.level,
                StyleName = CaptureStyles.Resolve(Profile.Profile.styleId).Name,
                Points = m.points,
                // At max level the bar reads full rather than an empty 0/0.
                XpFraction = m.level >= Mastery.MaxLevel ? 1f : m.xp / (float)Mastery.CostToAdvance(m.level),
            });
            Main.SetWarning(Profile.Warning);        // save failures only: readable, on their own line
            // A cheated run never counts, so say so right under Play.
            Main.SetCheatNotice(Cheats.AnyActive);
        }

        void OnRunEnded(RunSummary summary)
        {
            // The sim raised this for ITS run; Sim may already have been replaced if a restart
            // landed in the same frame, so finalize with the summary's own setup.
            var setup = summary.RunId == Sim.RunId ? Sim.Setup : null;
            LastFinalize = Profile.FinalizeRun(summary, setup);
            // Task 16: one pure call regroups the finalize into outcome, rewards and Details.
            Flow.SetOutcome(ResultsCopy.FromFinalize(LastFinalize ?? new FinalizeResult(), Profile.Profile.mastery, Profile.Warning));
        }

        void OpenSettingsFromMain() => OpenSettings();
        void OpenSettingsFromPause() => OpenSettings();

        void OpenSettings()
        {
            Settings.Show(Profile.Profile.settings, Profile.Profile.narrative.settings, SettingsChanged, CloseSettings);
            Screens.Push(Settings.gameObject, () => Settings.DefaultFocus, null);
        }

        // The stack restores whichever menu opened it, and that menu's focus.
        void CloseSettings() { if (Screens.Top == Settings.gameObject) Screens.Pop(); }

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
            // Jam build fix: Unity restores the last window size/mode from the registry and that
            // beats the project's fullscreen default, so one windowed dev launch left later builds
            // stuck in a 960x540 window. "As launched" on a desktop player now means native
            // borderless fullscreen; players who want a window pick Windowed explicitly.
            else if (s.displayMode == -1 && !IsWeb && !Application.isEditor && !Screen.fullScreen)
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        }
    }
}
