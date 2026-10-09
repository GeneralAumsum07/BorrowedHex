using System.Collections;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 6: optional still illustrations behind lore pages. A picture is an
    /// override of the black screen, never a requirement: missing art stays black, a new
    /// picture fades in (or appears at once under Reduce flashes), and the typing, the pages
    /// and the input are identical with or without pictures.
    /// </summary>
    public class IllustrationTests
    {
        Canvas canvas;
        NarrativePanel panel;
        static Sprite Art => PixelSprites.Get(PixelSprites.Kind.Collector);

        static NarrativePage Lore(string t, string art) => new NarrativePage(PageKind.Lore, Speaker.None, null, t, null, art);

        // Two pictures, then a missing one, then a dialogue line: every case in one scene.
        static readonly NarrativeScene Scene = new NarrativeScene("art_test", "Art",
            Lore("The first passage is long enough to still be typing a moment later on.", "art/a"),
            Lore("The second passage has a different picture behind it entirely.", "art/b"),
            Lore("The third passage asks for a picture that nobody has supplied.", "art/missing"),
            new NarrativePage(PageKind.Dialogue, Speaker.Rogue, null, "And a line of dialogue at the end."));

        static Sprite Lookup(string key) => key == "art/a" || key == "art/b" ? Art : null;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            canvas = Ui.CreateCanvas("IllustrationTestCanvas", 10);
            panel = NarrativePanel.Create(canvas, null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(canvas.gameObject);
            yield return null;
        }

        static IEnumerator Seconds(float s)
        {
            float until = Time.unscaledTime + s;
            while (Time.unscaledTime < until) yield return null;
        }

        /// <summary>Two presses: the first completes the passage, the second turns the page.</summary>
        IEnumerator NextPage(NarrativePanel p)
        {
            p.SimulateAdvance();
            yield return null;
            p.SimulateAdvance();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnIllustratedPageShowsItsPictureAndAMissingOneStaysBlack()
        {
            panel.Illustrations = Lookup;
            panel.Play(Scene, _ => { });
            yield return null;
            Assert.That(panel.Illustration.enabled, Is.True);
            Assert.That(panel.Illustration.sprite, Is.SameAs(Art));

            yield return NextPage(panel);
            yield return NextPage(panel);
            Assert.That(panel.PageIndex, Is.EqualTo(2));
            Assert.That(panel.Illustration.enabled, Is.False, "no picture: black");
            Assert.That(panel.Background.color, Is.EqualTo(Color.black));

            yield return NextPage(panel);
            Assert.That(panel.Layout, Is.EqualTo(PageKind.Dialogue));
            Assert.That(panel.Illustration.enabled, Is.False, "dialogue shows the arena, never a picture");
        }

        [UnityTest]
        public IEnumerator ANewPictureFadesInWithoutHoldingTheText()
        {
            panel.Illustrations = Lookup;
            panel.Play(Scene, _ => { });
            yield return Seconds(0.4f);   // past the opening fade
            yield return NextPage(panel);
            Assert.That(panel.PageIndex, Is.EqualTo(1));
            Assert.That(panel.Illustration.color.a, Is.LessThan(1f), "a changed picture fades in");
            int revealed = panel.RevealedElements;
            yield return Seconds(0.1f);
            Assert.That(panel.RevealedElements, Is.GreaterThan(revealed), "typing never waits for the picture");
            yield return Seconds(0.5f);
            Assert.That(panel.Illustration.color.a, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ReducedFlashesShowThePictureAtOnce()
        {
            panel.Illustrations = Lookup;
            panel.ReduceFlashes = true;
            panel.Play(Scene, _ => { });
            yield return null;
            yield return NextPage(panel);
            Assert.That(panel.PageIndex, Is.EqualTo(1));
            Assert.That(panel.Illustration.color.a, Is.EqualTo(1f), "no transition");
        }

        [UnityTest]
        public IEnumerator TypingAndPagesAreIdenticalWithOrWithoutPictures()
        {
            // Side by side on one canvas, started in one frame: same deltas, so any timing
            // difference is the pictures' doing.
            var plain = NarrativePanel.Create(canvas, null);
            panel.Illustrations = Lookup;
            panel.Play(Scene, _ => { });
            plain.Play(Scene, _ => { });
            yield return Seconds(0.5f);
            Assert.That(panel.RevealedElements, Is.EqualTo(plain.RevealedElements));
            // Turn both pages in the same frames: the picture changes here, the black does not.
            panel.SimulateAdvance(); plain.SimulateAdvance();
            yield return null;
            panel.SimulateAdvance(); plain.SimulateAdvance();
            yield return null;
            yield return Seconds(0.3f);
            Assert.That(panel.PageIndex, Is.EqualTo(1));
            Assert.That(plain.PageIndex, Is.EqualTo(1));
            Assert.That(panel.RevealedElements, Is.EqualTo(plain.RevealedElements), "a picture's fade holds no text");
        }
    }
}
