using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Task 18: the layout rules the sweep applies to every open screen, as functions that
    /// return failures instead of asserting, so one run reports every broken case at once.
    /// Measured in screen pixels: the canvas is ScreenSpaceOverlay, where world space is screen space.
    /// </summary>
    public static class LayoutProbe
    {
        public static Rect ScreenBox(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        // Half a pixel of grace, so boxes that only share an edge are not reported.
        public static bool Overlap(Rect a, Rect b) =>
            a.xMin < b.xMax - 0.5f && b.xMin < a.xMax - 0.5f && a.yMin < b.yMax - 0.5f && b.yMin < a.yMax - 0.5f;

        public static bool Inside(Rect inner, Rect outer) =>
            inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

        public static bool Shows(Graphic g) => g.isActiveAndEnabled && (g is Text || g.color.a > 0.01f);

        /// <summary>
        /// Every showing, non-empty Text under <paramref name="under"/> is inside the visible
        /// rect and fits its box. Text.preferredHeight lays the string out at the box's width,
        /// so an unplanned wrap shows as a height larger than the box.
        /// </summary>
        public static List<string> Check(Transform under, Rect visible, string where)
        {
            var bad = new List<string>();
            foreach (var t in under.GetComponentsInChildren<Text>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                // A text inside a scroll view is clipped by its viewport by design; its fit is
                // the content's height, not the screen's.
                if (t.GetComponentInParent<ScrollRect>() != null) continue;
                float need = t.preferredHeight, have = t.rectTransform.rect.height;
                if (need > have + 1f) bad.Add($"{where}: {t.transform.parent.name}/{t.name} needs {need:0}, has {have:0}");
                var box = ScreenBox(t.rectTransform);
                if (!Inside(box, visible)) bad.Add($"{where}: {t.transform.parent.name}/{t.name} off screen {box}");
            }
            // Every interactive control is on screen too: a button pushed off the edge is unreachable.
            foreach (var s in under.GetComponentsInChildren<Selectable>(false))
            {
                if (!s.isActiveAndEnabled || s.GetComponentInParent<ScrollRect>() != null) continue;
                var box = ScreenBox((RectTransform)s.transform);
                if (!Inside(box, visible)) bad.Add($"{where}: control {s.name} off screen {box}");
            }
            return bad;
        }

        /// <summary>Pairwise overlaps among the given boxes.</summary>
        public static List<string> Overlaps(List<(string name, Rect box)> boxes, string where)
        {
            var bad = new List<string>();
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (Overlap(boxes[i].box, boxes[j].box)) bad.Add($"{where}: {boxes[i].name} x {boxes[j].name}");
            return bad;
        }
    }
}
