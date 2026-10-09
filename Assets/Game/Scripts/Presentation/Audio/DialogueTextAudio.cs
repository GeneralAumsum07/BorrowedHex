using System;
using UnityEngine;

namespace BorrowedHex.Presentation.Audio
{
    /// <summary>
    /// Lore plan Task 5: the dialogue typewriter's tick, and nothing else (no speech, no voice
    /// catalog). One fixed sound for every speaker, at the saved dialogue-sound volume.
    ///
    /// WHEN to tick is not decided here. The Typewriter already ticks only for newly revealed
    /// letters and digits, at most one per 0.06 s, never for lore pages, instant text, skips or
    /// a revealed-rest press; NarrativePanel raises that as Ticked. This component only turns a
    /// Ticked into a sound, and can cut a sound short (Stop) when the story pauses or ends,
    /// so nothing plays on after the text has stopped. Missed ticks are never replayed: a tick
    /// is a one-shot or nothing.
    ///
    /// Its own AudioSource, outside GameAudio's voice pool: a dialogue tick must never steal a
    /// combat voice, and Stop must cut exactly this sound and no other.
    /// </summary>
    public sealed class DialogueTextAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        const float Seconds = 0.025f, Ramp = 0.005f, Hz = 640f, Peak = 0.08f;
        /// <summary>An optional supplied tick (Resources/Narrative/dialogue_tick); the generated one otherwise.</summary>
        public const string OverridePath = "Narrative/dialogue_tick";

        AudioSource source;
        AudioClip clip;

        /// <summary>Saved dialogue-sound volume, 0..1, read at every tick (Settings can change mid-scene).</summary>
        public Func<float> Volume = () => 0.35f;
        /// <summary>Ticks actually played (tests: volume zero plays none, a pause adds none).</summary>
        public int Played { get; private set; }
        public bool IsSounding => source != null && source.isPlaying;

        /// <summary>
        /// The guaranteed tick: a 25 ms mono 640 Hz sine at 22,050 Hz, 0.08 peak, with 5 ms
        /// linear attack and release ramps so it starts and ends at zero (no click at either edge).
        /// </summary>
        public static float[] TickSamples()
        {
            int n = (int)Math.Round(SampleRate * Seconds);
            float ramp = SampleRate * Ramp;
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                // The smaller of "how far in" and "how far from the end", over the ramp length.
                float env = Math.Min(1f, Math.Min(i / ramp, (n - 1 - i) / ramp));
                s[i] = Peak * env * (float)Math.Sin(2 * Math.PI * Hz * i / SampleRate);
            }
            return s;
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            clip = Resources.Load<AudioClip>(OverridePath);
            if (clip == null)
            {
                var samples = TickSamples();
                clip = AudioClip.Create("DialogueTick", samples.Length, 1, SampleRate, false);
                clip.SetData(samples, 0);
            }
            source.clip = clip;
        }

        /// <summary>One tick. Silent (and uncounted) at volume zero: muting changes nothing else.</summary>
        public void Tick()
        {
            float v = Mathf.Clamp01(Volume());
            if (v <= 0f || source == null) return;
            source.volume = v;
            // Play, not PlayOneShot: a new tick restarts the source instead of layering, so a
            // fast line can never build into a buzz, and Stop always silences everything.
            source.Play();
            Played++;
        }

        /// <summary>Cut the current tick: the story paused, lost focus, changed scene or ended.</summary>
        public void Stop()
        {
            if (source != null && source.isPlaying) source.Stop();
        }
    }
}
