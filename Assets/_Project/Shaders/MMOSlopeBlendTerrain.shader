Shader "RPG Clone/Terrain/MMO Slope Blend Terrain"
{
    Properties
    {
        [MainTexture] _FlatTex ("Flat Texture", 2D) = "white" {}
        _FlatVariationTex ("Flat Variation Texture", 2D) = "white" {}
        _SteepTex ("Steep Texture", 2D) = "gray" {}
        _PathTex ("Painted Path Texture", 2D) = "white" {}
        [HideInInspector] _Control ("Terrain Control Map", 2D) = "black" {}
        [HideInInspector] _TerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
        _FlatTint ("Flat Texture Tint", Color) = (1, 1, 1, 1)
        _FlatVariationTint ("Flat Variation Texture Tint", Color) = (1, 1, 1, 1)
        _SteepTint ("Steep Texture Tint", Color) = (1, 1, 1, 1)
        _PathTint ("Painted Path Tint", Color) = (1, 1, 1, 1)
        _FlatTilingSize ("Flat Texture Tiling Size", Range(0.25, 128)) = 7
        _FlatVariationTilingSize ("Flat Variation Texture Tiling Size", Range(0.25, 128)) = 9
        _FlatVariationBlendStrength ("Flat Variation Blend Strength", Range(0, 1)) = 0.35
        _FlatVariationNoiseSize ("Flat Variation Noise Size", Range(4, 256)) = 38
        _FlatVariationNoiseSoftness ("Flat Variation Noise Softness", Range(0.01, 0.5)) = 0.18
        _SteepTilingSize ("Steep Texture Tiling Size", Range(0.25, 128)) = 5
        _PathTilingSize ("Painted Path Tiling Size", Range(0.25, 128)) = 8
        _PathBlendStrength ("Painted Path Blend Strength", Range(0, 1)) = 1
        _SlopeBlendThreshold ("Slope Blend Threshold", Range(0, 1)) = 0.38
        _SlopeBlendSoftness ("Slope Blend Softness", Range(0.001, 0.5)) = 0.16
        _TextureRandomizationStrength ("Texture Rotation/Randomization Strength", Range(0, 1)) = 0.65
        _TileEdgeBlendStrength ("Tile Edge Blending Strength", Range(0, 1)) = 0.28
        _MacroVariationStrength ("Same-Level Variation Strength", Range(0, 1)) = 0.22
        _MacroVariationSize ("Same-Level Variation Size", Range(8, 256)) = 64
        _TriplanarBlendSharpness ("Triplanar Projection Sharpness", Range(1, 8)) = 4
        _Smoothness ("Surface Smoothness", Range(0, 1)) = 0.18
        _Occlusion ("Ambient Occlusion", Range(0, 1)) = 1
    }

    HLSLINCLUDE
        #pragma multi_compile_fragment __ _ALPHATEST_ON
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-100"
            "UniversalMaterialType" = "Lit"
            "TerrainCompatible" = "True"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            Blend One Zero
            BlendOp Add
            ZWrite On
            ZTest LEqual
            AlphaToMask Off

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
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogCoord : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float2 terrainUV : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_FlatTex);
            SAMPLER(sampler_FlatTex);
            TEXTURE2D(_FlatVariationTex);
            SAMPLER(sampler_FlatVariationTex);
            TEXTURE2D(_SteepTex);
            SAMPLER(sampler_SteepTex);
            TEXTURE2D(_PathTex);
            SAMPLER(sampler_PathTex);
            TEXTURE2D(_Control);
            SAMPLER(sampler_Control);

            #ifdef _ALPHATEST_ON
                TEXTURE2D(_TerrainHolesTexture);
                SAMPLER(sampler_TerrainHolesTexture);

                void ClipTerrainHoles(float2 terrainUV)
                {
                    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, terrainUV).r;
                    clip(hole < 0.0005 ? -1.0 : 1.0);
                }
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _FlatTint;
                half4 _FlatVariationTint;
                half4 _SteepTint;
                half4 _PathTint;
                float _FlatTilingSize;
                float _FlatVariationTilingSize;
                float _FlatVariationBlendStrength;
                float _FlatVariationNoiseSize;
                float _FlatVariationNoiseSoftness;
                float _SteepTilingSize;
                float _PathTilingSize;
                float _PathBlendStrength;
                float _SlopeBlendThreshold;
                float _SlopeBlendSoftness;
                float _TextureRandomizationStrength;
                float _TileEdgeBlendStrength;
                float _MacroVariationStrength;
                float _MacroVariationSize;
                float _TriplanarBlendSharpness;
                half _Smoothness;
                half _Occlusion;
            CBUFFER_END

            CBUFFER_START(_Terrain)
                #ifdef UNITY_INSTANCING_ENABLED
                    float4 _TerrainHeightmapRecipSize;
                #endif
                float4 _Control_TexelSize;
                float4 _TerrainHeightmapScale;
            CBUFFER_END

            #ifdef UNITY_INSTANCING_ENABLED
                TEXTURE2D(_TerrainHeightmapTexture);
                TEXTURE2D(_TerrainNormalmapTexture);

                UNITY_INSTANCING_BUFFER_START(Terrain)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
                UNITY_INSTANCING_BUFFER_END(Terrain)
            #endif

            void ApplyUnityTerrainInstancing(inout float4 positionOS, inout float3 normalOS, inout float2 terrainUV)
            {
                #ifdef UNITY_INSTANCING_ENABLED
                    float2 patchVertex = positionOS.xy;
                    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                    float2 sampleCoords = (patchVertex + instanceData.xy) * instanceData.z;
                    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

                    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                    positionOS.y = height * _TerrainHeightmapScale.y;
                    normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
                    terrainUV = sampleCoords * _TerrainHeightmapRecipSize.zw;
                #endif
            }

            float Hash12(float2 value)
            {
                float3 p = frac(float3(value.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float2 Hash22(float2 value)
            {
                float3 p = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.xx + p.yz) * p.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash12(i);
                float b = Hash12(i + float2(1.0, 0.0));
                float c = Hash12(i + float2(0.0, 1.0));
                float d = Hash12(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 RotateTileUv(float2 uv, float2 tileId, float randomizationStrength)
            {
                float2 randomValue = Hash22(tileId);
                float angle = randomValue.x * TWO_PI * saturate(randomizationStrength);
                float sineValue;
                float cosineValue;
                sincos(angle, sineValue, cosineValue);

                float2 centeredUv = uv - 0.5;
                float2 rotatedUv = float2(
                    centeredUv.x * cosineValue - centeredUv.y * sineValue,
                    centeredUv.x * sineValue + centeredUv.y * cosineValue
                ) + 0.5;

                return rotatedUv + (randomValue.yx - 0.5) * saturate(randomizationStrength);
            }

            half3 SampleRandomizedTile(TEXTURE2D_PARAM(sourceTexture, sourceSampler), float2 uv, float randomizationStrength, float edgeBlendStrength)
            {
                float2 tileId = floor(uv);
                float2 tileUv = frac(uv);
                float edgeWidth = max(saturate(edgeBlendStrength) * 0.5, 0.001);
                float2 blend = smoothstep(1.0 - edgeWidth, 1.0, tileUv);

                float2 tileId00 = tileId;
                float2 tileId10 = tileId + float2(1.0, 0.0);
                float2 tileId01 = tileId + float2(0.0, 1.0);
                float2 tileId11 = tileId + float2(1.0, 1.0);

                half3 sample00 = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, RotateTileUv(uv - tileId00, tileId00, randomizationStrength)).rgb;
                half3 sample10 = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, RotateTileUv(uv - tileId10, tileId10, randomizationStrength)).rgb;
                half3 sample01 = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, RotateTileUv(uv - tileId01, tileId01, randomizationStrength)).rgb;
                half3 sample11 = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, RotateTileUv(uv - tileId11, tileId11, randomizationStrength)).rgb;

                return lerp(lerp(sample00, sample10, blend.x), lerp(sample01, sample11, blend.x), blend.y);
            }

            half3 GetTriplanarWeights(half3 normalWS)
            {
                half3 weights = pow(abs(normalWS), max(_TriplanarBlendSharpness, 1.0));
                return weights / max(dot(weights, half3(1.0, 1.0, 1.0)), 0.0001);
            }

            half3 SampleTriplanarTerrainTexture(TEXTURE2D_PARAM(sourceTexture, sourceSampler), float3 positionWS, half3 normalWS, float tilingSize)
            {
                float safeTilingSize = max(tilingSize, 0.001);
                half3 weights = GetTriplanarWeights(normalWS);

                float2 uvX = positionWS.zy / safeTilingSize;
                float2 uvY = positionWS.xz / safeTilingSize;
                float2 uvZ = positionWS.xy / safeTilingSize;

                uvX.x *= sign(normalWS.x + 0.0001);
                uvY.x *= sign(normalWS.y + 0.0001);
                uvZ.x *= -sign(normalWS.z + 0.0001);

                half3 xSample = SampleRandomizedTile(TEXTURE2D_ARGS(sourceTexture, sourceSampler), uvX, _TextureRandomizationStrength, _TileEdgeBlendStrength);
                half3 ySample = SampleRandomizedTile(TEXTURE2D_ARGS(sourceTexture, sourceSampler), uvY, _TextureRandomizationStrength, _TileEdgeBlendStrength);
                half3 zSample = SampleRandomizedTile(TEXTURE2D_ARGS(sourceTexture, sourceSampler), uvZ, _TextureRandomizationStrength, _TileEdgeBlendStrength);

                return xSample * weights.x + ySample * weights.y + zSample * weights.z;
            }

            float GetTriplanarValueNoise(float3 positionWS, half3 normalWS, float noiseSize)
            {
                float safeNoiseSize = max(noiseSize, 0.001);
                half3 weights = GetTriplanarWeights(normalWS);
                float noiseX = ValueNoise(positionWS.zy / safeNoiseSize);
                float noiseY = ValueNoise(positionWS.xz / safeNoiseSize);
                float noiseZ = ValueNoise(positionWS.xy / safeNoiseSize);
                return noiseX * weights.x + noiseY * weights.y + noiseZ * weights.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                float2 terrainUV = input.texcoord;
                ApplyUnityTerrainInstancing(positionOS, normalOS, terrainUV);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                output.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                output.terrainUV = terrainUV;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    ClipTerrainHoles(input.terrainUV);
                #endif

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half upFacing = saturate(dot(normalWS, half3(0.0, 1.0, 0.0)));
                float slope = 1.0 - upFacing;
                float slopeSoftness = max(_SlopeBlendSoftness, 0.0001);
                half steepBlend = smoothstep(_SlopeBlendThreshold - slopeSoftness, _SlopeBlendThreshold + slopeSoftness, slope);

                half3 flatColor = SampleTriplanarTerrainTexture(TEXTURE2D_ARGS(_FlatTex, sampler_FlatTex), input.positionWS, normalWS, _FlatTilingSize) * _FlatTint.rgb;
                half3 flatVariationColor = SampleTriplanarTerrainTexture(TEXTURE2D_ARGS(_FlatVariationTex, sampler_FlatVariationTex), input.positionWS, normalWS, _FlatVariationTilingSize) * _FlatVariationTint.rgb;
                float flatVariationNoise = GetTriplanarValueNoise(input.positionWS + float3(41.0, 0.0, 17.0), normalWS, _FlatVariationNoiseSize);
                float flatVariationSoftness = max(_FlatVariationNoiseSoftness, 0.001);
                half flatVariationMask = smoothstep(0.5 - flatVariationSoftness, 0.5 + flatVariationSoftness, flatVariationNoise);
                flatColor = lerp(flatColor, flatVariationColor, flatVariationMask * saturate(_FlatVariationBlendStrength));

                half3 steepColor = SampleTriplanarTerrainTexture(TEXTURE2D_ARGS(_SteepTex, sampler_SteepTex), input.positionWS, normalWS, _SteepTilingSize) * _SteepTint.rgb;
                half3 albedo = lerp(flatColor, steepColor, steepBlend);
                float2 splatUV = (saturate(input.terrainUV) * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
                half3 pathColor = SampleTriplanarTerrainTexture(TEXTURE2D_ARGS(_PathTex, sampler_PathTex), input.positionWS, normalWS, _PathTilingSize) * _PathTint.rgb;
                half pathMask = saturate(SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV).a * _PathBlendStrength);
                albedo = lerp(albedo, pathColor, pathMask);

                float macroNoise = GetTriplanarValueNoise(input.positionWS, normalWS, _MacroVariationSize);
                half macroVariation = lerp(1.0h, (half)lerp(0.82, 1.18, macroNoise), (half)_MacroVariationStrength);
                albedo *= macroVariation;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif
                inputData.fogCoord = input.fogCoord;
                inputData.vertexLighting = half3(0.0, 0.0, 0.0);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.metallic = 0.0h;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.emission = half3(0.0, 0.0, 0.0);
                surfaceData.occlusion = _Occlusion;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainShadowPassVertex
            #pragma fragment TerrainShadowPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(_Terrain)
                #ifdef UNITY_INSTANCING_ENABLED
                    float4 _TerrainHeightmapRecipSize;
                #endif
                float4 _TerrainHeightmapScale;
            CBUFFER_END

            #ifdef _ALPHATEST_ON
                TEXTURE2D(_TerrainHolesTexture);
                SAMPLER(sampler_TerrainHolesTexture);

                void ClipTerrainHoles(float2 terrainUV)
                {
                    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, terrainUV).r;
                    clip(hole < 0.0005 ? -1.0 : 1.0);
                }
            #endif

            #ifdef UNITY_INSTANCING_ENABLED
                TEXTURE2D(_TerrainHeightmapTexture);
                TEXTURE2D(_TerrainNormalmapTexture);

                UNITY_INSTANCING_BUFFER_START(Terrain)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
                UNITY_INSTANCING_BUFFER_END(Terrain)
            #endif

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 terrainUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void ApplyUnityTerrainInstancingShadow(inout float4 positionOS, inout float3 normalOS, inout float2 terrainUV)
            {
                #ifdef UNITY_INSTANCING_ENABLED
                    float2 patchVertex = positionOS.xy;
                    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                    float2 sampleCoords = (patchVertex + instanceData.xy) * instanceData.z;
                    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

                    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                    positionOS.y = height * _TerrainHeightmapScale.y;
                    normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
                    terrainUV = sampleCoords * _TerrainHeightmapRecipSize.zw;
                #endif
            }

            ShadowVaryings TerrainShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                float2 terrainUV = input.texcoord;
                ApplyUnityTerrainInstancingShadow(positionOS, normalOS, terrainUV);

                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.terrainUV = terrainUV;
                return output;
            }

            half4 TerrainShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    ClipTerrainHoles(input.terrainUV);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Blend One Zero
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainDepthOnlyVertex
            #pragma fragment TerrainDepthOnlyFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(_Terrain)
                #ifdef UNITY_INSTANCING_ENABLED
                    float4 _TerrainHeightmapRecipSize;
                #endif
                float4 _TerrainHeightmapScale;
            CBUFFER_END

            #ifdef _ALPHATEST_ON
                TEXTURE2D(_TerrainHolesTexture);
                SAMPLER(sampler_TerrainHolesTexture);

                void ClipTerrainHoles(float2 terrainUV)
                {
                    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, terrainUV).r;
                    clip(hole < 0.0005 ? -1.0 : 1.0);
                }
            #endif

            #ifdef UNITY_INSTANCING_ENABLED
                TEXTURE2D(_TerrainHeightmapTexture);
                TEXTURE2D(_TerrainNormalmapTexture);

                UNITY_INSTANCING_BUFFER_START(Terrain)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
                UNITY_INSTANCING_BUFFER_END(Terrain)
            #endif

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 terrainUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void ApplyUnityTerrainInstancingDepth(inout float4 positionOS, inout float3 normalOS, inout float2 terrainUV)
            {
                #ifdef UNITY_INSTANCING_ENABLED
                    float2 patchVertex = positionOS.xy;
                    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                    float2 sampleCoords = (patchVertex + instanceData.xy) * instanceData.z;
                    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

                    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                    positionOS.y = height * _TerrainHeightmapScale.y;
                    normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
                    terrainUV = sampleCoords * _TerrainHeightmapRecipSize.zw;
                #endif
            }

            DepthVaryings TerrainDepthOnlyVertex(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                float2 terrainUV = input.texcoord;
                ApplyUnityTerrainInstancingDepth(positionOS, normalOS, terrainUV);

                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.terrainUV = terrainUV;
                return output;
            }

            half TerrainDepthOnlyFragment(DepthVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    ClipTerrainHoles(input.terrainUV);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Blend One Zero
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TerrainDepthNormalsVertex
            #pragma fragment TerrainDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            CBUFFER_START(_Terrain)
                #ifdef UNITY_INSTANCING_ENABLED
                    float4 _TerrainHeightmapRecipSize;
                #endif
                float4 _TerrainHeightmapScale;
            CBUFFER_END

            #ifdef _ALPHATEST_ON
                TEXTURE2D(_TerrainHolesTexture);
                SAMPLER(sampler_TerrainHolesTexture);

                void ClipTerrainHoles(float2 terrainUV)
                {
                    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, terrainUV).r;
                    clip(hole < 0.0005 ? -1.0 : 1.0);
                }
            #endif

            #ifdef UNITY_INSTANCING_ENABLED
                TEXTURE2D(_TerrainHeightmapTexture);
                TEXTURE2D(_TerrainNormalmapTexture);

                UNITY_INSTANCING_BUFFER_START(Terrain)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
                UNITY_INSTANCING_BUFFER_END(Terrain)
            #endif

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float2 terrainUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void ApplyUnityTerrainInstancingDepthNormals(inout float4 positionOS, inout float3 normalOS, inout float2 terrainUV)
            {
                #ifdef UNITY_INSTANCING_ENABLED
                    float2 patchVertex = positionOS.xy;
                    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
                    float2 sampleCoords = (patchVertex + instanceData.xy) * instanceData.z;
                    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

                    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
                    positionOS.y = height * _TerrainHeightmapScale.y;
                    normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
                    terrainUV = sampleCoords * _TerrainHeightmapRecipSize.zw;
                #endif
            }

            DepthNormalsVaryings TerrainDepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                float2 terrainUV = input.texcoord;
                ApplyUnityTerrainInstancingDepthNormals(positionOS, normalOS, terrainUV);

                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(normalOS));
                output.terrainUV = terrainUV;
                return output;
            }

            void TerrainDepthNormalsFragment(
                DepthNormalsVaryings input,
                out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                    , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    ClipTerrainHoles(input.terrainUV);
                #endif

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    outNormalWS = half4(packedNormalWS, 0.0);
                #else
                    outNormalWS = half4(normalWS, 0.0);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
