Shader "RPG Clone/VFX/Healing Beam Unlit"
{
    Properties
    {
        _BaseMap ("Ribbon", 2D) = "white" {}
        _NoiseMap ("Noise", 2D) = "gray" {}
        [HDR] _Tint ("Tint", Color) = (1, 0.8, 0.3, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _ScrollOffset ("Scroll Offset", Float) = 0
        _Tiling ("Distance Tiling", Float) = 1
        _DistortionStrength ("Distortion Strength", Range(0, 0.25)) = 0.04
        _PulseProgress ("Pulse Progress", Float) = -10
        _PulseWidth ("Pulse Width", Range(0.01, 0.5)) = 0.15
        _PulseBrightness ("Pulse Brightness", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HealingBeam"

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Opacity;
                float _ScrollOffset;
                float _Tiling;
                float _DistortionStrength;
                float _PulseProgress;
                float _PulseWidth;
                float _PulseBrightness;
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
                float2 noiseUv = float2(input.uv.x * (_Tiling * 0.45) - (_ScrollOffset * 0.37), input.uv.y * 1.6);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).r;
                float2 ribbonUv = float2(input.uv.x * _Tiling + _ScrollOffset, input.uv.y + ((noise - 0.5) * _DistortionStrength));
                half4 ribbon = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ribbonUv);
                half endpointFade = smoothstep(0.0, 0.08, input.uv.x) * (1.0 - smoothstep(0.92, 1.0, input.uv.x));
                half pulseDistance = abs(input.uv.x - _PulseProgress);
                half pulse = 1.0 - smoothstep(_PulseWidth * 0.35, _PulseWidth, pulseDistance);
                half brightness = 1.0 + (pulse * _PulseBrightness);
                half alpha = ribbon.a * _Tint.a * input.color.a * endpointFade * _Opacity;
                half3 color = ribbon.rgb * _Tint.rgb * input.color.rgb * brightness;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
