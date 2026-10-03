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
        public static readonly Color Ink = new Color(0.96f, 0.93f, 1f);
        public static readonly Color Panel = new Color(0.07f, 0.05f, 0.12f, 0.92f);
        public static readonly Color Accent = new Color(0.98f, 0.82f, 0.31f);
        public static readonly Color ButtonFill = new Color(0.27f, 0.19f, 0.47f, 1f);

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
            t.alignment = align;
            t.color = Ink;
            t.text = text;
            // Labels never eat clicks; only buttons and panels are raycast targets.
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, int fontSize = 30)
        {
            var img = Image(name, parent, ButtonFill);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(1.2f, 1.15f, 1.05f);
            b.colors = colors;
            var label = Label("Label", img.transform, text, fontSize);
            Stretch(label.rectTransform);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

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
