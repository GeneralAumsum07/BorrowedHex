using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The gold pointer orb beside whatever has keyboard focus (plan 0.7). One per canvas; it
    /// follows EventSystem.currentSelectedGameObject and animates its five frames at 8 fps, or
    /// holds frame 0 under Reduce flashes. Without art it is a small honey diamond.
    /// </summary>
    public sealed class FocusPointer : MonoBehaviour
    {
        // How far left of the focused control the pointer reaches: the 64 px sprite plus the
        // 6 px gap. Layouts that put text left of a focusable control keep this much clear.
        public const float Reach = 70f;
        const float Gap = 6f;

        Image img; Sprite[] frames; RectTransform rt;

        public static FocusPointer Ensure(Canvas canvas)
        {
            var found = canvas.GetComponentInChildren<FocusPointer>(true);
            if (found != null) return found;
            var img = Ui.Image("FocusPointer", canvas.transform, UiPalette.Honey);
            img.raycastTarget = false;
            var p = img.gameObject.AddComponent<FocusPointer>();
            p.img = img; p.rt = img.rectTransform; p.frames = UiSkin.Frames("pointer");
            if (p.frames.Length > 0) { img.sprite = p.frames[0]; img.color = Color.white; }
            p.rt.sizeDelta = p.frames.Length > 0 ? new Vector2(64, 38) : new Vector2(16, 16);
            p.rt.pivot = new Vector2(1f, 0.5f);
            return p;
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            bool on = sel != null && sel.activeInHierarchy && sel.transform is RectTransform;
            img.enabled = on;
            if (!on) return;
            transform.SetAsLastSibling();   // drawn over the screen that owns the focus
            var target = (RectTransform)sel.transform;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            rt.position = new Vector3(corners[0].x - Gap * transform.lossyScale.x, (corners[0].y + corners[1].y) * 0.5f, 0);
            if (frames.Length > 0)
                img.sprite = DisplayOptions.ReduceFlashes ? frames[0] : frames[(int)(Time.unscaledTime * 8f) % frames.Length];
        }
    }
}
