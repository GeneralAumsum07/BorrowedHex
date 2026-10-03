using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Spec 5: from every clamped camera extreme, plus the risen travel camera, at 16:9 and
    /// 21:9, every ray meets arena geometry within RayLimit units. Only the layer that never
    /// dissolves (floor, outer ground, ribbon) gets colliders, so the property holds even
    /// with every dressing prop mid-dissolve.
    /// </summary>
    public class ArenaVisibilityPlayModeTests
    {
        // 150, not 60: at the 30-degree pitch the top of the frame looks only 10 degrees below
        // the horizon, so even rays that stay over the arena floor meet it 60-70 units out. The
        // real void criterion is "hits nothing before the far clip" (1000); 150 keeps a margin
        // while still failing any ray that escapes past the outer ground.
        const float RayLimit = 150;
        readonly List<Object> created = new List<Object>();
        WorldArtLibrary art;

        [UnitySetUp]
        public IEnumerator Setup() { art = new WorldArtLibrary(); yield return null; }

        [UnityTest]
        public IEnumerator NoVoidVisible()
        {
            var parent = new GameObject("VisibilityProbe").transform; created.Add(parent.gameObject);
            var cameraObject = new GameObject("VisibilityCamera"); created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 40;
            var extremes = new[] { new Vector2(-1000, -1000), new Vector2(1000, -1000), new Vector2(-1000, 1000), new Vector2(1000, 1000) };
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage);
                var geometry = new WorldGeometry(parent, arena, art, null);
                yield return null;
                var colliders = new List<Collider>();
                var environment = parent.Find("Environment");
                foreach (string name in new[] { "Floor", "Outer ground", ArenaEnclosure.RibbonName })
                {
                    var filter = environment.Find(name).GetComponent<MeshFilter>();
                    var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                    colliders.Add(collider);
                }
                Physics.SyncTransforms();
                try
                {
                    foreach (var extreme in extremes)
                        foreach (float rise in new[] { 0f, 2f }) // 2 = the travel camera's rise (spec 4.4)
                        {
                            var focus = Geometry2D.ToWorld(WorldCameraPolicy.ClampFocus(arena.bounds.center + extreme, arena.bounds));
                            camera.transform.SetPositionAndRotation(focus + WorldCameraPolicy.Offset + Vector3.up * rise, WorldCameraPolicy.Rotation());
                            foreach (var (aspect, columns) in new[] { (16f / 9, 16), (21f / 9, 21) })
                            {
                                camera.aspect = aspect;
                                for (int x = 0; x < columns; x++) for (int y = 0; y < 9; y++)
                                {
                                    var ray = camera.ViewportPointToRay(new Vector3(x / (columns - 1f), y / 8f));
                                    Assert.That(Physics.Raycast(ray, RayLimit), Is.True,
                                        $"{arena.worldTheme} focus {extreme} rise {rise} aspect {aspect:0.00} viewport ({x},{y}) sees void");
                                }
                            }
                        }
                }
                finally { foreach (var collider in colliders) Object.Destroy(collider); geometry.Dispose(); }
                yield return null; // let the deferred destroys land before the next arena is built
            }
        }

        [UnityTest]
        public IEnumerator BeginMorphFromACompletedArenaIsCheap()
        {
            var parent = new GameObject("MorphProbe").transform; created.Add(parent.gameObject);
            var outgoing = new WorldGeometry(parent, WorldArenaLayouts.Create(0), art, null);
            var incoming = new WorldGeometry(parent, WorldArenaLayouts.Create(1), art, null);
            yield return null;
            var watch = Stopwatch.StartNew(); incoming.BeginMorphFrom(outgoing); watch.Stop();
            try { Assert.That(watch.Elapsed.TotalMilliseconds, Is.LessThan(50), "A completed morph must not bake a CPU snapshot (spec 2.2)"); }
            finally { incoming.Dispose(); outgoing.Dispose(); }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in created) if (item != null) Object.Destroy(item);
            created.Clear(); art.Dispose();
            yield return null;
        }
    }
}
