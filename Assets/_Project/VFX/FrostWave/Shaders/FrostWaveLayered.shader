Shader "RPG Clone/VFX/Frost Wave Layered"
{
    Properties
    {
        _BaseMap ("Painted Frost Texture", 2D) = "white" {}
        _NoiseMap ("Distortion / Erosion Noise", 2D) = "gray" {}
        [HDR] _Tint ("Tint", Color) = (0.3, 0.8, 1, 1)
        _AtlasRect ("Atlas Rect", Vector) = (1, 1, 0, 0)
        _Scroll ("UV Scroll XY / Noise Scroll ZW", Vector) = (0, 0, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 8)) = 1
        _Reveal ("Radial Reveal", Range(0, 1.5)) = 1.5
        _RevealSoftness ("Reveal Softness", Range(0.001, 0.4)) = 0.12
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Erosion Softness", Range(0.001, 0.3)) = 0.08
        _DistortionStrength ("UV Distortion", Range(0, 0.2)) = 0.025
        _SoftIntersection ("Soft Intersection", Range(0, 4)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
        [Toggle] _ZWrite ("Depth Write", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "FrostWaveLayered"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float4 _AtlasRect;
                float4 _Scroll;
                float _Opacity;
                float _Brightness;
                float _Reveal;
                float _RevealSoftness;
                float _Dissolve;
                float _EdgeSoftness;
                float _DistortionStrength;
                float _SoftIntersection;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 noiseUv = frac(input.uv * 1.83 + _Scroll.zw * _Time.y);
                half2 distortion = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).rg - 0.5h;
                float2 localUv = frac(input.uv + _Scroll.xy * _Time.y + distortion * _DistortionStrength);
                float2 atlasUv = localUv * _AtlasRect.xy + _AtlasRect.zw;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(input.uv * 2.47 - _Scroll.yx * _Time.y)).r;
                half radial = length(input.uv - 0.5) * 1.41421356;
                half reveal = 1.0h - smoothstep(_Reveal, _Reveal + _RevealSoftness, radial);
                half erosion = smoothstep(_Dissolve, _Dissolve + _EdgeSoftness, painted.a * 0.78h + noise * 0.22h);
                half alpha = painted.a * reveal * erosion * _Tint.a * input.color.a * _Opacity;

                if (_SoftIntersection > 0.001)
                {
                    float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                    float sceneRawDepth = SampleSceneDepth(screenUv);
                    float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                    float fragmentEyeDepth = -TransformWorldToView(input.positionWS).z;
                    alpha *= saturate((sceneEyeDepth - fragmentEyeDepth) * _SoftIntersection);
                }

                half luminance = dot(painted.rgb, half3(0.299h, 0.587h, 0.114h));
                half hot = smoothstep(0.48h, 0.95h, luminance);
                half3 color = painted.rgb * _Tint.rgb;
                color += _Tint.rgb * hot * 0.45h;
                color *= _Brightness * input.color.rgb;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
