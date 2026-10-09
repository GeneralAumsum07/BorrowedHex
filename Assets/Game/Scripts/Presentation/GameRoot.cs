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
    public sealed partial class GameRoot : MonoBehaviour, Feedback.IFeedbackHost
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
        /// <summary>The foreground screen router: one top screen owns input, focus and Esc.</summary>
        public ScreenStack Screens { get; } = new ScreenStack();
        public RunFlowPanels Flow { get; private set; }
        public ArenaView View { get; private set; }

        Camera cam;
        Canvas canvas;
        /// <summary>The UI canvas, read-only: the layout sweep reaches its scaler through this.</summary>
        public Canvas Canvas => canvas;
        float accumulator;
        int runCounter;

        // Hit-stop (D94, spec 4.2.3): real (unscaled) time during which no sim step runs. The sim
        // clock simply does not advance, so nothing about gameplay changes: it is the same run,
        // shown with a short beat. CombatFeedback decides when and how long (and honours Reduce
        // flashes); this only owns the clock it must not step.
        float hitStopUntil;
        CameraShake shake;
        public bool HitStopActive => Time.unscaledTime < hitStopUntil;

        public void HitStop(float seconds) =>
            hitStopUntil = (float)Feedback.FeedbackPolicy.ExtendHitStop(hitStopUntil, Time.unscaledTime, seconds);

        public void Shake(float amp, float duration)
        {
            // Not "?? AddComponent": Unity's destroyed-object null is invisible to ??.
            if (shake == null && cam != null)
            {
                shake = cam.GetComponent<CameraShake>();
                if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            }
            if (shake != null) shake.Kick(amp, duration);
        }

        bool debugFire;
        /// <summary>PlayMode test hook: the next sim step carries a release command.</summary>
        public void DebugFireSelected() => debugFire = true;

        static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        void Awake()
        {
            if (config == null) config = GameConfig.CreateDefault();
            // First, so the menus built below can already make sounds.
            InitAudio();
            Input = gameObject.AddComponent<PlayerInputReader>();
            cam = Camera.main;

            canvas = Ui.CreateCanvas("GameCanvas", 10);
            Ui.CreateEventSystem(Input);
            Hud = GameplayHud.Create(canvas, () => SetMenuOpen(true), Restart);
            // Desktop pause menu: Main menu + Quit. In a browser tab Quit returns to the main
            // menu instead (Phase 8): closing the tab is the browser's job.
            // Task 12: every way out of a counted run from pause asks first (ConfirmLeave). The
            // HUD's own Reset above is practice-only, so it restarts directly.
            System.Action quit = IsWeb ? null : () => ConfirmLeave("Quit the game?", Application.Quit);
            Menu = PauseMenu.Create(canvas, () => SetMenuOpen(false), () => ConfirmLeave("Restart run?", Restart), quit);
            Menu.AddButton(() => "Settings", OpenSettingsFromPause, "Settings", first: true);
            Menu.AddButton(() => IsWeb ? "Quit to menu" : "Main menu", () => ConfirmLeave("Abandon this run?", ShowMainMenu), "MainMenuButton");
            // uGUI draws later siblings on top: the pause menu is raised above the flow panels
            // so pausing during an upgrade choice shows the menu, not the panel behind it.
            // D96: a card click passes (card, replace-or--1); Continue is the free "take nothing".
            Flow = RunFlowPanels.Create(canvas, (i, r) => Sim.ChooseUpgrade(i, r), () => Sim.ContinueFromUpgrade(), Restart, ShowMainMenu, () => Sim.RetireRun());
            // Phase 14: built before the pause menu is raised, so Esc over the tutorial card
            // still shows the pause menu on top.
            BuildTutorial();
            Menu.transform.SetAsLastSibling();
            BuildDevPanel();
            BuildMenus();
            // After the menus, then slotted just under the pause menu (GameRoot.Narrative.cs).
            BuildNarrative();

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
            // GameRoot reports the end of the Sanctum pull itself (TickNarrative), so the
            // anomaly can play between the arrival and the final upgrades (lore plan Task 3).
            Sim.HoldSanctumArrival = true;
            // The sandbox upgrade picked on the dev button survives Reset, like auto-spawn.
            if (kind == RunKind.Sandbox && devUpgrade > 0) Sim.ForceUpgrade(UpgradeInfo.Pool[devUpgrade - 1]);
            // The one path into the profile: finalized exactly once per run ID (section 8).
            Sim.Events.RunEnded += OnRunEnded;
            BindAudio();
            // A freeze from the previous run must not carry into the new one.
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
            View = ArenaView.Create(Sim, this);
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
            // Last: cancels the previous run's scene and may start this run's prologue.
            BindNarrative();
        }

        public void Restart() => BeginRun();

        /// <summary>
        /// A run whose result reaches the profile (records, mastery) and is still in progress:
        /// leaving it throws something away. Practice, debug and finished runs lose nothing.
        /// </summary>
        public bool IsCountedRunActive =>
            (kind == RunKind.Short || kind == RunKind.Endless) && Sim != null && !Sim.Setup.Debug && Sim.State != RunState.Results;

        /// <summary>
        /// Leave or restart, asking first when a counted run would be lost. The question is an
        /// overlay on the pause menu with Cancel focused, so a stray Enter or Esc keeps the run.
        /// </summary>
        void ConfirmLeave(string title, System.Action go)
        {
            if (!IsCountedRunActive) { go(); return; }
            Confirm.Ask(title, "Unfinished progress from this run is lost.",
                title.StartsWith("Restart") ? "Restart" : title.StartsWith("Quit") ? "Quit" : "Leave", go, Screens);
        }

        /// <summary>
        /// D97 (R14): R restarts ONLY from the results screen. In combat it would throw runs away
        /// by accident; the pause menu already has Restart. The profile was finalized inside the
        /// tick that ended the run, before the results appeared, so restarting at once is safe.
        /// </summary>
        public bool HandleRestartKey(bool pressed)
        {
            // R never answers the confirm dialog: only its own buttons (or Esc) do.
            if (Confirm != null && Screens.Top == Confirm.gameObject) return false;
            // The victory ending holds the results: R cannot skip past it into a new run.
            if (Story != null && Story.IsPlaying) return false;
            if (!pressed || InMainMenu || Sim == null || Sim.State != RunState.Results) return false;
            Restart();
            return true;
        }

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
            Sim.SetPause(PauseReason.Menu, open);
            // Only the life bar stays over the pause; the rest of the HUD (and the drawer) hides.
            Hud.SetCovered(open);
            if (open)
            {
                // Already up (perhaps under Settings): a focus loss must not raise it over that.
                if (!Screens.Contains(Menu.gameObject))
                {
                    // Task 13: the held upgrades, named and explained, for a calm look mid-run.
                    Menu.SetHeld(Sim.HeldUpgrades, Sim.Config.upgrades);
                    Menu.Show(true);
                    Screens.Push(Menu.gameObject, () => Menu.DefaultFocus, () => SetMenuOpen(false));
                }
            }
            else
            {
                Screens.Clear();
                Menu.Show(false);
                // Nothing focused in combat, so Enter cannot re-trigger the last menu button.
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null) es.SetSelectedGameObject(null);
            }
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
            bool on = !InMainMenu && Screens.Count == 0
                && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition)
                && !Sim.Clock.HasPauseReason(PauseReason.Narrative)
                && (s == RunState.Ready || s == RunState.Combat || s == RunState.BossCombat);
            if (gameplayInput == on) return;
            gameplayInput = on;
            Input.SetGameplayEnabled(on);
        }

        void Update()
        {
            bool pausePressed = Input.ConsumePause();
            bool restartPressed = Input.ConsumeRestart();
            if (cam == null) cam = Camera.main;
            // Menus fade on unscaled time: they animate while the run is paused.
            Screens.Tick(Time.unscaledTime);
            if (InMainMenu)
            {
                // Esc closes the top sub-screen back to the menu; on the menu itself it does
                // nothing, and it never starts or resumes anything. During a Story replay Esc
                // ends the replay, back onto the Story list (lore plan Task 4).
                if (pausePressed && !EscapeReplay()) Screens.Escape();
                SyncGameplayInput();
                View.Render(1f);
                return;
            }
            if (pausePressed)
            {
                // One rule (plan Task 4): the top screen handles Esc. Only with nothing stacked
                // does Esc reach the game, and then only to OPEN pause: the pause menu is on the
                // stack, so closing it is its own onEscape (resume).
                if (!Screens.Escape()) SetMenuOpen(true);
            }
            SyncGameplayInput();
            if (HandleRestartKey(restartPressed)) return;
            TickNarrative();
            // The boss banner holds the frozen frame for a beat, then the fight starts.
            if (Sim.State == RunState.BossIntro && !Sim.Clock.HasPauseReason(PauseReason.WorldTransition)
                && Flow.BannerDone && !NarrativeHoldsBossIntro) Sim.CompleteBossIntro();

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
        /// <summary>
        /// Editor tooling only (scripted screenshots): the unfocused editor would otherwise
        /// auto-pause every run. Never set by game code, so players always get the focus pause.
        /// </summary>
        public static bool IgnoreFocus;

        public void SetFocus(bool hasFocus)
        {
            if (Sim == null || IgnoreFocus) return;
            Sim.SetPause(PauseReason.FocusLost, !hasFocus);
            if (!hasFocus) SetMenuOpen(true);
        }

        void OnApplicationFocus(bool hasFocus) => SetFocus(hasFocus);
        void OnApplicationPause(bool paused) { if (paused) SetFocus(false); }
    }
}
