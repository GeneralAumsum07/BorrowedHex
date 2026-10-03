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

        public static TutorialPanel Create(Canvas canvas, Action onPlay, Action onMainMenu)
        {
            var root = Ui.Stretch(Ui.Rect("Tutorial", canvas.transform));
            var p = root.gameObject.AddComponent<TutorialPanel>();
            p.Build(root, onPlay, onMainMenu);
            return p;
        }

        void Build(RectTransform root, Action onPlay, Action onMainMenu)
        {
            // --- Prompt band. Placed below the HUD's top row (objective + life bar) so the two
            // never overlap, and it never takes raycasts: a click "through" it must still reach
            // the arena as a catch, or the capture lesson would eat the player's first clicks.
            var img = Ui.Image("PromptBand", root, new Color(0.05f, 0.03f, 0.1f, 0.82f));
            img.raycastTarget = false;
            Ui.Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1180, 132));
            band = img.gameObject;
            header = Ui.Label("Header", band.transform, "", 22);
            Ui.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(1140, 28));
            header.color = Ui.Accent;
            prompt = Ui.Label("Prompt", band.transform, "", 30);
            Ui.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(1120, 70));
            prompt.color = Ui.Ink;
            progress = Ui.Label("Progress", band.transform, "", 22);
            Ui.Place(progress.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(1140, 28));
            progress.color = new Color(0.75f, 0.95f, 1f);
            foreach (var t in new[] { header, prompt, progress }) t.raycastTarget = false;
            band.SetActive(false);

            // --- Completion card, built like the results panel so it reads as "the end".
            var dim = Ui.Image("TutorialComplete", root, new Color(0, 0, 0, 0.65f));
            Ui.Stretch(dim.rectTransform);
            cardDim = dim.gameObject;
            var panel = Ui.Image("Panel", dim.transform, Ui.Panel);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel.rectTransform, 14);
            col.padding = new RectOffset(44, 44, 32, 36);
            var title = Ui.Sized(Ui.Label("Title", panel.transform, "TUTORIAL COMPLETE", 52), 72);
            title.color = Ui.Accent;
            Ui.Sized(Ui.Label("Body", panel.transform,
                "You can move, dash, catch, fire from both slots and parry,\n" +
                "and you know to kill enemies before they evolve.\n" +
                "In a real run the life bar drains and every hit costs time.", 24), 110);
            Ui.Sized(Ui.Button("Play", panel.transform, "Play a run", onPlay), 72);
            Ui.Sized(Ui.Button("MainMenu", panel.transform, "Main menu", onMainMenu), 64);
            cardDim.SetActive(false);
        }

        public void Bind(ArenaSim s)
        {
            sim = s;
            shownPrompt = null;
            band.SetActive(false);
            cardDim.SetActive(false);
        }

        void LateUpdate()
        {
            var t = sim?.Tutorial;
            if (t == null)
            {
                band.SetActive(false);
                cardDim.SetActive(false);
                return;
            }
            // The card replaces the band: once it is up there is nothing left to instruct.
            band.SetActive(!t.IsComplete);
            cardDim.SetActive(t.IsComplete);
            if (t.IsComplete) return;

            header.text = t.Celebrating ? "LESSON COMPLETE" : $"LESSON {t.LessonNumber} / {TutorialDirector.LessonCount}";
            if (t.Prompt != shownPrompt)
            {
                shownPrompt = t.Prompt;
                lastPromptChange = Time.unscaledTime;
            }
            prompt.text = t.Prompt;
            progress.text = t.Progress;
            // A brief gold flash when the instruction changes, so a new step inside a lesson
            // ("Caught! Now fire it back") is noticed mid-fight. Unscaled: it is UI, not gameplay.
            float k = Mathf.Clamp01((Time.unscaledTime - lastPromptChange) / 0.5f);
            prompt.color = Color.Lerp(Ui.Accent, Ui.Ink, k);
        }
    }
}
