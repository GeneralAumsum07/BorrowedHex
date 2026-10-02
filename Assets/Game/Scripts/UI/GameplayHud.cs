using System;
using System.Collections.Generic;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// In-run HUD. Polls durable state and subscribes only for a transient clock flash;
    /// rebinding detaches the old sim so a stale run can never update the screen.
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        ArenaSim sim;
        Image dashFill;
        Text dashLabel;
        Image lifeHeart, lifeBarBg, lifeBarFill;
        Text objectiveLabel;
        Text scoreLabel;
        Image bossBarBg, bossBarFill;
        public Button PauseButton { get; private set; }
        public Button ResetButton { get; private set; }

        public static GameplayHud Create(Canvas canvas, Action onPause, Action onReset)
        {
            var root = Ui.Stretch(Ui.Rect("Hud", canvas.transform));
            var hud = root.gameObject.AddComponent<GameplayHud>();
            hud.Build(root, onPause, onReset);
            return hud;
        }

        void Build(RectTransform root, Action onPause, Action onReset)
        {
            var dashBg = Ui.Image("DashBar", root, new Color(0, 0, 0, 0.55f));
            Ui.Place(dashBg.rectTransform, new Vector2(0, 1), new Vector2(32, -86), new Vector2(220, 18));
            dashFill = Ui.Image("Fill", dashBg.transform, Ui.Accent);
            var fr = dashFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 1); fr.pivot = new Vector2(0, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            dashLabel = Ui.Label("DashLabel", root, "DASH", 20, TextAnchor.MiddleLeft);
            Ui.Place(dashLabel.rectTransform, new Vector2(0, 1), new Vector2(262, -83), new Vector2(160, 24));

            // D65: life is shown as a heart plus a draining bar instead of a numeric timer.
            // The life clock still drains with time underneath; the player reads "how full
            // am I" at a glance rather than doing mm:ss arithmetic mid-fight.
            lifeHeart = Ui.Image("LifeHeart", root, Color.white);
            lifeHeart.sprite = Presentation.PixelSprites.Heart();
            lifeHeart.preserveAspect = true;
            Ui.Place(lifeHeart.rectTransform, new Vector2(0.5f, 1), new Vector2(-210, -24), new Vector2(52, 52));
            // Bar sits to the right of the heart, vertically centred on it (heart centre is -50).
            lifeBarBg = Ui.Image("LifeBar", root, new Color(0, 0, 0, 0.6f));
            Ui.Place(lifeBarBg.rectTransform, new Vector2(0.5f, 1), new Vector2(26, -38), new Vector2(400, 24));
            lifeBarFill = Ui.Image("Fill", lifeBarBg.transform, Color.green);
            var lf = lifeBarFill.rectTransform;
            lf.anchorMin = Vector2.zero; lf.anchorMax = Vector2.one; lf.pivot = new Vector2(0, 0.5f);
            lf.offsetMin = lf.offsetMax = Vector2.zero;
            // Section 6: "present the objective clearly from the start" — what phase this is.
            objectiveLabel = Ui.Label("Objective", root, "", 22);
            Ui.Place(objectiveLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(800, 30));
            objectiveLabel.color = new Color(1, 1, 1, 0.8f);
            scoreLabel = Ui.Label("Score", root, "", 26, TextAnchor.MiddleLeft);
            Ui.Place(scoreLabel.rectTransform, new Vector2(0, 1), new Vector2(32, -116), new Vector2(400, 34));

            bossBarBg = Ui.Image("BossBar", root, new Color(0, 0, 0, 0.6f));
            Ui.Place(bossBarBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(560, 20));
            bossBarFill = Ui.Image("Fill", bossBarBg.transform, new Color(0.9f, 0.25f, 0.35f));
            var bf = bossBarFill.rectTransform;
            bf.anchorMin = Vector2.zero; bf.anchorMax = Vector2.one; bf.pivot = new Vector2(0, 0.5f);
            bf.offsetMin = bf.offsetMax = Vector2.zero;
            bossBarBg.gameObject.SetActive(false);

            PauseButton = Ui.Button("Pause", root, "II", onPause, 30);
            Ui.Place((RectTransform)PauseButton.transform, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(64, 64));
            ResetButton = Ui.Button("Reset", root, "Reset", onReset, 22);
            Ui.Place((RectTransform)ResetButton.transform, new Vector2(1, 1), new Vector2(-104, -28), new Vector2(110, 64));

            Packets = PacketIndicator.Create(root);
        }

        public PacketIndicator Packets { get; private set; }

        public void Bind(ArenaSim s)
        {
            if (sim != null) sim.Events.LifeClockChanged -= FlashClock;
            sim = s;
            sim.Events.LifeClockChanged += FlashClock;
            clockFlashUntil = 0;
            Packets.Bind(s);
        }

        float clockFlashUntil;
        Color clockFlashColor;
        void FlashClock(float delta, Vector2 at)
        {
            // Losses only (D66): hits and backfires are what the player must notice. Kill gains
            // flashed too often to mean anything and already get a floating "+N" at the kill.
            if (delta >= 0) return;
            clockFlashUntil = Time.unscaledTime + 0.25f;
            clockFlashColor = new Color(1f, 0.3f, 0.3f);
        }
        void OnDestroy() { if (sim != null) sim.Events.LifeClockChanged -= FlashClock; }

        readonly List<Button> devButtons = new List<Button>();

        /// <summary>
        /// Development buttons stacked under Reset (summon enemies, toggle auto-spawn, clear). They share
        /// Reset's visibility: sandbox/debug runs only, never in a scored run.
        /// </summary>
        public Button AddDevButton(string label, Action onClick)
        {
            var b = Ui.Button("Dev_" + label, transform, label, onClick, 20);
            Ui.Place((RectTransform)b.transform, new Vector2(1, 1), new Vector2(-28, -104 - devButtons.Count * 56), new Vector2(186, 48));
            b.gameObject.SetActive(ResetButton.gameObject.activeSelf);
            devButtons.Add(b);
            return b;
        }

        /// <summary>The dev reset only exists in sandbox/debug runs, never in a scored run.</summary>
        public void SetResetVisible(bool on)
        {
            ResetButton.gameObject.SetActive(on);
            foreach (var b in devButtons) b.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (sim == null) return;
            var p = sim.Player;
            // Cooldown progress from the gameplay clock, so it freezes while paused.
            double now = sim.Clock.Now;
            float cd = Mathf.Max(0.0001f, sim.Stats.DashCooldown);
            float ready = Mathf.Clamp01(1f - (float)((p.DashReadyAt - now) / cd));
            dashFill.rectTransform.anchorMax = new Vector2(ready, 1);
            dashFill.color = ready >= 1f ? Ui.Accent : new Color(0.55f, 0.48f, 0.3f);
            dashLabel.text = ready >= 1f ? "DASH" : "...";

            // Life and elapsed time are independent: gains must not rewind enemies.
            // Fraction of the cap (StartingSeconds), the same cap kill rewards clamp to,
            // so a full bar always means "cannot gain more".
            float frac = Mathf.Clamp01(sim.LifeSeconds / Mathf.Max(0.0001f, sim.Stats.StartingSeconds));
            lifeBarFill.rectTransform.anchorMax = new Vector2(frac, 1);
            // Hue 0.33 (green) -> 0 (red) passes through yellow on the way, which reads as a
            // traffic light without needing a three-stop gradient.
            lifeBarFill.color = Color.HSVToRGB(frac * 0.33f, 0.85f, 0.95f);
            // Hits and backfires flash the bar background red (gains do not, D66), replacing
            // the old clock-text flash so the feedback stays on the life display.
            bool flashing = Time.unscaledTime < clockFlashUntil;
            lifeBarBg.color = flashing ? clockFlashColor * new Color(1, 1, 1, 0.8f) : new Color(0, 0, 0, 0.6f);
            // Low life: the heart beats (unscaled time, so it keeps beating while paused; it is
            // a reminder, not gameplay). Below 1/6 of the cap = 30 s at 180, the old red-text threshold.
            float beat = frac < 1f / 6f ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            lifeHeart.rectTransform.localScale = new Vector3(beat, beat, 1);
            int n = sim.Config.shortMode.encounterCount;
            objectiveLabel.text = !sim.IsShortRun ? "SANDBOX"
                : sim.Encounter < n ? $"ENCOUNTER {sim.Encounter + 1}/{n} — KILL ALL ENEMIES ({sim.EnemiesLeftInEncounter()} LEFT)"
                : $"DEFEAT {sim.Config.collector.displayName.ToUpperInvariant()}";

            var score = sim.Score;
            scoreLabel.text = score.Multiplier > 1f ? $"SCORE {score.Score}   x{score.Multiplier:0.00}" : $"SCORE {score.Score}";
            scoreLabel.color = score.Multiplier > 1f ? Ui.Accent : Ui.Ink;

            var boss = sim.LivingBoss();
            bossBarBg.gameObject.SetActive(boss != null);
            if (boss != null)
                bossBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(boss.Health / Mathf.Max(1f, boss.MaxHealth)), 1);
        }
    }
}
