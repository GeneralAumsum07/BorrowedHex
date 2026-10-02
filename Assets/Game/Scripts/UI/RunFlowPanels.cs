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
        Text upgradeExpiring;
        Text upgradeNote;
        Button retireButton;
        // One button per offer slot. Offers are re-read from the sim each time the panel
        // opens, so the cards never show a previous transition's draw.
        readonly Button[] cards = new Button[3];
        readonly Text[] cardTexts = new Text[3];

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

        public static RunFlowPanels Create(Canvas canvas, Action<int> onChoose, Action onPlayAgain, Action onMainMenu, Action onRetire = null)
        {
            var root = Ui.Stretch(Ui.Rect("RunFlow", canvas.transform));
            var p = root.gameObject.AddComponent<RunFlowPanels>();
            p.Build(root, onChoose, onPlayAgain, onMainMenu, onRetire);
            return p;
        }

        void Build(RectTransform root, Action<int> onChoose, Action onPlayAgain, Action onMainMenu, Action onRetire)
        {
            // --- Upgrade choice: three cards, each a button. Section 5 offers no skip: a
            // pick is free and only lasts one encounter, so there is nothing to decline.
            var dim = Ui.Image("UpgradeChoice", root, new Color(0, 0, 0, 0.55f));
            Ui.Stretch(dim.rectTransform);
            upgradeDim = dim.gameObject;
            var panel = Ui.Image("Panel", dim.transform, Ui.Panel);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = Ui.Column(panel.rectTransform, 14);
            col.padding = new RectOffset(40, 40, 32, 36);
            upgradeTitle = Ui.Sized(Ui.Label("Title", panel.transform, "", 46), 64);
            upgradeTitle.color = Ui.Accent;
            upgradeExpiring = Ui.Sized(Ui.Label("Expiring", panel.transform, "", 22), 30);
            upgradeExpiring.color = new Color(1f, 0.6f, 0.5f);
            var note = Ui.Sized(Ui.Label("Note", panel.transform, "", 22), 30);
            note.color = new Color(1, 1, 1, 0.7f);
            upgradeNote = note;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;   // captured per card; the loop variable would be 3 for all of them
                cards[i] = Ui.Sized(Ui.Button($"Card{i}", panel.transform, "", () => onChoose(index), 24), 112);
                cardTexts[i] = cards[i].GetComponentInChildren<Text>();
                cardTexts[i].supportRichText = true;
                // Inset so long descriptions wrap inside the card instead of touching its edge.
                cardTexts[i].rectTransform.offsetMin = new Vector2(20, 8);
                cardTexts[i].rectTransform.offsetMax = new Vector2(-20, -8);
            }
            // Endless only (section 6): retiring is offered between waves, i.e. here. Built
            // after the cards so it sits under them, smaller, and is never the default focus.
            retireButton = Ui.Sized(Ui.Button("Retire", panel.transform, "Retire (end run, keep progress)", () => onRetire?.Invoke(), 22), 52);
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
            resultsBody = Ui.Sized(Ui.Label("Body", rp.transform, "", 28, TextAnchor.UpperCenter), 270);
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
                if (sim.IsEndlessRun)
                {
                    // The wave-six choice is the one deferred until the boss fell.
                    upgradeTitle.text = sim.Wave >= sim.Config.endless.wavesPerCycle ? "BOSS DEFEATED" : $"WAVE {sim.Wave} COMPLETE";
                    upgradeNote.text = "Choose one. It lasts until the next choice.";
                }
                else
                {
                    // Encounters end on a full clear now (D50), not on surviving a timer.
                    upgradeTitle.text = $"ENCOUNTER {sim.TransitionsReached} CLEARED";
                    upgradeNote.text = "Choose one. It lasts until the next encounter is cleared.";
                }
                retireButton.gameObject.SetActive(sim.IsEndlessRun);
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

        void FillCards()
        {
            var t = sim.Config.upgrades;
            upgradeExpiring.text = sim.ExpiredUpgrade.HasValue
                ? $"Expired: {UpgradeInfo.Name(sim.ExpiredUpgrade.Value.Id)}" : "";
            for (int i = 0; i < cards.Length; i++)
            {
                bool has = i < sim.Offers.Count;
                cards[i].gameObject.SetActive(has);
                if (!has) continue;
                var o = sim.Offers[i];
                // The description is built from the live tuning, so the card can never
                // promise a number the sim does not use.
                cardTexts[i].text = $"<b><color=#FAD14F>{UpgradeInfo.Name(o.Id)}</color></b>\n"
                    + $"<size=21>{UpgradeInfo.Describe(o.Id, o.Rank, t)}</size>";
            }
            if (sim.Offers.Count > 0) Select(cards[0]);
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
                $"Time lost to hits: {s.DamageTaken}s   Time gained: {s.SecondsGained:0.#}s\n" +
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
