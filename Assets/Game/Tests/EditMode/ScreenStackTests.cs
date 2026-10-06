using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowedHex.Tests
{
    public class ScreenStackTests
    {
        GameObject es, a, b, opener;
        EventSystem sys;
        ScreenStack stack;

        [SetUp]
        public void SetUp()
        {
            es = new GameObject("ES", typeof(EventSystem));
            // EditMode never runs OnEnable, so an EventSystem cannot become EventSystem.current
            // here; the stack takes its focus source by injection instead.
            sys = es.GetComponent<EventSystem>();
            a = new GameObject("A", typeof(RectTransform), typeof(CanvasGroup));
            b = new GameObject("B", typeof(RectTransform), typeof(CanvasGroup));
            opener = new GameObject("Opener");
            stack = new ScreenStack(() => sys);
        }
        [TearDown] public void TearDown() { foreach (var g in new[] { es, a, b, opener }) Object.DestroyImmediate(g); }

        [Test]
        public void PushHidesTheScreenBelowAndPopRestoresItAndItsFocus()
        {
            sys.SetSelectedGameObject(opener);
            stack.Push(a, () => a, null);
            stack.Push(b, () => b, null);
            Assert.IsFalse(a.activeSelf, "only the top screen shows (spec: hide parent controls)");
            Assert.AreSame(b, sys.currentSelectedGameObject);
            stack.Pop();
            Assert.IsTrue(a.activeSelf);
            Assert.AreSame(a, sys.currentSelectedGameObject, "focus returns to what opened B");
            stack.Pop();
            Assert.AreSame(opener, sys.currentSelectedGameObject);
        }

        // Task 10: a confirm dialog is about the screen under it, so that screen stays in view.
        [Test]
        public void AnOverlayKeepsTheScreenBelowVisibleButInert()
        {
            stack.Push(a, () => a, null);
            stack.PushOverlay(b, () => b, null);
            Assert.IsTrue(a.activeSelf, "the dialog is about this screen, so it stays in view");
            Assert.IsFalse(a.GetComponent<CanvasGroup>().interactable);
            Assert.IsFalse(a.GetComponent<CanvasGroup>().blocksRaycasts);
            stack.Pop();
            Assert.IsTrue(a.GetComponent<CanvasGroup>().interactable);
            Assert.IsTrue(a.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.AreSame(a, sys.currentSelectedGameObject);
        }

        // Review Focus 3.
        [Test]
        public void EscDuringFadeClosesOnlyTheTop()
        {
            stack.Push(a, null, null);
            stack.Push(b, null, null);
            stack.Tick(0f);                 // b mid-fade
            Assert.IsTrue(stack.Escape());
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(a, stack.Top);
            Assert.IsTrue(a.activeSelf);
        }

        [Test]
        public void EscapeHandlerOverridesThePop()
        {
            int asked = 0;
            stack.Push(a, null, () => asked++);   // e.g. a dirty form asking "discard?"
            Assert.IsTrue(stack.Escape());
            Assert.AreEqual(1, asked); Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void EmptyStackLeavesEscToTheGame() => Assert.IsFalse(stack.Escape());

        [Test]
        public void FadeRunsOnUnscaledTimeAndEndsOpaque()
        {
            stack.Push(a, null, null);
            stack.Tick(100f); stack.Tick(100f + ScreenStack.FadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, a.GetComponent<CanvasGroup>().alpha, 0.05f);
            stack.Tick(100f + ScreenStack.FadeSeconds + 0.01f);
            Assert.AreEqual(1f, a.GetComponent<CanvasGroup>().alpha, 1e-4);
        }

        [Test]
        public void PushingTheSameScreenTwiceIsANoOp()
        {
            stack.Push(a, null, null); stack.Push(a, null, null);
            Assert.AreEqual(1, stack.Count);
        }
    }
}
