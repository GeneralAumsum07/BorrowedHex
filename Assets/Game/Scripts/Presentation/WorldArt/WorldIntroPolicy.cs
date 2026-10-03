using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    public static class WorldIntroPolicy
    {
        public const float Rise = 1.6f, Darkness = .6f, LightInterval = .4f, TitleHold = 2.4f;
        public const float LightsAt = Rise + Darkness;
        public const float TitleAt = LightsAt + 8 * LightInterval;
        public const float Duration = TitleAt + TitleHold;
        public static int LitPillars(float elapsed) => Mathf.Clamp(Mathf.FloorToInt((elapsed - LightsAt) / LightInterval), 0, 8);
        public static bool ShowTitle(float elapsed) => elapsed >= TitleAt;
        public static float Curtain(float elapsed)
        {
            if (elapsed < Rise) return Mathf.Clamp01((elapsed - .8f) / .8f);
            if (elapsed < LightsAt) return 1;
            return 1 - Mathf.Clamp01((elapsed - LightsAt) / .5f);
        }
    }
}
