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

            PauseButton = Ui.Button("Pause", root, "II", onPause, 30);
            Ui.Place((RectTransform)PauseButton.transform, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(64, 64));
            ResetButton = Ui.Button("Reset", root, "Reset", onReset, 22);
            Ui.Place((RectTransform)ResetButton.transform, new Vector2(1, 1), new Vector2(-104, -28), new Vector2(110, 64));
        }

        public void Bind(ArenaSim s) => sim = s;

        /// <summary>The dev reset only exists in sandbox/debug runs, never in a scored run.</summary>
        public void SetResetVisible(bool on) => ResetButton.gameObject.SetActive(on);

        void EnsurePips(int count)
        {
            while (pips.Count < count)
            {
                var pip = Ui.Image("Pip", pipRow, PipFull);
                pip.rectTransform.sizeDelta = new Vector2(36, 36);
                pips.Add(pip);
            }
            for (int i = 0; i < pips.Count; i++) pips[i].gameObject.SetActive(i < count);
        }

        void LateUpdate()
        {
            if (sim == null) return;
            var p = sim.Player;
            EnsurePips(p.MaxHealth);
            for (int i = 0; i < p.MaxHealth; i++) pips[i].color = i < p.Health ? PipFull : PipEmpty;

            // Cooldown progress from the gameplay clock, so it freezes while paused.
            double now = sim.Clock.Now;
            float cd = Mathf.Max(0.0001f, sim.Stats.DashCooldown);
            float ready = Mathf.Clamp01(1f - (float)((p.DashReadyAt - now) / cd));
            dashFill.rectTransform.anchorMax = new Vector2(ready, 1);
            dashFill.color = ready >= 1f ? Ui.Accent : new Color(0.55f, 0.48f, 0.3f);
            dashLabel.text = ready >= 1f ? "DASH" : "...";

            int secs = (int)now;
            clockLabel.text = $"{secs / 60}:{secs % 60:00}";
        }
    }
}
