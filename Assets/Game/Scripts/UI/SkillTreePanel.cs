using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The Skills tab of the Character screen: the Broken Accord (spec 3, plan Task 10). The
    /// twelve nodes are seals on a ritual figure (geometry in <see cref="AccordLayout"/>), and an
    /// inspector beside it says what the selected seal does and why it can or cannot be taken.
    ///
    /// Selection and purchase are separate (spec): clicking a seal only selects it, so a player
    /// can read a locked node's requirements without risk. Unlock is the one spending action, and
    /// SkillTree.TryBuy stays the single authority on whether it may happen.
    /// </summary>
    public sealed class SkillTreePanel : MonoBehaviour
    {
        // Per-node view parts, refreshed together from the node's state.
        sealed class NodeView
        {
            public SkillNode Node; public Button Button; public Image Seal, Pulse, Ring, Glyph, Lock;
            public Text Cheat, Name; public readonly List<Image> Dots = new List<Image>();
        }

        PlayerProfile profile;
        ProgressionTuning tuning;
        Action onChanged;
        SkillNode selected;
        readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        Text nameText, branchText, stateText, effectText, requireText, preText, reasonText;
        Image stateGem;
        Button unlock, reset;
        CanvasGroup unlockFade;
        Text unlockLabel;
        ConfirmDialog confirm;
        ScreenStack screens;
        // The unlock animation: dots into this node light in order from this time.
        NodeView lighting; float lightStart;

        public const float LightSeconds = 0.4f;

        public bool IsOpen => gameObject.activeInHierarchy;
        /// <summary>The selected seal, so keyboard focus starts where the inspector is.</summary>
        public GameObject DefaultFocus => selected != null ? views[selected.Id].Button.gameObject : views[SkillTree.Nodes[0].Id].Button.gameObject;
        public SkillNode Selected => selected;
        public Button UnlockButton => unlock;

        // Text and marks on the parchment need dark ink; the light palette vanishes on it.
        static readonly Color ParchInk = UiPalette.Ink;
        static readonly Color ParchSoft = new Color32(0x4A, 0x3B, 0x2A, 255);
        static readonly Color ParchWarn = new Color32(0x7A, 0x22, 0x22, 255);
        static readonly Color32 LitFill = new Color32(0x3A, 0x2A, 0x14, 255);
        static readonly Color32 DarkFill = new Color32(0x15, 0x12, 0x1C, 255);

        /// <summary>Built inside <paramref name="host"/> (CharacterScreen.Body), stretched over it.</summary>
        public static SkillTreePanel Create(RectTransform host)
        {
            var root = Ui.Stretch(Ui.Rect("SkillTree", host));
            var p = root.gameObject.AddComponent<SkillTreePanel>();
            p.Build(root);
            root.gameObject.SetActive(false);
            return p;
        }

        /// <summary>The dialog and stack Reset asks through. Unbound (tests), Reset refunds at once.</summary>
        public void Bind(ConfirmDialog dialog, ScreenStack stack) { confirm = dialog; screens = stack; }

        void Build(RectTransform root)
        {
            // "Panel" keeps the old tree's name: LayoutTests finds the view's container by it.
            var panel = Ui.Stretch(Ui.Rect("Panel", root));
            var map = Ui.Place(Ui.Rect("Map", panel), new Vector2(0, 0.5f), Vector2.zero, AccordLayout.MapSize);
            var decor = Ui.Stretch(Ui.Rect("Decor", map));
            BuildDecor(decor);
            foreach (var n in SkillTree.Nodes) BuildNode(map, decor, n);
            BuildInspector(panel);
            BuildNavigation();
        }

        // ---- Map -----------------------------------------------------------------------------

        void BuildDecor(RectTransform decor)
        {
            // The two broken rings, faint Camel, stopping short of every branch axis.
            foreach (float r in AccordLayout.RingRadii)
            {
                var tex = PixelGeometry.Ring(Mathf.RoundToInt(r / 2f), AccordLayout.BranchAxes, AccordLayout.RingGapDegrees, new Color32(0xC1, 0x91, 0x49, 110));
                Centered(decor, "Ring", Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 50f), Vector2.zero, tex.width * 2f, Color.white);
            }
            // The core: the torn Ledger scrap everything is borrowed from, the four branch
            // emblems pinned to it where their paths leave.
            Centered(decor, "Core", SealSprite(false, Mathf.RoundToInt(AccordLayout.CoreRadius), new Color32(0xC1, 0x91, 0x49, 255), new Color32(0x24, 0x1D, 0x2E, 255)), Vector2.zero, AccordLayout.CoreRadius * 2f, Color.white);
            Centered(decor, "Ledger", UiGlyphs.Get("style.collector"), Vector2.zero, 48f, Color.white);
            foreach (SkillBranch b in Enum.GetValues(typeof(SkillBranch)))
                Centered(decor, "Branch_" + b, UiGlyphs.Get(BranchGlyph(b)), AccordLayout.Direction(b) * 54f, 32f, new Color(1, 1, 1, 0.85f));
        }

        static string BranchGlyph(SkillBranch b) => b switch
        {
            SkillBranch.Precision => "branch.precision", SkillBranch.Mobility => "branch.mobility",
            SkillBranch.Resilience => "branch.resilience", _ => "branch.blood",
        };

        static Image Centered(Transform parent, string name, Sprite s, Vector2 pos, float size, Color tint)
        {
            var img = Ui.Image(name, parent, tint);
            img.sprite = s; img.raycastTarget = false;
            Ui.Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return img;
        }

        void BuildNode(RectTransform map, RectTransform decor, SkillNode n)
        {
            var v = new NodeView { Node = n };
            float size = AccordLayout.SealSize(n.Tier);
            var pos = AccordLayout.NodePosition(n);
            string id = n.Id;

            // Dots first, into Decor, so seals draw over any that run close.
            foreach (var d in AccordLayout.PathDots(n))
            {
                var dot = Ui.Image("Dot", decor, UiPalette.Muted);
                dot.raycastTarget = false;
                Ui.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), d, new Vector2(4, 4));
                v.Dots.Add(dot);
            }

            var seal = Ui.Image("Node_" + id, map, Color.white);
            Ui.Place(seal.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            v.Seal = seal;
            v.Button = seal.gameObject.AddComponent<Button>();
            v.Button.transition = Selectable.Transition.None;   // the Ivory ring is the selected look
            v.Button.onClick.AddListener(() => Click(id));
            seal.gameObject.AddComponent<SelectRelay>().Bind(this, id);
            UiTooltip.Attach(v.Button, () => $"{n.Name}  ·  {StateWord(AccordLayout.State(profile, n))}");

            int art = Mathf.RoundToInt(size / 2f);
            bool diamond = n.Tier == 3;
            // Available: a gold outline just outside the rim (pulsed in Update).
            v.Pulse = Centered(seal.transform, "Pulse", SealSprite(diamond, art + 4, new Color32(0xDC, 0xC4, 0x7C, 255), new Color32(0, 0, 0, 0)), Vector2.zero, size + 8, Color.white);
            // Selected: an Ivory ring 6 reference px outside the seal, whatever the state.
            v.Ring = Centered(seal.transform, "Selected", SealSprite(diamond, art + 10, new Color32(0xF2, 0xE8, 0xC9, 255), new Color32(0, 0, 0, 0)), Vector2.zero, size + 20, Color.white);
            v.Glyph = Centered(seal.transform, "Glyph", UiGlyphs.Get("skill." + id), Vector2.zero, 48f, Color.white);
            v.Lock = Centered(seal.transform, "Lock", UiGlyphs.Get("state.locked"), new Vector2(size / 2f - 6f, -size / 2f + 6f), 24f, Color.white);
            v.Cheat = UiKit.Text("Cheat", seal.transform, "C", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            v.Cheat.color = UiPalette.Violet;
            Ui.Place(v.Cheat.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size / 2f - 6f, -size / 2f + 6f), new Vector2(28, 40));
            v.Cheat.raycastTarget = false;

            // The name sits BESIDE the seal on its outward side (left of the left branches, right
            // of the right ones). Under the seal it landed on the next seal inward, because every
            // branch runs diagonally; level with the seal, the neighbours are always above or
            // below it. Width is capped so the outermost names stay inside the map.
            bool leftSide = pos.x < 0f;
            float inner = size / 2f + 8f;
            float width = Mathf.Min(200f, AccordLayout.MapSize.x / 2f - Mathf.Abs(pos.x) - inner);
            v.Name = UiKit.Text("Name", seal.transform, n.Name, UiFonts.Role.Small,
                                leftSide ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            Ui.Place(v.Name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(leftSide ? -inner : inner, 0), new Vector2(width, 40));
            v.Name.rectTransform.pivot = new Vector2(leftSide ? 1f : 0f, 0.5f);
            v.Name.raycastTarget = false;
            views[id] = v;
        }

        // Generated seal sprites, shared across rebuilds: one texture per shape/size/colour.
        static readonly Dictionary<string, Sprite> sealCache = new Dictionary<string, Sprite>();

        static Sprite SealSprite(bool diamond, int art, Color32 rim, Color32 fill)
        {
            string key = $"{diamond}|{art}|{rim.r},{rim.g},{rim.b},{rim.a}|{fill.r},{fill.g},{fill.b},{fill.a}";
            if (sealCache.TryGetValue(key, out var s) && s != null) return s;
            var t = diamond ? PixelGeometry.Diamond(art, rim, fill) : PixelGeometry.Disc(art, rim, fill);
            // PPU 50: one art pixel is two reference pixels, as the rest of the UI art.
            return sealCache[key] = Sprite.Create(t, new Rect(0, 0, art, art), new Vector2(0.5f, 0.5f), 50f);
        }

        // Explicit navigation from the figure's shape. Up/down walk a branch's tiers on screen
        // (outward for the upper branches, inward for the lower ones) and cross to the other
        // half at tier 1; left/right jump to the mirrored branch; right from the right half
        // reaches the Unlock button.
        void BuildNavigation()
        {
            Button At(SkillBranch b, int tier)
            {
                foreach (var v in views.Values) if (v.Node.Branch == b && v.Node.Tier == tier) return v.Button;
                return null;
            }
            foreach (var v in views.Values)
            {
                var n = v.Node;
                bool upper = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Mobility;
                bool left = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Resilience;
                SkillBranch mirror = n.Branch switch
                {
                    SkillBranch.Precision => SkillBranch.Mobility, SkillBranch.Mobility => SkillBranch.Precision,
                    SkillBranch.Resilience => SkillBranch.BloodPrice, _ => SkillBranch.Resilience,
                };
                SkillBranch below = upper ? (left ? SkillBranch.Resilience : SkillBranch.BloodPrice) : n.Branch;
                SkillBranch above = upper ? n.Branch : (left ? SkillBranch.Precision : SkillBranch.Mobility);
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (upper)
                {
                    nav.selectOnUp = At(n.Branch, n.Tier + 1);
                    nav.selectOnDown = n.Tier > 1 ? At(n.Branch, n.Tier - 1) : At(below, 1);
                }
                else
                {
                    nav.selectOnDown = At(n.Branch, n.Tier + 1);
                    nav.selectOnUp = n.Tier > 1 ? At(n.Branch, n.Tier - 1) : At(above, 1);
                }
                if (left) nav.selectOnRight = At(mirror, n.Tier);
                else { nav.selectOnLeft = At(mirror, n.Tier); nav.selectOnRight = unlock; }
                v.Button.navigation = nav;
            }
        }

        // ---- Inspector -----------------------------------------------------------------------

        void BuildInspector(RectTransform panel)
        {
            var sheet = UiKit.Frame("Inspector", panel, UiKit.FrameKind.Parchment);
            Ui.Place(sheet.rectTransform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(464, AccordLayout.MapSize.y));
            var t = sheet.transform;
            Text Line(string name, UiFonts.Role role, float y, float h, Color c, TextAnchor a = TextAnchor.UpperLeft)
            {
                var x = UiKit.Text(name, t, "", role, a);
                x.color = c;
                Ui.Place(x.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(400, h));
                return x;
            }
            nameText = Line("Name", UiFonts.Role.Sub, -40, 48, ParchInk);
            branchText = Line("Branch", UiFonts.Role.Small, -92, 40, ParchSoft);
            // State: a gem plus the word, so it never rests on the gem's colour alone.
            stateGem = Ui.Image("StateGem", t, Color.white);
            var gs = UiSkin.Sprite("gem.1");
            if (gs != null) stateGem.sprite = gs;
            stateGem.raycastTarget = false;
            Ui.Place(stateGem.rectTransform, new Vector2(0, 1), new Vector2(32, -142), new Vector2(24, 26));
            stateText = UiKit.Text("State", t, "", UiFonts.Role.Body);
            stateText.color = ParchInk;
            Ui.Place(stateText.rectTransform, new Vector2(0, 1), new Vector2(68, -134), new Vector2(364, 40));
            var rule = Ui.Image("Rule", t, new Color(ParchSoft.r, ParchSoft.g, ParchSoft.b, 0.6f));
            rule.raycastTarget = false;
            Ui.Place(rule.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(400, 2));
            effectText = Line("Effect", UiFonts.Role.Body, -204, 128, ParchInk);
            requireText = Line("Requirement", UiFonts.Role.Small, -340, 40, ParchSoft);
            preText = Line("Prerequisite", UiFonts.Role.Small, -380, 40, ParchSoft);
            reasonText = Line("Reason", UiFonts.Role.Small, -420, 80, ParchWarn);

            unlock = UiKit.Button("Unlock", t, "Unlock  ·  1 point", UnlockSelected, UiKit.Tier.Primary);
            Ui.Place((RectTransform)unlock.transform, new Vector2(0.5f, 0), new Vector2(0, 192), new Vector2(400, 72));
            // The kit's disabled sprite is the idle one, so a refused Unlock would look live:
            // fade the whole button instead (spec: never rely on one cue alone; the reason line
            // says why in words).
            unlockFade = unlock.gameObject.AddComponent<CanvasGroup>();
            unlockLabel = unlock.GetComponentInChildren<Text>();
            var note = UiKit.Text("Note", t, "Skills apply next run", UiFonts.Role.Small, TextAnchor.MiddleCenter);
            note.color = ParchSoft;
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 144), new Vector2(400, 40));
            reset = UiKit.Button("Reset", t, "Reset skills", AskReset, UiKit.Tier.Quiet);
            Ui.Place((RectTransform)reset.transform, new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(280, 56));
            // The Quiet label is Camel, too light on parchment: ink it like the rest of the sheet.
            reset.GetComponentInChildren<Text>().color = ParchInk;
        }

        // ---- Behaviour -----------------------------------------------------------------------

        public void Show(PlayerProfile p, ProgressionTuning t, Action changed, Action backAction)
        {
            profile = p;
            tuning = t ?? new ProgressionTuning();
            onChanged = changed;   // backAction: leaving is the Character screen's Back now
            if (selected == null) selected = FirstWorthReading();
            lighting = null;
            Refresh();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // Open on something the player can act on; otherwise the first seal.
        SkillNode FirstWorthReading()
        {
            foreach (var n in SkillTree.Nodes) if (AccordLayout.State(profile, n) == NodeState.Available) return n;
            return SkillTree.Nodes[0];
        }

        /// <summary>Select a seal (never buys). Public so tests and pointer clicks share the path.</summary>
        public void Click(string id)
        {
            Select(id);
            var go = selected != null ? views[selected.Id].Button.gameObject : null;
            var es = EventSystem.current;
            if (es != null && go != null && go.activeInHierarchy && es.currentSelectedGameObject != go) es.SetSelectedGameObject(go);
        }

        // Keyboard focus moving onto a seal selects it too, so arrows browse the inspector.
        internal void Select(string id)
        {
            var n = SkillTree.Find(id);
            if (n == null || n == selected) { Refresh(); return; }
            selected = n;
            Refresh();
        }

        public void UnlockSelected()
        {
            if (selected == null) return;
            // TryBuy is the single authority: a second press finds the node owned and spends nothing.
            if (SkillTree.TryBuy(profile, selected.Id, out _))
            {
                lighting = views[selected.Id];
                lightStart = Time.unscaledTime;
                onChanged?.Invoke();
            }
            Refresh();
        }

        void AskReset()
        {
            int n = profile.ownedNodes.Count;
            if (n == 0) return;
            // Each node costs one point, so the owned count is the exact refund (spec: state it).
            string body = $"All {n} skill point{(n == 1 ? "" : "s")} come back to you. Skills change from your next run.";
            if (confirm != null && screens != null) confirm.Ask("Reset skills?", body, "Reset", Respec, screens);
            else Respec();
        }

        public void Respec()
        {
            int refunded = SkillTree.Respec(profile);
            if (refunded > 0) onChanged?.Invoke();
            Refresh();
        }

        static string StateWord(NodeState s) => s switch
        {
            NodeState.Owned => "Active", NodeState.CheatActive => "Active through cheats",
            NodeState.Available => "Available", _ => "Locked",
        };

        static string BranchName(SkillBranch b) => b == SkillBranch.BloodPrice ? "Blood Price" : b.ToString();

        /// <summary>The inspector's content as one string (tests read it; the sheet shows it in parts).</summary>
        public string InspectorText
        {
            get
            {
                if (selected == null || profile == null) return "";
                var st = AccordLayout.State(profile, selected);
                var pre = SkillTree.Prerequisite(selected);
                string why = Reason(st);
                return $"{selected.Name}\n{StateWord(st)}\n\n{SkillTree.Describe(selected, tuning)}\n\n" +
                       $"Requires mastery {SkillTree.LevelForTier(selected.Tier)}" +
                       (pre != null ? $"\nAfter {pre.Name}" : "") + (why != null ? $"\n{why}" : "");
            }
        }

        // The rule's own refusal, shown only when there is something to refuse.
        string Reason(NodeState st) => st == NodeState.Locked ? SkillTree.WhyCannotBuy(profile, selected.Id) : null;

        void Refresh()
        {
            if (profile == null) return;
            foreach (var v in views.Values) Paint(v, AccordLayout.State(profile, v.Node));

            var n = selected;
            var st = AccordLayout.State(profile, n);
            var pre = SkillTree.Prerequisite(n);
            nameText.text = n.Name;
            branchText.text = $"{BranchName(n.Branch)}  ·  Tier {n.Tier}";
            stateText.text = StateWord(st);
            stateGem.color = st == NodeState.Owned ? Color.white : st == NodeState.CheatActive ? UiPalette.Violet
                           : st == NodeState.Available ? new Color(1, 1, 1, 0.6f) : new Color(0.4f, 0.37f, 0.42f, 0.8f);
            effectText.text = SkillTree.Describe(n, tuning);
            requireText.text = $"Requires mastery {SkillTree.LevelForTier(n.Tier)}";
            preText.text = pre != null ? $"After {pre.Name}" : "";
            // Mark the failing requirement in place rather than repeating it: "needs mastery 7"
            // under "Requires mastery 7" said the same thing twice. Only a refusal with no line
            // of its own ("no points") gets the reason line.
            string why = Reason(st);
            bool levelShort = why != null && why.StartsWith("needs mastery");
            bool preShort = why != null && pre != null && why == "needs " + pre.Name;
            requireText.color = levelShort ? ParchWarn : ParchSoft;
            preText.color = preShort ? ParchWarn : ParchSoft;
            reasonText.text = why != null && !levelShort && !preShort ? Capitalise(why) : "";
            bool canBuy = st == NodeState.Available && SkillTree.WhyCannotBuy(profile, n.Id) == null;
            unlock.interactable = canBuy;
            unlockFade.alpha = canBuy ? 1f : 0.45f;
            unlockLabel.text = st == NodeState.Owned || st == NodeState.CheatActive ? "Active" : "Unlock  ·  1 point";
            reset.gameObject.SetActive(profile.ownedNodes.Count > 0);

            // Left from Unlock returns to the seal being inspected.
            var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = views[n.Id].Button,
                                       selectOnDown = reset.gameObject.activeSelf ? reset : null };
            unlock.navigation = nav;
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        void Paint(NodeView v, NodeState st)
        {
            bool diamond = v.Node.Tier == 3;
            int art = Mathf.RoundToInt(AccordLayout.SealSize(v.Node.Tier) / 2f);
            Color32 rim = st switch
            {
                NodeState.Owned => new Color32(0xDC, 0xC4, 0x7C, 255),
                NodeState.CheatActive => new Color32(0x9B, 0x6B, 0xE0, 255),
                NodeState.Available => new Color32(0xC1, 0x91, 0x49, 255),
                _ => new Color32(0x9A, 0x90, 0xA6, 255),
            };
            bool lit = st == NodeState.Owned || st == NodeState.CheatActive;
            v.Seal.sprite = SealSprite(diamond, art, rim, lit ? LitFill : DarkFill);
            // Glyph: full on a lit seal; Camel at 70% when available; Muted at 40% when locked.
            v.Glyph.color = lit ? Color.white : st == NodeState.Available
                ? new Color(UiPalette.Camel.r / UiPalette.Honey.r, UiPalette.Camel.g / UiPalette.Honey.g, UiPalette.Camel.b / UiPalette.Honey.b, 0.7f)
                : new Color(UiPalette.Muted.r, UiPalette.Muted.g, UiPalette.Muted.b, 0.4f);
            v.Pulse.gameObject.SetActive(st == NodeState.Available);
            v.Ring.gameObject.SetActive(v.Node == selected);
            v.Lock.gameObject.SetActive(st == NodeState.Locked);
            v.Cheat.gameObject.SetActive(st == NodeState.CheatActive);
            v.Name.color = lit ? UiPalette.Honey : st == NodeState.Available ? UiPalette.Ivory : UiPalette.Muted;
            if (v != lighting) PaintDots(v, lit ? v.Dots.Count : 0);
        }

        static readonly Color DotLit = UiPalette.Honey;
        static readonly Color DotDim = new Color(UiPalette.Muted.r, UiPalette.Muted.g, UiPalette.Muted.b, 0.5f);

        static void PaintDots(NodeView v, int litCount)
        {
            // Dots run from the prerequisite (or core) toward the node, so they light outward.
            for (int i = 0; i < v.Dots.Count; i++) v.Dots[i].color = i < litCount ? DotLit : DotDim;
        }

        void Update()
        {
            float now = Time.unscaledTime;   // menus animate while a run is paused
            if (lighting != null)
            {
                // Under Reduce flashes the path switches on at once (spec).
                float k = DisplayOptions.ReduceFlashes ? 1f : Mathf.Clamp01((now - lightStart) / LightSeconds);
                PaintDots(lighting, Mathf.CeilToInt(k * lighting.Dots.Count));
                if (k >= 1f) lighting = null;
            }
            // The Available outline breathes between 45% and 100%; steady under Reduce flashes.
            float a = DisplayOptions.ReduceFlashes ? 1f : 0.725f + 0.275f * Mathf.Sin(now * Mathf.PI * 2f);
            foreach (var v in views.Values)
                if (v.Pulse.gameObject.activeSelf) v.Pulse.color = new Color(1, 1, 1, a);
        }

        /// <summary>Keyboard focus landing on a seal selects it, so the arrows browse the inspector.</summary>
        sealed class SelectRelay : MonoBehaviour, ISelectHandler
        {
            SkillTreePanel panel; string id;
            public void Bind(SkillTreePanel p, string nodeId) { panel = p; id = nodeId; }
            public void OnSelect(BaseEventData e) { if (panel != null && panel.profile != null) panel.Select(id); }
        }
    }
}
