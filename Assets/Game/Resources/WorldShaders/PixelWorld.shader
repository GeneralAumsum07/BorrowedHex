Shader "BorrowedHex/PixelWorld"
{
    Properties
    {
        _BaseMap("Pixel art", 2D) = "white" {}
        _FromMap("Previous world", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Morph("World morph", Range(0,1)) = 1
        _Visible("Pixel coverage", Range(0,1)) = 1
        _Retiring("Retiring coverage", Float) = 0
        _GlitchTime("Gameplay time", Float) = 0
        _Glitches("Glitch bands", Float) = 1
        _Reveal("Room light", Range(0,1)) = 1
        _Emission("Rune light", Color) = (0,0,0,0)
        _NorthLimit("Terrain horizon", Float) = 100000
        _FogAmount("Atmospheric depth", Range(0,1)) = 1
        _Occlusion("Actor visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FromMap); SAMPLER(sampler_FromMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _Emission;
            float _Morph, _Visible, _Retiring, _GlitchTime, _Glitches, _Reveal, _NorthLimit, _FogAmount, _Occlusion;
            CBUFFER_END
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float shade : TEXCOORD2; float fog : TEXCOORD3; float4 color : COLOR; };
            float Hash(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
            Output Vert(Input input)
            {
                Output o; o.position = TransformObjectToHClip(input.vertex.xyz);
                o.world = TransformObjectToWorld(input.vertex.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap); o.color = input.color;
                float3 normal = TransformObjectToWorldNormal(input.normal);
                // Quantized directional shading keeps solid 3D faces readable without
                // smooth specular surfaces that fight the downloaded pixel artwork.
                o.shade = floor((.8 + .2 * abs(normal.y)) * 5) / 5;
                o.fog = ComputeFogFactor(o.position.z); return o;
            }
            half4 Frag(Output input) : SV_Target
            {
                // The apron covers side/near edges but ends at the authored skyline.
                // Otherwise the enormous safety floor occludes the pixel backgrounds.
                clip(_NorthLimit - input.world.z);
                float grain = Hash(floor(input.world * 12));
                float visible = _Visible * _Occlusion;
                float coverage = _Retiring > .5 ? grain - (1 - visible) : visible - grain;
                clip(coverage - .00001);
                float patch = Hash(float3(floor(input.uv * 18), 5));
                float mix = saturate(_Morph * 1.65 - patch * .65);
                float2 incoming = input.uv;
                // Sparse moving bands expose the incoming texture over the outgoing one.
                // Both morph and glitch clocks are supplied by gameplay time, so pause
                // never leaves scenery shifting behind a frozen fight.
                if (_Glitches > .5 && _Morph > .02 && _Morph < .98 &&
                    fmod(floor(input.uv.y * 96) + floor(_GlitchTime * 6) * 3, 53) < 1)
                { incoming.x += .075; mix = saturate(mix + .4); }
                half4 old = SAMPLE_TEXTURE2D(_FromMap, sampler_FromMap, input.uv);
                half4 next = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, incoming);
                half4 color = lerp(old, next, mix) * _BaseColor * input.color;
                clip(color.a - .05);
                color.rgb = color.rgb * input.shade * _Reveal + _Emission.rgb;
                color.rgb = lerp(color.rgb, MixFog(color.rgb, input.fog), _FogAmount);
                return color;
            }
            ENDHLSL
        }
    }
}
