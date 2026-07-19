Shader "RPG Clone/VFX/Water Shield Layered Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Primary Water Texture", 2D) = "white" {}
        _SecondaryMap("Secondary Water Texture", 2D) = "white" {}
        _DistortionMap("Flow Distortion", 2D) = "gray" {}
        [MainColor] _Tint("Primary Tint", Color) = (1,1,1,1)
        _SecondaryTint("Secondary Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _Brightness("Emission Brightness", Range(0,8)) = 1
        _SecondaryMix("Secondary Layer Mix", Range(0,1)) = 0.5
        _ScrollSpeed("Primary UV Scroll", Vector) = (0.1,0.05,0,0)
        _SecondaryScrollSpeed("Secondary UV Scroll", Vector) = (-0.06,0.11,0,0)
        _DistortionScrollSpeed("Distortion UV Scroll", Vector) = (0.08,0.12,0,0)
        _DistortionStrength("UV Distortion", Range(0,0.2)) = 0.025
        _WobbleAmount("Vertex Wobble", Range(0,0.25)) = 0.04
        _WobbleSpeed("Vertex Wobble Speed", Range(0,12)) = 2.5
        _Dissolve("Soft Dissolve", Range(0,1)) = 0
        _EdgeSoftness("Dissolve Softness", Range(0.001,0.3)) = 0.08
        _EdgePower("Fresnel Edge Power", Range(0.2,8)) = 2.2
        _EdgeBrightness("Fresnel Edge Brightness", Range(0,4)) = 0.4
        _PulseFrequency("Opacity Pulse Frequency", Range(0,20)) = 2
        _PulseAmount("Opacity Pulse Amount", Range(0,1)) = 0.08
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
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
            Name "WaterShieldLayered"
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SecondaryMap); SAMPLER(sampler_SecondaryMap);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _SecondaryMap_ST;
                float4 _DistortionMap_ST;
                float4 _Tint;
                float4 _SecondaryTint;
                float4 _ScrollSpeed;
                float4 _SecondaryScrollSpeed;
                float4 _DistortionScrollSpeed;
                float _Opacity;
                float _Brightness;
                float _SecondaryMix;
                float _DistortionStrength;
                float _WobbleAmount;
                float _WobbleSpeed;
                float _Dissolve;
                float _EdgeSoftness;
                float _EdgePower;
                float _EdgeBrightness;
                float _PulseFrequency;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float waveA = sin((input.positionOS.x * 5.1 + input.positionOS.y * 4.2 + input.positionOS.z * 6.3) + _Time.y * _WobbleSpeed);
                float waveB = sin((input.positionOS.x * -3.7 + input.positionOS.y * 6.8 + input.positionOS.z * 2.9) - _Time.y * _WobbleSpeed * 0.73);
                float3 displaced = input.positionOS.xyz + input.normalOS * (waveA + waveB) * 0.5 * _WobbleAmount;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displaced);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 distortionUv = input.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw + _Time.y * _DistortionScrollSpeed.xy;
                half2 distortion = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, distortionUv).rg * 2.0h - 1.0h;
                float2 primaryUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy + distortion * _DistortionStrength;
                float2 secondaryUv = input.uv * _SecondaryMap_ST.xy + _SecondaryMap_ST.zw + _Time.y * _SecondaryScrollSpeed.xy - distortion.yx * _DistortionStrength * 0.72;
                half4 primary = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, primaryUv) * _Tint;
                half4 secondary = SAMPLE_TEXTURE2D(_SecondaryMap, sampler_SecondaryMap, secondaryUv) * _SecondaryTint;
                half blendMask = saturate(secondary.r * _SecondaryMix + secondary.a * _SecondaryMix);
                half3 rgb = lerp(primary.rgb, secondary.rgb, blendMask);
                half alphaMask = saturate(max(primary.a, secondary.a * _SecondaryMix));
                half luminanceMask = saturate(max(primary.r, secondary.r * _SecondaryMix));
                half dissolveMask = smoothstep(_Dissolve, _Dissolve + max(0.001h, _EdgeSoftness), alphaMask * max(0.18h, luminanceMask));
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(normalize(input.normalWS), viewDirection)), _EdgePower);
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseFrequency));
                rgb = (rgb + _Tint.rgb * fresnel * _EdgeBrightness) * _Brightness;
                return half4(rgb * input.color.rgb, alphaMask * _Opacity * dissolveMask * pulse * input.color.a);
            }
            ENDHLSL
        }
    }
}
