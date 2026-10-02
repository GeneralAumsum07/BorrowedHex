using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BorrowedHex.Progression
{
    /// <summary>
    /// Where profile snapshots live. The storage only moves strings; deciding which snapshot is
    /// valid and newest is the service's job, so every platform shares one recovery rule.
    /// </summary>
    public interface IProfileStorage
    {
        /// <summary>Every snapshot the storage holds (primary and backups), any order. Unreadable ones are skipped.</summary>
        List<string> ReadAll();
        /// <summary>Persist a new snapshot. Throws on failure; the caller reports and keeps playing in memory.</summary>
        void Write(string json, int generation);
        /// <summary>Move an invalid snapshot aside so a later write can never destroy the evidence or the valid backup.</summary>
        void PreserveInvalid(string json);
        string Describe { get; }
    }

    /// <summary>Tests and the "storage unavailable" fallback. Never touches disk or PlayerPrefs.</summary>
    public sealed class MemoryProfileStorage : IProfileStorage
    {
        public readonly List<string> Snapshots = new List<string>();
        public readonly List<string> Preserved = new List<string>();
        public bool FailWrites;
        public int Writes { get; private set; }

        public List<string> ReadAll() => new List<string>(Snapshots);

        public void Write(string json, int generation)
        {
            if (FailWrites) throw new IOException("simulated storage failure");
            Writes++;
            // Keep primary + one backup, like the file storage.
            Snapshots.Insert(0, json);
            while (Snapshots.Count > 2) Snapshots.RemoveAt(Snapshots.Count - 1);
        }

        public void PreserveInvalid(string json)
        {
            Preserved.Add(json);
            Snapshots.Remove(json);
        }

        public string Describe => "memory";
    }

    /// <summary>
    /// Windows (and the editor): profile.json plus profile.bak.json under persistentDataPath.
    /// A save writes a temporary file first and then swaps it in, so a crash mid-write leaves
    /// either the old primary or the new one, never half a file.
    /// </summary>
    public sealed class FileProfileStorage : IProfileStorage
    {
        readonly string dir;
        string Primary => Path.Combine(dir, "profile.json");
        string Backup => Path.Combine(dir, "profile.bak.json");
        string Temp => Path.Combine(dir, "profile.tmp");

        public FileProfileStorage(string directory) => dir = directory;

        public List<string> ReadAll()
        {
            var list = new List<string>();
            // The temp file is a candidate too: a crash between the two moves in Write leaves
            // the newest snapshot only there. If it is half-written it simply fails validation.
            foreach (var path in new[] { Primary, Backup, Temp })
            {
                try { if (File.Exists(path)) list.Add(File.ReadAllText(path)); }
                catch (Exception) { /* unreadable counts as missing; the other copy may still load */ }
            }
            return list;
        }

        public void Write(string json, int generation)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Temp, json);
            if (File.Exists(Primary))
            {
                // The current primary becomes the backup in the same step the new file lands.
                if (File.Exists(Backup)) File.Delete(Backup);
                File.Move(Primary, Backup);
            }
            File.Move(Temp, Primary);
        }

        public void PreserveInvalid(string json)
        {
            // Rename whichever file holds this text, so the next save rotates a VALID primary
            // into the backup instead of the corrupt one.
            foreach (var path in new[] { Primary, Backup, Temp })
            {
                try
                {
                    if (!File.Exists(path) || File.ReadAllText(path) != json) continue;
                    string dest = Path.Combine(dir, $"profile.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileNameWithoutExtension(path)}.json");
                    File.Move(path, dest);
                    return;
                }
                catch (Exception) { /* best effort: failing to preserve must not block play */ }
            }
        }

        public string Describe => dir;
    }

    /// <summary>
    /// Web: PlayerPrefs (IndexedDB behind the scenes, 1 MB total). Two keys alternate by
    /// generation, so the previous snapshot always survives a failed or interrupted save.
    /// PlayerPrefs.Save() is called at each explicit save point (section 8).
    /// </summary>
    public sealed class PlayerPrefsProfileStorage : IProfileStorage
    {
        readonly string KeyA, KeyB;

        /// <param name="prefix">Tests pass their own prefix so they never touch the game's keys.</param>
        public PlayerPrefsProfileStorage(string prefix = "borrowedhex.profile")
        {
            KeyA = prefix + ".a";
            KeyB = prefix + ".b";
        }

        /// <summary>Remove this storage's keys (tests clean up after themselves).</summary>
        public void DeleteAll()
        {
            foreach (var k in new[] { KeyA, KeyB, KeyA + ".corrupt", KeyB + ".corrupt" }) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
        /// <summary>Section 8: each snapshot under 64 KiB.</summary>
        public const int MaxBytes = 64 * 1024;

        public List<string> ReadAll()
        {
            var list = new List<string>();
            foreach (var k in new[] { KeyA, KeyB })
                if (PlayerPrefs.HasKey(k)) list.Add(PlayerPrefs.GetString(k));
            return list;
        }

        public void Write(string json, int generation)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
                throw new IOException($"profile snapshot over {MaxBytes} bytes");
            PlayerPrefs.SetString((generation & 1) == 0 ? KeyA : KeyB, json);
            PlayerPrefs.Save();
        }

        public void PreserveInvalid(string json)
        {
            foreach (var k in new[] { KeyA, KeyB })
            {
                if (!PlayerPrefs.HasKey(k) || PlayerPrefs.GetString(k) != json) continue;
                PlayerPrefs.SetString(k + ".corrupt", json);
                PlayerPrefs.DeleteKey(k);
                PlayerPrefs.Save();
                return;
            }
        }

        public string Describe => "PlayerPrefs";
    }
}
