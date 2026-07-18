Shader "RPG Clone/VFX/Berzerkitis Sprite Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _NoiseMap("Breakup Noise", 2D) = "white" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _Brightness("Emission Brightness", Range(0,8)) = 1
        _ScrollSpeed("UV Scroll", Vector) = (0,0,0,0)
        _NoiseScrollSpeed("Noise Scroll", Vector) = (0.11,0.23,0,0)
        _DistortionStrength("UV Distortion", Range(0,0.2)) = 0
        _Dissolve("Dissolve", Range(0,1)) = 0
        _EdgeSoftness("Dissolve Softness", Range(0.001,0.25)) = 0.06
        _PulseFrequency("Opacity Pulse Frequency", Range(0,20)) = 0
        _PulseAmount("Opacity Pulse Amount", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BerzerkitisSprite"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                float4 _Tint;
                float4 _ScrollSpeed;
                float4 _NoiseScrollSpeed;
                float _Opacity;
                float _Brightness;
                float _DistortionStrength;
                float _Dissolve;
                float _EdgeSoftness;
                float _PulseFrequency;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 noiseUv = input.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _Time.y * _NoiseScrollSpeed.xy;
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).r;
                float2 uv = input.uv + _Time.y * _ScrollSpeed.xy + (noise - 0.5h) * _DistortionStrength;
                half4 textureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half dissolveMask = smoothstep(_Dissolve, _Dissolve + max(0.001h, _EdgeSoftness), textureColor.a * noise);
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseFrequency));
                half4 color = textureColor * _Tint * input.color;
                color.rgb *= _Brightness;
                color.a *= _Opacity * dissolveMask * pulse;
                return color;
            }
            ENDHLSL
        }
    }
}
