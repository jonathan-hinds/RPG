Shader "RPG Clone/Targeting/Selection Indicator Unlit"
{
    Properties
    {
        _BaseMap ("Alpha Mask", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 4)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        [Toggle] _RadialOrb ("Procedural Radial Orb", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "SelectionIndicator"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _Opacity;
                float _Intensity;
                float _ZTest;
                float _RadialOrb;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half textureMask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                half radialDistance = length((input.uv - 0.5h) * 2.0h);
                half radialGlow = saturate(1.0h - radialDistance);
                radialGlow *= radialGlow;
                half radialCore = saturate(1.0h - radialDistance * 3.4h);
                half orbMask = saturate(radialGlow * 0.82h + radialCore * 0.78h);
                half mask = lerp(textureMask, orbMask, saturate(_RadialOrb));
                half alpha = saturate(mask * _Tint.a * _Opacity);
                clip(alpha - 0.002h);
                return half4(_Tint.rgb * _Intensity * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
