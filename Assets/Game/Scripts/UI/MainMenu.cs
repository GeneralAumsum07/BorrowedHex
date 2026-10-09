using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    public struct MenuStatus { public int Mastery; public string StyleName; public int Points; public float XpFraction; }

    /// <summary>
    /// The launch screen (spec 2 / plan Task 6): three destinations in a left column so the arena
    /// and the rogue stay visible on the right. Entries are still keyed (the Phase 8 contract);
    /// MenuRouting decides which visible control each key drives, and a disabled key greys that
    /// control out rather than disappearing, so the player sees what exists.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        // The selected mode survives returning to the menu, not a restart of the game (spec):
        // static, never saved to the profile.
        public static string SelectedMode = "play_short";

        // The key tables are the source of truth; the buttons only reflect them in Refresh. That
        // is what lets one Play button serve two keys and Character serve a key with no button.
        readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();
        readonly Dictionary<string, bool> enabled = new Dictionary<string, bool>();
        Button play, character, records, story, training, settings, cheats, quit;
        UiKit.TabStrip modes;
        Text statusLine, warning, cheatNotice, badge, title;
        Image xpFill;
        Outline titleGlow;
        TrainingMenu trainingMenu;

        public bool IsOpen => gameObject.activeSelf;
        public GameObject DefaultFocus => play.gameObject;
        /// <summary>The Training sub-menu, so GameRoot can push it on the screen stack.</summary>
        public TrainingMenu Training => trainingMenu;
        public event Action OpenTraining;

        public static MainMenu Create(Canvas canvas)
        {
            var root = Ui.Rect("MainMenu", canvas.transform);
            Ui.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();   // the ScreenStack fades it in
            var menu = root.gameObject.AddComponent<MainMenu>();
            menu.Build(root, canvas);
            return menu;
        }

        void Build(RectTransform root, Canvas canvas)
        {
            // Readability scrim on the left only: the right half stays the living arena. It still
            // catches raycasts, so a click beside the column never reaches the backdrop run.
            var scrim = root.gameObject.AddComponent<RawImage>();
            scrim.texture = LeftScrim(); scrim.raycastTarget = true;
            Atmosphere.Create(root);   // embers and vignette over the arena, under the column

            // BYOG 2026 game-jam mark: small, top-right, over the living arena, out of the
            // column's way. Decorative only (no raycasts), and absent if the texture is missing.
            var byog = Resources.Load<Texture2D>("Branding/BYOG26");
            if (byog != null)
            {
                var badgeImg = Ui.Rect("JamBadge", root).gameObject.AddComponent<RawImage>();
                badgeImg.texture = byog; badgeImg.raycastTarget = false;
                // 440x210 source shown at 264x126: about a seventh of the screen's width.
                // Bottom-right above Quit: the top-right corner already holds the Tutorial button.
                Ui.Place(badgeImg.rectTransform, new Vector2(1, 0), new Vector2(-48, 110), new Vector2(264, 126));
            }

            // The column is anchored to the left edge and stretched vertically, so it keeps its
            // 128 px margin at every aspect ratio while the arena gets whatever width is left.
            var col = Ui.Rect("Column", root);
            col.anchorMin = new Vector2(0, 0); col.anchorMax = new Vector2(0, 1); col.pivot = new Vector2(0, 1);
            col.sizeDelta = new Vector2(640, 0); col.anchoredPosition = new Vector2(128, 0);

            title = UiKit.Text("Title", col, "Borrowed Hex", UiFonts.Role.Title);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, -96), new Vector2(640, 112));
            // Outline, not a second text: one draw, and Update only touches its alpha.
            titleGlow = title.gameObject.AddComponent<Outline>();
            titleGlow.effectDistance = new Vector2(2, -2);
            // divider.b, not filigree.a: the filigree is a 64x64 corner frame and smears when
            // stretched to a rule (Task 6 ruling).
            var flourish = Ui.Image("Flourish", col, UiPalette.Camel);
            var fs = UiSkin.Sprite("divider.b");
            if (fs != null) { flourish.sprite = fs; flourish.type = Image.Type.Sliced; flourish.color = Color.white; }
            Ui.Place(flourish.rectTransform, new Vector2(0, 1), new Vector2(0, -216), new Vector2(420, fs != null ? 12 : 2));

            modes = UiKit.Tabs(col, new[] { "Short run", "Endless" }, i => { SelectedMode = MenuRouting.ModeKeys[i]; RefreshPlay(); });
            Ui.Place(modes.Rect, new Vector2(0, 1), new Vector2(0, -288), new Vector2(modes.Width, 64));

            // Play launches whichever mode the tabs hold: two keys, one button.
            play = UiKit.Button("Play", col, "Play", () => Press(SelectedMode), UiKit.Tier.Primary);
            Ui.Place((RectTransform)play.transform, new Vector2(0, 1), new Vector2(0, -376), new Vector2(560, 80));
            // Right under Play, where the player decides to start: a cheated run counts for nothing.
            cheatNotice = UiKit.Text("CheatNotice", col, "", UiFonts.Role.Body);
            cheatNotice.color = UiPalette.Warning;
            Ui.Place(cheatNotice.rectTransform, new Vector2(0, 1), new Vector2(0, -464), new Vector2(560, 40));

            character = UiKit.Button("Character", col, "Character", () => Press("mastery"));
            Ui.Place((RectTransform)character.transform, new Vector2(0, 1), new Vector2(0, -512), new Vector2(560, 64));
            // Unspent points pull the player toward the tree without a modal nag.
            badge = UiKit.Text("Badge", character.transform, "", UiFonts.Role.Body, TextAnchor.MiddleRight);
            badge.color = UiPalette.Honey;
            Ui.Place(badge.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(120, 48));
            records = UiKit.Button("Records", col, "Records", () => Press("records"));
            // Lore plan Task 4: Story shares the Records row (both "look back" entries). A row of
            // its own pushed the column into the Cheats button at the 130% interface scale.
            Ui.Place((RectTransform)records.transform, new Vector2(0, 1), new Vector2(0, -592), new Vector2(272, 64));
            story = UiKit.Button("Story", col, "Story", () => Press("story"));
            Ui.Place((RectTransform)story.transform, new Vector2(0, 1), new Vector2(288, -592), new Vector2(272, 64));

            statusLine = UiKit.Text("Status", col, "", UiFonts.Role.Small);
            Ui.Place(statusLine.rectTransform, new Vector2(0, 1), new Vector2(0, -688), new Vector2(560, 40));
            var tray = Ui.Image("Xp", col, UiPalette.PanelDeep);
            var trayS = UiSkin.Sprite("bar.tray");
            if (trayS != null) { tray.sprite = trayS; tray.type = Image.Type.Sliced; tray.color = Color.white; }
            Ui.Place(tray.rectTransform, new Vector2(0, 1), new Vector2(0, -736), new Vector2(320, 26));
            xpFill = Ui.Image("Fill", tray.transform, UiPalette.Violet);
            var fillS = UiSkin.Sprite("fill.green");
            if (fillS != null) { xpFill.sprite = fillS; xpFill.type = Image.Type.Sliced; xpFill.color = Color.white; }
            // Anchored on the left with anchorMax.x as the fraction: no layout pass, no rounding drift.
            xpFill.rectTransform.anchorMin = new Vector2(0, 0); xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.offsetMin = new Vector2(16, 9); xpFill.rectTransform.offsetMax = new Vector2(-16, -9);

            training = UiKit.Button("Training", root, "Training", () => OpenTraining?.Invoke(), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)training.transform, new Vector2(1, 1), new Vector2(-136, -40), new Vector2(240, 64));
            settings = UiKit.Button("Settings", root, "", () => Press("settings"), UiKit.Tier.Icon);
            var gear = UiSkin.Frames("btn.options");
            if (gear.Length == 4)
            {
                var img = settings.GetComponent<Image>(); img.sprite = gear[0]; img.type = Image.Type.Simple;
                settings.transition = Selectable.Transition.SpriteSwap;
                settings.spriteState = new SpriteState { highlightedSprite = gear[1], selectedSprite = gear[1], pressedSprite = gear[2], disabledSprite = gear[3] };
            }
            else settings.GetComponentInChildren<Text>().text = "*";   // flat fallback still reads as a control
            Ui.Place((RectTransform)settings.transform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(64, 64));
            // An icon alone is a guess; the tooltip names it on hover and on keyboard focus.
            UiTooltip.Attach(settings, () => "Settings");

            cheats = UiKit.Button("Cheats", root, "Cheats", () => Press("cheats"), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)cheats.transform, new Vector2(0, 0), new Vector2(128, 48), new Vector2(200, 56));
            quit = UiKit.Button("Quit", root, "Quit", () => Press("quit"), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)quit.transform, new Vector2(1, 0), new Vector2(-128, 48), new Vector2(200, 56));
            quit.gameObject.SetActive(false);   // shown once AddEntry("quit") arrives (never on WebGL)
            // Save failures only, on their own line (spec): never merged with the cheat notice.
            warning = UiKit.Text("Warning", root, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            warning.color = UiPalette.Warning;
            Ui.Place(warning.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(1000, 64));

            trainingMenu = TrainingMenu.Create(canvas, k => Press(k));
            Refresh();
        }

        static Texture2D LeftScrim()
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
            {
                // Opaque ink to 30%, stepping down to clear by 55%: hard steps, pixel style,
                // rather than a smooth ramp that would read as a modern blur.
                float u = x / 63f, a = u < 0.3f ? 0.86f : u > 0.55f ? 0f : Mathf.Floor((0.55f - u) / 0.25f * 4f) / 4f * 0.86f;
                t.SetPixel(x, 0, new Color(UiPalette.Ink.r, UiPalette.Ink.g, UiPalette.Ink.b, a));
            }
            t.Apply(false);
            return t;
        }

        /// <summary>
        /// Register a key. <paramref name="onClick"/> null = visibly disabled. The label is no
        /// longer shown (each slot has its own fixed label), and the disabled reason is accepted
        /// for the old callers but not displayed: every key is enabled by the time the menu shows.
        /// </summary>
        public Button AddEntry(string key, string label, Action onClick, string disabledReason = null)
        {
            actions[key] = onClick;
            if (key == "quit") quit.gameObject.SetActive(true);
            SetEntry(key, onClick != null, disabledReason);
            return ControlFor(key);
        }

        public void SetEntry(string key, bool on, string disabledReason = null)
        {
            enabled[key] = on;
            Refresh();
        }

        /// <summary>Give a (disabled) entry its action and switch it on.</summary>
        public void EnableEntry(string key, Action onClick) { actions[key] = onClick; SetEntry(key, onClick != null); }

        /// <summary>Invoke an entry as if clicked (tests, keyboard shortcuts); a disabled entry does nothing.</summary>
        public void Press(string key)
        {
            if (IsEntryEnabled(key) && actions.TryGetValue(key, out var a)) a?.Invoke();
        }

        public bool IsEntryEnabled(string key) => enabled.TryGetValue(key, out var on) && on;

        Button ControlFor(string key) => MenuRouting.SlotFor(key) switch
        {
            MenuSlot.Mode => play, MenuSlot.Character => character, MenuSlot.Records => records, MenuSlot.Story => story,
            MenuSlot.Training => training, MenuSlot.Settings => settings, MenuSlot.Cheats => cheats,
            MenuSlot.Quit => quit, _ => null,
        };

        void Refresh()
        {
            if (play == null) return;
            // A disabled mode's tab greys out but stays visible, so the player knows it exists.
            for (int i = 0; i < MenuRouting.ModeKeys.Length; i++)
                modes.SetInteractable(i, IsEntryEnabled(MenuRouting.ModeKeys[i]));
            // Never leave Play pointing at a mode that cannot start.
            if (!IsEntryEnabled(SelectedMode)) SelectedMode = MenuRouting.ModeKeys[0];
            modes.Select(Array.IndexOf(MenuRouting.ModeKeys, SelectedMode));
            RefreshPlay();
            character.interactable = IsEntryEnabled("mastery");
            records.interactable = IsEntryEnabled("records");
            story.interactable = IsEntryEnabled("story");
            // Training opens while either of its entries works; the sub-menu greys the other.
            training.interactable = IsEntryEnabled("tutorial") || IsEntryEnabled("practice");
            settings.interactable = IsEntryEnabled("settings");
            cheats.interactable = IsEntryEnabled("cheats");
            trainingMenu.SetEnabled(IsEntryEnabled("tutorial"), IsEntryEnabled("practice"));
        }

        void RefreshPlay() { if (play != null) play.interactable = IsEntryEnabled(SelectedMode); }

        public void SetStatus(MenuStatus s)
        {
            statusLine.text = $"Mastery {s.Mastery}  ·  {s.StyleName}";
            badge.text = s.Points > 0 ? $"◆ {s.Points}" : "";
            xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.XpFraction), 1);
        }

        /// <summary>Kept for GameRoot's existing call; the status block replaces the old free-text line.</summary>
        public void SetProfileLine(string s) { }
        public void SetWarning(string s) => warning.text = s ?? "";
        public void SetCheatNotice(bool on) => cheatNotice.text = on ? "Cheats active — progression disabled" : "";

        public void Show(bool on) => gameObject.SetActive(on);   // focus: ScreenStack (Task 4)

        void Update()
        {
            // Unscaled: the menu breathes while the backdrop run is paused or slowed.
            var c = UiPalette.Violet; c.a = Atmosphere.TitleGlow(Time.unscaledTime);
            titleGlow.effectColor = c;
        }
    }
}
