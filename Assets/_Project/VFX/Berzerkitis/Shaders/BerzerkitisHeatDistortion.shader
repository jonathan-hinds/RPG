Shader "RPG Clone/VFX/Berzerkitis Heat Distortion"
{
    Properties
    {
        [MainTexture] _BaseMap("Distortion Mask", 2D) = "white" {}
        _NoiseMap("Distortion Noise", 2D) = "gray" {}
        [MainColor] _Tint("Tint", Color) = (1,0.35,0.08,1)
        _Opacity("Opacity", Range(0,1)) = 0.35
        _Brightness("Brightness", Range(0,4)) = 1
        _ScrollSpeed("UV Scroll", Vector) = (0.05,0.4,0,0)
        _DistortionStrength("Scene Distortion", Range(0,0.1)) = 0.025
        _Dissolve("Dissolve", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "BerzerkitisHeatDistortion"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float4 _ScrollSpeed;
                float _Opacity;
                float _Brightness;
                float _DistortionStrength;
                float _Dissolve;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 scrollingUv = input.uv + _Time.y * _ScrollSpeed.xy;
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, scrollingUv);
                half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, scrollingUv * 1.7).rg * 2.0h - 1.0h;
                float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                half3 scene = SampleSceneColor(screenUv + noise * _DistortionStrength * mask.a);
                half breakup = smoothstep(_Dissolve, _Dissolve + 0.08h, mask.a);
                half3 heated = lerp(scene, scene + _Tint.rgb * 0.08h * _Brightness, 0.35h);
                return half4(heated, mask.a * breakup * _Opacity);
            }
            ENDHLSL
        }
    }
}
