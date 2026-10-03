Shader "BorrowedHex/PaintedWorld"
{
    // Painted, lit world shader. Surfaces (floor, outer ground, ribbon; _Surface = 1) resolve
    // the reality-glitch morph per pixel from the patch-field globals that GlitchField uploads.
    // Objects (cover, rubble, dressing) take their glitch state per renderer: _Fill, _Tear,
    // _Ghost from GlitchMorphPolicy, plus the _Visible wear dissolve.
    Properties
    {
        _BaseMap("Painted texture", 2D) = "white" {}
        _FromMap("Previous world", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Visible("Coverage", Range(0,1)) = 1
        _Fill("Glitch fill", Range(0,1)) = 1
        _Tear("Glitch tear", Range(0,1)) = 0
        _Ghost("Glitch ghost", Range(0,1)) = 0
        _Grain("Legacy grain dissolve", Float) = 0
        _Surface("Per-pixel morph surface", Float) = 0
        _HeightRange("World height range", Vector) = (0,4,0,0)
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
        float4 _BaseMap_ST, _BaseColor, _Emission, _HeightRange;
        float _Visible, _Fill, _Tear, _Ghost, _Grain, _Surface, _Reveal, _FogAmount, _Occlusion;
        CBUFFER_END

        // Patch-field globals (GlitchField.Upload). _PatchSeeds: xy seed, z start time.
        float _PatchActive, _WorldTime, _GlitchStrength;
        float4 _PatchGrid, _PatchDims, _SeamColor;
        float4 _PatchSeeds[32];

        float Hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
        float Hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

        // Smooth value noise. Every glitch motion below is driven by these, interpolated with
        // a smoothstep, never by floor(time * rate): that stepping is what read as cheap.
        float Noise1(float seed, float t)
        {
            float i = floor(t), f = frac(t); f = f * f * (3 - 2 * f);
            return lerp(Hash2(float2(seed, i)), Hash2(float2(seed, i + 1)), f);
        }
        float Noise2(float2 p)
        {
            float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + 1), f.x), f.y);
        }
        float Noise3(float3 p)
        {
            float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            float a = lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y);
            float b = lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + 1), f.x), f.y);
            return lerp(a, b, f.z);
        }

        // Thin horizontal bands (world height and depth, so they run across the screen on both
        // floor and walls). `active` picks which bands tear right now; it drifts smoothly.
        float Band(float3 world) { return floor(world.z * 2.5 + world.y * 2.5); }
        float BandActive(float band) { return smoothstep(.55, .75, Noise1(band * 1.7 + 3, _WorldTime * 3)); }

        // Objects tear sideways in bands while their patch is unstable. Applied identically in
        // every pass so depth, shadows and colour stay aligned.
        float3 Tear(float3 world)
        {
            if (_Tear <= 0 || _GlitchStrength <= 0) return world;
            float band = floor(world.y * 4);
            world.x += (Noise1(band + 11, _WorldTime * 6) - .5) * .35 * _Tear * BandActive(band) * _GlitchStrength;
            return world;
        }

        // Wear, fill, ghost and actor cut-out for objects; returns the glitch glow to add.
        float3 ClipObject(float3 world, bool includeOcclusion, bool includeGhost)
        {
            float visible = _Visible * (includeOcclusion ? _Occlusion : 1);
            if (visible < 1)
            {
                // _Grain keeps the old voxel dissolve for the Sanctum reveal (owner: "keep it
                // as it is"). Wear uses soft noise blobs, not 8 cm cubes blinking out.
                float grain = _Grain > .5 ? Hash(floor(world * 12)) : saturate((Noise3(world * 1.4) - .2) / .6);
                clip(visible - grain - .00001);
            }
            float3 glow = 0;
            float solid = 1;
            if (_Fill < .999)
            {
                // Fill rises from the base when forming and sinks from the top when retiring,
                // along a noisy edge; the 1.25 / .125 margin lets 0 and 1 clear the noise.
                float h = saturate((world.y - _HeightRange.x) / max(.01, _HeightRange.y - _HeightRange.x));
                solid = _Fill * 1.25 - .125 - (h + (Noise3(world * 2.2) - .5) * .25);
                glow += _SeamColor.rgb * smoothstep(.08, 0, abs(solid)) * 1.5 * step(.001, _Fill);
            }
            bool ghostShown = false;
            if (includeGhost && _Ghost > .001)
            {
                // A shimmering scanline preview: bands scroll upward, broken up by drifting noise.
                float bands = step(frac(world.y * 5 - _WorldTime * 1.3), .32 * _Ghost);
                ghostShown = bands * step(.4, Noise3(world * 3 + _WorldTime * .7)) > .5;
                if (ghostShown && solid <= 0) glow += _SeamColor.rgb * .8 * _Ghost;
            }
            if (solid <= 0 && !ghostShown) clip(-1);
            // A torn object flickers with the seam colour in its active bands.
            glow += _SeamColor.rgb * _Tear * _GlitchStrength * BandActive(floor(world.y * 4)) * .35;
            return glow;
        }

        // Nearest seed in the 3x3 cells around p (exact because seeds sit inside their cells).
        float2 PatchAt(float2 p)
        {
            float2 cell = clamp(floor((p - _PatchGrid.xy) / _PatchGrid.zw), 0, _PatchDims.xy - 1);
            float best = 1e9, start = 1e7;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    float2 c = cell + float2(dx, dy);
                    if (c.x < 0 || c.y < 0 || c.x >= _PatchDims.x || c.y >= _PatchDims.y) continue;
                    float4 seed = _PatchSeeds[(int)(c.y * _PatchDims.x + c.x)];
                    float d = distance(p, seed.xy);
                    if (d < best) { best = d; start = seed.z; }
                }
            return float2(start, best);
        }

        half4 SampleSplit(TEXTURE2D_PARAM(map, mapSampler), float2 uv, float split)
        {
            half4 c = SAMPLE_TEXTURE2D(map, mapSampler, uv);
            if (split > 0)
            {
                c.r = SAMPLE_TEXTURE2D(map, mapSampler, uv + float2(split, 0)).r;
                c.b = SAMPLE_TEXTURE2D(map, mapSampler, uv - float2(split, 0)).b;
            }
            return c;
        }

        // Surface colour through the glitch morph. Each pixel finds its patch (with a noise
        // warp so the outlines are organic), then shows: the old world before the patch
        // triggers; an unstable shimmer (sliding slices, colour split, the new world bleeding
        // through); a noisy front growing out from the seed with a glowing seam; the new world.
        half4 WorldColor(float2 uv, float3 world, out float3 seam)
        {
            seam = 0;
            if (_Surface < .5 || _PatchActive < .5) return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            float2 p = world.xz;
            float2 lo = _PatchGrid.xy, hi = _PatchGrid.xy + _PatchGrid.zw * _PatchDims.xy;
            // Clamped to the footprint: the outer ground and walls follow their nearest edge
            // patch instead of growing out to 80 m away.
            float2 warped = clamp(p + (float2(Noise2(p * .18), Noise2(p * .18 + 17.3)) - .5) * 3, lo, hi);
            float2 patch = PatchAt(warped);
            float age = _WorldTime - patch.x;
            float unstable = _PatchDims.z, grow = _PatchDims.w;
            if (age < 0) return SAMPLE_TEXTURE2D(_FromMap, sampler_FromMap, uv) * _BaseColor;
            if (age >= unstable + grow) return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

            float growth = saturate((age - unstable) / grow);
            // Ease-out: the crack bursts open, then slows as it reaches the patch outline. Any
            // pixel of a Voronoi cell lies within one grid-cell diagonal of its seed.
            float radius = length(_PatchGrid.zw) * 1.15 * (1 - (1 - growth) * (1 - growth));
            float front = patch.y + (Noise2(p * .9) - .5) * 1.6;
            float mixNew = saturate((radius - front) / .35);

            float strength = _GlitchStrength;
            float g = smoothstep(0, .2, age) * (1 - smoothstep(unstable, unstable + grow * .8, age)) * strength;
            float band = Band(world), active = BandActive(band);
            float2 slid = uv + float2((Noise1(band, _WorldTime * 5) - .5) * .05 * g * active, 0);
            float split = .004 * g;
            mixNew = max(mixNew, g * active * smoothstep(.6, .8, Noise1(band + 9.1, _WorldTime * 7)));
            half4 before = SampleSplit(TEXTURE2D_ARGS(_FromMap, sampler_FromMap), slid, split);
            half4 after = SampleSplit(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), slid, split);

            // The seam rides the front and fades as the growth settles; Reduce Flashes keeps a
            // softer seam and drops the tearing entirely (strength 0 above).
            float ring = exp(-abs(front - radius) / .18) * step(.001, growth) * (1 - smoothstep(.75, 1, growth));
            seam = _SeamColor.rgb * (ring * 1.4 * lerp(.35, 1, strength) + g * active * .25);
            return lerp(before, after, mixNew) * _BaseColor;
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
                o.world = Tear(TransformObjectToWorld(input.vertex.xyz));
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
                float3 glow = ClipObject(input.world, true, true);
                float3 seam;
                half4 color = WorldColor(input.uv, input.world, seam) * input.color;
                glow += seam;
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
                // The glitch light is emissive (it is the tear in reality, not lit matter) but
                // still fogs, so far patches do not glow through the night.
                color.rgb = color.rgb * light * _Reveal + _Emission.rgb + glow;
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
                float3 world = Tear(TransformObjectToWorld(input.vertex.xyz));
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
                // A dissolving prop must not cast a solid shadow (spec 2.1); a ghost casts none.
                ClipObject(input.world, false, false);
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
                DepthOutput o; o.world = Tear(TransformObjectToWorld(input.vertex.xyz));
                o.position = TransformWorldToHClip(o.world); o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normal = TransformObjectToWorldNormal(input.normal); return o;
            }
            half DepthFrag(DepthOutput input) : SV_Target
            {
                // Ghosts stay in the depth prepass so depth priming can never hide them.
                ClipObject(input.world, true, true);
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
                DepthOutput o; o.world = Tear(TransformObjectToWorld(input.vertex.xyz));
                o.position = TransformWorldToHClip(o.world); o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normal = TransformObjectToWorldNormal(input.normal); return o;
            }
            half4 NormalFrag(DepthOutput input) : SV_Target
            {
                // Ghosts stay in the depth prepass so depth priming can never hide them.
                ClipObject(input.world, true, true);
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
