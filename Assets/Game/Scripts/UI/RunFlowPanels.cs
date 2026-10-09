using System;
using BorrowedHex.Core;
using BorrowedHex.Presentation.WorldArt;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Run-flow screens: the encounter upgrade choice (Phase 7), the boss name banner,
    /// and the results panel. Like the HUD, it only POLLS the bound sim's State each frame,
    /// so a restart is one Bind call and no panel can be left showing a previous run.
    /// All animation runs on unscaled real time: the gameplay clock is frozen while these show.
    /// </summary>
    public sealed class RunFlowPanels : MonoBehaviour
    {
        ArenaSim sim;

        GameObject upgradeDim;
        Text upgradeTitle;
        // Life summary (Task 15, where the flavour line was): the bar with the purchase's share
        // drawn faded, and "Life 142 → 124 (−18, 10%)" beside it.
        RectTransform lifeRow;
        Image lifeTray, lifeFill, lifeCost;
        Text lifeText;
        // Held row: one chip per held upgrade plus "Held n/m". The chips replace the old
        // "Holding: a, b" line, so each carries a tooltip with the name, rank and effect.
        RectTransform heldRow;
        Text upgradeHolding;
        readonly System.Collections.Generic.List<Image> heldChips = new System.Collections.Generic.List<Image>();
        readonly System.Collections.Generic.List<string> heldTips = new System.Collections.Generic.List<string>();
        Button retireButton;
        Button continueButton;
        GameObject footer;   // the layout's place-holder for the bottom row (Continue / Retire / Back)
        Action<int, int> choose;
        // One row per offer slot: glyph, name, effect, plus its own buttons (D96). Offers are
        // re-read from the sim each time the panel opens, so the cards never show a previous draw.
        readonly CardView[] cards = new CardView[3];
        readonly Button[] cardMain = new Button[3];   // Take / Add / Rank up (paid)
        readonly Button[] cardSwap = new Button[3];   // Swap (free)

        // R13d swap-target step: "Replace an upgrade", the incoming card, then one card-shaped
        // button per held upgrade and Back. Three targets cover every legal swap: swaps stop once
        // maxHeld (4) is reached, so at most three are held when one is offered. A larger
        // maxHeld hides Swap past three.
        const int MaxSwapTargets = 3;

        // Panel and card geometry, shared by Build (the rects) and the fills (the heights).
        // 1100 wide (was 900): the paid label at the kit's Button size needs ~380 px ("Rank up to
        // 3  ·  9999 Life" measured 379), and at 900 that left the effect text 388 px, which
        // made the tallest draw (Fusion, rank 3) ~1000 px. At 1100 the text column is 588.
        const float PanelWidth = 1100f, PanelPad = 32f;
        const float Inner = PanelWidth - 2 * PanelPad;
        const float CardPad = 16f, CardTop = 12f, ActionWidth = 400f, GlyphSize = 48f, NameHeight = 48f, LockedHeight = 36f;   // 36 = the Small face's line box (m6x11plus)
        const float CardTextWidth = Inner - CardPad - UiKit.Gap - ActionWidth - CardPad;
        // Two 64-px buttons, an 8 gap and 12 margins: the floor for a card carrying Main + Swap.
        const float CardMinHeight = 2 * UiKit.ButtonHeight + 8f + 2 * CardTop;
        const float ChipSize = 40f, ChipGap = 8f;
        Text swapTitle;
        CardView incoming;
        readonly Button[] swapTargets = new Button[MaxSwapTargets];
        readonly CardView[] swapViews = new CardView[MaxSwapTargets];
        Button swapBack;
        int swapCard = -1;   // the offer being swapped in while the target row shows, else -1
        float upgradeOpenedAt = -1f;   // unscaled time the current choice opened (PaidClickGuard)

        // Unscaled time: the sim clock is paused for the whole choice, so game time never moves.
        bool PaidArmed => upgradeOpenedAt >= 0f && Time.unscaledTime - upgradeOpenedAt >= PaidClickGuard;

        RectTransform banner;
        CanvasGroup bannerGroup;
        Text bannerName;
        // Whether the banner is in its show/fade lifetime, separate from the band's activeSelf:
        // the band can be hidden for a frame (the cinematic owns the name) while still live.
        bool bannerLive;
        RectTransform upgradePanel;
        float bannerShownAt = -1f;
        bool bannerWasIntro;

        GameObject resultsDim;
        Text resultsTitle, resultsSubtitle, resultsRecall;
        string recall;

        /// <summary>
        /// Lore plan section G: one muted line under the title after a story run ends in death
        /// or time expiry (null hides it). Text only, never a scene, so Play again is as
        /// immediate as ever. GameRoot sets it from RunEnded and clears it at every new run.
        /// </summary>
        public string Recall
        {
            get => recall;
            set
            {
                recall = value;
                // Live as well as at FillResults: RunEnded and the results opening share a frame,
                // and either may come first.
                if (resultsRecall != null) ShowRecall();
            }
        }
        public Text RecallLabel => resultsRecall;
        Text scoreValue, durationValue, killsValue;   // the Headline's three stat blocks
        Text resultsBody;                             // full statistics, inside the collapsed Details
        RectTransform progress;                       // XP bar, XP line, level-up, rewards
        Image xpFill;
        GameObject xpRow;
        Text xpText, levelUpText, notRecordedText, warningText;
        readonly System.Collections.Generic.List<Text> rewardRows = new System.Collections.Generic.List<Text>();
        GameObject detailsView;
        Button detailsButton;
        Button againButton;
        RunSummary shownSummary;
        ResultsOutcome outcome;   // the last finalize, regrouped by ResultsCopy

        /// <summary>Seconds the boss banner holds the frozen frame before the fight starts.</summary>
        public const float BannerHold = 2.4f;

        /// <summary>
        /// Seconds after the upgrade choice opens during which the paid buttons ignore clicks.
        /// The choice opens mid-fight, often under a held or freshly pressed left button (aim /
        /// fire); without a guard that click lands on whatever card is under the cursor and
        /// spends life the player never meant to spend (final review, Important 2). Free
        /// actions (Continue, Swap, Back) are not guarded: a stray click on them costs nothing.
        /// </summary>
        public const float PaidClickGuard = 0.35f;
        const float BannerFade = 0.6f;

        public static RunFlowPanels Create(Canvas canvas, Action<int, int> onChoose, Action onContinue, Action onPlayAgain, Action onMainMenu, Action onRetire = null)
        {
            var root = Ui.Stretch(Ui.Rect("RunFlow", canvas.transform));
            var p = root.gameObject.AddComponent<RunFlowPanels>();
            p.Build(root, onChoose, onContinue, onPlayAgain, onMainMenu, onRetire);
            return p;
        }

        void Build(RectTransform root, Action<int, int> onChoose, Action onContinue, Action onPlayAgain, Action onMainMenu, Action onRetire)
        {
            // --- Upgrade choice (D96): each card has its own buttons (pay to take/add/rank up,
            // or swap for free), and "Continue without an upgrade" is always there, for free.
            choose = onChoose;
            var dim = Ui.Image("UpgradeChoice", root, UiPalette.Scrim);
            Ui.Stretch(dim.rectTransform);
            upgradeDim = dim.gameObject;
            var panel = UiKit.Frame("Panel", dim.transform, UiKit.FrameKind.Ornate);
            // Anchored by its TOP edge, 104 px down: the HUD's Life bar and heart stay showing
            // during the choice and end at ~88, and the panel grows downward from there. Centred,
            // the tallest draw (Fusion, rank 3, Endless) would push its top into the Life bar.
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(PanelWidth, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            upgradePanel = panel.rectTransform;
            var col = Ui.Column(panel.rectTransform, 12);
            col.padding = new RectOffset((int)PanelPad, (int)PanelPad, 28, 28);
            upgradeTitle = Ui.Sized(UiKit.Text("Title", panel.transform, "", UiFonts.Role.Heading, TextAnchor.MiddleCenter), 56);

            // Life summary row. The bar is the HUD's own builder (bar.trayDark at scale 4, 28 tall,
            // not the brief's 24: the art is 7 px tall, and only whole scales keep it crisp; the
            // same ruling as Task 13). Its width is set per opening, to the space the text leaves.
            lifeRow = Ui.Sized(Ui.Rect("Life", panel.transform), 40);
            (lifeTray, lifeFill) = GameplayHud.Bar("Bar", lifeRow, Vector2.zero, 400, 4, "fill.red");
            lifeFill.color = UiPalette.Blood;
            // The share a paid pick would take, drawn over the trough from After to Now, so the
            // solid fill is what is LEFT and the faded part is what the click spends.
            lifeCost = Ui.Image("Cost", lifeFill.transform.parent, new Color(UiPalette.Blood.r, UiPalette.Blood.g, UiPalette.Blood.b, 0.4f));
            lifeCost.raycastTarget = false;
            lifeText = UiKit.Text("Text", lifeRow, "", UiFonts.Role.Body, TextAnchor.MiddleRight);
            lifeText.horizontalOverflow = HorizontalWrapMode.Overflow;   // one line, sized to fit in FillLife

            // Held row: chips are created on demand (maxHeld comes from the config, which Build
            // has not seen yet), then "Held n/m" after them.
            heldRow = Ui.Sized(Ui.Rect("Held", panel.transform), ChipSize + 4);
            upgradeHolding = UiKit.Text("Holding", heldRow, "", UiFonts.Role.Body);

            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;   // captured per card; the loop variable would be 3 for all of them
                var frame = UiKit.Frame($"Card{i}", panel.transform, UiKit.FrameKind.Card);
                Ui.Sized(frame, CardMinHeight);
                cards[i] = NewCard(frame, withLock: true);
                // Paid: guarded against the click that was already in flight when the panel opened.
                cardMain[i] = UiKit.Button("Main", frame.transform, "", () => { if (PaidArmed) choose?.Invoke(index, -1); }, UiKit.Tier.Primary);
                cardSwap[i] = UiKit.Button("Swap", frame.transform, "Swap (free)", () => OnSwap(index), UiKit.Tier.Secondary);
            }

            // Swap step (R13d), hidden until a Swap with 2+ held asks which card goes.
            swapTitle = Ui.Sized(UiKit.Text("SwapTitle", panel.transform, "Replace an upgrade", UiFonts.Role.Heading, TextAnchor.MiddleCenter), 56);
            // The incoming upgrade, shown once, as a card that takes no input: the player compares
            // it against the targets under it without being able to "buy" it by accident.
            var inFrame = UiKit.Frame("Incoming", panel.transform, UiKit.FrameKind.CardSelected);
            incoming = NewCard(inFrame, withLock: false);
            incoming.Side = SideCaption(inFrame.transform, "Incoming  ·  free");
            for (int j = 0; j < swapTargets.Length; j++)
            {
                int target = j;
                // A whole card is the button: the name and effect ARE the label, so the player
                // reads what they lose on the thing they click.
                var frame = UiKit.Frame($"Replace{j}", panel.transform, UiKit.FrameKind.Card);
                var b = frame.gameObject.AddComponent<Button>();
                var lit = UiSkin.Sprite("frame.cardAlt");
                if (frame.sprite != null && lit != null)
                {
                    // Sprite swap like the kit's buttons: the selected card gets the gilded frame.
                    b.transition = Selectable.Transition.SpriteSwap;
                    var ss = b.spriteState;
                    ss.highlightedSprite = ss.selectedSprite = lit;
                    ss.pressedSprite = ss.disabledSprite = frame.sprite;
                    b.spriteState = ss;
                }
                b.onClick.AddListener(() => { if (swapCard >= 0) choose?.Invoke(swapCard, target); });
                swapTargets[j] = b;
                swapViews[j] = NewCard(frame, withLock: false);
                swapViews[j].Side = SideCaption(frame.transform, "Replace");
            }

            // The bottom row. Continue, Retire and Back sit OUTSIDE the column (ignoreLayout) so
            // Continue can be a normal-width button with Retire beside it; the Footer spacer keeps
            // their row's height in the column so the panel still sizes itself around them.
            footer = Ui.Sized(Ui.Rect("Footer", panel.transform), UiKit.ButtonHeight).gameObject;
            swapBack = FooterButton(UiKit.Button("Back", panel.transform, "Back", () => { ShowSwapTargets(-1); FillCards(); }), new Vector2(0.5f, 0f), Vector2.zero, 320);
            // R13k: the free choice is the default focus, so a stray Enter never spends life.
            continueButton = FooterButton(UiKit.Button("Continue", panel.transform, "Continue", () => onContinue?.Invoke()), new Vector2(0.5f, 0f), Vector2.zero, 320);
            // Endless only (section 6): retiring is offered between waves, i.e. here. Quiet and
            // off to the side, so it is never the default focus nor the obvious next click.
            retireButton = FooterButton(UiKit.Button("Retire", panel.transform, "Retire run", () => onRetire?.Invoke(), UiKit.Tier.Quiet), new Vector2(1f, 0f), new Vector2(-PanelPad, 0), 240);
            UiTooltip.Attach(retireButton, () => "End the run here and keep its progress").Fit(420);
            ShowSwapTargets(-1);
            upgradeDim.SetActive(false);

            // --- Boss banner (Task 17): a 140-tall band across the upper third carrying the name
            // once, in the Title face, between two divider ornaments. The "— BOSS —" label and the
            // sub-line are gone: the name alone says it, and the cinematic says it too.
            var band = Ui.Image("BossBanner", root, new Color(0.05f, 0.02f, 0.08f, 0.88f));
            banner = band.rectTransform;
            banner.anchorMin = new Vector2(0f, 0.5f);
            banner.anchorMax = new Vector2(1f, 0.5f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition = new Vector2(0f, 150f);
            banner.sizeDelta = new Vector2(0f, 140f);
            band.raycastTarget = false;
            bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var top = UiKit.Divider("DividerTop", band.transform);
            Ui.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(720, 12));
            bannerName = UiKit.Text("Name", band.transform, "", UiFonts.Role.Title, TextAnchor.MiddleCenter);
            Ui.Place(bannerName.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 104));
            bannerName.color = UiPalette.Honey;
            bannerName.horizontalOverflow = HorizontalWrapMode.Overflow;
            var bottom = UiKit.Divider("DividerBottom", band.transform);
            Ui.Place(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(720, 12));
            foreach (var g in band.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            band.gameObject.SetActive(false);

            BuildResults(root, onPlayAgain, onMainMenu);
        }

        public void Bind(ArenaSim s)
        {
            sim = s;
            shownSummary = null;
            bannerShownAt = -1f;
            bannerWasIntro = false;
            bannerLive = false;
            banner.gameObject.SetActive(false);
            upgradeDim.SetActive(false);
            resultsDim.SetActive(false);
            SetOutcome(default);
        }

        // ---- Results ---------------------------------------------------------------------------
        // Task 16 (spec 2, Results): outcome, then the headline stats, then XP and rewards; the
        // full statistics fold into Details. A 960-wide ornate frame that fits its content.

        const float ResultsWidth = 960f, ResultsPad = 48f, ResultsInner = ResultsWidth - 2 * ResultsPad;
        const float DetailsHeight = 360f, DetailsPad = 24f;

        void BuildResults(RectTransform root, Action onPlayAgain, Action onMainMenu)
        {
            var rdim = Ui.Image("Results", root, UiPalette.Scrim);
            Ui.Stretch(rdim.rectTransform);
            resultsDim = rdim.gameObject;
            var rp = UiKit.Frame("Panel", rdim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(rp.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ResultsWidth, 0));
            rp.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rcol = Ui.Column(rp.rectTransform, 16);
            rcol.padding = new RectOffset((int)ResultsPad, (int)ResultsPad, 36, 36);

            resultsTitle = Ui.Sized(UiKit.Text("Title", rp.transform, "", UiFonts.Role.Title, TextAnchor.MiddleCenter), 104);
            resultsSubtitle = Ui.Sized(UiKit.Text("Subtitle", rp.transform, "", UiFonts.Role.Sub, TextAnchor.MiddleCenter), 40);
            resultsRecall = UiKit.Text("Recall", rp.transform, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            resultsRecall.color = UiPalette.Muted;
            resultsRecall.gameObject.SetActive(false);

            // D97: the outcome reads in a glance. Three labelled blocks, value over label, so the
            // numbers are never a run-on "1234 · 3:20 · 40 kills" line the eye has to parse.
            var headline = Ui.Sized(Ui.Rect("Headline", rp.transform), 104);
            scoreValue = StatBlock(headline, 0, "Score");
            durationValue = StatBlock(headline, 1, "Duration");
            killsValue = StatBlock(headline, 2, "Kills");

            // Progress: its own column, so its height is exactly what it holds (the gap rule the
            // layout test protects: no fixed box with dead space under the text).
            progress = Ui.Rect("Progress", rp.transform);
            Ui.Column(progress, 8);
            xpRow = Ui.Sized(Ui.Rect("XpRow", progress), 28).gameObject;
            // The HUD's bar builder: bar.trayDark at scale 4 (the only tray it draws) with the green fill.
            var (xpTray, fill) = GameplayHud.Bar("XpBar", xpRow.transform, Vector2.zero, 480, 4, "fill.green");
            Ui.Place(xpTray.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 28));
            xpFill = fill;
            if (UiSkin.Sprite("fill.green") == null) xpFill.color = UiPalette.Good;
            xpText = Ui.Sized(UiKit.Text("Xp", progress, "", UiFonts.Role.Body, TextAnchor.MiddleCenter), 36);
            levelUpText = Ui.Sized(UiKit.Text("LevelUp", progress, "", UiFonts.Role.Body, TextAnchor.MiddleCenter), 36);
            levelUpText.color = UiPalette.Honey;
            notRecordedText = Ui.Sized(UiKit.Text("NotRecorded", progress, "", UiFonts.Role.Body, TextAnchor.MiddleCenter), 36);
            notRecordedText.color = UiPalette.Muted;

            warningText = Ui.Sized(UiKit.Text("Warning", rp.transform, "", UiFonts.Role.Body, TextAnchor.MiddleCenter), 36);
            warningText.color = UiPalette.Warning;

            // Details: Quiet, collapsed by default. The statistics are for whoever wants them.
            detailsButton = Ui.Sized(UiKit.Button("Details", rp.transform, "Details", ToggleDetails, UiKit.Tier.Quiet), 48);
            var sheet = Ui.Sized(UiKit.Frame("DetailsView", rp.transform, UiKit.FrameKind.Parchment), DetailsHeight);
            detailsView = sheet.gameObject;
            var content = UiKit.ScrollView("Scroll", sheet.transform, out var scroll);
            var sr = (RectTransform)scroll.transform;
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(DetailsPad, DetailsPad); sr.offsetMax = new Vector2(-DetailsPad, -DetailsPad);
            // Height is set per run from the text it holds, so the scroll range is exact.
            resultsBody = UiKit.Text("Body", content, "", UiFonts.Role.Body, TextAnchor.UpperLeft);
            resultsBody.color = UiPalette.Ink;   // ink on parchment
            detailsView.SetActive(false);

            // The two exits side by side: Play again is the Primary and the default focus.
            var buttons = Ui.Sized(Ui.Rect("Buttons", rp.transform), UiKit.ButtonHeight);
            againButton = UiKit.Button("PlayAgain", buttons, "Play again  [R]", onPlayAgain, UiKit.Tier.Primary);
            Ui.Place((RectTransform)againButton.transform, new Vector2(0.5f, 0.5f), new Vector2(-184, 0), new Vector2(352, UiKit.ButtonHeight));
            var menu = UiKit.Button("MainMenu", buttons, "Main menu", onMainMenu, UiKit.Tier.Secondary);
            Ui.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(184, 0), new Vector2(352, UiKit.ButtonHeight));
            resultsDim.SetActive(false);
        }

        /// <summary>One headline block: the value in the Number face over its label in Small Muted.</summary>
        static Text StatBlock(RectTransform headline, int i, string label)
        {
            float w = ResultsInner / 3f;
            var block = Ui.Rect(label, headline);
            Ui.Place(block, new Vector2(0, 1), new Vector2(i * w, 0), new Vector2(w, 104));
            var value = UiKit.Text("Value", block, "", UiFonts.Role.Number, TextAnchor.MiddleCenter);
            Ui.Place(value.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(w, 64));
            var caption = UiKit.Text("Label", block, label, UiFonts.Role.Small, TextAnchor.MiddleCenter);
            Ui.Place(caption.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(w, 40));
            return value;
        }

        void ToggleDetails()
        {
            bool open = !detailsView.activeSelf;
            detailsView.SetActive(open);
            detailsButton.GetComponentInChildren<Text>().text = open ? "Hide details" : "Details";
        }

        /// <summary>
        /// The profile outcome (XP, level-up, rewards, save warning). Set by GameRoot when the run
        /// is finalized, which happens inside the sim tick, before this panel first shows.
        /// Replaces SetProgress(string): nothing else called it.
        /// </summary>
        public void SetOutcome(ResultsOutcome o)
        {
            outcome = o;
            bool recorded = o.XpLine != null;
            xpRow.SetActive(recorded);
            xpText.gameObject.SetActive(recorded);
            xpText.text = o.XpLine ?? "";
            xpFill.rectTransform.anchorMax = new Vector2(o.XpFrac, 1);
            levelUpText.gameObject.SetActive(o.LevelUp != null);
            levelUpText.text = o.LevelUp ?? "";
            notRecordedText.gameObject.SetActive(o.NotRecorded != null);
            notRecordedText.text = o.NotRecorded ?? "";
            int n = o.Rewards?.Count ?? 0;
            while (rewardRows.Count < n) rewardRows.Add(NewRewardRow(rewardRows.Count));
            for (int k = 0; k < rewardRows.Count; k++)
            {
                bool on = k < n;
                rewardRows[k].transform.parent.gameObject.SetActive(on);
                if (on) rewardRows[k].text = o.Rewards[k];
            }
            warningText.gameObject.SetActive(o.Warning != null);
            warningText.text = o.Warning ?? "";
            // An empty Progress (a replayed finalize) takes no row at all.
            progress.gameObject.SetActive(recorded || o.NotRecorded != null || n > 0);
            if (shownSummary != null) FillBody(shownSummary);
        }

        /// <summary>A reward row: a check glyph, then the reward. Centred as a group.</summary>
        Text NewRewardRow(int k)
        {
            var row = Ui.Sized(Ui.Rect("Reward" + k, progress), 36);
            var icon = Ui.Image("Icon", row, Color.white);
            icon.sprite = UiGlyphs.Get("state.check");
            icon.raycastTarget = false;
            Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(24, 24));
            var t = UiKit.Text("Text", row, "", UiFonts.Role.Body);
            t.raycastTarget = false;
            Ui.Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(156, 0), new Vector2(ResultsInner - 156, 36));
            return t;
        }

        /// <summary>True while the boss banner has held long enough to start the fight.</summary>
        public bool BannerDone => bannerShownAt >= 0f && Time.unscaledTime - bannerShownAt >= BannerHold;

        /// <summary>
        /// Lore plan Task 3: set by GameRoot while a story scene blocks the flow. The upgrade
        /// cards stay hidden (not merely covered) so they open fresh, with their click guard
        /// re-armed, only once the scene has ended.
        /// </summary>
        public bool HoldUpgrades { get; set; }
        /// <summary>The upgrade choice is on screen and usable.</summary>
        public bool UpgradesShowing => upgradeDim.activeSelf;
        /// <summary>The boss name band is visible this frame.</summary>
        public bool BannerShowing => banner.gameObject.activeSelf;

        /// <summary>The "Play again" button label, for testing that it contains [R].</summary>
        public string AgainLabel => againButton.GetComponentInChildren<Text>().text;

        void LateUpdate()
        {
            if (sim == null) return;
            var state = sim.State;

            bool upgrade = state == RunState.UpgradeChoice && !HoldUpgrades;
            if (upgrade && !upgradeDim.activeSelf)
            {
                // The wave-six choice is the one deferred until the boss fell. Encounters end on
                // a full clear (D50), not on surviving a timer.
                // Sentence case (spec copy rules); the Heading face carries the emphasis.
                upgradeTitle.text = sim.IsEndlessRun
                    ? (sim.Wave >= sim.Config.endless.wavesPerCycle ? "Boss defeated" : $"Wave {sim.Wave} complete")
                    : $"Encounter {sim.TransitionsReached} cleared";
                ShowSwapTargets(-1);   // a new choice always opens on the cards, never mid-swap
                upgradeOpenedAt = Time.unscaledTime;
                FillCards();
            }
            upgradeDim.SetActive(upgrade);
            // Grey the paid buttons out while the guard holds, so a click that is ignored reads as
            // "not yet" rather than as a broken button. 0.35 s is shorter than any deliberate read.
            if (upgrade)
            {
                bool armed = PaidArmed;
                foreach (var b in cardMain) if (b.interactable != armed) b.interactable = armed;
                FitUpgradePanel();
            }

            UpdateBanner(state);

            if (state == RunState.Results && sim.Summary != null && shownSummary != sim.Summary)
            {
                shownSummary = sim.Summary;
                FillResults(shownSummary);
                resultsDim.SetActive(true);
                Select(againButton);
            }
            else if (state != RunState.Results && resultsDim.activeSelf) resultsDim.SetActive(false);
        }

        // ---- Card view -------------------------------------------------------------------------
        // One shape for the offer cards, the incoming card and the swap targets, so the three
        // read as the same object: glyph and "Name rank" on a header line, the effect under it.

        sealed class CardView
        {
            public RectTransform Root;
            public Image Glyph;
            public Text Name, Effect;
            public GameObject Locked;   // "Set full: rank-ups only" (offer cards only)
            public Text Side;           // the right-column caption on the swap step's cards
        }

        static CardView NewCard(Image frame, bool withLock)
        {
            var v = new CardView { Root = frame.rectTransform };
            v.Glyph = Ui.Image("Glyph", frame.transform, Color.white);
            v.Glyph.raycastTarget = false;
            v.Glyph.preserveAspect = true;
            Ui.Place(v.Glyph.rectTransform, new Vector2(0, 1), new Vector2(CardPad, -CardTop), new Vector2(GlyphSize, GlyphSize));
            float nameX = CardPad + GlyphSize + 12f;
            v.Name = UiKit.Text("Name", frame.transform, "", UiFonts.Role.Sub);
            v.Name.raycastTarget = false;
            Ui.Place(v.Name.rectTransform, new Vector2(0, 1), new Vector2(nameX, -CardTop), new Vector2(CardTextWidth + CardPad - nameX, NameHeight));
            // Named "Text": the layout tests and older callers know the card's body by that name.
            v.Effect = UiKit.Text("Text", frame.transform, "", UiFonts.Role.Body, TextAnchor.UpperLeft);
            v.Effect.raycastTarget = false;
            if (withLock)
            {
                var row = Ui.Rect("Locked", frame.transform);
                var icon = Ui.Image("Icon", row, Color.white);
                icon.sprite = UiGlyphs.Get("state.locked");
                icon.raycastTarget = false;
                Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(24, 24));
                var t = UiKit.Text("Text", row, "Set full: rank-ups only", UiFonts.Role.Small);
                t.color = UiPalette.Warning;
                t.raycastTarget = false;
                Ui.Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(CardTextWidth - 32, LockedHeight));
                v.Locked = row.gameObject;
            }
            return v;
        }

        /// <summary>The swap step's right-column caption, where an offer card has its buttons.</summary>
        static Text SideCaption(Transform card, string text)
        {
            var t = UiKit.Text("Side", card, text, UiFonts.Role.Button, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f);
            r.anchoredPosition = new Vector2(-CardPad, 0);
            r.sizeDelta = new Vector2(ActionWidth, 0);
            return t;
        }

        /// <summary>
        /// Fills one card from an offer and sizes it to its text. Returns nothing: the card's
        /// LayoutElement carries the height, so the panel's column re-flows around it.
        /// </summary>
        void FillCard(CardView v, UpgradeOffer o, bool showLock)
        {
            v.Glyph.sprite = UiGlyphs.Get("upgrade." + o.Id);
            // "Name rank" exactly as the HUD tooltip and the pause sheet write it.
            v.Name.text = $"{UpgradeInfo.Name(o.Id)} {o.Rank}";
            // Built from the live tuning, so the card can never promise a number the sim does not use.
            v.Effect.text = UpgradeInfo.Describe(o.Id, o.Rank, sim.Config.upgrades);
            float effectH = Mathf.Ceil(Ui.TextHeight(v.Effect, CardTextWidth));
            float y = CardTop + NameHeight + 8f;
            Ui.Place(v.Effect.rectTransform, new Vector2(0, 1), new Vector2(CardPad, -y), new Vector2(CardTextWidth, effectH));
            y += effectH;
            if (v.Locked != null)
            {
                v.Locked.SetActive(showLock);
                if (showLock)
                {
                    Ui.Place((RectTransform)v.Locked.transform, new Vector2(0, 1), new Vector2(CardPad, -(y + 4f)), new Vector2(CardTextWidth, LockedHeight));
                    y += 4f + LockedHeight;
                }
            }
            // The text decides the height; the floor is what Main + Swap need stacked.
            Ui.Sized(v.Root, Mathf.Max(CardMinHeight, y + CardTop));
        }

        static Button FooterButton(Button b, Vector2 anchor, Vector2 pos, float width)
        {
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // 28 = the column's bottom padding, so the button sits exactly on the Footer spacer.
            Ui.Place((RectTransform)b.transform, anchor, pos + new Vector2(0, 28), new Vector2(width, UiKit.ButtonHeight));
            return b;
        }

        /// <summary>
        /// D96/D100: what a paid pick costs, before and after, in Life points. The clock is paused
        /// during the choice, so this is exactly what the click will take (R13e).
        /// </summary>
        LifeCost Cost() => UpgradeCopy.LifePreview(sim.LifeSeconds, sim.Stats.StartingSeconds, sim.TakeCost);

        void FillLife(LifeCost c)
        {
            lifeText.text = $"Life {c.Now} → {c.After} (−{c.Cost}, {c.Percent}%)";
            float textW = Mathf.Ceil(lifeText.preferredWidth) + 4f;
            Ui.Place(lifeText.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(textW, 40));
            // The bar takes whatever the text leaves, so a 4-digit Life never collides with it.
            Ui.Place(lifeTray.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(Inner - textW - UiKit.Gap, 28));
            lifeFill.rectTransform.anchorMax = new Vector2(c.FracAfter, 1);
            var cr = lifeCost.rectTransform;
            cr.anchorMin = new Vector2(c.FracAfter, 0); cr.anchorMax = new Vector2(c.FracNow, 1);
            cr.offsetMin = cr.offsetMax = Vector2.zero;
        }

        void FillHeld()
        {
            var held = sim.HeldUpgrades;
            int max = Mathf.Max(1, sim.Config.upgrades.maxHeld);
            while (heldChips.Count < held.Count) heldChips.Add(NewHeldChip(heldChips.Count));
            for (int k = 0; k < heldChips.Count; k++)
            {
                bool on = k < held.Count;
                heldChips[k].gameObject.SetActive(on);
                if (!on) continue;
                var o = held[k];
                heldChips[k].transform.Find("Glyph").GetComponent<Image>().sprite = UiGlyphs.Get("upgrade." + o.Id);
                heldChips[k].transform.Find("Rank").GetComponent<Text>().text = o.Rank > 1 ? o.Rank.ToString() : "";
                heldTips[k] = $"{UpgradeInfo.Name(o.Id)} {o.Rank}\n{UpgradeInfo.Describe(o.Id, o.Rank, sim.Config.upgrades)}";
            }
            // The last-slot and full-set warnings live here, once, not on every card (spec).
            upgradeHolding.text = UpgradeCopy.HeldLine(held.Count, max);
            upgradeHolding.color = held.Count >= max - 1 ? UiPalette.Warning : UiPalette.Ivory;
            float x = held.Count == 0 ? 0f : held.Count * (ChipSize + ChipGap) + 8f;
            Ui.Place(upgradeHolding.rectTransform, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(Inner - x, ChipSize + 4));
        }

        /// <summary>The HUD's chip at 40 px, its glyph at 24 (2x the 12-px art, so it stays crisp).</summary>
        Image NewHeldChip(int k)
        {
            var chip = Ui.Image("Chip" + k, heldRow, UiPalette.Panel);
            var frame = UiSkin.Sprite("square.dark");
            if (frame != null) { chip.sprite = frame; chip.type = Image.Type.Sliced; chip.color = Color.white; }
            Ui.Place(chip.rectTransform, new Vector2(0, 0.5f), new Vector2(k * (ChipSize + ChipGap), 0), new Vector2(ChipSize, ChipSize));
            // A Selectable only so the tooltip has hover events; no navigation, so the keyboard
            // path through the panel stays cards → Continue.
            var sel = chip.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            sel.navigation = new Navigation { mode = Navigation.Mode.None };
            heldTips.Add("");
            int index = k;
            UiTooltip.Attach(sel, () => heldTips[index]).Fit(520);
            var glyph = Ui.Image("Glyph", chip.transform, Color.white);
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            Ui.Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            var rank = UiKit.Text("Rank", chip.transform, "", UiFonts.Role.Small, TextAnchor.LowerRight);
            rank.color = UiPalette.Honey;
            rank.raycastTarget = false;
            Ui.Place(rank.rectTransform, new Vector2(1, 0), new Vector2(-1, -6), new Vector2(20, 32));
            return chip;
        }

        void FillCards()
        {
            var held = sim.HeldUpgrades;
            bool locked = sim.UpgradesLocked;
            var cost = Cost();
            FillLife(cost);
            FillHeld();
            for (int i = 0; i < cards.Length; i++)
            {
                bool has = i < sim.Offers.Count;
                cards[i].Root.gameObject.SetActive(has);
                if (!has) continue;
                var o = sim.Offers[i];
                bool rankUp = sim.IsRankUp(o);
                // A new card while the set is full can only be read, not taken: it says why.
                FillCard(cards[i], o, showLock: !rankUp && locked);

                // The button column follows D96's table: rank-up = one paid button; a new card =
                // Take (nothing held) or Add + Swap (1-3 held); a new card while locked = none.
                bool showMain = rankUp || !locked;
                bool showSwap = !rankUp && sim.CanSwap && held.Count <= MaxSwapTargets;
                cardMain[i].gameObject.SetActive(showMain);
                cardSwap[i].gameObject.SetActive(showSwap);
                // The pair is centred on the card; alone, the paid button sits mid-card.
                if (showMain)
                {
                    cardMain[i].GetComponentInChildren<Text>().text = UpgradeCopy.Action(rankUp, held.Count, o.Rank, cost.Cost);
                    Ui.Place((RectTransform)cardMain[i].transform, new Vector2(1, 0.5f),
                        new Vector2(-CardPad, showSwap ? 36 : 0), new Vector2(ActionWidth, UiKit.ButtonHeight));
                }
                if (showSwap)
                    Ui.Place((RectTransform)cardSwap[i].transform, new Vector2(1, 0.5f),
                        new Vector2(-CardPad, showMain ? -36 : 0), new Vector2(ActionWidth, UiKit.ButtonHeight));
            }
            Select(continueButton);   // R13k
        }

        /// <summary>
        /// Swap on card <paramref name="card"/>: with one held there is nothing to ask, so it
        /// swaps at once; with 2+ held the player picks which card goes (R13d).
        /// </summary>
        void OnSwap(int card)
        {
            if (sim == null) return;
            if (sim.HeldUpgrades.Count == 1) { choose?.Invoke(card, 0); return; }
            ShowSwapTargets(card);
        }

        /// <summary>Show the "Replace which?" step for <paramref name="card"/>, or hide it with -1.</summary>
        void ShowSwapTargets(int card)
        {
            swapCard = card;
            bool picking = card >= 0 && sim != null && card < sim.Offers.Count;
            if (!picking) swapCard = -1;
            swapTitle.gameObject.SetActive(picking);
            swapBack.gameObject.SetActive(picking);
            incoming.Root.gameObject.SetActive(picking);
            var held = sim != null ? sim.HeldUpgrades : null;
            for (int j = 0; j < swapTargets.Length; j++)
            {
                bool show = picking && j < held.Count;
                swapTargets[j].gameObject.SetActive(show);
                if (show) FillCard(swapViews[j], held[j], showLock: false);
            }
            // Spec: the step shows only the choice it asks. The title, Life summary, held line,
            // cards, Continue and Retire make way; FillCards brings the cards back.
            upgradeTitle.gameObject.SetActive(!picking);
            lifeRow.gameObject.SetActive(!picking);
            heldRow.gameObject.SetActive(!picking);
            if (picking) foreach (var c in cards) c.Root.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(!picking);
            retireButton.gameObject.SetActive(!picking && sim != null && sim.IsEndlessRun);
            if (picking)
            {
                FillCard(incoming, sim.Offers[card], showLock: false);
                // Back is the safe default: a stray Enter returns to the cards instead of discarding one.
                Select(swapBack);
            }
        }

        /// <summary>
        /// Task 18 sweep: at a 130% interface scale the canvas is only 831 units tall, and the
        /// tallest draw (three held, three long cards) ran its footer off the bottom. The panel
        /// keeps its top at 104 so the Life bar stays clear, and shrinks uniformly when its
        /// content cannot fit below that. Scaling (not scrolling) keeps every card and the
        /// footer reachable with no extra input. At 100% nothing reaches the limit, so scale is 1.
        /// </summary>
        void FitUpgradePanel()
        {
            float room = ((RectTransform)upgradeDim.transform).rect.height - 104f - UiKit.Gap;
            float need = LayoutUtility.GetPreferredHeight(upgradePanel);
            float s = need > room && need > 0f ? room / need : 1f;
            if (!Mathf.Approximately(upgradePanel.localScale.x, s)) upgradePanel.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>The banner's copy for a boss: the display name, as written (sentence case rule).</summary>
        public static string BannerText(string bossName) => bossName;

        void UpdateBanner(RunState state)
        {
            if (state == RunState.BossIntro && !bannerWasIntro)
            {
                bannerWasIntro = true;
                bannerShownAt = Time.unscaledTime;
                bannerName.text = BannerText(sim.Config.collector.displayName);
                // A short run's Sanctum reveal already showed the title at the arrival, before
                // the final upgrades: the band is not repeated at the confrontation. The timer
                // (BannerDone) still runs, so the normal spawn warning beat is kept. Classic
                // arenas (no reveal) and endless (title shown during its own pull) keep the band.
                bannerLive = !(sim.IsShortRun && sim.ArenaStage == 3);
            }
            // The run can end inside the fade (a fast kill, a death): the results must never
            // sit on top of a half-faded boss name.
            if (state == RunState.Results) bannerLive = false;
            // The timer (BannerDone) keeps running from bannerShownAt regardless; only the band's
            // visibility stands down while the cinematic shows the same name, so the player never
            // reads it twice in one frame.
            banner.gameObject.SetActive(bannerLive && !WorldIntroOverlay.TitleShowing);
            if (!bannerLive) return;

            float age = Time.unscaledTime - bannerShownAt;
            // Pop in: overshoot from 1.35x down to 1x in a quarter second, then hold, then fade
            // once the fight has started (the banner never covers live combat for long).
            float pop = Mathf.Clamp01(age / 0.25f);
            float scale = Mathf.Lerp(1.35f, 1f, 1f - (1f - pop) * (1f - pop));
            bannerName.rectTransform.localScale = Vector3.one * scale;
            float fade = state == RunState.BossIntro || state == RunState.Paused ? 1f
                : 1f - Mathf.Clamp01((age - BannerHold) / BannerFade);
            bannerGroup.alpha = Mathf.Min(pop * 2f, fade);
            if (fade <= 0f) { bannerLive = false; banner.gameObject.SetActive(false); }
        }

        void FillResults(RunSummary s)
        {
            (resultsTitle.text, resultsTitle.color) = ResultsCopy.Title(s.Reason);
            string sub = ResultsCopy.Subtitle(s.Reason, s.Mode, s.Cycle, s.WavesCompleted, s.BossesDefeated, s.VictoryBonus);
            resultsSubtitle.gameObject.SetActive(sub != null);
            resultsSubtitle.text = sub ?? "";
            ShowRecall();
            int secs = Mathf.FloorToInt(s.Duration);
            scoreValue.text = s.Score.ToString();
            durationValue.text = $"{secs / 60}:{secs % 60:00}";
            killsValue.text = s.Kills.ToString();
            // Every results screen opens with Details collapsed, whatever the last one was left at.
            detailsView.SetActive(false);
            detailsButton.GetComponentInChildren<Text>().text = "Details";
            FillBody(s);
        }

        void ShowRecall()
        {
            resultsRecall.gameObject.SetActive(!string.IsNullOrEmpty(recall));
            resultsRecall.text = recall ?? "";
            // Sized to its wrapped text (two lines at Body in the inner width): the column has
            // no fixed box to leave dead space in (the layout test's gap rule).
            Ui.Sized(resultsRecall, Mathf.Ceil(Ui.TextHeight(resultsRecall, ResultsInner)) + 4f);
        }

        /// <summary>
        /// Details: the full statistics (the pre-Task-16 text, unchanged), then the XP breakdown
        /// and the record comparisons that did not change, from the finalize.
        /// </summary>
        void FillBody(RunSummary s)
        {
            string bonus = s.VictoryBonus > 0 ? $"   (time bonus +{s.VictoryBonus})" : "";
            // D102: where the skill-shot score came from, only when there is any, so a quiet
            // run does not grow a line of zeros.
            string skillScore = s.ScoreFromOvercharges > 0 || s.ScoreFromChains > 0
                ? $"Overcharge +{s.ScoreFromOvercharges}   Chains +{s.ScoreFromChains}\n" : "";
            resultsBody.text =
                $"{ReasonText(s)}{bonus}\n" +
                skillScore +
                $"Best volley: {s.BestVolleyKills}   Best chain: x{s.BestChain}   Overcharges: {s.Overcharges}\n" +
                $"Hit rate: {Mathf.RoundToInt(s.HitRate * 100f)}%  ({s.PacketsHit}/{s.PacketsReleased})   Average power: x{s.AverageFirePower:0.00}\n" +
                $"Health lost to hits: {LifeDisplay.Points(s.DamageTaken)}   Health gained: {LifeDisplay.Points(s.SecondsGained)}   Health stolen: {LifeDisplay.Points(s.LifeStolen)}   Backfires: {s.Backfires}   Swaps: {s.Swaps}\n" +
                $"Health sacrificed: {LifeDisplay.Points(s.SecondsSacrificed)} ({s.UpgradesPaidFor} upgrade{(s.UpgradesPaidFor == 1 ? "" : "s")})   Most held: {s.MostUpgradesHeld}";
            if (outcome.Details != null && outcome.Details.Count > 0)
                resultsBody.text += "\n\n" + string.Join("\n", outcome.Details);
            // Sized to its text so the scroll range is exact: the ScrollView's content is a
            // column with a fitter, so the body's LayoutElement height IS the scroll length.
            Ui.Sized(resultsBody, Mathf.Ceil(Ui.TextHeight(resultsBody, BodyWidth)) + 4f);
        }

        // The viewport's width: the sheet's inner width minus the scroll track's 24.
        const float BodyWidth = ResultsInner - 2 * DetailsPad - 24f;

        static string ReasonText(RunSummary s)
        {
            // Endless has no victory: the line says how far the run got (D84).
            if (s.Mode == GameMode.Endless)
            {
                string how = s.Reason == RunEndReason.Retired ? "Retired" : s.Reason == RunEndReason.Death ? "Fell" : "Clock ran out";
                return $"{how} in cycle {s.Cycle}   ·   {s.WavesCompleted} wave{(s.WavesCompleted == 1 ? "" : "s")}   ·   {s.BossesDefeated} boss{(s.BossesDefeated == 1 ? "" : "es")}";
            }
            return ReasonText(s.Reason);
        }

        static string ReasonText(RunEndReason r) => r switch
        {
            RunEndReason.Victory => "The Collector fell",
            RunEndReason.Death => "You fell",
            RunEndReason.TimeExpired => "The Collector outlasted the clock",
            _ => r.ToString(),
        };

        static void Select(Button b)
        {
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(b.gameObject);
        }
    }
}
