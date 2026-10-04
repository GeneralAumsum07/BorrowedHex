using System.Collections;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    // Shot views are pooled and keyed by projectile (spec 3.3, 7): the pool never grows past
    // the peak number of live shots, and a reused view's trail never streaks from its last shot.
    public class ShotViewPlayModeTests
    {
        GameObject camGo, rootGo;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            camGo = new GameObject("TestCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 17, -17.5f);
            cam.transform.LookAt(new Vector3(0, 0, -2.2f));
            GameRoot.StorageOverride = new MemoryProfileStorage();   // never the real save
            rootGo = new GameObject("GameRoot");
            root = rootGo.AddComponent<GameRoot>();
            yield return null;
            root.SetFocus(true);
            root.PlaySandbox();
            yield return null;
            root.Sim.AutoSpawn = false;
            root.Sim.Player.InvulnerableUntil = double.PositiveInfinity;   // shots pass through the player
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(rootGo);
            Object.Destroy(camGo);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None)) Object.Destroy(es.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            GameRoot.StorageOverride = null;
            yield return null;
        }

        ProjectileActor Fire(Vector2 from, Vector2 dir, float range)
        {
            var sim = root.Sim;
            var s = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f);
            return sim.SpawnProjectile(s, AttackFaction.Hostile, from, dir, maxDistance: range);
        }

        int LiveShots() { int n = 0; foreach (var p in root.Sim.Projectiles) if (p.Active) n++; return n; }

        [UnityTest]
        public IEnumerator TwoHundredShotsReuseThePoolWithoutGrowingPastThePeak()
        {
            int peak = 0;
            for (int i = 0; i < 200; i++)
            {
                Fire(new Vector2(-6f, (i % 5) - 2f), Vector2.right, 1.5f);   // short range: they expire quickly
                peak = Mathf.Max(peak, LiveShots());
                yield return null;
                peak = Mathf.Max(peak, LiveShots());
            }
            Assert.LessOrEqual(root.View.ShotViewCount, peak);
            Assert.Greater(root.View.ShotViewCount, 0);
        }

        [UnityTest]
        public IEnumerator AReusedTrailStartsEmpty()
        {
            var a = Fire(new Vector2(-8f, 5f), Vector2.right, 3f);
            int aId = a.ProjectileId;
            // Wait on the condition, not a frame count: editor frames can be far shorter than a
            // sim step's worth of time (60 frames once measured 0.28 s), so A was still flying.
            // The real-time cap turns a broken expiry into a failure instead of a hang.
            float giveUp = Time.realtimeSinceStartup + 5f;
            while (a.Active && a.ProjectileId == aId && Time.realtimeSinceStartup < giveUp) yield return null;
            Assert.IsFalse(a.Active && a.ProjectileId == aId, "A expired at its range");
            yield return null;   // the view is released on the frame after the shot ends
            var b = Fire(new Vector2(8f, -5f), Vector2.left, 3f);
            yield return null;
            var trail = root.View.TrailFor(b.ProjectileId);
            Assert.NotNull(trail, "B has a view");
            Assert.AreEqual(1, root.View.ShotViewCount, "B reused A's view");
            var points = new Vector3[trail.positionCount];
            trail.GetPositions(points);
            var bWorld = Geometry2D.ToWorld(b.Position);
            foreach (var pt in points)
                Assert.Less(Vector2.Distance(new Vector2(pt.x, pt.z), new Vector2(bWorld.x, bWorld.z)), 1.5f,
                    "no trail point from A's flight on the far side of the arena");
        }
    }
}
