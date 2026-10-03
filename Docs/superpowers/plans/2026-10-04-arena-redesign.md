# Arena Visual Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the pixel-atlas arenas with painted floors, a closed-in 3D enclosure built from CC0 kit models, and real moon and fire lighting, seen from a 50° camera, so that no void is ever visible.

**Architecture:** Everything is still built at runtime from code recipes, with no prefabs. Pure, EditMode-testable units decide *what* goes where: `ArenaDressing`, `CoverModels.Fit`, `PaintedFloorGenerator` and `WorldLightingPolicy`. Thin Unity units build it: `PaintedFloor`, `ArenaEnclosure`, `WorldModelLibrary` and `FireLights`. `WorldGeometry` keeps its public effect API, so `WorldPresentation`'s morph, reveal, decay and occlusion code barely changes. A new URP shader, `BorrowedHex/PaintedWorld`, keeps PixelWorld's property interface and adds lighting and shadows.

**Tech Stack:** Unity 6000.3.25f1, URP 17 (Forward+, `_CLUSTER_LIGHT_LOOP`), C#, NUnit (EditMode and PlayMode), HLSL. Kenney Graveyard Kit 5.0 and Quaternius Ultimate Nature Pack (CC0 FBX). Szadi "Open World And Cave Dungeon" overlays (public domain; the pack may not be resold) are used only as decals baked into the floor JPGs.

**Spec:** `Docs/superpowers/specs/2026-10-04-arena-redesign-design.md` (APPROVED, with amendments A1–A3).

## Global Constraints

- Gameplay is untouched: `ArenaLayout` rects, sizes, cover positions and `ArenaSim` stay authoritative. This work is presentation only.
- Camera pitch is **50°**, distance about **20**, FOV **40**. `ClampFocus` margins are unchanged (x ±6, y +5/-5).
- Enclosure ring: about **9** deep on the north, **about 16** on east and west, about **6** on the south. Heights: north **6–9**, east and west **about 2 at the south end rising to about 7 at the north**, south **≤ 1.2**.
- Nothing taller than **0.3 units** may sit inside the play rect except cover.
- Floors are **40 px per unit**, **1920×1440** for 48×36, committed as **JPG quality 92**. Import settings: bilinear, mipmaps, Read/Write enabled.
- **One floor mesh** (A1). The shader uses Forward+ `_CLUSTER_LIGHT_LOOP`/`LIGHT_LOOP_BEGIN`. Ambient arrives as the global `_WorldAmbient`.
- PaintedWorld keeps `_BaseMap _FromMap _BaseColor _Morph _Visible _Retiring _GlitchTime _Glitches _Reveal _Emission _FogAmount _Occlusion`. It drops `_NorthLimit`.
- PixelWorld is **not deleted**.
- Kit models are committed under `Assets/Game/Resources/WorldModels/{Kenney,Quaternius}/`, each kit with its `License.txt`.
- Excluded models: pumpkins, hay, Kenney characters, and the snow, autumn, cactus and palm variants.
- A missing model calls `Debug.LogError` with its name and that placement is skipped. A test fails if any recipe model is missing.
- `BeginMorphFrom` takes **< 50 ms** when the outgoing morph is complete. An interrupted morph bakes at a **512** cap.
- Mobile is not a target; the Mobile quality level only needs to not break.
- **Repo rules (the owner's, non-negotiable):**
  - Commit locally only and **never push**.
  - No Co-Authored-By trailer and no tool credit in any commit.
  - Stage named paths only, and run `git diff --cached --name-only` before every commit.
  - Write the message to `.superpowers/msg.txt` (BOM-free) and commit with `git commit -q -F .superpowers/msg.txt`.
  - **Never stage:** `Assets/Sprites.meta`, `Assets/Sprites/*.png.meta`, `Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/*.asset`, `Assets/_Recovery*`, `Docs/LORE_DOCUMENT.md`, `Docs/REWORK_PLAN.md`.
- Comment code heavily, explaining *why*, as the surrounding code does.

## Tooling (read once)

- **Shells.** The Bash tool is broken on this machine. Use PowerShell, and run scripts through `& "C:\Program Files\Git\bin\bash.exe" <script>`.
- **Compile only:** `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/uc.sh`. A clean compile prints `failed: False`.
- **Gate:** `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit|play|both TAG [playmode-filter]`, with timeout 1200000.
  - It prints `COMPILE NOT CLEAN - stopping` on compile errors.
  - The EditMode suite always runs in full; the PlayMode suite takes an optional name filter.
  - Pass shows as an `EditMode`/`PlayMode` summary with no `NOT PASSED` lines.
- **Run C# in the live editor:** `unity command eval --code '<C#>; return 1;'`.
- **Screenshot:** `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/cap.sh NAME` writes `.superpowers/shots/NAME.png`.
- **Source downloads:** `C:\Users\Rachit\Downloads\itch_downloads\` (`kenney_graveyard-kit_5.0`, `Ultimate Nature Pack by Quaternius`, `OpenWorldandcavedung_1.0`).

## Review Focus

1. **Interrupted morph** (stage change while `_Morph` < 1). A reasonable person expects the outgoing floor to cross-fade from what was on screen, with no flash to a stale texture. Pinned by `InterruptedMorphSnapshotsWhatWasOnScreen` in Task 9.
2. **Missing kit file on a fresh checkout or a renamed FBX.** Expect an error naming the model, while the arena still builds with no exception and no void. Pinned by `MissingModelLogsAndSkips` in Task 6.
3. **Dissolving cover casting shadows.** A crumbling prop must not leave a solid shadow. This is covered by the ShadowCaster coverage clip (Task 2) and checked in the Task 12 screenshots, since shadow maps can't be asserted cheaply.
4. **Tutorial and non-world themes** (`WorldArtPolicy.Theme` may hand `ApplyTheme` a theme for a non-world arena). Expect sane lighting rather than black. Pinned by `UnknownThemeFallsBackToCourtyard` in Task 11.
5. **Pause during the Sanctum reveal.** Fire lights must keep their reveal fade and not pop to full. Their flicker runs on unscaled time by design, and the reveal fade is driven by `travel.Elapsed`, which pause holds. Pinned by `RevealFadeScalesFireLights` in Task 11.

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `Assets/Game/Scripts/Presentation/WorldArt/WorldCameraPolicy.cs` | modify | 50° pitch, 20 distance |
| `Assets/Game/Resources/WorldShaders/PaintedWorld.shader` | create | Lit painted shader with the PixelWorld interface, shadows, depth and normals |
| `Assets/Game/Scripts/Presentation/WorldArt/PaintedMaterials.cs` | create | Creates PaintedWorld material instances |
| `Assets/Game/Scripts/Presentation/WorldArt/PaintedImage.cs` | create | Float RGBA image, blob extraction, bilinear stamp (pure) |
| `Assets/Game/Scripts/Presentation/WorldArt/PaintedFloorGenerator.cs` | create | Noise, flagstones, bases, decals; Floor/Outer/Ribbon per theme (pure) |
| `Assets/Game/Editor/PaintedFloorBaker.cs` | create | Menu that bakes floor, outer and ribbon JPGs |
| `Assets/Game/Resources/WorldFloors/*.jpg` | generated | 4 floors, 4 outers, 4 ribbons |
| `Assets/Game/Scripts/Presentation/WorldArt/WorldModelCatalog.cs` | create | The allowed model names and their Resources paths |
| `Assets/Game/Editor/WorldModelImporter.cs` | create | Copies the FBX subset and licences, bakes the night colormap |
| `Assets/Game/Resources/WorldModels/**` | generated | Committed CC0 models |
| `Assets/Game/Scripts/Presentation/WorldArt/WorldModelLibrary.cs` | create | Spawns models with PaintedWorld materials; crystal mesh |
| `Assets/Game/Scripts/Presentation/WorldArt/ArenaDressing.cs` | create | Per-theme recipes and deterministic placement (pure) |
| `Assets/Game/Scripts/Presentation/WorldArt/CoverModels.cs` | create | Cover kind → model; footprint fit (pure part plus a builder) |
| `Assets/Game/Scripts/Presentation/WorldArt/PaintedFloor.cs` | create | Floor quad, outer ground, snapshot short-circuit |
| `Assets/Game/Scripts/Presentation/WorldArt/ArenaEnclosure.cs` | create | Ribbon mesh plus dressing instances |
| `Assets/Game/Scripts/Presentation/WorldArt/WorldGeometry.cs` | rewrite | Orchestration plus the unchanged effect API |
| `Assets/Game/Scripts/Presentation/WorldArt/WorldLightingPolicy.cs` | create | Per-theme moon, ambient, fog and fire values (pure) |
| `Assets/Game/Scripts/Presentation/WorldArt/FireLights.cs` | create | 8 flickering point lights |
| `Assets/Game/Scripts/Presentation/WorldArt/WorldPresentation.cs` | modify | Remove the backdrop; add the lighting rig |
| `WorldBackdrop.cs`, `WorldPixelSurfaces.cs`, `WorldBoundaryPolicy.cs` | delete | Superseded |
| `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs` | create | All new EditMode tests |
| `Assets/Game/Tests/PlayMode/ArenaVisibilityPlayModeTests.cs` | create | No-void rays, morph hitch, screenshots |
| `Assets/Game/Tests/EditMode/WorldPixelRevisionTests.cs`, `Assets/Game/Tests/PlayMode/WorldPresentationPlayModeTests.cs` | modify | Update the old expectations |

Every new runtime file uses `namespace BorrowedHex.Presentation.WorldArt`, and every new test uses `namespace BorrowedHex.Tests`. Unity creates `.meta` files; stage each new file's `.meta` with it.

---

### Task 1: Camera at 50°

**Files:**
- Modify: `Assets/Game/Scripts/Presentation/WorldArt/WorldCameraPolicy.cs`
- Modify: `Assets/Game/Tests/EditMode/WorldPixelRevisionTests.cs` (the `CameraUsesTheRequestedTwentyFiveDegreePitch` test)
- Modify: `Assets/Game/Tests/PlayMode/WorldPresentationPlayModeTests.cs:177`

**Interfaces:**
- Produces: `WorldCameraPolicy.Pitch` (50), `WorldCameraPolicy.Distance` (20), `Rotation()`, `Offset` (≈ (0, 15.32, -12.86)), and `ClampFocus` unchanged.

- [ ] **Step 1: Replace the pitch test.** In `WorldPixelRevisionTests.cs`, replace the whole `CameraUsesTheRequestedTwentyFiveDegreePitch` method with:

```csharp
        [Test]
        public void CameraLooksDownAtFiftyDegreesFromTwentyUnits()
        {
            // Rogue's Odyssey reads as 2.5D because the camera looks DOWN onto the floor.
            // At 25 degrees the frustum saw far past the north wall into the void.
            Assert.That(WorldCameraPolicy.Rotation().eulerAngles.x, Is.EqualTo(50).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.magnitude, Is.EqualTo(20).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.y, Is.EqualTo(15.32f).Within(.01));
            Assert.That(WorldCameraPolicy.Offset.z, Is.EqualTo(-12.86f).Within(.01));
        }
```

In `WorldPresentationPlayModeTests.cs` line 177, change `Is.EqualTo(25)` to `Is.EqualTo(50)`.

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t1red`
Expected: a `NOT PASSED` line naming `CameraLooksDownAtFiftyDegreesFromTwentyUnits`, saying expected 50 but was 25.

- [ ] **Step 3: Implement.** Replace `Rotation` and `Offset` in `WorldCameraPolicy.cs` (keep `ClampFocus` as it is):

```csharp
        // 50 degrees looks down onto the arena (the 2.5D read the owner asked for) and keeps
        // the north edge of the frustum within ~9 units of the north wall, where the
        // enclosure's statement wall blocks the line of sight. FOV stays 40.
        public const float Pitch = 50, Distance = 20;
        public static Quaternion Rotation() => Quaternion.Euler(Pitch, 0, 0);
        // Derived from the pitch, not hand-typed, so the two can never drift apart.
        public static Vector3 Offset => Rotation() * Vector3.back * Distance;
```

- [ ] **Step 4: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh both t1 WorldPresentation`
Expected: the EditMode and PlayMode summaries show 0 failures and there are no `NOT PASSED` lines.

- [ ] **Step 5: Commit.**

```powershell
git add Assets/Game/Scripts/Presentation/WorldArt/WorldCameraPolicy.cs Assets/Game/Tests/EditMode/WorldPixelRevisionTests.cs Assets/Game/Tests/PlayMode/WorldPresentationPlayModeTests.cs
git diff --cached --name-only
```
Message file: `Raise the world camera to a 50 degree pitch`. Then run `git commit -q -F .superpowers/msg.txt`.

---

### Task 2: PaintedWorld shader and material factory

**Files:**
- Create: `Assets/Game/Resources/WorldShaders/PaintedWorld.shader`
- Create: `Assets/Game/Scripts/Presentation/WorldArt/PaintedMaterials.cs`
- Create: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Produces: `PaintedMaterials.ShaderName` = `"BorrowedHex/PaintedWorld"`, and `PaintedMaterials.Create(string name, Texture texture, Color tint) : Material`. The caller owns the result and releases it with `WorldArtLibrary.Release`.
- Produces: the global shader colour `_WorldAmbient`.

- [ ] **Step 1: Write the failing test.** Create `ArenaRedesignTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Data;
using BorrowedHex.Presentation.WorldArt;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>EditMode coverage for the arena redesign (spec 2026-10-04).</summary>
    public class ArenaRedesignTests
    {
        static readonly string[] Themes = { "Courtyard", "Graveyard", "Cave", "Sanctum" };

        [Test]
        public void PaintedWorldCompilesAndKeepsThePixelWorldInterface()
        {
            var shader = Shader.Find(PaintedMaterials.ShaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "PaintedWorld has compile errors");
            // WorldGeometry's effect code drives these by name; losing one silently breaks
            // decay, morph, reveal or occlusion with no compile error anywhere.
            foreach (var name in new[] { "_BaseMap", "_FromMap", "_BaseColor", "_Morph", "_Visible", "_Retiring",
                         "_GlitchTime", "_Glitches", "_Reveal", "_Emission", "_FogAmount", "_Occlusion" })
                Assert.That(shader.FindPropertyIndex(name), Is.GreaterThanOrEqualTo(0), name);
            Assert.That(shader.FindPropertyIndex("_NorthLimit"), Is.EqualTo(-1), "The apron clip is retired");
            var material = PaintedMaterials.Create("probe", Texture2D.whiteTexture, Color.white);
            try
            {
                foreach (var pass in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                    Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0), pass);
                Assert.That(material.GetTexture("_FromMap"), Is.SameAs(Texture2D.whiteTexture));
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t2red`
Expected: `COMPILE NOT CLEAN - stopping`, with an error that `PaintedMaterials` does not exist.

- [ ] **Step 3: Write `PaintedMaterials.cs`.**

```csharp
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// One place that knows the painted shader's name and its texture slots. _FromMap starts
    /// equal to _BaseMap so a material that never morphs shows its own texture.
    /// </summary>
    public static class PaintedMaterials
    {
        public const string ShaderName = "BorrowedHex/PaintedWorld";

        public static Material Create(string name, Texture texture, Color tint)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // Loud rather than silent: a pink or unlit arena would hide the real cause.
                Debug.LogError("PaintedWorld shader missing; falling back to URP Lit for " + name);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var material = new Material(shader) { name = name };
            if (texture != null) { material.SetTexture("_BaseMap", texture); material.SetTexture("_FromMap", texture); }
            material.SetColor("_BaseColor", tint);
            return material;
        }
    }
}
```

- [ ] **Step 4: Write `PaintedWorld.shader`.**

```hlsl
Shader "BorrowedHex/PaintedWorld"
{
    // Painted, lit counterpart of PixelWorld. Same property interface so WorldGeometry's
    // decay / morph / reveal / occlusion code drives it unchanged; _NorthLimit is gone
    // because the enclosure, not a clip plane, now hides the outside.
    Properties
    {
        _BaseMap("Painted texture", 2D) = "white" {}
        _FromMap("Previous world", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Morph("World morph", Range(0,1)) = 1
        _Visible("Coverage", Range(0,1)) = 1
        _Retiring("Retiring coverage", Float) = 0
        _GlitchTime("Gameplay time", Float) = 0
        _Glitches("Glitch bands", Float) = 1
        _Reveal("Room light", Range(0,1)) = 1
        _Emission("Rune light", Color) = (0,0,0,0)
        _FogAmount("Atmospheric depth", Range(0,1)) = 1
        _Occlusion("Actor visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_FromMap); SAMPLER(sampler_FromMap);
        // Identical CBUFFER in every pass keeps the material SRP-Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _Emission;
        float _Morph, _Visible, _Retiring, _GlitchTime, _Glitches, _Reveal, _FogAmount, _Occlusion;
        CBUFFER_END

        float Hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

        // The world-space grain dissolve, verbatim from PixelWorld, so decaying cover looks
        // the same. Shadows skip actor occlusion: the cutout exists to show the player
        // THROUGH a pillar, not to punch holes in the pillar's shadow.
        void ClipCoverage(float3 world, bool includeOcclusion)
        {
            float grain = Hash(floor(world * 12));
            float visible = _Visible * (includeOcclusion ? _Occlusion : 1);
            float coverage = _Retiring > .5 ? grain - (1 - visible) : visible - grain;
            clip(coverage - .00001);
        }

        // Morph patch mix plus glitch bands, verbatim from PixelWorld. The CPU snapshot in
        // PaintedFloor reproduces the same patch hash, so an interrupted morph matches.
        half4 MorphColor(float2 uv)
        {
            float patch = Hash(float3(floor(uv * 18), 5));
            float mix = saturate(_Morph * 1.65 - patch * .65);
            float2 incoming = uv;
            if (_Glitches > .5 && _Morph > .02 && _Morph < .98 &&
                fmod(floor(uv.y * 96) + floor(_GlitchTime * 6) * 3, 53) < 1)
            { incoming.x += .075; mix = saturate(mix + .4); }
            half4 old = SAMPLE_TEXTURE2D(_FromMap, sampler_FromMap, uv);
            half4 next = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, incoming);
            return lerp(old, next, mix) * _BaseColor;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // Set by WorldPresentation from the same colour as RenderSettings.ambientLight, so
            // ambient does not depend on the scene's ambient mode or a baked probe.
            float4 _WorldAmbient;
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float3 normal : TEXCOORD2; float fog : TEXCOORD3; float4 color : COLOR; };

            Output Vert(Input input)
            {
                Output o;
                o.world = TransformObjectToWorld(input.vertex.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(input.normal);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap); o.color = input.color;
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }

            // Wrapped Lambert: the terminator is pushed past 90 degrees so forms turn softly
            // into shadow. No specular at all; that is the painted look.
            float Wrap(float3 n, float3 l) { return saturate((dot(n, l) + .5) / 1.5); }

            half4 Frag(Output input) : SV_Target
            {
                ClipCoverage(input.world, true);
                half4 color = MorphColor(input.uv) * input.color;
                clip(color.a - .05);
                float3 n = normalize(input.normal);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.position);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                Light main = GetMainLight(TransformWorldToShadowCoord(input.world));
                float3 light = _WorldAmbient.rgb * ao.indirectAmbientOcclusion
                    + main.color * Wrap(n, main.direction) * main.shadowAttenuation * ao.directAmbientOcclusion;
                #if defined(_ADDITIONAL_LIGHTS)
                // The Forward+ cluster loop needs these two InputData fields in scope.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.world;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light extra = GetAdditionalLight(lightIndex, input.world, half4(1, 1, 1, 1));
                    light += extra.color * Wrap(n, extra.direction) * extra.distanceAttenuation * extra.shadowAttenuation;
                LIGHT_LOOP_END
                #endif
                // Same order as PixelWorld: the room-light reveal darkens, rune light adds.
                color.rgb = color.rgb * light * _Reveal + _Emission.rgb;
                color.rgb = lerp(color.rgb, MixFog(color.rgb, input.fog), _FogAmount);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct ShadowInput { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct ShadowOutput { float4 position : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; };
            ShadowOutput ShadowVert(ShadowInput input)
            {
                ShadowOutput o;
                float3 world = TransformObjectToWorld(input.vertex.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normal);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction = normalize(_LightPosition - world);
                #else
                float3 direction = _LightDirection;
                #endif
                o.position = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normal, direction)));
                o.world = world; o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            half4 ShadowFrag(ShadowOutput input) : SV_Target
            {
                // A dissolving prop must not cast a solid shadow (spec 2.1).
                ClipCoverage(input.world, false);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - .05);
                return 0;
            }
            ENDHLSL
        }

        // SSAO is enabled on PC_Renderer and reads the depth/normals prepass; without these
        // passes the painted world would be missing from it (no contact darkening at wall feet).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct DepthInput { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct DepthOutput { float4 position : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; float3 normal : TEXCOORD2; };
            DepthOutput DepthVert(DepthInput input)
            {
                DepthOutput o; o.world = TransformObjectToWorld(input.vertex.xyz);
                o.position = TransformWorldToHClip(o.world); o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normal = TransformObjectToWorldNormal(input.normal); return o;
            }
            half DepthFrag(DepthOutput input) : SV_Target
            {
                ClipCoverage(input.world, true);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - .05);
                return input.position.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct DepthInput { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct DepthOutput { float4 position : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; float3 normal : TEXCOORD2; };
            DepthOutput DepthVert(DepthInput input)
            {
                DepthOutput o; o.world = TransformObjectToWorld(input.vertex.xyz);
                o.position = TransformWorldToHClip(o.world); o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normal = TransformObjectToWorldNormal(input.normal); return o;
            }
            half4 NormalFrag(DepthOutput input) : SV_Target
            {
                ClipCoverage(input.world, true);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - .05);
                float3 n = normalize(input.normal);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octahedral = saturate(PackNormalOctQuadEncode(n) * .5 + .5);
                return half4(PackFloat2To888(octahedral), 0);
                #else
                return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 5: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t2`
Expected: the EditMode summary shows 0 failures. If `ShaderHasError` is true, read the error with `unity command eval --code 'var s=UnityEngine.Shader.Find("BorrowedHex/PaintedWorld"); var m=UnityEditor.ShaderUtil.GetShaderMessages(s); return string.Join("\n", System.Linq.Enumerable.Select(m, x=>x.message+" @"+x.line));'` and fix the HLSL. Don't weaken the test.

- [ ] **Step 6: Commit.** Stage `Assets/Game/Resources/WorldShaders/PaintedWorld.shader(.meta)`, `Assets/Game/Scripts/Presentation/WorldArt/PaintedMaterials.cs(.meta)` and `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs(.meta)`. Message: `Add the lit PaintedWorld shader with shadow and depth passes`.


---

### Task 3: Painted image and the floor generator (pure)

This ports the approved swatch generator (`Swatch.cs` from brainstorming) into runtime code with no Unity objects, so it can be tested in EditMode. The approved painted settings were bilinear decals at scale 0.5 with the parameters below. Moonlight and Pixelate are **not** ported: real lighting replaces Moonlight, and the owner rejected the pixel style. The Cave and Sanctum looks were never swatched, so their values are a first pass that the owner reviews at the Task 12 sign-off.

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/PaintedImage.cs`
- Create: `Assets/Game/Scripts/Presentation/WorldArt/PaintedFloorGenerator.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs` (append tests)

**Interfaces:**
- Produces: `PaintedImage(int width, int height)` with public `Width`, `Height` and `float[] R, G, B, A`, plus `FromColors(Color32[], int, int)`, `ToColors() : Color32[]`, `Luminance(int x, int y) : float` and `MeanLuminance() : float`.
- Produces: `DecalSheet` (`Image`, `List<RectInt> Sprites`) and the constructor `DecalSheet(PaintedImage image, int minSize)`.
- Produces: `PaintedFloorGenerator.PixelsPerUnit` (40) and the decal keys `"grass"`, `"rocks"`, `"vegetation"`.
- Produces: `Floor(string theme, int width, int height, IReadOnlyDictionary<string, DecalSheet> decals) : PaintedImage`. `decals` may be null.
- Produces: `Outer(string theme, int size) : PaintedImage` and `Ribbon(string theme, int width, int height) : PaintedImage`. Both tile seamlessly.
- Produces: `FbmTiled(float u, float v, int seed, int octaves, int periodX, int periodY) : float`.

- [ ] **Step 1: Append the failing tests** inside `ArenaRedesignTests`:

```csharp
        [Test]
        public void FloorGenerationIsDeterministic()
        {
            var a = PaintedFloorGenerator.Floor("Graveyard", 96, 72, null);
            var b = PaintedFloorGenerator.Floor("Graveyard", 96, 72, null);
            Assert.That(a.R, Is.EqualTo(b.R)); Assert.That(a.G, Is.EqualTo(b.G)); Assert.That(a.B, Is.EqualTo(b.B));
        }

        [Test]
        public void EveryThemeFloorIsNightDark()
        {
            // Real lighting brightens later; the albedo itself must stay in the nightmare range
            // or the moonlit arena reads as daytime.
            foreach (var theme in Themes)
                Assert.That(PaintedFloorGenerator.Floor(theme, 160, 120, null).MeanLuminance(), Is.InRange(.02f, .30f), theme);
        }

        [Test]
        public void TiledNoiseWrapsExactlyOnItsPeriod()
        {
            for (int i = 0; i < 20; i++)
            {
                float u = i * .37f, v = i * .53f;
                float at = PaintedFloorGenerator.FbmTiled(u, v, 9, 4, 8, 4);
                Assert.That(PaintedFloorGenerator.FbmTiled(u + 8, v, 9, 4, 8, 4), Is.EqualTo(at).Within(1e-5));
                Assert.That(PaintedFloorGenerator.FbmTiled(u, v + 4, 9, 4, 8, 4), Is.EqualTo(at).Within(1e-5));
            }
        }

        [Test]
        public void OuterAndRibbonTexturesTileWithoutVisibleSeams()
        {
            foreach (var theme in Themes)
                foreach (var image in new[] { PaintedFloorGenerator.Outer(theme, 64), PaintedFloorGenerator.Ribbon(theme, 64, 32) })
                {
                    // The wrap pair (last column, first column) must differ no more than an
                    // ordinary neighbouring pair does, or the repeat shows as a line.
                    float seam = 0, interior = 0;
                    for (int y = 0; y < image.Height; y++)
                    {
                        seam += Mathf.Abs(image.Luminance(image.Width - 1, y) - image.Luminance(0, y));
                        interior += Mathf.Abs(image.Luminance(image.Width / 2, y) - image.Luminance(image.Width / 2 - 1, y));
                    }
                    Assert.That(seam, Is.LessThanOrEqualTo(interior * 3 + .05f * image.Height), theme);
                }
        }

        [Test]
        public void DecalsAreFoundAsBlobsAndScatterStaysInsideTheImage()
        {
            var sheet = new PaintedImage(64, 64);
            for (int i = 0; i < sheet.A.Length; i++) sheet.A[i] = 0;
            foreach (var (x0, y0) in new[] { (4, 4), (40, 36) })
                for (int y = y0; y < y0 + 16; y++) for (int x = x0; x < x0 + 16; x++)
                { int i = y * 64 + x; sheet.A[i] = 1; sheet.R[i] = sheet.G[i] = sheet.B[i] = 1; }
            var decals = new DecalSheet(sheet, 8);
            Assert.That(decals.Sprites.Count, Is.EqualTo(2));
            var floor = new PaintedImage(20, 20);
            // Stamps that start off-image must clip, not throw.
            PaintedFloorGenerator.Scatter(floor, decals, 50, 1, 1f, Color.white, 0, 1, 64);
            Assert.That(floor.R.Any(v => v > .5f), Is.True);
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t3red`
Expected: `COMPILE NOT CLEAN - stopping` (`PaintedFloorGenerator`/`PaintedImage` do not exist).

- [ ] **Step 3: Write `PaintedImage.cs`.**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Float RGBA working image for the floor painter. Floats (not Color32) because several
    /// layers multiply and lerp; rounding to bytes between layers bands the dark gradients.
    /// Row 0 is the bottom row, matching Texture2D.SetPixels32.
    /// </summary>
    public sealed class PaintedImage
    {
        public readonly int Width, Height;
        public readonly float[] R, G, B, A;

        public PaintedImage(int width, int height)
        {
            Width = width; Height = height;
            R = new float[width * height]; G = new float[width * height]; B = new float[width * height]; A = new float[width * height];
            for (int i = 0; i < A.Length; i++) A[i] = 1;
        }

        public static PaintedImage FromColors(Color32[] pixels, int width, int height)
        {
            var image = new PaintedImage(width, height);
            for (int i = 0; i < pixels.Length; i++)
            {
                image.R[i] = pixels[i].r / 255f; image.G[i] = pixels[i].g / 255f;
                image.B[i] = pixels[i].b / 255f; image.A[i] = pixels[i].a / 255f;
            }
            return image;
        }

        public Color32[] ToColors()
        {
            var pixels = new Color32[R.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(Byte(R[i]), Byte(G[i]), Byte(B[i]), 255);
            return pixels;
        }

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255), 0, 255);
        public float Luminance(int x, int y) { int i = y * Width + x; return R[i] * .3f + G[i] * .59f + B[i] * .11f; }

        public float MeanLuminance()
        {
            double sum = 0;
            for (int i = 0; i < R.Length; i++) sum += R[i] * .3f + G[i] * .59f + B[i] * .11f;
            return (float)(sum / R.Length);
        }
    }

    /// <summary>
    /// An overlay atlas (separate blobs on transparency) plus each blob's bounds. Connected
    /// components on a 4x-downsampled alpha mask: the source sheets are up to 2304 px square.
    /// </summary>
    public sealed class DecalSheet
    {
        public readonly PaintedImage Image;
        public readonly List<RectInt> Sprites = new List<RectInt>();

        public DecalSheet(PaintedImage image, int minSize)
        {
            Image = image;
            const int K = 4;
            int w = image.Width / K, h = image.Height / K;
            var mask = new bool[w * h]; var seen = new bool[w * h]; var stack = new Stack<int>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                mask[y * w + x] = image.A[(y * K + K / 2) * image.Width + x * K + K / 2] > .1f;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
                stack.Push(start); seen[start] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop(), px = p % w, py = p / w;
                    x0 = Mathf.Min(x0, px); y0 = Mathf.Min(y0, py); x1 = Mathf.Max(x1, px); y1 = Mathf.Max(y1, py);
                    // Left/right neighbours must not wrap onto the previous/next row.
                    if (px > 0) Visit(p - 1); if (px < w - 1) Visit(p + 1);
                    if (py > 0) Visit(p - w); if (py < h - 1) Visit(p + w);
                }
                var rect = new RectInt(x0 * K, y0 * K, (x1 - x0 + 1) * K, (y1 - y0 + 1) * K);
                if (rect.width >= minSize && rect.height >= minSize) Sprites.Add(rect);
            }
            void Visit(int q) { if (mask[q] && !seen[q]) { seen[q] = true; stack.Push(q); } }
        }
    }
}
```

- [ ] **Step 4: Write `PaintedFloorGenerator.cs`.**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Paints one arena floor as a single image (no tiling, so no visible repeat), plus small
    /// seamless textures for the outer ground and the enclosure ribbon. Ported from the swatch
    /// generator the owner approved on 2026-10-04; the Courtyard and Graveyard numbers are
    /// those approved values verbatim. Pure: no Unity objects, deterministic per theme.
    /// </summary>
    public static class PaintedFloorGenerator
    {
        public const int PixelsPerUnit = 40;
        // Decal counts were approved on a 768x480 swatch; scale them by area so density holds.
        const float SwatchArea = 768f * 480f;

        // ---------- noise ----------
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        // Smoothstep value noise: soft low-frequency drift reads as "painted".
        static float Noise(float x, float y, int seed, int periodX = 0, int periodY = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x1 = xi + 1, y1 = yi + 1;
            // Wrapping the integer lattice makes the noise periodic, which is what lets the
            // outer-ground and ribbon textures repeat without a seam.
            if (periodX > 0) { xi = Mod(xi, periodX); x1 = Mod(x1, periodX); }
            if (periodY > 0) { yi = Mod(yi, periodY); y1 = Mod(y1, periodY); }
            float a = Hash(xi, yi, seed), b = Hash(x1, yi, seed), c = Hash(xi, y1, seed), d = Hash(x1, y1, seed);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        static int Mod(int value, int period) { int m = value % period; return m < 0 ? m + period : m; }

        static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0, amplitude = .5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            { sum += Noise(x, y, seed + o * 31) * amplitude; norm += amplitude; x *= 2.03f; y *= 2.03f; amplitude *= .5f; }
            return sum / norm;
        }

        /// Periodic fbm. Lacunarity is exactly 2 (not 2.03) and the period doubles per
        /// octave, so every octave wraps on the same tile boundary.
        public static float FbmTiled(float u, float v, int seed, int octaves, int periodX, int periodY)
        {
            float sum = 0, amplitude = .5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, seed + o * 31, periodX << o, periodY << o) * amplitude;
                norm += amplitude; u *= 2; v *= 2; amplitude *= .5f;
            }
            return sum / norm;
        }

        static float Sat(float v) => Mathf.Clamp01(v);

        static void Set(PaintedImage d, int i, Color c) { d.R[i] = c.r; d.G[i] = c.g; d.B[i] = c.b; }

        // ---------- bases ----------
        // Two-colour fbm field with a third "patch" colour in the troughs of a second field.
        static void PaintBase(PaintedImage d, int seed, float frequency, Color low, Color high, Color patchColor, float patch)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float n = Fbm(x * frequency, y * frequency, seed, 5);
                float m = Fbm(x * frequency * .6f + 50, y * frequency * .6f + 50, seed + 7, 4);
                Set(d, y * d.Width + x, Color.Lerp(Color.Lerp(low, high, Sat((n - .3f) / .4f)), patchColor, Sat((patch - m) / .08f)));
            }
        }

        // Seamless variant for the outer ground and ribbon. cellsX/cellsY are lattice cells
        // across the image and must be even (the patch field runs at half frequency).
        static void PaintBaseTiled(PaintedImage d, int seed, int cellsX, int cellsY, Color low, Color high, Color patchColor, float patch)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float u = x * (float)cellsX / d.Width, v = y * (float)cellsY / d.Height;
                float n = FbmTiled(u, v, seed, 5, cellsX, cellsY);
                float m = FbmTiled(u * .5f + 50, v * .5f + 50, seed + 7, 4, cellsX / 2, cellsY / 2);
                Set(d, y * d.Width + x, Color.Lerp(Color.Lerp(low, high, Sat((n - .3f) / .4f)), patchColor, Sat((patch - m) / .08f)));
            }
        }

        // Domain-warped jittered-grid Voronoi flagstones with mossy cracks (Courtyard).
        static void Flagstones(PaintedImage d, int seed, float cell, float gap, Color stone, Color crack)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float gx = x / cell + (Fbm(x * .02f, y * .02f, seed + 40, 3) - .5f) * .45f;
                float gy = y / cell + (Fbm(x * .02f + 9, y * .02f + 9, seed + 41, 3) - .5f) * .45f;
                int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                float d1 = 9, d2 = 9; int best = 0;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int cx = ix + ox, cy = iy + oy;
                    float px = cx + .15f + .7f * Hash(cx, cy, seed), py = cy + .15f + .7f * Hash(cx, cy, seed + 1);
                    float dd = Mathf.Sqrt((gx - px) * (gx - px) + (gy - py) * (gy - py));
                    if (dd < d1) { d2 = d1; d1 = dd; best = cx * 7919 + cy; } else if (dd < d2) d2 = dd;
                }
                float tone = .65f + .5f * Hash(best, 3, seed);
                float wear = .85f + .3f * Fbm(x * .03f, y * .03f, seed + 11, 4);
                float edge = Sat((d2 - d1 - gap) / .05f);              // 0 in the crack, 1 on the stone
                float bevel = Sat((d2 - d1) / .25f) * .15f + .85f;     // stones darken toward their rims
                Set(d, y * d.Width + x, Color.Lerp(crack, stone * (tone * wear * bevel), edge));
            }
        }

        // Regular basalt tiles with a faint inlaid line every `inlayEvery` tiles (Sanctum).
        static void Tiles(PaintedImage d, int seed, int cell, float gap, Color stone, Color crack, int inlayEvery, Color inlay)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                int tx = x / cell, ty = y / cell;
                float fx = (x % cell) / (float)cell, fy = (y % cell) / (float)cell;
                float edgeDistance = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
                float tone = .75f + .4f * Hash(tx, ty, seed);
                float wear = .85f + .3f * Fbm(x * .025f, y * .025f, seed + 5, 4);
                var color = Color.Lerp(crack, stone * (tone * wear), Sat((edgeDistance - gap) / .03f));
                // The ritual grid: a thin pale line along every Nth tile seam, fading with wear.
                bool line = tx % inlayEvery == 0 && x % cell < 2 || ty % inlayEvery == 0 && y % cell < 2;
                if (line) color = Color.Lerp(color, inlay, .55f * wear);
                Set(d, y * d.Width + x, color);
            }
        }

        // Worn footpaths: two sinuous bands of bare earth crossing the field (Graveyard).
        static void Footpath(PaintedImage d, int seed, float width, Color earth, float strength)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                float u = x / (float)d.Width, v = y / (float)d.Height;
                float along = Mathf.Abs(v - (.5f + .16f * Mathf.Sin(u * 6.283f * 1.3f + seed))) * d.Height;
                float across = Mathf.Abs(u - (.42f + .1f * Mathf.Sin(v * 6.283f * .9f + seed * 2))) * d.Width;
                float ragged = (Fbm(x * .05f, y * .05f, seed, 3) - .5f) * width * .8f;
                float path = Mathf.Max(Sat(1 - (along + ragged) / width), Sat(1 - (across + ragged) / (width * .7f)));
                int i = y * d.Width + x;
                Set(d, i, Color.Lerp(new Color(d.R[i], d.G[i], d.B[i]), earth, path * strength));
            }
        }

        // Pale mineral flecks (Cave): a few-pixel soft dots.
        static void Flecks(PaintedImage d, int seed, int count, Color fleck)
        {
            var random = new System.Random(seed);
            for (int n = 0; n < count; n++)
            {
                int cx = random.Next(d.Width), cy = random.Next(d.Height); float radius = 1 + (float)random.NextDouble() * 1.5f;
                for (int y = cy - 3; y <= cy + 3; y++) for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if (x < 0 || y < 0 || x >= d.Width || y >= d.Height) continue;
                    float k = Sat(1 - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius) * .6f;
                    int i = y * d.Width + x; Set(d, i, Color.Lerp(new Color(d.R[i], d.G[i], d.B[i]), fleck, k));
                }
            }
        }

        // Seamless brick courses for the ribbon (Courtyard, Sanctum). Width must be a multiple
        // of brickWidth and height of 2*brickHeight so the running bond wraps.
        static void Masonry(PaintedImage d, int seed, int brickWidth, int brickHeight, Color stone, Color mortar)
        {
            for (int y = 0; y < d.Height; y++) for (int x = 0; x < d.Width; x++)
            {
                int row = y / brickHeight; int shifted = x + (row % 2) * brickWidth / 2;
                int column = (shifted / brickWidth) % (d.Width / brickWidth);
                float fx = (shifted % brickWidth) / (float)brickWidth, fy = (y % brickHeight) / (float)brickHeight;
                float edge = Mathf.Min(Mathf.Min(fx, 1 - fx) * brickWidth, Mathf.Min(fy, 1 - fy) * brickHeight);
                float tone = .7f + .45f * Hash(column, row, seed);
                int i = y * d.Width + x;
                var under = new Color(d.R[i], d.G[i], d.B[i]);
                Set(d, i, Color.Lerp(mortar, under * tone + stone * .35f, Sat((edge - 1.5f) / 1.5f)));
            }
        }

        // ---------- decals ----------
        /// Bilinear, premultiplied stamping (no black fringes); daytime pack colours are
        /// desaturated then multiplied into the night palette.
        public static void Scatter(PaintedImage d, DecalSheet sheet, int count, int seed, float scale, Color tint,
            float desaturate, float alpha, int maxSprite)
        {
            if (sheet == null || sheet.Sprites.Count == 0) return;
            var pool = sheet.Sprites.FindAll(r => r.width <= maxSprite && r.height <= maxSprite);
            if (pool.Count == 0) pool = sheet.Sprites;
            var random = new System.Random(seed);
            for (int n = 0; n < count; n++)
            {
                var rect = pool[random.Next(pool.Count)];
                int x = random.Next(-(int)(rect.width * scale) / 2, d.Width), y = random.Next(-(int)(rect.height * scale) / 2, d.Height);
                Stamp(d, sheet.Image, rect, x, y, scale, tint, desaturate, alpha * (.7f + .3f * (float)random.NextDouble()));
            }
        }

        static void Stamp(PaintedImage d, PaintedImage s, RectInt r, int dx, int dy, float scale, Color tint, float desaturate, float alpha)
        {
            int ow = (int)(r.width * scale), oh = (int)(r.height * scale);
            for (int y = 0; y < oh; y++)
            {
                int ty = dy + y; if (ty < 0 || ty >= d.Height) continue;
                for (int x = 0; x < ow; x++)
                {
                    int tx = dx + x; if (tx < 0 || tx >= d.Width) continue;
                    Bilinear(s, r.x + x / scale - .5f, r.y + y / scale - .5f, out float cr, out float cg, out float cb, out float ca);
                    if (ca <= .01f) continue;
                    float lum = cr * .3f + cg * .59f + cb * .11f;
                    cr = Mathf.Lerp(cr, lum, desaturate) * tint.r; cg = Mathf.Lerp(cg, lum, desaturate) * tint.g; cb = Mathf.Lerp(cb, lum, desaturate) * tint.b;
                    float a = ca * alpha; int o = ty * d.Width + tx;
                    d.R[o] = Mathf.Lerp(d.R[o], cr, a); d.G[o] = Mathf.Lerp(d.G[o], cg, a); d.B[o] = Mathf.Lerp(d.B[o], cb, a);
                }
            }
        }

        static void Bilinear(PaintedImage s, float x, float y, out float r, out float g, out float b, out float a)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, s.Width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, s.Height - 1);
            int x1 = Mathf.Min(s.Width - 1, x0 + 1), y1 = Mathf.Min(s.Height - 1, y0 + 1);
            float fx = Sat(x - x0), fy = Sat(y - y0);
            int i00 = y0 * s.Width + x0, i10 = y0 * s.Width + x1, i01 = y1 * s.Width + x0, i11 = y1 * s.Width + x1;
            float a00 = s.A[i00], a10 = s.A[i10], a01 = s.A[i01], a11 = s.A[i11];
            a = Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
            if (a < 1e-4f) { r = g = b = 0; return; }
            r = Mathf.Lerp(Mathf.Lerp(s.R[i00] * a00, s.R[i10] * a10, fx), Mathf.Lerp(s.R[i01] * a01, s.R[i11] * a11, fx), fy) / a;
            g = Mathf.Lerp(Mathf.Lerp(s.G[i00] * a00, s.G[i10] * a10, fx), Mathf.Lerp(s.G[i01] * a01, s.G[i11] * a11, fx), fy) / a;
            b = Mathf.Lerp(Mathf.Lerp(s.B[i00] * a00, s.B[i10] * a10, fx), Mathf.Lerp(s.B[i01] * a01, s.B[i11] * a11, fx), fy) / a;
        }

        // ---------- per-theme recipes ----------
        static DecalSheet Sheet(IReadOnlyDictionary<string, DecalSheet> decals, string key)
            => decals != null && decals.TryGetValue(key, out var sheet) ? sheet : null;

        public static PaintedImage Floor(string theme, int width, int height, IReadOnlyDictionary<string, DecalSheet> decals)
        {
            var d = new PaintedImage(width, height);
            float area = width * height / SwatchArea;
            int N(int swatchCount) => Mathf.Max(1, Mathf.RoundToInt(swatchCount * area));
            DecalSheet grass = Sheet(decals, "grass"), rocks = Sheet(decals, "rocks"), vegetation = Sheet(decals, "vegetation");
            switch (theme)
            {
                case "Graveyard": // approved swatch e2_painted
                    PaintBase(d, 21, .007f, new Color(.08f, .11f, .07f), new Color(.17f, .21f, .12f), new Color(.15f, .11f, .08f), .44f);
                    Footpath(d, 25, 46, new Color(.13f, .10f, .08f), .7f);
                    Scatter(d, grass, N(30), 22, .5f, new Color(.48f, .62f, .40f), .35f, .9f, 400);
                    Scatter(d, rocks, N(22), 23, .5f, new Color(.55f, .55f, .6f), .6f, .9f, 160);
                    Scatter(d, vegetation, N(16), 24, .5f, new Color(.9f, .88f, .95f), .9f, .9f, 60);
                    break;
                case "Cave": // first pass: violet-grey rock, dark damp pools, mineral flecks
                    PaintBase(d, 31, .006f, new Color(.10f, .09f, .13f), new Color(.20f, .18f, .24f), new Color(.03f, .03f, .05f), .40f);
                    Flecks(d, 33, N(900), new Color(.45f, .5f, .75f));
                    Scatter(d, rocks, N(18), 34, .5f, new Color(.5f, .48f, .62f), .6f, .9f, 160);
                    break;
                case "Sanctum": // first pass: worn basalt tiles with faint inlaid lines
                    Tiles(d, 41, 80, .04f, new Color(.14f, .13f, .16f), new Color(.03f, .025f, .04f), 4, new Color(.30f, .24f, .42f));
                    Scatter(d, rocks, N(10), 43, .5f, new Color(.5f, .5f, .58f), .6f, .8f, 160);
                    break;
                default: // Courtyard, approved swatch e1_painted; also the fallback theme
                    Flagstones(d, 3, 64, .035f, new Color(.21f, .21f, .25f), new Color(.04f, .05f, .04f));
                    Scatter(d, grass, N(14), 5, .5f, new Color(.45f, .58f, .40f), .45f, .85f, 300);
                    Scatter(d, rocks, N(28), 6, .5f, new Color(.55f, .55f, .62f), .55f, .9f, 160);
                    Scatter(d, vegetation, N(10), 7, .5f, new Color(.9f, .88f, .95f), .9f, .9f, 60);
                    break;
            }
            return d;
        }

        // Base colours shared by the outer ground (darker) and the ribbon.
        static (Color low, Color high, Color patch) Palette(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return (new Color(.06f, .08f, .05f), new Color(.12f, .15f, .09f), new Color(.10f, .08f, .06f));
                case "Cave": return (new Color(.08f, .07f, .10f), new Color(.16f, .14f, .19f), new Color(.03f, .03f, .05f));
                case "Sanctum": return (new Color(.07f, .06f, .09f), new Color(.14f, .12f, .16f), new Color(.05f, .04f, .07f));
                default: return (new Color(.08f, .08f, .09f), new Color(.15f, .15f, .17f), new Color(.06f, .07f, .05f));
            }
        }

        public static PaintedImage Outer(string theme, int size)
        {
            var d = new PaintedImage(size, size); var p = Palette(theme);
            // 55% of the ring palette: the ground past the walls should sink into the fog.
            PaintBaseTiled(d, 61, 8, 8, p.low * .55f, p.high * .55f, p.patch * .55f, .42f);
            return d;
        }

        public static PaintedImage Ribbon(string theme, int width, int height)
        {
            var d = new PaintedImage(width, height); var p = Palette(theme);
            PaintBaseTiled(d, 71, 8, 4, p.low, p.high, p.patch, .40f);
            // Built themes (and the Courtyard fallback) get coursed masonry; Graveyard reads
            // as hedge/earth and Cave as raw rock, so they keep the plain painted base.
            if (theme != "Graveyard" && theme != "Cave")
                Masonry(d, 73, width / 8, height / 8, p.high, p.low * .4f);
            return d;
        }
    }
}
```

- [ ] **Step 5: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t3`
Expected: the EditMode summary shows 0 failures, including the 5 new tests.

- [ ] **Step 6: Commit.** Stage `PaintedImage.cs(.meta)`, `PaintedFloorGenerator.cs(.meta)` and `ArenaRedesignTests.cs`. Message: `Port the approved painted floor generator to runtime code`.

---

### Task 4: Bake and commit the painted floors

**Files:**
- Create: `Assets/Game/Editor/PaintedFloorBaker.cs`
- Generate: `Assets/Game/Resources/WorldFloors/{Courtyard,Graveyard,Cave,Sanctum}{Floor,Outer,Ribbon}.jpg` (+ `.meta`)
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `PaintedFloorGenerator.Floor/Outer/Ribbon`, `DecalSheet`, `PaintedImage.FromColors/ToColors`, `WorldArenaLayouts.Create(stage)`.
- Produces: the Resources paths `WorldFloors/{Theme}Floor`, `WorldFloors/{Theme}Outer` and `WorldFloors/{Theme}Ribbon`. Floor is clamped; Outer and Ribbon repeat. All are readable.

- [ ] **Step 1: Append the failing test.**

```csharp
        [Test]
        public void BakedFloorsExistAtFortyPixelsPerUnitAndAreReadable()
        {
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage); string theme = arena.worldTheme;
                var floor = Resources.Load<Texture2D>("WorldFloors/" + theme + "Floor");
                Assert.That(floor, Is.Not.Null, theme + "Floor");
                Assert.That(floor.width, Is.EqualTo(Mathf.RoundToInt(arena.bounds.width * PaintedFloorGenerator.PixelsPerUnit)));
                Assert.That(floor.height, Is.EqualTo(Mathf.RoundToInt(arena.bounds.height * PaintedFloorGenerator.PixelsPerUnit)));
                // The interrupted-morph snapshot samples pixels on the CPU.
                Assert.That(floor.isReadable, Is.True);
                Assert.That(floor.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                foreach (var suffix in new[] { "Outer", "Ribbon" })
                {
                    var tile = Resources.Load<Texture2D>("WorldFloors/" + theme + suffix);
                    Assert.That(tile, Is.Not.Null, theme + suffix);
                    Assert.That(tile.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                    Assert.That(tile.isReadable, Is.True);
                }
            }
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t4red`
Expected: `NOT PASSED: ...BakedFloorsExistAtFortyPixelsPerUnitAndAreReadable`, with "CourtyardFloor" expected not null.

- [ ] **Step 3: Write `PaintedFloorBaker.cs`.**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using BorrowedHex.Data;
using BorrowedHex.Presentation.WorldArt;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Bakes the painted floors once, offline: painting 2.7M pixels with several noise octaves
    /// takes seconds, far too slow for runtime. The JPGs are committed (amendment A3), so a
    /// fresh checkout never needs the owner's downloads. The Szadi overlays only add decals; a
    /// missing pack still bakes, minus decals, with a warning.
    /// </summary>
    public static class PaintedFloorBaker
    {
        const string Output = "Assets/Game/Resources/WorldFloors";
        static string Overlays => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "itch_downloads", "OpenWorldandcavedung_1.0", "Open World And Cave Dungeon", "1. OpenWorld");

        [MenuItem("Borrowed Hex/Art/Bake Painted Floors")]
        public static void Bake()
        {
            var decals = LoadDecals();
            Directory.CreateDirectory(Output);
            var written = new List<(string path, bool repeat)>();
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage); string theme = arena.worldTheme;
                int width = Mathf.RoundToInt(arena.bounds.width * PaintedFloorGenerator.PixelsPerUnit);
                int height = Mathf.RoundToInt(arena.bounds.height * PaintedFloorGenerator.PixelsPerUnit);
                written.Add((Write(theme + "Floor", PaintedFloorGenerator.Floor(theme, width, height, decals)), false));
                written.Add((Write(theme + "Outer", PaintedFloorGenerator.Outer(theme, 512)), true));
                written.Add((Write(theme + "Ribbon", PaintedFloorGenerator.Ribbon(theme, 512, 256)), true));
            }
            AssetDatabase.Refresh();
            foreach (var (path, repeat) in written) Configure(path, repeat);
            Debug.Log("Baked " + written.Count + " painted textures into " + Output);
        }

        static Dictionary<string, DecalSheet> LoadDecals()
        {
            var sources = new Dictionary<string, (string file, int minSize)>
            {
                ["grass"] = (Path.Combine(Overlays, "2.Second Layer", "grassF_2.png"), 12),
                ["rocks"] = (Path.Combine(Overlays, "2.Second Layer", "groundrocksA.png"), 8),
                ["vegetation"] = (Path.Combine(Overlays, "3.Third Layer", "vegetationA.png"), 8),
            };
            var sheets = new Dictionary<string, DecalSheet>();
            foreach (var pair in sources)
            {
                if (!File.Exists(pair.Value.file)) { Debug.LogWarning("Decal source missing, baking without it: " + pair.Value.file); continue; }
                var texture = new Texture2D(2, 2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(pair.Value.file));
                    sheets[pair.Key] = new DecalSheet(PaintedImage.FromColors(texture.GetPixels32(), texture.width, texture.height), pair.Value.minSize);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            return sheets;
        }

        static string Write(string name, PaintedImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(image.ToColors()); texture.Apply();
                string path = Output + "/" + name + ".jpg";
                File.WriteAllBytes(path, texture.EncodeToJPG(92));
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static void Configure(string path, bool repeat)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.isReadable = true; importer.filterMode = FilterMode.Bilinear;
            // Clamped floors: the arena image covers the bounds exactly once.
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            // Uncompressed: block compression smears the dark gradients into visible steps.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
```

- [ ] **Step 4: Compile, then bake.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/uc.sh`. Expected: `failed: False`.
Run: `unity command eval --code 'BorrowedHex.EditorTools.PaintedFloorBaker.Bake(); return 1;'`. Expected: success, plus 12 `.jpg` files in `Assets/Game/Resources/WorldFloors/`, each Floor about 1 MB. Check with `Get-ChildItem Assets/Game/Resources/WorldFloors -Filter *.jpg | Select Name,Length`.

- [ ] **Step 5: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t4`
Expected: the EditMode summary shows 0 failures.

- [ ] **Step 6: Look at the floors.** Open `CourtyardFloor.jpg` and `GraveyardFloor.jpg` with Read and compare them with the approved swatches (`e1_painted`/`e2_painted`). Record any mismatch in the ledger; the owner tunes looks at Task 12.

- [ ] **Step 7: Commit.** Stage `Assets/Game/Editor/PaintedFloorBaker.cs(.meta)`, `Assets/Game/Resources/WorldFloors.meta`, `Assets/Game/Resources/WorldFloors/*.jpg` and `*.jpg.meta`, and `ArenaRedesignTests.cs`. Message: `Bake the painted arena floors, outer ground and ribbon textures`.

---

### Task 5: Import the kit models

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/WorldModelCatalog.cs`
- Create: `Assets/Game/Editor/WorldModelImporter.cs`
- Generate: `Assets/Game/Resources/WorldModels/Kenney/*.fbx`, `colormap_night.png` and `License.txt`; `Assets/Game/Resources/WorldModels/Quaternius/*.fbx` and `License.txt`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Produces: `WorldModelCatalog.Kenney` and `WorldModelCatalog.Quaternius` (`string[]`), `WorldModelCatalog.Crystal` (`"crystal-cluster"`, built in code), `IsKenney(string)`, `ResourcePath(string) : string` and `Exists(string) : bool`.
- Produces: the Resources path `WorldModels/Kenney/colormap_night`.

- [ ] **Step 1: Append the failing test.**

```csharp
        [Test]
        public void EveryCatalogModelLoadsFromResources()
        {
            foreach (var name in WorldModelCatalog.Kenney.Concat(WorldModelCatalog.Quaternius))
                Assert.That(Resources.Load<GameObject>(WorldModelCatalog.ResourcePath(name)), Is.Not.Null, name);
            Assert.That(Resources.Load<Texture2D>("WorldModels/Kenney/colormap_night"), Is.Not.Null);
            Assert.That(WorldModelCatalog.Exists(WorldModelCatalog.Crystal), Is.True, "Built in code, never loaded");
        }
```

- [ ] **Step 2: Write `WorldModelCatalog.cs`.**

```csharp
using System;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The only kit models the recipes may use. The importer copies exactly this list, so the
    /// committed subset stays small; a name typed in a recipe but missing here fails a test.
    /// Excluded on purpose (spec 3.5): pumpkins, hay, characters, snow/autumn/cactus/palm.
    /// </summary>
    public static class WorldModelCatalog
    {
        public const string Root = "WorldModels";
        public const string Crystal = "crystal-cluster"; // neither kit has one; WorldModelLibrary builds it

        public static readonly string[] Kenney =
        {
            "altar-stone", "border-pillar", "brick-wall", "candle-multiple", "column-large", "crypt-a", "crypt-b",
            "crypt-large", "crypt-large-door", "crypt-small", "debris", "fire-basket", "grave-border",
            "gravestone-broken", "gravestone-cross", "gravestone-debris", "gravestone-decorative", "gravestone-round",
            "gravestone-wide", "iron-fence", "iron-fence-border", "iron-fence-damaged", "lantern-candle",
            "lightpost-single", "pillar-obelisk", "pine-crooked", "rocks", "rocks-tall", "stone-wall",
            "stone-wall-column", "stone-wall-damaged", "trunk", "urn-round",
        };

        public static readonly string[] Quaternius =
        {
            "CommonTree_Dead_1", "CommonTree_Dead_2", "CommonTree_Dead_3", "CommonTree_Dead_4", "CommonTree_Dead_5",
            "Willow_Dead_1", "Willow_Dead_2", "Willow_Dead_3", "Willow_Dead_4", "Willow_Dead_5",
            "Rock_1", "Rock_2", "Rock_3", "Rock_4", "Rock_5", "Rock_6", "Rock_7",
            "Rock_Moss_1", "Rock_Moss_2", "Rock_Moss_3", "Rock_Moss_4", "Rock_Moss_5", "Rock_Moss_6", "Rock_Moss_7",
            "TreeStump_Moss", "Bush_1", "Bush_2",
        };

        public static bool IsKenney(string name) => Array.IndexOf(Kenney, name) >= 0;
        public static string ResourcePath(string name) => Root + (IsKenney(name) ? "/Kenney/" : "/Quaternius/") + name;
        public static bool Exists(string name) => name == Crystal || Resources.Load<GameObject>(ResourcePath(name)) != null;
    }
}
```

- [ ] **Step 3: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t5red`
Expected: `NOT PASSED: ...EveryCatalogModelLoadsFromResources` ("altar-stone" expected not null).

- [ ] **Step 4: Write `WorldModelImporter.cs`.**

```csharp
using System;
using System.IO;
using BorrowedHex.Presentation.WorldArt;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Copies the catalog's FBX subset of two CC0 kits into Resources. Unlike WorldArt these
    /// are committed (spec 3.1, owner decision), so the arenas render on a fresh checkout.
    /// Validates every source before copying anything, like WorldArtImporter, so a partial
    /// download never leaves a half-imported folder.
    /// </summary>
    public static class WorldModelImporter
    {
        const string Output = "Assets/Game/Resources/WorldModels";
        static string Downloads => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "itch_downloads");

        [MenuItem("Borrowed Hex/Art/Import World Models")]
        public static void Import()
        {
            string kenney = Path.Combine(Downloads, "kenney_graveyard-kit_5.0"), quaternius = Path.Combine(Downloads, "Ultimate Nature Pack by Quaternius");
            string Kfbx(string n) => Path.Combine(kenney, "Models", "FBX format", n + ".fbx");
            string Qfbx(string n) => Path.Combine(quaternius, "FBX", n + ".fbx");
            string colormap = Path.Combine(kenney, "Models", "FBX format", "Textures", "colormap.png");
            foreach (var n in WorldModelCatalog.Kenney) Require(Kfbx(n));
            foreach (var n in WorldModelCatalog.Quaternius) Require(Qfbx(n));
            Require(colormap); Require(Path.Combine(kenney, "License.txt")); Require(Path.Combine(quaternius, "License.txt"));

            AssetDatabase.StartAssetEditing();
            try
            {
                Directory.CreateDirectory(Output + "/Kenney"); Directory.CreateDirectory(Output + "/Quaternius");
                foreach (var n in WorldModelCatalog.Kenney) File.Copy(Kfbx(n), Output + "/Kenney/" + n + ".fbx", true);
                foreach (var n in WorldModelCatalog.Quaternius) File.Copy(Qfbx(n), Output + "/Quaternius/" + n + ".fbx", true);
                File.Copy(Path.Combine(kenney, "License.txt"), Output + "/Kenney/License.txt", true);
                File.Copy(Path.Combine(quaternius, "License.txt"), Output + "/Quaternius/License.txt", true);
                // The FBX importer also wants the original colormap beside the models.
                Directory.CreateDirectory(Output + "/Kenney/Textures");
                File.Copy(colormap, Output + "/Kenney/Textures/colormap.png", true);
                BakeNightColormap(colormap, Output + "/Kenney/colormap_night.png");
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Output, "*.fbx", SearchOption.AllDirectories)) ConfigureModel(path.Replace('\\', '/'));
            var night = (TextureImporter)AssetImporter.GetAtPath(Output + "/Kenney/colormap_night.png");
            // Kenney colormaps are flat swatches: point filter, no mips, or neighbours bleed.
            night.filterMode = FilterMode.Point; night.mipmapEnabled = false;
            night.textureCompression = TextureImporterCompression.Uncompressed; night.SaveAndReimport();
            Debug.Log("Imported " + (WorldModelCatalog.Kenney.Length + WorldModelCatalog.Quaternius.Length) + " world models");
        }

        static void Require(string path) { if (!File.Exists(path)) throw new FileNotFoundException("World model source missing", path); }

        // Daytime kit colours pulled into the night palette once, offline: desaturate 35%
        // then multiply by a cold grey-violet. Per-theme tints finish the job at runtime.
        static void BakeNightColormap(string source, string destination)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(source));
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float lum = pixels[i].r * .3f + pixels[i].g * .59f + pixels[i].b * .11f;
                    var c = Color.Lerp(pixels[i], new Color(lum, lum, lum, pixels[i].a), .35f);
                    pixels[i] = new Color(c.r * .55f, c.g * .55f, c.b * .62f, c.a);
                }
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(destination, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            // Material names survive import so WorldModelLibrary can map Quaternius' flat
            // colours (Wood, LightWood, Rock, Green, Berry) to night colours by name.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.SaveAndReimport();
        }
    }
}
```

- [ ] **Step 5: Compile and import.**

Run `uc.sh`; expected: `failed: False`.
Run: `unity command eval --code 'BorrowedHex.EditorTools.WorldModelImporter.Import(); return 1;'`
Expected: success. `Assets/Game/Resources/WorldModels/Kenney` holds 33 `.fbx` files and `Quaternius` holds 27.

- [ ] **Step 6: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t5`
Expected: 0 failures.

- [ ] **Step 7: Commit.** Stage `WorldModelCatalog.cs(.meta)`, `Assets/Game/Editor/WorldModelImporter.cs(.meta)`, `Assets/Game/Resources/WorldModels.meta` and `Assets/Game/Resources/WorldModels/**` (`git add Assets/Game/Resources/WorldModels`), plus `ArenaRedesignTests.cs`. Check that the staged list has only those paths. If Unity generated a `Materials/` folder under WorldModels, commit it with the models: it is import output the FBX references. Message: `Import the CC0 Kenney and Quaternius models the arena recipes use`.

---

### Task 6: World model library

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/WorldModelLibrary.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `WorldModelCatalog`, `PaintedMaterials.Create`, `WorldArtLibrary.Release`.
- Produces: `WorldModelLibrary(Color kitTint) : IDisposable`.
- Produces: `GameObject Spawn(string model, Transform parent, out Vector3 size)`. It returns a wrapper named `model`, with the pivot at bottom-centre and `size` = the unscaled renderer bounds at the identity transform. On a missing model it logs an error and returns null.
- Produces: `static Color NightColor(string materialName)`.
- Every spawned renderer uses PaintedWorld, casts and receives shadows, and has its colliders removed.

- [ ] **Step 1: Append the failing tests.**

```csharp
        [Test]
        public void SpawnedModelsUsePaintedMaterialsWithAGroundedPivot()
        {
            var parent = new GameObject("probe").transform;
            using (var library = new WorldModelLibrary(Color.white))
                try
                {
                    foreach (var name in new[] { "crypt-small", "Rock_Moss_1", WorldModelCatalog.Crystal })
                    {
                        var model = library.Spawn(name, parent, out var size);
                        Assert.That(model, Is.Not.Null, name);
                        Assert.That(size.y, Is.GreaterThan(.05f), name);
                        var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                        // Bottom-centre pivot: recipes place models ON the ground at a point.
                        Assert.That(bounds.min.y, Is.EqualTo(0).Within(.01f), name);
                        Assert.That(bounds.center.x, Is.EqualTo(0).Within(.05f * Mathf.Max(1, size.x)), name);
                        Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty, name);
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                            foreach (var material in renderer.sharedMaterials)
                                Assert.That(material.shader.name, Is.EqualTo(PaintedMaterials.ShaderName), name);
                    }
                }
                finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test]
        public void MissingModelLogsAndSkips()
        {
            var parent = new GameObject("probe").transform;
            using (var library = new WorldModelLibrary(Color.white))
                try
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no-such-model"));
                    Assert.That(library.Spawn("no-such-model", parent, out _), Is.Null);
                }
                finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test]
        public void QuaterniusMaterialNamesMapToNightColours()
        {
            Assert.That(WorldModelLibrary.NightColor("Wood").maxColorComponent, Is.LessThan(.15f));
            Assert.That(WorldModelLibrary.NightColor("Rock (Instance)"), Is.EqualTo(WorldModelLibrary.NightColor("Rock")));
            Assert.That(WorldModelLibrary.NightColor("Something"), Is.EqualTo(new Color(.12f, .12f, .14f)));
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t6red`
Expected: `COMPILE NOT CLEAN - stopping` (`WorldModelLibrary` does not exist).

- [ ] **Step 3: Write `WorldModelLibrary.cs`.**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Instantiates kit models with PaintedWorld materials. Materials are cached per source
    /// (one for all Kenney models, one per Quaternius colour) so a few hundred props share a
    /// handful of materials; WorldGeometry's property blocks still vary them per renderer.
    /// Everything created here is owned and released on Dispose, as WorldGeometry does today.
    /// </summary>
    public sealed class WorldModelLibrary : IDisposable
    {
        readonly Color kitTint;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Mesh crystal;

        public WorldModelLibrary(Color kitTint) => this.kitTint = kitTint;

        // Quaternius models carry flat colours named by material; these are their night
        // versions. Prefix match because Unity may append " (Instance)" or a numeric suffix.
        static readonly (string name, Color color)[] Night =
        {
            ("LightWood", new Color(.13f, .09f, .08f)), ("Wood", new Color(.09f, .06f, .06f)),
            ("Rock", new Color(.17f, .17f, .21f)), ("DarkGreen", new Color(.04f, .06f, .04f)),
            ("Green", new Color(.07f, .10f, .06f)), ("Black", new Color(.03f, .03f, .03f)),
            ("White", new Color(.35f, .36f, .34f)), ("Leaves", new Color(.10f, .08f, .06f)),
            ("Berry", new Color(.16f, .04f, .06f)),
        };

        public static Color NightColor(string materialName)
        {
            foreach (var (name, color) in Night)
                if (materialName.StartsWith(name, StringComparison.Ordinal)) return color;
            return new Color(.12f, .12f, .14f);
        }

        public GameObject Spawn(string model, Transform parent, out Vector3 size)
        {
            size = Vector3.zero;
            GameObject instance;
            if (model == WorldModelCatalog.Crystal) instance = BuildCrystal();
            else
            {
                var prefab = Resources.Load<GameObject>(WorldModelCatalog.ResourcePath(model));
                if (prefab == null)
                {
                    // Spec 4.4: name it loudly, skip the placement, never throw mid-build.
                    Debug.LogError("World model missing from Resources/" + WorldModelCatalog.ResourcePath(model) + ": " + model);
                    return null;
                }
                instance = UnityEngine.Object.Instantiate(prefab);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) WorldArtLibrary.Release(collider);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) Paint(renderer, model);
            }
            instance.name = "Mesh";
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }

            // Measure at the identity transform, then offset the mesh inside a wrapper so the
            // wrapper's origin is the bottom-centre of the model (recipes place on the ground).
            var wrapper = new GameObject(model);
            instance.transform.SetParent(wrapper.transform, false);
            var bounds = Bounds(instance);
            instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            size = bounds.size;
            wrapper.transform.SetParent(parent, false);
            return wrapper;
        }

        static Bounds Bounds(GameObject value)
        {
            var renderers = value.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(value.transform.position, Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        void Paint(Renderer renderer, string model)
        {
            var source = renderer.sharedMaterials; var painted = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (WorldModelCatalog.IsKenney(model)) painted[i] = Cached("Kenney", () =>
                    PaintedMaterials.Create("Kenney night", Resources.Load<Texture2D>("WorldModels/Kenney/colormap_night"), kitTint));
                else
                {
                    var color = NightColor(source[i] != null ? source[i].name : "");
                    painted[i] = Cached("Q" + ColorUtility.ToHtmlStringRGB(color), () =>
                        PaintedMaterials.Create("Quaternius night", Texture2D.whiteTexture, color));
                }
            }
            renderer.sharedMaterials = painted;
        }

        Material Cached(string key, Func<Material> create)
        {
            if (materials.TryGetValue(key, out var material)) return material;
            material = create(); owned.Add(material);
            return materials[key] = material;
        }

        // Neither kit has a crystal (spec 3.4): five tapered hexagonal prisms leaning outward,
        // emissive cold blue so the Cave keeps its glow under very dark ambient.
        GameObject BuildCrystal()
        {
            if (crystal == null)
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                var random = new System.Random(17);
                for (int shard = 0; shard < 5; shard++)
                {
                    float height = .9f + (float)random.NextDouble() * .9f, radius = .14f + (float)random.NextDouble() * .1f;
                    var tilt = Quaternion.Euler((float)random.NextDouble() * 30 - 15, shard * 72, (float)random.NextDouble() * 30 - 15);
                    var foot = new Vector3(Mathf.Cos(shard * 1.26f), 0, Mathf.Sin(shard * 1.26f)) * (shard == 0 ? 0 : .25f);
                    int start = vertices.Count;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3;
                        vertices.Add(foot + tilt * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                        vertices.Add(foot + tilt * new Vector3(Mathf.Cos(a) * radius * .55f, height * .8f, Mathf.Sin(a) * radius * .55f));
                    }
                    vertices.Add(foot + tilt * new Vector3(0, height, 0));
                    for (int i = 0; i < 6; i++)
                    {
                        int b0 = start + i * 2, t0 = b0 + 1, b1 = start + (i + 1) % 6 * 2, t1 = b1 + 1, tip = start + 12;
                        triangles.AddRange(new[] { b0, t0, b1, b1, t0, t1, t0, tip, t1 });
                    }
                }
                crystal = new Mesh { name = "Crystal cluster" };
                crystal.SetVertices(vertices); crystal.SetTriangles(triangles, 0); crystal.RecalculateNormals(); crystal.RecalculateBounds();
                owned.Add(crystal);
            }
            var value = new GameObject("Crystal");
            value.AddComponent<MeshFilter>().sharedMesh = crystal;
            var material = Cached("Crystal", () =>
            {
                var m = PaintedMaterials.Create("Crystal", Texture2D.whiteTexture, new Color(.35f, .55f, .9f));
                m.SetColor("_Emission", new Color(.15f, .3f, .6f)); return m;
            });
            value.AddComponent<MeshRenderer>().sharedMaterial = material;
            return value;
        }

        public void Dispose() { foreach (var item in owned) WorldArtLibrary.Release(item); owned.Clear(); materials.Clear(); crystal = null; }
    }
}
```

- [ ] **Step 4: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t6`
Expected: 0 failures. If the crystal faces render inside-out in Task 12, reverse the triangle winding. Ledger it as a ruling.

- [ ] **Step 5: Commit.** Stage `WorldModelLibrary.cs(.meta)` and `ArenaRedesignTests.cs`. Message: `Spawn kit models with night PaintedWorld materials`.


---

### Task 7: Dressing recipes (pure)

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/ArenaDressing.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `WorldGeometry.FlameAnchor(ArenaLayout, int)` (existing, static), `WorldModelCatalog.Exists`, `WorldModelCatalog.Crystal`.
- Produces: `enum DressingBand { North, East, West, South, Inside, Sides, Light }`. `Sides` appears only in recipe rules and is never on a placement.
- Produces: `struct DressingPlacement { string Model; DressingBand Band; Vector3 Position; float Yaw; float Height; }`. `Height` is the target world height; the model is scaled uniformly to reach it.
- Produces: `ArenaDressing.Place(ArenaLayout) : List<DressingPlacement>`, `Known(string) : string` (unknown → "Courtyard"), `KitTint(string) : Color`, `LightModel(string) : string` (null for Sanctum), `RibbonInset(DressingBand) : float`, `RibbonHeight(Rect, DressingBand, float z) : float`.
- Produces the constants `InsideLimit` (.3), `SouthLimit` (1.2), `NorthMinimum` (6) and `RibbonThickness` (2).

The geometry, from spec §1 and amendment A2:

| Band | Ribbon inner edge | Ribbon height | What stands in front of the ribbon | What stands behind it |
|---|---|---|---|---|
| North | 5 units past `yMax` | 7.5 | Hero pieces and a statement row (depth 0.4–4.5) | Tall trees and obelisks (depth 7.5–10) |
| East/West | 1.2 units out | 2 → 7, south to north | A wall-foot row (depth 0.3–1.1) | Tall masses, depth 3.5–16 |
| South | 1.2 units out | 1.0 | A low row (depth 0.3–1.1), at most 1.2 tall | — |

- [ ] **Step 1: Append the failing tests.**

```csharp
        [Test]
        public void DressingIsDeterministicPerTheme()
        {
            for (int stage = 0; stage < 4; stage++)
            {
                var a = ArenaDressing.Place(WorldArenaLayouts.Create(stage));
                var b = ArenaDressing.Place(WorldArenaLayouts.Create(stage));
                Assert.That(b.Count, Is.EqualTo(a.Count));
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.That(b[i].Model, Is.EqualTo(a[i].Model)); Assert.That(b[i].Position, Is.EqualTo(a[i].Position));
                    Assert.That(b[i].Yaw, Is.EqualTo(a[i].Yaw)); Assert.That(b[i].Height, Is.EqualTo(a[i].Height));
                }
            }
        }

        [Test]
        public void DressingFollowsTheHeightGrade()
        {
            for (int stage = 0; stage < 4; stage++)
            {
                var arena = WorldArenaLayouts.Create(stage); var b = arena.bounds;
                var placements = ArenaDressing.Place(arena);
                Assert.That(placements.Count, Is.GreaterThan(40), arena.worldTheme);
                float north = 0;
                foreach (var p in placements)
                {
                    var ground = new Vector2(p.Position.x, p.Position.z);
                    Assert.That(p.Band, Is.Not.EqualTo(DressingBand.Sides));
                    // Anything tall inside the play rect would read as cover that does not collide.
                    if (b.Contains(ground)) Assert.That(p.Height, Is.LessThanOrEqualTo(ArenaDressing.InsideLimit), p.Model);
                    // The camera looks north over the south edge; it must never hide the fight.
                    if (p.Band == DressingBand.South || p.Position.z < b.yMin && p.Position.x >= b.xMin && p.Position.x <= b.xMax)
                        Assert.That(p.Height, Is.LessThanOrEqualTo(ArenaDressing.SouthLimit), p.Model);
                    if (p.Band == DressingBand.North) north = Mathf.Max(north, p.Height);
                }
                Assert.That(north, Is.GreaterThanOrEqualTo(ArenaDressing.NorthMinimum), arena.worldTheme);
                Assert.That(ArenaDressing.RibbonHeight(b, DressingBand.North, b.yMax), Is.GreaterThanOrEqualTo(ArenaDressing.NorthMinimum));
                Assert.That(ArenaDressing.RibbonHeight(b, DressingBand.South, b.yMin), Is.LessThanOrEqualTo(ArenaDressing.SouthLimit));
                Assert.That(ArenaDressing.RibbonHeight(b, DressingBand.East, b.yMin), Is.LessThan(ArenaDressing.RibbonHeight(b, DressingBand.East, b.yMax)));
            }
        }

        [Test]
        public void EveryDressingModelResolves()
        {
            for (int stage = 0; stage < 4; stage++)
                foreach (var p in ArenaDressing.Place(WorldArenaLayouts.Create(stage)))
                    Assert.That(WorldModelCatalog.Exists(p.Model), Is.True, p.Model);
        }

        [Test]
        public void UnknownDressingThemeUsesTheCourtyardRecipe()
        {
            var odd = WorldArenaLayouts.Create(0); odd.worldTheme = "Tutorial";
            Assert.That(ArenaDressing.Known("Tutorial"), Is.EqualTo("Courtyard"));
            Assert.That(ArenaDressing.Known(null), Is.EqualTo("Courtyard"));
            Assert.That(ArenaDressing.Place(odd).Count, Is.EqualTo(ArenaDressing.Place(WorldArenaLayouts.Create(0)).Count));
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t7red`
Expected: `COMPILE NOT CLEAN - stopping` (`ArenaDressing` does not exist).

- [ ] **Step 3: Write `ArenaDressing.cs`.**

```csharp
using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Where a dressing piece stands. Sides only appears in recipe rules (it expands
    /// to East and West); Light marks the holders under the eight flame anchors.</summary>
    public enum DressingBand { North, East, West, South, Inside, Sides, Light }

    public struct DressingPlacement
    {
        public string Model;
        public DressingBand Band;
        public Vector3 Position; // on the ground (y = 0); the model's pivot is bottom-centre
        public float Yaw;
        public float Height;     // target world height; the model is scaled uniformly to it
    }

    /// <summary>
    /// The per-theme enclosure recipes (spec 3.2/3.5) and their deterministic placement.
    /// Pure data and arithmetic so the height grade that hides the void is EditMode-testable.
    /// Every first-pass number here is the owner's to tune at the Task 12 sign-off.
    /// </summary>
    public static class ArenaDressing
    {
        public const float InsideLimit = .3f, SouthLimit = 1.2f, NorthMinimum = 6, RibbonThickness = 2;
        // Kenney models' front faces are assumed to point -Z after import; verify in the
        // Task 12 screenshots and flip to 0 if crypt doors face away (ledger it as a ruling).
        const float HeroYaw = 180;
        static readonly string[] Themes = { "Courtyard", "Graveyard", "Cave", "Sanctum" };

        sealed class Rule
        {
            public readonly DressingBand Band; public readonly bool Aligned; public readonly string[] Models;
            public readonly float Spacing, DepthMin, DepthMax, HeightMin, HeightMax;
            public Rule(DressingBand band, float spacing, float depthMin, float depthMax, float heightMin, float heightMax,
                bool aligned, params string[] models)
            {
                Band = band; Spacing = spacing; DepthMin = depthMin; DepthMax = depthMax;
                HeightMin = heightMin; HeightMax = heightMax; Aligned = aligned; Models = models;
            }
        }

        readonly struct Hero
        {
            public readonly string Model; public readonly float X, Depth, Height;
            public Hero(string model, float x, float depth, float height) { Model = model; X = x; Depth = depth; Height = height; }
        }

        public static string Known(string theme) => Array.IndexOf(Themes, theme) >= 0 ? theme : "Courtyard";

        // The north ribbon stands back far enough for the hero row to sit in front of it.
        public static float RibbonInset(DressingBand side) => side == DressingBand.North ? 5 : 1.2f;

        public static float RibbonHeight(Rect bounds, DressingBand side, float z)
            => side == DressingBand.North ? 7.5f : side == DressingBand.South ? 1f
                : Mathf.Lerp(2, 7, Mathf.InverseLerp(bounds.yMin, bounds.yMax, z));

        // Multiplied over the baked night colormap; a cold cast per theme.
        public static Color KitTint(string theme)
        {
            switch (Known(theme))
            {
                case "Graveyard": return new Color(.75f, .85f, .8f);
                case "Cave": return new Color(.7f, .7f, .95f);
                case "Sanctum": return new Color(.8f, .7f, .95f);
                default: return new Color(.8f, .8f, .9f);
            }
        }

        // The Sanctum's flame anchors are its obelisk tops, so it needs no holders.
        public static string LightModel(string theme)
        {
            switch (Known(theme))
            {
                case "Graveyard": return "lightpost-single";
                case "Cave": return WorldModelCatalog.Crystal;
                case "Sanctum": return null;
                default: return "fire-basket";
            }
        }

        static float LightHeight(string theme) => Known(theme) == "Graveyard" ? 2.2f : Known(theme) == "Cave" ? .9f : 1.1f;

        static string[] Series(string prefix, int count)
        { var names = new string[count]; for (int i = 0; i < count; i++) names[i] = prefix + (i + 1); return names; }

        static string[] Plus(string[] names, params string[] more)
        { var all = new string[names.Length + more.Length]; names.CopyTo(all, 0); more.CopyTo(all, names.Length); return all; }

        static Hero[] Heroes(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return new[] { new Hero("crypt-large", 0, 3, 6.5f) };
                case "Cave": return new[] { new Hero("rocks-tall", 0, 2.5f, 7.5f) };
                case "Sanctum": return new[] { new Hero("crypt-large-door", 0, 4.2f, 6.5f), new Hero("altar-stone", 0, 1.8f, 1.6f) };
                default: return new[] { new Hero("column-large", -9, 1.6f, 6.5f), new Hero("column-large", 9, 1.6f, 6.5f) };
            }
        }

        // (band, spacing, depth min/max, height min/max, aligned to the band, models)
        static Rule[] Rules(string theme)
        {
            var deadTrees = Series("CommonTree_Dead_", 5); var rocks = Series("Rock_", 7); var mossRocks = Series("Rock_Moss_", 7);
            var gravestones = new[] { "gravestone-round", "gravestone-cross", "gravestone-decorative", "gravestone-broken" };
            switch (theme)
            {
                case "Graveyard":
                    return new[]
                    {
                        new Rule(DressingBand.North, 7, 1, 2, 4, 5, true, "crypt-a", "crypt-b"),
                        new Rule(DressingBand.North, 1.8f, .4f, .9f, .9f, 1.3f, false, gravestones),
                        new Rule(DressingBand.North, 5, 7.5f, 10, 8, 10, false, Plus(Series("Willow_Dead_", 5), "pine-crooked")),
                        new Rule(DressingBand.Sides, 2, .4f, .7f, 1, 1.4f, true, "iron-fence"),
                        new Rule(DressingBand.Sides, 1.7f, .8f, 1.1f, .8f, 1.3f, false, gravestones),
                        new Rule(DressingBand.Sides, 5, 3.5f, 8, 2.5f, 3.5f, false, "crypt-small"),
                        new Rule(DressingBand.Sides, 3.5f, 4, 15, 6, 9, false, deadTrees),
                        new Rule(DressingBand.South, 2.2f, .4f, .7f, .8f, 1.1f, true, "iron-fence-damaged"),
                        new Rule(DressingBand.South, 2.6f, .8f, 1.1f, .3f, .5f, true, "grave-border"),
                    };
                case "Cave":
                    return new[]
                    {
                        new Rule(DressingBand.North, 3, .5f, 3.5f, 4, 7, false, rocks),
                        new Rule(DressingBand.North, 4, 7.5f, 10, 8, 11, false, Plus(rocks, "rocks-tall")),
                        new Rule(DressingBand.Sides, 2.5f, .3f, 1, .8f, 1.8f, false, mossRocks),
                        new Rule(DressingBand.Sides, 3, 3.5f, 8, 4, 7, false, "rocks-tall"),
                        new Rule(DressingBand.Sides, 4, 8, 16, 5, 9, false, rocks),
                        new Rule(DressingBand.South, 2, .3f, 1, .5f, 1, false, Plus(mossRocks, "rocks")),
                    };
                case "Sanctum":
                    return new[]
                    {
                        new Rule(DressingBand.North, 3.5f, 1, 1.6f, 6, 7, false, "column-large"),
                        new Rule(DressingBand.North, 5, 7.5f, 10, 7, 9, false, "pillar-obelisk"),
                        new Rule(DressingBand.Sides, 4, .5f, 1, 3.5f, 6, false, "column-large"),
                        new Rule(DressingBand.Sides, 5, 3.5f, 7, 5, 8, false, "pillar-obelisk"),
                        new Rule(DressingBand.Sides, 3, 7, 15, 3, 5, true, "stone-wall", "brick-wall"),
                        new Rule(DressingBand.South, 3, .4f, .7f, .9f, 1.1f, false, "border-pillar"),
                        new Rule(DressingBand.South, 3, .8f, 1.1f, .3f, .5f, true, "grave-border"),
                        // Candles at the north wall's foot: the only pieces allowed inside the rect.
                        new Rule(DressingBand.Inside, 4, .3f, .4f, .2f, .28f, false, "candle-multiple"),
                    };
                default: // Courtyard: a ruined cloister
                    return new[]
                    {
                        new Rule(DressingBand.North, 2.4f, .6f, 1.4f, 3.2f, 4.2f, true, "stone-wall", "stone-wall-column", "stone-wall-damaged"),
                        new Rule(DressingBand.North, 6, 7.5f, 10, 8, 10.5f, false, "pine-crooked"),
                        new Rule(DressingBand.Sides, 3, .4f, 1, 1.4f, 2.4f, true, "stone-wall-damaged", "brick-wall"),
                        new Rule(DressingBand.Sides, 5, 3.5f, 6, 2.5f, 4, true, "brick-wall", "stone-wall"),
                        new Rule(DressingBand.Sides, 3.5f, 4, 15, 6, 9, false, deadTrees),
                        new Rule(DressingBand.South, 2.6f, .4f, .7f, .7f, 1.1f, true, "stone-wall-damaged"),
                        new Rule(DressingBand.South, 2.2f, .8f, 1.1f, .5f, .8f, true, "iron-fence-border"),
                    };
            }
        }

        public static List<DressingPlacement> Place(ArenaLayout arena)
        {
            string theme = Known(arena.worldTheme); var b = arena.bounds;
            var list = new List<DressingPlacement>();
            // Seeded per theme index: string.GetHashCode is not stable across runtimes.
            var random = new System.Random(1009 + Array.IndexOf(Themes, theme) * 7919);
            var keepClear = new List<(Vector3 at, float radius)>();
            string light = LightModel(theme);
            for (int i = 0; i < 8; i++)
            {
                var anchor = WorldGeometry.FlameAnchor(arena, i); var ground = new Vector3(anchor.x, 0, anchor.z);
                keepClear.Add((ground, 1.2f));
                if (light != null)
                    list.Add(new DressingPlacement { Model = light, Band = DressingBand.Light, Position = ground, Yaw = i * 45, Height = LightHeight(theme) });
            }
            foreach (var hero in Heroes(theme))
            {
                var at = new Vector3(b.center.x + hero.X, 0, b.yMax + hero.Depth);
                keepClear.Add((at, hero.Height > 4 ? 3.5f : 1.5f));
                list.Add(new DressingPlacement { Model = hero.Model, Band = DressingBand.North, Position = at, Yaw = HeroYaw, Height = hero.Height });
            }
            foreach (var rule in Rules(theme))
                if (rule.Band == DressingBand.Sides)
                { Walk(list, b, rule, DressingBand.East, random, keepClear); Walk(list, b, rule, DressingBand.West, random, keepClear); }
                else Walk(list, b, rule, rule.Band, random, keepClear);
            return list;
        }

        static void Walk(List<DressingPlacement> list, Rect b, Rule rule, DressingBand side, System.Random random,
            List<(Vector3 at, float radius)> keepClear)
        {
            bool alongX = side == DressingBand.North || side == DressingBand.South || side == DressingBand.Inside;
            // North and south rows overrun the corners so the diagonal corner views meet dressing.
            float start = side == DressingBand.Inside ? b.xMin + .5f : alongX ? b.xMin - (side == DressingBand.North ? 4 : 2) : b.yMin - 2;
            float end = side == DressingBand.Inside ? b.xMax - .5f : alongX ? b.xMax + (side == DressingBand.North ? 4 : 2) : b.yMax + 4;
            for (float s = start + rule.Spacing * .5f * Next(random); s <= end; s += rule.Spacing * (.75f + .5f * Next(random)))
            {
                // Every draw happens before any skip, so one skipped spot never reshuffles the rest.
                float depth = Mathf.Lerp(rule.DepthMin, rule.DepthMax, Next(random));
                float height = Mathf.Lerp(rule.HeightMin, rule.HeightMax, Next(random));
                float spin = Next(random), flip = Next(random);
                string model = rule.Models[random.Next(rule.Models.Length)];
                var at = side == DressingBand.North ? new Vector3(s, 0, b.yMax + depth)
                    : side == DressingBand.South ? new Vector3(s, 0, b.yMin - depth)
                    : side == DressingBand.East ? new Vector3(b.xMax + depth, 0, s)
                    : side == DressingBand.West ? new Vector3(b.xMin - depth, 0, s)
                    : new Vector3(s, 0, b.yMax - depth);
                // Side masses follow the §1 grade: low at the camera's end, tall at the north.
                if (side == DressingBand.East || side == DressingBand.West)
                    height *= Mathf.Lerp(.4f, 1, Mathf.InverseLerp(b.yMin, b.yMax, s));
                if (Blocked(at, keepClear)) continue;
                float yaw = rule.Aligned ? (alongX ? 0 : 90) + (flip < .5f ? 0 : 180) : spin * 360;
                list.Add(new DressingPlacement { Model = model, Band = side, Position = at, Yaw = yaw, Height = height });
            }
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        static bool Blocked(Vector3 at, List<(Vector3 at, float radius)> keepClear)
        {
            foreach (var (centre, radius) in keepClear)
                if (new Vector2(at.x - centre.x, at.z - centre.z).sqrMagnitude < radius * radius) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t7`
Expected: 0 failures. If `DressingFollowsTheHeightGrade` fails on a count of 40 or fewer, a recipe's spacing is wrong; fix the recipe, not the threshold.

- [ ] **Step 5: Commit.** Stage `ArenaDressing.cs(.meta)` and `ArenaRedesignTests.cs`. Message: `Add deterministic per-theme enclosure dressing recipes`.

---

### Task 8: Cover models

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/CoverModels.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `WorldModelLibrary.Spawn`, `WorldModelCatalog.Crystal`, `DecayPropKind`.
- Produces: `CoverModels.Model(DecayPropKind, int index) : string`, `Rubble(DecayPropKind, int index) : string` and `Height(DecayPropKind) : float`.
- Produces: `Fit(Vector3 size, Vector2 rect, float height, out Vector3 scale, out float yaw)` (pure).
- Produces: `Build(WorldModelLibrary, Transform parent, string name, string model, Vector2 rect, float height) : GameObject`. It returns the wrapper named `name` at the parent's origin, or null.
- Produces: `CoverModels.Fill` (.92).

- [ ] **Step 1: Append the failing tests.**

```csharp
        [Test]
        public void CoverFitFillsTheRectWithoutOverhang()
        {
            var sizes = new[] { new Vector3(2, 1, .5f), new Vector3(.5f, 3, 2), new Vector3(1, 1, 1) };
            var rects = new[] { new Vector2(3.2f, 1), new Vector2(1, 3.2f), new Vector2(1.4f, 1.4f) };
            foreach (var size in sizes) foreach (var rect in rects)
            {
                CoverModels.Fit(size, rect, 2.5f, out var scale, out float yaw);
                var local = Vector3.Scale(size, scale);
                // After a 90 degree yaw the model's local x runs along world z.
                var footprint = yaw == 0 ? new Vector2(local.x, local.z) : new Vector2(local.z, local.x);
                Assert.That(footprint.x, Is.LessThanOrEqualTo(rect.x + 1e-4f)); Assert.That(footprint.y, Is.LessThanOrEqualTo(rect.y + 1e-4f));
                Assert.That(footprint.x * footprint.y, Is.GreaterThanOrEqualTo(.8f * rect.x * rect.y));
                Assert.That(local.y, Is.EqualTo(2.5f).Within(1e-4f));
            }
        }

        [Test]
        public void EveryCoverKindBuildsInsideItsCollisionRect()
        {
            var parent = new GameObject("probe").transform; var rect = new Vector2(2.2f, 1.3f);
            using (var library = new WorldModelLibrary(Color.white))
                try
                {
                    foreach (DecayPropKind kind in System.Enum.GetValues(typeof(DecayPropKind)))
                        foreach (var model in new[] { CoverModels.Model(kind, 0), CoverModels.Rubble(kind, 0) })
                        {
                            var built = CoverModels.Build(library, parent, kind.ToString(), model, rect, CoverModels.Height(kind));
                            Assert.That(built, Is.Not.Null, model);
                            var bounds = built.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                            Assert.That(bounds.size.x, Is.LessThanOrEqualTo(rect.x + .02f), model);
                            Assert.That(bounds.size.z, Is.LessThanOrEqualTo(rect.y + .02f), model);
                            Assert.That(bounds.size.x * bounds.size.z, Is.GreaterThanOrEqualTo(.8f * rect.x * rect.y), model);
                            Object.DestroyImmediate(built);
                        }
                }
                finally { Object.DestroyImmediate(parent.gameObject); }
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t8red`
Expected: `COMPILE NOT CLEAN - stopping` (`CoverModels` does not exist).

- [ ] **Step 3: Write `CoverModels.cs`.**

```csharp
using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Gameplay cover as kit models (spec 3.4). Each model is stretched so its bounding
    /// footprint fills 92% of the analytic collision rect: what blocks a shot looks like
    /// it blocks a shot, and nothing visibly overhangs into open floor. Collision itself
    /// stays in ArenaSim.
    /// </summary>
    public static class CoverModels
    {
        public const float Fill = .92f;

        public static string Model(DecayPropKind kind, int index)
        {
            switch (kind)
            {
                case DecayPropKind.Tomb: return index % 2 == 0 ? "crypt-small" : "gravestone-wide";
                case DecayPropKind.Urn: return "urn-round";
                case DecayPropKind.Column: return "column-large";
                case DecayPropKind.Rock: return "Rock_Moss_" + (1 + index % 7);
                case DecayPropKind.DeadTree: return "CommonTree_Dead_" + (1 + index % 5);
                case DecayPropKind.Obelisk: return "pillar-obelisk";
                case DecayPropKind.Crystal: return WorldModelCatalog.Crystal;
                default: return "stone-wall-damaged"; // RuinedWall and any later kind
            }
        }

        public static string Rubble(DecayPropKind kind, int index)
        {
            switch (kind)
            {
                case DecayPropKind.Tomb: return "gravestone-debris";
                case DecayPropKind.Rock: return "Rock_" + (1 + index % 7);
                case DecayPropKind.DeadTree: return "trunk";
                case DecayPropKind.Crystal: return WorldModelCatalog.Crystal; // flattened to shards by the rubble height
                default: return "debris";
            }
        }

        // Heights keep each kind's old silhouette: the obelisk top stays under the 3.1
        // flame anchor, and low kinds (urns, tombs) stay under the actors' 1.2 eye line plus a margin.
        public static float Height(DecayPropKind kind)
        {
            switch (kind)
            {
                case DecayPropKind.Column: return 3.2f;
                case DecayPropKind.Tomb: return 1.6f;
                case DecayPropKind.DeadTree: return 3.6f;
                case DecayPropKind.Rock: return 1.6f;
                case DecayPropKind.Obelisk: return 2.8f;
                case DecayPropKind.Urn: return 1.3f;
                case DecayPropKind.Crystal: return 1.8f;
                default: return 2.2f;
            }
        }

        public static void Fit(Vector3 size, Vector2 rect, float height, out Vector3 scale, out float yaw)
        {
            // Turn the model so its long side runs along the rect's long side; stretching a
            // wall 3x across its thin axis would look far worse than a quarter turn.
            yaw = size.x >= size.z == rect.x >= rect.y ? 0 : 90;
            // After a 90 degree yaw the model's local x lies along world z.
            float alongLocalX = yaw == 0 ? rect.x : rect.y, alongLocalZ = yaw == 0 ? rect.y : rect.x;
            scale = new Vector3(alongLocalX * Fill / Mathf.Max(.01f, size.x), height / Mathf.Max(.01f, size.y),
                alongLocalZ * Fill / Mathf.Max(.01f, size.z));
        }

        public static GameObject Build(WorldModelLibrary library, Transform parent, string name, string model, Vector2 rect, float height)
        {
            var value = library.Spawn(model, parent, out var size);
            if (value == null) return null; // already logged by the library; the cover stays invisible but solid
            value.name = name;
            Fit(size, rect, height, out var scale, out float yaw);
            // TRS order: the scale applies in model space, then the yaw turns it into place.
            value.transform.localPosition = Vector3.zero;
            value.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            value.transform.localScale = scale;
            return value;
        }
    }
}
```

- [ ] **Step 4: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t8`
Expected: 0 failures.

- [ ] **Step 5: Commit.** Stage `CoverModels.cs(.meta)` and `ArenaRedesignTests.cs`. Message: `Fit kit-model cover to the collision rects`.

---

### Task 9: Painted floor and the enclosure

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/PaintedFloor.cs`
- Create: `Assets/Game/Scripts/Presentation/WorldArt/ArenaEnclosure.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `PaintedMaterials.Create`, `ArenaDressing.*`, `WorldModelLibrary.Spawn`, and the `WorldFloors/*` resources from Task 4.
- Produces: `PaintedFloor(Transform root, ArenaLayout arena, string theme, List<UnityEngine.Object> owned)`. It exposes the `Floor` and `Outer` materials and creates the GameObjects "Floor" (a quad at y 0, scale = bounds) and "Outer ground" (a quad at y -0.02, bounds ±40).
- Produces: `PaintedFloor.Texture(string theme, string suffix) : Texture2D`, `PaintedFloor.SnapshotCap` (512), `PaintedFloor.PatchNoise(float u, float v) : float`, and `PaintedFloor.Snapshot(Material, int cap, out bool baked) : Texture2D`.
- Produces: `ArenaEnclosure(Transform root, ArenaLayout arena, WorldModelLibrary models, Material ribbon, List<UnityEngine.Object> owned)`. It exposes `Scenery` (`List<Renderer>`, the dressing renderers) and creates the GameObjects `ArenaEnclosure.RibbonName` ("Enclosure ribbon") and "Dressing".

- [ ] **Step 1: Append the failing tests.**

```csharp
        static Texture2D Solid(Color color, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height]; for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels); texture.Apply(); return texture;
        }

        [Test]
        public void InterruptedMorphSnapshotsWhatWasOnScreen()
        {
            Texture2D black = Solid(Color.black, 64, 48), white = Solid(Color.white, 64, 48), shot = null;
            var material = PaintedMaterials.Create("probe", white, Color.white);
            try
            {
                material.SetTexture("_FromMap", black);
                // A finished morph needs no CPU work: the snapshot IS the texture on screen.
                material.SetFloat("_Morph", 1);
                Assert.That(PaintedFloor.Snapshot(material, 512, out bool baked), Is.SameAs(white)); Assert.That(baked, Is.False);
                // Interrupted at 60%: every pixel matches the shader's patch mix at that moment.
                material.SetFloat("_Morph", .6f);
                shot = PaintedFloor.Snapshot(material, 32, out baked);
                Assert.That(baked, Is.True); Assert.That(shot.width, Is.EqualTo(32)); Assert.That(shot.height, Is.EqualTo(24));
                var pixels = shot.GetPixels();
                for (int y = 0; y < shot.height; y++) for (int x = 0; x < shot.width; x++)
                {
                    float expected = Mathf.Clamp01(.6f * 1.65f - PaintedFloor.PatchNoise((x + .5f) / shot.width, (y + .5f) / shot.height) * .65f);
                    Assert.That(pixels[y * shot.width + x].r, Is.EqualTo(expected).Within(2.5f / 255), $"({x},{y})");
                }
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(black); Object.DestroyImmediate(white); if (shot != null) Object.DestroyImmediate(shot); }
        }

        [Test]
        public void FloorGroundAndRibbonFormASolidLayer()
        {
            var root = new GameObject("probe").transform; var owned = new List<Object>();
            var arena = WorldArenaLayouts.Create(0); var b = arena.bounds;
            using (var models = new WorldModelLibrary(Color.white))
                try
                {
                    new PaintedFloor(root, arena, arena.worldTheme, owned);
                    var ribbonMaterial = PaintedMaterials.Create("Wall", PaintedFloor.Texture(arena.worldTheme, "Ribbon"), Color.white); owned.Add(ribbonMaterial);
                    var enclosure = new ArenaEnclosure(root, arena, models, ribbonMaterial, owned);
                    var floor = root.Find("Floor"); var outer = root.Find("Outer ground"); var ribbon = root.Find(ArenaEnclosure.RibbonName);
                    Assert.That(floor.position.y, Is.EqualTo(0).Within(1e-4)); Assert.That(floor.localScale.x, Is.EqualTo(b.width).Within(1e-4));
                    Assert.That(outer.position.y, Is.LessThan(0)); Assert.That(outer.localScale.x, Is.EqualTo(b.width + 80).Within(1e-3));
                    var ribbonBounds = ribbon.GetComponent<Renderer>().bounds;
                    // The ribbon closes all four sides of the arena and reaches the §1 north height.
                    Assert.That(ribbonBounds.min.x, Is.LessThan(b.xMin)); Assert.That(ribbonBounds.max.x, Is.GreaterThan(b.xMax));
                    Assert.That(ribbonBounds.min.z, Is.LessThan(b.yMin)); Assert.That(ribbonBounds.max.z, Is.GreaterThan(b.yMax));
                    Assert.That(ribbonBounds.max.y, Is.GreaterThanOrEqualTo(ArenaDressing.NorthMinimum));
                    Assert.That(enclosure.Scenery.Count, Is.GreaterThan(40));
                    foreach (var renderer in enclosure.Scenery) Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(PaintedMaterials.ShaderName));
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                }
                finally { Object.DestroyImmediate(root.gameObject); foreach (var item in owned) Object.DestroyImmediate(item); }
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t9red`
Expected: `COMPILE NOT CLEAN - stopping` (`PaintedFloor`/`ArenaEnclosure` do not exist).

- [ ] **Step 3: Write `PaintedFloor.cs`.**

```csharp
using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The arena floor (one painted quad covering the bounds once, amendment A1) and the
    /// darker outer ground that runs 40 units past it under the enclosure, the spec's
    /// safety net: any gap between props reads as dark ground, never as void.
    /// </summary>
    public sealed class PaintedFloor
    {
        public const float OuterMargin = 40, OuterTile = 8;
        public const int SnapshotCap = 512;
        public Material Floor { get; }
        public Material Outer { get; }

        public PaintedFloor(Transform root, ArenaLayout arena, string theme, List<Object> owned)
        {
            var b = arena.bounds;
            Floor = PaintedMaterials.Create("Floor", Texture(theme, "Floor"), Color.white); owned.Add(Floor);
            Quad("Floor", root, new Vector3(b.center.x, 0, b.center.y), b.size, Floor);
            var outerSize = b.size + Vector2.one * OuterMargin * 2;
            Outer = PaintedMaterials.Create("Outer ground", Texture(theme, "Outer"), Color.white); owned.Add(Outer);
            // _FromMap shares the _BaseMap UVs in the shader, so the morph tiles both alike.
            Outer.SetTextureScale("_BaseMap", outerSize / OuterTile);
            // 2 cm under the floor: no z-fighting at the seam, invisible at this camera height.
            Quad("Outer ground", root, new Vector3(b.center.x, -.02f, b.center.y), outerSize, Outer);
        }

        public static Texture2D Texture(string theme, string suffix)
        {
            string path = "WorldFloors/" + ArenaDressing.Known(theme) + suffix;
            var texture = Resources.Load<Texture2D>(path);
            if (texture != null) return texture;
            Debug.LogError("Painted texture missing: Resources/" + path + " (run Borrowed Hex/Art/Bake Painted Floors)");
            return Texture2D.grayTexture;
        }

        static void Quad(string name, Transform root, Vector3 at, Vector2 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Quad); value.name = name;
            var collider = value.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            value.transform.SetParent(root, false);
            // Lying flat and facing up: texture row 0 (the baker's bottom row) lands on the south edge.
            value.transform.SetPositionAndRotation(at, Quaternion.Euler(90, 0, 0));
            value.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = value.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        /// The shader's morph patch hash, Hash(float3(floor(uv*18), 5)), on the CPU.
        public static float PatchNoise(float u, float v)
            => Mathf.Repeat(Mathf.Sin(Mathf.Floor(u * 18) * 127.1f + Mathf.Floor(v * 18) * 311.7f + 5 * 74.7f) * 43758.5453f, 1);

        /// <summary>
        /// What the material shows right now, as a texture the next morph can start from.
        /// A finished morph (the usual case) is just its _BaseMap: no CPU work and no new
        /// texture (spec 2.2). Only an interrupted morph bakes, capped at `cap` px wide.
        /// When `baked` is false the result is a shared asset the caller must NOT release.
        /// </summary>
        public static Texture2D Snapshot(Material material, int cap, out bool baked)
        {
            baked = false;
            var target = material.GetTexture("_BaseMap") as Texture2D; var source = material.GetTexture("_FromMap") as Texture2D;
            float amount = material.GetFloat("_Morph");
            if (amount >= 1 || source == null || source == target || target == null) return target;
            int width = Mathf.Min(cap, target.width), height = Mathf.Max(1, Mathf.RoundToInt(width * (float)target.height / target.width));
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width, v = (y + .5f) / height;
                float mix = Mathf.Clamp01(amount * 1.65f - PatchNoise(u, v) * .65f);
                pixels[y * width + x] = Color.Lerp(source.GetPixelBilinear(u, v), target.GetPixelBilinear(u, v), mix);
            }
            var snapshot = new Texture2D(width, height, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = target.wrapMode };
            snapshot.SetPixels32(pixels); snapshot.Apply();
            baked = true; return snapshot;
        }
    }
}
```

- [ ] **Step 4: Write `ArenaEnclosure.cs`.**

```csharp
using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The visual ring around the play rect (spec 1, 3.3): a continuous ribbon wall that
    /// guarantees the no-void property whatever the props do, plus the kit dressing placed
    /// from ArenaDressing. The ribbon never dissolves; dressing renderers are returned as
    /// Scenery so WorldGeometry can form and retire them with the morph.
    /// </summary>
    public sealed class ArenaEnclosure
    {
        public const string RibbonName = "Enclosure ribbon";
        public readonly List<Renderer> Scenery = new List<Renderer>();

        public ArenaEnclosure(Transform root, ArenaLayout arena, WorldModelLibrary models, Material ribbon, List<Object> owned)
        {
            BuildRibbon(root, arena.bounds, ribbon, owned);
            var dressing = new GameObject("Dressing").transform; dressing.SetParent(root, false);
            foreach (var placement in ArenaDressing.Place(arena))
            {
                var model = models.Spawn(placement.Model, dressing, out var size);
                if (model == null) continue; // the library logged it (spec 4.4)
                model.transform.SetPositionAndRotation(placement.Position, Quaternion.Euler(0, placement.Yaw, 0));
                model.transform.localScale = Vector3.one * (placement.Height / Mathf.Max(.01f, size.y));
                Scenery.AddRange(model.GetComponentsInChildren<Renderer>());
            }
        }

        static void BuildRibbon(Transform root, Rect b, Material material, List<Object> owned)
        {
            float t = ArenaDressing.RibbonThickness;
            float west = b.xMin - ArenaDressing.RibbonInset(DressingBand.West), east = b.xMax + ArenaDressing.RibbonInset(DressingBand.East);
            float south = b.yMin - ArenaDressing.RibbonInset(DressingBand.South), north = b.yMax + ArenaDressing.RibbonInset(DressingBand.North);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            // North and south strips run the full outer width, so they close the four corners.
            Strip(new Vector3(west - t, 0, south), new Vector3(east + t, 0, south), Vector3.back, DressingBand.South);
            Strip(new Vector3(west - t, 0, north), new Vector3(east + t, 0, north), Vector3.forward, DressingBand.North);
            Strip(new Vector3(west, 0, south), new Vector3(west, 0, north), Vector3.left, DressingBand.West);
            Strip(new Vector3(east, 0, south), new Vector3(east, 0, north), Vector3.right, DressingBand.East);
            var mesh = new Mesh { name = RibbonName };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            var value = new GameObject(RibbonName); value.transform.SetParent(root, false);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;

            void Strip(Vector3 from, Vector3 to, Vector3 outward, DressingBand side)
            {
                float length = Vector3.Distance(from, to); int steps = Mathf.Max(1, Mathf.CeilToInt(length));
                int start = vertices.Count;
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector3.Lerp(from, to, i / (float)steps);
                    float jitter = Jitter(side, i);
                    // A ragged face and crest read as rock or ruin, not a ruler-straight wall.
                    p -= outward * (jitter * .3f);
                    float h = ArenaDressing.RibbonHeight(b, side, p.z);
                    // The south crest may only dip: it must stay under the 1.2 limit.
                    h += side == DressingBand.South ? Mathf.Min(0, jitter) * .3f : jitter * .8f;
                    float u = i * length / steps / 8; // the ribbon texture spans 8 x 4 units
                    vertices.Add(p); vertices.Add(p + Vector3.up * h); vertices.Add(p + Vector3.up * h + outward * t);
                    uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, h / 4)); uv.Add(new Vector2(u, (h + t) / 4));
                    if (i == 0) continue;
                    int a = start + (i - 1) * 3, c = start + i * 3;
                    Quad(a, c, a + 1, c + 1, -outward);           // the face toward the arena
                    Quad(a + 1, c + 1, a + 2, c + 2, Vector3.up); // the crest
                }
            }

            void Quad(int p00, int p10, int p01, int p11, Vector3 facing) { Tri(p00, p01, p10, facing); Tri(p10, p01, p11, facing); }

            void Tri(int i0, int i1, int i2, Vector3 facing)
            {
                // PaintedWorld culls back faces. Unity draws a triangle whose
                // Cross(v1 - v0, v2 - v0) points at the viewer, so each triangle is ordered
                // against its intended facing rather than trusting the loop's winding.
                var normal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
                if (Vector3.Dot(normal, facing) >= 0) triangles.AddRange(new[] { i0, i1, i2 });
                else triangles.AddRange(new[] { i0, i2, i1 });
            }
        }

        static float Jitter(DressingBand side, int i)
            => Mathf.Repeat(Mathf.Sin(i * 12.9898f + (int)side * 78.233f) * 43758.5453f, 1) - .5f;
    }
}
```

- [ ] **Step 5: Run the gate and watch it pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t9`
Expected: 0 failures.

- [ ] **Step 6: Commit.** Stage `PaintedFloor.cs(.meta)`, `ArenaEnclosure.cs(.meta)` and `ArenaRedesignTests.cs`. Message: `Build the painted floor, outer ground and enclosure ribbon`.

---

### Task 10: Swap WorldGeometry onto the new parts and remove the old ones

**Files:**
- Rewrite: `Assets/Game/Scripts/Presentation/WorldArt/WorldGeometry.cs` (the public API is unchanged, per spec 4.1)
- Modify: `Assets/Game/Scripts/Presentation/WorldArt/WorldPresentation.cs` (remove the backdrop and the north-limit bookkeeping)
- Delete: `WorldBackdrop.cs`, `WorldPixelSurfaces.cs`, `WorldBoundaryPolicy.cs` (and their `.meta` files)
- Modify: `Assets/Game/Tests/EditMode/WorldPixelRevisionTests.cs` (delete `BoundaryProgressesFromClosedToBrokenToOpen`)
- Modify: `Assets/Game/Tests/PlayMode/WorldPresentationPlayModeTests.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `PaintedFloor`, `ArenaEnclosure`, `CoverModels`, `WorldModelLibrary`, `ArenaDressing.KitTint` and `PaintedMaterials`.
- Produces: the same `WorldGeometry` API: `CurrentTheme`, `Stone` (now the ribbon material), `FloorTexture`, `FloorFrom`, `RenderCover`, `RenderRetiring`, `BeginMorphFrom`, `RenderMorph`, `RenderSceneryFormation`, `RenderReveal`, `FinishReveal`, `RenderActorOcclusion`, `FlameAnchor`, `SetTheme`, `SetVisible` and `Dispose`.
- Produces the hierarchy under `Environment/`: `Floor`, `Outer ground`, `Enclosure ribbon`, `Dressing/…`, `CoverTrim{i}/{Kind}` and `CoverRubble{i}/Rubble`.

- [ ] **Step 1: Append the failing test.**

```csharp
        [Test]
        public void WorldGeometryBuildsTheRedesignedArena()
        {
            var parent = new GameObject("probe").transform; var art = new WorldArtLibrary();
            var geometry = new WorldGeometry(parent, WorldArenaLayouts.Create(1), art, null);
            try
            {
                var environment = parent.Find("Environment");
                Assert.That(environment.Find("Floor").GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo(PaintedMaterials.ShaderName));
                Assert.That(environment.Find(ArenaEnclosure.RibbonName), Is.Not.Null);
                Assert.That(environment.Find("CoverTrim0/Tomb"), Is.Not.Null, "Stage 1 pillar 0 is a Tomb");
                foreach (Transform child in environment) Assert.That(child.name, Does.Not.StartWith("Boundary"));
                Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty, "No billboard sprites in ordinary arenas");
                Assert.That(parent.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { geometry.Dispose(); art.Dispose(); Object.DestroyImmediate(parent.gameObject); }
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t10red`
Expected: `NOT PASSED: ...WorldGeometryBuildsTheRedesignedArena`, because `Enclosure ribbon` was expected not null. The old geometry still builds pixel boxes.

- [ ] **Step 3: Rewrite `WorldGeometry.cs`** in full:

```csharp
using System;
using System.Collections.Generic;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Orchestrates one arena's world art and owns its effect API (spec 4.1/4.2): the painted
    /// floor and outer ground (PaintedFloor), the backing ribbon and kit dressing
    /// (ArenaEnclosure) and kit-model cover fitted to the collision rects (CoverModels).
    /// Analytic collision stays in ArenaSim; nothing here has a collider. Coverage
    /// dissolves (_Visible/_Retiring) replace growth and shatter animations, as before.
    /// </summary>
    public sealed class WorldGeometry : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Transform root;
        readonly WorldArtLibrary art;
        readonly WorldModelLibrary models;
        readonly ArenaLayout layout;
        // Floor, Outer and Wall (the ribbon) cross-fade through _Morph and never dissolve,
        // so a morph can never open a hole onto the void (spec 3.3).
        readonly Dictionary<string, Material> surfaces = new Dictionary<string, Material>();
        readonly List<GameObject> cover = new List<GameObject>(), rubble = new List<GameObject>();
        readonly List<Renderer> scenery = new List<Renderer>();
        readonly Dictionary<Renderer, float> sceneryBase = new Dictionary<Renderer, float>();
        readonly Dictionary<GameObject, float> coverBase = new Dictionary<GameObject, float>();
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        bool morphing, retiring;
        float sceneryAmount = 1;
        public string CurrentTheme { get; private set; }
        public Material Stone => surfaces["Wall"];
        public Texture2D FloorTexture => (Texture2D)surfaces["Floor"].GetTexture("_BaseMap");
        public Texture2D FloorFrom => (Texture2D)surfaces["Floor"].GetTexture("_FromMap");

        public WorldGeometry(Transform parent, ArenaLayout arena, WorldArtLibrary library, Material unusedTemplate)
        {
            art = library; layout = arena; CurrentTheme = arena.worldTheme;
            root = Group("Environment", parent).transform;
            models = new WorldModelLibrary(ArenaDressing.KitTint(CurrentTheme));
            var floor = new PaintedFloor(root, arena, CurrentTheme, owned);
            surfaces["Floor"] = floor.Floor; surfaces["Outer"] = floor.Outer;
            surfaces["Wall"] = PaintedMaterials.Create("Wall", PaintedFloor.Texture(CurrentTheme, "Ribbon"), Color.white);
            owned.Add(surfaces["Wall"]);
            scenery.AddRange(new ArenaEnclosure(root, arena, models, surfaces["Wall"], owned).Scenery);
            for (int i = 0; i < arena.pillars.Count; i++) BuildCover(i);
            if (CurrentTheme == "Sanctum")
            {
                // The ritual seal stays on top of the painted basalt (spec 2.2).
                var b = arena.bounds; var glyph = art.Effect("Vortex", 100);
                if (glyph.Length > 0)
                {
                    var ring = Group("Ritual seal", root); ring.transform.position = Geometry2D.ToWorld(b.center, .025f);
                    ring.transform.rotation = Quaternion.Euler(90, 0, 0); ring.transform.localScale = Vector3.one * 19;
                    var renderer = ring.AddComponent<SpriteRenderer>(); renderer.sprite = glyph[0];
                    renderer.color = new Color(.38f, .55f, .8f, .3f);
                }
            }
        }

        void BuildCover(int index)
        {
            var p = layout.pillars[index];
            var kind = index < layout.propKinds.Count ? layout.propKinds[index] : DecayPropKind.Column;
            var group = Group("CoverTrim" + index, root); group.transform.position = Geometry2D.ToWorld(p.center);
            float height = CoverModels.Height(kind);
            CoverModels.Build(models, group.transform, kind.ToString(), CoverModels.Model(kind, index), p.size, height);
            if (kind == DecayPropKind.Obelisk)
            {
                // The rune crown is the Sanctum reveal's landmark; spec 3.4 keeps it.
                var crown = Box("Ritual crown", group.transform, Geometry2D.ToWorld(p.center, height + .25f), new Vector3(.7f, .5f, .7f), Stone);
                crown.transform.rotation = Quaternion.Euler(0, 45, 0);
            }
            cover.Add(group);
            var debris = Group("CoverRubble" + index, root); debris.transform.position = Geometry2D.ToWorld(p.center);
            CoverModels.Build(models, debris.transform, "Rubble", CoverModels.Rubble(kind, index), p.size * .7f, .45f);
            debris.SetActive(false); rubble.Add(debris);
        }

        static GameObject Group(string name, Transform parent)
        { var value = new GameObject(name); value.transform.SetParent(parent, false); return value; }

        static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            // Disabled first: in play mode Release defers destruction to the end of the frame.
            var collider = value.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            value.transform.SetParent(parent, false); value.transform.position = at; value.transform.localScale = size;
            value.GetComponent<Renderer>().sharedMaterial = material; return value;
        }

        public static Vector3 FlameAnchor(ArenaLayout arena, int i)
        {
            if (arena.worldTheme == "Sanctum" && i < arena.pillars.Count) return Geometry2D.ToWorld(arena.pillars[i].center, 3.1f);
            var b = arena.bounds;
            return new Vector3(b.center.x + (i % 2 == 0 ? -1 : 1) * (b.width / 2 + .45f), 1.4f, b.yMin + 1 + i * b.height / 8);
        }

        public void SetTheme(string value) => CurrentTheme = value;

        void Visibility(Renderer renderer, float amount, bool old = false, Color? emission = null)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Visible", Mathf.Clamp01(amount)); properties.SetFloat("_Retiring", old ? 1 : 0);
            if (emission.HasValue) properties.SetColor("_Emission", emission.Value);
            renderer.SetPropertyBlock(properties); properties.Clear();
        }

        void Visibility(GameObject value, float amount, bool old = false)
        { foreach (var renderer in value.GetComponentsInChildren<Renderer>(true)) Visibility(renderer, amount, old); }

        public void RenderCover(ArenaSim sim)
        {
            for (int i = 0; i < cover.Count; i++)
            {
                var state = sim.Pillars[i]; bool broken = state.Crumbled;
                float visible = sim.CoverFormed(i) ? Mathf.Clamp01(1 - (float)(sim.Clock.Now - state.RestoredAt) / (state.MaxDurability * state.DecayInterval)) : sim.CoverFormation(i);
                cover[i].SetActive(!broken); Visibility(cover[i], broken ? 0 : visible);
                coverBase[cover[i]] = broken ? 0 : visible;
                rubble[i].SetActive(broken); // static remains; no shatter/growth animation
            }
        }

        public void RenderRetiring(ArenaSim sim)
        {
            for (int i = 0; i < cover.Count; i++)
            {
                int oldIndex = sim.RetiringCover.FindIndex(value => value.Bounds == layout.pillars[i]);
                float visible = oldIndex < 0 ? 0 : sim.RetiringCoverVisibility(oldIndex);
                // Retirement starts from the displayed state, so partially decayed cover
                // never fills itself back in when the next arena starts replacing it.
                visible *= coverBase.TryGetValue(cover[i], out float baseline) ? baseline : 1;
                cover[i].SetActive(visible > 0); Visibility(cover[i], visible, true);
                rubble[i].SetActive(false);
            }
        }

        Texture2D Snapshot(Material material)
        {
            var snapshot = PaintedFloor.Snapshot(material, PaintedFloor.SnapshotCap, out bool baked);
            // An un-baked snapshot is a shared Resources texture: releasing it would destroy the asset.
            if (baked) owned.Add(snapshot);
            return snapshot;
        }

        public void BeginMorphFrom(WorldGeometry previous)
        {
            morphing = true; previous.retiring = true;
            foreach (var pair in surfaces)
            {
                if (!previous.surfaces.TryGetValue(pair.Key, out var before)) continue;
                var snapshot = Snapshot(before); pair.Value.SetTexture("_FromMap", snapshot); pair.Value.SetFloat("_Morph", 0);
                before.SetTexture("_FromMap", snapshot); before.SetTexture("_BaseMap", pair.Value.GetTexture("_BaseMap"));
            }
            foreach (var renderer in previous.scenery) previous.sceneryBase[renderer] = previous.sceneryAmount;
            // Stages 0-2 share one footprint: the incoming floor, ground and ribbon take over
            // in place, so the outgoing copies are hidden rather than dissolved (no z-fighting).
            foreach (string name in new[] { "Floor", "Outer ground", ArenaEnclosure.RibbonName })
            {
                var old = previous.root.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }
        }

        public void RenderMorph(float amount, double now, bool reduceFlashes)
        {
            foreach (var material in surfaces.Values)
            {
                material.SetFloat("_Morph", morphing || retiring ? amount : 1);
                material.SetFloat("_GlitchTime", (float)now); material.SetFloat("_Glitches", reduceFlashes ? 0 : 1);
            }
        }

        public void RenderSceneryFormation(float amount)
        {
            sceneryAmount = amount;
            foreach (var renderer in scenery)
                Visibility(renderer, amount * (sceneryBase.TryGetValue(renderer, out float start) ? start : 1), retiring);
        }

        public void RenderReveal(int lit, float opening)
        {
            float room = Mathf.Lerp(.025f + lit * .035f, 1, opening);
            foreach (var material in surfaces.Values) material.SetFloat("_Reveal", room);
            foreach (var renderer in scenery) Visibility(renderer, opening);
            for (int i = 0; i < cover.Count; i++)
                foreach (var renderer in cover[i].GetComponentsInChildren<Renderer>())
                {
                    renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", i < lit ? 1 : .015f);
                    properties.SetColor("_Emission", i < lit ? new Color(.035f, .12f, .2f) : Color.black);
                    renderer.SetPropertyBlock(properties); properties.Clear();
                }
        }

        public void FinishReveal()
        {
            foreach (var material in surfaces.Values) material.SetFloat("_Reveal", 1);
            foreach (var group in cover) foreach (var renderer in group.GetComponentsInChildren<Renderer>())
            { renderer.GetPropertyBlock(properties); properties.SetFloat("_Reveal", 1); renderer.SetPropertyBlock(properties); properties.Clear(); }
            RenderSceneryFormation(1);
        }

        public void RenderActorOcclusion(Camera camera, Vector3 player, Vector3? boss)
        {
            if (camera == null) return;
            bool Behind(Renderer renderer, Vector3 actor)
            {
                var delta = actor + Vector3.up * 1.2f - camera.transform.position;
                return renderer.bounds.IntersectRay(new Ray(camera.transform.position, delta.normalized), out float distance)
                    && distance < delta.magnitude - .15f;
            }
            foreach (var group in cover)
            {
                var renderers = group.GetComponentsInChildren<Renderer>();
                bool blocks = false;
                foreach (var renderer in renderers)
                    if (Behind(renderer, player) || boss.HasValue && Behind(renderer, boss.Value)) { blocks = true; break; }
                // A retained pillar can sit directly over the Collector or the player. The
                // cutout keeps their silhouettes readable while keeping the pillar's geometry
                // and collision; this is unrelated to world decay.
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(properties); properties.SetFloat("_Occlusion", blocks ? .12f : 1);
                    renderer.SetPropertyBlock(properties); properties.Clear();
                }
            }
        }

        public void Dispose()
        {
            WorldArtLibrary.Release(root.gameObject);
            foreach (var item in owned) WorldArtLibrary.Release(item); owned.Clear();
            models.Dispose();
        }

        public void SetVisible(bool value) => root.gameObject.SetActive(value);
    }
}
```

- [ ] **Step 4: Remove the backdrop from `WorldPresentation.cs`.** Make these exact edits; nothing else changes in this task.
  - Delete the field line `WorldBackdrop backdrop;` (line 25) and `float arenaNorth, departureNorth;` (line 37).
  - In `Start`, delete `backdrop = new WorldBackdrop(transform, art);`.
  - In `LateUpdate`, delete these two lines:
    ```csharp
            float visibleNorth = travel.Active && travel.Elapsed < WorldIntroPolicy.Rise ? departureNorth : arenaNorth;
            backdrop.Render(geometry.FloorTexture, geometry.FloorFrom, sim.WorldMorphProgress, sim.Clock.Now, UI.DisplayOptions.ReduceFlashes, visibleNorth);
    ```
  - In `Rebind`, delete `arenaNorth = sim.Arena.bounds.yMax;` and change `introOverlay.Clear(); backdrop.SetReveal(1);` to `introOverlay.Clear();`.
  - In `ChangeArena`, delete the two comment lines ("Keep the departure horizon…" / "cutoff only when…") and `departureNorth = arenaNorth; arenaNorth = sim.Arena.bounds.yMax;`.
  - In `RenderPullAndCamera`, change `geometry.RenderReveal(lit, opening); backdrop.SetReveal(Mathf.Lerp(.015f, 1, opening));` to `geometry.RenderReveal(lit, opening);`, and `geometry.FinishReveal(); backdrop.SetReveal(1); introOverlay.Clear();` to `geometry.FinishReveal(); introOverlay.Clear();`.
  - In `OnDestroy`, change `geometry?.Dispose(); backdrop.Dispose(); introOverlay.Dispose(); art.Dispose();` to `geometry?.Dispose(); introOverlay.Dispose(); art.Dispose();`.

- [ ] **Step 5: Delete the superseded files.**

Run: `git rm -q Assets/Game/Scripts/Presentation/WorldArt/WorldBackdrop.cs Assets/Game/Scripts/Presentation/WorldArt/WorldBackdrop.cs.meta Assets/Game/Scripts/Presentation/WorldArt/WorldPixelSurfaces.cs Assets/Game/Scripts/Presentation/WorldArt/WorldPixelSurfaces.cs.meta Assets/Game/Scripts/Presentation/WorldArt/WorldBoundaryPolicy.cs Assets/Game/Scripts/Presentation/WorldArt/WorldBoundaryPolicy.cs.meta`
Then delete the whole `BoundaryProgressesFromClosedToBrokenToOpen` method from `WorldPixelRevisionTests.cs`; spec 4.3 says the ring tests replace it.

- [ ] **Step 6: Update `WorldPresentationPlayModeTests.cs`.**
  - Replace the body of `GeometryHasNoPhysicalCollisionAndUsesLogicalFloorBounds` from `var floor = …` onward with:
    ```csharp
            var floor = artObject.transform.Find("Environment/Floor");
            Assert.That(floor, Is.Not.Null);
            // One painted quad lies exactly on the gameplay plane and covers the bounds once.
            Assert.That(floor.position.y, Is.EqualTo(0).Within(0.001));
            Assert.That(floor.localScale.x, Is.EqualTo(root.config.arena.bounds.width).Within(.001));
            Assert.That(floor.localScale.y, Is.EqualTo(root.config.arena.bounds.height).Within(.001));
    ```
  - In the morph test, replace the `ContinuousWorldGround` block (the comment and `var plane` lines through the closing brace of the corner loop) with:
    ```csharp
            // Every viewport corner meets the outer ground; no clear colour can leak through.
            var plane = new Plane(Vector3.up, new Vector3(0, -.02f, 0));
            Transform ground = null;
            foreach (Transform child in artObject.transform)
                if (child.name == "Environment") ground = child.Find("Outer ground"); // the newest arena is last
            Assert.That(ground, Is.Not.Null);
            foreach (float x in new[] { 0f, 1f }) foreach (float y in new[] { 0f, 1f })
            {
                var ray = camera.ViewportPointToRay(new Vector3(x, y));
                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                var point = ray.GetPoint(distance);
                Assert.That(Mathf.Abs(point.x - ground.position.x), Is.LessThan(ground.localScale.x / 2));
                Assert.That(Mathf.Abs(point.z - ground.position.z), Is.LessThan(ground.localScale.y / 2));
            }
    ```
  - In `SanctumPullPausesForSafetyThenLandsAndResumesCombat`, delete `float departureNorth = root.Sim.Arena.bounds.yMax;` and the three-line `_NorthLimit` assertion.
  - In `InterruptedMorphDoesNotResurrectPartiallyFormedScenery`, replace the four lines from `Renderer incoming = null;` through `Assert.That(incoming, Is.Not.Null);` with:
    ```csharp
            Transform environment = null;
            foreach (Transform child in artObject.transform)
                if (child.name == "Environment") environment = child; // the incoming arena is the newest
            var incoming = environment.Find("Dressing").GetComponentInChildren<Renderer>();
            Assert.That(incoming, Is.Not.Null);
    ```
  - In `FloorAndWallsOverlapDifferentAtlasTexturesWhileCoverKeepsItsShape`, change `if (renderer.name.StartsWith("Boundary")) wall = renderer;` to `if (renderer.name == ArenaEnclosure.RibbonName) wall = renderer;`, and change `Is.EqualTo("BorrowedHex/PixelWorld")` to `Is.EqualTo(PaintedMaterials.ShaderName)`.
  - In the Sanctum reveal test, change `.Find("Environment/CoverTrim2/Obelisk").GetComponent<Renderer>()` to `.Find("Environment/CoverTrim2/Obelisk").GetComponentInChildren<Renderer>()`; the renderer now sits on the model's child mesh.

- [ ] **Step 7: Run both suites and watch them pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh both t10`
Expected: EditMode and PlayMode summaries with 0 failures, and no `NOT PASSED`.

- [ ] **Step 8: Commit.**
  - Stage `WorldGeometry.cs`, `WorldPresentation.cs`, the three deletions (already staged by `git rm`), `WorldPixelRevisionTests.cs`, `WorldPresentationPlayModeTests.cs` and `ArenaRedesignTests.cs`.
  - Check `git diff --cached --name-only`: it must list exactly those paths.
  - Message: `Build arenas from painted floors, kit dressing and kit cover`.

---

### Task 11: Night lighting rig

**Files:**
- Create: `Assets/Game/Scripts/Presentation/WorldArt/WorldLightingPolicy.cs`
- Create: `Assets/Game/Scripts/Presentation/WorldArt/FireLights.cs`
- Modify: `Assets/Game/Scripts/Presentation/WorldArt/WorldPresentation.cs`
- Modify: `Assets/Game/Tests/EditMode/ArenaRedesignTests.cs`

**Interfaces:**
- Consumes: `WorldGeometry.FlameAnchor`, `WorldArtLibrary.Release`, and the `_WorldAmbient` global from the Task 2 shader.
- Produces: `struct WorldLighting { Color Ambient, Fog, Moon, Fire; float MoonIntensity; }`, `WorldLightingPolicy.For(string theme) : WorldLighting` (unknown → Courtyard) and `WorldLightingPolicy.MoonRotation`.
- Produces: `FireLights(Transform parent, ArenaLayout arena, Color color) : IDisposable`, with `Lights` (8 point lights), `Render(float unscaledTime, float fade)` and the constants `Count`, `Range` and `Intensity`.

- [ ] **Step 1: Append the failing tests.**

```csharp
        [Test]
        public void UnknownThemeFallsBackToCourtyard()
        {
            var courtyard = WorldLightingPolicy.For("Courtyard");
            foreach (var odd in new[] { "Tutorial", "", null })
            {
                var lighting = WorldLightingPolicy.For(odd);
                Assert.That(lighting.Ambient, Is.EqualTo(courtyard.Ambient)); Assert.That(lighting.Fog, Is.EqualTo(courtyard.Fog));
                Assert.That(lighting.Moon, Is.EqualTo(courtyard.Moon)); Assert.That(lighting.MoonIntensity, Is.EqualTo(courtyard.MoonIntensity));
                Assert.That(lighting.Fire, Is.EqualTo(courtyard.Fire));
            }
        }

        [Test]
        public void EveryThemeIsANightRig()
        {
            foreach (var theme in Themes)
            {
                var lighting = WorldLightingPolicy.For(theme);
                Assert.That(lighting.Ambient.maxColorComponent, Is.LessThanOrEqualTo(.15f), theme);
                Assert.That(lighting.Fog.maxColorComponent, Is.LessThanOrEqualTo(.06f), theme);
                Assert.That(lighting.MoonIntensity, Is.InRange(.2f, .5f), theme);
            }
        }

        [Test]
        public void RevealFadeScalesFireLights()
        {
            var parent = new GameObject("probe").transform;
            var fire = new FireLights(parent, WorldArenaLayouts.Create(3), Color.white);
            try
            {
                Assert.That(fire.Lights.Count, Is.EqualTo(FireLights.Count));
                // The Sanctum starts dark: a zero fade means zero light, whatever the flicker.
                fire.Render(12.3f, 0);
                foreach (var light in fire.Lights) Assert.That(light.intensity, Is.EqualTo(0));
                fire.Render(12.3f, .5f);
                var half = fire.Lights.Select(l => l.intensity).ToArray();
                foreach (float value in half) Assert.That(value, Is.InRange(1e-4f, FireLights.Intensity * .5f * 1.2f + 1e-4f));
                // Same clock, double fade: exactly double, so a held (paused) reveal holds its light.
                fire.Render(12.3f, 1);
                for (int i = 0; i < half.Length; i++) Assert.That(fire.Lights[i].intensity, Is.EqualTo(half[i] * 2).Within(1e-4f));
                foreach (var light in fire.Lights) { Assert.That(light.type, Is.EqualTo(LightType.Point)); Assert.That(light.range, Is.EqualTo(FireLights.Range)); }
            }
            finally { fire.Dispose(); Object.DestroyImmediate(parent.gameObject); }
        }
```

- [ ] **Step 2: Run the gate and watch it fail.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh edit t11red`
Expected: `COMPILE NOT CLEAN - stopping` (`WorldLightingPolicy` and `FireLights` do not exist).

- [ ] **Step 3: Write `WorldLightingPolicy.cs`.**

```csharp
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    public readonly struct WorldLighting
    {
        public readonly Color Ambient, Fog, Moon, Fire;
        public readonly float MoonIntensity;
        public WorldLighting(Color ambient, Color fog, Color moon, float moonIntensity, Color fire)
        { Ambient = ambient; Fog = fog; Moon = moon; MoonIntensity = moonIntensity; Fire = fire; }
    }

    /// <summary>
    /// The per-theme night rig (spec 2.4): very dark ambient, near-black fog, a cold low moon
    /// and fire lights at the flame anchors. Unknown themes (tutorial and non-world arenas,
    /// via WorldArtPolicy.Theme) get the Courtyard rig rather than black.
    /// </summary>
    public static class WorldLightingPolicy
    {
        public static readonly Color Warm = new Color(1, .62f, .32f), Cold = new Color(.45f, .65f, 1);
        // Pitched 50 degrees and aimed back toward the camera, so props cast readable shadows on
        // the camera's side of the floor.
        public static Quaternion MoonRotation => Quaternion.Euler(50, 160, 0);

        public static WorldLighting For(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return new WorldLighting(new Color(.08f, .10f, .10f), new Color(.02f, .035f, .03f), new Color(.5f, .62f, .75f), .4f, Warm);
                case "Cave": return new WorldLighting(new Color(.09f, .08f, .13f), new Color(.03f, .02f, .05f), new Color(.45f, .45f, .75f), .3f, Cold);
                // Cold to match the Sanctum's existing blue pillar flames (inference; owner may retune).
                case "Sanctum": return new WorldLighting(new Color(.07f, .06f, .10f), new Color(.025f, .015f, .04f), new Color(.55f, .45f, .8f), .35f, Cold);
                default: return new WorldLighting(new Color(.10f, .10f, .14f), new Color(.03f, .03f, .05f), new Color(.55f, .6f, .85f), .45f, Warm);
            }
        }
    }
}
```

- [ ] **Step 4: Write `FireLights.cs`.**

```csharp
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
```

- [ ] **Step 5: Wire the rig into `WorldPresentation.cs`.**
  - **New fields.** Next to the `sun` field, add:
    ```csharp
        // Night rig state. moonIntensity is the reveal's target; fireFade is 0 while the
        // Sanctum is dark and rises with the reveal.
        FireLights fireLights;
        float moonIntensity = 1.15f, fireFade = 1;
        Quaternion originalSunRotation; LightShadows originalShadows;
    ```
  - **In `Start`.** Change `if (sun != null) { originalSun = sun.color; originalIntensity = sun.intensity; }` to:
    ```csharp
            if (sun != null)
            {
                originalSun = sun.color; originalIntensity = sun.intensity;
                originalSunRotation = sun.transform.rotation; originalShadows = sun.shadows;
            }
    ```
  - **Replace the top of `ApplyTheme`.** Replace everything before `if (travel.Active) return;` (the `tint` line, the ambient/fog line, the camera background line and the sun line) with:
    ```csharp
            geometry.SetTheme(name); effects.Clear();
            var lighting = WorldLightingPolicy.For(name);
            RenderSettings.ambientLight = lighting.Ambient;
            // PaintedWorld reads ambient from this global, not from the SH probe.
            Shader.SetGlobalColor("_WorldAmbient", lighting.Ambient);
            RenderSettings.fogColor = lighting.Fog;
            if (camera != null) camera.backgroundColor = lighting.Fog;
            moonIntensity = lighting.MoonIntensity;
            if (sun != null)
            {
                sun.color = lighting.Moon; sun.intensity = lighting.MoonIntensity;
                sun.transform.rotation = WorldLightingPolicy.MoonRotation; sun.shadows = LightShadows.Soft;
            }
            fireLights?.Dispose();
            fireLights = new FireLights(transform, sim.Arena, lighting.Fire);
    ```
  - **In the Sanctum reveal branch of `RenderPullAndCamera`.** Change `if (sun != null) sun.intensity = Mathf.Lerp(0, 1.15f, opening);` to:
    ```csharp
                    if (sun != null) sun.intensity = Mathf.Lerp(0, moonIntensity, opening);
                    fireFade = opening;
    ```
  - **In `ChangeArena`'s travel branch.** Right after `travel.Begin(true); sim.Clock.SetPauseReason(PauseReason.WorldTransition, true);`, add `fireFade = sim.ArenaStage == 3 ? 0 : 1; // the Sanctum starts dark`.
  - **When travel ends.** In the `if (!travel.Active)` block of `RenderPullAndCamera`, add `fireFade = 1;` before `ApplyTheme(sim.Arena.worldTheme);`. In `Rebind`, add `fireFade = 1;` after `travel.Reset();`.
  - **At the end of `LateUpdate`.** After `terminalEffects.Render(sim.Clock.Now + terminalElapsed, camera);`, add `fireLights?.Render(Time.unscaledTime, fireFade);`.
  - **In `OnDestroy`.**
    - Change `if (sun != null) { sun.color = originalSun; sun.intensity = originalIntensity; }` to also set `sun.transform.rotation = originalSunRotation; sun.shadows = originalShadows;` inside the braces.
    - After `RenderSettings.ambientLight = originalAmbient;`, add `Shader.SetGlobalColor("_WorldAmbient", originalAmbient);`.
    - Change `geometry?.Dispose(); introOverlay.Dispose();` to `geometry?.Dispose(); fireLights?.Dispose(); introOverlay.Dispose();`.

- [ ] **Step 6: Run both suites and watch them pass.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh both t11`
Expected: EditMode and PlayMode both show 0 failures. The existing Sanctum reveal PlayMode test still passes; the reveal's timing and pause behaviour are untouched.

- [ ] **Step 7: Commit.** Stage `WorldLightingPolicy.cs(.meta)`, `FireLights.cs(.meta)`, `WorldPresentation.cs` and `ArenaRedesignTests.cs`. Message: `Light the arenas with a moon, dark ambient and flickering fire`.

---

### Task 12: No-void and morph-cost tests, then visual sign-off

**Files:**
- Create: `Assets/Game/Tests/PlayMode/ArenaVisibilityPlayModeTests.cs`

**Interfaces:**
- Consumes: `WorldGeometry`, `WorldArtLibrary`, `WorldCameraPolicy.ClampFocus/Offset/Rotation`, `ArenaEnclosure.RibbonName` and `WorldArenaLayouts.Create`.

- [ ] **Step 1: Write the tests.** Everything they test already exists, so they should pass on the first run. If either fails, that is a real defect: debug it with superpowers:systematic-debugging and do not loosen the test.

```csharp
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
    /// 21:9, every ray meets arena geometry within 60 units. Only the layer that never
    /// dissolves (floor, outer ground, ribbon) gets colliders, so the property holds even
    /// with every dressing prop mid-dissolve.
    /// </summary>
    public class ArenaVisibilityPlayModeTests
    {
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
                                    Assert.That(Physics.Raycast(ray, 60), Is.True,
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
```

- [ ] **Step 2: Run the full gate.**

Run: `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/gate.sh both t12`
Expected: EditMode and PlayMode both show 0 failures, including `NoVoidVisible` and `BeginMorphFromACompletedArenaIsCheap`.

- [ ] **Step 3: Commit.** Stage `Assets/Game/Tests/PlayMode/ArenaVisibilityPlayModeTests.cs(.meta)`. Message: `Prove no void is visible from any camera extreme`.

- [ ] **Step 4: Capture one screenshot per arena.** Run each command and check that it succeeds before running the next.
  1. Open the scene and enter play mode: `unity command eval --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Arena.unity"); UnityEditor.EditorApplication.isPlaying = true; return 1;'`. Poll `unity command eval --code 'return UnityEditor.EditorApplication.isPlaying;'` until it returns `true`.
  2. Start a world run: `unity command eval --code 'var r = UnityEngine.Object.FindFirstObjectByType<BorrowedHex.Presentation.GameRoot>(); r.useWorldArenas = true; r.PlaySandbox(); r.Sim.AutoSpawn = false; return 1;'`. Wait 2 s, then run `& "C:\Program Files\Git\bin\bash.exe" /c/Users/Rachit/BorrowedHex/.superpowers/cap.sh arena-courtyard`.
  3. For stages 1 and 2, unpause (cap.sh steps one frame and leaves the editor paused) and select the stage:
     ```
     unity command eval --code 'UnityEditor.EditorApplication.isPaused = false; var r = UnityEngine.Object.FindFirstObjectByType<BorrowedHex.Presentation.GameRoot>(); var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic; typeof(BorrowedHex.Runs.ArenaSim).GetMethod("SelectWorldArena", f).Invoke(r.Sim, new object[] { 1 }); typeof(BorrowedHex.Runs.ArenaSim).GetMethod("RestorePillars", f).Invoke(r.Sim, null); return 1;'
     ```
     The morph lasts 24 s of game time (`ArenaSim.WorldMorphDuration`), so wait 26 s (`Start-Sleep 26`), then run `cap.sh arena-graveyard`. Repeat with `{ 2 }` for `arena-cave`.
  4. Unpause, then run `r.Sim.SpawnBoss();` through the same eval pattern. Poll `return r.Sim.State.ToString();` until it is `BossCombat`, then run `cap.sh arena-sanctum`.
  5. Spec 4.4 sets a 60 fps budget, so check the frame time while in the Sanctum: `unity command eval --code 'UnityEditor.EditorApplication.isPaused = false; return UnityEngine.Time.smoothDeltaTime * 1000;'`. This is indicative only, because the editor adds overhead. Ledger the value; above 16.7 ms is a finding to report, not a test failure.
  6. Exit play mode: `unity command eval --code 'UnityEditor.EditorApplication.isPlaying = false; return 1;'`.

- [ ] **Step 5: Owner sign-off.** Read all four `.superpowers/shots/arena-*.png` files yourself first. Then show them to the owner: restart the brainstorming visual companion if it is wanted, or use SendUserFile. Ask for a verdict per arena.
  - Look specifically for: no grey void anywhere; the north statement wall; low south edges; readable cover; door and hero orientation (`ArenaDressing.HeroYaw`); and ribbon faces not culled away.
  - The work is not done until the owner approves. Tuning requests are new edits that follow the same TDD and commit pattern: recipes in `ArenaDressing`, floor numbers in `PaintedFloorGenerator` plus a re-bake, and the light table in `WorldLightingPolicy`.
