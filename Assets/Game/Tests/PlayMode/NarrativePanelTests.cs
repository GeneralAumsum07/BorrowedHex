using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Narrative;
using BorrowedHex.Presentation;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Lore plan Task 2: the panel's two layouts (black lore page, bottom dialogue box), its
    /// input rules and its limits, on a bare canvas with no GameRoot.
    /// </summary>
    public class NarrativePanelTests
    {
        Canvas canvas;
        NarrativePanel panel;
        int ticks;
        readonly List<NarrativeEnd> ends = new List<NarrativeEnd>();

        static NarrativeScene Scene(params NarrativePage[] pages) => new NarrativeScene("test", "Test", pages);
        static NarrativePage Lore(string t) => new NarrativePage(PageKind.Lore, Speaker.None, null, t);
        static NarrativePage Line(Speaker s, string t) => new NarrativePage(PageKind.Dialogue, s, null, t);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            canvas = Ui.CreateCanvas("NarrativePanelTestCanvas", 10);
            panel = NarrativePanel.Create(canvas, s => PixelSprites.Get(s == Speaker.Collector ? PixelSprites.Kind.Collector : PixelSprites.Kind.Magician));
            panel.Ticked += () => ticks++;
            ticks = 0; ends.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(canvas.gameObject);
            yield return null;
        }

        IEnumerator Seconds(float s)
        {
            float until = Time.unscaledTime + s;
            while (Time.unscaledTime < until) yield return null;
        }

        void Play(NarrativeScene scene) => panel.Play(scene, e => ends.Add(e));

        [UnityTest]
        public IEnumerator ALorePageIsBlackCentredAndTypesSilently()
        {
            Play(Scene(Lore("The Collector executed you. Your heart is beating again, but every beat spends time that isn't yours.")));
            yield return Seconds(1f);
            Assert.That(panel.Layout, Is.EqualTo(PageKind.Lore));
            Assert.That(panel.Background.color.a, Is.EqualTo(1f), "lore pages are black");
            Assert.That(panel.Background.color.r + panel.Background.color.g + panel.Background.color.b, Is.Zero);
            Assert.That(panel.BodyLabel.text, Does.Contain("<color=#00000000>"), "typing, not shown at once");
            Assert.That(panel.BodyLabel.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(ticks, Is.Zero, "lore typing is silent");
            Assert.That(panel.DialogueBox.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ADialogueLineSitsInTheBottomQuarterWithNameAndSpriteAndTicks()
        {
            Play(Scene(Line(Speaker.Collector, "There you are. Your bed is still made.")));
            yield return Seconds(1f);
            Assert.That(panel.Layout, Is.EqualTo(PageKind.Dialogue));
            Assert.That(panel.Background.color.a, Is.Zero, "the frozen arena shows above the box");
            Assert.That(panel.DialogueBox.gameObject.activeSelf, Is.True);
            var corners = new Vector3[4];
            panel.DialogueBox.GetWorldCorners(corners);
            float top = corners[1].y;
            Assert.That(top, Is.LessThanOrEqualTo(Screen.height * 0.25f + 1f), "the box stays in the bottom quarter");
            Assert.That(panel.SpeakerLabel.text, Is.EqualTo("The Collector"));
            Assert.That(panel.Portrait.sprite, Is.Not.Null);
            Assert.That(panel.Portrait.preserveAspect, Is.True);
            Assert.That(ticks, Is.GreaterThan(0), "dialogue types to the tick");
        }

        [UnityTest]
        public IEnumerator CompletingALineNeverAdvancesItAndAFreshPressDoes()
        {
            Play(Scene(Line(Speaker.Rogue, "You kept their signatures. Did you keep their answers?"), Line(Speaker.Collector, "They were tired.")));
            yield return null;
            yield return null;
            panel.SimulateAdvance();
            yield return null;
            Assert.That(panel.PageIndex, Is.Zero, "the first press only completes the line");
            Assert.That(panel.BodyLabel.text, Does.Not.Contain("<color"));
            int before = ticks;
            yield return null;
            Assert.That(ticks, Is.EqualTo(before), "completing early is silent");
            panel.SimulateAdvance();
            yield return null;
            Assert.That(panel.PageIndex, Is.EqualTo(1));
            Assert.That(panel.SpeakerLabel.text, Is.EqualTo("The Collector"));
        }

        [UnityTest]
        public IEnumerator TheLastAdvanceCompletesTheSceneAndSkipEndsItEarly()
        {
            Play(Scene(Lore("One page.")));
            panel.InstantText = true;
            yield return null;
            yield return null;
            panel.SimulateAdvance();
            yield return null;
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(ends, Is.EqualTo(new[] { NarrativeEnd.Completed }));

            panel.InstantText = false;
            Play(Scene(Lore("First."), Lore("Second.")));
            yield return null;
            panel.Skip();
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(ends.Last(), Is.EqualTo(NarrativeEnd.Skipped));
        }

        [UnityTest]
        public IEnumerator InstantStoryTextAppliesToBothLayouts()
        {
            panel.InstantText = true;
            Play(Scene(Lore("A whole passage at once."), Line(Speaker.Rogue, "A whole line at once.")));
            yield return null;
            Assert.That(panel.BodyLabel.text, Is.EqualTo("A whole passage at once."));
            panel.SimulateAdvance();
            yield return null;
            yield return null;
            Assert.That(panel.Layout, Is.EqualTo(PageKind.Dialogue));
            Assert.That(panel.BodyLabel.text, Is.EqualTo("A whole line at once."));
            Assert.That(ticks, Is.Zero, "instant text is silent");
        }

        [UnityTest]
        public IEnumerator FrozenStopsTypingAndTicksWithNoCatchUp()
        {
            Play(Scene(Line(Speaker.Collector, "You have already spent several of my patients. Shall I tell you their names?")));
            yield return Seconds(0.5f);
            panel.Frozen = true;
            int shown = panel.RevealedElements, t = ticks;
            yield return Seconds(1f);
            Assert.That(panel.RevealedElements, Is.EqualTo(shown));
            Assert.That(ticks, Is.EqualTo(t));
            panel.Frozen = false;
            yield return null;
            yield return null;
            Assert.That(panel.RevealedElements - shown, Is.LessThanOrEqualTo(8), "no fast-forward on return");
        }

        [UnityTest]
        public IEnumerator CancelDuringTypingHidesWithoutACallbackOrSound()
        {
            Play(Scene(Line(Speaker.Collector, "Until your entry is settled, nothing can be finished.")));
            yield return Seconds(0.4f);
            panel.Cancel();
            int t = ticks;
            yield return Seconds(0.3f);
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(ends, Is.Empty, "cancellation is the caller's, not a completion");
            Assert.That(ticks, Is.EqualTo(t));
        }

        [UnityTest]
        public IEnumerator TheCrossedOutWordIsMarked()
        {
            panel.InstantText = true;
            Play(Scene(new NarrativePage(PageKind.Lore, Speaker.None, null, "Beside her name, she wrote: Enough. He crossed it out.", "Enough")));
            yield return null;
            yield return null;
            Assert.That(panel.BodyLabel.text, Does.Contain(">Enough</color>"), "restrained colour on the word");
            Assert.That(panel.Strike.gameObject.activeSelf, Is.True, "a line through the word");
            Assert.That(panel.Strike.rectTransform.rect.width, Is.GreaterThan(10f));
        }

        [UnityTest]
        public IEnumerator EveryCataloguePageFitsWithoutScrollingAtMaximumScale()
        {
            // The largest interface scale shrinks the logical canvas (GameRoot.ApplySettings).
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f) / 1.4f;
            panel.InstantText = true;
            foreach (var scene in NarrativeCatalog.Scenes)
            {
                Play(scene);
                for (int i = 0; i < scene.Pages.Count; i++)
                {
                    yield return null;
                    yield return null;
                    var label = panel.BodyLabel;
                    float needed = Ui.TextHeight(label, label.rectTransform.rect.width);
                    Assert.That(needed, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f),
                        $"{scene.Id} page {i} overflows ({needed} > {label.rectTransform.rect.height})");
                    panel.SimulateAdvance();
                }
                yield return null;
                Assert.That(panel.IsPlaying, Is.False, scene.Id);
            }
        }
    }
}
