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
