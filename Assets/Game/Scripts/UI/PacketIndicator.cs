using System.Collections.Generic;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Packet slots and catch readiness (Phase 3). One panel per slot, each with its OWN
    /// shrinking countdown bar, so two stored packets are never merged into a single timer:
    /// the player has to read which bundle fires first.
    ///
    /// Panels are PINNED to slot indices (D34, superseding D18's oldest-first order): with
    /// early release and Q selection, "slot 2" must stay the same bundle in the same place,
    /// or the selection highlight would appear to jump when the other slot fires. The label
    /// lists everything the slot holds (e.g. "Rocket x1  Bolt x2"), not one dominant type.
    ///
    /// Like the rest of the HUD it only polls the sim, so a restart needs just Bind().
    /// </summary>
    public sealed class PacketIndicator : MonoBehaviour
    {
        sealed class Panel
        {
            public Image Back, Fill;
            public Text Label;
            public Outline Highlight;
        }

        static readonly Color FillColor = new Color(0.45f, 0.95f, 1f);
        static readonly Color UrgentColor = new Color(1f, 1f, 1f);
        static readonly Color EmptyBack = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color UsedBack = new Color(0f, 0.1f, 0.15f, 0.7f);
        static readonly Color LockedBack = new Color(0.2f, 0.05f, 0.1f, 0.7f);
        static readonly Color RejectRed = new Color(1f, 0.3f, 0.3f);

        ArenaSim sim;
        RectTransform root;
        readonly List<Panel> panels = new List<Panel>();
        Image catchFill;
        Text catchLabel, hint;
        static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f);

        const float PanelW = 270f, PanelH = 70f, Gap = 14f;

        public static PacketIndicator Create(RectTransform parent)
        {
            var rt = Ui.Place(Ui.Rect("Packets", parent), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(600, 110));
            var pi = rt.gameObject.AddComponent<PacketIndicator>();
            pi.root = rt;
            pi.BuildCatchBar();
            return pi;
        }

        float handFullUntil;

        public void Bind(ArenaSim s)
        {
            if (sim != null) sim.Events.CaptureRejected -= OnRejected;
            sim = s;
            handFullUntil = 0f;
            sim.Events.CaptureRejected += OnRejected;
        }

        // D89: the one rejection Q would have prevented gets its own cue on the hand itself,
        // so "why didn't that catch?" reads as "my hand was full", not as a missed click.
        void OnRejected(Vector2 at, Combat.CaptureResult r)
        {
            if (r == Combat.CaptureResult.HandFull) handFullUntil = Time.unscaledTime + 0.3f;
        }

        void OnDestroy() { if (sim != null) sim.Events.CaptureRejected -= OnRejected; }

        void BuildCatchBar()
        {
            var bg = Ui.Image("CatchBar", root, new Color(0, 0, 0, 0.55f));
            Ui.Place(bg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(240, 12));
            catchFill = Ui.Image("Fill", bg.transform, FillColor);
            var fr = catchFill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            catchLabel = Ui.Label("CatchLabel", root, "CATCH", 18);
            Ui.Place(catchLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(240, 22));
            hint = Ui.Label("Hint", root, "LMB catch   RMB fire   Q freeze / swap   Expiry backfires!", 16);
            hint.color = new Color(1f, 1f, 1f, 0.55f);
            // The three-row panels reach y=120; keep the hint above their selection outline.
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 144), new Vector2(600, 20));
        }

        Panel AddPanel()
        {
            var p = new Panel { Back = Ui.Image("Slot", root, EmptyBack) };
            p.Fill = Ui.Image("Countdown", p.Back.transform, FillColor);
            var fr = p.Fill.rectTransform;
            // A thin bar along the panel's bottom edge; its width shrinks with time left.
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 0); fr.pivot = new Vector2(0, 0);
            fr.offsetMin = Vector2.zero; fr.offsetMax = new Vector2(0, 8);
            p.Highlight = p.Back.gameObject.AddComponent<Outline>();
            p.Highlight.effectColor = SelectedColor;
            p.Highlight.effectDistance = new Vector2(3, -3);
            p.Label = Ui.Label("Text", p.Back.transform, "", 17);
            Ui.Stretch(p.Label.rectTransform);
            p.Label.rectTransform.offsetMin = new Vector2(0, 8);
            panels.Add(p);
            return p;
        }

        void LateUpdate()
        {
            if (sim == null) return;
            hint.enabled = DisplayOptions.ShowHints;
            double now = sim.Clock.Now;
            int slots = sim.Packets.SlotCount;
            while (panels.Count < slots) AddPanel();

            float total = slots * PanelW + (slots - 1) * Gap;
            var store = sim.Packets;
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                bool shown = i < slots;
                p.Back.gameObject.SetActive(shown);
                if (!shown) continue;
                Ui.Place(p.Back.rectTransform, new Vector2(0.5f, 0),
                    new Vector2(-total * 0.5f + PanelW * 0.5f + i * (PanelW + Gap), 50), new Vector2(PanelW, PanelH));

                bool selected = i == store.SelectedSlot;
                p.Highlight.enabled = selected;
                bool handFullFlash = selected && Time.unscaledTime < handFullUntil;
                // Red outline while the HandFull cue runs (D89), the usual gold otherwise.
                p.Highlight.effectColor = handFullFlash ? RejectRed : SelectedColor;
                var pk = store.InSlot(i);
                string tag = (selected ? "> " : "") + (i + 1);
                if (pk != null)
                {
                    float left = pk.Remaining(now);
                    float frac = Mathf.Clamp01(left / Mathf.Max(0.01f, pk.Lifetime));
                    p.Back.color = UsedBack;
                    p.Fill.enabled = true;
                    p.Fill.rectTransform.anchorMax = new Vector2(frac, 0);
                    // D90: an unprimed hex is drawn dimmed and labelled UNSTABLE until it can fire.
                    bool primed = sim.IsPrimed(pk);
                    var fill = selected && left < 0.5f ? UrgentColor : FillColor;
                    if (!primed) fill.a = 0.5f;
                    p.Fill.color = fill;
                    // FirePower, not Power: a fused packet shows the +25% it will actually fire with.
                    string state = (handFullFlash ? "HAND FULL — Q" : !primed ? "UNSTABLE" : selected ? "DECAYING" : "FROZEN") + (pk.PowerScale > 1f ? "  FUSED" : "");
                    p.Label.text = $"{tag}  {Contents(pk)}\n{pk.CapacityUsed}/{pk.Capacity}   x{pk.FirePower(sim.Stats.PowerPerSecond):0.00}   {left:0.0}s\n{state}";
                }
                else
                {
                    p.Back.color = store.IsLocked(i) ? LockedBack : EmptyBack;
                    p.Fill.enabled = false;
                    // Fusion lock (section 5): the slot is unusable until the fused packet leaves.
                    p.Label.text = store.IsLocked(i) ? $"{tag}  LOCKED\n(fused)" : $"{tag}  empty";
                }
            }

            // Catch readiness: drains while the window + recovery run, full when ready again.
            var c = sim.Capture;
            float span = Mathf.Max(0.0001f, (float)(c.RecoveryEndsAt - c.WindowOpensAt));
            float ready = c.IsReady(now) ? 1f : Mathf.Clamp01((float)((now - c.WindowOpensAt) / span));
            catchFill.rectTransform.anchorMax = new Vector2(ready, 1);
            catchFill.color = ready >= 1f ? FillColor : new Color(0.3f, 0.5f, 0.55f);
            catchLabel.text = c.IsWindowOpen(now) ? "CATCHING" : ready >= 1f ? "CATCH" : "...";
        }

        static readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

        /// <summary>
        /// Every kind the packet holds with its count, heaviest first ("Rocket x1  Bolt x2"),
        /// so a mixed packet is never mislabelled as a single type.
        /// </summary>
        static string Contents(Combat.CapturedPacket pk)
        {
            sb.Clear();
            for (int kind = (int)Core.AttackKind.Riposte; kind >= 0; kind--)
            {
                int n = 0;
                foreach (var s in pk.Payloads) if ((int)s.Kind == kind) n++;
                if (n == 0) continue;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append((Core.AttackKind)kind).Append(" x").Append(n);
            }
            return sb.ToString();
        }
    }
}
