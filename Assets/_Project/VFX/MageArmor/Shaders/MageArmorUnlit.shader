Shader "RPG Clone/VFX/Mage Armor Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _Opacity("Opacity", Range(0, 1)) = 1
        _Scroll("Scroll", Vector) = (0,0,0,0)
        _DistortionStrength("Distortion", Range(0, 0.3)) = 0
        _Dissolve("Dissolve", Range(0, 1)) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            Name "MageArmorUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float4 _Scroll;
                float _Opacity;
                float _DistortionStrength;
                float _Dissolve;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv + _Scroll.xy;
                float distortion = sin((uv.y + _Time.y * 0.7) * 19.0) * cos((uv.x - _Time.y * 0.45) * 15.0);
                uv.x += distortion * _DistortionStrength;
                half4 sampleColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float dissolveNoise = frac(sin(dot(floor(uv * 48.0), float2(12.9898, 78.233))) * 43758.5453);
                float dissolveMask = smoothstep(_Dissolve - 0.12, _Dissolve + 0.08, dissolveNoise);
                half4 color = sampleColor * _Tint * input.color;
                color.a *= _Opacity * dissolveMask;
                return color;
            }
            ENDHLSL
        }
    }
}
