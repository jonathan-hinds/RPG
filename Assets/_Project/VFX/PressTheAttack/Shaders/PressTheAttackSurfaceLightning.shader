Shader "RPG Clone/VFX/Press the Attack/Surface Lightning"
{
    Properties
    {
        [MainTexture] _LightningMaskA("Crawling Lightning", 2D) = "black" {}
        _LightningMaskB("Dense Rage Veins", 2D) = "black" {}
        _FlowMask("Directional Rage Flow", 2D) = "black" {}
        _DistortionMap("Distortion", 2D) = "gray" {}
        _BreakupMask("Rage Breakup", 2D) = "white" {}
        _MainTint("Main Crimson", Color) = (0.94,0.025,0.035,1)
        _DarkTint("Dark Red", Color) = (0.24,0.004,0.012,1)
        _HighlightTint("White-hot Red", Color) = (1,0.68,0.62,1)
        _EmissionIntensity("Lightning Emission", Range(0,18)) = 7.4
        _Opacity("Opacity", Range(0,1)) = 1
        _PatternScale("Pattern Scale", Range(0.25,6)) = 1.15
        _PulseSpeed("Pulse Speed", Range(0,12)) = 4.25
        _TravelSpeed("Surge Speed", Range(0,5)) = 0.82
        _LightningFrequency("Snap Frequency", Range(0,8)) = 4.4
        _LightningThickness("Bolt Thickness", Range(0.01,0.3)) = 0.14
        _LightningSpeed("Crawl Speed", Range(0,8)) = 2.25
        _LightningDistortion("Distortion", Range(0,0.25)) = 0.062
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
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+23" "IgnoreProjector"="True" }
        Pass
        {
            Name "CrawlingSurfaceLightning"
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
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float2 surfaceUv:TEXCOORD2;
                float longitudinal:TEXCOORD3;
                float3 normalizedPosition:TEXCOORD4;
                float3 normalOS:TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_LightningMaskA); SAMPLER(sampler_LightningMaskA);
            TEXTURE2D(_LightningMaskB); SAMPLER(sampler_LightningMaskB);
            TEXTURE2D(_FlowMask); SAMPLER(sampler_FlowMask);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);
            TEXTURE2D(_BreakupMask); SAMPLER(sampler_BreakupMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTint,_DarkTint,_HighlightTint,_BoundsMin,_BoundsSize,_FlowAxis;
                float4x4 _ProjectionWorldToLocal;
                float _EmissionIntensity,_Opacity,_PatternScale,_PulseSpeed,_TravelSpeed;
                float _LightningFrequency,_LightningThickness,_LightningSpeed,_LightningDistortion;
                float _MovementResponse,_AttackResponse,_FinalInstability,_RevealProgress,_SurfaceLift;
            CBUFFER_END

            half MaskValue(half4 value)
            {
                return saturate(dot(value.rgb, half3(0.299h,0.587h,0.114h)) * value.a);
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 fractionValue = frac(value);
                fractionValue = fractionValue * fractionValue * (3.0 - 2.0 * fractionValue);
                return lerp(
                    lerp(Hash21(cell), Hash21(cell + float2(1,0)), fractionValue.x),
                    lerp(Hash21(cell + float2(0,1)), Hash21(cell + float2(1,1)), fractionValue.x),
                    fractionValue.y);
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
                float3 safeSize = max(abs(_BoundsSize.xyz), float3(0.0001,0.0001,0.0001));
                float3 p = saturate((projectionPosition - _BoundsMin.xyz) / safeSize);
                float3 axis = abs(_FlowAxis.xyz);
                axis /= max(0.0001, axis.x + axis.y + axis.z);
                float longitudinal = dot(p, axis);
                float3 n = abs(projectionNormal);
                float crossCoordinate;
                if (axis.x > axis.y && axis.x > axis.z)
                    crossCoordinate = lerp(p.y, p.z, n.y / max(0.0001, n.y + n.z));
                else if (axis.y > axis.z)
                    crossCoordinate = lerp(p.x, p.z, n.x / max(0.0001, n.x + n.z));
                else
                    crossCoordinate = lerp(p.x, p.y, n.x / max(0.0001, n.x + n.y));

                float response = _MovementResponse + _AttackResponse + _FinalInstability;
                float snapLift = 0.8 + 0.2 * sin(_Time.y * (_PulseSpeed + response * 1.5) + longitudinal * 14.0);
                VertexPositionInputs positions = GetVertexPositionInputs(
                    input.positionOS.xyz + input.normalOS * (_SurfaceLift * snapLift));
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = sourceNormalWS;
                output.surfaceUv = float2(crossCoordinate, longitudinal);
                output.longitudinal = longitudinal;
                output.normalizedPosition = p;
                output.normalOS = projectionNormal;
                return output;
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float response = _MovementResponse + _AttackResponse + _FinalInstability;
                float speedBoost = 1.0 + response * 0.3;
                float3 surfacePosition = input.normalizedPosition;
                float3 surfaceNormal = normalize(input.normalOS);
                float2 distortionOffset = _Time.y * float2(0.09,0.13) * speedBoost;
                half2 distortion = PTASampleTriplanar(
                    TEXTURE2D_ARGS(_DistortionMap, sampler_DistortionMap),
                    surfacePosition, surfaceNormal, 2.7, distortionOffset, 0.23).rg * 2.0h - 1.0h;

                float2 offsetA = _Time.y * float2(0.038,-0.13 * _LightningSpeed) * speedBoost
                    + distortion * _LightningDistortion;
                float2 offsetB = _Time.y * float2(-0.061,0.087 * _LightningSpeed) * speedBoost
                    - distortion.yx * _LightningDistortion * 0.64;
                float2 flowOffset = _Time.y * float2(0.045,0.19) * speedBoost;

                half a = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_LightningMaskA,sampler_LightningMaskA),
                    surfacePosition, surfaceNormal, max(_PatternScale * 0.32, 0.56), offsetA, -0.16));
                half b = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_LightningMaskB,sampler_LightningMaskB),
                    surfacePosition, surfaceNormal, max(_PatternScale * 0.7, 0.92), offsetB, 0.58));
                half flow = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_FlowMask,sampler_FlowMask),
                    surfacePosition, surfaceNormal, _PatternScale * float2(0.82,1.18), flowOffset, -0.42));
                half breakup = MaskValue(PTASampleTriplanar(
                    TEXTURE2D_ARGS(_BreakupMask,sampler_BreakupMask),
                    surfacePosition, surfaceNormal, 1.72, _Time.y*float2(-0.018,0.024), 0.31));

                float2 surfaceUv = input.surfaceUv;

                half regionPhase = frac(
                    floor(surfaceUv.x * 5.0h) * 0.173h
                    + floor(surfaceUv.y * 7.0h) * 0.317h
                    + _Time.y * (_LightningFrequency + response * 1.25h));
                half localFlicker = smoothstep(0.08h,0.3h,regionPhase) * (1.0h - smoothstep(0.72h,0.96h,regionPhase));
                localFlicker = lerp(0.62h,1.15h,localFlicker);
                // Preserve the broad gray falloff painted around the white bolt
                // cores. High thresholds made the source art read as stippling.
                half threshold = saturate(0.08h - _LightningThickness * 0.16h - response * 0.008h);
                half boltA = smoothstep(threshold,0.3h,a);
                half boltB = smoothstep(0.14h,0.52h,b);
                half flowBolt = smoothstep(0.72h,0.92h,flow) * (0.28h + _MovementResponse * 0.18h);

                float steppedTime = floor(_Time.y * (9.0 + response * 2.0)) / (9.0 + response * 2.0);
                float2 proceduralUv = float2(
                    surfacePosition.x + surfacePosition.z * 0.73 + surfacePosition.y * 0.31,
                    surfacePosition.y - surfacePosition.x * 0.42 + surfacePosition.z * 0.25) * float2(2.65, 3.2);
                float mainWobble = (ValueNoise(float2(proceduralUv.y * 1.42, steppedTime * 1.35)) - 0.5) * 0.92
                    + sin(proceduralUv.y * 8.5 + steppedTime * 9.0) * 0.11;
                float mainDistance = abs(frac(proceduralUv.x + mainWobble) - 0.5);
                float mainWidth = 0.025 + _LightningThickness * 0.22 + response * 0.004;
                half proceduralMain = 1.0h - smoothstep(
                    mainWidth,
                    mainWidth + max(fwidth(mainDistance) * 1.35, 0.018),
                    mainDistance);
                half proceduralMainCore = 1.0h - smoothstep(
                    mainWidth * 0.2,
                    mainWidth * 0.2 + max(fwidth(mainDistance) * 0.7, 0.007),
                    mainDistance);

                float branchWobble = (ValueNoise(float2(proceduralUv.y * 2.2 + 17.0, steppedTime * 1.8)) - 0.5) * 0.58;
                float branchDistance = abs(frac(
                    proceduralUv.x * 1.48 - proceduralUv.y * 0.72 + branchWobble + 0.19) - 0.5);
                half branchGate = smoothstep(
                    0.48h,
                    0.78h,
                    ValueNoise(floor(proceduralUv * float2(1.1, 0.72)) + steppedTime * 0.65));
                half proceduralBranch = (1.0h - smoothstep(
                    mainWidth * 0.55,
                    mainWidth * 0.55 + max(fwidth(branchDistance), 0.014),
                    branchDistance)) * branchGate;
                half proceduralBranchCore = (1.0h - smoothstep(
                    mainWidth * 0.12,
                    mainWidth * 0.12 + max(fwidth(branchDistance) * 0.6, 0.006),
                    branchDistance)) * branchGate;
                half proceduralBolt = max(proceduralMain, proceduralBranch * 0.86h)
                    * lerp(0.7h, 1.0h, breakup) * localFlicker;
                half proceduralCore = max(proceduralMainCore, proceduralBranchCore * 0.78h)
                    * lerp(0.78h, 1.0h, breakup) * localFlicker;

                // The hand-painted crawling-lightning texture owns the visible
                // shape. Dense veins and procedural lines only support it.
                half textureBolt = saturate(boltA + boltB * 0.045h + flowBolt * 0.018h)
                    * lerp(0.72h,1.0h,breakup) * localFlicker;
                half bolt = saturate(textureBolt + proceduralBolt * 0.018h);
                half core = max(
                    max(smoothstep(0.48h,0.9h,a), smoothstep(0.72h,0.96h,b) * 0.05h) * textureBolt,
                    proceduralCore * 0.015h);
                half surgeBand = pow(
                    saturate(1.0h - abs(frac(input.longitudinal * 1.46h - _Time.y * _TravelSpeed * speedBoost) - 0.5h) * 2.0h),
                    5.0h);
                bolt *= 0.82h + surgeBand * 0.52h;

                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h-saturate(dot(normalize(input.normalWS),viewDirection)),3.6h);
                half edgeArc = fresnel * bolt * (0.68h + response * 0.2h);
                half reveal = smoothstep(input.longitudinal-0.18h,input.longitudinal+0.08h,_RevealProgress);
                half alpha = saturate((bolt + edgeArc * 0.55h) * _Opacity * reveal);
                half3 color = _DarkTint.rgb * bolt * 0.16h
                    + _MainTint.rgb * (bolt * 1.5h + edgeArc)
                    + _HighlightTint.rgb * (core * 1.2h + surgeBand * core * 0.38h);
                return half4(color * _EmissionIntensity, alpha);
            }
            ENDHLSL
        }
    }
}
