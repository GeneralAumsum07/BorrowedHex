using System;
using System.Collections.Generic;
using BorrowedHex.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Lore plan Task 4: the main menu's Story screen. Lists the scenes this profile has
    /// UNLOCKED, in narrative order, for rereading; undiscovered scenes are not named (only
    /// counted), so the list never spoils what comes next, and the ending appears only after
    /// a victory reached it. Choosing a scene hands its id to GameRoot, which replays it.
    /// </summary>
    public sealed class StoryPanel : MonoBehaviour
    {
        // Six scenes at 64 + 16 fit the frame with room for the count and Back.
        const float RowStep = UiKit.RowHeight + 24f;

        RectTransform list;
        Text count, empty;
        Button back;
        readonly List<Button> rows = new List<Button>();
        readonly List<string> listed = new List<string>();
        Func<NarrativeProfileState> state;
        Action<string> onChoose;
        Action onBack;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>Scene ids on screen, top to bottom.</summary>
        public IReadOnlyList<string> Listed => listed;
        /// <summary>The first scene when there is one, else Back.</summary>
        public GameObject DefaultFocus => rows.Count > 0 ? rows[0].gameObject : back.gameObject;

        public static StoryPanel Create(Canvas canvas)
        {
            var scrim = Ui.Image("Story", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var p = scrim.gameObject.AddComponent<StoryPanel>();
            p.Build(scrim.transform);
            scrim.gameObject.SetActive(false);
            return p;
        }

        void Build(Transform root)
        {
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 760));
            var head = UiKit.Text("Heading", frame.transform, "Story", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 64));
            list = Ui.Rect("Scenes", frame.transform);
            Ui.Place(list, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(640, NarrativeCatalog.Scenes.Count * RowStep));
            empty = UiKit.Text("Empty", frame.transform, "No scenes discovered yet. Play a short run to begin.", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            Ui.Place(empty.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(760, 64));
            count = UiKit.Text("Count", frame.transform, "", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            Ui.Place(count.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(760, 40));
            back = UiKit.Button("Back", frame.transform, "Back", () => onBack?.Invoke(), UiKit.Tier.Secondary);
            Ui.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 64));
        }

        /// <summary>
        /// Open on <paramref name="profileState"/> (read on every refresh, so a discovery made
        /// since the last visit appears). <paramref name="choose"/> receives the chosen scene id.
        /// </summary>
        public void Show(Func<NarrativeProfileState> profileState, Action<string> choose, Action backAction)
        {
            state = profileState;
            onChoose = choose;
            onBack = backAction;
            gameObject.SetActive(true);
            Refresh();
            // No SetSelectedGameObject here: the ScreenStack owns focus.
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Rebuild the list from the profile.</summary>
        public void Refresh()
        {
            foreach (var b in rows) Destroy(b.gameObject);
            rows.Clear();
            listed.Clear();
            var s = state?.Invoke();
            int found = 0;
            foreach (var scene in NarrativeCatalog.Scenes)
            {
                if (s == null || !s.IsUnlocked(scene.Id)) continue;
                string id = scene.Id;
                var b = UiKit.Button("Scene_" + id, list, scene.Title, () => onChoose?.Invoke(id));
                Ui.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0, -found * RowStep), new Vector2(640, UiKit.RowHeight + 8));
                rows.Add(b);
                listed.Add(id);
                found++;
            }
            empty.gameObject.SetActive(found == 0);
            int total = NarrativeCatalog.Scenes.Count;
            count.text = found < total ? $"{found} of {total} scenes discovered" : "Every scene discovered";
        }

        /// <summary>As if the scene's row was clicked (tests, keyboard shortcuts).</summary>
        public void Choose(string id) { if (listed.Contains(id)) onChoose?.Invoke(id); }
    }
}
