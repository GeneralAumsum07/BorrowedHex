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
        public WorldIntroOverlay()
        {
            // Below the existing HUD/menu canvas: pausing must always leave Resume usable.
            canvas = Ui.CreateCanvas("WorldIntroOverlay", 9);
            curtain = Ui.Image("Dark arrival", canvas.transform, Color.clear);
            Ui.Stretch(curtain.rectTransform); curtain.raycastTarget = false;
            title = Ui.Label("Collector reveal", canvas.transform, "", 64);
            Ui.Place(title.rectTransform, new Vector2(.5f, .72f), Vector2.zero, new Vector2(1500, 200));
            title.color = new Color(1, .83f, .56f); title.fontStyle = FontStyle.Bold; title.raycastTarget = false;
            Clear();
        }
        public void Render(float elapsed, string name, bool held, bool sanctum)
        {
            curtain.color = new Color(0, 0, 0, WorldIntroPolicy.Curtain(elapsed));
            title.text = "— BOSS —\n" + name.ToUpperInvariant();
            title.gameObject.SetActive(sanctum && WorldIntroPolicy.ShowTitle(elapsed) && !held);
        }
        public void Clear() { curtain.color = Color.clear; title.gameObject.SetActive(false); }
        public void Dispose() { if (canvas != null) WorldArtLibrary.Release(canvas.gameObject); }
    }
}
