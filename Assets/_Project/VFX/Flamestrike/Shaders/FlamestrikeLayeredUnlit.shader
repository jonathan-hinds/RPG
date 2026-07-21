Shader "RPG Clone/VFX/Flamestrike Layered Unlit"
{
    Properties
    {
        _BaseMap ("Painted Atlas", 2D) = "white" {}
        _NoiseMap ("Flow Noise", 2D) = "gray" {}
        [HDR] _Tint ("Tint", Color) = (1, 0.35, 0.02, 1)
        [HDR] _HotTint ("Hot Tint", Color) = (1.7, 1.1, 0.35, 1)
        _AtlasRect ("Atlas Rect", Vector) = (1, 1, 0, 0)
        _Scroll ("UV Scroll", Vector) = (0, 0, 0, 0)
        _DistortionStrength ("Distortion", Range(0, 0.2)) = 0.035
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Dissolve Edge", Range(0.001, 0.3)) = 0.08
        _Opacity ("Opacity", Range(0, 1)) = 1
        _FresnelStrength ("Mesh Edge Glow", Range(0, 2)) = 0.35
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FlamestrikeLayer"
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
                float _DistortionStrength;
                float _Dissolve;
                float _EdgeSoftness;
                float _Opacity;
                float _FresnelStrength;
                float _SrcBlend;
                float _DstBlend;
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
                // Keep authored silhouettes clamped inside their atlas cells. Only the
                // flow noise moves; scrolling the painted sprite itself caused hard,
                // square clipping at card and atlas boundaries.
                float2 localUv = saturate(input.uv);
                half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(localUv * 1.7 + _Scroll.xy * 0.31)).rg - 0.5h;
                localUv = saturate(localUv + noise * _DistortionStrength);
                float2 atlasUv = localUv * _AtlasRect.xy + _AtlasRect.zw;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv);
                half value = dot(painted.rgb, half3(0.299h, 0.587h, 0.114h));
                half hot = smoothstep(0.48h, 0.92h, value);
                half dissolveMask = smoothstep(_Dissolve, _Dissolve + _EdgeSoftness, painted.a * (0.72h + value * 0.28h));
                half4 color;
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalize(input.normalWS), viewDirection))), 2.0h);
                color.rgb = painted.rgb * lerp(_Tint.rgb, _HotTint.rgb, hot) * input.color.rgb;
                color.rgb += _HotTint.rgb * fresnel * _FresnelStrength * painted.a;
                color.a = painted.a * dissolveMask * _Tint.a * input.color.a * _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
