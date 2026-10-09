using System.Collections.Generic;

namespace BorrowedHex.Narrative
{
    /// <summary>How a page is presented (plan section 1).</summary>
    public enum PageKind
    {
        /// <summary>Black screen, centred passage, typed silently.</summary>
        Lore,
        /// <summary>A written source on a black screen (a request, a correction): a label above, typed silently, no portrait.</summary>
        Writing,
        /// <summary>Bottom dialogue box over the frozen arena: speaker name and sprite, typed to the dialogue tick.</summary>
        Dialogue,
    }

    /// <summary>Who speaks a dialogue line. Mara never speaks: she appears through written corrections.</summary>
    public enum Speaker { None, Collector, Rogue }

    /// <summary>One page or dialogue line. Immutable, so a scene can never be edited mid-playback.</summary>
    public sealed class NarrativePage
    {
        public readonly PageKind Kind;
        public readonly Speaker Speaker;
        /// <summary>Writing pages only: names the written source ("Mara's correction").</summary>
        public readonly string Label;
        public readonly string Text;
        /// <summary>A word drawn in a restrained colour and crossed out (Mara's "Enough"); null for none.</summary>
        public readonly string Emphasis;
        /// <summary>Optional illustration (Task 6) shown instead of black; null keeps the black screen.</summary>
        public readonly string IllustrationKey;

        public NarrativePage(PageKind kind, Speaker speaker, string label, string text, string emphasis = null, string illustrationKey = null)
        {
            Kind = kind; Speaker = speaker; Label = label; Text = text; Emphasis = emphasis; IllustrationKey = illustrationKey;
        }
    }

    public sealed class NarrativeScene
    {
        /// <summary>Stable ID: saved in the profile, so it must never be renamed.</summary>
        public readonly string Id;
        /// <summary>Menu name in the Story replay list.</summary>
        public readonly string Title;
        public readonly IReadOnlyList<NarrativePage> Pages;

        public NarrativeScene(string id, string title, params NarrativePage[] pages) { Id = id; Title = title; Pages = pages; }
    }

    /// <summary>
    /// Lore plan Task 1: the guaranteed English script, in code so no optional asset (an
    /// illustration, a voice-free audio library) can ever remove the story. Passages are the
    /// plan's section 2, adapted from Docs/LORE_DOCUMENT.md, which stays the narrative authority.
    /// </summary>
    public static class NarrativeCatalog
    {
        /// <summary>
        /// Plan section G: the results caption after a story run ends in death or time expiry.
        /// A line, never a scene: those results stay instantly restartable.
        /// </summary>
        public const string RecallCaption = "The unfinished renewal recalls you. Another advance waits to be stolen.";

        public const int MaxLoreWords = 28;
        public const int MaxDialogueWords = 18;

        public const string Prologue = "prologue", LastKindness = "last_kindness", ForgottenAnswer = "forgotten_answer",
            Anomaly = "anomaly", Confrontation = "confrontation", OpenWindow = "open_window";

        // Illustration groups (plan Task 6). Keys only: a missing image falls back to black.
        const string ArtTornEntry = "lore/torn_entry", ArtMarasWindow = "lore/maras_window", ArtAncientConsent = "lore/ancient_consent",
            ArtUnfinishedCircle = "lore/unfinished_circle", ArtLastLamp = "lore/last_lamp", ArtOpenWindow = "lore/open_window";

        static NarrativePage L(string text, string art = null) => new NarrativePage(PageKind.Lore, Speaker.None, null, text, null, art);
        static NarrativePage W(string label, string text, string art = null) => new NarrativePage(PageKind.Writing, Speaker.None, label, text, null, art);
        static NarrativePage C(string text) => new NarrativePage(PageKind.Dialogue, Speaker.Collector, null, text);
        static NarrativePage R(string text) => new NarrativePage(PageKind.Dialogue, Speaker.Rogue, null, text);

        /// <summary>Every scene, in narrative order (the Story menu lists them in this order).</summary>
        public static readonly IReadOnlyList<NarrativeScene> Scenes = new[]
        {
            new NarrativeScene(Prologue, "The stolen life",
                L("The Collector executed you. Your heart is beating again—but every beat spends time that isn't yours.", ArtTornEntry),
                L("The baker whispered warmth into his ovens. The healer borrowed another morning. You made coins disappear. The children waited, smiling, for their return.", ArtTornEntry),
                L("Later, you carried letters through wards where nobody left. Some letters escaped. One register didn't survive your visit. The next entry was your own.", ArtTornEntry),
                L("You caught the binding meant to claim you and stole its remaining time. Your true name stayed in his Ledger.", ArtTornEntry),
                L("A physician refused to let his patients die. He kept their bodies, their homes, and finally the world.", ArtTornEntry),
                L("Your world remained. Its health did not. Those who stayed too long became something worse.", ArtTornEntry),
                L("Every heartbeat spends that time. Your own spellmaking died with your life. Borrow your enemies' magic. Reach the Ledger. Take your name back.", ArtTornEntry)),

            new NarrativeScene(LastKindness, "The last kindness",
                L("Before he was The Collector, Avel Sere was a mortal physician. During a famine, he borrowed years to keep his patients alive.", ArtMarasWindow),
                L("His sister Mara recorded their last wishes. When her time came, she asked him to open the window.", ArtMarasWindow),
                L("He closed it, cut the ending from her healing spell, and made her stay.", ArtMarasWindow),
                new NarrativePage(PageKind.Lore, Speaker.None, null, "Beside her name, she wrote: Enough. He crossed it out.", "Enough", ArtMarasWindow)),

            new NarrativeScene(ForgottenAnswer, "The forgotten answer",
                L("Deep in the House, patients lie beneath clean blankets. Beside each bed is a record of consent dated centuries ago.", ArtAncientConsent),
                W("An unfiled request", "I asked for time to see my son. He came. We spoke. He went home. What is the rest of this for?", ArtAncientConsent),
                W("Mara's correction", "In the Ledger's margins, Mara writes: He remembers everything about us except the last thing we said.", ArtAncientConsent)),

            new NarrativeScene(Anomaly, "The anomaly",
                L("The Collector means to seal every renewal into one unbroken circle. Nothing within his keeping would ever be permitted to leave.", ArtUnfinishedCircle),
                L("But you are neither ordinarily alive nor one of his bound dead. Your unfinished entry keeps that circle open.", ArtUnfinishedCircle),
                L("Killing you only begins the failed renewal again. He must reclaim your torn entry. Mara's corrections show where the buried exits remain.", ArtUnfinishedCircle)),

            new NarrativeScene(Confrontation, "The confrontation",
                C("There you are. Your bed is still made."),
                C("You have already spent several of my patients. Shall I tell you their names?"),
                R("You kept their signatures. Did you keep their answers?"),
                C("They were tired."),
                R("They told you what they wanted."),
                C("Until your entry is settled, nothing can be finished."),
                R("Then I'm opening their entries before I close mine.")),

            new NarrativeScene(OpenWindow, "The open window",
                L("The Collector falls. His Ledger remains. While its renewals endure, even he can be recalled.", ArtLastLamp),
                L("You place the torn transfer beside your true name. Closing only your entry would complete his circle.", ArtLastLamp),
                L("You leave your name open and restore the dismissals he buried.", ArtOpenWindow),
                L("The dead can leave. Those with time remaining can spend it freely. Exhausted buildings fall. Places capable of growth can change again.", ArtOpenWindow),
                L("Mara's entry closes. Beside it, one word remains: Enough.", ArtOpenWindow),
                L("The world does not become young. What survives can finally have a future.", ArtOpenWindow),
                C("The lamp. Please. Don't leave me in the dark."),
                R("Is there someone I should send for?"),
                L("You open the window. You stay until the lamp goes out. Then you close your own entry.", ArtLastLamp),
                L("There will be no further recall. The unused time leaves your hands. There is light beyond the window.", ArtOpenWindow)),
        };

        public static NarrativeScene Get(string id)
        {
            foreach (var s in Scenes) if (s.Id == id) return s;
            return null;
        }

        /// <summary>Position in narrative order, or -1 for an unknown (e.g. renamed, from an old save) ID.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < Scenes.Count; i++) if (Scenes[i].Id == id) return i;
            return -1;
        }

        /// <summary>Whitespace-separated words; an em dash joining two words counts them apart.</summary>
        public static int WordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Replace('—', ' ').Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>Name shown in the dialogue box (the plan's script labels the protagonist "Rogue").</summary>
        public static string SpeakerName(Speaker s) => s == Speaker.Collector ? "The Collector" : s == Speaker.Rogue ? "Rogue" : "";
    }
}
