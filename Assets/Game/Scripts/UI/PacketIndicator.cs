using System.Collections.Generic;
using BorrowedHex.Presentation.Feedback;
using BorrowedHex.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The two hex slots and catch readiness (plan Task 14). One card per slot, each with its OWN
    /// countdown bar, so two stored packets are never merged into a single timer: the player has
    /// to read which bundle fires first.
    ///
    /// Cards are PINNED to slot indices (D34): with early release and Q selection, "slot 2" must
    /// stay the same bundle in the same place, or the selection would appear to jump when the
    /// other slot fires.
    ///
    /// What each card SAYS is decided by <see cref="SlotCue"/> (pure, tested); this class only
    /// draws it. Two channels never stand in for each other: an ivory chevron above the card is
    /// "the hand you fire from", a gold border around it is "this one is overcharged".
    ///
    /// Like the rest of the HUD it only polls the sim, so a restart needs just Bind().
    /// </summary>
    public sealed class PacketIndicator : MonoBehaviour
    {
        // Sized from the font, not the brief's 300x96: Small is 36 px a line, and two rows of it
        // plus a countdown bar need 120; four payload kinds plus the time need 400 across.
        public const float CardW = 400f, CardH = 120f, Gap = 24f;
        const float Pad = 16f, RowA = -8f, RowB = -44f, Row = 36f;
        const float TimeW = 88f, CapW = 64f, PowerW = 80f, EntryW = 68f, Glyph = 24f;
        // The hint clears the selection chevron: card top 150, border +4, gap 8, chevron 16, gap 8.
        const float CardY = 30f, HintY = 186f, RootH = 222f;
        const int BarScale = 2;   // bar.trayDark at 2 = 14 px, the same as the HUD's dash bar

        /// <summary>Every AttackKind has its own payload glyph, heaviest first (the old label order).</summary>
        static readonly (Core.AttackKind kind, string glyph)[] Kinds =
        {
            (Core.AttackKind.Riposte, "payload.riposte"), (Core.AttackKind.Rocket, "payload.rocket"),
            (Core.AttackKind.HeavyShot, "payload.heavy"), (Core.AttackKind.Bolt, "payload.bolt"),
        };

        // The countdown's own colours (spec: "consistent"): cyan as the world's returned shots,
        // ivory when the selected hand is about to go, the shared gold while overcharged.
        static readonly Color FillColor = FeedbackColors.Returned;
        static readonly Color UrgentColor = UiPalette.Ivory;

        sealed class Card
        {
            public Image Back, Marker, Border, Tray, Fill;
            public Text Time, Cap, Power;
            public readonly Image[] PayloadGlyphs = new Image[Kinds.Length];
            public readonly Text[] PayloadCounts = new Text[Kinds.Length];
            public readonly List<(Image glyph, Text word)> States = new List<(Image, Text)>();
            public string StateKey;   // rebuild the state row only when what it says changes
        }

        ArenaSim sim;
        RectTransform root;
        readonly List<Card> cards = new List<Card>();
        PulseClock[] pulse = new PulseClock[2];
        Image catchFill;
        Text catchLabel, hint;
        bool hintVisible = true;
        float handFullUntil;

        public static PacketIndicator Create(RectTransform parent)
        {
            var rt = Ui.Place(Ui.Rect("Packets", parent), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(2 * CardW + Gap, RootH));
            var pi = rt.gameObject.AddComponent<PacketIndicator>();
            pi.root = rt;
            pi.BuildCatchBar();
            return pi;
        }

        public void Bind(ArenaSim s)
        {
            if (sim != null) sim.Events.CaptureRejected -= OnRejected;
            sim = s;
            handFullUntil = 0f;
            // A rebind never carries a border or a pulse phase into the new run.
            pulse = new PulseClock[2];
            foreach (var c in cards) { c.Border.gameObject.SetActive(false); c.StateKey = null; }
            sim.Events.CaptureRejected += OnRejected;
        }

        /// <summary>The control hint; hidden in the tutorial, whose prompts teach the same keys.</summary>
        public void SetHintVisible(bool on) => hintVisible = on;

        // D89: the one rejection Q would have prevented gets its own cue on the hand itself,
        // so "why didn't that catch?" reads as "my hand was full", not as a missed click.
        void OnRejected(Vector2 at, Combat.CaptureResult r)
        {
            if (r == Combat.CaptureResult.HandFull) handFullUntil = Time.unscaledTime + 0.3f;
        }

        void OnDestroy() { if (sim != null) sim.Events.CaptureRejected -= OnRejected; }

        void BuildCatchBar()
        {
            (_, catchFill) = GameplayHud.Bar("CatchBar", root, new Vector2(0, -(RootH - 7 * BarScale)), 240, BarScale, "fill.blue");
            catchLabel = UiKit.Text("CatchLabel", root, "", UiFonts.Role.Small, TextAnchor.MiddleLeft);
            catchLabel.color = UiPalette.Muted;
            // Beside the bar, its 36-px line centred on the 14-px bar.
            Ui.Place(catchLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(128 + 60, -11), new Vector2(120, Row));
            hint = UiKit.Text("Hint", root, "Catch [LMB]  ·  Fire [RMB]  ·  Swap [Q]", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            hint.color = UiPalette.Muted;
            Ui.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, HintY), new Vector2(2 * CardW + Gap, Row));
        }

        Card AddCard(int index)
        {
            var c = new Card { Back = UiKit.Frame("Slot" + index, root, UiKit.FrameKind.Card) };
            c.Back.raycastTarget = false;   // catches land anywhere, including over the slots
            var back = c.Back.transform;

            // Gold border: a 2-art-px ring sliced around the card, 4 px outside it, so it never
            // covers the card's own frame art. White texture, tinted: the colour is exactly
            // FeedbackColors.Overcharge with the cue's alpha.
            c.Border = Ui.Image("Border", back, Color.white);
            c.Border.sprite = RingSprite();
            c.Border.type = Image.Type.Sliced;
            c.Border.fillCenter = false;
            c.Border.raycastTarget = false;
            Ui.Stretch(c.Border.rectTransform);
            c.Border.rectTransform.offsetMin = new Vector2(-4, -4);
            c.Border.rectTransform.offsetMax = new Vector2(4, 4);
            c.Border.gameObject.SetActive(false);

            // Ivory chevron above the card, pointing at it: a separate Image, never an outline,
            // so it cannot be confused with the border.
            c.Marker = Ui.Image("Marker", back, UiPalette.Ivory);
            c.Marker.sprite = ChevronSprite();
            c.Marker.raycastTarget = false;
            Ui.Place(c.Marker.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 4 + 8), new Vector2(32, 16));
            c.Marker.rectTransform.pivot = new Vector2(0.5f, 0);   // its bottom sits 8 px above the border (which is 4 px out)

            for (int k = 0; k < Kinds.Length; k++)
            {
                var g = Ui.Image("Payload" + k, back, Color.white);
                g.sprite = UiGlyphs.Get(Kinds[k].glyph);
                g.preserveAspect = true; g.raycastTarget = false;
                c.PayloadGlyphs[k] = g;
                var n = UiKit.Text("Count" + k, back, "", UiFonts.Role.Small, TextAnchor.MiddleLeft);
                n.color = UiPalette.Ivory;
                c.PayloadCounts[k] = n;
            }

            // Fixed rects, right-aligned: the numbers never jump as they change (spec).
            c.Time = Num(back, "Time", new Vector2(CardW - Pad - TimeW, RowA), TimeW, UiPalette.Ivory);
            c.Power = Num(back, "Power", new Vector2(CardW - Pad - PowerW, RowB), PowerW, UiPalette.Honey);
            c.Cap = Num(back, "Capacity", new Vector2(CardW - Pad - PowerW - 8 - CapW, RowB), CapW, UiPalette.Muted);

            (c.Tray, c.Fill) = GameplayHud.Bar("Countdown", back, new Vector2(0, -(CardH - 12 - 7 * BarScale)), CardW - 2 * Pad, BarScale, "fill.blue");
            cards.Add(c);
            return c;
        }

        static Text Num(Transform parent, string name, Vector2 at, float w, Color color)
        {
            var t = UiKit.Text(name, parent, "", UiFonts.Role.Small, TextAnchor.MiddleRight);
            t.color = color;
            Ui.Place(t.rectTransform, new Vector2(0, 1), at, new Vector2(w, Row));
            return t;
        }

        void LateUpdate()
        {
            if (sim == null) return;
            hint.enabled = DisplayOptions.ShowHints && hintVisible;
            double now = sim.Clock.Now;
            var store = sim.Packets;
            int slots = store.SlotCount;
            while (cards.Count < slots) AddCard(cards.Count);
            if (pulse.Length < slots) System.Array.Resize(ref pulse, slots);

            float total = slots * CardW + (slots - 1) * Gap;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                bool shown = i < slots;
                c.Back.gameObject.SetActive(shown);
                if (!shown) continue;
                Ui.Place(c.Back.rectTransform, new Vector2(0.5f, 0), new Vector2(-total * 0.5f + CardW * 0.5f + i * (CardW + Gap), CardY), new Vector2(CardW, CardH));

                var pk = store.InSlot(i);
                bool selected = i == store.SelectedSlot;
                bool over = pk != null && pk.IsOvercharged(sim.Stats.Power);   // the slot's own packet only (spec)
                double since = SlotCue.Track(ref pulse[i], pk, pk != null && selected && over, now);
                var cue = SlotCue.Evaluate(new SlotInput
                {
                    HasPacket = pk != null, Locked = store.IsLocked(i), Selected = selected,
                    Primed = pk == null || sim.IsPrimed(pk), Overcharged = over, Fused = pk != null && pk.PowerScale > 1f,
                    HandFull = selected && Time.unscaledTime < handFullUntil,
                    ReduceFlashes = DisplayOptions.ReduceFlashes, Now = now, OverchargeSince = since,
                });

                c.Marker.gameObject.SetActive(cue.Marker);
                c.Marker.color = cue.MarkerColor;
                c.Border.gameObject.SetActive(cue.Border);
                if (cue.Border) c.Border.color = cue.BorderColor;
                SetStates(c, cue);

                if (pk != null)
                {
                    float left = pk.Remaining(now);
                    c.Tray.gameObject.SetActive(true);
                    c.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left / Mathf.Max(0.01f, pk.Lifetime)), 1);
                    // D93: inside the zone "about to expire" is exactly the moment to fire, so gold
                    // replaces the urgent colour rather than competing with it.
                    var fill = cue.CountdownGold ? FeedbackColors.Overcharge : selected && left < 0.5f ? UrgentColor : FillColor;
                    if (!sim.IsPrimed(pk)) fill.a = 0.5f;   // D90: an unprimed hex is drawn dimmed
                    c.Fill.color = fill;
                    c.Time.text = $"{left:0.0} s";
                    // FirePower, not Power: a fused packet shows the +25% it will actually fire with.
                    c.Power.text = $"x{pk.FirePower(sim.Stats.Power):0.00}";
                    c.Cap.text = $"{pk.CapacityUsed}/{pk.Capacity}";
                    SetPayloads(c, pk);
                }
                else
                {
                    c.Tray.gameObject.SetActive(false);   // an empty slot has no clock
                    c.Time.text = c.Power.text = c.Cap.text = "";
                    SetPayloads(c, null);
                }
            }

            // Catch readiness: drains while the window + recovery run, full when ready again.
            var cap = sim.Capture;
            float span = Mathf.Max(0.0001f, (float)(cap.RecoveryEndsAt - cap.WindowOpensAt));
            float ready = cap.IsReady(now) ? 1f : Mathf.Clamp01((float)((now - cap.WindowOpensAt) / span));
            catchFill.rectTransform.anchorMax = new Vector2(ready, 1);
            catchFill.color = ready >= 1f ? Color.white : new Color(1f, 1f, 1f, 0.45f);   // dims, as the dash bar
            catchLabel.text = cap.IsWindowOpen(now) ? "Catching" : ready >= 1f ? "Catch" : "";
        }

        /// <summary>
        /// Every kind the packet holds with its count, heaviest first, in a fixed left-to-right
        /// run, so a mixed packet is never mislabelled as a single type (the old label's rule).
        /// </summary>
        static void SetPayloads(Card c, Combat.CapturedPacket pk)
        {
            float x = Pad;
            for (int k = 0; k < Kinds.Length; k++)
            {
                int n = 0;
                if (pk != null) foreach (var s in pk.Payloads) if (s.Kind == Kinds[k].kind) n++;
                c.PayloadGlyphs[k].gameObject.SetActive(n > 0);
                c.PayloadCounts[k].gameObject.SetActive(n > 0);
                if (n == 0) continue;
                Ui.Place(c.PayloadGlyphs[k].rectTransform, new Vector2(0, 1), new Vector2(x, RowA - (Row - Glyph) / 2), new Vector2(Glyph, Glyph));
                c.PayloadCounts[k].text = "x" + n;
                Ui.Place(c.PayloadCounts[k].rectTransform, new Vector2(0, 1), new Vector2(x + Glyph + 4, RowA), new Vector2(EntryW - Glyph - 4, Row));
                x += EntryW;
            }
        }

        /// <summary>
        /// The state row: each state's glyph with its word, left to right. When the words run out
        /// of room the remaining states keep their glyph and drop the word: the symbol is the
        /// constant, the word is the gloss. Rebuilt only when the cue's text changes.
        /// </summary>
        void SetStates(Card c, in SlotCue cue)
        {
            string key = cue.Label + "|" + string.Join(",", cue.States);
            if (key == c.StateKey) return;
            c.StateKey = key;
            foreach (var (g, w) in c.States) { g.gameObject.SetActive(false); w.gameObject.SetActive(false); }

            // Words without a glyph ("Hand full", "Decaying") come first in Label; the rest pair
            // one-to-one with States, in the same order (SlotCue.Evaluate builds both together).
            var words = cue.Label.Length > 0 ? cue.Label.Split(new[] { "  " }, System.StringSplitOptions.None) : new string[0];
            int bare = words.Length - cue.States.Length;
            float x = Pad, limit = CardW - Pad - PowerW - 8 - CapW - 8;
            for (int i = 0; i < words.Length; i++)
            {
                if (i == c.States.Count) c.States.Add(NewState(c));
                var (g, w) = c.States[i];
                string glyphId = i >= bare ? cue.States[i - bare] : null;
                if (glyphId != null)
                {
                    if (x + Glyph > limit) break;   // not even the symbol fits: stop (cannot happen with today's four states)
                    g.sprite = UiGlyphs.Get(glyphId);
                    g.color = glyphId == "state.frozen" ? FeedbackColors.Returned : Color.white;   // UiGlyphs: frozen is tinted ice blue
                    g.gameObject.SetActive(true);
                    Ui.Place(g.rectTransform, new Vector2(0, 1), new Vector2(x, RowB - (Row - Glyph) / 2), new Vector2(Glyph, Glyph));
                    x += Glyph + 4;
                }
                w.text = words[i];
                w.color = words[i] == "Hand full" ? SlotCue.RejectRed : UiPalette.Ivory;
                float ww = Mathf.Ceil(w.preferredWidth);
                if (x + ww <= limit)
                {
                    w.gameObject.SetActive(true);
                    Ui.Place(w.rectTransform, new Vector2(0, 1), new Vector2(x, RowB), new Vector2(ww, Row));
                    x += ww + 12;
                }
                else if (glyphId == null) continue;   // a bare word that does not fit is simply not said
                else x += 4;
            }
        }

        static (Image, Text) NewState(Card c)
        {
            var g = Ui.Image("State", c.Back.transform, Color.white);
            g.preserveAspect = true; g.raycastTarget = false;
            var w = UiKit.Text("StateWord", c.Back.transform, "", UiFonts.Role.Small, TextAnchor.MiddleLeft);
            g.gameObject.SetActive(false); w.gameObject.SetActive(false);
            return (g, w);
        }

        // ---- Generated sprites (PixelGeometry: no import, no licence) ----------------------
        // PPU 50 on the 100-PPU canvas, as all the UI art: 1 texel = 2 reference px.

        static Sprite ring, chevron;

        static Sprite RingSprite()
        {
            if (ring != null) return ring;
            var t = PixelGeometry.Frame(6, 2);
            ring = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 50f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
            ring.hideFlags = HideFlags.DontSave;
            return ring;
        }

        static Sprite ChevronSprite()
        {
            if (chevron != null) return chevron;
            var t = PixelGeometry.Chevron(16, 8);   // 32x16 ref: 24x12 read as a speck in the capture
            chevron = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 50f);
            chevron.hideFlags = HideFlags.DontSave;
            return chevron;
        }
    }
}
