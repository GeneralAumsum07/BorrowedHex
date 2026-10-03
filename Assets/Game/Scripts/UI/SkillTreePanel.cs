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
    /// Mastery and the twelve-node tree (Phase 9, D99), opened from the main menu between runs.
    /// Every card is one button whose click buys it; an owned node is simply active (D101), so
    /// there is nothing else to click for. A refusal shows the domain rule's own reason
    /// (SkillTree.WhyCannotBuy), so this panel holds no rules of its own.
    /// </summary>
    public sealed class SkillTreePanel : MonoBehaviour
    {
        PlayerProfile profile;
        ProgressionTuning tuning;
        Action onChanged, onBack;
        Text header, status;
        readonly Dictionary<string, Text> cardTexts = new Dictionary<string, Text>();
        readonly Dictionary<string, Image> cardFills = new Dictionary<string, Image>();
        Button back;

        public bool IsOpen => gameObject.activeSelf;

        // Card geometry, shared by the branch headers and the cards so they cannot drift apart.
        const float ColumnStep = 380f, CardWidth = 360f;

        // Owned = active since D101, so an owned card takes the green that "equipped" used to.
        static readonly Color Active = new Color(0.20f, 0.42f, 0.30f, 1f);
        // Unlocked only by the cheat: a different fill, so it never reads as earned progress.
        static readonly Color CheatOnly = new Color(0.27f, 0.19f, 0.47f, 1f);
        static readonly Color Locked = new Color(0.12f, 0.10f, 0.16f, 1f);

        public static SkillTreePanel Create(Canvas canvas)
        {
            var dim = Ui.Image("SkillTree", canvas.transform, new Color(0, 0, 0, 0.85f));
            Ui.Stretch(dim.rectTransform);
            var p = dim.gameObject.AddComponent<SkillTreePanel>();
            p.Build(dim.rectTransform);
            dim.gameObject.SetActive(false);
            return p;
        }

        void Build(RectTransform root)
        {
            var panel = Ui.Image("Panel", root, Ui.Panel).rectTransform;
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 900));
            var title = Ui.Label("Title", panel, "MASTERY & SKILLS", 52);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1400, 64));
            title.color = Ui.Accent;
            header = Ui.Label("Header", panel, "", 26);
            Ui.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -116), new Vector2(1400, 36));
            header.supportRichText = true;

            // Columns are branches, rows are tiers; tier one at the top reads as "start here".
            // Four branches since D99: 380 px apart, 360 wide, centred. The span is
            // 3 * 380 + 360 = 1500, inside the 1560 panel with 30 px either side.
            string[] branchNames = { "PRECISION", "MOBILITY", "RESILIENCE", "BLOOD PRICE" };
            for (int b = 0; b < branchNames.Length; b++)
            {
                float x = (b - 1.5f) * ColumnStep;
                var bl = Ui.Label($"Branch{b}", panel, branchNames[b], 28);
                Ui.Place(bl.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -160), new Vector2(CardWidth, 36));
                // Blood Price is the price side of borrowed power: a muted red, not the cool blue.
                bl.color = (SkillBranch)b == SkillBranch.BloodPrice ? new Color(1f, 0.55f, 0.55f) : new Color(0.75f, 0.95f, 1f);
            }
            foreach (var n in SkillTree.Nodes)
            {
                string id = n.Id;
                var btn = Ui.Button($"Node_{id}", panel, "", () => Click(id), 22);
                float x = ((int)n.Branch - 1.5f) * ColumnStep;
                // Ui.Place pins the TOP edge here. Rows sit below the branch names (which end at
                // -196) and the tier-3 row ends at -700, clear of the status line (bottom-anchored
                // at 150 + 34 = 184 up, i.e. -716 from the top of the 900-tall panel).
                float y = -210f - (n.Tier - 1) * 170f;
                Ui.Place(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(CardWidth, 150));
                var t = btn.GetComponentInChildren<Text>();
                t.supportRichText = true;
                t.rectTransform.offsetMin = new Vector2(16, 8);
                t.rectTransform.offsetMax = new Vector2(-16, -8);
                cardTexts[id] = t;
                cardFills[id] = btn.GetComponent<Image>();
            }

            status = Ui.Label("Status", panel, "", 24);
            Ui.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1400, 34));
            status.color = new Color(1f, 0.75f, 0.55f);
            var note = Ui.Label("Note", panel, "Click a node to buy it. Owned nodes are always active. Changes apply from the next run.", 20);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 112), new Vector2(1400, 30));
            note.color = new Color(1, 1, 1, 0.6f);
            var respec = Ui.Button("Respec", panel, "Respec (free)", Respec, 26);
            Ui.Place(respec.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-170, 46), new Vector2(300, 60));
            back = Ui.Button("Back", panel, "Back", () => onBack?.Invoke(), 26);
            Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170, 46), new Vector2(300, 60));
        }

        public void Show(PlayerProfile p, ProgressionTuning t, Action changed, Action backAction)
        {
            profile = p;
            tuning = t ?? new ProgressionTuning();
            onChanged = changed;
            onBack = backAction;
            status.text = "";
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(back.gameObject);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The card's action; public so tests and keyboard play go through the same path.</summary>
        public void Click(string id)
        {
            // D101: buying is the only action. Clicking an owned card is refused with the rule's
            // own "already owned", which is harmless and tells the player why nothing happened.
            SkillTree.TryBuy(profile, id, out string why);
            status.text = why == null ? "" : "Cannot: " + why;
            if (why == null) onChanged?.Invoke();
            Refresh();
        }

        void Respec()
        {
            int refunded = SkillTree.Respec(profile);
            status.text = refunded > 0 ? $"Refunded {refunded} point{(refunded == 1 ? "" : "s")}." : "Nothing to refund.";
            if (refunded > 0) onChanged?.Invoke();
            Refresh();
        }

        void Refresh()
        {
            var m = profile.mastery;
            string xp = m.level >= Mastery.MaxLevel ? "max level" : $"{m.xp} / {Mastery.CostToAdvance(m.level)} XP";
            header.text = $"Mastery <color=#FAD150>{m.level}</color>   ·   {xp}   ·   " +
                          $"<color=#FAD150>{m.points}</color> point{(m.points == 1 ? "" : "s")}";
            foreach (var n in SkillTree.Nodes)
            {
                bool owned = SkillTree.IsOwned(profile, n.Id);
                // Only unlocked by the cheat: said on the card, so nobody mistakes it for progress.
                bool cheatOnly = owned && !profile.ownedNodes.Contains(n.Id);
                string state;
                if (cheatOnly) state = "<color=#FF8C73>ACTIVE (cheat)</color>";
                else if (owned) state = "<color=#8CF0A8>ACTIVE</color>";
                else
                {
                    string why = SkillTree.WhyCannotBuy(profile, n.Id);
                    state = why == null ? "<color=#FAD150>Buy: 1 point</color>" : $"<color=#A09AB0>Locked: {why}</color>";
                }
                cardTexts[n.Id].text = $"<b>{n.Name}</b>  <size=18>(tier {n.Tier})</size>\n{SkillTree.Describe(n, tuning)}\n<size=19>{state}</size>";
                cardFills[n.Id].color = cheatOnly ? CheatOnly : owned ? Active : Locked;
            }
        }
    }
}
