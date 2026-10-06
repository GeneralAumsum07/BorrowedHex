using BorrowedHex.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Owned cinematic overlay; existing flow/menu code is left with its owner.</summary>
    public sealed class WorldIntroOverlay : System.IDisposable
    {
        readonly Canvas canvas;
        readonly Image curtain;
        readonly Text title;
        public bool TitleVisible => title.gameObject.activeSelf;
        public float Blackness => curtain.color.a;

        /// <summary>
        /// True on any frame the cinematic is showing the boss name (Task 17). The flow banner
        /// reads it to stand down, so the name is never on screen twice. Static because the
        /// overlay is owned by WorldPresentation, which this redesign may not edit to pass a
        /// reference across; there is only ever one boss intro on screen at a time.
        /// </summary>
        public static bool TitleShowing { get; private set; }

        public WorldIntroOverlay()
        {
            // Below the existing HUD/menu canvas: pausing must always leave Resume usable.
            canvas = Ui.CreateCanvas("WorldIntroOverlay", 9);
            curtain = Ui.Image("Dark arrival", canvas.transform, Color.clear);
            Ui.Stretch(curtain.rectTransform); curtain.raycastTarget = false;
            title = Ui.Label("Collector reveal", canvas.transform, "", 96);
            // The display face at its 6x native size, so the cinematic name matches the banner's
            // Title role instead of the legacy bold sans.
            title.font = UiFonts.Display;
            Ui.Place(title.rectTransform, new Vector2(.5f, .72f), Vector2.zero, new Vector2(1500, 200));
            title.color = UiPalette.Honey; title.raycastTarget = false;
            Clear();
        }
        public void Render(float elapsed, string name, bool held, bool sanctum)
        {
            curtain.color = new Color(0, 0, 0, WorldIntroPolicy.Curtain(elapsed));
            // The name only: "— BOSS —" was a second label saying what the arrival already shows.
            title.text = name;
            title.gameObject.SetActive(sanctum && WorldIntroPolicy.ShowTitle(elapsed) && !held);
            TitleShowing = title.gameObject.activeSelf;
        }
        public void Clear() { curtain.color = Color.clear; title.gameObject.SetActive(false); TitleShowing = false; }
        public void Dispose() { TitleShowing = false; if (canvas != null) WorldArtLibrary.Release(canvas.gameObject); }
    }
}
