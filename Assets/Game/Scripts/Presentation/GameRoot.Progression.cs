using System.Collections.Generic;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Presentation
{
    /// <summary>Phase 9: mastery on the menu and results, the tree panel, and the run loadout.</summary>
    public sealed partial class GameRoot
    {
        public SkillTreePanel Tree { get; private set; }
        /// <summary>The Character screen hosting the tree (Skills tab) and the styles (Capture style tab).</summary>
        public CharacterScreen Character { get; private set; }
        /// <summary>The overlay confirm for irreversible menu actions (spec: Reset skills, abandon run).</summary>
        public ConfirmDialog Confirm { get; private set; }

        partial void BuildProgressionMenus()
        {
            // Character first: the tree and the style cards parent their views into its Body.
            Character = CharacterScreen.Create(canvas);
            Tree = SkillTreePanel.Create(Character.Body);
            // One shared confirm dialog: Reset skills asks through it now, the pause menu later (Task 12).
            Confirm = ConfirmDialog.Create(canvas);
            Tree.Bind(Confirm, Screens);
            Character.TabChanged += ShowCharacterTab;
            Character.Back += CloseCharacter;
            Main.EnableEntry("mastery", OpenTree);
            BuildLaterMenus();
        }

        // Phases 10-12 switch on their own entries here.
        partial void BuildLaterMenus();

        void OpenTree() => OpenCharacter(CharacterScreen.SkillsTab);

        void OpenCharacter(int tab)
        {
            Character.SetHeader(Profile.Profile.mastery);
            Character.Open(tab);
            Screens.Push(Character.gameObject, () => Character.DefaultFocus, CloseCharacter);
        }

        // Only the active tab's view is active; each view's IsOpen reads activeInHierarchy.
        void ShowCharacterTab(int tab)
        {
            if (tab == CharacterScreen.SkillsTab)
            {
                Styles?.Hide();
                Tree.Show(Profile.Profile, Config.progression, OnTreeChanged, CloseCharacter);
                Character.ViewFocus = () => Tree.DefaultFocus;
            }
            else
            {
                Tree.Hide();
                Styles.Show(Profile.Profile, Config, () => Profile.Save(), CloseCharacter);
                Character.ViewFocus = () => Styles.DefaultFocus;
            }
        }

        // A purchase spends points and may change the header's numbers.
        void OnTreeChanged()
        {
            Profile.Save();
            Character.SetHeader(Profile.Profile.mastery);
        }

        void CloseCharacter()
        {
            if (Screens.Top == Character.gameObject) Screens.Pop();
            RefreshMainMenu();
        }

        void CloseTree() => CloseCharacter();

        /// <summary>
        /// Section 7: style plus equipped passives are resolved ONCE here, into the setup the sim
        /// copies at construction. The tree can change between runs without touching a live one.
        /// </summary>
        partial void ApplyLoadoutExtra(RunSetup setup)
        {
            setup.PassiveIds = SkillTree.ActiveNodes(Profile.Profile);   // D101: owned = active
            setup.Stats = Loadout.Resolve(Config, setup.PassiveIds);
            ApplyStyleExtra(setup);
        }

        // Phase 11 applies the capture style on top of the passives here.
        partial void ApplyStyleExtra(RunSetup setup);

        // Task 16: the XP lines moved to ResultsCopy.FromFinalize (the results screen's copy).
    }
}
