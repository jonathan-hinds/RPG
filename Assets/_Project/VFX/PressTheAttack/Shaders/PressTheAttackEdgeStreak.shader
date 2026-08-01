Shader "RPG Clone/VFX/Press the Attack/Edge Streak"
{
    Properties
    {
        _StreakMask("Tapered Streaks", 2D) = "black" {}
        _SlashMask("Broken Slash Marks", 2D) = "black" {}
        _EdgeBreakupMask("Edge Breakup", 2D) = "white" {}
        _DistortionMap("Distortion", 2D) = "gray" {}
        _MainTint("Main Crimson", Color) = (0.94,0.025,0.035,1)
        _DarkTint("Dark Red", Color) = (0.24,0.004,0.012,1)
        _HighlightTint("Highlight", Color) = (1,0.68,0.62,1)
        _EmissionIntensity("Emission", Range(0,12)) = 2.5
        _Opacity("Opacity", Range(0,1)) = 1
        _EdgeGlowWidth("Edge Tightness", Range(0.1,10)) = 5.6
        _EdgeGlowIntensity("Edge Intensity", Range(0,8)) = 1.75
        _SurfaceStreakSpeed("Streak Speed", Range(0,8)) = 1.35
        _MovementResponse("Movement Response", Range(0,4)) = 0
        _AttackResponse("Attack Response", Range(0,4)) = 0
        _FinalInstability("Final Instability", Range(0,4)) = 0
        _RevealProgress("Feet Up Reveal", Range(0,1)) = 1
        _SurfaceLift("Surface Lift", Range(0,0.02)) = 0.0052
        [HideInInspector] _BoundsMin("Bounds Min", Vector) = (0,0,0,0)
        [HideInInspector] _BoundsSize("Bounds Size", Vector) = (1,1,1,0)
        [HideInInspector] _FlowAxis("Flow Axis", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+24" }
        Pass
        {
            Name "EdgeStreak"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PressTheAttackSurfaceProjection.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float vertical:TEXCOORD3; float3 normalizedPosition:TEXCOORD4; float3 normalOS:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
            TEXTURE2D(_StreakMask); SAMPLER(sampler_StreakMask);
            TEXTURE2D(_SlashMask); SAMPLER(sampler_SlashMask);
            TEXTURE2D(_EdgeBreakupMask); SAMPLER(sampler_EdgeBreakupMask);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTint,_DarkTint,_HighlightTint,_BoundsMin,_BoundsSize,_FlowAxis;
                float4x4 _ProjectionWorldToLocal;
                float _EmissionIntensity,_Opacity,_EdgeGlowWidth,_EdgeGlowIntensity,_SurfaceStreakSpeed;
                float _MovementResponse,_AttackResponse,_FinalInstability,_RevealProgress,_SurfaceLift;
            CBUFFER_END
            half Mask(half4 v){return saturate(dot(v.rgb,half3(0.299h,0.587h,0.114h))*v.a);}
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output);
                float3 sourcePositionWS=TransformObjectToWorld(input.positionOS.xyz); float3 sourceNormalWS=TransformObjectToWorldNormal(input.normalOS);
                float3 projectionPosition=mul(_ProjectionWorldToLocal,float4(sourcePositionWS,1.0)).xyz;
                float3 projectionNormal=normalize(mul((float3x3)_ProjectionWorldToLocal,sourceNormalWS));
                float3 size=max(abs(_BoundsSize.xyz),0.0001); float3 p=saturate((projectionPosition-_BoundsMin.xyz)/size);
                float3 axis=abs(_FlowAxis.xyz); axis/=max(0.0001,axis.x+axis.y+axis.z);
                float longitudinal=dot(p,axis); float3 n=abs(projectionNormal); float crossCoordinate;
                if(axis.x>axis.y&&axis.x>axis.z) crossCoordinate=lerp(p.y,p.z,n.y/max(0.0001,n.y+n.z));
                else if(axis.y>axis.z) crossCoordinate=lerp(p.x,p.z,n.x/max(0.0001,n.x+n.z));
                else crossCoordinate=lerp(p.x,p.y,n.x/max(0.0001,n.x+n.y));
                VertexPositionInputs positions=GetVertexPositionInputs(input.positionOS.xyz+input.normalOS*_SurfaceLift);
                output.positionCS=positions.positionCS; output.positionWS=positions.positionWS; output.normalWS=sourceNormalWS;
                output.uv=float2(crossCoordinate,longitudinal); output.vertical=longitudinal; output.normalizedPosition=p; output.normalOS=projectionNormal; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float response=_MovementResponse+_AttackResponse+_FinalInstability;
                float3 surfaceNormal=normalize(input.normalOS);
                half2 distortion=PTASampleTriplanar(TEXTURE2D_ARGS(_DistortionMap,sampler_DistortionMap),input.normalizedPosition,surfaceNormal,2.2,_Time.y*float2(0.06,0.09),0.2).rg*2.0h-1.0h;
                float speed=_SurfaceStreakSpeed*(1.0+response*0.38);
                half streak=Mask(PTASampleTriplanar(TEXTURE2D_ARGS(_StreakMask,sampler_StreakMask),input.normalizedPosition,surfaceNormal,float2(1.25,1.45),_Time.y*float2(-0.09,0.24)*speed+distortion*0.025,-0.34));
                half slash=Mask(PTASampleTriplanar(TEXTURE2D_ARGS(_SlashMask,sampler_SlashMask),input.normalizedPosition,surfaceNormal,1.36,_Time.y*float2(0.075,0.18)*speed-distortion.yx*0.018,0.62));
                half breakup=Mask(PTASampleTriplanar(TEXTURE2D_ARGS(_EdgeBreakupMask,sampler_EdgeBreakupMask),input.normalizedPosition,surfaceNormal,2.1,_Time.y*float2(-0.018,0.026),0.17));
                half3 viewDir=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel=pow(1.0h-saturate(dot(normalize(input.normalWS),viewDir)),_EdgeGlowWidth);
                half streakEnergy=smoothstep(0.74h,0.93h,max(streak,slash*(0.72h+_AttackResponse*0.35h)))*lerp(0.62h,1.0h,breakup);
                half edge=fresnel*lerp(0.38h,1.0h,breakup)*_EdgeGlowIntensity*(0.62h+response*0.28h);
                half reveal=smoothstep(input.vertical-0.15h,input.vertical+0.1h,_RevealProgress);
                half alpha=saturate((edge*0.58h+streakEnergy*0.92h)*_Opacity*reveal);
                half3 color=_MainTint.rgb*(edge*1.18h+streakEnergy)+_HighlightTint.rgb*streakEnergy*(0.62h+_AttackResponse*0.55h);
                return half4(color*_EmissionIntensity,alpha);
            }
            ENDHLSL
        }
    }
}
