Shader "RPG Clone/VFX/Press the Attack/Particle Unlit"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [MainColor] _Tint("Tint", Color) = (1,1,1,1)
        _EmissionIntensity("Emission", Range(0,12)) = 1
        _Opacity("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+18" "IgnoreProjector"="True" }
        Pass
        {
            Name "ParticleUnlit"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_particles
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                float _EmissionIntensity;
                float _Opacity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.color=input.color*_Tint;
                output.uv=input.uv*_MainTex_ST.xy+_MainTex_ST.zw; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 sampleValue=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
                sampleValue.a*=_Opacity; sampleValue.rgb*=_EmissionIntensity; return sampleValue;
            }
            ENDHLSL
        }
    }
}
