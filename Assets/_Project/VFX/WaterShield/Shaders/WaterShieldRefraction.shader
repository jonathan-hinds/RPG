Shader "RPG Clone/VFX/Water Shield Refraction"
{
    Properties
    {
        [MainTexture] _BaseMap("Water Mask", 2D) = "white" {}
        _DistortionMap("Distortion Flow", 2D) = "gray" {}
        [MainColor] _Tint("Water Tint", Color) = (0.2,0.8,1,1)
        _SecondaryTint("Secondary Tint", Color) = (0.5,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 0.3
        _Brightness("Tint Brightness", Range(0,4)) = 1
        _ScrollSpeed("Mask Scroll", Vector) = (0.08,0.11,0,0)
        _SecondaryScrollSpeed("Unused", Vector) = (0,0,0,0)
        _DistortionScrollSpeed("Distortion Scroll", Vector) = (0.09,0.13,0,0)
        _DistortionStrength("Scene Refraction", Range(0,0.12)) = 0.025
        _WobbleAmount("Vertex Wobble", Range(0,0.2)) = 0.035
        _WobbleSpeed("Wobble Speed", Range(0,12)) = 2.4
        _Dissolve("Dissolve", Range(0,1)) = 0
        _PulseAmount("Pulse", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "WaterShieldRefraction"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DistortionMap_ST;
                float4 _Tint;
                float4 _SecondaryTint;
                float4 _ScrollSpeed;
                float4 _SecondaryScrollSpeed;
                float4 _DistortionScrollSpeed;
                float _Opacity;
                float _Brightness;
                float _DistortionStrength;
                float _WobbleAmount;
                float _WobbleSpeed;
                float _Dissolve;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float wave = sin(dot(input.positionOS.xyz, float3(5.1, 4.3, 6.2)) + _Time.y * _WobbleSpeed);
                float3 displaced = input.positionOS.xyz + input.normalOS * wave * _WobbleAmount;
                output.positionCS = TransformObjectToHClip(displaced);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 maskUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy;
                float2 noiseUv = input.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw + _Time.y * _DistortionScrollSpeed.xy;
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, maskUv);
                half2 noise = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, noiseUv).rg * 2.0h - 1.0h;
                float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                half3 scene = SampleSceneColor(screenUv + noise * _DistortionStrength * mask.r);
                half breakup = smoothstep(_Dissolve, _Dissolve + 0.08h, max(mask.a, mask.r));
                half3 tinted = lerp(scene, scene + _Tint.rgb * 0.12h * _Brightness, 0.42h);
                return half4(tinted, max(mask.a, mask.r * 0.65h) * breakup * _Opacity);
            }
            ENDHLSL
        }
    }
}
