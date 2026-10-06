using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BorrowedHex.Player
{
    /// <summary>
    /// Owns the Input System actions: a "Gameplay" map (move/aim/catch/dash/release/cycle) and a "UI" map
    /// (pointer, navigation, pause). The maps are separate so menus can disable gameplay
    /// input entirely; a click on a HUD button is additionally filtered so it never catches.
    ///
    /// Presses are latched here and consumed by the fixed-step simulation, so a press that
    /// lands between two sim steps is applied exactly once.
    ///
    /// Runs early (execution order) so presses are latched before GameRoot steps the sim.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public InputActionAsset Asset { get; private set; }
        public InputActionMap Gameplay { get; private set; }
        public InputActionMap Ui { get; private set; }

        InputAction move, aim, catchAction, dash, release, cycle, pause, restart;
        public InputAction UiPoint { get; private set; }
        public InputAction UiClick { get; private set; }
        public InputAction UiNavigate { get; private set; }
        public InputAction UiSubmit { get; private set; }
        public InputAction UiCancel { get; private set; }
        public InputAction UiScroll { get; private set; }

        bool catchLatched, dashLatched, releaseLatched, cycleLatched, pauseLatched, restartLatched;

        /// <summary>
        /// Count of catch presses accepted for gameplay (diagnostics/tests). Lets a test prove
        /// a click on a HUD button was routed to the UI and never became a catch.
        /// </summary>
        public int AcceptedCatchPresses { get; private set; }

        /// <summary>Mouse sensitivity is not used for absolute cursor aim; kept for settings.</summary>
        public Vector2 PointerPosition => aim != null ? aim.ReadValue<Vector2>() : Vector2.zero;

        void Awake() => Build();

        void Build()
        {
            if (Asset != null) return;
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "BorrowedHexInput";

            Gameplay = Asset.AddActionMap("Gameplay");
            move = Gameplay.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            aim = Gameplay.AddAction("Aim", InputActionType.PassThrough, "<Pointer>/position");
            catchAction = Gameplay.AddAction("Catch", InputActionType.Button, "<Mouse>/leftButton");
            dash = Gameplay.AddAction("Dash", InputActionType.Button, "<Keyboard>/space");
            // D32/D33: right mouse fires the selected packet early; Q cycles the selection.
            // The Web loader already suppresses the browser context menu on the canvas.
            release = Gameplay.AddAction("Release", InputActionType.Button, "<Mouse>/rightButton");
            cycle = Gameplay.AddAction("CycleSlot", InputActionType.Button, "<Keyboard>/q");

            Ui = Asset.AddActionMap("UI");
            UiPoint = Ui.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            UiClick = Ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
            UiScroll = Ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll");
            UiNavigate = Ui.AddAction("Navigate", InputActionType.PassThrough);
            UiNavigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            UiSubmit = Ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            UiCancel = Ui.AddAction("Cancel", InputActionType.Button);
            // Escape can also leave browser fullscreen, so P is a fallback on both platforms.
            pause = Ui.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            pause.AddBinding("<Keyboard>/p");
            // D97: one key to retry from the results screen. Lives in the UI map because the
            // gameplay map is off on the results screen. GameRoot decides when it counts.
            restart = Ui.AddAction("Restart", InputActionType.Button, "<Keyboard>/r");

            Ui.Enable();
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled) Gameplay.Enable();
            else
            {
                Gameplay.Disable();
                ClearLatches();
            }
        }

        void Update()
        {
            if (pause.WasPressedThisFrame()) pauseLatched = true;
            if (restart.WasPressedThisFrame()) restartLatched = true;
            if (!Gameplay.enabled) return;
            // A press that starts over a HUD button belongs to the UI, never to combat.
            if (catchAction.WasPressedThisFrame() && !PointerOverUi())
            {
                catchLatched = true;
                AcceptedCatchPresses++;
            }
            if (dash.WasPressedThisFrame()) dashLatched = true;
            // Same UI filter as catch: right-clicking a HUD element is not a release.
            if (release.WasPressedThisFrame() && !PointerOverUi()) releaseLatched = true;
            if (cycle.WasPressedThisFrame()) cycleLatched = true;
        }

        static bool PointerOverUi()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        /// <summary>Test hook: a pause press (Esc or P) without a keyboard device.</summary>
        internal void SimulatePause() => pauseLatched = true;

        public bool ConsumePause()
        {
            bool p = pauseLatched;
            pauseLatched = false;
            return p;
        }

        public bool ConsumeRestart()
        {
            bool r = restartLatched;
            restartLatched = false;
            return r;
        }

        /// <summary>Build the command for one sim step; latched presses are cleared.</summary>
        public PlayerCommand ConsumeCommand(Camera cam)
        {
            var cmd = new PlayerCommand();
            if (Gameplay.enabled)
            {
                cmd.Move = CameraRelative(cam, move.ReadValue<Vector2>());
                if (AimResolver.TryProject(cam, aim.ReadValue<Vector2>(), out var p))
                {
                    cmd.HasAim = true;
                    cmd.AimPoint = p;
                }
                cmd.Catch = catchLatched;
                cmd.Dash = dashLatched;
                cmd.Release = releaseLatched;
                cmd.CycleSlot = cycleLatched;
            }
            catchLatched = dashLatched = releaseLatched = cycleLatched = false;
            return cmd;
        }

        public void ClearLatches() => catchLatched = dashLatched = releaseLatched = cycleLatched = false;

        /// <summary>WASD relative to the camera's view, flattened onto the gameplay plane.</summary>
        static Vector2 CameraRelative(Camera cam, Vector2 raw)
        {
            if (raw.sqrMagnitude < 1e-6f || cam == null) return raw;
            Vector3 f = cam.transform.forward; f.y = 0;
            Vector3 r = cam.transform.right; r.y = 0;
            if (f.sqrMagnitude < 1e-6f || r.sqrMagnitude < 1e-6f) return raw;
            f.Normalize(); r.Normalize();
            Vector3 w = r * raw.x + f * raw.y;
            return new Vector2(w.x, w.z);
        }

        void OnDestroy()
        {
            if (Asset != null)
            {
                Asset.Disable();
                Destroy(Asset);
            }
        }
    }

    public static class AimResolver
    {
        /// <summary>
        /// Project a screen position onto the y = 0 gameplay plane. Fails (caller keeps the last
        /// valid aim) when the cursor is outside the window or the ray cannot reach the plane.
        /// </summary>
        public static bool TryProject(Camera cam, Vector2 screen, out Vector2 world)
        {
            world = default;
            if (cam == null) return false;
            if (float.IsNaN(screen.x) || float.IsNaN(screen.y)) return false;
            if (screen.x < 0 || screen.y < 0 || screen.x > Screen.width || screen.y > Screen.height) return false;
            Ray ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            Vector3 hit = ray.origin + ray.direction * t;
            world = new Vector2(hit.x, hit.z);
            return true;
        }
    }
}
