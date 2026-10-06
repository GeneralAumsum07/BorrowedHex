using System;
using BorrowedHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The themed building blocks every screen uses (plan Task 3). Static and code-only like Ui:
    /// each call returns live uGUI objects. Each piece picks a pixel sprite from UiSkin when the
    /// local art exists, and flat palette colour plus an Outline when it does not, so layout is
    /// identical either way and the layout tests prove both.
    /// </summary>
    public static class UiKit
    {
        public enum Tier { Primary, Secondary, Quiet, Icon }
        public enum FrameKind { Ornate, Card, CardSelected, Parchment, Tooltip }

        public const int Gap = 16;          // the spacing scale: 8, 16, 24, 32, 48
        public const int RowHeight = 56;    // one control row: 2x the 23-px plate + caption room
        public const int ButtonHeight = 64;

        static string FrameId(FrameKind k) => k switch
        {
            FrameKind.Ornate => "frame.ornate", FrameKind.Card => "frame.card",
            FrameKind.CardSelected => "frame.cardAlt", FrameKind.Parchment => "frame.parchment",
            _ => "strip.parchment",
        };

        public static Image Frame(string name, Transform parent, FrameKind kind)
        {
            var img = Ui.Image(name, parent, Color.white);
            var s = UiSkin.Sprite(FrameId(kind));
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; }
            else
            {
                // Flat fallback: the panel tone with a camel hairline standing in for the gilded edge.
                bool light = kind == FrameKind.Parchment || kind == FrameKind.Tooltip;
                img.color = light ? new Color(0.85f, 0.78f, 0.6f, 0.97f) : kind == FrameKind.Ornate ? UiPalette.PanelDeep : UiPalette.Panel;
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = kind == FrameKind.CardSelected ? UiPalette.Honey : UiPalette.Camel;
                o.effectDistance = new Vector2(2, -2);
            }
            return img;
        }

        public static Text Text(string name, Transform parent, string text, UiFonts.Role role, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Ui.Label(name, parent, text, UiFonts.Size(role), align);
            t.font = UiFonts.For(role);
            // Ui.Label snapped the size to the body grid; a display role needs its own grid back.
            t.fontSize = UiFonts.Size(role);
            t.color = role == UiFonts.Role.Heading || role == UiFonts.Role.Title || role == UiFonts.Role.Sub ? UiPalette.Honey
                : role == UiFonts.Role.Small ? UiPalette.Muted : UiPalette.Ivory;
            // Pixel faces have tall line boxes; 1.0 spacing keeps rows on the 8-unit grid.
            t.lineSpacing = 1f;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Tier tier = Tier.Secondary)
        {
            var img = Ui.Image(name, parent, Color.white);
            var b = img.gameObject.AddComponent<Button>();
            string idle = tier == Tier.Primary ? "plate.crest" : tier == Tier.Icon ? "square.dark" : "plate.dark";
            string lit = tier == Tier.Primary ? "plate.crest" : tier == Tier.Icon ? "square.crest" : "plate.darkAlt";
            var s0 = tier == Tier.Quiet ? null : UiSkin.Sprite(idle);
            if (tier == Tier.Quiet) img.color = new Color(0, 0, 0, 0);       // still catches the pointer
            else if (s0 != null)
            {
                img.sprite = s0; img.type = Image.Type.Sliced;
                // Sprite swap, not tint: a multiply tint muddies the gold leaf.
                b.transition = Selectable.Transition.SpriteSwap;
                var ss = b.spriteState;
                ss.highlightedSprite = ss.selectedSprite = UiSkin.Sprite(lit);
                ss.pressedSprite = s0; ss.disabledSprite = s0;
                b.spriteState = ss;
            }
            else
            {
                img.color = tier == Tier.Primary ? new Color(0.36f, 0.24f, 0.12f) : UiPalette.Panel;
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = tier == Tier.Primary ? UiPalette.Honey : UiPalette.Camel;
            }
            var role = UiFonts.Role.Button;
            var label = Text("Label", img.transform, text, role, TextAnchor.MiddleCenter);
            label.color = tier == Tier.Primary ? UiPalette.Honey : tier == Tier.Quiet ? UiPalette.Camel : UiPalette.Ivory;
            Ui.Stretch(label.rectTransform);
            if (tier == Tier.Quiet) img.gameObject.AddComponent<QuietUnderline>().Bind(b, label);
            // Every kit button clicks audibly; the sound is added before the action so a button
            // that destroys its own screen still plays it.
            b.onClick.AddListener(() => Presentation.Audio.GameAudio.PlayUi("button_press"));
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static Image Divider(string name, Transform parent)
        {
            var img = Ui.Image(name, parent, UiPalette.Camel);
            var s = UiSkin.Sprite("divider.a");
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; img.color = Color.white; }
            return Ui.Sized(img, s != null ? 12 : 2);
        }

        /// <summary>A labelled settings-style row: label left, control right, value text in between.</summary>
        public sealed class Row
        {
            public RectTransform Rect; public Text Label, Value; public Selectable Control;
            internal Func<string> Read;
            // Anything beyond the text that mirrors the value (the toggle's gem). Without it, a
            // row refreshed after its value changed elsewhere showed "On" beside an empty box.
            internal Action Sync;
            public void Refresh() { if (Read != null) Value.text = Read(); Sync?.Invoke(); }
        }

        static Row NewRow(Transform parent, string label)
        {
            var rt = Ui.Sized(Ui.Rect("Row_" + label, parent), RowHeight);
            var l = Text("Label", rt, label, UiFonts.Role.Body);
            l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(0.5f, 1);
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
            var v = Text("Value", rt, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            // The value ends where the focus pointer's reach begins: the control on the right is
            // what takes focus, and the pointer is drawn to its left (toggle box 56 wide; the
            // stepper's right arrow ends 54 in). Ends 128 in, 176 wide: "As launched" fits.
            Ui.Place(v.rectTransform, new Vector2(1, 0.5f), new Vector2(-(56 + FocusPointer.Reach + 2), 0), new Vector2(176, RowHeight));
            return new Row { Rect = rt, Label = l, Value = v };
        }

        public static Row ToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = NewRow(parent, label);
            row.Read = () => get() ? "On" : "Off";
            Button box = null;
            box = Button("Toggle", row.Rect, "", () => { set(!get()); row.Refresh(); }, Tier.Icon);
            Ui.Place((RectTransform)box.transform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(56, 54));
            row.Control = box;
            row.Sync = () => SetGem(box, get());
            row.Refresh();
            return row;
        }

        // The checkbox mark: a lit gem when on, nothing when off. Plus the On/Off text, so the
        // state never relies on colour or a tiny sprite alone.
        static void SetGem(Button box, bool on)
        {
            var gem = box.transform.Find("Gem") as RectTransform;
            if (gem == null)
            {
                var g = Ui.Image("Gem", box.transform, UiPalette.Honey);
                var s = UiSkin.Sprite("gem.1");
                if (s != null) { g.sprite = s; g.color = Color.white; }
                g.raycastTarget = false;
                gem = Ui.Place(g.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 26));
            }
            gem.gameObject.SetActive(on);
        }

        public static Row StepperRow(Transform parent, string label, Func<string> value, Action<int> step)
        {
            var row = NewRow(parent, label);
            row.Read = value;
            var less = Arrow("Less", row.Rect, "arrow.left", "<", () => { step(-1); row.Refresh(); });
            Ui.Place((RectTransform)less.transform, new Vector2(1, 0.5f), new Vector2(-312, 0), new Vector2(38, 36));
            var more = Arrow("More", row.Rect, "arrow.right", ">", () => { step(+1); row.Refresh(); });
            Ui.Place((RectTransform)more.transform, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(38, 36));
            row.Control = more;
            row.Refresh();
            return row;
        }

        static Button Arrow(string name, Transform parent, string spriteId, string fallback, Action onClick)
        {
            var frames = UiSkin.Frames(spriteId);
            var b = Button(name, parent, frames.Length > 0 ? "" : fallback, onClick, Tier.Icon);
            if (frames.Length == 4)
            {
                var img = b.GetComponent<Image>();
                img.sprite = frames[0]; img.type = Image.Type.Simple;
                b.transition = Selectable.Transition.SpriteSwap;
                // Frame order: idle, hover, pressed, disabled (verified on the proof sheet, Task 2 Step 6).
                b.spriteState = new SpriteState { highlightedSprite = frames[1], selectedSprite = frames[1], pressedSprite = frames[2], disabledSprite = frames[3] };
            }
            return b;
        }

        public sealed class TabStrip
        {
            public RectTransform Rect; public int Selected { get; private set; } = -1;
            /// <summary>The strip's natural width (tabs plus gaps): callers size Rect to it.</summary>
            public float Width { get; internal set; }
            internal Button[] Buttons; internal Action<int> OnSelect;
            // Fixed at creation: the label colours depend on what face they sit on.
            internal bool OnArt;
            public void Select(int i)
            {
                if (i == Selected) return;
                Selected = i;
                Paint();
                OnSelect?.Invoke(i);
            }

            /// <summary>Grey a tab out without hiding it: the player still sees the option exists.</summary>
            public void SetInteractable(int i, bool on)
            {
                Buttons[i].interactable = on;
                Paint();
            }

            void Paint()
            {
                var frames = UiSkin.Frames("tab");
                for (int k = 0; k < Buttons.Length; k++)
                {
                    bool sel = k == Selected, live = Buttons[k].interactable;
                    if (frames.Length == 4) Buttons[k].GetComponent<Image>().sprite = frames[sel ? 1 : 0];
                    var t = Buttons[k].GetComponentInChildren<Text>();
                    // The gold tab art is mid-luminance, so the light text colours vanish on it
                    // (Honey measured 1.6:1 on Camel). Ink on art, the light palette on the dark
                    // flat face. Unselected is a step lighter than selected; disabled drops far
                    // enough that it never reads as merely unselected (UiKitTests pins both).
                    Color c = OnArt ? UiPalette.Ink : sel ? UiPalette.Honey : UiPalette.Muted;
                    if (!sel && OnArt) c.a = 0.78f;
                    if (!live) c.a = OnArt ? 0.38f : 0.42f;
                    t.color = c;
                    // Selected also reads without colour: the active tab carries a pointer gem.
                    t.text = (sel ? "♦ " : "") + t.text.TrimStart('♦', ' ');
                }
            }
        }

        /// <summary>
        /// Plain face at each end of the tab art that a label must not cover. The art is
        /// asymmetric: a 12-art-px edge on the left, a 30-art-px scroll ornament on the right
        /// (measured from DwTab.png). At PPU 50 on the PPU 100 canvas one art px is two
        /// reference px; +4 keeps a glyph off the art.
        /// </summary>
        public const float TabInsetLeft = 28f, TabInsetRight = 64f;

        public static TabStrip Tabs(Transform parent, string[] labels, Action<int> onSelect)
        {
            var rt = Ui.Rect("Tabs", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            var strip = new TabStrip { Rect = rt, Buttons = new Button[labels.Length], OnArt = UiSkin.Frames("tab").Length == 4 };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = Button("Tab" + i, rt, labels[i], () => strip.Select(k), Tier.Secondary);
                // Sized for the label's widest form, the selected one with its "♦ " pointer, so
                // the text never slides under the end ornaments when it is picked. 280 stays the
                // floor: short labels keep the strip's even rhythm.
                var label = b.GetComponentInChildren<Text>();
                label.text = "♦ " + labels[i];
                // +8: sized to the exact pixel, layout rounding wrapped "♦ Achievements" onto two lines.
                float need = Mathf.Ceil(label.preferredWidth + TabInsetLeft + TabInsetRight + 8f);
                // A tab label is one line by definition; never let it wrap under the art.
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.text = labels[i];
                var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = Mathf.Max(280f, need); le.preferredHeight = 64;
                strip.Width += le.preferredWidth + (i > 0 ? h.spacing : 0f);
                var frames = UiSkin.Frames("tab");
                if (frames.Length == 4)
                {
                    var img = b.GetComponent<Image>(); img.sprite = frames[0]; img.type = Image.Type.Sliced; b.transition = Selectable.Transition.None;
                    // Centre the label on the plain face between the edge and the ornament,
                    // not on the whole tab, which would push it toward the ornament.
                    label.rectTransform.offsetMin = new Vector2(TabInsetLeft, 0);
                    label.rectTransform.offsetMax = new Vector2(-TabInsetRight, 0);
                }
                strip.Buttons[i] = b;
            }
            strip.OnSelect = onSelect;
            return strip;
        }

        /// <summary>
        /// Bounded vertical scrolling for long content (Records, Details). The viewport clips with
        /// RectMask2D (no stencil, cheap on WebGL); the caller sizes the returned content.
        /// </summary>
        public static RectTransform ScrollView(string name, Transform parent, out ScrollRect scroll)
        {
            var root = Ui.Rect(name, parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;   // one body line per wheel notch
            var viewport = Ui.Rect("Viewport", root);
            Ui.Stretch(viewport); viewport.offsetMax = new Vector2(-24, 0);   // room for the track
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var col = Ui.Column(content, 8);
            col.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;

            var track = Ui.Image("Track", root, UiPalette.PanelDeep);
            var ts = UiSkin.Sprite("scroll.track");
            if (ts != null) { track.sprite = ts; track.type = Image.Type.Sliced; track.color = Color.white; }
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = new Vector2(1, 1);
            track.rectTransform.pivot = new Vector2(1, 0.5f); track.rectTransform.sizeDelta = new Vector2(14, 0);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Ui.Image("Handle", track.transform, UiPalette.Honey);
            Ui.Stretch(handle.rectTransform);
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            root.gameObject.AddComponent<ScrollToFocus>().Bind(scroll);
            return content;
        }
    }

    /// <summary>Quiet buttons have no plate; a 2-px underline shows focus/hover instead.</summary>
    public sealed class QuietUnderline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button button; Image line; bool hover;
        public void Bind(Button b, Text label)
        {
            button = b;
            line = Ui.Image("Underline", transform, UiPalette.Camel);
            line.raycastTarget = false;
            line.rectTransform.anchorMin = new Vector2(0.2f, 0); line.rectTransform.anchorMax = new Vector2(0.8f, 0);
            line.rectTransform.sizeDelta = new Vector2(0, 2); line.rectTransform.anchoredPosition = new Vector2(0, 8);
        }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        void LateUpdate()
        {
            var es = EventSystem.current;
            line.enabled = hover || (es != null && es.currentSelectedGameObject == gameObject);
        }
    }

    /// <summary>Keyboard focus inside a scroll view scrolls the focused row into view.</summary>
    public sealed class ScrollToFocus : MonoBehaviour
    {
        ScrollRect scroll; GameObject last;
        public void Bind(ScrollRect s) => scroll = s;
        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null || sel == last || !sel.transform.IsChildOf(scroll.content)) { last = sel; return; }
            last = sel;
            Canvas.ForceUpdateCanvases();
            var view = scroll.viewport.rect.height;
            var content = scroll.content.rect.height;
            if (content <= view) return;
            var item = (RectTransform)sel.transform;
            float top = -scroll.content.InverseTransformPoint(item.TransformPoint(new Vector3(0, item.rect.yMax))).y;
            float bottom = top + item.rect.height;
            float y = scroll.content.anchoredPosition.y;
            if (top < y) y = top; else if (bottom > y + view) y = bottom - view;
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0, content - view));
        }
    }
}
