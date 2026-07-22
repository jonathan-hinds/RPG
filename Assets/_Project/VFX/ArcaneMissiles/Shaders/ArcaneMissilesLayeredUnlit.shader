Shader "RPG Clone/VFX/Arcane Missiles Layered Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Hand-painted Layer", 2D) = "white" {}
        _NoiseMap("Distortion / Dissolve Noise", 2D) = "gray" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _Brightness("Emission Brightness", Range(0,10)) = 1
        _ScrollSpeed("UV Scroll XY", Vector) = (0,0,0,0)
        _NoiseScrollSpeed("Noise Scroll XY", Vector) = (0.05,-0.04,0,0)
        _DistortionStrength("UV Distortion", Range(0,0.2)) = 0.02
        _Dissolve("Dissolve", Range(0,1)) = 0
        _DissolveSoftness("Dissolve Softness", Range(0.001,0.3)) = 0.08
        _FlickerSpeed("Flicker Speed", Range(0,30)) = 8
        _FlickerAmount("Flicker Amount", Range(0,1)) = 0.06
        _PulseSpeed("Opacity Pulse Speed", Range(0,20)) = 3
        _PulseAmount("Opacity Pulse Amount", Range(0,1)) = 0.08
        _DepthFadeDistance("Depth Fade Distance", Range(0,2)) = 0.18
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
            Name "ArcaneMissilesLayered"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 screenPosition : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

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
                float _DissolveSoftness;
                float _FlickerSpeed;
                float _FlickerAmount;
                float _PulseSpeed;
                float _PulseAmount;
                float _DepthFadeDistance;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.screenPosition = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -TransformWorldToView(positions.positionWS).z;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 noiseUv = input.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _Time.y * _NoiseScrollSpeed.xy;
                half4 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv);
                float2 distortion = (noise.rg * 2.0h - 1.0h) * _DistortionStrength;
                float2 baseUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy + distortion;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv);
                half mask = max(painted.a, max(painted.r, max(painted.g, painted.b)));
                half dissolve = smoothstep(_Dissolve, _Dissolve + max(0.001h, _DissolveSoftness), mask * (0.72h + noise.r * 0.28h));
                half flicker = 1.0h - _FlickerAmount + _FlickerAmount * (0.5h + 0.5h * sin(_Time.y * _FlickerSpeed + noise.b * 6.283h));
                half pulse = 1.0h - _PulseAmount + _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));

                float2 screenUv = input.screenPosition.xy / max(0.0001, input.screenPosition.w);
                float rawDepth = SampleSceneDepth(screenUv);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                half depthFade = _DepthFadeDistance <= 0.001h ? 1.0h : saturate((sceneDepth - input.eyeDepth) / _DepthFadeDistance);

                half3 rgb = painted.rgb * _Tint.rgb * _Brightness * flicker;
                half alpha = mask * painted.a * _Tint.a * _Opacity * dissolve * pulse * depthFade * input.color.a;
                return half4(rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
