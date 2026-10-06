using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.Audio
{
    /// <summary>
    /// The one audio player. Owns a small pool of 2D one-shot voices, two music sources for
    /// crossfades, one ambience bed and named state loops (charge, heartbeat, orbit).
    ///
    /// Everything is 2D: the arena fits on one screen, so panning adds little and the manifest's
    /// mono "for future spatial placement" masters play centred. Every call is safe without a
    /// library (tests, or before the asset is built): a missing cue simply makes no sound.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        // Mix buses as plain multipliers. There is no volume setting yet, so these are the mix.
        public float Master = 1f, Sfx = 0.9f, Music = 0.55f, Ambience = 0.45f, Ui = 0.7f;

        const int Voices = 24;
        // Same-cue spam guard: two copies of one cue inside this window are one sound to the ear,
        // and a 12-pellet volley would otherwise stack 12 full-volume launches (README: one per volley).
        const float DefaultMinGap = 0.05f;

        AudioLibrary lib;
        readonly List<AudioSource> voices = new List<AudioSource>();
        int nextVoice;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly Dictionary<string, int> lastVariation = new Dictionary<string, int>();
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        AudioSource musicA, musicB, ambience;
        string musicCue, ambienceCue;
        float musicFade = 1f;

        public static GameAudio Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            return host.AddComponent<GameAudio>();
        }

        void Awake()
        {
            Instance = this;
            lib = AudioLibrary.Load();
            for (int i = 0; i < Voices; i++) voices.Add(NewSource("Voice" + i));
            musicA = NewSource("MusicA"); musicA.loop = true;
            musicB = NewSource("MusicB"); musicB.loop = true;
            ambience = NewSource("Ambience"); ambience.loop = true;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        /// <summary>A random variation, never the same one twice in a row (README: pick one, don't layer).</summary>
        AudioClip Pick(string name, out bool loop)
        {
            loop = false;
            var cue = lib != null ? lib.Find(name) : null;
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return null;
            loop = cue.loop;
            int n = cue.clips.Length, i = Random.Range(0, n);
            if (n > 1 && lastVariation.TryGetValue(name, out int prev) && prev == i) i = (i + 1) % n;
            lastVariation[name] = i;
            return cue.clips[i];
        }

        /// <summary>One-shot on the effects bus.</summary>
        public static void Play(string cue, float volume = 1f, float minGap = DefaultMinGap) =>
            Instance?.PlayOn(cue, volume * (Instance != null ? Instance.Sfx : 0f), minGap);

        /// <summary>One-shot on the interface bus.</summary>
        public static void PlayUi(string cue, float volume = 1f) =>
            Instance?.PlayOn(cue, volume * (Instance != null ? Instance.Ui : 0f), 0.03f);

        void PlayOn(string cue, float volume, float minGap)
        {
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(cue, out float t) && now - t < minGap) return;
            var clip = Pick(cue, out _);
            if (clip == null) return;
            lastPlayed[cue] = now;
            // Round-robin, but prefer an idle voice so a long tail is not cut while one is free.
            AudioSource v = null;
            for (int k = 0; k < voices.Count; k++)
            {
                var c = voices[(nextVoice + k) % voices.Count];
                if (!c.isPlaying) { v = c; nextVoice = (nextVoice + k + 1) % voices.Count; break; }
            }
            if (v == null) { v = voices[nextVoice]; nextVoice = (nextVoice + 1) % voices.Count; }
            v.clip = clip;
            v.volume = Mathf.Clamp01(volume * Master);
            v.Play();
        }

        /// <summary>
        /// Starts or stops a named state loop (charge, heartbeat, orbit, flight). Idempotent, so
        /// callers can drive it every frame from the state they observe.
        /// </summary>
        public static void Loop(string cue, bool on, float volume = 1f)
        {
            var a = Instance;
            if (a == null) return;
            a.loops.TryGetValue(cue, out var s);
            if (!on)
            {
                if (s != null && s.isPlaying) s.Stop();
                return;
            }
            if (s == null)
            {
                var clip = a.Pick(cue, out _);
                if (clip == null) return;
                s = a.NewSource("Loop_" + cue);
                s.clip = clip; s.loop = true;
                a.loops[cue] = s;
            }
            s.volume = Mathf.Clamp01(volume * a.Sfx * a.Master);
            if (!s.isPlaying) s.Play();
        }

        public static void StopAllLoops()
        {
            if (Instance == null) return;
            foreach (var s in Instance.loops.Values) if (s != null) s.Stop();
        }

        /// <summary>
        /// The score to play (null = silence). Changing it crossfades over about a second, so a
        /// state change never cuts the music dead; asking for the playing cue does nothing.
        /// </summary>
        public static void SetMusic(string cue)
        {
            var a = Instance;
            if (a == null || cue == a.musicCue) return;
            a.musicCue = cue;
            // Swap roles: B becomes the outgoing track, A takes the new one from the top.
            (a.musicA, a.musicB) = (a.musicB, a.musicA);
            var clip = cue != null ? a.Pick(cue, out _) : null;
            a.musicA.clip = clip;
            a.musicA.volume = 0f;
            if (clip != null) a.musicA.Play(); else a.musicA.Stop();
            a.musicFade = 0f;
        }

        public static void SetAmbience(string cue)
        {
            var a = Instance;
            if (a == null || cue == a.ambienceCue) return;
            a.ambienceCue = cue;
            var clip = cue != null ? a.Pick(cue, out _) : null;
            a.ambience.clip = clip;
            if (clip != null) a.ambience.Play(); else a.ambience.Stop();
        }

        void Update()
        {
            // Unscaled: the music keeps fading in pause menus and hit-stop.
            musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime / 1.2f);
            float m = Music * Master;
            musicA.volume = m * musicFade;
            musicB.volume = m * (1f - musicFade);
            if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();
            ambience.volume = Ambience * Master;
        }
    }
}
