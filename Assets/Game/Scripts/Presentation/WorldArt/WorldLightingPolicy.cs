using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    public readonly struct WorldLighting
    {
        public readonly Color Ambient, Fog, Moon, Fire;
        public readonly float MoonIntensity;
        public WorldLighting(Color ambient, Color fog, Color moon, float moonIntensity, Color fire)
        { Ambient = ambient; Fog = fog; Moon = moon; MoonIntensity = moonIntensity; Fire = fire; }
    }

    /// <summary>
    /// The per-theme night rig (spec 2.4): dark ambient, near-black fog, a cold low moon
    /// and fire lights at the flame anchors. Unknown themes (tutorial and non-world arenas,
    /// via WorldArtPolicy.Theme) get the Courtyard rig rather than black.
    /// </summary>
    public static class WorldLightingPolicy
    {
        public static readonly Color Warm = new Color(1, .62f, .32f), Cold = new Color(.45f, .65f, 1);
        // Pitched 50 degrees and aimed back toward the camera, so props cast readable shadows on
        // the camera's side of the floor.
        public static Quaternion MoonRotation => Quaternion.Euler(50, 160, 0);

        /// <summary>
        /// The glow of the glitch seam, taken from the INCOMING arena so the crack reads as the
        /// next reality bleeding through. Saturated rather than white so it reads as a colour,
        /// not as a flash.
        /// </summary>
        public static Color Seam(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return new Color(.35f, 1f, .6f);
                case "Cave": return new Color(.5f, .55f, 1f);
                case "Sanctum": return new Color(.75f, .45f, 1f);
                default: return new Color(1f, .7f, .4f);
            }
        }

        public static WorldLighting For(string theme)
        {
            switch (theme)
            {
                // Graveyard, Cave and Sanctum were raised after the first in-game review found them
                // too dark; the Cave most of all (ambient ~2x, moon .3 -> .5) because it has no sky
                // fill and read as a void. Courtyard was judged right and is unchanged.
                case "Graveyard": return new WorldLighting(new Color(.12f, .14f, .14f), new Color(.03f, .045f, .04f), new Color(.55f, .66f, .78f), .5f, Warm);
                case "Cave": return new WorldLighting(new Color(.18f, .16f, .24f), new Color(.05f, .04f, .08f), new Color(.55f, .55f, .82f), .5f, Cold);
                // Cold to match the Sanctum's existing blue pillar flames (inference; owner may retune).
                case "Sanctum": return new WorldLighting(new Color(.13f, .11f, .17f), new Color(.035f, .025f, .055f), new Color(.6f, .5f, .85f), .48f, Cold);
                default: return new WorldLighting(new Color(.10f, .10f, .14f), new Color(.03f, .03f, .05f), new Color(.55f, .6f, .85f), .45f, Warm);
            }
        }
    }
}
