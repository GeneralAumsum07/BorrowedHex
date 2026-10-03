using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    public static class WorldPixelSurfaces
    {
        // Source rectangles are in native atlas pixels, with top-origin coordinates
        // recorded from the downloaded sheets. No procedural brick replacement remains.
        static Color Sample(WorldArtLibrary art, Texture2D texture, int x, int top)
            => art.Pixels(texture.name)[Mathf.Clamp(texture.height - 1 - top, 0, texture.height - 1) * texture.width + Mathf.Clamp(x, 0, texture.width - 1)];

        public static Texture2D Create(WorldArtLibrary art, string theme, bool wall)
        {
            int n = wall ? 128 : 512;
            var result = new Texture2D(n, n, TextureFormat.RGBA32, false) {
                name = theme + (wall ? " pixel masonry" : " pixel terrain"), filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
            var tiles = art.Texture("GraveTiles"); var cave = art.Texture("CaveGround");
            var moss = art.Texture("MossGround"); var ground = art.Texture("WorldGround");
            var temple = art.Texture("Temple"); var rocks = art.Texture("CaveWalls");
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                Color c = new Color(.25f, .28f, .3f);
                if (wall)
                {
                    if (theme == "Sanctum" && temple != null) c = Sample(art, temple, 20 + x % 96, 464 + y % 80);
                    else if (theme == "Cave" && rocks != null) c = Sample(art, rocks, 365 + x % 72, 464 + y % 96);
                    else if (tiles != null)
                        c = Sample(art, tiles, theme == "Graveyard" ? 164 + x % 24 : 47 + x % 32,
                            theme == "Graveyard" ? 88 + y % 16 : 52 + y % 32);
                }
                else
                {
                    int sx = 64 + x, sy = 32 + y;
                    if (theme == "Cave" && cave != null) c = Sample(art, cave, 700 + x % 128, 160 + y % 128);
                    else if (theme == "Graveyard" && moss != null) c = Sample(art, moss, 90 + x, 100 + y);
                    else if (ground != null) c = Sample(art, ground, sx, sy);
                    // Interleave intact slabs with buried moss/soil in early stages; the
                    // wasteland consumes the stone entirely rather than just tinting it.
                    bool slabs = theme == "Sanctum" || theme == "Courtyard"
                        || theme == "Graveyard" && ((x / 32 * 7 + y / 32 * 11) % 7 < 2);
                    if (slabs && tiles != null)
                        c = Sample(art, tiles, 48 + x % 32, 25 + y % 14);
                    if (theme == "Sanctum" && temple != null)
                        c = Color.Lerp(c, Sample(art, temple, 20 + x % 128, 472 + y % 64), .65f);
                }
                // Ground packs contain overlays, not opaque base tiles. Composite their
                // transparent regions over real stone pixels rather than flat fill.
                // CaveGround stores its matching soil colour in transparent pixels;
                // preserve that palette rather than revealing square stone underlays.
                if (c.a < .5f && !(theme == "Cave" && !wall && cave != null)) c = tiles != null ? Sample(art, tiles, 48 + x % 32, 25 + y % 14)
                    * (theme == "Graveyard" ? new Color(.52f, .62f, .46f) : new Color(.4f, .42f, .48f)) : new Color(.18f, .2f, .23f);
                if (theme == "Cave") c *= new Color(.82f, .9f, 1.1f, 1);
                if (theme == "Sanctum") c *= new Color(.7f, .75f, 1f, 1);
                c.a = 1; pixels[y * n + x] = c;
            }
            result.SetPixels(pixels); result.Apply(); return result;
        }
    }
}
