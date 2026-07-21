Shader "RPG Clone/VFX/Thunder Clap Layered Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _NoiseMap("Breakup Noise", 2D) = "white" {}
        [MainColor][HDR] _Tint("Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _Brightness("Emission Brightness", Range(0,8)) = 1
        _ScrollSpeed("UV Scroll", Vector) = (0,0,0,0)
        _NoiseScrollSpeed("Noise Scroll", Vector) = (0.07,-0.05,0,0)
        _NoiseStrength("Edge Breakup", Range(0,1)) = 0
        _Dissolve("Dissolve", Range(0,1)) = 0
        _EdgeSoftness("Dissolve Softness", Range(0.001,0.3)) = 0.08
        _FlickerSpeed("Flicker Speed", Range(0,40)) = 0
        _FlickerAmount("Flicker Amount", Range(0,1)) = 0
        _PulseSpeed("Opacity Pulse Speed", Range(0,30)) = 0
        _PulseAmount("Opacity Pulse Amount", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ThunderClapLayeredUnlit"
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                half4 _Tint;
                float4 _ScrollSpeed;
                float4 _NoiseScrollSpeed;
                half _Opacity;
                half _Brightness;
                half _NoiseStrength;
                half _Dissolve;
                half _EdgeSoftness;
                half _FlickerSpeed;
                half _FlickerAmount;
                half _PulseSpeed;
                half _PulseAmount;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _ScrollSpeed.xy * _Time.y;
                float2 noiseUv = input.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _NoiseScrollSpeed.xy * _Time.y;
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).r;
                half breakup = lerp(1.0h, noise, _NoiseStrength);
                half dissolve = smoothstep(_Dissolve, _Dissolve + max(0.001h, _EdgeSoftness), sample.a * breakup);
                half flicker = 1.0h - _FlickerAmount + _FlickerAmount * step(0.34h, frac(_Time.y * _FlickerSpeed + noise));
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half alpha = saturate(sample.a * _Tint.a * input.color.a * _Opacity * dissolve * flicker * pulse);
                half3 rgb = sample.rgb * _Tint.rgb * input.color.rgb * _Brightness * flicker;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
