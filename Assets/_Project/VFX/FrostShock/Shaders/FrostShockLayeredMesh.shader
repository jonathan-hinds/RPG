Shader "RPG Clone/VFX/Frost Shock Layered Mesh"
{
    Properties
    {
        _BaseMap ("Painted Frost Atlas", 2D) = "white" {}
        _NoiseMap ("Breakup Noise", 2D) = "gray" {}
        [HDR] _Tint ("Body Tint", Color) = (0.08, 0.48, 1, 1)
        [HDR] _HotTint ("Highlight Tint", Color) = (0.7, 1.4, 1.8, 1)
        _AtlasRect ("Atlas Rect", Vector) = (1, 1, 0, 0)
        _Scroll ("UV Scroll", Vector) = (0, 0, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 8)) = 1
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Dissolve Edge", Range(0.001, 0.3)) = 0.08
        _DistortionStrength ("UV Distortion", Range(0, 0.2)) = 0.03
        _FresnelStrength ("Edge Highlight", Range(0, 3)) = 0.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        [Toggle] _ZWrite ("Depth Write", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "FrostShockMesh"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float4 _HotTint;
                float4 _AtlasRect;
                float4 _Scroll;
                float _Opacity;
                float _Brightness;
                float _Dissolve;
                float _EdgeSoftness;
                float _DistortionStrength;
                float _FresnelStrength;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 localUv = frac(input.uv + _Scroll.xy * _Time.y);
                half2 noiseOffset = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(input.uv * 1.73 + _Scroll.zw * _Time.y)).rg - 0.5h;
                localUv = frac(localUv + noiseOffset * _DistortionStrength);
                float2 atlasUv = localUv * _AtlasRect.xy + _AtlasRect.zw;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv);
                half breakup = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(input.uv * 2.31 - _Scroll.yx * _Time.y)).r;
                half luminance = dot(painted.rgb, half3(0.299h, 0.587h, 0.114h));
                half dissolveMask = smoothstep(_Dissolve, _Dissolve + _EdgeSoftness, painted.a * 0.76h + breakup * 0.24h);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalize(input.normalWS), viewDir))), 2.1h);
                half hot = smoothstep(0.5h, 0.94h, luminance);
                half3 color = painted.rgb * lerp(_Tint.rgb, _HotTint.rgb, hot);
                color += _HotTint.rgb * fresnel * _FresnelStrength * painted.a;
                color *= _Brightness * input.color.rgb;
                half alpha = painted.a * dissolveMask * _Tint.a * input.color.a * _Opacity;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
