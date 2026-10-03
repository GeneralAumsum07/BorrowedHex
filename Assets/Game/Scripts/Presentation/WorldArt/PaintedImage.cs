using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Float RGBA working image for the floor painter. Floats (not Color32) because several
    /// layers multiply and lerp; rounding to bytes between layers bands the dark gradients.
    /// Row 0 is the bottom row, matching Texture2D.SetPixels32.
    /// </summary>
    public sealed class PaintedImage
    {
        public readonly int Width, Height;
        public readonly float[] R, G, B, A;

        public PaintedImage(int width, int height)
        {
            Width = width; Height = height;
            R = new float[width * height]; G = new float[width * height]; B = new float[width * height]; A = new float[width * height];
            for (int i = 0; i < A.Length; i++) A[i] = 1;
        }

        public static PaintedImage FromColors(Color32[] pixels, int width, int height)
        {
            var image = new PaintedImage(width, height);
            for (int i = 0; i < pixels.Length; i++)
            {
                image.R[i] = pixels[i].r / 255f; image.G[i] = pixels[i].g / 255f;
                image.B[i] = pixels[i].b / 255f; image.A[i] = pixels[i].a / 255f;
            }
            return image;
        }

        public Color32[] ToColors()
        {
            var pixels = new Color32[R.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(Byte(R[i]), Byte(G[i]), Byte(B[i]), 255);
            return pixels;
        }

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255), 0, 255);
        public float Luminance(int x, int y) { int i = y * Width + x; return R[i] * .3f + G[i] * .59f + B[i] * .11f; }

        public float MeanLuminance()
        {
            double sum = 0;
            for (int i = 0; i < R.Length; i++) sum += R[i] * .3f + G[i] * .59f + B[i] * .11f;
            return (float)(sum / R.Length);
        }
    }

    /// <summary>
    /// An overlay atlas (separate blobs on transparency) plus each blob's bounds. Connected
    /// components on a 4x-downsampled alpha mask: the source sheets are up to 2304 px square.
    /// </summary>
    public sealed class DecalSheet
    {
        public readonly PaintedImage Image;
        public readonly List<RectInt> Sprites = new List<RectInt>();

        public DecalSheet(PaintedImage image, int minSize)
        {
            Image = image;
            const int K = 4;
            int w = image.Width / K, h = image.Height / K;
            var mask = new bool[w * h]; var seen = new bool[w * h]; var stack = new Stack<int>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                mask[y * w + x] = image.A[(y * K + K / 2) * image.Width + x * K + K / 2] > .1f;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
                stack.Push(start); seen[start] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop(), px = p % w, py = p / w;
                    x0 = Mathf.Min(x0, px); y0 = Mathf.Min(y0, py); x1 = Mathf.Max(x1, px); y1 = Mathf.Max(y1, py);
                    // Left/right neighbours must not wrap onto the previous/next row.
                    if (px > 0) Visit(p - 1); if (px < w - 1) Visit(p + 1);
                    if (py > 0) Visit(p - w); if (py < h - 1) Visit(p + w);
                }
                var rect = new RectInt(x0 * K, y0 * K, (x1 - x0 + 1) * K, (y1 - y0 + 1) * K);
                if (rect.width >= minSize && rect.height >= minSize) Sprites.Add(rect);
            }
            void Visit(int q) { if (mask[q] && !seen[q]) { seen[q] = true; stack.Push(q); } }
        }
    }
}
