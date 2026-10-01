using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Data
{
    /// <summary>
    /// The arena's logical layout. The editor bootstrap builds the visible floor/walls from
    /// this same data and the simulation collides against it, so visuals and collision can
    /// never drift apart.
    /// </summary>
    [Serializable]
    public class ArenaLayout
    {
        [Tooltip("Playable interior on the XZ plane (x = world X, y = world Z).")]
        public Rect bounds = new Rect(-12f, -8f, 24f, 16f);
        public float wallThickness = 1f;
        public float wallHeight = 1.2f;
        public List<Rect> pillars = new List<Rect>
        {
            new Rect(-6.6f, 2.9f, 1.2f, 1.2f),
            new Rect(5.4f, 2.9f, 1.2f, 1.2f),
            new Rect(-6.6f, -4.1f, 1.2f, 1.2f),
            new Rect(5.4f, -4.1f, 1.2f, 1.2f),
        };
        public Vector2 playerSpawn = new Vector2(0f, -4f);
        public Vector2 lanternPosition = new Vector2(0f, 6.6f);

        /// <summary>Every solid box: the four border walls plus pillars.</summary>
        public List<Rect> BuildObstacles()
        {
            var b = bounds;
            float w = wallThickness;
            var list = new List<Rect>
            {
                new Rect(b.xMin - w, b.yMin - w, b.width + 2 * w, w), // south
                new Rect(b.xMin - w, b.yMax, b.width + 2 * w, w),     // north
                new Rect(b.xMin - w, b.yMin, w, b.height),            // west
                new Rect(b.xMax, b.yMin, w, b.height),                // east
            };
            list.AddRange(pillars);
            return list;
        }
    }

    [Serializable]
    public class CameraRig
    {
        // Fixed elevated camera (section 1: Octopath-like 2.5D). Pitch is steep enough that
        // the whole arena is readable but shallow enough that billboard sprites keep height.
        public Vector3 position = new Vector3(0f, 17f, -17.5f);
        public Vector3 lookAt = new Vector3(0f, 0f, -2.2f);
        public float fieldOfView = 38f;
    }

    /// <summary>
    /// Root definition asset: immutable authoring data and tuning. Never written at runtime —
    /// mutable run state lives in the simulation, persistent data in the player profile.
    /// Sections grow phase by phase; every field initializer is the plan's starting default.
    /// </summary>
    [CreateAssetMenu(menuName = "Borrowed Hex/Game Config", fileName = "GameConfig")]
    public partial class GameConfig : ScriptableObject
    {
        public ArenaLayout arena = new ArenaLayout();
        public CameraRig cameraRig = new CameraRig();

        [Header("Presentation (placeholders)")]
        [Tooltip("URP Unlit base material; tinted instances are made at runtime.")]
        public Material unlitMaterial;
        [Tooltip("URP Lit base material for the floor and walls.")]
        public Material litMaterial;

        /// <summary>Config with code defaults, for tests and as a missing-asset fallback.</summary>
        public static GameConfig CreateDefault()
        {
            var c = CreateInstance<GameConfig>();
            c.name = "GameConfig (defaults)";
            return c;
        }
    }
}
