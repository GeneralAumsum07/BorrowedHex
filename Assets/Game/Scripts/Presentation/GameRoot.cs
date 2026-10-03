using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Scene entry point for Arena.unity: owns the current ArenaSim, steps it at a fixed 60 Hz
    /// and wires input, views and UI around it. Everything that persists across restarts (input
    /// reader, canvas, HUD, menus, profile) lives here; everything per-run (sim, ArenaView) is
    /// rebuilt. The profile and the menus live in GameRoot.Menus.cs.
    ///
    /// Runs after the input reader (execution order) so a press latched this frame is
    /// consumed by this frame's sim steps instead of waiting a frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class GameRoot : MonoBehaviour
    {
        public GameConfig config;
        public bool useWorldArenas;

        /// <summary>Fixed sim step. Fixed rather than per-frame so outcomes do not depend on frame rate.</summary>
        public const float Step = 1f / 60f;

        /// <summary>
        /// Longest frame we simulate. A hitch (window drag, GC, tab switch) beyond this is
        /// dropped rather than replayed as a burst of steps the player could not react to.
        /// </summary>
        const float MaxFrame = 0.1f;

        public ArenaSim Sim { get; private set; }
        public PlayerInputReader Input { get; private set; }
        public GameplayHud Hud { get; private set; }
        public PauseMenu Menu { get; private set; }
        public RunFlowPanels Flow { get; private set; }
        public ArenaView View { get; private set; }

        Camera cam;
        Canvas canvas;
        float accumulator;
        int runCounter;

        // D94: Overcharge freeze-frame. Real (unscaled) time during which no sim step runs. The
        // sim clock simply does not advance, so nothing about gameplay changes: it is the same
        // run, shown with a 70 ms beat on the perfect release.
        const float HitStopSeconds = 0.07f;
        float hitStopUntil;
        CameraShake shake;
        public bool HitStopActive => Time.unscaledTime < hitStopUntil;

        void OnOvercharged(BorrowedHex.Combat.CapturedPacket p, int root)
        {
            // R12: both effects honour Reduce flashes, the existing motion/flash accessibility switch.
            if (DisplayOptions.ReduceFlashes) return;
            hitStopUntil = Time.unscaledTime + HitStopSeconds;
            // Not "?? AddComponent": Unity's destroyed-object null is invisible to ??.
            if (shake == null && cam != null)
            {
                shake = cam.GetComponent<CameraShake>();
                if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            }
            if (shake != null) shake.Kick(0.18f, 0.25f);
        }

        bool debugFire;
        /// <summary>PlayMode test hook: the next sim step carries a release command.</summary>
        public void DebugFireSelected() => debugFire = true;

        static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        void Awake()
        {
            if (config == null) config = GameConfig.CreateDefault();
            Input = gameObject.AddComponent<PlayerInputReader>();
            cam = Camera.main;

            canvas = Ui.CreateCanvas("GameCanvas", 10);
            Ui.CreateEventSystem(Input);
            Hud = GameplayHud.Create(canvas, () => SetMenuOpen(true), Restart);
            // Desktop pause menu: Main menu + Quit. In a browser tab Quit returns to the main
            // menu instead (Phase 8): closing the tab is the browser's job.
            System.Action quit = IsWeb ? null : Application.Quit;
            Menu = PauseMenu.Create(canvas, () => SetMenuOpen(false), Restart, quit);
            Menu.AddButton(() => "Settings", OpenSettingsFromPause);
            Menu.AddButton(() => IsWeb ? "Quit to menu" : "Main menu", ShowMainMenu);
            // uGUI draws later siblings on top: the pause menu is raised above the flow panels
            // so pausing during an upgrade choice shows the menu, not the panel behind it.
            Flow = RunFlowPanels.Create(canvas, i => Sim.ChooseUpgrade(i), Restart, ShowMainMenu, () => Sim.RetireRun());
            // Phase 14: built before the pause menu is raised, so Esc over the tutorial card
            // still shows the pause menu on top.
            BuildTutorial();
            Menu.transform.SetAsLastSibling();
            BuildDevPanel();
            BuildMenus();

            ShowMainMenu();
        }

        /// <summary>
        /// Playtest controls (sandbox/debug only, hidden in scored runs with the Reset button).
        /// Single-kind summons let one matchup be practised in isolation — e.g. auto-spawn off,
        /// one Pursuer, drill the parry timing.
        /// </summary>
        void BuildDevPanel()
        {
            Hud.AddDevButton("+ Formation", () => Sim.SpawnNextSandboxFormation());
            Hud.AddDevButton("+ Acolyte", () => Sim.SummonEnemy(ActorCategory.Acolyte));
            Hud.AddDevButton("+ Pursuer", () => Sim.SummonEnemy(ActorCategory.Pursuer));
            Hud.AddDevButton("+ Scatter", () => Sim.SummonEnemy(ActorCategory.ScatterCaster));
            Hud.AddDevButton("+ Siege", () => Sim.SummonEnemy(ActorCategory.SiegeFamiliar));
            autoSpawnButton = Hud.AddDevButton(AutoSpawnLabel, () =>
            {
                autoSpawn = !autoSpawn;
                Sim.AutoSpawn = autoSpawn;
                autoSpawnButton.GetComponentInChildren<UnityEngine.UI.Text>().text = AutoSpawnLabel;
            });
            Hud.AddDevButton("+ Collector", () => { if (Sim.LivingBoss() == null) Sim.SpawnBoss(); });
            Hud.AddDevButton("Clear arena", () => Sim.ClearArena());
            // Phase 7 practice: cycle the held upgrade (none -> each in pool order -> none).
            UnityEngine.UI.Button upgradeButton = null;
            upgradeButton = Hud.AddDevButton("Upgrade: none", () =>
            {
                devUpgrade = (devUpgrade + 1) % (UpgradeInfo.Pool.Length + 1);
                if (devUpgrade == 0) Sim.ClearUpgrade(); else Sim.ForceUpgrade(UpgradeInfo.Pool[devUpgrade - 1]);
                upgradeButton.GetComponentInChildren<UnityEngine.UI.Text>().text =
                    "Upgrade: " + (devUpgrade == 0 ? "none" : UpgradeInfo.Name(UpgradeInfo.Pool[devUpgrade - 1]));
            });
        }

        // Held here, not on the sim, so the choice survives Reset: a tester drilling one enemy
        // should not have the director switch back on every restart.
        bool autoSpawn = true;
        int devUpgrade;   // 0 = none, else 1 + index into UpgradeInfo.Pool; reapplied on Reset
        UnityEngine.UI.Button autoSpawnButton;
        string AutoSpawnLabel => autoSpawn ? "Auto-spawn: ON" : "Auto-spawn: OFF";

        /// <summary>What the next BeginRun starts. Chosen on the main menu; Restart repeats it.</summary>
        enum RunKind { Backdrop, Short, Sandbox, Endless, EndlessDebug, Tutorial }
        RunKind kind = RunKind.Backdrop;

        /// <summary>Start a fresh run of the current kind: a new sim and view; the old ones are discarded whole.</summary>
        public void BeginRun()
        {
            if (View != null) Destroy(View.gameObject);
            runCounter++;
            RunSetup setup;
            if (kind == RunKind.Short)
            {
                // A fresh seed per run, so formation picks and spawn points vary between runs
                // (section 6: seeded variation); the seed is kept in the summary for debugging.
                setup = new RunSetup { Mode = GameMode.Short, Seed = System.Environment.TickCount ^ (runCounter * 7919) };
                ApplyLoadout(setup);
            }
            else if (kind == RunKind.Endless || kind == RunKind.EndlessDebug)
            {
                // Phase 12. The debug variant is the same run with the dev panel on; it is
                // marked Debug up front, so nothing it does can reach the profile.
                setup = new RunSetup { Mode = GameMode.Endless, Seed = System.Environment.TickCount ^ (runCounter * 7919),
                    Debug = kind == RunKind.EndlessDebug };
                ApplyLoadout(setup);
            }
            else if (kind == RunKind.Tutorial)
            {
                // Phase 14 (D86): a sandbox with the lesson script and a frozen clock. The
                // loadout still applies so the tutorial teaches the style the player picked
                // (a Daredevil catches by dashing, and the prompts are read with that in mind).
                setup = RunSetup.ForTutorial(runCounter);
                ApplyLoadout(setup);
            }
            else
            {
                setup = RunSetup.ForSandbox(runCounter);
                // The backdrop behind the main menu is an empty, frozen arena; practice cycles
                // formations while the arena is clear (toggleable).
                setup.SandboxAutoSpawn = kind == RunKind.Sandbox && autoSpawn;
                if (kind == RunKind.Sandbox) ApplyLoadout(setup);
            }
            setup.WorldArenas = useWorldArenas && !setup.Tutorial
                && FindFirstObjectByType<WorldArt.WorldPresentation>() != null;
            Sim = new ArenaSim(config, setup);
            // The sandbox upgrade picked on the dev button survives Reset, like auto-spawn.
            if (kind == RunKind.Sandbox && devUpgrade > 0) Sim.ForceUpgrade(UpgradeInfo.Pool[devUpgrade - 1]);
            // The one path into the profile: finalized exactly once per run ID (section 8).
            Sim.Events.RunEnded += OnRunEnded;
            // The old sim is discarded with its subscriptions, so nothing leaks across runs;
            // a freeze from the previous run must not carry into the new one.
            Sim.Events.PacketOvercharged += OnOvercharged;
            hitStopUntil = 0f;
            // Keep the authored arena meshes and bind their disposable state to each run.
            // Tests without an arena and future layouts with fewer pillars simply skip them.
            for (int i = 0; i < Sim.Pillars.Count; i++)
            {
                var pillar = GameObject.Find($"Pillar_{i}");
                if (pillar == null) continue;
                var view = pillar.GetComponent<DecayingPillarView>();
                if (view == null) view = pillar.AddComponent<DecayingPillarView>();
                view.Bind(Sim, i);
            }
            View = ArenaView.Create(Sim);
            Hud.Bind(Sim);
            Flow.Bind(Sim);
            TutorialUi.Bind(Sim);
            // The tutorial is a sandbox under the hood, but its dev panel would let a new player
            // summon a boss into lesson one: hidden, like in a scored run.
            Hud.SetResetVisible((Sim.Setup.Sandbox || Sim.Setup.Debug) && !Sim.Setup.Tutorial);
            Hud.gameObject.SetActive(kind != RunKind.Backdrop);
            accumulator = 0f;
            gameplayInput = null;
            if (kind == RunKind.Backdrop) Sim.SetPause(PauseReason.Menu, true);
            else SetMenuOpen(false);
        }

        public void Restart() => BeginRun();

        /// <summary>
        /// Menu open == PauseReason.Menu on the clock AND gameplay input off. The reason is
        /// separate from FocusLost, so regaining focus never silently resumes an open menu.
        /// </summary>
        public void SetMenuOpen(bool open)
        {
            // The main menu owns the screen: no pause menu over it, and nothing here may
            // unpause its frozen backdrop.
            if (InMainMenu) return;
            // The results screen is its own menu; Esc there does nothing rather than stacking
            // a pause menu over it.
            if (open && Sim.State == RunState.Results) open = false;
            if (!open) Settings.Hide();
            Sim.SetPause(PauseReason.Menu, open);
            Menu.Show(open);
            SyncGameplayInput();
        }

        bool? gameplayInput;

        /// <summary>
        /// Gameplay input is live only in actual combat with no menu up: never on the main menu,
        /// during an upgrade choice, the boss banner or the results, so a click on a menu button
        /// can never also arrive as a catch.
        /// </summary>
        void SyncGameplayInput()
        {
            var s = Sim.State;
            bool on = !InMainMenu && !Menu.IsOpen && !Settings.IsOpen
                && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition)
                && (s == RunState.Ready || s == RunState.Combat || s == RunState.BossCombat);
            if (gameplayInput == on) return;
            gameplayInput = on;
            Input.SetGameplayEnabled(on);
        }

        void Update()
        {
            bool pausePressed = Input.ConsumePause();
            if (cam == null) cam = Camera.main;
            if (InMainMenu)
            {
                // Esc closes settings (or a later phase's sub-menu) back to the menu; it never
                // starts or resumes anything.
                if (pausePressed)
                {
                    if (Settings.IsOpen) CloseSettings();
                    else if (CheatPanel.IsOpen) CloseCheats();
                    else CloseSubMenus();
                }
                SyncGameplayInput();
                View.Render(1f);
                return;
            }
            if (pausePressed)
            {
                if (Settings.IsOpen) CloseSettings();
                else SetMenuOpen(!Menu.IsOpen);
            }
            SyncGameplayInput();
            // The boss banner holds the frozen frame for a beat, then the fight starts.
            if (Sim.State == RunState.BossIntro && Flow.BannerDone) Sim.CompleteBossIntro();

            if (Sim.Clock.IsPaused)
            {
                // Drop banked time: resuming must not fast-forward through the pause.
                accumulator = 0f;
                View.Render(1f);
                return;
            }

            if (HitStopActive)
            {
                // Same rule as a pause: real time spent frozen is dropped, never fast-forwarded.
                accumulator = 0f;
                View.Render(1f);
                return;
            }

            accumulator += Mathf.Min(Time.unscaledDeltaTime, MaxFrame);
            while (accumulator >= Step)
            {
                // One command per step; latched presses are cleared by the first consume, so a
                // single Space press can never start two dashes in a multi-step frame.
                var cmd = Input.ConsumeCommand(cam);
                if (debugFire) { cmd = cmd.WithRelease(); debugFire = false; }
                View.BeforeStep();
                Sim.Tick(cmd, Step);
                View.AfterStep();
                accumulator -= Step;
                // A perfect release inside a multi-step frame freezes right there, rather than
                // running the frame's remaining steps first.
                if (HitStopActive) { accumulator = 0f; break; }
            }
            TickTutorialCard();
            View.Render(accumulator / Step);
        }

        /// <summary>
        /// Focus loss pauses combat (section 8) and opens the pause menu, so coming back to the
        /// window (or browser tab) lands on Resume instead of straight into live combat.
        /// </summary>
        public void SetFocus(bool hasFocus)
        {
            if (Sim == null) return;
            Sim.SetPause(PauseReason.FocusLost, !hasFocus);
            if (!hasFocus) SetMenuOpen(true);
        }

        void OnApplicationFocus(bool hasFocus) => SetFocus(hasFocus);
        void OnApplicationPause(bool paused) { if (paused) SetFocus(false); }
    }
}
