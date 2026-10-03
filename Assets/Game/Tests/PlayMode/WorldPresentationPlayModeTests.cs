using System;
using System.Collections;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BorrowedHex.Tests
{
    public class WorldPresentationPlayModeTests
    {
        GameObject rootObject, artObject, cameraObject;
        GameRoot root;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameRoot.StorageOverride = new MemoryProfileStorage();
            cameraObject = new GameObject("WorldArtTestCamera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            rootObject = new GameObject("WorldArtTestRoot");
            root = rootObject.AddComponent<GameRoot>();
            yield return null;
        }

        Component Attach()
        {
            var type = typeof(GameRoot).Assembly.GetType("BorrowedHex.Presentation.WorldArt.WorldPresentation");
            Assert.That(type, Is.Not.Null, "The independent world observer must exist.");
            artObject = new GameObject("TestWorldArt");
            var component = artObject.AddComponent(type);
            type.GetMethod("BindRoot").Invoke(component, new object[] { root });
            return component;
        }

        [UnityTest]
        public IEnumerator GeometryHasNoPhysicalCollisionAndUsesLogicalFloorBounds()
        {
            Attach();
            yield return null;
            Assert.That(artObject.GetComponentsInChildren<Collider>(true), Is.Empty);
            var floor = artObject.transform.Find("Environment/Floor");
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor.position.y + floor.localScale.y * 0.5f, Is.EqualTo(0).Within(0.001));
            Assert.That(floor.localScale.x, Is.EqualTo(root.config.arena.bounds.width + 2));
        }

        [UnityTest]
        public IEnumerator RestartRebindsTheObserverToTheNewSimulation()
        {
            var component = Attach();
            yield return null;
            var old = root.Sim;
            root.Restart();
            yield return null;
            Assert.That(root.Sim, Is.Not.SameAs(old));
            Assert.That(component.GetType().GetProperty("BoundSim").GetValue(component), Is.SameAs(root.Sim));
        }

        [UnityTest]
        public IEnumerator PracticeBossSelectsSanctumAndFreezesItsPoseOnPause()
        {
            var observer = (WorldPresentation)Attach();
            root.PlaySandbox(); root.Sim.AutoSpawn = false;
            var boss = root.Sim.SpawnBoss();
            yield return null;
            Assert.That(observer.Theme, Is.EqualTo("Sanctum"));
            root.Sim.SetPause(PauseReason.Manual, true);
            yield return null;
            var body = root.View.transform.Find("Enemies/Boss#" + boss.ActorId + "/Billboard/Sprite").GetComponent<SpriteRenderer>();
            var sprite = body.sprite;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(body.sprite, Is.SameAs(sprite));
        }

        [UnityTest]
        public IEnumerator CoverRubbleRestoresAndOldRunEventsDoNotSpawnEffects()
        {
            var observer = (WorldPresentation)Attach();
            yield return null;
            var old = root.Sim;
            var pillar = old.Pillars[0];
            pillar.Advance(pillar.RestoredAt + pillar.MaxDurability * pillar.DecayInterval);
            yield return null;
            Assert.That(artObject.transform.Find("Environment/CoverRubble0").gameObject.activeSelf, Is.True);
            root.Restart();
            yield return null;
            Assert.That(artObject.transform.Find("Environment/CoverRubble0").gameObject.activeSelf, Is.False);
            int count = observer.EffectCount;
            // Exercise the abandoned event source without ticking its unrelated director.
            typeof(BorrowedHex.Runs.SimEvents).GetMethod("RaisePillarCrumbled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(old.Events, new object[] { pillar });
            Assert.That(observer.EffectCount, Is.EqualTo(count), "An abandoned sim must have no art listeners.");
        }

        [UnityTest]
        public IEnumerator BossDeathEffectFinishesWhileResultsHoldTheSimulationClock()
        {
            if (Resources.Load<Texture2D>("WorldArt/Necromancer") == null)
                Assert.Ignore("Death animation requires the owner's optional local art.");
            var observer = (WorldPresentation)Attach();
            root.PlaySandbox(); root.Sim.AutoSpawn = false;
            var boss = root.Sim.SpawnBoss();
            yield return null;
            typeof(BorrowedHex.Runs.SimEvents).GetMethod("RaiseEnemyKilled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(root.Sim.Events, new object[] { boss, default(DamageEvent) });
            root.Sim.Clock.SetPauseReason(PauseReason.Results, true);
            yield return null;
            int withDeath = observer.EffectCount;
            root.Sim.Clock.SetPauseReason(PauseReason.Manual, true);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(observer.EffectCount, Is.EqualTo(withDeath), "Manual pause must also freeze terminal art.");
            root.Sim.Clock.SetPauseReason(PauseReason.Manual, false);
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(observer.EffectCount, Is.LessThan(withDeath), "A terminal pose must not freeze on its first frame.");
        }

        [UnityTest]
        public IEnumerator MissingLibraryPreservesTheBossPlaceholderAndGeometry()
        {
            var observer = (WorldPresentation)Attach();
            typeof(WorldPresentation).GetField("resourceFolder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(observer, "MissingArtForTest");
            root.PlaySandbox(); root.Sim.AutoSpawn = false;
            var boss = root.Sim.SpawnBoss();
            yield return null;
            var body = root.View.transform.Find("Enemies/Boss#" + boss.ActorId + "/Billboard/Sprite").GetComponent<SpriteRenderer>();
            Assert.That(body.sprite, Is.Not.Null);
            Assert.That(body.transform.localScale.x, Is.EqualTo(1));
            Assert.That(observer.EffectCount, Is.Zero);
            Assert.That(artObject.transform.Find("Environment/Floor"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RemovingOnlyTheComponentRestoresBossAndRemovesOwnedChildren()
        {
            root.PlaySandbox(); root.Sim.AutoSpawn = false;
            var boss = root.Sim.SpawnBoss();
            root.Sim.SetPause(PauseReason.Manual, true);
            yield return null;
            var body = root.View.transform.Find("Enemies/Boss#" + boss.ActorId + "/Billboard/Sprite").GetComponent<SpriteRenderer>();
            var original = body.sprite; float scale = body.transform.localScale.x;
            var observer = Attach();
            yield return null;
            UnityEngine.Object.Destroy(observer);
            yield return null;
            yield return null; // Destroy requested inside OnDestroy drains on the next frame.
            Assert.That(body.sprite, Is.SameAs(original));
            Assert.That(body.transform.localScale.x, Is.EqualTo(scale));
            Assert.That(artObject.transform.childCount, Is.Zero);
        }

        void SelectStage(int stage)
        {
            // Exercise the director's stage edge without killing the artist's actors or
            // waiting for an entire run inside a presentation integration test.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BorrowedHex.Runs.ArenaSim).GetMethod("SelectWorldArena", flags).Invoke(root.Sim, new object[] { stage });
            typeof(BorrowedHex.Runs.ArenaSim).GetMethod("RestorePillars", flags).Invoke(root.Sim, null);
        }

        [UnityTest]
        public IEnumerator OrdinaryArenaMorphsDuringCombatAndCameraFollowsWithoutExposingVoid()
        {
            var observer = (WorldPresentation)Attach();
            root.useWorldArenas = true; root.PlaySandbox(); root.Sim.AutoSpawn = false;
            yield return null;
            var camera = cameraObject.GetComponent<Camera>();
            var start = camera.transform.position;
            root.Sim.Player.Position += Vector2.right * 8;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(camera.transform.position.x, Is.GreaterThan(start.x + 3));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo(30).Within(.01));
            Assert.That(camera.fieldOfView, Is.EqualTo(40));
            SelectStage(1);
            yield return null;
            Assert.That(observer.Theme, Is.EqualTo("Graveyard"));
            Assert.That(observer.Travelling, Is.False);
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.False);
            double before = root.Sim.Clock.Now;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(root.Sim.Clock.Now, Is.GreaterThan(before));
            Assert.That(observer.MorphProgress, Is.InRange(0.001f, .1f));
            // Every viewport corner intersects continuous terrain; no background clear
            // colour can leak through at the camera's shallow pitch.
            var plane = new Plane(Vector3.up, new Vector3(0, -.725f, 0));
            var terrain = artObject.transform.Find("ContinuousWorldGround");
            Assert.That(terrain, Is.Not.Null);
            foreach (float x in new[] { 0f, 1f }) foreach (float y in new[] { 0f, 1f })
            {
                var ray = camera.ViewportPointToRay(new Vector3(x, y));
                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                var point = ray.GetPoint(distance);
                Assert.That(Mathf.Abs(point.x - terrain.position.x), Is.LessThan(terrain.localScale.x / 2));
                Assert.That(Mathf.Abs(point.z - terrain.position.z), Is.LessThan(terrain.localScale.z / 2));
            }
            Assert.That(artObject.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SanctumPullPausesForSafetyThenLandsAndResumesCombat()
        {
            var observer = (WorldPresentation)Attach();
            root.useWorldArenas = true; root.PlaySandbox(); root.Sim.AutoSpawn = false;
            yield return null;
            root.Sim.SpawnBoss();
            yield return null;
            Assert.That(observer.Travelling, Is.True);
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.True);
            double before = root.Sim.Clock.Now;
            root.Sim.SetPause(PauseReason.Manual, true);
            var held = root.View.PlayerView.transform.position;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(root.View.PlayerView.transform.position, Is.EqualTo(held));
            Assert.That(root.Sim.Clock.Now, Is.EqualTo(before));
            root.Sim.SetPause(PauseReason.Manual, false);
            yield return new WaitForSecondsRealtime(4.8f);
            Assert.That(observer.Travelling, Is.False);
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.False);
            Assert.That(observer.Theme, Is.EqualTo("Sanctum"));
            root.View.Render(.25f);
            Assert.That(root.Sim.Clock.Now, Is.GreaterThan(before));
            Assert.That(Vector3.Distance(root.View.PlayerView.transform.position, Geometry2D.ToWorld(root.Sim.Player.Position)), Is.LessThan(.1f));
            // The next endless cycle leaves the remote Sanctum through the same safe
            // presentation route; ordinary same-footprint stages still morph in place.
            SelectStage(0); yield return null;
            Assert.That(observer.Travelling, Is.True);
            Assert.That(root.Sim.Clock.HasPauseReason(PauseReason.WorldTransition), Is.True);
        }

        [UnityTest]
        public IEnumerator RestartDuringPullReleasesTheAbandonedPauseEvenWithoutArt()
        {
            var observer = (WorldPresentation)Attach();
            typeof(WorldPresentation).GetField("resourceFolder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(observer, "MissingArtForTest");
            root.useWorldArenas = true; root.PlaySandbox(); root.Sim.AutoSpawn = false;
            yield return null;
            root.Sim.SpawnBoss(); yield return null;
            var old = root.Sim;
            Assert.That(observer.Travelling, Is.True);
            root.Restart(); yield return null;
            Assert.That(observer.Travelling, Is.False);
            Assert.That(old.Clock.HasPauseReason(PauseReason.WorldTransition), Is.False);
            Assert.That(root.Sim.ArenaStage, Is.Zero);
            Assert.That(observer.EffectCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InterruptedMorphDoesNotResurrectPartiallyFormedScenery()
        {
            Attach(); root.useWorldArenas = true; root.PlaySandbox(); root.Sim.AutoSpawn = false;
            yield return null;
            SelectStage(1); yield return new WaitForSecondsRealtime(.3f);
            Renderer incoming = null;
            foreach (var renderer in artObject.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Gravestone") { incoming = renderer; break; }
            Assert.That(incoming, Is.Not.Null);
            float height = incoming.transform.localScale.y;
            Assert.That(height, Is.LessThan(.1f));
            SelectStage(2); yield return null;
            Assert.That(incoming.transform.localScale.y, Is.LessThanOrEqualTo(height + .001f),
                "The old theme retires from its visible size, never from its original full size.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (artObject != null) UnityEngine.Object.Destroy(artObject);
            UnityEngine.Object.Destroy(rootObject);
            UnityEngine.Object.Destroy(cameraObject);
            GameRoot.StorageOverride = null;
            yield return null;
        }
    }
}
