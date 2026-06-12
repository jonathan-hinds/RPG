using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.World.Foliage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOClassicGrassFoliageBuilder
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs/World/Foliage";
        private const string GeneratedFolder = RootFolder + "/Generated/Foliage";
        private const string MaterialFolder = GeneratedFolder + "/Materials";
        private const string MeshFolder = GeneratedFolder + "/Meshes";
        private const string PrefabFolder = GeneratedFolder + "/Prefabs";
        private const string ProfilePath = ConfigFolder + "/ClassicGrassFoliageProfile.asset";
        private static readonly Quaternion ModelDetailMeshRotation = Quaternion.Euler(-90f, 0f, 0f);

        private static readonly DefaultVariationDefinition[] DefaultVariations =
        {
            DefaultVariationDefinition.Texture("Classic Grass 01", "Assets/grass-01.png", 0.68f, 1.08f, 0.62f, 1.18f, 1, 73, 0.018f, 0.46f, 0.095f, 0.24f, 0.35f),
            DefaultVariationDefinition.Texture("Classic Grass 02", "Assets/grass-02.png", 0.71f, 1.12f, 0.67f, 1.26f, 1, 174, 0.0215f, 0.485f, 0.113f, 0.258f, 0.35f),
            DefaultVariationDefinition.Texture("Classic Grass 03", "Assets/grass-03.png", 0.74f, 1.16f, 0.72f, 1.34f, 1, 275, 0.025f, 0.51f, 0.131f, 0.276f, 0.35f),
            DefaultVariationDefinition.Texture("Classic Grass 04", "Assets/grass-04.png", 0.77f, 1.2f, 0.77f, 1.42f, 1, 376, 0.0285f, 0.535f, 0.149f, 0.294f, 0.35f),
            DefaultVariationDefinition.Model("Classic Bush 01", "Assets/bushes/Bush1/Untitled.fbx", 0.85f, 1.2f, 0.85f, 1.25f, 1, 517, 0.014f, 0.63f, 0.082f, 0.34f, 0.60f),
            DefaultVariationDefinition.Model("Classic Bush 02", "Assets/bushes/bush2/bush2.fbx", 0.9f, 1.25f, 0.9f, 1.3f, 1, 619, 0.012f, 0.66f, 0.078f, 0.37f, 0.60f),
            DefaultVariationDefinition.Model("Classic Bush 03", "Assets/bushes/Bush3/bush3.fbx", 0.8f, 1.15f, 0.8f, 1.2f, 1, 727, 0.016f, 0.61f, 0.087f, 0.32f, 0.60f)
        };

        [MenuItem("Tools/RPG Clone/Apply Classic Grass Foliage")]
        public static void ApplyToActiveTerrain()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Classic grass foliage could not be applied because no Terrain exists in the active scene.");
                return;
            }

            ApplyToTerrain(terrain);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/RPG Clone/Prepare Classic Grass For Manual Painting")]
        public static void PrepareActiveTerrainForManualPainting()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Classic grass foliage could not be prepared because no Terrain exists in the active scene.");
                return;
            }

            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            ConfigureTerrainDetailRendering(terrain, profile);
            BuiltFoliageVariation[] builtVariations = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, builtVariations);
            ClearAllDetailLayers(terrain.terrainData);

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prepared {terrain.name} for sparse manual classic grass painting. Detail prototypes were kept and painted instances were cleared.");
        }

        [MenuItem("Tools/RPG Clone/Sync Classic Foliage Details For Painting")]
        public static void SyncActiveTerrainDetailsForPainting()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Classic foliage details could not be synced because no Terrain exists in the active scene.");
                return;
            }

            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            ConfigureTerrainDetailRendering(terrain, profile);
            BuiltFoliageVariation[] builtVariations = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, builtVariations);

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Synced {builtVariations.Length} classic foliage detail prototypes on {terrain.name} for manual painting.");
        }

        [MenuItem("Tools/RPG Clone/Refresh Classic Grass Materials")]
        public static void RefreshClassicGrassMaterials()
        {
            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            BuildVariationPrefabs(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Refreshed classic grass materials with lit alpha-cutout shadow settings.");
        }


        [MenuItem("Tools/RPG Clone/Log Classic Grass Foliage Summary")]
        public static void LogActiveTerrainFoliageSummary()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogWarning("Classic grass foliage summary could not find a Terrain in the active scene.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            long total = 0;
            string summary = $"Classic grass foliage summary: prototypes={terrainData.detailPrototypes.Length}, resolution={terrainData.detailWidth}x{terrainData.detailHeight}";
            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                int[,] details = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, layer);
                long layerTotal = 0;
                for (int z = 0; z < terrainData.detailHeight; z++)
                {
                    for (int x = 0; x < terrainData.detailWidth; x++)
                    {
                        layerTotal += details[z, x];
                    }
                }

                total += layerTotal;
                summary += $", layer{layer}={layerTotal}";
            }

            summary += $", total={total}, drawFoliage={terrain.drawTreesAndFoliage}, density={terrain.detailObjectDensity}, distance={terrain.detailObjectDistance}";
            Debug.Log(summary);
        }

        [MenuItem("Tools/RPG Clone/Foliage/Paint Bush Detail Verification Patch")]
        public static void PaintBushDetailVerificationPatch()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Classic bush detail verification could not run because no Terrain exists in the active scene.");
                return;
            }

            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            ConfigureTerrainDetailRendering(terrain, profile);
            BuiltFoliageVariation[] builtVariations = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, builtVariations);
            int paintedLayers = PaintDetailVerificationPatch(terrain, profile, "Bush");

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Painted a classic bush detail verification patch on {terrain.name}. Bush layers painted={paintedLayers}.");
        }

        [MenuItem("Tools/RPG Clone/Foliage/Validate Detail Prototype Paintability")]
        public static void ValidateDetailPrototypePaintability()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Classic foliage detail validation could not run because no Terrain exists in the active scene.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            DetailPrototype[] prototypes = terrainData.detailPrototypes;
            List<string> failures = new();
            for (int i = 0; i < prototypes.Length; i++)
            {
                DetailPrototype prototype = prototypes[i];
                if (!prototype.Validate(out string errorMessage))
                {
                    failures.Add($"layer {i}: {errorMessage}");
                }

                if (prototype.prototype != null
                    && prototype.prototype.TryGetComponent(out Renderer renderer)
                    && renderer.sharedMaterials.Length != 1)
                {
                    failures.Add($"layer {i}: renderer has {renderer.sharedMaterials.Length} material slots");
                }
            }

            if (failures.Count > 0)
            {
                Debug.LogError("Classic foliage detail paintability validation failed: " + string.Join("; ", failures));
                return;
            }

            Debug.Log($"Classic foliage detail paintability validation passed for {prototypes.Length} prototypes on {terrain.name}.");
        }

        [MenuItem("Tools/RPG Clone/Clear Painted Classic Grass Foliage")]
        public static void ClearPaintedFoliageFromActiveTerrain()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogWarning("Classic grass foliage clear could not find a Terrain in the active scene.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            ClearAllDetailLayers(terrainData);

            EditorUtility.SetDirty(terrainData);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"Cleared painted classic grass foliage from {terrain.name}. Detail prototypes were kept for manual painting.");
        }

        public static void ApplyToStarterWorldScene()
        {
            const string scenePath = "Assets/Scenes/OrcishStarterValley.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ApplyToActiveTerrain();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        }

        public static void ApplyToTerrain(Terrain terrain)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            ConfigureTerrainDetailRendering(terrain, profile);

            BuiltFoliageVariation[] builtVariations = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, builtVariations);
            PaintSparsePatchyDetails(terrain, profile);

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.Log($"Applied {profile.variations.Count} classic MMO grass foliage variations to {terrain.name}.");
        }

        private static MMOClassicGrassFoliageProfile EnsureDefaultProfile()
        {
            MMOClassicGrassFoliageProfile profile = AssetDatabase.LoadAssetAtPath<MMOClassicGrassFoliageProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOClassicGrassFoliageProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            List<MMOClassicGrassFoliageVariation> ordered = new();
            for (int i = 0; i < DefaultVariations.Length; i++)
            {
                DefaultVariationDefinition definition = DefaultVariations[i];
                Texture2D texture = null;
                GameObject modelPrefab = null;
                if (definition.kind == FoliageVariationKind.Texture)
                {
                    texture = LoadConfiguredFoliageTexture(definition.assetPath);
                    if (texture == null)
                    {
                        Debug.LogWarning($"Classic foliage texture is missing: {definition.assetPath}");
                        continue;
                    }
                }
                else
                {
                    modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.assetPath);
                    if (modelPrefab == null)
                    {
                        Debug.LogWarning($"Classic foliage model is missing: {definition.assetPath}");
                        continue;
                    }
                }

                MMOClassicGrassFoliageVariation variation = FindExistingVariation(profile, definition.displayName, texture, modelPrefab, out bool wasCreated);
                ApplyDefaultVariation(definition, texture, modelPrefab, variation, wasCreated);
                ordered.Add(variation);
            }

            for (int i = 0; i < profile.variations.Count; i++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[i];
                if (variation != null && !ordered.Contains(variation))
                {
                    ordered.Add(variation);
                }
            }

            profile.variations.Clear();
            profile.variations.AddRange(ordered);
            profile.opacity = 0.35f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOClassicGrassFoliageVariation FindExistingVariation(
            MMOClassicGrassFoliageProfile profile,
            string displayName,
            Texture2D texture,
            GameObject modelPrefab,
            out bool wasCreated)
        {
            for (int i = 0; i < profile.variations.Count; i++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[i];
                if (variation == null)
                {
                    continue;
                }

                if (variation.displayName == displayName
                    || (texture != null && variation.texture == texture)
                    || (modelPrefab != null && variation.modelPrefab == modelPrefab))
                {
                    wasCreated = false;
                    return variation;
                }
            }

            MMOClassicGrassFoliageVariation created = new();
            profile.variations.Add(created);
            wasCreated = true;
            return created;
        }

        private static void ApplyDefaultVariation(
            DefaultVariationDefinition definition,
            Texture2D texture,
            GameObject modelPrefab,
            MMOClassicGrassFoliageVariation variation,
            bool resetAuthoringValues)
        {
            variation.displayName = definition.displayName;
            variation.texture = texture;
            variation.modelPrefab = modelPrefab;

            if (!resetAuthoringValues)
            {
                ClampAuthoredVariationValues(variation, definition);
                return;
            }

            variation.minWidth = definition.minWidth;
            variation.maxWidth = definition.maxWidth;
            variation.minHeight = definition.minHeight;
            variation.maxHeight = definition.maxHeight;
            variation.maxDensityPerCell = definition.maxDensityPerCell;
            variation.noiseSeed = definition.noiseSeed;
            variation.clusterNoiseScale = definition.clusterNoiseScale;
            variation.clusterThreshold = definition.clusterThreshold;
            variation.fineNoiseScale = definition.fineNoiseScale;
            variation.fineThreshold = definition.fineThreshold;
            variation.opacity = definition.opacity;
        }

        private static void ClampAuthoredVariationValues(
            MMOClassicGrassFoliageVariation variation,
            DefaultVariationDefinition fallback)
        {
            variation.minWidth = variation.minWidth > 0f ? variation.minWidth : fallback.minWidth;
            variation.maxWidth = Mathf.Max(variation.minWidth, variation.maxWidth > 0f ? variation.maxWidth : fallback.maxWidth);
            variation.minHeight = variation.minHeight > 0f ? variation.minHeight : fallback.minHeight;
            variation.maxHeight = Mathf.Max(variation.minHeight, variation.maxHeight > 0f ? variation.maxHeight : fallback.maxHeight);
            variation.maxDensityPerCell = Mathf.Max(1, variation.maxDensityPerCell);
            variation.clusterNoiseScale = variation.clusterNoiseScale > 0f ? variation.clusterNoiseScale : fallback.clusterNoiseScale;
            variation.fineNoiseScale = variation.fineNoiseScale > 0f ? variation.fineNoiseScale : fallback.fineNoiseScale;
            variation.opacity = variation.opacity > 0f ? variation.opacity : fallback.opacity;
        }

        private static Texture2D LoadConfiguredFoliageTexture(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ConfigureTerrainDetailRendering(Terrain terrain, MMOClassicGrassFoliageProfile profile)
        {
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = true;
            terrain.detailObjectDensity = profile.terrainDetailDensity;
            terrain.detailObjectDistance = profile.detailDrawDistance;
        }

        private static BuiltFoliageVariation[] BuildVariationPrefabs(MMOClassicGrassFoliageProfile profile)
        {
            List<BuiltFoliageVariation> builtVariations = new();
            for (int i = 0; i < profile.variations.Count; i++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[i];
                if (variation.modelPrefab != null)
                {
                    builtVariations.Add(new BuiltFoliageVariation(variation, CreateModelPrefab(variation, profile.alphaCutoff, GetVariationOpacity(profile, variation))));
                    continue;
                }

                if (variation.texture == null)
                {
                    continue;
                }

                Material material = CreateGrassMaterial(variation, profile.alphaCutoff, GetVariationOpacity(profile, variation));
                Mesh mesh = CreateCrossedPlaneMesh(
                    $"{MeshFolder}/{Sanitize(variation.displayName)}_CrossedCards.asset",
                    Mathf.Max(2, profile.crossedPlaneCount),
                    Mathf.Max(0.1f, profile.cardWidth),
                    Mathf.Max(0.1f, profile.cardHeight));

                builtVariations.Add(new BuiltFoliageVariation(variation, CreateGrassPrefab(variation, mesh, material)));
            }

            return builtVariations.ToArray();
        }

        private static Material CreateGrassMaterial(MMOClassicGrassFoliageVariation variation, float alphaCutoff, float opacity)
        {
            string path = $"{MaterialFolder}/{Sanitize(variation.displayName)}_AlphaCutout.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = FindGrassShader();
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                Shader shader = FindGrassShader();
                if (shader != null && material.shader != shader)
                {
                    material.shader = shader;
                }
            }

            SetTextureIfPresent(material, "_BaseMap", variation.texture);
            SetTextureIfPresent(material, "_MainTex", variation.texture);
            ConfigureTransparentCutoutMaterial(material, alphaCutoff, opacity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentCutoutMaterial(Material material, float alphaCutoff, float opacity)
        {
            float clampedOpacity = Mathf.Clamp01(opacity);
            float effectiveAlphaCutoff = Mathf.Min(alphaCutoff, Mathf.Max(0.001f, clampedOpacity * 0.5f));
            Color foliageTint = new(1f, 1f, 1f, clampedOpacity);
            SetColorIfPresent(material, "_BaseColor", foliageTint);
            SetColorIfPresent(material, "_Color", foliageTint);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 1f);
            SetFloatIfPresent(material, "_AlphaToMask", 0f);
            SetFloatIfPresent(material, "_Cutoff", effectiveAlphaCutoff);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0f);
            SetFloatIfPresent(material, "_ReceiveShadows", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.doubleSidedGI = true;
            material.enableInstancing = true;
        }

        private static float GetVariationOpacity(MMOClassicGrassFoliageProfile profile, MMOClassicGrassFoliageVariation variation)
        {
            return variation.opacity > 0f ? variation.opacity : profile.opacity;
        }

        private static Shader FindGrassShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent Cutout")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Standard");
        }

        private static Mesh CreateCrossedPlaneMesh(string assetPath, int planeCount, float cardWidth, float cardHeight)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            List<Vector3> vertices = new();
            List<Vector2> uvs = new();
            List<int> triangles = new();
            for (int plane = 0; plane < planeCount; plane++)
            {
                float angle = plane * Mathf.PI / planeCount;
                Vector3 right = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 halfWidth = right * (cardWidth * 0.5f);
                int start = vertices.Count;

                vertices.Add(-halfWidth);
                vertices.Add(halfWidth);
                vertices.Add(-halfWidth + Vector3.up * cardHeight);
                vertices.Add(halfWidth + Vector3.up * cardHeight);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));

                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(assetPath);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject CreateGrassPrefab(MMOClassicGrassFoliageVariation variation, Mesh mesh, Material material)
        {
            string path = $"{PrefabFolder}/{Sanitize(variation.displayName)}_Clump.prefab";
            GameObject root = new($"{variation.displayName} Clump");
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            meshRenderer.receiveShadows = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateModelPrefab(MMOClassicGrassFoliageVariation variation, float alphaCutoff, float opacity)
        {
            string path = $"{PrefabFolder}/{Sanitize(variation.displayName)}_Clump.prefab";
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(variation.modelPrefab);
            modelInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelInstance.transform.localScale = Vector3.one;
            Material material = CreateModelMaterial(
                variation,
                variation.displayName,
                FindFirstMaterial(modelInstance),
                0,
                alphaCutoff,
                opacity);
            Mesh mesh = CreateCombinedModelMesh(
                $"{MeshFolder}/{Sanitize(variation.displayName)}_ModelMesh.asset",
                modelInstance,
                variation.displayName);

            UnityEngine.Object.DestroyImmediate(modelInstance);

            GameObject root = new($"{variation.displayName} Clump");
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            meshRenderer.receiveShadows = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Mesh CreateCombinedModelMesh(
            string assetPath,
            GameObject modelInstance,
            string displayName)
        {
            List<CombineInstance> combines = new();
            MeshFilter[] meshFilters = modelInstance.GetComponentsInChildren<MeshFilter>(true);
            Matrix4x4 rootWorldToLocal = modelInstance.transform.worldToLocalMatrix;
            for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
            {
                MeshFilter meshFilter = meshFilters[filterIndex];
                Mesh sourceMesh = meshFilter.sharedMesh;
                if (sourceMesh == null)
                {
                    continue;
                }

                Matrix4x4 transform = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
                int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    combines.Add(new CombineInstance
                    {
                        mesh = sourceMesh,
                        subMeshIndex = subMesh,
                        transform = transform
                    });
                }
            }

            if (combines.Count == 0)
            {
                throw new InvalidOperationException($"{displayName} must contain at least one MeshFilter with a mesh.");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(assetPath);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(combines.ToArray(), true, true, false);
            mesh.RecalculateBounds();
            NormalizeModelDetailMesh(mesh);
            RotateModelDetailMesh(mesh, ModelDetailMeshRotation);
            CenterModelDetailMeshOnGround(mesh);
            if (mesh.normals == null || mesh.normals.Length == 0)
            {
                mesh.RecalculateNormals();
            }

            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void NormalizeModelDetailMesh(Mesh mesh)
        {
            OrientTallestMeshAxisUp(mesh);

            Bounds bounds = mesh.bounds;
            float sourceHeight = bounds.size.y;
            if (sourceHeight <= 0.0001f)
            {
                sourceHeight = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            if (sourceHeight <= 0.0001f)
            {
                return;
            }

            float scale = 1f / sourceHeight;
            Vector3 pivotOffset = new(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = (vertices[i] - pivotOffset) * scale;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void RotateModelDetailMesh(Mesh mesh, Quaternion rotation)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * vertices[i];
            }

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == vertices.Length)
            {
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = rotation * normals[i];
                }

                mesh.normals = normals;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void CenterModelDetailMeshOnGround(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            Vector3 pivotOffset = new(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] -= pivotOffset;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static void OrientTallestMeshAxisUp(Mesh mesh)
        {
            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            int verticalAxis = 1;
            float verticalSize = size.y;
            if (size.x > verticalSize)
            {
                verticalAxis = 0;
                verticalSize = size.x;
            }

            if (size.z > verticalSize)
            {
                verticalAxis = 2;
            }

            if (verticalAxis == 1)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                vertices[i] = verticalAxis == 0
                    ? new Vector3(vertex.y, vertex.x, vertex.z)
                    : new Vector3(vertex.x, vertex.z, vertex.y);
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static Material FindFirstMaterial(GameObject modelInstance)
        {
            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] != null)
                    {
                        return materials[materialIndex];
                    }
                }

            }

            return null;
        }

        private static Material CreateModelMaterial(
            MMOClassicGrassFoliageVariation variation,
            string displayName,
            Material sourceMaterial,
            int materialSlot,
            float alphaCutoff,
            float opacity)
        {
            string materialName = variation != null ? variation.displayName : displayName;
            string suffix = materialSlot == 0 ? string.Empty : $"_{materialSlot + 1:00}";
            string path = $"{MaterialFolder}/{Sanitize(materialName)}_AlphaCutout{suffix}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = sourceMaterial != null ? sourceMaterial.shader : FindGrassShader();
            if (material == null)
            {
                material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (sourceMaterial != null)
            {
                material.CopyPropertiesFromMaterial(sourceMaterial);
            }

            ConfigureTransparentCutoutMaterial(material, alphaCutoff, opacity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyDetailPrototypes(TerrainData terrainData, MMOClassicGrassFoliageProfile profile, BuiltFoliageVariation[] builtVariations)
        {
            terrainData.SetDetailResolution(profile.detailResolution, profile.detailResolutionPerPatch);
            terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            DetailPrototype[] prototypes = new DetailPrototype[builtVariations.Length];
            for (int i = 0; i < builtVariations.Length; i++)
            {
                MMOClassicGrassFoliageVariation variation = builtVariations[i].variation;
                prototypes[i] = new DetailPrototype
                {
                    prototype = builtVariations[i].prefab,
                    renderMode = DetailRenderMode.VertexLit,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    useDensityScaling = true,
                    alignToGround = 0f,
                    minWidth = variation.minWidth,
                    maxWidth = variation.maxWidth,
                    minHeight = variation.minHeight,
                    maxHeight = variation.maxHeight,
                    noiseSeed = variation.noiseSeed,
                    noiseSpread = 13f,
                    density = 0.2f,
                    targetCoverage = 0.08f,
                    positionJitter = 0.9f,
                    healthyColor = Color.white,
                    dryColor = Color.white
                };
            }

            terrainData.detailPrototypes = prototypes;
            terrainData.RefreshPrototypes();
        }

        private static void ClearAllDetailLayers(TerrainData terrainData)
        {
            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                terrainData.SetDetailLayer(0, 0, layer, new int[terrainData.detailHeight, terrainData.detailWidth]);
            }
        }

        private static int PaintDetailVerificationPatch(Terrain terrain, MMOClassicGrassFoliageProfile profile, string displayNameFragment)
        {
            TerrainData terrainData = terrain.terrainData;
            int patchSize = Mathf.Min(9, terrainData.detailWidth, terrainData.detailHeight);
            int startX = Mathf.Clamp((terrainData.detailWidth - patchSize) / 2, 0, terrainData.detailWidth - patchSize);
            int startZ = Mathf.Clamp((terrainData.detailHeight - patchSize) / 2, 0, terrainData.detailHeight - patchSize);
            int layerCount = Mathf.Min(terrainData.detailPrototypes.Length, profile.variations.Count);
            int paintedLayers = 0;

            for (int layer = 0; layer < layerCount; layer++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[layer];
                if (variation == null
                    || string.IsNullOrWhiteSpace(variation.displayName)
                    || variation.displayName.IndexOf(displayNameFragment, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int[,] details = terrainData.GetDetailLayer(startX, startZ, patchSize, patchSize, layer);
                int density = Mathf.Max(1, variation.maxDensityPerCell);
                for (int z = 0; z < patchSize; z++)
                {
                    for (int x = 0; x < patchSize; x++)
                    {
                        details[z, x] = Mathf.Max(details[z, x], density);
                    }
                }

                terrainData.SetDetailLayer(startX, startZ, layer, details);
                paintedLayers++;
            }

            return paintedLayers;
        }

        private static void PaintSparsePatchyDetails(Terrain terrain, MMOClassicGrassFoliageProfile profile)
        {
            TerrainData terrainData = terrain.terrainData;
            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);

            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[layer];
                int[,] details = new int[height, width];

                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float normalizedX = x / (float)(width - 1);
                        float normalizedZ = z / (float)(height - 1);
                        Vector2 world = DetailToWorld(terrain, normalizedX, normalizedZ);
                        details[z, x] = CalculateDetailDensity(terrainData, alphamaps, variation, world, normalizedX, normalizedZ);
                    }
                }

                terrainData.SetDetailLayer(0, 0, layer, details);
            }
        }

        private static int CalculateDetailDensity(
            TerrainData terrainData,
            float[,,] alphamaps,
            MMOClassicGrassFoliageVariation variation,
            Vector2 world,
            float normalizedX,
            float normalizedZ)
        {
            float terrainMask = Mathf.Clamp01(0.35f + SampleTerrainMask(terrainData, alphamaps, normalizedX, normalizedZ) * 0.65f);
            float pathMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(11f, 28f, DistanceToStarterPath(world)));
            float slopeMask = Mathf.InverseLerp(38f, 8f, terrainData.GetSteepness(normalizedX, normalizedZ));
            float cluster = Mathf.PerlinNoise(
                world.x * variation.clusterNoiseScale + variation.noiseSeed,
                world.y * variation.clusterNoiseScale - variation.noiseSeed);
            float fine = Mathf.PerlinNoise(
                world.x * variation.fineNoiseScale - variation.noiseSeed * 0.37f,
                world.y * variation.fineNoiseScale + variation.noiseSeed * 0.61f);

            float patchMask = Mathf.SmoothStep(variation.clusterThreshold, variation.clusterThreshold + 0.18f, cluster);
            float fineMask = Mathf.SmoothStep(variation.fineThreshold, 0.92f, fine);
            float density = terrainMask * pathMask * slopeMask * patchMask * fineMask;
            if (density <= 0.08f)
            {
                return 0;
            }

            float stochastic = Hash01(world, variation.noiseSeed);
            return Mathf.Clamp(Mathf.FloorToInt(density * variation.maxDensityPerCell + stochastic), 0, variation.maxDensityPerCell);
        }

        private static float SampleTerrainMask(TerrainData terrainData, float[,,] alphamaps, float normalizedX, float normalizedZ)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (terrainData.alphamapWidth - 1)), 0, terrainData.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (terrainData.alphamapHeight - 1)), 0, terrainData.alphamapHeight - 1);
            float grass = alphamaps[z, x, 0];
            float clay = alphamaps[z, x, 1];
            float rock = alphamaps[z, x, 2];
            float trail = alphamaps[z, x, 3];
            return Mathf.Clamp01(grass * 1.15f + clay * 0.28f - rock * 0.75f - trail * 1.35f);
        }

        private static Vector2 DetailToWorld(Terrain terrain, float normalizedX, float normalizedZ)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            return new Vector2(
                terrainPosition.x + normalizedX * terrainSize.x,
                terrainPosition.z + normalizedZ * terrainSize.z);
        }

        private static float DistanceToStarterPath(Vector2 world)
        {
            float distance = float.MaxValue;
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-42f, -178f), new Vector2(-34f, -118f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-34f, -118f), new Vector2(82f, -48f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(82f, -48f), new Vector2(145f, 72f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-34f, -118f), new Vector2(-132f, 128f)));
            return distance;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, a + segment * t);
        }

        private static float Hash01(Vector2 world, int seed)
        {
            float value = Mathf.Sin(Vector2.Dot(world, new Vector2(12.9898f, 78.233f)) + seed * 19.19f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(GeneratedFolder);
            CreateFolderIfMissing(MaterialFolder);
            CreateFolderIfMissing(MeshFolder);
            CreateFolderIfMissing(PrefabFolder);
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }

        private enum FoliageVariationKind
        {
            Texture,
            Model
        }

        private readonly struct BuiltFoliageVariation
        {
            public readonly MMOClassicGrassFoliageVariation variation;
            public readonly GameObject prefab;

            public BuiltFoliageVariation(MMOClassicGrassFoliageVariation variation, GameObject prefab)
            {
                this.variation = variation;
                this.prefab = prefab;
            }
        }

        private readonly struct DefaultVariationDefinition
        {
            public readonly string displayName;
            public readonly string assetPath;
            public readonly FoliageVariationKind kind;
            public readonly float minWidth;
            public readonly float maxWidth;
            public readonly float minHeight;
            public readonly float maxHeight;
            public readonly int maxDensityPerCell;
            public readonly int noiseSeed;
            public readonly float clusterNoiseScale;
            public readonly float clusterThreshold;
            public readonly float fineNoiseScale;
            public readonly float fineThreshold;
            public readonly float opacity;

            public static DefaultVariationDefinition Texture(
                string displayName,
                string assetPath,
                float minWidth,
                float maxWidth,
                float minHeight,
                float maxHeight,
                int maxDensityPerCell,
                int noiseSeed,
                float clusterNoiseScale,
                float clusterThreshold,
                float fineNoiseScale,
                float fineThreshold,
                float opacity)
            {
                return new DefaultVariationDefinition(
                    displayName,
                    assetPath,
                    FoliageVariationKind.Texture,
                    minWidth,
                    maxWidth,
                    minHeight,
                    maxHeight,
                    maxDensityPerCell,
                    noiseSeed,
                    clusterNoiseScale,
                    clusterThreshold,
                    fineNoiseScale,
                    fineThreshold,
                    opacity);
            }

            public static DefaultVariationDefinition Model(
                string displayName,
                string assetPath,
                float minWidth,
                float maxWidth,
                float minHeight,
                float maxHeight,
                int maxDensityPerCell,
                int noiseSeed,
                float clusterNoiseScale,
                float clusterThreshold,
                float fineNoiseScale,
                float fineThreshold,
                float opacity)
            {
                return new DefaultVariationDefinition(
                    displayName,
                    assetPath,
                    FoliageVariationKind.Model,
                    minWidth,
                    maxWidth,
                    minHeight,
                    maxHeight,
                    maxDensityPerCell,
                    noiseSeed,
                    clusterNoiseScale,
                    clusterThreshold,
                    fineNoiseScale,
                    fineThreshold,
                    opacity);
            }

            private DefaultVariationDefinition(
                string displayName,
                string assetPath,
                FoliageVariationKind kind,
                float minWidth,
                float maxWidth,
                float minHeight,
                float maxHeight,
                int maxDensityPerCell,
                int noiseSeed,
                float clusterNoiseScale,
                float clusterThreshold,
                float fineNoiseScale,
                float fineThreshold,
                float opacity)
            {
                this.displayName = displayName;
                this.assetPath = assetPath;
                this.kind = kind;
                this.minWidth = minWidth;
                this.maxWidth = maxWidth;
                this.minHeight = minHeight;
                this.maxHeight = maxHeight;
                this.maxDensityPerCell = maxDensityPerCell;
                this.noiseSeed = noiseSeed;
                this.clusterNoiseScale = clusterNoiseScale;
                this.clusterThreshold = clusterThreshold;
                this.fineNoiseScale = fineNoiseScale;
                this.fineThreshold = fineThreshold;
                this.opacity = opacity;
            }
        }
    }
}
