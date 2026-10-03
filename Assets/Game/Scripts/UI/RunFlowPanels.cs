using System;
using BorrowedHex.Core;
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
        Text upgradeHolding;
        Text upgradeNote;
        Button retireButton;
        Button continueButton;
        Action<int, int> choose;
        // One row per offer slot: the card's text plus its own buttons (D96). Offers are re-read
        // from the sim each time the panel opens, so the cards never show a previous draw.
        readonly GameObject[] cardRows = new GameObject[3];
        readonly Text[] cardTexts = new Text[3];
        readonly Button[] cardMain = new Button[3];   // Take / Add / Rank up (paid)
        readonly Button[] cardSwap = new Button[3];   // Swap (free)

        // R13d swap-target step: "Replace which?" plus one button per held upgrade and Back.
        // Three targets cover every legal swap: swaps stop once maxHeld (4) is reached, so at
        // most three are held when one is offered. A larger maxHeld hides Swap past three.
        const int MaxSwapTargets = 3;
        Text swapTitle;
        readonly Button[] swapTargets = new Button[MaxSwapTargets];
        Button swapBack;
        int swapCard = -1;   // the offer being swapped in while the target row shows, else -1

        RectTransform banner;
        CanvasGroup bannerGroup;
        Text bannerName;
        Text bannerSub;
        float bannerShownAt = -1f;
        bool bannerWasIntro;

        GameObject resultsDim;
        Text resultsTitle;
        Text resultsBody;
        Button againButton;
        RunSummary shownSummary;
        Text progressBody;

        /// <summary>Seconds the boss banner holds the frozen frame before the fight starts.</summary>
        public const float BannerHold = 2.4f;
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
            var dim = Ui.Image("UpgradeChoice", root, new Color(0, 0, 0, 0.55f));
            Ui.Stretch(dim.rectTransform);
            upgradeDim = dim.gameObject;
            var panel = Ui.Image("Panel", dim.transform, Ui.Panel);
            // 900 wide (was 780): each card now carries a 290 px button column beside its text.
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel.rectTransform, 14);
            col.padding = new RectOffset(40, 40, 32, 36);
            upgradeTitle = Ui.Sized(Ui.Label("Title", panel.transform, "", 46), 64);
            upgradeTitle.color = Ui.Accent;
            var note = Ui.Sized(Ui.Label("Note", panel.transform, "", 22), 30);
            note.color = new Color(1, 1, 1, 0.7f);
            upgradeNote = note;
            upgradeHolding = Ui.Sized(Ui.Label("Holding", panel.transform, "", 22), 30);
            upgradeHolding.color = new Color(0.75f, 0.95f, 1f);
            upgradeHolding.supportRichText = true;
            for (int i = 0; i < cardRows.Length; i++)
            {
                int index = i;   // captured per card; the loop variable would be 3 for all of them
                var row = Ui.Sized(Ui.Image($"Card{i}", panel.transform, new Color(0.16f, 0.12f, 0.26f, 1f)), 124);
                cardRows[i] = row.gameObject;
                cardTexts[i] = Ui.Label("Text", row.transform, "", 24, TextAnchor.MiddleLeft);
                cardTexts[i].supportRichText = true;
                // Text fills the row left of the 290 px button column, inset so long descriptions wrap.
                var tr = cardTexts[i].rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(20, 8);
                tr.offsetMax = new Vector2(-312, -8);
                cardMain[i] = Ui.Button("Main", row.transform, "", () => choose?.Invoke(index, -1), 20);
                cardSwap[i] = Ui.Button("Swap", row.transform, "Swap — free", () => OnSwap(index), 20);
            }
            // Swap targets (R13d), hidden until a Swap with 2+ held asks which card goes.
            swapTitle = Ui.Sized(Ui.Label("SwapTitle", panel.transform, "", 26), 40);
            swapTitle.supportRichText = true;
            for (int j = 0; j < swapTargets.Length; j++)
            {
                int target = j;
                swapTargets[j] = Ui.Sized(Ui.Button($"Replace{j}", panel.transform, "", () => { if (swapCard >= 0) choose?.Invoke(swapCard, target); }, 24), 60);
            }
            swapBack = Ui.Sized(Ui.Button("Back", panel.transform, "Back", () => { ShowSwapTargets(-1); FillCards(); }, 22), 52);
            // R13k: the free choice is the default focus, so a stray Enter never spends life.
            continueButton = Ui.Sized(Ui.Button("Continue", panel.transform, "Continue without an upgrade", () => onContinue?.Invoke(), 24), 60);
            // Endless only (section 6): retiring is offered between waves, i.e. here. Built
            // after the cards so it sits under them, smaller, and is never the default focus.
            retireButton = Ui.Sized(Ui.Button("Retire", panel.transform, "Retire (end run, keep progress)", () => onRetire?.Invoke(), 22), 52);
            ShowSwapTargets(-1);
            upgradeDim.SetActive(false);

            // --- Boss banner: a full-width band across the upper third, name in gold.
            var band = Ui.Image("BossBanner", root, new Color(0.05f, 0.02f, 0.08f, 0.88f));
            banner = band.rectTransform;
            banner.anchorMin = new Vector2(0f, 0.5f);
            banner.anchorMax = new Vector2(1f, 0.5f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition = new Vector2(0f, 150f);
            banner.sizeDelta = new Vector2(0f, 190f);
            band.raycastTarget = false;
            bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var warn = Ui.Label("Warning", band.transform, "— BOSS —", 26);
            Ui.Place(warn.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(800, 34));
            warn.color = new Color(1f, 0.4f, 0.32f);
            bannerName = Ui.Label("Name", band.transform, "", 84);
            Ui.Place(bannerName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(1400, 100));
            bannerName.color = Ui.Accent;
            bannerName.fontStyle = FontStyle.Bold;
            bannerSub = Ui.Label("Sub", band.transform, "Defeat it before the time runs out", 26);
            Ui.Place(bannerSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(900, 34));
            band.gameObject.SetActive(false);

            // --- Results.
            var rdim = Ui.Image("Results", root, new Color(0, 0, 0, 0.7f));
            Ui.Stretch(rdim.rectTransform);
            resultsDim = rdim.gameObject;
            var rp = Ui.Image("Panel", rdim.transform, Ui.Panel);
            Ui.Place(rp.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 0));
            rp.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rcol = Ui.Column(rp.rectTransform, 14);
            rcol.padding = new RectOffset(44, 44, 32, 36);
            resultsTitle = Ui.Sized(Ui.Label("Title", rp.transform, "", 58), 76);
            resultsBody = Ui.Sized(Ui.Label("Body", rp.transform, "", 28, TextAnchor.UpperCenter), 340);   // 340: D96 added a line, and long lines wrap at 620 wide
            // Profile outcome (saved / XP / achievements / records), filled by the finalization.
            progressBody = Ui.Sized(Ui.Label("Progress", rp.transform, "", 24, TextAnchor.UpperCenter), 0);
            progressBody.color = new Color(0.75f, 0.95f, 1f);
            progressBody.supportRichText = true;
            resultsBody.lineSpacing = 1.15f;
            againButton = Ui.Sized(Ui.Button("PlayAgain", rp.transform, "Play again", onPlayAgain), 72);
            Ui.Sized(Ui.Button("MainMenu", rp.transform, "Main menu", onMainMenu), 64);
            resultsDim.SetActive(false);
        }

        public void Bind(ArenaSim s)
        {
            sim = s;
            shownSummary = null;
            bannerShownAt = -1f;
            bannerWasIntro = false;
            banner.gameObject.SetActive(false);
            upgradeDim.SetActive(false);
            resultsDim.SetActive(false);
            SetProgress(null);
        }

        /// <summary>
        /// The profile outcome under the run statistics. Set by GameRoot when the run is
        /// finalized, which happens inside the sim tick, before this panel first shows.
        /// </summary>
        public void SetProgress(string text)
        {
            progressBody.text = text ?? "";
            int lines = string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;
            progressBody.GetComponent<LayoutElement>().preferredHeight = lines * 30;
        }

        /// <summary>True while the boss banner has held long enough to start the fight.</summary>
        public bool BannerDone => bannerShownAt >= 0f && Time.unscaledTime - bannerShownAt >= BannerHold;

        void LateUpdate()
        {
            if (sim == null) return;
            var state = sim.State;

            bool upgrade = state == RunState.UpgradeChoice;
            if (upgrade && !upgradeDim.activeSelf)
            {
                // The wave-six choice is the one deferred until the boss fell. Encounters end on
                // a full clear (D50), not on surviving a timer.
                upgradeTitle.text = sim.IsEndlessRun
                    ? (sim.Wave >= sim.Config.endless.wavesPerCycle ? "BOSS DEFEATED" : $"WAVE {sim.Wave} COMPLETE")
                    : $"ENCOUNTER {sim.TransitionsReached} CLEARED";
                // D96 caption (owner's words): upgrades are kept now, and taking one costs life.
                upgradeNote.text = "You rely on borrowed power — and it comes with a price.";
                ShowSwapTargets(-1);   // a new choice always opens on the cards, never mid-swap
                FillCards();
            }
            upgradeDim.SetActive(upgrade);

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

        /// <summary>Name plus rank, as the HUD shows it ("Echo Volley 2"; rank 1 is implied).</summary>
        static string NameWithRank(UpgradeOffer o) => UpgradeInfo.Name(o.Id) + (o.Rank > 1 ? $" {o.Rank}" : "");

        /// <summary>
        /// D96/D100: what a paid pick costs, in display points and as a share of life. The clock
        /// is paused during the choice, so this is exactly what the click will take (R13e).
        /// </summary>
        string Price() =>
            $"<color=#FF6B6B>{LifeDisplay.Points(sim.TakeCost)} life ({Mathf.RoundToInt(sim.TakeCostFraction * 100f)}%)</color>";

        void FillCards()
        {
            var t = sim.Config.upgrades;
            var held = sim.HeldUpgrades;
            if (held.Count == 0) upgradeHolding.text = "";
            else
            {
                var names = new string[held.Count];
                for (int j = 0; j < held.Count; j++) names[j] = NameWithRank(held[j]);
                upgradeHolding.text = "Holding: " + string.Join(", ", names);
            }
            bool locked = sim.UpgradesLocked;
            // Adding now would fill the set: warn that this is the last new card (R13c).
            bool lastCard = held.Count == Mathf.Max(1, t.maxHeld) - 1;
            for (int i = 0; i < cardRows.Length; i++)
            {
                bool has = i < sim.Offers.Count;
                cardRows[i].SetActive(has);
                if (!has) continue;
                var o = sim.Offers[i];
                bool rankUp = sim.IsRankUp(o);
                // The description is built from the live tuning, so the card can never
                // promise a number the sim does not use.
                string body = $"<b><color=#FAD14F>{UpgradeInfo.Name(o.Id)}</color></b>{(o.Rank > 1 ? $"  <size=20>rank {o.Rank}</size>" : "")}\n"
                    + $"<size=21>{UpgradeInfo.Describe(o.Id, o.Rank, t)}</size>";
                if (!rankUp && locked) body += $"\n<size=20><color=#FF6B6B>Locked: {held.Count} held</color></size>";
                else if (!rankUp && lastCard) body += "\n<size=20><b>Your last card: after this, only rank-ups.</b></size>";
                cardTexts[i].text = body;

                // The button column follows D96's table: rank-up = one paid button; a new card =
                // Take (nothing held) or Add + Swap (1-3 held); a new card while locked = none.
                bool showMain = rankUp || !locked;
                bool showSwap = !rankUp && sim.CanSwap && held.Count <= MaxSwapTargets;
                cardMain[i].gameObject.SetActive(showMain);
                cardSwap[i].gameObject.SetActive(showSwap);
                if (showMain)
                {
                    string verb = rankUp ? $"Rank up to {o.Rank}" : held.Count == 0 ? "Take" : "Add";
                    cardMain[i].GetComponentInChildren<Text>().text = $"{verb} — {Price()}";
                    // Alone, the paid button sits mid-row; with Swap under it, in the top half.
                    Ui.Place((RectTransform)cardMain[i].transform, new Vector2(1, showSwap ? 1 : 0.5f),
                        new Vector2(-12, showSwap ? -10 : 0), new Vector2(290, 48));
                }
                if (showSwap)
                    Ui.Place((RectTransform)cardSwap[i].transform, new Vector2(1, 0), new Vector2(-12, 10), new Vector2(290, 48));
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
            var held = sim != null ? sim.HeldUpgrades : null;
            for (int j = 0; j < swapTargets.Length; j++)
            {
                bool show = picking && j < held.Count;
                swapTargets[j].gameObject.SetActive(show);
                if (show) swapTargets[j].GetComponentInChildren<Text>().text = $"Replace {NameWithRank(held[j])}";
            }
            // The cards, Continue and Retire make way for the target row; FillCards brings the cards back.
            if (picking) foreach (var row in cardRows) row.SetActive(false);
            continueButton.gameObject.SetActive(!picking);
            retireButton.gameObject.SetActive(!picking && sim != null && sim.IsEndlessRun);
            if (picking)
            {
                swapTitle.text = $"Swap in <color=#FAD14F>{UpgradeInfo.Name(sim.Offers[card].Id)}</color>: replace which? (free)";
                // Back is the safe default: a stray Enter returns to the cards instead of discarding one.
                Select(swapBack);
            }
        }

        void UpdateBanner(RunState state)
        {
            if (state == RunState.BossIntro && !bannerWasIntro)
            {
                bannerWasIntro = true;
                bannerShownAt = Time.unscaledTime;
                bannerName.text = sim.Config.collector.displayName.ToUpperInvariant();
                banner.gameObject.SetActive(true);
            }
            if (!banner.gameObject.activeSelf) return;
            // The run can end inside the fade (a fast kill, a death): the results must never
            // sit on top of a half-faded boss name.
            if (state == RunState.Results)
            {
                banner.gameObject.SetActive(false);
                return;
            }

            float age = Time.unscaledTime - bannerShownAt;
            // Pop in: overshoot from 1.35x down to 1x in a quarter second, then hold, then fade
            // once the fight has started (the banner never covers live combat for long).
            float pop = Mathf.Clamp01(age / 0.25f);
            float scale = Mathf.Lerp(1.35f, 1f, 1f - (1f - pop) * (1f - pop));
            bannerName.rectTransform.localScale = Vector3.one * scale;
            float fade = state == RunState.BossIntro || state == RunState.Paused ? 1f
                : 1f - Mathf.Clamp01((age - BannerHold) / BannerFade);
            bannerGroup.alpha = Mathf.Min(pop * 2f, fade);
            if (fade <= 0f) banner.gameObject.SetActive(false);
        }

        void FillResults(RunSummary s)
        {
            switch (s.Reason)
            {
                case RunEndReason.Victory: resultsTitle.text = "VICTORY"; resultsTitle.color = Ui.Accent; break;
                case RunEndReason.Death: resultsTitle.text = "DEFEATED"; resultsTitle.color = new Color(1f, 0.4f, 0.45f); break;
                case RunEndReason.TimeExpired: resultsTitle.text = "TIME EXPIRED"; resultsTitle.color = new Color(1f, 0.6f, 0.3f); break;
                case RunEndReason.Retired: resultsTitle.text = "RETIRED"; resultsTitle.color = Ui.Accent; break;
                default: resultsTitle.text = s.Reason.ToString().ToUpperInvariant(); resultsTitle.color = Ui.Ink; break;
            }
            int secs = Mathf.FloorToInt(s.Duration);
            string bonus = s.VictoryBonus > 0 ? $"   (time bonus +{s.VictoryBonus})" : "";
            // Centred "label: value" lines: the built-in font is proportional, so space-padded
            // columns would not line up.
            resultsBody.text =
                $"{ReasonText(s)}\n" +
                $"Score: {s.Score}{bonus}\n" +
                $"Best volley: {s.BestVolleyKills} kill{(s.BestVolleyKills == 1 ? "" : "s")}\n" +
                $"Hit rate: {Mathf.RoundToInt(s.HitRate * 100f)}%  ({s.PacketsHit}/{s.PacketsReleased} packets)\n" +
                // D100: life reads x10 on screen, so these use the same points as the pops.
                $"Health lost to hits: {LifeDisplay.Points(s.DamageTaken)}   Health gained: {LifeDisplay.Points(s.SecondsGained)}\n" +
                $"Health sacrificed: {LifeDisplay.Points(s.SecondsSacrificed)} ({s.UpgradesPaidFor} upgrade{(s.UpgradesPaidFor == 1 ? "" : "s")})   Most held: {s.MostUpgradesHeld}\n" +
                $"Backfires: {s.Backfires}   Swaps: {s.Swaps}   Average power: x{s.AverageFirePower:0.00}\n" +
                $"Duration: {secs / 60}:{secs % 60:00}   Kills: {s.Kills}";
        }

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
