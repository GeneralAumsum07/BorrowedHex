using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Animated character frames for the player (Assets/Sprites/RogueMagician_Exact) and the
    /// four enemies and their evolved forms (Assets/Sprites/EnemyAnimations), keyed
    /// "{Character}_{View}_{State}", for example "Acolyte_Side_Moving" or "RogueMagician_Left_Dash".
    ///
    /// Like AudioLibrary, this asset under Resources references sprites that live outside
    /// Resources, so builds include them without moving the art. It is rebuilt by
    /// BorrowedHex/Art/Rebuild Character Art (CharacterArtBuilder) from the sliced master sheets.
    /// </summary>
    public sealed class CharacterArt : ScriptableObject
    {
        public const string ResourcePath = "CharacterArt/CharacterArt";

        [Serializable]
        public sealed class Clip
        {
            public string key;
            public Sprite[] frames;
        }

        public List<Clip> clips = new List<Clip>();
        Dictionary<string, Sprite[]> byKey;

        public Sprite[] Get(string key)
        {
            if (byKey == null)
            {
                byKey = new Dictionary<string, Sprite[]>(clips.Count);
                foreach (var c in clips) if (c != null && c.frames != null && c.frames.Length > 0) byKey[c.key] = c.frames;
            }
            return key != null && byKey.TryGetValue(key, out var f) ? f : null;
        }

        static CharacterArt cached;
        static bool looked;

        /// <summary>The shipped art, loaded once; null if it has not been built (views keep the pixel sprites).</summary>
        public static CharacterArt Load()
        {
            if (!looked) { cached = Resources.Load<CharacterArt>(ResourcePath); looked = true; }
            return cached;
        }

        /// <summary>
        /// Playback rate and looping per state, from the two art READMEs: Idle 8 fps, Moving 12,
        /// Attack 12 (once), Dash 24 (once), Hurt 15 (once), Death 10 (once, holds the last frame).
        /// </summary>
        public static float Fps(string state) => state switch
        {
            "Idle" => 8f, "Moving" => 12f, "Attack" => 12f, "Dash" => 24f, "Hurt" => 15f, "Death" => 10f, _ => 8f,
        };

        public static bool Loops(string state) => state == "Idle" || state == "Moving";
    }
}
