using System.Collections.Generic;
using System.IO;
using BorrowedHex.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Builds Assets/Game/Resources/Audio/AudioLibrary.asset from Assets/SFX/manifest.json.
    /// The manifest is the single source of truth for cue names and variations; this only
    /// turns its relative paths into clip references. "reuse_existing_cues" rows are aliases
    /// of cues that already have their own row, so they are skipped rather than duplicated.
    /// </summary>
    public static class AudioLibraryBuilder
    {
        const string ManifestPath = "Assets/SFX/manifest.json";
        const string AssetPath = "Assets/Game/Resources/Audio/AudioLibrary.asset";

        // JsonUtility needs concrete types matching the manifest's shape.
        [System.Serializable] class Row { public string name; public bool loop; public string[] paths; }
        [System.Serializable] class Manifest { public Row[] inventory; }

        [MenuItem("BorrowedHex/Audio/Rebuild Audio Library")]
        public static string Build()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
            bool created = lib == null;
            if (created) lib = ScriptableObject.CreateInstance<AudioLibrary>();
            lib.cues.Clear();
            int clips = 0, missing = 0;
            var seen = new HashSet<string>();
            foreach (var row in manifest.inventory)
            {
                if (row.name == "reuse_existing_cues" || !seen.Add(row.name)) continue;
                var list = new List<AudioClip>();
                foreach (var rel in row.paths)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/" + rel);
                    if (clip != null) { list.Add(clip); clips++; } else missing++;
                }
                lib.cues.Add(new AudioLibrary.Cue { name = row.name, loop = row.loop, clips = list.ToArray() });
            }
            if (created) AssetDatabase.CreateAsset(lib, AssetPath);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            string report = $"AudioLibrary: {lib.cues.Count} cues, {clips} clips, {missing} missing";
            Debug.Log(report);
            return report;
        }
    }
}
