Shader "RPG Clone/Environment/Scenic Bark Triplanar"
{
    Properties
    {
        [Header(Bark Surface)]
        [MainTexture] _BaseMap ("Seamless Bark Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Zone Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.16
        _Occlusion ("Ambient Occlusion", Range(0, 1)) = 0.92

        [Header(Scale Independent Projection)]
        _WorldTileSize ("World Tile Size (smaller = more detail)", Range(0.25, 12)) = 2.5
        _ProjectionOffset ("World Projection Offset", Vector) = (0, 0, 0, 0)
        _TriplanarBlendSharpness ("Triplanar Blend Sharpness", Range(1, 12)) = 5

        [Header(Seam and Repetition Control)]
        _AntiTilingStrength ("Per-Tile Offset Variation", Range(0, 1)) = 0.28
        _TileEdgeBlendStrength ("Tile Edge Blend", Range(0, 0.75)) = 0.22
        _MacroVariationScale ("Macro Variation Size", Range(4, 64)) = 18
        _MacroVariationStrength ("Macro Variation Strength", Range(0, 0.35)) = 0.10

        [Header(Painted Relief)]
        _DetailNormalStrength ("Painted Detail Normal Strength", Range(0, 2)) = 0.42

        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _AlphaClip ("Alpha Clip", Float) = 0
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _Blend ("Blend", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            Blend One Zero
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogCoord : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _ProjectionOffset;
                float _WorldTileSize;
                float _TriplanarBlendSharpness;
                float _AntiTilingStrength;
                float _TileEdgeBlendStrength;
                float _MacroVariationScale;
                float _MacroVariationStrength;
                float _DetailNormalStrength;
                half _Smoothness;
                half _Occlusion;
                half _Cutoff;
                half _Cull;
                half _AlphaClip;
                half _Surface;
                half _Blend;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
            CBUFFER_END

            float2 Hash22(float2 value)
            {
                float3 p = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.xx + p.yz) * p.zy);
            }

            float Hash13(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.zyx + 31.32);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                float3 blend = local * local * (3.0 - 2.0 * local);

                float n000 = Hash13(cell);
                float n100 = Hash13(cell + float3(1, 0, 0));
                float n010 = Hash13(cell + float3(0, 1, 0));
                float n110 = Hash13(cell + float3(1, 1, 0));
                float n001 = Hash13(cell + float3(0, 0, 1));
                float n101 = Hash13(cell + float3(1, 0, 1));
                float n011 = Hash13(cell + float3(0, 1, 1));
                float n111 = Hash13(cell + float3(1, 1, 1));

                float nearPlane = lerp(lerp(n000, n100, blend.x), lerp(n010, n110, blend.x), blend.y);
                float farPlane = lerp(lerp(n001, n101, blend.x), lerp(n011, n111, blend.x), blend.y);
                return lerp(nearPlane, farPlane, blend.z);
            }

            float2 OffsetTileUv(float2 localUv, float2 tileId, float seed)
            {
                float2 randomValue = Hash22(tileId + seed);
                return localUv + (randomValue - 0.5) * _AntiTilingStrength;
            }

            half3 SampleEdgeBlendedTile(float2 uv, float seed)
            {
                if (_AntiTilingStrength < 0.001 || _TileEdgeBlendStrength < 0.001)
                {
                    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                }

                float2 tileId = floor(uv);
                float2 tileUv = frac(uv);
                float edgeWidth = max(_TileEdgeBlendStrength * 0.5, 0.001);
                float2 blend = smoothstep(1.0 - edgeWidth, 1.0, tileUv);

                half3 sample00 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, OffsetTileUv(uv - tileId, tileId, seed)).rgb;
                half3 sample10 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, OffsetTileUv(uv - (tileId + float2(1, 0)), tileId + float2(1, 0), seed)).rgb;
                half3 sample01 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, OffsetTileUv(uv - (tileId + float2(0, 1)), tileId + float2(0, 1), seed)).rgb;
                half3 sample11 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, OffsetTileUv(uv - (tileId + float2(1, 1)), tileId + float2(1, 1), seed)).rgb;

                return lerp(lerp(sample00, sample10, blend.x), lerp(sample01, sample11, blend.x), blend.y);
            }

            half3 GetTriplanarWeights(half3 normalWS)
            {
                half3 weights = pow(abs(normalWS), max(_TriplanarBlendSharpness, 1.0));
                return weights / max(dot(weights, half3(1, 1, 1)), 0.0001);
            }

            half3 SampleBarkTriplanar(float3 positionWS, half3 normalWS)
            {
                float tileSize = max(_WorldTileSize, 0.001);
                float3 projectedPosition = positionWS + _ProjectionOffset.xyz;
                half3 weights = GetTriplanarWeights(normalWS);

                // Vertical faces keep the bark grain upright; top-facing surfaces use XZ.
                float2 uvX = float2(projectedPosition.z * sign(normalWS.x + 0.0001), projectedPosition.y) / tileSize;
                float2 uvY = float2(projectedPosition.x, projectedPosition.z * sign(normalWS.y + 0.0001)) / tileSize;
                float2 uvZ = float2(-projectedPosition.x * sign(normalWS.z + 0.0001), projectedPosition.y) / tileSize;

                half3 xSample = SampleEdgeBlendedTile(uvX, 19.0);
                half3 ySample = SampleEdgeBlendedTile(uvY, 47.0);
                half3 zSample = SampleEdgeBlendedTile(uvZ, 83.0);
                return xSample * weights.x + ySample * weights.y + zSample * weights.z;
            }

            half3 PerturbNormalFromPaintedHeight(float3 positionWS, half3 normalWS, half height)
            {
                if (_DetailNormalStrength < 0.001)
                {
                    return normalWS;
                }

                float3 positionDerivativeX = ddx(positionWS);
                float3 positionDerivativeY = ddy(positionWS);
                float heightDerivativeX = ddx(height);
                float heightDerivativeY = ddy(height);
                float3 crossY = cross(positionDerivativeY, normalWS);
                float3 crossX = cross(normalWS, positionDerivativeX);
                float determinant = dot(positionDerivativeX, crossY);
                float3 surfaceGradient = sign(determinant) * (heightDerivativeX * crossY + heightDerivativeY * crossX);
                return normalize(abs(determinant) * normalWS - surfaceGradient * _DetailNormalStrength);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 baseNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 albedo = SampleBarkTriplanar(input.positionWS, baseNormalWS) * _BaseColor.rgb;

                float macroScale = max(_MacroVariationScale, 0.001);
                float macroNoise = ValueNoise3D((input.positionWS + _ProjectionOffset.xyz) / macroScale);
                half macroMultiplier = lerp(1.0h, lerp(0.82h, 1.18h, (half)macroNoise), (half)_MacroVariationStrength);
                albedo *= macroMultiplier;

                half paintedHeight = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
                half3 normalWS = PerturbNormalFromPaintedHeight(input.positionWS, baseNormalWS, paintedHeight);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = input.fogCoord;
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0.04h, 0.04h, 0.04h);
                surfaceData.metallic = 0.0h;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = _Occlusion;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}