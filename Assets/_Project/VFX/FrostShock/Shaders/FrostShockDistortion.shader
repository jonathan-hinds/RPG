Shader "RPG Clone/VFX/Frost Shock Distortion"
{
    Properties
    {
        _NoiseMap ("Distortion Noise", 2D) = "gray" {}
        _Tint ("Cold Tint", Color) = (0.45, 0.85, 1, 0.15)
        _Strength ("Distortion Strength", Range(0, 0.08)) = 0.018
        _Opacity ("Opacity", Range(0, 1)) = 0.4
        _Scroll ("Scroll", Vector) = (0.07, -0.12, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+40" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "FrostDistortion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float4 _Tint;
                float4 _Scroll;
                float _Strength;
                float _Opacity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 noiseUv = frac(input.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _Scroll.xy * _Time.y);
                half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).rg - 0.5h;
                float2 screenUv = input.screenPos.xy / input.screenPos.w + noise * _Strength;
                half3 scene = SampleSceneColor(screenUv);
                return half4(lerp(scene, scene + _Tint.rgb * 0.12h, _Opacity), _Tint.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
