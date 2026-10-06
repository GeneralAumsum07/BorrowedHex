using System;
using BorrowedHex.Enemies;
using BorrowedHex.Data;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Presentation choices depend on gameplay time, never wall-clock animation timers.</summary>
    public static class WorldArtPolicy
    {
        static readonly string[] Themes = { "Courtyard", "Graveyard", "Cave" };
        public static int Frame(double elapsed, int count, float fps, bool loop)
        {
            if (count <= 0 || fps <= 0 || float.IsNaN(fps) || float.IsInfinity(fps)
                || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed <= 0) return 0;
            double frame = Math.Floor(elapsed * fps);
            return loop ? (int)(frame % count) : (int)Math.Min(count - 1, frame);
        }

        public static string Clip(BossStage stage, BossPattern pattern)
        {
            if (stage == BossStage.Reposition) return pattern == BossPattern.Summon ? "Idle" : "Run";
            if (stage == BossStage.Recover) return "Idle";
            if (stage == BossStage.Teleport || pattern == BossPattern.FanVolley || pattern == BossPattern.Summon) return "Attack3";
            return pattern == BossPattern.BoltStream ? "Attack1" : "Attack2";
        }

        public static float AttackFps(BossStage stage, BossPattern pattern, BossTuning t)
        {
            if (stage == BossStage.Teleport) return 12f;
            // Single casts reach the end of their existing 17-frame row when they resolve.
            // The stream's 13-frame row runs 15% faster while its repeated bolts execute.
            if (pattern == BossPattern.Summon) return 17f / UnityEngine.Mathf.Max(.01f, t.summonTelegraph);
            if (pattern == BossPattern.FanVolley) return 17f / UnityEngine.Mathf.Max(.01f, t.fanTelegraph);
            return pattern == BossPattern.BoltStream ? 13.8f : 12f;
        }

        public static string Theme(int encounter, bool boss)
            => boss ? "Sanctum" : Themes[Math.Max(0, encounter) % Themes.Length];
    }
}
