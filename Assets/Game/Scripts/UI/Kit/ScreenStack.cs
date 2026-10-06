using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The foreground screen router (spec section 4.2). Only the top screen is active; pushing
    /// remembers what was focused and popping gives it back. Esc asks the top screen first.
    /// A plain class ticked by GameRoot, so tests drive it without frames.
    /// </summary>
    public sealed class ScreenStack
    {
        public const float FadeSeconds = 0.12f;

        sealed class Entry
        {
            public GameObject Root; public Func<GameObject> DefaultFocus; public Action OnEscape;
            public GameObject FocusBefore; public float FadeFrom = -1f;
            // Pushed with PushOverlay: the entry below stayed active but inert.
            public bool Overlay;
        }

        readonly List<Entry> entries = new List<Entry>();
        // Where focus lives. The game uses the live EventSystem.current; EditMode tests inject
        // their own, because an EventSystem only registers as current in OnEnable.
        readonly Func<EventSystem> events;

        public ScreenStack(Func<EventSystem> events = null) => this.events = events ?? (() => EventSystem.current);

        public int Count => entries.Count;
        public GameObject Top => entries.Count > 0 ? entries[entries.Count - 1].Root : null;
        public bool Contains(GameObject root) => entries.Exists(e => e.Root == root);

        public void Push(GameObject root, Func<GameObject> defaultFocus, Action onEscape)
        {
            if (root == null || Contains(root)) return;
            var es = events();
            var e = new Entry { Root = root, DefaultFocus = defaultFocus, OnEscape = onEscape,
                                FocusBefore = es != null ? es.currentSelectedGameObject : null };
            if (entries.Count > 0) entries[entries.Count - 1].Root.SetActive(false);
            entries.Add(e);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;       // Tick starts the fade on its first call
            Focus(defaultFocus?.Invoke());
        }

        /// <summary>
        /// Like Push, but the screen below stays in view, only inert: a confirm dialog is about the
        /// screen under it, and hiding that screen would hide what is being confirmed.
        /// </summary>
        public void PushOverlay(GameObject root, Func<GameObject> defaultFocus, Action onEscape)
        {
            if (root == null || Contains(root)) return;
            var es = events();
            var e = new Entry { Root = root, DefaultFocus = defaultFocus, OnEscape = onEscape, Overlay = true,
                                FocusBefore = es != null ? es.currentSelectedGameObject : null };
            if (entries.Count > 0) SetInert(entries[entries.Count - 1].Root, true);
            entries.Add(e);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
            Focus(defaultFocus?.Invoke());
        }

        // Inert = no clicks and no keyboard selection, but still drawn.
        static void SetInert(GameObject go, bool inert)
        {
            var cg = go != null ? go.GetComponent<CanvasGroup>() : null;
            if (cg == null) return;
            cg.interactable = !inert; cg.blocksRaycasts = !inert;
        }

        public bool Pop()
        {
            if (entries.Count == 0) return false;
            var top = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            if (top.Root != null) top.Root.SetActive(false);
            if (entries.Count > 0)
            {
                var below = entries[entries.Count - 1];
                below.Root.SetActive(true);
                if (top.Overlay) SetInert(below.Root, false);
                var cg = below.Root.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;     // returning is instant: no fade-in on the way back
            }
            Focus(top.FocusBefore);
            return true;
        }

        /// <summary>Esc: the top screen's handler, or a pop. False when nothing is open (the game decides).</summary>
        public bool Escape()
        {
            if (entries.Count == 0) return false;
            var top = entries[entries.Count - 1];
            if (top.OnEscape != null) top.OnEscape(); else Pop();
            return true;
        }

        public void Clear() { while (entries.Count > 0) Pop(); }

        /// <summary>Advance the top screen's fade on unscaled time (menus fade while the run is paused).</summary>
        public void Tick(float now)
        {
            if (entries.Count == 0) return;
            var top = entries[entries.Count - 1];
            var cg = top.Root != null ? top.Root.GetComponent<CanvasGroup>() : null;
            if (cg == null) return;
            if (top.FadeFrom < 0f) top.FadeFrom = now;
            cg.alpha = Mathf.Clamp01((now - top.FadeFrom) / FadeSeconds);
        }

        void Focus(GameObject go)
        {
            var es = events();
            if (es != null) es.SetSelectedGameObject(go != null && go.activeInHierarchy ? go : null);
        }
    }
}
