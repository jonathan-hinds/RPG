Shader "RPG Clone/VFX/Fire Blast Sprite Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Brightness ("Brightness", Range(0,4)) = 1
        _ScrollSpeed ("Scroll Speed", Vector) = (0,0,0,0)
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FireBlastUnlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Opacity;
                half _Brightness;
                float4 _ScrollSpeed;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) + _ScrollSpeed.xy * _Time.y;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half vertexColorWeight = step(0.001h, dot(input.color.rgb, half3(1.0h, 1.0h, 1.0h)));
                half3 vertexColor = lerp(half3(1.0h, 1.0h, 1.0h), input.color.rgb, vertexColorWeight);
                half alpha = saturate(sample.a * _Tint.a * input.color.a * _Opacity);
                half3 color = sample.rgb * _Tint.rgb * vertexColor * _Brightness;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
