Shader "RPG Clone/VFX/Empower Weapon Alpha"
{
    Properties
    {
        [MainTexture] _BaseMap("VFX Texture", 2D) = "white" {}
        _DistortionMap("Soft Distortion", 2D) = "gray" {}
        [MainColor] _Tint("Tint", Color) = (0.72,1,0.82,0.65)
        _Brightness("Brightness", Range(0,8)) = 1.5
        _Opacity("Opacity", Range(0,1)) = 0.7
        _ScrollSpeed("UV Scroll", Vector) = (0,0.12,0,0)
        _DistortionScroll("Distortion Scroll", Vector) = (0.07,-0.05,0,0)
        _DistortionStrength("Distortion Strength", Range(0,0.25)) = 0.035
        _PulseSpeed("Pulse Speed", Range(0,12)) = 2.4
        _PulseAmount("Pulse Amount", Range(0,1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "EmpowerWeaponAlpha"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _DistortionMap_ST, _Tint, _ScrollSpeed, _DistortionScroll;
                float _Brightness, _Opacity, _DistortionStrength, _PulseSpeed, _PulseAmount;
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
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 noiseUv = input.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw + _Time.y * _DistortionScroll.xy;
                half2 noise = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, noiseUv).rg * 2.0h - 1.0h;
                float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy + noise * _DistortionStrength;
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half alpha = max(sample.a, sample.r * 0.65h) * _Tint.a * _Opacity * input.color.a;
                return half4(sample.rgb * _Tint.rgb * _Brightness * pulse * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
