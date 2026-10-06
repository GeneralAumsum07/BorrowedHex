using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Parchment tooltip on hover AND keyboard focus (plan 0.8), after 0.35 s so sweeping the
    /// mouse across a row of icons does not strobe. Tick(now) is public so tests drive time.
    /// </summary>
    public sealed class UiTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public const float Delay = 0.35f;
        Func<string> text; Image strip; float since = -1f; bool hover, focus;
        public Text Label { get; private set; }
        public bool Showing => strip != null && strip.gameObject.activeSelf;

        public static UiTooltip Attach(Selectable s, Func<string> text)
        {
            var t = s.gameObject.AddComponent<UiTooltip>();
            t.text = text;
            t.strip = UiKit.Frame("Tooltip", s.transform, UiKit.FrameKind.Tooltip);
            t.strip.raycastTarget = false;
            Ui.Place(t.strip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -12), new Vector2(360, 56));
            t.strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.Label = UiKit.Text("Text", t.strip.transform, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            t.Label.color = UiPalette.Ink;     // dark ink on parchment
            Ui.Stretch(t.Label.rectTransform);
            // Overrides the parent's sorting so a tooltip is never under the next row.
            var c = t.strip.gameObject.AddComponent<Canvas>(); c.overrideSorting = true; c.sortingOrder = 500;
            t.strip.gameObject.SetActive(false);
            return t;
        }

        float fitWidth;

        /// <summary>
        /// For long text (an upgrade's effect): a fixed width, the height follows the text, and
        /// the strip hangs from the owner's LEFT edge so an owner at the screen's left edge does
        /// not push it off screen the way the centred default would.
        /// </summary>
        public UiTooltip Fit(float width)
        {
            fitWidth = width;
            var rt = strip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            Label.alignment = TextAnchor.UpperLeft;
            Label.rectTransform.offsetMin = new Vector2(20f, 12f);
            Label.rectTransform.offsetMax = new Vector2(-20f, -12f);
            return this;
        }

        public void Hover(bool on, float now) { hover = on; Restart(now); }
        public void Focus(bool on, float now) { focus = on; Restart(now); }
        void Restart(float now) { since = hover || focus ? now : -1f; if (since < 0f) strip.gameObject.SetActive(false); }

        public void Tick(float now)
        {
            bool show = since >= 0f && now - since >= Delay - 1e-4f;
            if (show)
            {
                Label.text = text();
                // 40 = the 20-px side padding; 24 = 12 above and below.
                if (fitWidth > 0f) strip.rectTransform.sizeDelta = new Vector2(fitWidth, Ui.TextHeight(Label, fitWidth - 40f) + 24f);
            }
            if (strip.gameObject.activeSelf != show) strip.gameObject.SetActive(show);
        }

        void Update() => Tick(Time.unscaledTime);
        public void OnPointerEnter(PointerEventData e) => Hover(true, Time.unscaledTime);
        public void OnPointerExit(PointerEventData e) => Hover(false, Time.unscaledTime);
        public void OnSelect(BaseEventData e) => Focus(true, Time.unscaledTime);
        public void OnDeselect(BaseEventData e) => Focus(false, Time.unscaledTime);
    }
}
