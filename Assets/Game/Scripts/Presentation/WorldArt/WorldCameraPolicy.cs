using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Closer, lower perspective. Extended terrain covers the full ground frustum.</summary>
    public static class WorldCameraPolicy
    {
        // 30 degrees: the owner lowered it from 50 after seeing it in game, for a more
        // side-on view of the props. The top frustum ray is then only 10 degrees below the
        // horizon, so the enclosure's north wall (not the pitch) is what hides the void;
        // ArenaVisibilityPlayModeTests.NoVoidVisible proves it from every camera extreme.
        // FOV stays 40.
        public const float Pitch = 30, Distance = 20;
        public static Quaternion Rotation() => Quaternion.Euler(Pitch, 0, 0);
        // Derived from the pitch, not hand-typed, so the two can never drift apart.
        public static Vector3 Offset => Rotation() * Vector3.back * Distance;
        public static Vector2 ClampFocus(Vector2 player, Rect bounds)
            => new Vector2(Mathf.Clamp(player.x, bounds.xMin + 6, bounds.xMax - 6),
                Mathf.Clamp(player.y + 3, bounds.yMin + 5, bounds.yMax - 5));
    }
}
