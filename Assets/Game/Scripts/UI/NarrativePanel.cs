using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>One story page: an optional speaker (null for narration) and its passage.</summary>
    public readonly struct NarrativePage
    {
        public readonly string Speaker;
        public readonly string Text;
        public NarrativePage(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    /// <summary>
    /// Lore plan (Docs/LORE_IMPLEMENTATION_PLAN.md), submission cut: a full-screen black page
    /// with centred text that types out to a soft synthetic tick. Enter, Space or left click
    /// reveals the rest of a typing passage; a second fresh press advances. Skip ends the scene.
    ///
    /// The panel is opaque and a raycast target, so the upgrade/results panels behind it can
    /// be neither seen nor clicked while a scene plays. It owns no game state: GameRoot decides
    /// when a scene plays, freezes the sim, and is told through the callback when it ends.
    /// </summary>
    public sealed class NarrativePanel : MonoBehaviour
    {
        // Plan section 2: 35 text elements per second, ticks no closer than 0.06 s.
        const float CharsPerSecond = 35f;
        const float TickGap = 0.06f;
        // Longest frame of typing we accept. A hitch or a return from the pause menu must not
        // fast-forward a passage (and with it, emit a burst of ticks).
        const float MaxFrame = 0.1f;

        Text speakerLabel, body, hint;
        AudioSource tickSource;
        AudioClip tickClip;

        readonly List<NarrativePage> pages = new List<NarrativePage>();
        int pageIndex;
        float revealed;          // characters revealed so far (fractional while typing)
        int lastRevealedCount;
        float lastTickAt = -1f;
        int openedFrame = -1;    // the press that opened/changed a page must not also advance it
        Action onFinished;
        GameObject restoreSelection;
        bool restorePending;

        /// <summary>A scene is on screen. GameRoot holds the sim and gameplay input while true.</summary>
        public bool IsPlaying => gameObject.activeSelf;
        /// <summary>Set by GameRoot while the pause menu (or a focus loss) sits on top: typing, ticks and input stop.</summary>
        public bool Frozen { get; set; }
        /// <summary>Dialogue tick loudness (0 mutes it; the story and its controls are unchanged).</summary>
        public float TickVolume { get; set; } = 0.6f;

        public static NarrativePanel Create(Canvas canvas)
        {
            var bg = Ui.Image("NarrativePanel", canvas.transform, Color.black);
            Ui.Stretch(bg.rectTransform);
            // Opaque and raycast-blocking on purpose: the panels underneath stay unclickable.
            bg.raycastTarget = true;
            var p = bg.gameObject.AddComponent<NarrativePanel>();
            p.Build(bg.transform);
            bg.gameObject.SetActive(false);
            return p;
        }

        void Build(Transform root)
        {
            // Central safe area: 1100 units wide keeps a 28-word passage to about five lines,
            // well clear of the edges at any supported aspect ratio.
            speakerLabel = Ui.Label("Speaker", root, "", 32);
            speakerLabel.color = UiPalette.Honey;
            Ui.Place(speakerLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1100, 60));

            body = Ui.Label("Passage", root, "", 40);
            body.supportRichText = true;
            body.lineSpacing = 1.25f;
            Ui.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1100, 300));

            hint = Ui.Label("Hint", root, "Enter / Space / Click", 22);
            hint.color = new Color(1f, 1f, 1f, 0.35f);
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(800, 40));

            var skip = UiKit.Button("SkipScene", root, "Skip scene", Skip, UiKit.Tier.Quiet);
            Ui.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(-40, 40), new Vector2(260, UiKit.ButtonHeight));
            // Never keyboard-selectable: Enter must always mean "advance", never "skip".
            var nav = skip.navigation; nav.mode = Navigation.Mode.None; skip.navigation = nav;

            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.spatialBlend = 0f;
            tickClip = BuildTick();
        }

        /// <summary>
        /// Plan Task 5: a 25 ms, 640 Hz mono sine at 22,050 Hz with 5 ms attack/release ramps.
        /// Generated in code so the tick can never be missing from a build. Peak raised from the
        /// plan's 0.08 because ticks now accompany every page and must survive the music bed.
        /// </summary>
        static AudioClip BuildTick()
        {
            const int rate = 22050;
            const float freq = 640f, length = 0.025f, ramp = 0.005f, peak = 0.25f;
            int n = Mathf.RoundToInt(rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Linear ramps at both ends: an abrupt start/stop clicks on its own.
                float env = Mathf.Min(1f, Mathf.Min(t / ramp, (length - t) / ramp));
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * peak * Mathf.Max(0f, env);
            }
            var clip = AudioClip.Create("NarrativeTick", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Show <paramref name="scene"/> from its first page; <paramref name="finished"/> runs once, on completion or skip.</summary>
        public void Play(IList<NarrativePage> scene, Action finished)
        {
            pages.Clear();
            pages.AddRange(scene);
            onFinished = finished;
            pageIndex = 0;
            // Whatever the panel underneath had focused (an upgrade card, Play again) is put
            // back when the scene ends, so keyboard players land where they would have.
            var es = EventSystem.current;
            restoreSelection = es != null ? es.currentSelectedGameObject : null;
            restorePending = false;
            gameObject.SetActive(true);
            ShowPage();
        }

        /// <summary>Teardown (restart, menu return): hide without calling back and without sound.</summary>
        public void Cancel()
        {
            onFinished = null;
            tickSource.Stop();
            restorePending = false;
            gameObject.SetActive(false);
        }

        public void Skip() => Finish();

        void ShowPage()
        {
            var page = pages[pageIndex];
            speakerLabel.text = page.Speaker ?? "";
            revealed = 0f;
            lastRevealedCount = 0;
            openedFrame = Time.frameCount;
            Render();
        }

        void Finish()
        {
            if (!IsPlaying) return;
            tickSource.Stop();
            var done = onFinished;
            onFinished = null;
            gameObject.SetActive(false);
            restorePending = restoreSelection != null;
            done?.Invoke();
        }

        void LateUpdate()
        {
            // Runs on the panel's own object, so it only ticks while a scene is up; restoring
            // focus after a scene is GameRoot's job (RestoreFocusIfPending), as this is inactive then.
            if (!IsPlaying) return;
            // Under the pause menu the story is inert: no typing, no ticks, and focus belongs
            // to the menu (clearing it here would strand keyboard players in pause/confirm).
            if (Frozen) { tickSource.Stop(); return; }
            var es = EventSystem.current;
            // Nothing underneath may be focused while reading: Enter is ours. A panel that
            // grabbed focus after the scene started (the upgrade cards) gets it back at the end.
            if (es != null && es.currentSelectedGameObject != null)
            {
                // The Skip button selects itself on press; it is ours, not something to restore.
                if (!es.currentSelectedGameObject.transform.IsChildOf(transform))
                    restoreSelection = es.currentSelectedGameObject;
                es.SetSelectedGameObject(null);
            }

            string text = pages[pageIndex].Text;
            bool complete = lastRevealedCount >= text.Length;
            if (!complete)
            {
                revealed += Mathf.Min(Time.unscaledDeltaTime, MaxFrame) * CharsPerSecond;
                int count = Mathf.Min(text.Length, Mathf.FloorToInt(revealed));
                // Never split a surrogate pair: a half character renders as garbage.
                if (count > 0 && count < text.Length && char.IsHighSurrogate(text[count - 1])) count++;
                if (count > lastRevealedCount)
                {
                    // Tick on letters and digits only, throttled, so spaces and punctuation
                    // read as natural pauses in the rhythm.
                    bool audible = false;
                    for (int i = lastRevealedCount; i < count; i++) if (char.IsLetterOrDigit(text[i])) { audible = true; break; }
                    if (audible && TickVolume > 0f && Time.unscaledTime - lastTickAt >= TickGap)
                    {
                        tickSource.PlayOneShot(tickClip, TickVolume);
                        lastTickAt = Time.unscaledTime;
                    }
                    lastRevealedCount = count;
                    Render();
                }
            }

            if (!AdvancePressed()) return;
            if (lastRevealedCount < text.Length)
            {
                // First press: reveal the rest silently and stay on this page.
                lastRevealedCount = text.Length;
                revealed = text.Length;
                tickSource.Stop();
                openedFrame = Time.frameCount;
                Render();
            }
            else if (pageIndex + 1 < pages.Count) { pageIndex++; ShowPage(); }
            else Finish();
        }

        /// <summary>A fresh press of Enter, Space or the left mouse button, never on the frame the page changed.</summary>
        bool AdvancePressed()
        {
            if (Time.frameCount == openedFrame) return false;
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            // A click on the Skip button is the button's, not an advance.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var es = EventSystem.current;
                var over = es != null && es.IsPointerOverGameObject() ? PointerTarget() : null;
                return over == null || over.GetComponentInParent<Button>() == null;
            }
            return false;
        }

        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        static GameObject PointerTarget()
        {
            var es = EventSystem.current;
            var mouse = Mouse.current;
            if (es == null || mouse == null) return null;
            var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
            hits.Clear();
            es.RaycastAll(data, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        /// <summary>
        /// The whole passage is always laid out; the unrevealed suffix is transparent, so the
        /// line breaks never jump as letters appear.
        /// </summary>
        void Render()
        {
            string text = pages[pageIndex].Text;
            int n = Mathf.Clamp(lastRevealedCount, 0, text.Length);
            body.text = n >= text.Length ? text : text.Substring(0, n) + "<color=#00000000>" + text.Substring(n) + "</color>";
            hint.enabled = n >= text.Length;
        }

        /// <summary>Called by GameRoot each frame: puts focus back one frame after a scene ends.</summary>
        public void RestoreFocusIfPending()
        {
            if (!restorePending || IsPlaying) return;
            restorePending = false;
            var es = EventSystem.current;
            if (es != null && restoreSelection != null && restoreSelection.activeInHierarchy) es.SetSelectedGameObject(restoreSelection);
        }
    }
}
