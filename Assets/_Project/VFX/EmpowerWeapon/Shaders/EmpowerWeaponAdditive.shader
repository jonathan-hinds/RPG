Shader "RPG Clone/VFX/Empower Weapon Additive"
{
    Properties
    {
        [MainTexture] _BaseMap("VFX Texture", 2D) = "white" {}
        _SecondaryMap("Secondary Texture", 2D) = "white" {}
        [MainColor] _Tint("Main Tint", Color) = (0.08,1,0.24,1)
        _SecondaryTint("Secondary Tint", Color) = (1,0.68,0.08,1)
        _Brightness("Emission Brightness", Range(0,10)) = 2
        _Opacity("Opacity", Range(0,1)) = 1
        _ScrollSpeed("Primary Scroll", Vector) = (0,0,0,0)
        _SecondaryScrollSpeed("Secondary Scroll", Vector) = (0,0,0,0)
        _SecondaryMix("Secondary Mix", Range(0,1)) = 0
        _PulseSpeed("Pulse Speed", Range(0,16)) = 0
        _PulseAmount("Pulse Amount", Range(0,1)) = 0
        _Dissolve("Dissolve", Range(0,1)) = 0
        _Softness("Edge Softness", Range(0.001,0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "EmpowerWeaponAdditive"
            Blend SrcAlpha One
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
            TEXTURE2D(_SecondaryMap); SAMPLER(sampler_SecondaryMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _SecondaryMap_ST, _Tint, _SecondaryTint, _ScrollSpeed, _SecondaryScrollSpeed;
                float _Brightness, _Opacity, _SecondaryMix, _PulseSpeed, _PulseAmount, _Dissolve, _Softness;
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
                float2 uvA = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy;
                float2 uvB = input.uv * _SecondaryMap_ST.xy + _SecondaryMap_ST.zw + _Time.y * _SecondaryScrollSpeed.xy;
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvA) * _Tint;
                half4 b = SAMPLE_TEXTURE2D(_SecondaryMap, sampler_SecondaryMap, uvB) * _SecondaryTint;
                half mask = saturate(max(a.a, a.r) + max(b.a, b.r) * _SecondaryMix);
                half dissolve = smoothstep(_Dissolve, _Dissolve + max(0.001h, _Softness), mask);
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half3 color = (a.rgb + b.rgb * _SecondaryMix) * _Brightness * pulse * input.color.rgb;
                return half4(color, mask * dissolve * _Opacity * input.color.a);
            }
            ENDHLSL
        }
    }
}
