using System;
using System.Collections.Generic;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// In-run HUD. It only READS the sim each frame (no event subscriptions), so binding a new
    /// sim on restart is a single assignment and a stale run can never update the screen.
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        ArenaSim sim;
        readonly List<Image> pips = new List<Image>();
        RectTransform pipRow;
        Image dashFill;
        Text dashLabel;
        Text clockLabel;
        Text objectiveLabel;
        Text scoreLabel;
        Image bossBarBg, bossBarFill;
        public Button PauseButton { get; private set; }
        public Button ResetButton { get; private set; }

        static readonly Color PipFull = new Color(0.95f, 0.3f, 0.42f);
        static readonly Color PipEmpty = new Color(0.25f, 0.18f, 0.25f);

        public static GameplayHud Create(Canvas canvas, Action onPause, Action onReset)
        {
            var root = Ui.Stretch(Ui.Rect("Hud", canvas.transform));
            var hud = root.gameObject.AddComponent<GameplayHud>();
            hud.Build(root, onPause, onReset);
            return hud;
        }

        void Build(RectTransform root, Action onPause, Action onReset)
        {
            pipRow = Ui.Place(Ui.Rect("Health", root), new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 44));
            var h = pipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlWidth = h.childControlHeight = false;
            // Default force-expand would spread 3 pips across the whole 400 px row.
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;

            var dashBg = Ui.Image("DashBar", root, new Color(0, 0, 0, 0.55f));
            Ui.Place(dashBg.rectTransform, new Vector2(0, 1), new Vector2(32, -86), new Vector2(220, 18));
            dashFill = Ui.Image("Fill", dashBg.transform, Ui.Accent);
            var fr = dashFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 1); fr.pivot = new Vector2(0, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            dashLabel = Ui.Label("DashLabel", root, "DASH", 20, TextAnchor.MiddleLeft);
            Ui.Place(dashLabel.rectTransform, new Vector2(0, 1), new Vector2(262, -83), new Vector2(160, 24));

            clockLabel = Ui.Label("Clock", root, "0:00", 34);
            Ui.Place(clockLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(300, 48));
            // Section 6: "present the objective clearly from the start" — what phase this is,
            // and the clock above it counts down the time left in that phase.
            objectiveLabel = Ui.Label("Objective", root, "", 22);
            Ui.Place(objectiveLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(700, 30));
            objectiveLabel.color = new Color(1, 1, 1, 0.8f);
            scoreLabel = Ui.Label("Score", root, "", 26, TextAnchor.MiddleLeft);
            Ui.Place(scoreLabel.rectTransform, new Vector2(0, 1), new Vector2(32, -116), new Vector2(400, 34));

            bossBarBg = Ui.Image("BossBar", root, new Color(0, 0, 0, 0.6f));
            Ui.Place(bossBarBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(560, 20));
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
            sim = s;
            Packets.Bind(s);
        }

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

        // Health is in half hearts (D51): one pip per heart, each an empty square with a fill
        // that covers none, half or all of it from the left.
        readonly List<RectTransform> pipFills = new List<RectTransform>();

        void EnsurePips(int count)
        {
            while (pips.Count < count)
            {
                var pip = Ui.Image("Pip", pipRow, PipEmpty);
                pip.rectTransform.sizeDelta = new Vector2(36, 36);
                var fill = Ui.Image("Fill", pip.transform, PipFull).rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                pips.Add(pip);
                pipFills.Add(fill);
            }
            for (int i = 0; i < pips.Count; i++) pips[i].gameObject.SetActive(i < count);
        }

        void LateUpdate()
        {
            if (sim == null) return;
            var p = sim.Player;
            int hearts = (p.MaxHealth + 1) / 2;
            EnsurePips(hearts);
            for (int i = 0; i < hearts; i++)
            {
                // Half hearts this pip holds: 0, 1 or 2.
                int inPip = Mathf.Clamp(p.Health - i * 2, 0, 2);
                pipFills[i].anchorMax = new Vector2(inPip * 0.5f, 1f);
                pipFills[i].gameObject.SetActive(inPip > 0);
            }

            // Cooldown progress from the gameplay clock, so it freezes while paused.
            double now = sim.Clock.Now;
            float cd = Mathf.Max(0.0001f, sim.Stats.DashCooldown);
            float ready = Mathf.Clamp01(1f - (float)((p.DashReadyAt - now) / cd));
            dashFill.rectTransform.anchorMax = new Vector2(ready, 1);
            dashFill.color = ready >= 1f ? Ui.Accent : new Color(0.55f, 0.48f, 0.3f);
            dashLabel.text = ready >= 1f ? "DASH" : "...";

            if (sim.IsShortRun)
            {
                // Count DOWN the shared run clock (D50), rounded up so "0:00" only shows at
                // the moment it runs out.
                int left = Mathf.CeilToInt(sim.SecondsLeftInRun() - 1e-4f);
                clockLabel.text = $"{left / 60}:{left % 60:00}";
                int n = sim.Config.shortMode.encounterCount;
                objectiveLabel.text = sim.Encounter < n
                    ? $"ENCOUNTER {sim.Encounter + 1}/{n} — KILL ALL ENEMIES ({sim.EnemiesLeftInEncounter()} LEFT)"
                    : $"DEFEAT {sim.Config.collector.displayName.ToUpperInvariant()}";
            }
            else
            {
                int secs = (int)now;
                clockLabel.text = $"{secs / 60}:{secs % 60:00}";
                objectiveLabel.text = "SANDBOX";
            }

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
