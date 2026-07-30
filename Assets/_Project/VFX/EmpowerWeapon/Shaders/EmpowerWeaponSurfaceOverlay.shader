Shader "RPG Clone/VFX/Empower Weapon Surface Overlay"
{
    Properties
    {
        [MainTexture] _VeinMask("Nature Vein Mask", 2D) = "white" {}
        _FlowMask("Directional Flow Mask", 2D) = "white" {}
        _RuneMask("Runic Band Mask", 2D) = "black" {}
        _BreakupMask("Elemental Breakup Mask", 2D) = "white" {}
        _DistortionMap("Surface Distortion", 2D) = "gray" {}
        [MainColor] _MainTint("Nature Tint", Color) = (0.035,1,0.19,1)
        _SecondaryTint("Golden Rune Tint", Color) = (1,0.52,0.035,1)
        _HighlightTint("Mint Core Tint", Color) = (0.72,1,0.86,1)
        _EmissionIntensity("Surface Emission", Range(0,12)) = 4.2
        _RuneIntensity("Rune Intensity", Range(0,6)) = 1.8
        _FlowIntensity("Flow Intensity", Range(0,6)) = 2.1
        _EdgeBrightness("Edge Corona", Range(0,6)) = 1.5
        _Opacity("Overlay Opacity", Range(0,1)) = 0.92
        _PatternScale("Pattern Scale", Range(0.25,8)) = 1.05
        _RuneScale("Rune Scale", Range(0.25,8)) = 0.85
        _BreakupScale("Breakup Scale", Range(0.25,10)) = 2.4
        _PulseSpeed("Pulse Speed", Range(0,12)) = 3.1
        _TravelSpeed("Travelling Pulse Speed", Range(0,5)) = 0.72
        _PrimaryScroll("Vein Scroll", Vector) = (0.015,0.08,0,0)
        _SecondaryScroll("Flow Scroll", Vector) = (-0.025,0.24,0,0)
        _DistortionScroll("Distortion Scroll", Vector) = (0.06,0.09,0,0)
        _DistortionStrength("Surface Distortion", Range(0,0.2)) = 0.025
        _FresnelPower("Edge Tightness", Range(0.5,10)) = 4.5
        _SurfaceExtrusion("Surface Lift", Range(0,0.012)) = 0.0025
        [HideInInspector] _BoundsMin("Bounds Min", Vector) = (0,0,0,0)
        [HideInInspector] _BoundsSize("Bounds Size", Vector) = (1,1,1,0)
        [HideInInspector] _FlowAxis("Flow Axis", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+24"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "EmpowerWeaponSurface"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_VeinMask); SAMPLER(sampler_VeinMask);
            TEXTURE2D(_FlowMask); SAMPLER(sampler_FlowMask);
            TEXTURE2D(_RuneMask); SAMPLER(sampler_RuneMask);
            TEXTURE2D(_BreakupMask); SAMPLER(sampler_BreakupMask);
            TEXTURE2D(_DistortionMap); SAMPLER(sampler_DistortionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTint;
                float4 _SecondaryTint;
                float4 _HighlightTint;
                float4 _PrimaryScroll;
                float4 _SecondaryScroll;
                float4 _DistortionScroll;
                float4 _BoundsMin;
                float4 _BoundsSize;
                float4 _FlowAxis;
                float _EmissionIntensity;
                float _RuneIntensity;
                float _FlowIntensity;
                float _EdgeBrightness;
                float _Opacity;
                float _PatternScale;
                float _RuneScale;
                float _BreakupScale;
                float _PulseSpeed;
                float _TravelSpeed;
                float _DistortionStrength;
                float _FresnelPower;
                float _SurfaceExtrusion;
            CBUFFER_END

            half MaskValue(half4 sampleValue)
            {
                return saturate(dot(sampleValue.rgb, half3(0.299h, 0.587h, 0.114h)) * sampleValue.a);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 safeSize = max(abs(_BoundsSize.xyz), float3(0.0001, 0.0001, 0.0001));
                float3 normalizedPosition = saturate((input.positionOS.xyz - _BoundsMin.xyz) / safeSize);
                float3 axis = abs(_FlowAxis.xyz);
                axis /= max(0.0001, axis.x + axis.y + axis.z);
                float longitudinal = dot(normalizedPosition, axis);

                float3 normalWeight = abs(normalize(input.normalOS));
                float crossCoordinate;
                if (axis.x > axis.y && axis.x > axis.z)
                {
                    float blend = normalWeight.y / max(0.0001, normalWeight.y + normalWeight.z);
                    crossCoordinate = lerp(normalizedPosition.y, normalizedPosition.z, blend);
                }
                else if (axis.y > axis.z)
                {
                    float blend = normalWeight.x / max(0.0001, normalWeight.x + normalWeight.z);
                    crossCoordinate = lerp(normalizedPosition.x, normalizedPosition.z, blend);
                }
                else
                {
                    float blend = normalWeight.x / max(0.0001, normalWeight.x + normalWeight.y);
                    crossCoordinate = lerp(normalizedPosition.x, normalizedPosition.y, blend);
                }

                float liftPulse = 0.72 + 0.28 * sin(_Time.y * _PulseSpeed + longitudinal * 8.0);
                VertexPositionInputs positions = GetVertexPositionInputs(
                    input.positionOS.xyz + input.normalOS * (_SurfaceExtrusion * liftPulse));
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.surfaceUv = float2(crossCoordinate, longitudinal);
                output.longitudinal = longitudinal;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 distortionUv = input.surfaceUv * (_BreakupScale * 0.72)
                    + _Time.y * _DistortionScroll.xy;
                half2 distortion = SAMPLE_TEXTURE2D(
                    _DistortionMap,
                    sampler_DistortionMap,
                    distortionUv).rg * 2.0h - 1.0h;

                float2 veinUv = input.surfaceUv * _PatternScale
                    + _Time.y * _PrimaryScroll.xy
                    + distortion * _DistortionStrength;
                float2 veinUvCounter = input.surfaceUv * (_PatternScale * 1.37)
                    - _Time.y * _PrimaryScroll.yx * 0.43
                    - distortion.yx * _DistortionStrength * 0.65;
                float2 flowUv = input.surfaceUv * (_PatternScale * float2(0.82, 1.18))
                    + _Time.y * _SecondaryScroll.xy
                    + distortion * _DistortionStrength * 0.45;
                float2 runeUv = input.surfaceUv * _RuneScale
                    + float2(distortion.x * 0.018, _Time.y * 0.018);
                float2 breakupUv = input.surfaceUv * _BreakupScale
                    + float2(_Time.y * 0.012, -_Time.y * 0.019);

                half veinA = MaskValue(SAMPLE_TEXTURE2D(_VeinMask, sampler_VeinMask, veinUv));
                half veinB = MaskValue(SAMPLE_TEXTURE2D(_VeinMask, sampler_VeinMask, veinUvCounter));
                half flow = MaskValue(SAMPLE_TEXTURE2D(_FlowMask, sampler_FlowMask, flowUv));
                half rune = MaskValue(SAMPLE_TEXTURE2D(_RuneMask, sampler_RuneMask, runeUv));
                half breakup = MaskValue(SAMPLE_TEXTURE2D(_BreakupMask, sampler_BreakupMask, breakupUv));

                half travellingBand = pow(
                    saturate(1.0h - abs(frac(input.longitudinal * 1.22h - _Time.y * _TravelSpeed) - 0.5h) * 2.0h),
                    4.0h);
                half pulse = 0.72h + 0.28h * sin(_Time.y * _PulseSpeed + input.longitudinal * 9.0h + veinB * 4.0h);
                half veinSample = max(veinA, veinB * 0.52h);
                half vein = smoothstep(0.12h, 0.72h, veinSample) * lerp(0.58h, 1.0h, breakup);
                half veinCore = smoothstep(0.66h, 0.96h, vein);
                half veinRim = saturate(smoothstep(0.12h, 0.5h, vein) - veinCore * 0.72h);
                half flowEnergy = smoothstep(0.14h, 0.74h, flow)
                    * lerp(0.42h, 1.0h, travellingBand) * (0.62h + breakup * 0.38h);
                half runeEnergy = smoothstep(0.14h, 0.68h, rune) * (0.62h + 0.38h * pulse)
                    * lerp(0.52h, 1.0h, breakup);
                half surfaceCharge = breakup * (0.1h + travellingBand * 0.12h);

                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(
                    1.0h - saturate(dot(normalize(input.normalWS), viewDirection)),
                    _FresnelPower);
                half coverage = saturate(vein * 0.78h + flowEnergy * 0.68h + runeEnergy * 0.64h);
                half edgeCorona = fresnel * coverage * _EdgeBrightness;

                half3 color =
                    _MainTint.rgb * (veinRim * 0.88h + vein * 0.36h)
                    + _SecondaryTint.rgb * runeEnergy * _RuneIntensity
                    + _HighlightTint.rgb * (veinCore * 1.35h + flowEnergy * _FlowIntensity
                        + travellingBand * flow * 0.5h)
                    + _MainTint.rgb * surfaceCharge
                    + lerp(_MainTint.rgb, _HighlightTint.rgb, 0.65h) * edgeCorona;
                half alpha = saturate(
                    (vein * 0.78h + flowEnergy * 0.84h + runeEnergy * 0.8h
                        + surfaceCharge * 0.5h + edgeCorona * 0.48h)
                    * _Opacity);

                return half4(color * _EmissionIntensity * pulse, alpha);
            }
            ENDHLSL
        }
    }
}
