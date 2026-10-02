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
    /// reader, canvas, HUD, menu) lives here; everything per-run (sim, ArenaView) is rebuilt.
    ///
    /// Runs after the input reader (execution order) so a press latched this frame is
    /// consumed by this frame's sim steps instead of waiting a frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GameRoot : MonoBehaviour
    {
        public GameConfig config;

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
        float accumulator;
        int runCounter;

        void Awake()
        {
            if (config == null) config = GameConfig.CreateDefault();
            Input = gameObject.AddComponent<PlayerInputReader>();
            cam = Camera.main;

            var canvas = Ui.CreateCanvas("GameCanvas", 10);
            Ui.CreateEventSystem(Input);
            Hud = GameplayHud.Create(canvas, () => SetMenuOpen(true), Restart);
            // Desktop gets a Quit button; in a browser tab, quitting is the browser's job.
            System.Action quit = Application.platform == RuntimePlatform.WebGLPlayer ? null : Application.Quit;
            Menu = PauseMenu.Create(canvas, () => SetMenuOpen(false), Restart, quit);
            Menu.AddButton(() => ModeSwitchLabel, SwitchMode);
            // uGUI draws later siblings on top: the pause menu is raised above the flow panels
            // so pausing during an upgrade choice shows the menu, not the panel behind it.
            Flow = RunFlowPanels.Create(canvas, i => Sim.ChooseUpgrade(i), Restart, SwitchMode, () => ModeSwitchLabel);
            Menu.transform.SetAsLastSibling();
            BuildDevPanel();

            BeginRun();
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

        // Phase 5: the default run is a scored short run. The sandbox (dev panel, free
        // practice) stays one click away in the pause menu and on the results screen. Held
        // here so it survives restarts, like the auto-spawn switch.
        bool sandboxMode;
        string ModeSwitchLabel => sandboxMode ? "Play short run" : "Practice sandbox";

        void SwitchMode()
        {
            sandboxMode = !sandboxMode;
            BeginRun();
        }

        /// <summary>Start a fresh run: a new sim and a new view; the old ones are discarded whole.</summary>
        public void BeginRun()
        {
            if (View != null) Destroy(View.gameObject);
            runCounter++;
            RunSetup setup;
            if (sandboxMode)
            {
                setup = RunSetup.ForSandbox(runCounter);
                setup.SandboxAutoSpawn = autoSpawn; // practice: formations cycle while the arena is clear (toggleable)
            }
            else
            {
                // A fresh seed per run, so formation picks and spawn points vary between runs
                // (section 6: seeded variation); the seed is kept in the summary for debugging.
                setup = new RunSetup { Mode = GameMode.Short, Seed = System.Environment.TickCount ^ (runCounter * 7919) };
            }
            Sim = new ArenaSim(config, setup);
            // The sandbox upgrade picked on the dev button survives Reset, like auto-spawn.
            if (sandboxMode && devUpgrade > 0) Sim.ForceUpgrade(UpgradeInfo.Pool[devUpgrade - 1]);
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
            Hud.SetResetVisible(Sim.Setup.Sandbox || Sim.Setup.Debug);
            accumulator = 0f;
            gameplayInput = null;
            SetMenuOpen(false);
        }

        public void Restart() => BeginRun();

        /// <summary>
        /// Menu open == PauseReason.Menu on the clock AND gameplay input off. The reason is
        /// separate from FocusLost, so regaining focus never silently resumes an open menu.
        /// </summary>
        public void SetMenuOpen(bool open)
        {
            // The results screen is its own menu; Esc there does nothing rather than stacking
            // a pause menu over it.
            if (open && Sim.State == RunState.Results) open = false;
            Sim.SetPause(PauseReason.Menu, open);
            Menu.Show(open);
            SyncGameplayInput();
        }

        bool? gameplayInput;

        /// <summary>
        /// Gameplay input is live only in actual combat with no menu up: never during an
        /// upgrade choice, the boss banner or the results, so a click on Continue can never also
        /// arrive as a catch.
        /// </summary>
        void SyncGameplayInput()
        {
            var s = Sim.State;
            bool on = !Menu.IsOpen && (s == RunState.Ready || s == RunState.Combat || s == RunState.BossCombat);
            if (gameplayInput == on) return;
            gameplayInput = on;
            Input.SetGameplayEnabled(on);
        }

        void Update()
        {
            if (Input.ConsumePause()) SetMenuOpen(!Menu.IsOpen);
            if (cam == null) cam = Camera.main;
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

            accumulator += Mathf.Min(Time.unscaledDeltaTime, MaxFrame);
            while (accumulator >= Step)
            {
                // One command per step; latched presses are cleared by the first consume, so a
                // single Space press can never start two dashes in a multi-step frame.
                var cmd = Input.ConsumeCommand(cam);
                View.BeforeStep();
                Sim.Tick(cmd, Step);
                View.AfterStep();
                accumulator -= Step;
            }
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
