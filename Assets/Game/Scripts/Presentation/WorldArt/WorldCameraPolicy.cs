using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Closer, lower perspective. Extended terrain covers the full ground frustum.</summary>
    public static class WorldCameraPolicy
    {
        // 50 degrees looks down onto the arena (the 2.5D read the owner asked for) and keeps
        // the north edge of the frustum within ~9 units of the north wall, where the
        // enclosure's statement wall blocks the line of sight. FOV stays 40.
        public const float Pitch = 50, Distance = 20;
        public static Quaternion Rotation() => Quaternion.Euler(Pitch, 0, 0);
        // Derived from the pitch, not hand-typed, so the two can never drift apart.
        public static Vector3 Offset => Rotation() * Vector3.back * Distance;
        public static Vector2 ClampFocus(Vector2 player, Rect bounds)
            => new Vector2(Mathf.Clamp(player.x, bounds.xMin + 6, bounds.xMax - 6),
                Mathf.Clamp(player.y + 3, bounds.yMin + 5, bounds.yMax - 5));
    }
}
