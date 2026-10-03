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
    /// Mastery and the nine-node tree (Phase 9), opened from the main menu between runs. Every
    /// card is one button whose click does the one sensible thing for its state: buy when
    /// buyable, equip when owned, unequip when equipped. A refusal shows the domain rule's own
    /// reason (SkillTree.WhyCannot...), so this panel holds no rules of its own.
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

        static readonly Color Equipped = new Color(0.20f, 0.42f, 0.30f, 1f);
        static readonly Color Owned = new Color(0.27f, 0.19f, 0.47f, 1f);
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
            Ui.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -102), new Vector2(1400, 36));
            header.supportRichText = true;

            // Columns are branches, rows are tiers; tier one at the top reads as "start here".
            string[] branchNames = { "PRECISION", "MOBILITY", "RESILIENCE" };
            for (int b = 0; b < 3; b++)
            {
                float x = (b - 1) * 500f;
                var bl = Ui.Label($"Branch{b}", panel, branchNames[b], 28);
                Ui.Place(bl.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -160), new Vector2(460, 36));
                bl.color = new Color(0.75f, 0.95f, 1f);
            }
            foreach (var n in SkillTree.Nodes)
            {
                string id = n.Id;
                var btn = Ui.Button($"Node_{id}", panel, "", () => Click(id), 22);
                float x = ((int)n.Branch - 1) * 500f;
                float y = -270f - (n.Tier - 1) * 170f;
                Ui.Place(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(460, 150));
                var t = btn.GetComponentInChildren<Text>();
                t.supportRichText = true;
                t.rectTransform.offsetMin = new Vector2(16, 8);
                t.rectTransform.offsetMax = new Vector2(-16, -8);
                cardTexts[id] = t;
                cardFills[id] = btn.GetComponent<Image>();
            }

            status = Ui.Label("Status", panel, "", 24);
            Ui.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(1400, 34));
            status.color = new Color(1f, 0.75f, 0.55f);
            var note = Ui.Label("Note", panel, "Click a node to buy, equip or unequip it. Up to 3 equipped. Changes apply from the next run.", 20);
            Ui.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 96), new Vector2(1400, 30));
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
            string why;
            if (profile.equippedNodes.Contains(id))
            {
                SkillTree.Unequip(profile, id);
                why = null;
            }
            // IsOwned, not ownedNodes: the unlock-all cheat makes an unbought node equippable.
            else if (SkillTree.IsOwned(profile, id)) SkillTree.TryEquip(profile, id, out why);
            else SkillTree.TryBuy(profile, id, out why);
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
                          $"<color=#FAD150>{m.points}</color> point{(m.points == 1 ? "" : "s")}   ·   " +
                          $"{profile.equippedNodes.Count} / {Mastery.MaxEquipped} equipped";
            foreach (var n in SkillTree.Nodes)
            {
                bool owned = SkillTree.IsOwned(profile, n.Id);
                // Only unlocked by the cheat: said on the card, so nobody mistakes it for progress.
                bool cheatOnly = owned && !profile.ownedNodes.Contains(n.Id);
                bool equipped = profile.equippedNodes.Contains(n.Id);
                string state;
                if (equipped) state = "<color=#8CF0A8>EQUIPPED</color> - click to unequip";
                else if (cheatOnly) state = "<color=#FF8C73>Unlocked (cheat)</color> - click to equip";
                else if (owned) state = "Owned - click to equip";
                else
                {
                    string why = SkillTree.WhyCannotBuy(profile, n.Id);
                    state = why == null ? "<color=#FAD150>Buy: 1 point</color>" : $"<color=#A09AB0>Locked: {why}</color>";
                }
                cardTexts[n.Id].text = $"<b>{n.Name}</b>  <size=18>(tier {n.Tier})</size>\n{SkillTree.Describe(n, tuning)}\n<size=19>{state}</size>";
                cardFills[n.Id].color = equipped ? Equipped : owned ? Owned : Locked;
            }
        }
    }
}
