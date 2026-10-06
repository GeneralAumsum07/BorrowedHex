using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BorrowedHex.Presentation;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Builds Assets/Game/Resources/CharacterArt/CharacterArt.asset from the sliced master
    /// sheets. Slices are named "{Char}_{View}_{State}_{NN}" (enemies add "_Composite", the
    /// body with its aura); the frame number is stripped to make the clip key and orders frames.
    /// The evolved "_Base"/"_Aura" layer sheets are not read: the composite already has both.
    /// </summary>
    public static class CharacterArtBuilder
    {
        const string AssetPath = "Assets/Game/Resources/CharacterArt/CharacterArt.asset";
        static readonly Regex Slice = new Regex(@"^(?<key>.+)_(?<n>\d{2})(_Composite)?$");

        [MenuItem("BorrowedHex/Art/Rebuild Character Art")]
        public static string Build()
        {
            var sheets = new List<string>();
            foreach (var dir in new[] { "Acolyte", "Pursuer", "Scatter", "Siege" })
            {
                sheets.Add($"Assets/Sprites/EnemyAnimations/{dir}/{dir}_Complete.png");
                sheets.Add($"Assets/Sprites/EnemyAnimations/{dir}_Evolved/{dir}_Evolved_Complete.png");
            }
            foreach (var view in new[] { "Front", "Back", "Left", "Right" })
                sheets.Add($"Assets/Sprites/RogueMagician_Exact/{view}/RogueMagician_{view}_Complete.png");

            var groups = new SortedDictionary<string, SortedDictionary<int, Sprite>>();
            int missing = 0;
            foreach (var path in sheets)
            {
                if (!File.Exists(path)) { missing++; continue; }
                foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    var m = Slice.Match(s.name);
                    if (!m.Success) continue;
                    var key = m.Groups["key"].Value;
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = new SortedDictionary<int, Sprite>();
                    g[int.Parse(m.Groups["n"].Value)] = s;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(AssetPath);
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<CharacterArt>();
            art.clips.Clear();
            int frames = 0;
            foreach (var kv in groups)
            {
                art.clips.Add(new CharacterArt.Clip { key = kv.Key, frames = kv.Value.Values.ToArray() });
                frames += kv.Value.Count;
            }
            if (created) AssetDatabase.CreateAsset(art, AssetPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            string report = $"CharacterArt: {art.clips.Count} clips, {frames} frames, {missing} sheets missing";
            Debug.Log(report);
            return report;
        }
    }
}
