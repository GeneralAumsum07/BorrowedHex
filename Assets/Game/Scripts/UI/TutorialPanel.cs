using System;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Phase 14 tutorial UI (D86): a prompt band across the top of the screen and the
    /// "Tutorial complete" card. Like the HUD and the flow panels it only POLLS the bound sim
    /// (its TutorialDirector) each frame, so a restart is one Bind call and nothing from a
    /// previous run can linger. Outside a tutorial run everything here stays hidden.
    /// </summary>
    public sealed class TutorialPanel : MonoBehaviour
    {
        ArenaSim sim;
        GameObject band;
        Text header, prompt, progress;
        GameObject cardDim;
        float lastPromptChange;
        string shownPrompt;

        /// <summary>True while the completion card is up; GameRoot keeps gameplay input off then.</summary>
        public bool CardOpen => cardDim.activeSelf;

        /// <summary>Raised when the completion card opens or closes; GameRoot covers the HUD with it.</summary>
        public event Action<bool> CardChanged;

        // Routes every card visibility change through one place so CardChanged fires on edges only.
        void SetCard(bool open)
        {
            if (cardDim.activeSelf == open) return;
            cardDim.SetActive(open);
            CardChanged?.Invoke(open);
        }

        public static TutorialPanel Create(Canvas canvas, Action onPlay, Action onMainMenu)
        {
            var root = Ui.Stretch(Ui.Rect("Tutorial", canvas.transform));
            var p = root.gameObject.AddComponent<TutorialPanel>();
            p.Build(root, onPlay, onMainMenu);
            return p;
        }

        /// <summary>The completion card's one reminder line: the spec sentence, verbatim.</summary>
        public const string CompletionReminder = "Your life drains during runs. Defeat enemies to reclaim it.";

        // The instruction is ink on parchment; it flashes from Honey when it changes.
        static readonly Color PromptInk = new Color32(0x15, 0x12, 0x1C, 0xFF);
        const float PromptFlash = 0.5f;

        /// <summary>
        /// The instruction's colour <paramref name="sincePromptChange"/> seconds after it changed:
        /// a Honey flash easing to ink over half a second, so a new step mid-fight is noticed.
        /// With Reduce flashes on it is the steady ink from the first frame. Static and pure.
        /// </summary>
        public static Color PromptColour(float sincePromptChange, bool reduceFlashes) =>
            reduceFlashes ? PromptInk : Color.Lerp(UiPalette.Honey, PromptInk, Mathf.Clamp01(sincePromptChange / PromptFlash));

        void Build(RectTransform root, Action onPlay, Action onMainMenu)
        {
            // --- Prompt plate (Task 17): a compact parchment, top-centre under the objective
            // line. It never takes raycasts: a click "through" it must still reach the arena as a
            // catch, or the capture lesson would eat the player's first clicks.
            var img = UiKit.Frame("PromptBand", root, UiKit.FrameKind.Parchment);
            img.raycastTarget = false;
            Ui.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(960, 120));
            band = img.gameObject;
            // The lesson number lives only here (Task 13 took it off the HUD).
            header = UiKit.Text("Header", band.transform, "", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            Ui.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(900, 36));
            header.color = UiPalette.Camel;
            prompt = UiKit.Text("Prompt", band.transform, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            Ui.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 36));
            prompt.color = PromptInk;
            progress = UiKit.Text("Progress", band.transform, "", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            Ui.Place(progress.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(900, 36));
            progress.color = UiPalette.Ink;
            foreach (var t in new[] { header, prompt, progress }) t.raycastTarget = false;
            band.SetActive(false);

            // --- Completion card: an ornate frame, built like the results so it reads as "the end".
            var dim = Ui.Image("TutorialComplete", root, UiPalette.Scrim);
            Ui.Stretch(dim.rectTransform);
            cardDim = dim.gameObject;
            var panel = UiKit.Frame("Panel", dim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel.rectTransform, 16);
            col.padding = new RectOffset(48, 48, 36, 36);
            Ui.Sized(UiKit.Text("Title", panel.transform, "Tutorial complete", UiFonts.Role.Heading, TextAnchor.MiddleCenter), 64);
            // One reminder line replaces the old three-line body, as the spec asks. Two lines'
            // room: at 664 px of Body the sentence wraps once.
            Ui.Sized(UiKit.Text("Body", panel.transform, CompletionReminder, UiFonts.Role.Body, TextAnchor.MiddleCenter), 80);
            Ui.Sized(UiKit.Button("Play", panel.transform, "Play a run", onPlay, UiKit.Tier.Primary), UiKit.ButtonHeight);
            Ui.Sized(UiKit.Button("MainMenu", panel.transform, "Main menu", onMainMenu, UiKit.Tier.Secondary), UiKit.ButtonHeight);
            cardDim.SetActive(false);
        }

        public void Bind(ArenaSim s)
        {
            sim = s;
            shownPrompt = null;
            band.SetActive(false);
            SetCard(false);
        }

        void LateUpdate()
        {
            var t = sim?.Tutorial;
            if (t == null)
            {
                band.SetActive(false);
                SetCard(false);
                return;
            }
            // The card replaces the band: once it is up there is nothing left to instruct.
            band.SetActive(!t.IsComplete);
            SetCard(t.IsComplete);
            if (t.IsComplete) return;

            header.text = t.Celebrating ? "Lesson complete" : $"Lesson {t.LessonNumber} of {TutorialDirector.LessonCount}";
            if (t.Prompt != shownPrompt)
            {
                shownPrompt = t.Prompt;
                lastPromptChange = Time.unscaledTime;
            }
            prompt.text = t.Prompt;
            progress.text = t.Progress;
            // A brief gold flash when the instruction changes, so a new step inside a lesson
            // ("Caught! Now fire it back") is noticed mid-fight. Unscaled: it is UI, not gameplay.
            prompt.color = PromptColour(Time.unscaledTime - lastPromptChange, DisplayOptions.ReduceFlashes);
        }
    }
}
