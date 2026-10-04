using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // Pierce needs no sim change (spec 2): a returned shot's second EnemyDamaged with the same
    // (root, shot, echo) key is a pierce. An echo copies its original's ShotId, hence the flag.
    public class PierceTrackerTests
    {
        static DamageEvent Hit(int root, int shot, DamageCategory c) => new DamageEvent { RootReleaseId = root, ShotId = shot, Category = c };

        [Test]
        public void TheSecondEnemyOfOneShotIsAPierce()
        {
            var t = new PierceTracker();
            Assert.IsFalse(t.Hit(Hit(7, 3, DamageCategory.ReturnedProjectile), new Vector2(1, 0), out _));
            Assert.IsTrue(t.Hit(Hit(7, 3, DamageCategory.ReturnedProjectile), new Vector2(4, 0), out var previous));
            Assert.AreEqual(new Vector2(1, 0), previous, "the arc starts at the first enemy");
        }

        [Test]
        public void AnEchoIsItsOwnShot()
        {
            var t = new PierceTracker();
            t.Hit(Hit(7, 3, DamageCategory.ReturnedProjectile), Vector2.zero, out _);
            Assert.IsFalse(t.Hit(Hit(7, 3, DamageCategory.Echo), Vector2.one, out _));
        }

        [Test]
        public void OnlyShotsCanPierce()
        {
            var t = new PierceTracker();
            t.Hit(Hit(0, 0, DamageCategory.Orbit), Vector2.zero, out _);
            Assert.IsFalse(t.Hit(Hit(0, 0, DamageCategory.Orbit), Vector2.one, out _));
            Assert.AreEqual(0, t.Count, "untracked categories leave nothing behind");
        }

        [Test]
        public void ForgettingAnEndedShotFreesItsKey()
        {
            var t = new PierceTracker();
            t.Hit(Hit(7, 3, DamageCategory.ReturnedProjectile), Vector2.zero, out _);
            t.Forget(7, 3, false);
            Assert.AreEqual(0, t.Count);
        }
    }
}
