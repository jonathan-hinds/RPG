Shader "RPG Clone/VFX/Flamestrike Procedural Tube"
{
    Properties
    {
        _NoiseMap ("Flow Noise", 2D) = "gray" {}
        [HDR] _Tint ("Body Tint", Color) = (1.35, 0.16, 0.01, 1)
        [HDR] _HotTint ("Hot Tint", Color) = (2.4, 1.25, 0.24, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _FlowSpeed ("Upward Flow", Range(0, 6)) = 1.8
        _FlowScale ("Flow Scale", Range(0.5, 8)) = 2.6
        _Cutoff ("Erosion", Range(0, 1)) = 0.42
        _EdgeSoftness ("Erosion Softness", Range(0.01, 0.4)) = 0.13
        _TopFadeStart ("Top Fade Start", Range(0, 1)) = 0.48
        _BaseFadeEnd ("Base Fade End", Range(0, 0.35)) = 0.06
        _FresnelStrength ("Edge Glow", Range(0, 3)) = 0.8
        _Phase ("Flow Phase", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ProceduralFireTube"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float4 _Tint;
                float4 _HotTint;
                float _Opacity;
                float _FlowSpeed;
                float _FlowScale;
                float _Cutoff;
                float _EdgeSoftness;
                float _TopFadeStart;
                float _BaseFadeEnd;
                float _FresnelStrength;
                float _Phase;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed + _Phase;
                float2 uvA = float2(input.uv.x * _FlowScale + time * 0.07, input.uv.y * (_FlowScale * 0.72) - time);
                float2 uvB = float2(input.uv.x * (_FlowScale * 1.73) - time * 0.11, input.uv.y * (_FlowScale * 1.15) - time * 1.37);
                // The imported mask uses mirrored wrapping. Sampling unbounded UVs
                // produces continuous motion without exposing a repeating edge.
                half noiseA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvA).r;
                half noiseB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvB).g;
                half flow = saturate(noiseA * 0.68h + noiseB * 0.46h);

                // Stretch the mask vertically and break it into rising tongues. The
                // threshold becomes more aggressive near the top, so the tube erodes
                // into flame strands instead of ending on a geometric rim.
                half circumferentialWaves = sin((input.uv.x * 7.0h + input.uv.y * 2.2h - time * 0.8h) * 6.2831853h) * 0.08h;
                half topErosion = smoothstep(_TopFadeStart, 1.0h, input.uv.y) * 0.32h;
                half flameMask = smoothstep(_Cutoff + topErosion, _Cutoff + topErosion + _EdgeSoftness, flow + circumferentialWaves);
                half baseFade = smoothstep(0.0h, max(0.001h, _BaseFadeEnd), input.uv.y);
                half topFade = 1.0h - smoothstep(_TopFadeStart, 1.0h, input.uv.y - (flow - 0.5h) * 0.24h);

                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalize(input.normalWS), viewDirection))), 1.7h);
                half heat = saturate((1.0h - input.uv.y) * 0.78h + flow * 0.52h);
                half3 color = lerp(_Tint.rgb, _HotTint.rgb, heat);
                color += _HotTint.rgb * fresnel * _FresnelStrength * 0.42h;
                half alpha = flameMask * baseFade * topFade * lerp(0.58h, 1.0h, fresnel) * _Tint.a * input.color.a * _Opacity;
                return half4(color * input.color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
