using System;
using BorrowedHex.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Tiny code-only uGUI kit. Every screen is built from these helpers instead of prefabs so
    /// the whole UI is diffable, reviewable text and the bootstrap never has to wire references
    /// by hand. Legacy Text + the built-in LegacyRuntime font keep the WebGL build free of a
    /// TMP essentials import.
    /// </summary>
    public static class Ui
    {
        // Aliases kept for screens not yet rebuilt; new code reads UiPalette directly.
        public static Color Ink => UiPalette.Ivory;
        public static Color Panel => UiPalette.Panel;
        public static Color Accent => UiPalette.Honey;
        public static Color ButtonFill => UiPalette.Panel;

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Overlay canvas scaled against 1080p, so layout reads the same at any window size.</summary>
        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            // Pixel art: no sub-pixel placement, or a 1-art-pixel line lands across two screen pixels.
            canvas.pixelPerfect = true;
            FocusPointer.Ensure(canvas);
            return canvas;
        }

        /// <summary>
        /// One EventSystem driven by the reader's UI action map. The module is configured while
        /// its GameObject is inactive so it never enables a second, default action asset that
        /// would also listen to the mouse.
        /// </summary>
        public static EventSystem CreateEventSystem(PlayerInputReader reader)
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var go = new GameObject("EventSystem");
            go.SetActive(false);
            var es = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.point = InputActionReference.Create(reader.UiPoint);
            module.leftClick = InputActionReference.Create(reader.UiClick);
            module.scrollWheel = InputActionReference.Create(reader.UiScroll);
            module.move = InputActionReference.Create(reader.UiNavigate);
            module.submit = InputActionReference.Create(reader.UiSubmit);
            module.cancel = InputActionReference.Create(reader.UiCancel);
            go.SetActive(true);
            return es;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor helper: anchors and pivot both at <paramref name="anchor"/>.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            // Pixel body face where imported. Sizes from older screens (22-30) snap to the
            // body role's grid so legacy text is crisp until its screen is rebuilt. UiKit.Text
            // re-asserts its own role size afterwards, so display text keeps alagard's grid.
            t.font = UiFonts.Body;
            if (UiFonts.HasPixelFonts) t.fontSize = Mathf.Max(UiFonts.NativeBody, Mathf.RoundToInt(size / (float)UiFonts.NativeBody) * UiFonts.NativeBody);
            t.alignment = align;
            t.color = Ink;
            t.text = text;
            // Labels never eat clicks; only buttons and panels are raycast targets.
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // fontSize stays for source compatibility; the kit sizes captions by role now.
        public static Button Button(string name, Transform parent, string text, Action onClick, int fontSize = 30)
            => UiKit.Button(name, parent, text, onClick, UiKit.Tier.Secondary);

        /// <summary>Vertical stack used by menus; children size themselves via LayoutElement.</summary>
        public static VerticalLayoutGroup Column(RectTransform rt, float spacing)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>
        /// Height <paramref name="t"/>'s current text needs when wrapped at <paramref name="width"/>.
        /// Takes the width explicitly because panels are often filled while still inactive, before
        /// any layout pass has given the label its real width. Settings and the divide use the same
        /// pixelsPerUnit, so the result is in layout units whether or not a canvas is found yet.
        /// </summary>
        public static float TextHeight(Text t, float width)
        {
            var settings = t.GetGenerationSettings(new Vector2(width, 0f));
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
        }

        public static T Sized<T>(T c, float height) where T : Component
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return c;
        }
    }
}
