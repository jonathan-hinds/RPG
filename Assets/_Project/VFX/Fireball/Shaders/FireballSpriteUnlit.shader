Shader "RPG Clone/VFX/Fireball Sprite Unlit"
{
    Properties
    {
        _BaseMap ("Painted Texture", 2D) = "white" {}
        _NoiseMap ("Flow Noise", 2D) = "gray" {}
        [HDR] _Tint ("Tint", Color) = (1, 0.4, 0.05, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Scroll ("UV Scroll", Vector) = (0, 0, 0, 0)
        _DistortionStrength ("Distortion", Range(0, 0.25)) = 0.04
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
            Name "FireballSprite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _Opacity;
                float4 _Scroll;
                float _DistortionStrength;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 noiseUv = input.uv * 1.65 + (_Scroll.xy * 0.37);
                half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).rg - 0.5h;
                float2 paintedUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Scroll.xy + noise * _DistortionStrength;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, paintedUv);
                half4 color = painted * _Tint * input.color;
                color.a *= _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
