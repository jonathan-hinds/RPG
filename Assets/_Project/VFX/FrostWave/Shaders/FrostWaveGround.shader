Shader "RPG Clone/VFX/Frost Wave Ground"
{
    Properties
    {
        _BaseMap ("Ground Frost Pattern", 2D) = "white" {}
        _NoiseMap ("Erosion Noise", 2D) = "gray" {}
        [HDR] _Tint ("Tint", Color) = (0.12, 0.55, 1, 1)
        _AtlasRect ("Atlas Rect", Vector) = (1, 1, 0, 0)
        _Scroll ("Noise Scroll", Vector) = (0, 0, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 8)) = 1
        _Reveal ("Radial Reveal", Range(0, 1.5)) = 0
        _RevealSoftness ("Reveal Softness", Range(0.001, 0.35)) = 0.1
        _Dissolve ("Erosion", Range(0, 1)) = 0
        _EdgeSoftness ("Erosion Softness", Range(0.001, 0.3)) = 0.1
        _DistortionStrength ("Edge Distortion", Range(0, 0.15)) = 0.025
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "FrostWaveGround"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
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
                float2 noiseUv = frac(input.uv * 2.05 + _Scroll.zw * _Time.y);
                half2 distortion = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).rg - 0.5h;
                float2 localUv = saturate(input.uv + distortion * _DistortionStrength);
                float2 atlasUv = localUv * _AtlasRect.xy + _AtlasRect.zw;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(input.uv * 3.1 - _Scroll.xy * _Time.y)).r;
                half radial = length(input.uv - 0.5) * 1.41421356;
                half reveal = 1.0h - smoothstep(_Reveal, _Reveal + _RevealSoftness, radial);
                half erosion = smoothstep(_Dissolve, _Dissolve + _EdgeSoftness, painted.a * 0.74h + noise * 0.26h);
                half alpha = painted.a * reveal * erosion * _Tint.a * input.color.a * _Opacity;
                half3 color = painted.rgb * _Tint.rgb * _Brightness * input.color.rgb;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
