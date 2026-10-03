using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Closer, lower perspective. Extended terrain covers the full ground frustum.</summary>
    public static class WorldCameraPolicy
    {
        public static Quaternion Rotation() => Quaternion.Euler(30, 0, 0);
        public static Vector3 Offset => new Vector3(0, 9, -15.588f);
        public static Vector2 ClampFocus(Vector2 player, Rect bounds)
            => new Vector2(Mathf.Clamp(player.x, bounds.xMin + 6, bounds.xMax - 6),
                Mathf.Clamp(player.y + 3, bounds.yMin + 5, bounds.yMax - 5));
    }
}
