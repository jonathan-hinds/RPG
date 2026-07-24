Shader "RPG Clone/VFX/Earthquake Layered Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _NoiseMap("Breakup Noise", 2D) = "white" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _Brightness("Brightness", Range(0,4)) = 1
        _ScrollSpeed("UV Scroll", Vector) = (0,0,0,0)
        _NoiseScrollSpeed("Noise Scroll", Vector) = (0.04,-0.03,0,0)
        _NoiseStrength("Edge Breakup", Range(0,1)) = 0.12
        _Dissolve("Dissolve", Range(0,1)) = 0
        _EdgeSoftness("Dissolve Softness", Range(0.001,0.3)) = 0.08
        _SoftParticleDistance("Soft Particle Distance", Range(0.01,3)) = 0.45
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "EarthquakeLayeredUnlit"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float fogFactor : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
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
                half _SoftParticleDistance;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _ScrollSpeed.xy * _Time.y;
                float2 noiseUv = input.uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw + _NoiseScrollSpeed.xy * _Time.y;
                half4 sampleValue = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half mask = max(sampleValue.a < 0.999h ? sampleValue.a : 0.0h, max(sampleValue.r, max(sampleValue.g, sampleValue.b)));
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).r;
                half breakup = lerp(1.0h, noise, _NoiseStrength);
                half dissolve = smoothstep(_Dissolve, _Dissolve + max(0.001h, _EdgeSoftness), mask * breakup);
                float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                float particleDepth = LinearEyeDepth(input.positionCS.z / input.positionCS.w, _ZBufferParams);
                half softFade = saturate((sceneDepth - particleDepth) / max(0.01h, _SoftParticleDistance));
                half alpha = saturate(mask * _Tint.a * input.color.a * _Opacity * dissolve * max(softFade, 0.18h));
                half3 rgb = max(sampleValue.rgb, mask.xxx * 0.62h) * _Tint.rgb * input.color.rgb * _Brightness;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
