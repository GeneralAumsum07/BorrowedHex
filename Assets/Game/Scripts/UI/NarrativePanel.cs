using System;
using System.Collections.Generic;
using BorrowedHex.Narrative;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Lore plan Task 2: plays one NarrativeScene in two layouts.
    ///
    ///  - Lore and Writing pages: a full black screen with a centred passage (Writing pages add
    ///    their source label above it). Typed silently at 35 text elements per second.
    ///  - Dialogue pages: a black box across the bottom quarter, over the frozen arena, with the
    ///    speaker's name and sprite. Typed to the dialogue tick (raised as <see cref="Ticked"/>;
    ///    DialogueTextAudio makes the sound, so this class stays free of audio).
    ///
    /// The typing rules live in <see cref="Typewriter"/>; this class only draws and reads input.
    /// The panel is a raycast target over the whole screen in BOTH layouts, so panels behind it
    /// (upgrade cards, results) can never be clicked while a scene is up. It owns no game
    /// state: GameRoot decides when a scene plays, freezes the sim, and hears how it ended.
    /// </summary>
    public sealed class NarrativePanel : MonoBehaviour
    {
        /// <summary>Panel and layout-change fades. Removed entirely by Reduce flashes.</summary>
        public const float FadeSeconds = 0.2f;
        // The crossed-out word: muted ivory, the "restrained colour" of the plan.
        const string EmphasisHex = "#9A9385";
        static readonly Color EmphasisColor = new Color(0x9A / 255f, 0x93 / 255f, 0x85 / 255f, 1f);

        // ---- Built UI (exposed read-only for the layout tests) -------------------------
        public Image Background { get; private set; }
        public Image Illustration { get; private set; }
        public RectTransform DialogueBox { get; private set; }
        public Text SpeakerLabel { get; private set; }
        public Image Portrait { get; private set; }
        /// <summary>The line drawn through an emphasised word.</summary>
        public Image Strike { get; private set; }
        /// <summary>The label currently typing: the lore body or the dialogue line.</summary>
        public Text BodyLabel => layout == PageKind.Dialogue ? lineBody : loreBody;

        Text loreBody, loreSource, loreHint, lineHint;
        CanvasGroup rootGroup, loreGroup, lineGroup;
        Func<Speaker, Sprite> portraits;

        // ---- Playback state ------------------------------------------------------------
        readonly Typewriter typer = new Typewriter();
        NarrativeScene scene;
        int pageIndex;
        PageKind layout = PageKind.Lore;
        Action<NarrativeEnd> onFinished;
        float fade = 1f;          // 0..1 progress of the current opening/layout fade
        CanvasGroup fading;       // which group the fade drives
        // Illustration fade (plan Task 6): its own clock, never gating the typing, so text
        // timing is identical with or without pictures. The key, not the sprite, decides a
        // "new picture" (two keys may share a placeholder sprite).
        string artKey;
        float artFade = 1f;
        bool pendingPress;        // SimulateAdvance (tests, accessibility hooks)
        GameObject restoreSelection;
        bool restorePending;

        /// <summary>Raised on an audible reveal of a dialogue line (never for lore, skips or instant text).</summary>
        public event Action Ticked;

        public bool IsPlaying => scene != null;
        public int PageIndex => pageIndex;
        public PageKind Layout => layout;
        public NarrativePage CurrentPage => scene != null ? scene.Pages[pageIndex] : null;
        public int RevealedElements => typer.Revealed;
        /// <summary>Set by GameRoot while the pause menu or a focus loss is on top: typing, fades and input stop.</summary>
        public bool Frozen { get; set; }
        /// <summary>Instant story text: whole passages and lines at once, in both layouts, silently.</summary>
        public bool InstantText
        {
            get => instantText;
            set
            {
                if (value == instantText) return;
                instantText = value;
                // Switched on mid-scene (pause-menu settings): the open page completes at once.
                if (value && IsPlaying) { typer.RevealAll(); Render(); }
            }
        }
        bool instantText;
        /// <summary>Reduce flashes: no panel, layout or illustration fades.</summary>
        public bool ReduceFlashes { get; set; }
        /// <summary>Optional illustration lookup (plan Task 6); null or a missing key keeps the black screen.</summary>
        public Func<string, Sprite> Illustrations { get; set; }

        public static NarrativePanel Create(Canvas canvas, Func<Speaker, Sprite> portraits)
        {
            var bg = Ui.Image("NarrativePanel", canvas.transform, Color.black);
            Ui.Stretch(bg.rectTransform);
            // Blocks clicks in both layouts (alpha 0 still raycasts): nothing behind is clickable.
            bg.raycastTarget = true;
            var p = bg.gameObject.AddComponent<NarrativePanel>();
            p.portraits = portraits;
            p.Background = bg;
            p.Build(bg.transform);
            bg.gameObject.SetActive(false);
            return p;
        }

        void Build(Transform root)
        {
            rootGroup = gameObject.AddComponent<CanvasGroup>();

            Illustration = Ui.Image("Illustration", root, Color.white);
            Ui.Stretch(Illustration.rectTransform);
            Illustration.preserveAspect = true;
            Illustration.raycastTarget = false;
            Illustration.enabled = false;

            // Lore layout: a central safe area. 1100 wide keeps a 28-word passage to about five
            // lines, clear of the edges at every supported aspect ratio and interface scale.
            var lore = Ui.Rect("Lore", root);
            Ui.Stretch(lore);
            loreGroup = lore.gameObject.AddComponent<CanvasGroup>();
            loreSource = Ui.Label("Source", lore, "", 28);
            loreSource.color = UiPalette.Honey;
            Ui.Place(loreSource.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1100, 50));
            loreBody = Ui.Label("Passage", lore, "", 40);
            loreBody.supportRichText = true;
            loreBody.lineSpacing = 1.25f;
            Ui.Place(loreBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1100, 320));
            loreHint = Ui.Label("Hint", lore, "Enter · Space · Click", 22);
            loreHint.color = new Color(1f, 1f, 1f, 0.35f);
            Ui.Place(loreHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(800, 40));

            // Dialogue layout: a black box inside the bottom quarter, the frozen arena above.
            var box = Ui.Image("DialogueBox", root, new Color(0f, 0f, 0f, 0.92f));
            box.raycastTarget = false;
            DialogueBox = box.rectTransform;
            DialogueBox.anchorMin = new Vector2(0.06f, 0.02f);
            DialogueBox.anchorMax = new Vector2(0.94f, 0.24f);
            DialogueBox.offsetMin = DialogueBox.offsetMax = Vector2.zero;
            lineGroup = box.gameObject.AddComponent<CanvasGroup>();

            Portrait = Ui.Image("Portrait", box.transform, Color.white);
            Portrait.preserveAspect = true;
            Portrait.raycastTarget = false;
            var pr = Portrait.rectTransform;
            // A square on the left, the box's full height minus a margin.
            pr.anchorMin = new Vector2(0f, 0.08f); pr.anchorMax = new Vector2(0f, 0.92f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(20f, 0f);
            pr.sizeDelta = new Vector2(150f, 0f);

            SpeakerLabel = Ui.Label("Speaker", box.transform, "", 28, TextAnchor.UpperLeft);
            SpeakerLabel.color = UiPalette.Honey;
            var sr = SpeakerLabel.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0f, 1f);
            sr.offsetMin = new Vector2(190f, -52f); sr.offsetMax = new Vector2(-24f, -12f);

            lineBody = Ui.Label("Line", box.transform, "", 34, TextAnchor.UpperLeft);
            lineBody.supportRichText = true;
            lineBody.lineSpacing = 1.15f;
            var lr = lineBody.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(190f, 40f); lr.offsetMax = new Vector2(-24f, -56f);

            lineHint = Ui.Label("Hint", box.transform, "Enter · Space · Click", 20, TextAnchor.LowerRight);
            lineHint.color = new Color(1f, 1f, 1f, 0.35f);
            var hr = lineHint.rectTransform;
            hr.anchorMin = new Vector2(1f, 0f); hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-20f, 8f); hr.sizeDelta = new Vector2(420f, 30f);

            // Skip sits top-right in both layouts: never over the name, sprite or line.
            var skip = UiKit.Button("SkipScene", root, "Skip scene", Skip, UiKit.Tier.Quiet);
            Ui.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(-30, -30), new Vector2(240, UiKit.ButtonHeight));
            // Never keyboard-selectable: Enter must always mean "advance", never "skip".
            var nav = skip.navigation; nav.mode = Navigation.Mode.None; skip.navigation = nav;

            Strike = Ui.Image("Strike", loreBody.transform, EmphasisColor);
            Strike.raycastTarget = false;
            Strike.gameObject.SetActive(false);
        }

        Text lineBody;

        // ---- Playback ------------------------------------------------------------------

        /// <summary>Show <paramref name="next"/> from its first page; <paramref name="finished"/> runs once, on completion or Skip.</summary>
        public void Play(NarrativeScene next, Action<NarrativeEnd> finished)
        {
            if (next == null || next.Pages.Count == 0) { finished?.Invoke(NarrativeEnd.Completed); return; }
            scene = next;
            onFinished = finished;
            pageIndex = 0;
            artKey = null;
            pendingPress = false;
            restoreSelection = null;
            restorePending = false;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) restoreSelection = es.currentSelectedGameObject;
            gameObject.SetActive(true);
            // The opening fade covers the whole panel; typing waits until it has finished.
            BeginFade(rootGroup);
            ShowPage();
        }

        /// <summary>Teardown (restart, menu return, replaced sim): hide at once, no callback, no sound.</summary>
        public void Cancel()
        {
            scene = null;
            onFinished = null;
            pendingPress = false;
            restorePending = false;
            gameObject.SetActive(false);
        }

        public void Skip() => Finish(NarrativeEnd.Skipped);

        /// <summary>Test/accessibility hook: one fresh advance press, handled next frame.</summary>
        public void SimulateAdvance() => pendingPress = true;

        void BeginFade(CanvasGroup group)
        {
            if (fading != null && fading != group) fading.alpha = 1f;
            fading = group;
            fade = ReduceFlashes ? 1f : 0f;
            group.alpha = fade;
        }

        void ShowPage()
        {
            var page = scene.Pages[pageIndex];
            var kind = page.Kind == PageKind.Dialogue ? PageKind.Dialogue : PageKind.Lore;
            bool changed = kind != layout;
            layout = kind;
            bool dialogue = kind == PageKind.Dialogue;

            loreGroup.gameObject.SetActive(!dialogue);
            DialogueBox.gameObject.SetActive(dialogue);
            // Lore is black; dialogue lets the frozen arena show above the box (still raycast-blocking).
            Background.color = dialogue ? new Color(0f, 0f, 0f, 0f) : Color.black;

            var art = !dialogue && page.IllustrationKey != null ? Illustrations?.Invoke(page.IllustrationKey) : null;
            Illustration.sprite = art;
            Illustration.enabled = art != null;
            string key = art != null ? page.IllustrationKey : null;
            // A changed picture fades in from black. Not on the scene's first page (the opening
            // fade already covers it) and never under Reduce flashes; pages sharing a picture
            // keep it steady.
            if (key != null && key != artKey && fade >= 1f && !ReduceFlashes) artFade = 0f;
            else if (key == null || ReduceFlashes) artFade = 1f;
            artKey = key;
            SetArtAlpha();

            loreSource.text = page.Kind == PageKind.Writing ? page.Label : "";
            SpeakerLabel.text = NarrativeCatalog.SpeakerName(page.Speaker);
            Portrait.sprite = dialogue && portraits != null ? portraits(page.Speaker) : null;
            Portrait.enabled = Portrait.sprite != null;

            // A switch between layouts inside one scene fades the incoming layout in (not the
            // whole panel, which would flash the arena); lore pages within one layout do not fade:
            // changing a page just clears the old passage and starts typing the next.
            // While the opening fade still runs it already covers the new layout.
            if (changed && fade >= 1f) BeginFade(dialogue ? lineGroup : loreGroup);

            // Ticks only for dialogue, and never in instant mode.
            typer.Begin(page.Text, Time.frameCount, dialogue && !InstantText, InstantText);
            Render();
        }

        void SetArtAlpha()
        {
            var c = Illustration.color;
            c.a = artFade;
            Illustration.color = c;
        }

        void Finish(NarrativeEnd how)
        {
            if (!IsPlaying) return;
            var done = onFinished;
            Cancel();
            restorePending = restoreSelection != null;
            done?.Invoke(how);
        }

        void LateUpdate()
        {
            // Inactive between scenes, so focus restoring is driven by GameRoot instead.
            if (!IsPlaying) return;
            // Under the pause menu the story is inert, and focus belongs to the menu.
            if (Frozen) return;

            var es = EventSystem.current;
            // Nothing underneath may be focused while reading: Enter is ours. A panel that
            // grabbed focus after the scene opened (the upgrade cards) gets it back at the end.
            if (es != null && es.currentSelectedGameObject != null)
            {
                // The Skip button selects itself on press; it is ours, not something to restore.
                if (!es.currentSelectedGameObject.transform.IsChildOf(transform))
                    restoreSelection = es.currentSelectedGameObject;
                es.SetSelectedGameObject(null);
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, Typewriter.MaxFrame);
            if (artFade < 1f)
            {
                artFade = ReduceFlashes ? 1f : Mathf.Min(1f, artFade + dt / FadeSeconds);
                SetArtAlpha();
            }
            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + dt / FadeSeconds);
                fading.alpha = fade;
            }
            // Typing starts after the opening fade (plan Task 2), and pauses during a layout fade.
            else if (typer.Tick(dt))
                Ticked?.Invoke();

            if (AdvancePressed())
            {
                var r = typer.Press(Time.frameCount);
                if (r == TypewriterPress.RevealedRest && fade < 1f) { fade = 1f; fading.alpha = 1f; }
                if (r == TypewriterPress.Advance)
                {
                    if (pageIndex + 1 < scene.Pages.Count) { pageIndex++; ShowPage(); return; }
                    Finish(NarrativeEnd.Completed);
                    return;
                }
            }
            Render();
        }

        /// <summary>A fresh press of Enter, Space or the left mouse button (the Typewriter rejects stale frames).</summary>
        bool AdvancePressed()
        {
            if (pendingPress) { pendingPress = false; return true; }
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            // A click on the Skip button is the button's, not an advance.
            return mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverButton();
        }

        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        static bool PointerOverButton()
        {
            var es = EventSystem.current;
            var mouse = Mouse.current;
            if (es == null || mouse == null) return false;
            var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
            hits.Clear();
            es.RaycastAll(data, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() != null;
        }

        // ---- Drawing -------------------------------------------------------------------

        void Render()
        {
            var page = scene.Pages[pageIndex];
            var label = BodyLabel;
            label.text = Compose(page.Text, typer.VisibleChars, page.Emphasis);
            bool complete = typer.Complete;
            loreHint.enabled = lineHint.enabled = complete;
            PlaceStrike(label, page, complete);
        }

        /// <summary>
        /// Revealed text, the emphasised word in its restrained colour, and the unrevealed rest
        /// transparent (so the layout never shifts as letters appear).
        /// </summary>
        static string Compose(string text, int visible, string emphasis)
        {
            int at = string.IsNullOrEmpty(emphasis) ? -1 : text.IndexOf(emphasis, StringComparison.Ordinal);
            var sb = new System.Text.StringBuilder(text.Length + 48);
            for (int i = 0; i < text.Length;)
            {
                // Segment boundaries: the reveal cut and the emphasis word's edges.
                int end = text.Length;
                if (visible > i && visible < end) end = visible;
                if (at >= 0)
                {
                    if (at > i && at < end) end = at;
                    int wordEnd = at + emphasis.Length;
                    if (wordEnd > i && wordEnd < end) end = wordEnd;
                }
                string seg = text.Substring(i, end - i);
                if (i >= visible) sb.Append("<color=#00000000>").Append(seg).Append("</color>");
                else if (at >= 0 && i >= at && i < at + emphasis.Length) sb.Append("<color=").Append(EmphasisHex).Append('>').Append(seg).Append("</color>");
                else sb.Append(seg);
                i = end;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Legacy uGUI Text has no strikethrough tag, so the crossing-out is a thin image placed
        /// from the text generator's own glyph positions, shown once the word is fully revealed.
        /// </summary>
        void PlaceStrike(Text label, NarrativePage page, bool complete)
        {
            int at = string.IsNullOrEmpty(page.Emphasis) ? -1 : page.Text.IndexOf(page.Emphasis, StringComparison.Ordinal);
            bool show = at >= 0 && label == loreBody && typer.VisibleChars >= at + page.Emphasis.Length;
            if (!show) { Strike.gameObject.SetActive(false); return; }

            // Lay out the PLAIN passage: colour tags never change glyph metrics, and plain text
            // keeps character indices aligned with the string.
            var rect = label.rectTransform.rect;
            var gen = new TextGenerator(page.Text.Length);
            gen.Populate(page.Text, label.GetGenerationSettings(rect.size));
            var chars = gen.characters;
            int last = at + page.Emphasis.Length - 1;
            if (chars.Count <= last) { Strike.gameObject.SetActive(false); return; }
            float ppu = label.pixelsPerUnit;
            var a = chars[at];
            var z = chars[last];
            float x0 = a.cursorPos.x / ppu, x1 = (z.cursorPos.x + z.charWidth) / ppu;
            // The line through the word sits a little below the middle of its text line.
            float lineTop = a.cursorPos.y / ppu;
            float lineHeight = label.fontSize * 1f;
            var rt = Strike.rectTransform;
            rt.anchorMin = rt.anchorMax = label.rectTransform.pivot;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x0, lineTop - lineHeight * 0.55f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(2f, label.fontSize / 12f));
            Strike.gameObject.SetActive(true);
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
