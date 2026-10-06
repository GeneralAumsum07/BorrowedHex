using BorrowedHex.Presentation.Audio;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The shipped AudioLibrary (built from Assets/SFX/manifest.json) is present and resolves
    /// the cues the game plays. A missing cue is silent at runtime, so this is where it shows.
    /// </summary>
    public class AudioLibraryTests
    {
        [Test]
        public void LibraryLoadsWithEveryClip()
        {
            var lib = AudioLibrary.Load();
            Assert.IsNotNull(lib, "Run BorrowedHex/Audio/Rebuild Audio Library");
            int clips = 0;
            foreach (var c in lib.cues)
            {
                Assert.IsNotEmpty(c.clips, c.name);
                foreach (var clip in c.clips) Assert.IsNotNull(clip, c.name);
                clips += c.clips.Length;
            }
            // Every WAV in Assets/SFX, as rendered on 2026-10-04.
            Assert.AreEqual(809, clips);
        }

        [TestCase("capture_success")]
        [TestCase("packet_charge")]
        [TestCase("low_life_heartbeat")]
        [TestCase("courtyard_combat")]
        [TestCase("collector_battle")]
        [TestCase("button_press")]
        [TestCase("chain_advance_05")]
        public void CueResolves(string name)
        {
            var cue = AudioLibrary.Load().Find(name);
            Assert.IsNotNull(cue, name);
            Assert.IsNotEmpty(cue.clips);
        }
    }
}
