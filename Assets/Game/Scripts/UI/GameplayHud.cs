using System;
using BorrowedHex.Core;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// In-run HUD (plan Task 13). Polls durable state and subscribes only for a transient clock
    /// flash; rebinding detaches the old sim so a stale run can never update the screen.
    ///
    /// Layout, at the 1920x1080 reference, everything hung from the top edge:
    /// - top centre: the Life block (heart + crested bar), the dominant element; under it the
    ///   one-line objective, then the boss bar while a boss lives; the chain to Life's right.
    /// - top left: score, dash, the held-upgrade chips.
    /// - top right: pause, and the Practice drawer under it (Task 12).
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        public const int ChipCount = 4;
        const float ChipSize = 56f, ChipGap = 8f;
        // Ornate bars are pixel art: drawn at a whole number of reference px per art px.
        // The sprites are PPU 50 on a PPU 100 canvas (2 ref px per art px), so the Image's
        // pixelsPerUnitMultiplier is 2 / scale.
        const int LifeScale = 4, BossScale = 3, DashScale = 2;

        ArenaSim sim;
        Image dashFill;
        Text dashLabel;
        Image lifeHeart, lifeTray, lifeFill;
        Text objectiveLabel, scoreLabel, chainLabel;
        Image bossTray, bossFill;
        RectTransform combat, lifeBlock;
        readonly Image[] chips = new Image[ChipCount];
        readonly Image[] chipGlyphs = new Image[ChipCount];
        readonly Text[] chipRanks = new Text[ChipCount];
        readonly string[] chipTips = new string[ChipCount];
        Image lockedBadge;

        public Button PauseButton { get; private set; }
        public Button ResetButton { get; private set; }
        public PacketIndicator Packets { get; private set; }
        public PracticeDrawer Drawer { get; private set; }

        public static GameplayHud Create(Canvas canvas, Action onPause, Action onReset)
        {
            var root = Ui.Stretch(Ui.Rect("Hud", canvas.transform));
            var hud = root.gameObject.AddComponent<GameplayHud>();
            hud.Build(root, onPause, onReset);
            return hud;
        }

        void Build(RectTransform root, Action onPause, Action onReset)
        {
            // Combat-only readouts (dash, objective, chain, boss bar, chips, the hand) live in
            // their own full-screen group so they go together while a choice or the results are
            // open: the clock is frozen then, none of them can change or be used, and the centred
            // panels would otherwise sit on top of them. Life, score and pause stay: the panels
            // price things in Life.
            combat = Ui.Stretch(Ui.Rect("Combat", root));

            // ---- Life: the one thing the player must always read --------------------------
            // A plain rect (no Graphic) so the layout tests still see heart and bar as boxes.
            lifeBlock = Ui.Stretch(Ui.Rect("LifeBlock", root));
            lifeHeart = Ui.Image("LifeHeart", lifeBlock, Color.white);
            lifeHeart.sprite = Presentation.PixelSprites.Heart();
            lifeHeart.preserveAspect = true;
            Ui.Place(lifeHeart.rectTransform, new Vector2(0.5f, 1), new Vector2(-316, -24), new Vector2(64, 64));
            // 600 x 28: bar.trayDark is 7 art px tall, so 28 at 4 ref px per art px (the brief's
            // 32 would stretch it by a non-whole factor). Centred on the heart (centre y -56).
            (lifeTray, lifeFill) = Bar("LifeBar", lifeBlock, new Vector2(24, -42), 600, LifeScale, "fill.red");
            // Blood for a full bar: "blood is life" is the premise. Urgency comes from the heart
            // beat and the damage flash, not from a traffic-light hue.
            lifeFill.color = UiPalette.Blood;
            var crestSprite = UiSkin.Sprite("bar.crest");
            if (crestSprite != null)
            {
                // Its bottom art row is the tray's top line: overlap by one art row so they meet.
                var crest = Ui.Image("Crest", lifeTray.transform, Color.white);
                crest.sprite = crestSprite;
                crest.raycastTarget = false;
                var cr = crest.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 1f);
                cr.pivot = new Vector2(0.5f, 0f);
                cr.anchoredPosition = new Vector2(0, -LifeScale);
                cr.sizeDelta = new Vector2(17 * LifeScale, 6 * LifeScale);
            }

            // D95: the chain counter sits right of Life, because chains are life. The brief put it
            // at x 380 (270..490), across the bar's right end (324); it starts after the bar (346).
            chainLabel = UiKit.Text("Chain", combat, "", UiFonts.Role.Small, TextAnchor.MiddleLeft);
            Ui.Place(chainLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(496, -38), new Vector2(300, 36));   // 300: "Chain x12  +2.5 next" at Small
            // Section 6: "present the objective clearly from the start": one short line.
            objectiveLabel = UiKit.Text("Objective", combat, "", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            objectiveLabel.color = UiPalette.Muted;
            Ui.Place(objectiveLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(900, 36));   // 36 = one line of Small
            (bossTray, bossFill) = Bar("BossBar", combat, new Vector2(0, -128), 600, BossScale, "fill.red");
            bossTray.gameObject.SetActive(false);

            // ---- Top left: score, dash, held upgrades --------------------------------------
            scoreLabel = UiKit.Text("Score", root, "", UiFonts.Role.Number, TextAnchor.MiddleLeft);
            Ui.Place(scoreLabel.rectTransform, new Vector2(0, 1), new Vector2(40, -28), new Vector2(480, 56));   // 56 >= one line of Number (54)
            Image dashTray;
            (dashTray, dashFill) = Bar("DashBar", combat, Vector2.zero, 220, DashScale, "fill.blue");
            Ui.Place(dashTray.rectTransform, new Vector2(0, 1), new Vector2(40, -96), dashTray.rectTransform.sizeDelta);   // under the score (bottom -84)
            dashLabel = UiKit.Text("DashLabel", combat, "Dash", UiFonts.Role.Small, TextAnchor.MiddleLeft);
            Ui.Place(dashLabel.rectTransform, new Vector2(0, 1), new Vector2(272, -85), new Vector2(120, 36));   // centred on the dash bar (y -96..-110)
            for (int i = 0; i < ChipCount; i++) BuildChip(i);
            // Locked set (no new cards can join): a fifth, smaller badge after the chips.
            lockedBadge = Ui.Image("Locked", combat, Color.white);
            lockedBadge.sprite = UiGlyphs.Get("state.locked");
            lockedBadge.preserveAspect = true;
            Ui.Place(lockedBadge.rectTransform, new Vector2(0, 1), new Vector2(40 + ChipCount * (ChipSize + ChipGap), -136), new Vector2(40, 40));
            var lockSel = lockedBadge.gameObject.AddComponent<Selectable>();
            lockSel.transition = Selectable.Transition.None;
            lockSel.navigation = new Navigation { mode = Navigation.Mode.None };
            UiTooltip.Attach(lockSel, () => "No new upgrades: rank-ups only").Fit(420);
            lockedBadge.gameObject.SetActive(false);

            // ---- Top right: pause, then the Practice drawer (Task 12) ----------------------
            // btn.options is a cog, not a pause sign. "II" set in the font read as "11" in the
            // captures (alagard's I carries serifs), so the sign is two drawn bars instead:
            // 8x24 ref px each = 4x12 art px, whole pixels at the canvas's 2:1 scale.
            PauseButton = UiKit.Button("Pause", root, "", onPause, UiKit.Tier.Icon);
            Ui.Place((RectTransform)PauseButton.transform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(64, 64));
            for (int i = 0; i < 2; i++)
            {
                var bar = Ui.Image(i == 0 ? "BarL" : "BarR", PauseButton.transform, UiPalette.Ivory);
                bar.raycastTarget = false;   // the plate is the hit area
                Ui.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -8 : 8, 0), new Vector2(8, 24));
            }
            Drawer = PracticeDrawer.Create(root);
            ResetButton = Drawer.Add("Reset", onReset);

            Packets = PacketIndicator.Create(combat);
        }

        /// <summary>
        /// An ornate tray with a fill inside its trough. Pixel scale is whole (see LifeScale);
        /// without the art (public checkout) it falls back to a dark strip with a flat fill.
        /// Returns the tray and the fill; the fill's anchorMax.x is the bar's value.
        /// </summary>
        // internal: PacketIndicator's countdown and catch bars are the same object (Task 14).
        internal static (Image tray, Image fill) Bar(string name, Transform parent, Vector2 pos, float width, int scale, string fillId)
        {
            var traySprite = UiSkin.Sprite("bar.trayDark");
            float height = 7 * scale;   // bar.trayDark is 7 art px tall
            var tray = Ui.Image(name, parent, traySprite != null ? Color.white : new Color(0, 0, 0, 0.6f));
            tray.raycastTarget = false;
            Ui.Place(tray.rectTransform, new Vector2(0.5f, 1), pos, new Vector2(width, height));
            // The trough: inside the end ornaments (11 art px each side) and the rim (1 art px).
            var trough = Ui.Rect("Trough", tray.transform);
            trough.anchorMin = Vector2.zero; trough.anchorMax = Vector2.one;
            if (traySprite != null)
            {
                tray.sprite = traySprite;
                tray.type = Image.Type.Sliced;
                tray.pixelsPerUnitMultiplier = 2f / scale;
                trough.offsetMin = new Vector2(11 * scale, scale);
                trough.offsetMax = new Vector2(-11 * scale, -scale);
            }
            else trough.offsetMin = trough.offsetMax = Vector2.zero;
            var fillSprite = UiSkin.Sprite(fillId);
            var fill = Ui.Image("Fill", trough, Color.white);
            fill.raycastTarget = false;
            if (fillSprite != null)
            {
                fill.sprite = fillSprite;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 2f / scale;
            }
            else fill.color = fillId == "fill.blue" ? UiPalette.Violet : UiPalette.Blood;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            return (tray, fill);
        }

        void BuildChip(int i)
        {
            var chip = Ui.Image("Chip" + i, combat, UiPalette.Panel);
            var frame = UiSkin.Sprite("square.dark");
            if (frame != null) { chip.sprite = frame; chip.type = Image.Type.Sliced; chip.color = Color.white; }
            Ui.Place(chip.rectTransform, new Vector2(0, 1), new Vector2(40 + i * (ChipSize + ChipGap), -128), new Vector2(ChipSize, ChipSize));
            // A Selectable only so the tooltip has hover events: no transition and no navigation,
            // so keyboard focus can never wander into the HUD mid-fight.
            var sel = chip.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            sel.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            UiTooltip.Attach(sel, () => chipTips[index]).Fit(520);
            var glyph = Ui.Image("Glyph", chip.transform, Color.white);
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            Ui.Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            var rank = UiKit.Text("Rank", chip.transform, "", UiFonts.Role.Small, TextAnchor.LowerRight);
            rank.color = UiPalette.Honey;
            rank.raycastTarget = false;
            Ui.Place(rank.rectTransform, new Vector2(1, 0), new Vector2(-2, -4), new Vector2(28, 32));
            chips[i] = chip; chipGlyphs[i] = glyph; chipRanks[i] = rank;
            chip.gameObject.SetActive(false);
        }

        public void Bind(ArenaSim s)
        {
            if (sim != null) sim.Events.LifeClockChanged -= FlashClock;
            sim = s;
            sim.Events.LifeClockChanged += FlashClock;
            clockFlashUntil = 0;
            heldKey = null;   // a new run: refill the chips on the next frame
            Packets.Bind(s);
        }

        float clockFlashUntil;
        void FlashClock(float delta, Vector2 at)
        {
            // Losses only (D66): hits and backfires are what the player must notice. Kill gains
            // flashed too often to mean anything and already get a floating "+N" at the kill.
            if (delta >= 0) return;
            clockFlashUntil = Time.unscaledTime + 0.25f;
        }
        void OnDestroy() { if (sim != null) sim.Events.LifeClockChanged -= FlashClock; }

        /// <summary>
        /// A development tool (summon enemies, toggle auto-spawn, clear...). It goes in the
        /// Practice drawer under the heading its label implies, and shares the drawer's
        /// visibility: sandbox/debug runs only, never in a scored run.
        /// </summary>
        public Button AddDevButton(string label, Action onClick) => Drawer.Add(label, onClick);

        bool practiceVisible, covered;

        /// <summary>
        /// The practice tools exist in sandbox/debug runs only, never in a scored run. Called by
        /// every BeginRun, which is also where the drawer is shut: each run starts with it closed.
        /// Reset is switched off as well as its drawer, so "is Reset there" reads true to its
        /// own activeSelf, not only to its parent's.
        /// </summary>
        public void SetPracticeVisible(bool on)
        {
            practiceVisible = on;
            ResetButton.gameObject.SetActive(on);
            Drawer.SetOpen(false);
            Drawer.gameObject.SetActive(on && !covered);
        }

        /// <summary>Old name (pre Task 12), kept so callers outside the HUD need no change.</summary>
        public void SetResetVisible(bool on) => SetPracticeVisible(on);

        /// <summary>
        /// Everything but the Life block hides (pause, tutorial completion, results): Life stays
        /// as context, everything else would compete with the panel over it. Explicit list rather
        /// than "every child": uncovering must restore the drawer to the run's practice state,
        /// not to whatever it was when covering began (a restart from pause changes it).
        /// </summary>
        public void SetCovered(bool on)
        {
            covered = on;
            // Covering only ever hides: the state rule in LateUpdate still decides combat.
            if (on) combat.gameObject.SetActive(false);
            scoreLabel.gameObject.SetActive(!on);
            PauseButton.gameObject.SetActive(!on);
            Drawer.gameObject.SetActive(!on && practiceVisible);
        }

        string heldKey;

        void LateUpdate()
        {
            if (sim == null) return;
            // Polled like everything else here, so a restart or a closed panel restores it with no event.
            var state = sim.State;
            combat.gameObject.SetActive(!covered && state != RunState.UpgradeChoice && state != RunState.Results);
            bool tutorial = sim.Tutorial != null;
            var p = sim.Player;
            // Cooldown progress from the gameplay clock, so it freezes while paused.
            double now = sim.Clock.Now;
            float cd = Mathf.Max(0.0001f, sim.Stats.DashCooldown);
            float ready = Mathf.Clamp01(1f - (float)((p.DashReadyAt - now) / cd));
            dashFill.rectTransform.anchorMax = new Vector2(ready, 1);
            // Dimmed while recharging: the bar's length says how long, the brightness says "not yet".
            dashFill.color = ready >= 1f ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            // Spec: hide the irrelevant during lessons. The dash bar stays (the dash lesson needs
            // it), its caption and the score go.
            dashLabel.gameObject.SetActive(!tutorial);
            // The tutorial's prompts teach the same keys; two sets of instructions compete (Task 13/14).
            Packets.SetHintVisible(!tutorial);
            dashLabel.color = ready >= 1f ? UiPalette.Ivory : UiPalette.Muted;

            // Life and elapsed time are independent: gains must not rewind enemies.
            // Fraction of the cap (StartingSeconds), the same cap kill rewards clamp to,
            // so a full bar always means "cannot gain more".
            float frac = Mathf.Clamp01(sim.LifeSeconds / Mathf.Max(0.0001f, sim.Stats.StartingSeconds));
            lifeFill.rectTransform.anchorMax = new Vector2(frac, 1);
            // Hits and backfires flash the tray (gains do not, D66). Reduce flashes keeps it steady.
            bool flashing = !DisplayOptions.ReduceFlashes && Time.unscaledTime < clockFlashUntil;
            lifeTray.color = flashing ? new Color(1f, 0.45f, 0.45f) : (UiSkin.HasArt ? Color.white : new Color(0, 0, 0, 0.6f));
            // Low life: the heart beats (unscaled time, so it keeps beating while paused; it is
            // a reminder, not gameplay). Below 1/6 of the cap = 30 s at 180, the old red-text threshold.
            float beat = frac < 1f / 6f && !DisplayOptions.ReduceFlashes ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            lifeHeart.rectTransform.localScale = new Vector3(beat, beat, 1);

            // Shown from the 2nd kill, fading out as the window runs down so the player can see
            // how long they have to keep it going.
            var chain = sim.Chain;
            bool showChain = chain != null && chain.Length >= 2 && chain.IsActive(now);
            chainLabel.text = showChain ? HudText.Chain(chain.Length, chain.BonusFor(chain.Length + 1)) : "";
            if (showChain)
            {
                float left = Mathf.Clamp01((float)((chain.ExpiresAt - now) / Mathf.Max(0.01f, sim.Config.combat.chainWindow)));
                var h = UiPalette.Honey;
                chainLabel.color = new Color(h.r, h.g, h.b, 0.35f + 0.65f * left);
            }

            bool boss = state == RunState.BossIntro || state == RunState.BossCombat;
            bool encounters = sim.Encounter < sim.Config.shortMode.encounterCount;
            var info = new ObjectiveInfo
            {
                Kind = tutorial ? ObjectiveKind.Tutorial
                    : sim.IsEndlessRun ? (boss ? ObjectiveKind.EndlessBoss : ObjectiveKind.EndlessWave)
                    : !sim.IsShortRun ? ObjectiveKind.Sandbox
                    : encounters ? ObjectiveKind.Encounter : ObjectiveKind.ShortBoss,
                Encounter = sim.Encounter, EncounterCount = sim.Config.shortMode.encounterCount,
                // Only asked for when it is shown: EnemiesLeftInEncounter walks the enemy list.
                EnemiesLeft = sim.IsShortRun && !tutorial && encounters ? sim.EnemiesLeftInEncounter() : 0,
                Wave = sim.Wave, WavesPerCycle = sim.Config.endless.wavesPerCycle, Cycle = sim.Cycle,
                WaveSecondsLeft = sim.WaveSecondsLeft, BossName = sim.Config.collector.displayName,
            };
            objectiveLabel.text = HudText.Objective(info);

            var score = sim.Score;
            scoreLabel.gameObject.SetActive(!covered && !tutorial);
            scoreLabel.text = HudText.Score(score.Score, score.Multiplier);
            scoreLabel.color = score.Multiplier > 1f ? UiPalette.Honey : UiPalette.Ivory;

            RefreshChips();

            var bossActor = sim.LivingBoss();
            bossTray.gameObject.SetActive(bossActor != null);
            if (bossActor != null)
                bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(bossActor.Health / Mathf.Max(1f, bossActor.MaxHealth)), 1);
        }

        /// <summary>
        /// D96: up to four upgrades are held for the whole run. Rebuilt only when the held set
        /// changes (ids + ranks + locked), so glyphs and tooltip text are not reassigned per frame.
        /// </summary>
        void RefreshChips()
        {
            var held = sim.HeldUpgrades;
            var key = new System.Text.StringBuilder(sim.UpgradesLocked ? "L" : "-");
            for (int i = 0; i < held.Count; i++) key.Append((int)held[i].Id).Append(':').Append(held[i].Rank).Append(',');
            string k = key.ToString();
            if (k == heldKey) return;
            heldKey = k;
            for (int i = 0; i < ChipCount; i++)
            {
                bool on = i < held.Count;
                chips[i].gameObject.SetActive(on);
                if (!on) { chipTips[i] = ""; continue; }
                var o = held[i];
                chipGlyphs[i].sprite = UiGlyphs.Get("upgrade." + o.Id);
                chipRanks[i].text = o.Rank > 1 ? o.Rank.ToString() : "";
                chipTips[i] = $"{UpgradeInfo.Name(o.Id)} {o.Rank}\n{UpgradeInfo.Describe(o.Id, o.Rank, sim.Config.upgrades)}";
            }
            lockedBadge.gameObject.SetActive(sim.UpgradesLocked && held.Count > 0);
        }
    }
}
