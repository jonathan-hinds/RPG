Shader "RPG Clone/VFX/Press the Attack/Rage Overlay"
{
    Properties
    {
        [MainTexture] _ChargeColorTex("Painted Red Charge", 2D) = "black" {}
        _VeinMask("Rage Vein Network", 2D) = "black" {}
        _FlowMask("Directional Rage Flow", 2D) = "black" {}
        _BreakupMask("Rage Breakup", 2D) = "white" {}
        _DistortionMap("Surface Distortion", 2D) = "gray" {}
        [MainColor] _MainTint("Main Crimson", Color) = (0.94,0.025,0.035,1)
        _DarkTint("Dark Blood Red", Color) = (0.24,0.004,0.012,1)
        _HighlightTint("White-hot Red", Color) = (1,0.68,0.62,1)
        _EmissionIntensity("Surface Emission", Range(0,16)) = 5.1
        _Opacity("Overlay Opacity", Range(0,1)) = 0.92
        _PatternScale("Pattern Scale", Range(0.25,6)) = 1.15
        _PulseSpeed("Pulse Speed", Range(0,12)) = 4.25
        _TravelSpeed("Travelling Charge Speed", Range(0,5)) = 0.82
        _UndercoatIntensity("Rage Undercoat", Range(0,2)) = 0.72
        _MovementResponse("Movement Response", Range(0,4)) = 0
        _AttackResponse("Attack Response", Range(0,4)) = 0
        _FinalInstability("Final Instability", Range(0,4)) = 0
        _RevealProgress("Feet Up Reveal", Range(0,1)) = 1
        _SurfaceLift("Surface Lift", Range(0,0.02)) = 0.0028
        [HideInInspector] _BoundsMin("Bounds Min", Vector) = (0,0,0,0)
        [HideInInspector] _BoundsSize("Bounds Size", Vector) = (1,1,1,0)
        [HideInInspector] _FlowAxis("Flow Axis", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" "IgnoreProjector"="True" }
        Pass
        {
            Name "ChargedRageSurface"
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 surfaceUv : TEXCOORD2;
                float longitudinal : TEXCOORD3;
                float3 normalizedPosition : TEXCOORD4;
                float3 normalOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_ChargeColorTex); SAMPLER(sampler_ChargeColorTex);
            TEXTURE2D(_VeinMask); SAMPLER(sampler_VeinMask);
            TEXTURE2D(_FlowMask); SAMPLER(sampler_FlowMask);
            TEXTURE2D(_BreakupMask); SAMPLER(sampler_BreakupMask);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTint, _DarkTint, _HighlightTint, _BoundsMin, _BoundsSize, _FlowAxis;
                float4x4 _ProjectionWorldToLocal;
                float _EmissionIntensity, _Opacity, _PatternScale, _PulseSpeed, _TravelSpeed, _UndercoatIntensity;
                float _MovementResponse, _AttackResponse, _FinalInstability, _RevealProgress, _SurfaceLift;
            CBUFFER_END

            half MaskValue(half4 value)
            {
                return saturate(dot(value.rgb, half3(0.299h, 0.587h, 0.114h)) * value.a);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 sourcePositionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 sourceNormalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 projectionPosition = mul(_ProjectionWorldToLocal, float4(sourcePositionWS, 1.0)).xyz;
                float3 projectionNormal = normalize(mul((float3x3)_ProjectionWorldToLocal, sourceNormalWS));
                float3 safeSize = max(abs(_BoundsSize.xyz), float3(0.0001, 0.0001, 0.0001));
                float3 normalizedPosition = saturate((projectionPosition - _BoundsMin.xyz) / safeSize);
                float3 axis = abs(_FlowAxis.xyz);
                axis /= max(0.0001, axis.x + axis.y + axis.z);
                float longitudinal = dot(normalizedPosition, axis);
                float3 normalWeight = abs(projectionNormal);
                float crossCoordinate;
                if (axis.x > axis.y && axis.x > axis.z)
                {
                    crossCoordinate = lerp(normalizedPosition.y, normalizedPosition.z,
                        normalWeight.y / max(0.0001, normalWeight.y + normalWeight.z));
                }
                else if (axis.y > axis.z)
                {
                    crossCoordinate = lerp(normalizedPosition.x, normalizedPosition.z,
                        normalWeight.x / max(0.0001, normalWeight.x + normalWeight.z));
                }
                else
                {
                    crossCoordinate = lerp(normalizedPosition.x, normalizedPosition.y,
                        normalWeight.x / max(0.0001, normalWeight.x + normalWeight.y));
                }

                float response = _MovementResponse + _AttackResponse + _FinalInstability;
                float liftPulse = 0.78 + 0.22 * sin(_Time.y * (_PulseSpeed + response) + longitudinal * 10.0);
                VertexPositionInputs positions = GetVertexPositionInputs(
                    input.positionOS.xyz + input.normalOS * (_SurfaceLift * liftPulse));
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = sourceNormalWS;
                output.surfaceUv = float2(crossCoordinate, longitudinal);
                output.longitudinal = longitudinal;
                output.normalizedPosition = normalizedPosition;
                output.normalOS = projectionNormal;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float response = _MovementResponse + _AttackResponse + _FinalInstability;
                float speedBoost = 1.0 + response * 0.22;
                float3 surfacePosition = input.normalizedPosition;
                float3 surfaceNormal = normalize(input.normalOS);
                half2 distortion = PTASampleTriplanar(
                    TEXTURE2D_ARGS(_DistortionMap, sampler_DistortionMap),
                    surfacePosition, surfaceNormal, 2.35,
                    _Time.y * float2(0.065, 0.093) * speedBoost, 0.18).rg * 2.0h - 1.0h;

                float2 veinOffsetA = _Time.y * float2(0.018, 0.105) * speedBoost + distortion * 0.032;
                float2 veinOffsetB = -_Time.y * float2(0.031, 0.071) * speedBoost - distortion.yx * 0.021;
                float2 flowOffset = _Time.y * float2(-0.052, 0.23) * speedBoost + distortion * 0.018;
                float2 breakupOffset = _Time.y * float2(0.014, -0.021);
                float2 colorOffset = _Time.y * float2(-0.022, 0.058) * speedBoost + distortion * 0.014;

                half veinA = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_VeinMask, sampler_VeinMask),
                    surfacePosition, surfaceNormal, max(_PatternScale * 0.5, 0.7), veinOffsetA, -0.12));
                half veinB = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_VeinMask, sampler_VeinMask),
                    surfacePosition, surfaceNormal, max(_PatternScale * 0.74, 0.95), veinOffsetB, 0.67));
                half flow = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_FlowMask, sampler_FlowMask),
                    surfacePosition, surfaceNormal, _PatternScale * float2(0.86, 1.12), flowOffset, -0.48));
                half breakup = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_BreakupMask, sampler_BreakupMask),
                    surfacePosition, surfaceNormal, 1.68, breakupOffset, 0.36));
                half3 paintedCharge = PTASampleTriplanar(
                    TEXTURE2D_ARGS(_ChargeColorTex, sampler_ChargeColorTex),
                    surfacePosition, surfaceNormal, _PatternScale * 0.84, colorOffset, 0.21).rgb;
                half paintedEnergy = saturate(max(paintedCharge.r, max(paintedCharge.g, paintedCharge.b)));

                half travellingBand = pow(
                    saturate(1.0h - abs(frac(input.longitudinal * 1.34h - _Time.y * _TravelSpeed * speedBoost) - 0.5h) * 2.0h),
                    4.0h);
                half pulse = 0.72h + 0.28h * sin(
                    _Time.y * (_PulseSpeed + response * 1.2h) + input.longitudinal * 11.0h + veinB * 4.0h);
                half veinSample = max(veinA, veinB * 0.62h);
                half vein = smoothstep(0.035h, 0.4h, veinSample) * lerp(0.72h, 1.0h, breakup);
                half veinCore = smoothstep(0.58h, 0.91h, veinSample);
                half veinRim = saturate(vein - veinCore * 0.54h);
                half flowEnergy = smoothstep(0.7h, 0.91h, flow)
                    * (0.38h + travellingBand * 0.62h) * lerp(0.58h, 1.0h, breakup);
                half undercoat = smoothstep(0.045h, 0.54h, paintedEnergy) * (0.5h + breakup * 0.5h);

                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(normalize(input.normalWS), viewDirection)), 3.2h);
                half edgeCorona = fresnel * saturate(vein + flowEnergy + undercoat * 0.42h)
                    * (1.1h + response * 0.34h);
                half reveal = smoothstep(input.longitudinal - 0.18h, input.longitudinal + 0.08h, _RevealProgress);

                half3 paintedTint = lerp(_DarkTint.rgb, _MainTint.rgb, saturate(paintedCharge.r * 1.4h));
                half3 color =
                    paintedTint * undercoat * _UndercoatIntensity * 0.92h
                    + _MainTint.rgb * (veinRim * 0.08h + vein * 0.035h + flowEnergy * 0.14h)
                    + _HighlightTint.rgb * (veinCore * 0.06h + travellingBand * flowEnergy * 0.12h)
                    + lerp(_MainTint.rgb, _HighlightTint.rgb, 0.58h) * edgeCorona * 0.1h;
                half alpha = saturate(
                    (undercoat * _UndercoatIntensity * 0.52h + vein * 0.055h + flowEnergy * 0.08h + edgeCorona * 0.06h)
                    * _Opacity * reveal);

                return half4(color * _EmissionIntensity * pulse, alpha);
            }
            ENDHLSL
        }
    }
}
