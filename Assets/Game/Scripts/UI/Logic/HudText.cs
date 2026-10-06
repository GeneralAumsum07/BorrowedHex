using UnityEngine;

namespace BorrowedHex.UI
{
    public enum ObjectiveKind { Tutorial, Sandbox, Encounter, ShortBoss, EndlessWave, EndlessBoss }

    /// <summary>What the objective line needs to know, read from the sim by the HUD each frame.</summary>
    public struct ObjectiveInfo
    {
        public ObjectiveKind Kind;
        public int Encounter, EncounterCount, EnemiesLeft, Wave, WavesPerCycle, Cycle;
        public float WaveSecondsLeft;
        public string BossName;
    }

    /// <summary>
    /// The HUD's words (spec 2, Combat HUD): one short objective line, sentence case, no shouting.
    /// Pure, so the copy is tested without a sim; GameplayHud only fills an ObjectiveInfo.
    /// </summary>
    public static class HudText
    {
        const string Sep = "  ·  ";   // the same separator the menus use

        public static string Objective(ObjectiveInfo i)
        {
            switch (i.Kind)
            {
                case ObjectiveKind.Tutorial: return "";   // the prompt panel shows "Lesson n/6" once
                case ObjectiveKind.Sandbox: return "Practice";
                // Encounter is 0-based in the sim; players count from 1.
                case ObjectiveKind.Encounter: return $"Encounter {i.Encounter + 1}/{i.EncounterCount}{Sep}{i.EnemiesLeft} remaining";
                // The configured name as written: capitals in alagard read as shouting.
                case ObjectiveKind.ShortBoss: return $"Defeat {i.BossName}";
                case ObjectiveKind.EndlessBoss: return $"Defeat {i.BossName}{Sep}Cycle {i.Cycle}";
                default:
                    // Rounded up, as before: "0:00" only once the wave is actually over.
                    int secs = Mathf.CeilToInt(i.WaveSecondsLeft);
                    return $"Wave {i.Wave}/{i.WavesPerCycle}{Sep}Cycle {i.Cycle}{Sep}{secs / 60}:{secs % 60:00}";
            }
        }

        /// <summary>The bare number; the multiplier appears only while it is doing something.</summary>
        public static string Score(int score, float multiplier) => multiplier > 1f ? $"{score}  x{multiplier:0.00}" : score.ToString();

        /// <summary>Empty below 2: a chain of one is just a kill.</summary>
        public static string Chain(int length, float nextBonus) => length < 2 ? "" : $"Chain x{length}  +{nextBonus:0.#} next";
    }
}
