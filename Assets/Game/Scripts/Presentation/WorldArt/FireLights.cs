using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Eight flickering point lights at the flame anchors (spec 2.4). The flicker runs on
    /// unscaled time, so braziers stay alive behind the pause menu. `fade` lets the Sanctum
    /// reveal bring them up from darkness; a held reveal holds its fade.
    /// </summary>
    public sealed class FireLights : IDisposable
    {
        public const int Count = 8;
        public const float Range = 6, Intensity = 2.5f;
        readonly GameObject root;
        readonly Light[] lights = new Light[Count];
        public IReadOnlyList<Light> Lights => lights;

        public FireLights(Transform parent, ArenaLayout arena, Color color)
        {
            root = new GameObject("FireLights"); root.transform.SetParent(parent, false);
            for (int i = 0; i < Count; i++)
            {
                var holder = new GameObject("Fire" + i); holder.transform.SetParent(root.transform, false);
                holder.transform.position = WorldGeometry.FlameAnchor(arena, i);
                var light = holder.AddComponent<Light>();
                light.type = LightType.Point; light.range = Range; light.color = color; light.intensity = 0;
                // No point-light shadows: eight cube shadow maps cost more than they show here.
                light.shadows = LightShadows.None;
                lights[i] = light;
            }
        }

        public void Render(float unscaledTime, float fade)
        {
            fade = Mathf.Clamp01(fade);
            for (int i = 0; i < Count; i++)
            {
                // Offset per light so the eight never pulse in step.
                float flicker = .8f + .4f * Mathf.Clamp01(Mathf.PerlinNoise(unscaledTime * 3.1f + i * 7.3f, i * 1.7f));
                lights[i].intensity = Intensity * fade * flicker;
            }
        }

        public void Dispose() => WorldArtLibrary.Release(root);
    }
}
