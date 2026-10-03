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
    /// The per-theme night rig (spec 2.4): very dark ambient, near-black fog, a cold low moon
    /// and fire lights at the flame anchors. Unknown themes (tutorial and non-world arenas,
    /// via WorldArtPolicy.Theme) get the Courtyard rig rather than black.
    /// </summary>
    public static class WorldLightingPolicy
    {
        public static readonly Color Warm = new Color(1, .62f, .32f), Cold = new Color(.45f, .65f, 1);
        // Pitched 50 degrees and aimed back toward the camera, so props cast readable shadows on
        // the camera's side of the floor.
        public static Quaternion MoonRotation => Quaternion.Euler(50, 160, 0);

        public static WorldLighting For(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return new WorldLighting(new Color(.08f, .10f, .10f), new Color(.02f, .035f, .03f), new Color(.5f, .62f, .75f), .4f, Warm);
                case "Cave": return new WorldLighting(new Color(.09f, .08f, .13f), new Color(.03f, .02f, .05f), new Color(.45f, .45f, .75f), .3f, Cold);
                // Cold to match the Sanctum's existing blue pillar flames (inference; owner may retune).
                case "Sanctum": return new WorldLighting(new Color(.07f, .06f, .10f), new Color(.025f, .015f, .04f), new Color(.55f, .45f, .8f), .35f, Cold);
                default: return new WorldLighting(new Color(.10f, .10f, .14f), new Color(.03f, .03f, .05f), new Color(.55f, .6f, .85f), .45f, Warm);
            }
        }
    }
}
