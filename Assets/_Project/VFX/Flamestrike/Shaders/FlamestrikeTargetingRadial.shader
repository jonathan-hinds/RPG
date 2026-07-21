Shader "RPG Clone/VFX/Flamestrike Targeting Radial"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (0.08, 0.45, 1.35, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.32
        _EdgeAlpha ("Edge Alpha", Range(0, 2)) = 1
        _EdgeWidth ("Edge Width", Range(0.005, 0.35)) = 0.075
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RadialTargeting"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 localXZ : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Opacity;
                float _FillAlpha;
                float _EdgeAlpha;
                float _EdgeWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localXZ = input.positionOS.xz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.localXZ) * 2.0;
                float disc = 1.0 - smoothstep(0.965, 1.0, radius);
                float radialFade = saturate(1.0 - radius);
                float edge = smoothstep(1.0 - _EdgeWidth, 1.0, radius) * disc;
                float centerBreath = 0.9 + sin(_Time.y * 3.2) * 0.1;
                float alpha = (_FillAlpha * radialFade * centerBreath + _EdgeAlpha * edge) * disc * _Opacity;
                return half4(_Tint.rgb, saturate(alpha) * _Tint.a);
            }
            ENDHLSL
        }
    }
}
