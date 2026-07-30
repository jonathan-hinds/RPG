Shader "RPG Clone/VFX/Frost Wave Ice"
{
    Properties
    {
        _BaseMap ("Painted Ice Surface", 2D) = "white" {}
        _NoiseMap ("Dissolve Noise", 2D) = "gray" {}
        [HDR] _Tint ("Body Tint", Color) = (0.08, 0.5, 1, 1)
        [HDR] _EdgeTint ("Edge Tint", Color) = (0.7, 1.3, 1.7, 1)
        _AtlasRect ("Atlas Rect", Vector) = (1, 1, 0, 0)
        _Scroll ("UV / Noise Scroll", Vector) = (0, 0, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 8)) = 1
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Dissolve Edge", Range(0.001, 0.3)) = 0.08
        _FresnelStrength ("Painted Edge Highlight", Range(0, 4)) = 1.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FrostWaveIce"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _EdgeTint;
                float4 _AtlasRect;
                float4 _Scroll;
                float _Opacity;
                float _Brightness;
                float _Dissolve;
                float _EdgeSoftness;
                float _FresnelStrength;
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
                float2 atlasUv = frac(input.uv + _Scroll.xy * _Time.y) * _AtlasRect.xy + _AtlasRect.zw;
                half4 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, frac(input.uv * 2.7 + _Scroll.zw * _Time.y)).r;
                half erosion = smoothstep(_Dissolve, _Dissolve + _EdgeSoftness, painted.a * 0.82h + noise * 0.18h);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalize(input.normalWS), viewDir))), 2.2h);
                half luminance = dot(painted.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 color = painted.rgb * _Tint.rgb;
                color += _EdgeTint.rgb * fresnel * _FresnelStrength * painted.a;
                color += _EdgeTint.rgb * smoothstep(0.68h, 0.96h, luminance) * 0.28h;
                color *= _Brightness * input.color.rgb;
                half alpha = painted.a * erosion * _Tint.a * input.color.a * _Opacity;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
