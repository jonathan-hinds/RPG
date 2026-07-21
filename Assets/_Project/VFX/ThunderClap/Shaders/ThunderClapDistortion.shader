Shader "RPG Clone/VFX/Thunder Clap Distortion"
{
    Properties
    {
        [MainTexture] _BaseMap("Pressure Mask", 2D) = "white" {}
        _DistortionMap("Distortion Flow", 2D) = "gray" {}
        [MainColor] _Tint("Pressure Tint", Color) = (0.7,0.9,1,1)
        _Opacity("Opacity", Range(0,1)) = 0.22
        _Brightness("Tint Brightness", Range(0,4)) = 1
        _ScrollSpeed("Mask Scroll", Vector) = (0,0,0,0)
        _DistortionScrollSpeed("Distortion Scroll", Vector) = (0.08,-0.06,0,0)
        _DistortionStrength("Scene Refraction", Range(0,0.08)) = 0.014
        _Dissolve("Dissolve", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Name "ThunderClapDistortion"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; half4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DistortionMap_ST;
                float4 _Tint;
                float4 _ScrollSpeed;
                float4 _DistortionScrollSpeed;
                float _Opacity;
                float _Brightness;
                float _DistortionStrength;
                float _Dissolve;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 maskUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _ScrollSpeed.xy;
                float2 noiseUv = input.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw + _Time.y * _DistortionScrollSpeed.xy;
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, maskUv);
                half2 noise = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, noiseUv).rg * 2.0h - 1.0h;
                float2 screenUv = input.screenPos.xy / max(0.0001, input.screenPos.w);
                half sceneAvailability = step(0.001h, dot(SampleSceneColor(screenUv), half3(1,1,1)));
                half3 scene = SampleSceneColor(screenUv + noise * _DistortionStrength * mask.a);
                half3 fallback = _Tint.rgb * _Brightness;
                half3 color = lerp(fallback, lerp(scene, scene + _Tint.rgb * 0.08h * _Brightness, 0.4h), sceneAvailability);
                half breakup = smoothstep(_Dissolve, _Dissolve + 0.08h, mask.a);
                return half4(color, mask.a * input.color.a * _Opacity * breakup);
            }
            ENDHLSL
        }
    }
}
