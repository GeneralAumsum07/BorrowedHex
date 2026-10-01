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
            Hud.AddDevButton("+ Acolyte", () =>
                Sim.SpawnEnemy(ActorCategory.Acolyte, Sim.FindSpawnPoint(config.combat.acolyte.bodyRadius)));
            Hud.AddDevButton("Lantern volley", () => Sim.FireLantern());

            BeginRun();
        }

        /// <summary>Start a fresh run: a new sim and a new view; the old ones are discarded whole.</summary>
        public void BeginRun()
        {
            if (View != null) Destroy(View.gameObject);
            runCounter++;
            // Phase 1 has no menus yet, so every run is a sandbox; Phase 5/7 pass a real setup.
            var setup = RunSetup.ForSandbox(runCounter);
            setup.SandboxAutoSpawn = true; // Phase 2 practice: always one acolyte to dodge
            Sim = new ArenaSim(config, setup);
            View = ArenaView.Create(Sim);
            Hud.Bind(Sim);
            Hud.SetResetVisible(Sim.Setup.Sandbox || Sim.Setup.Debug);
            accumulator = 0f;
            SetMenuOpen(false);
        }

        public void Restart() => BeginRun();

        /// <summary>
        /// Menu open == PauseReason.Menu on the clock AND gameplay input off. The reason is
        /// separate from FocusLost, so regaining focus never silently resumes an open menu.
        /// </summary>
        public void SetMenuOpen(bool open)
        {
            Sim.Clock.SetPauseReason(PauseReason.Menu, open);
            Menu.Show(open);
            Input.SetGameplayEnabled(!open);
        }

        void Update()
        {
            if (Input.ConsumePause()) SetMenuOpen(!Menu.IsOpen);
            if (cam == null) cam = Camera.main;

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
            Sim.Clock.SetPauseReason(PauseReason.FocusLost, !hasFocus);
            if (!hasFocus) SetMenuOpen(true);
        }

        void OnApplicationFocus(bool hasFocus) => SetFocus(hasFocus);
        void OnApplicationPause(bool paused) { if (paused) SetFocus(false); }
    }
}
