using System.IO;
using BorrowedHex.Core;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>Phase 8: the versioned profile, recovery, storage adapters and idempotent finalization.</summary>
    public class ProfileTests
    {
        /// <summary>A short run that has ended (death), so it carries a real RunSummary.</summary>
        internal static (ArenaSim sim, RunSummary summary) EndedRun(int seed = 1)
        {
            var sim = P5.Short(seed);
            sim.Tick(P5.Still, P5.Dt);
            Assert.IsTrue(sim.DamagePlayer(100000, 0));
            sim.Tick(P5.Still, P5.Dt);
            Assert.IsNotNull(sim.Summary, "the run ended");
            return (sim, sim.Summary);
        }

        static PlayerProfile Tweaked()
        {
            var p = PlayerProfile.CreateDefault();
            p.settings.uiScale = 1.2f;
            p.settings.reduceFlashes = true;
            p.settings.displayMode = 0;
            p.mastery.level = 4;
            p.mastery.xp = 37;
            p.mastery.points = 1;
            p.ownedNodes.Add("precision_angle");
            p.ownedNodes.Add("mobility_speed");
            p.equippedNodes.Add("mobility_speed");
            p.stats.runs = 3;
            p.stats.victories = 1;
            p.achievements.Add(new AchievementEntry { id = "first_borrow", runId = "abc" });
            p.records.Add(new RunRecord { mode = "Short", styleId = "snatcher", score = 1234, seed = 9 });
            return p;
        }

        [Test]
        public void RoundTrip_PreservesEveryValue()
        {
            var p = Tweaked();
            Assert.IsTrue(ProfileService.TryParse(ProfileService.ToJson(p), out var q, out string why), why);
            Assert.AreEqual(1.2f, q.settings.uiScale, 1e-6f);
            Assert.IsTrue(q.settings.reduceFlashes);
            Assert.AreEqual(0, q.settings.displayMode);
            Assert.AreEqual(4, q.mastery.level);
            Assert.AreEqual(37, q.mastery.xp);
            CollectionAssert.AreEqual(p.ownedNodes, q.ownedNodes);
            CollectionAssert.AreEqual(p.equippedNodes, q.equippedNodes);
            Assert.AreEqual(3, q.stats.runs);
            Assert.AreEqual("first_borrow", q.achievements[0].id);
            Assert.AreEqual(1234, q.records[0].score);
        }

        [Test]
        public void OldFileWithoutNewerFields_LoadsWithDefaults()
        {
            Assert.IsTrue(ProfileService.TryParse("{\"version\":1,\"generation\":3}", out var p, out string why), why);
            Assert.AreEqual(1, p.mastery.level);
            Assert.IsNotNull(p.records);
            Assert.AreEqual("snatcher", p.styleId);
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{\"version\":2}")]
        [TestCase("{\"version\":1,\"mastery\":{\"level\":11}}")]
        [TestCase("{\"version\":1,\"mastery\":{\"level\":1,\"points\":3}}")]
        [TestCase("{\"version\":1,\"settings\":{\"uiScale\":9}}")]
        [TestCase("{\"version\":1,\"stats\":{\"runs\":1,\"victories\":2}}")]
        [TestCase("{\"version\":1,\"mastery\":{\"level\":5},\"ownedNodes\":[\"a\"],\"equippedNodes\":[\"b\"]}")]
        public void InvalidSnapshots_AreRejected(string json)
        {
            Assert.IsFalse(ProfileService.TryParse(json, out _, out _));
        }

        [Test]
        public void Load_PrefersTheHighestValidGeneration_AndKeepsDamagedCopies()
        {
            var store = new MemoryProfileStorage();
            var good = Tweaked();
            good.generation = 5;
            store.Snapshots.Add("{ this is damaged");                 // primary
            store.Snapshots.Add(ProfileService.ToJson(good));         // backup
            var service = ProfileService.Load(store);
            Assert.AreEqual(5, service.Profile.generation);
            Assert.AreEqual(4, service.Profile.mastery.level);
            Assert.IsTrue(service.Recovered);
            Assert.IsNotNull(service.Warning);
            Assert.AreEqual(1, store.Preserved.Count, "the damaged snapshot is moved aside, not lost");
        }

        [Test]
        public void Load_NothingValid_StartsADefaultProfile_WithAWarning()
        {
            var store = new MemoryProfileStorage();
            store.Snapshots.Add("garbage");
            var service = ProfileService.Load(store);
            Assert.AreEqual(1, service.Profile.mastery.level);
            Assert.IsNotNull(service.Warning);
            Assert.AreEqual(0, store.Writes, "loading never writes");
        }

        [Test]
        public void Load_EmptyStorage_IsAFreshProfile_WithNoWarning()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            Assert.IsNull(service.Warning);
            Assert.IsFalse(service.Recovered);
        }

        [Test]
        public void FinalizingTwice_AddsNothingTheSecondTime()
        {
            var store = new MemoryProfileStorage();
            var service = ProfileService.Load(store);
            var (sim, summary) = EndedRun();
            var first = service.FinalizeRun(summary, sim.Setup);
            var second = service.FinalizeRun(summary, sim.Setup);
            Assert.IsTrue(first.Applied);
            Assert.IsTrue(first.Saved);
            Assert.IsFalse(second.Applied);
            Assert.AreEqual("already finalized", second.SkippedBecause);
            Assert.AreEqual(1, service.Profile.stats.runs);
            Assert.AreEqual(1, store.Writes);
        }

        [Test]
        public void FinalizedRun_SurvivesAReload_AndStillCannotBeAppliedAgain()
        {
            var store = new MemoryProfileStorage();
            var (sim, summary) = EndedRun();
            ProfileService.Load(store).FinalizeRun(summary, sim.Setup);
            var reloaded = ProfileService.Load(store);
            Assert.AreEqual(1, reloaded.Profile.stats.runs);
            Assert.IsFalse(reloaded.FinalizeRun(summary, sim.Setup).Applied);
        }

        [Test]
        public void SandboxAndDebugRuns_NeverSubmit()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            var (sim, summary) = EndedRun();
            sim.Setup.Debug = true;
            Assert.AreEqual("debug", service.FinalizeRun(summary, sim.Setup).SkippedBecause);
            sim.Setup.Debug = false;
            sim.Setup.Sandbox = true;
            Assert.AreEqual("sandbox", service.FinalizeRun(summary, sim.Setup).SkippedBecause);
            Assert.AreEqual(0, service.Profile.stats.runs);
        }

        [Test]
        public void FailedSave_KeepsPlayingInMemory_AndSaysSo()
        {
            var store = new MemoryProfileStorage { FailWrites = true };
            var service = ProfileService.Load(store);
            var (sim, summary) = EndedRun();
            var r = service.FinalizeRun(summary, sim.Setup);
            Assert.IsTrue(r.Applied);
            Assert.IsFalse(r.Saved);
            Assert.IsTrue(service.InMemoryOnly);
            Assert.IsNotNull(service.Warning);
            Assert.AreEqual(1, service.Profile.stats.runs, "the session keeps the progress");
        }

        [Test]
        public void FinalizedIds_AreCapped()
        {
            var service = ProfileService.Load(new MemoryProfileStorage());
            for (int i = 0; i < PlayerProfile.FinalizedRunIdsKept + 5; i++)
            {
                var (sim, s) = EndedRun(i + 1);
                service.FinalizeRun(s, sim.Setup);
            }
            Assert.AreEqual(PlayerProfile.FinalizedRunIdsKept, service.Profile.finalizedRunIds.Count);
        }

        // ---- PlayerPrefs adapter (Web), under a throwaway prefix ------------------------

        [Test]
        public void PlayerPrefsStorage_SurvivesAReload_AndFallsBackPastADamagedGeneration()
        {
            var prefs = new PlayerPrefsProfileStorage("borrowedhex.test." + System.Guid.NewGuid().ToString("N"));
            try
            {
                var a = ProfileService.Load(prefs);
                a.Profile.stats.runs = 1;
                a.Save();   // generation 1 -> key b
                a.Profile.stats.runs = 2;
                a.Save();   // generation 2 -> key a
                // A "refresh": a new storage object over the same keys.
                Assert.AreEqual(2, ProfileService.Load(prefs).Profile.stats.runs);
                // Damage the newest generation: the previous one loads.
                var all = prefs.ReadAll();
                foreach (var json in all)
                    if (ProfileService.TryParse(json, out var p, out _) && p.generation == 2) prefs.PreserveInvalid(json);
                Assert.AreEqual(1, ProfileService.Load(prefs).Profile.stats.runs);
            }
            finally { prefs.DeleteAll(); }
        }

        [Test]
        public void PlayerPrefsStorage_RefusesOversizedSnapshots()
        {
            var prefs = new PlayerPrefsProfileStorage("borrowedhex.test." + System.Guid.NewGuid().ToString("N"));
            try
            {
                var s = ProfileService.Load(prefs);
                for (int i = 0; i < 3000; i++) s.Profile.achievements.Add(new AchievementEntry { id = "padding_padding_" + i, runId = "r" });
                Assert.IsFalse(s.Save(), "over 64 KiB is refused, not truncated");
                Assert.IsTrue(s.InMemoryOnly);
            }
            finally { prefs.DeleteAll(); }
        }

        // ---- File adapter (Windows) on a throwaway directory, never the real save -------

        string dir;

        [SetUp]
        public void MakeDir() => dir = Path.Combine(Path.GetTempPath(), "bh-profile-test-" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void DropDir() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        [Test]
        public void FileStorage_SavesRotatesAndReloads()
        {
            var store = new FileProfileStorage(dir);
            var a = ProfileService.Load(store);
            a.Profile.stats.runs = 1;
            Assert.IsTrue(a.Save());
            a.Profile.stats.runs = 2;
            Assert.IsTrue(a.Save());
            Assert.IsTrue(File.Exists(Path.Combine(dir, "profile.json")));
            Assert.IsTrue(File.Exists(Path.Combine(dir, "profile.bak.json")));
            Assert.AreEqual(2, ProfileService.Load(store).Profile.stats.runs);
        }

        [Test]
        public void FileStorage_CorruptPrimary_FallsBackToTheBackup_AndTheNextSaveKeepsIt()
        {
            var store = new FileProfileStorage(dir);
            var a = ProfileService.Load(store);
            a.Profile.stats.runs = 1;
            a.Save();
            a.Profile.stats.runs = 2;
            a.Save();
            File.WriteAllText(Path.Combine(dir, "profile.json"), "{ broken");

            var b = ProfileService.Load(store);
            Assert.AreEqual(1, b.Profile.stats.runs, "the backup (generation 1) loads");
            Assert.IsTrue(b.Recovered);
            Assert.AreEqual(1, Directory.GetFiles(dir, "profile.corrupt-*").Length, "the damaged file is kept");

            // The next save must rotate the GOOD profile into the backup, not the corrupt one.
            b.Profile.stats.runs = 5;
            Assert.IsTrue(b.Save());
            Assert.AreEqual(5, ProfileService.Load(store).Profile.stats.runs);
            Assert.IsTrue(ProfileService.TryParse(File.ReadAllText(Path.Combine(dir, "profile.bak.json")), out _, out _));
        }

        [Test]
        public void FileStorage_InterruptedRotation_StillFindsTheNewestSnapshotInTheTempFile()
        {
            var store = new FileProfileStorage(dir);
            var a = ProfileService.Load(store);
            a.Profile.stats.runs = 1;
            a.Save();
            // Simulate a crash after the primary moved to the backup but before the temp landed.
            var newer = ProfileService.TryParse(File.ReadAllText(Path.Combine(dir, "profile.json")), out var p, out _) ? p : null;
            newer.stats.runs = 7;
            newer.generation = 9;
            File.Move(Path.Combine(dir, "profile.json"), Path.Combine(dir, "profile.bak.json"));
            File.WriteAllText(Path.Combine(dir, "profile.tmp"), ProfileService.ToJson(newer));
            Assert.AreEqual(7, ProfileService.Load(store).Profile.stats.runs);
        }
    }
}
