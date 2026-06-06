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

        private static readonly string[] DefaultGrassTexturePaths =
        {
            "Assets/grass-01.png",
            "Assets/grass-02.png",
            "Assets/grass-03.png",
            "Assets/grass-04.png"
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
            GameObject[] prefabs = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, prefabs);
            ClearAllDetailLayers(terrain.terrainData);

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prepared {terrain.name} for sparse manual classic grass painting. Detail prototypes were kept and painted instances were cleared.");
        }

        [MenuItem("Tools/RPG Clone/Refresh Classic Grass Materials")]
        public static void RefreshClassicGrassMaterials()
        {
            EnsureFolders();
            MMOClassicGrassFoliageProfile profile = EnsureDefaultProfile();
            BuildVariationPrefabs(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Refreshed classic grass materials with unlit transparent alpha-cutout settings.");
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

            GameObject[] prefabs = BuildVariationPrefabs(profile);
            ApplyDetailPrototypes(terrain.terrainData, profile, prefabs);
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

            bool changed = profile.variations.Count != DefaultGrassTexturePaths.Length;
            if (changed)
            {
                profile.variations.Clear();
            }

            for (int i = 0; i < DefaultGrassTexturePaths.Length; i++)
            {
                Texture2D texture = LoadConfiguredGrassTexture(DefaultGrassTexturePaths[i]);
                if (texture == null)
                {
                    Debug.LogWarning($"Classic grass foliage texture is missing: {DefaultGrassTexturePaths[i]}");
                    continue;
                }

                if (changed)
                {
                    profile.variations.Add(CreateDefaultVariation(i, texture));
                }
                else if (i < profile.variations.Count && profile.variations[i].texture == null)
                {
                    profile.variations[i].texture = texture;
                }
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOClassicGrassFoliageVariation CreateDefaultVariation(int index, Texture2D texture)
        {
            return new MMOClassicGrassFoliageVariation
            {
                displayName = $"Classic Grass {index + 1:00}",
                texture = texture,
                minWidth = 0.68f + index * 0.03f,
                maxWidth = 1.08f + index * 0.04f,
                minHeight = 0.62f + index * 0.05f,
                maxHeight = 1.18f + index * 0.08f,
                maxDensityPerCell = index == 0 ? 3 : 2,
                noiseSeed = 73 + index * 101,
                clusterNoiseScale = 0.018f + index * 0.0035f,
                clusterThreshold = 0.46f + index * 0.025f,
                fineNoiseScale = 0.095f + index * 0.018f,
                fineThreshold = 0.24f + index * 0.018f
            };
        }

        private static Texture2D LoadConfiguredGrassTexture(string assetPath)
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

        private static GameObject[] BuildVariationPrefabs(MMOClassicGrassFoliageProfile profile)
        {
            List<GameObject> prefabs = new();
            for (int i = 0; i < profile.variations.Count; i++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[i];
                if (variation.texture == null)
                {
                    continue;
                }

                Material material = CreateGrassMaterial(variation, profile.alphaCutoff, profile.materialTint);
                Mesh mesh = CreateCrossedPlaneMesh(
                    $"{MeshFolder}/{Sanitize(variation.displayName)}_CrossedCards.asset",
                    Mathf.Max(2, profile.crossedPlaneCount),
                    Mathf.Max(0.1f, profile.cardWidth),
                    Mathf.Max(0.1f, profile.cardHeight));

                prefabs.Add(CreateGrassPrefab(variation, mesh, material));
            }

            return prefabs.ToArray();
        }

        private static Material CreateGrassMaterial(MMOClassicGrassFoliageVariation variation, float alphaCutoff, Color materialTint)
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
            SetColorIfPresent(material, "_BaseColor", materialTint);
            SetColorIfPresent(material, "_Color", materialTint);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 1f);
            SetFloatIfPresent(material, "_Cutoff", alphaCutoff);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.12f);
            SetFloatIfPresent(material, "_ReceiveShadows", 0f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindGrassShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent Cutout")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Universal Render Pipeline/Lit")
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
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyDetailPrototypes(TerrainData terrainData, MMOClassicGrassFoliageProfile profile, GameObject[] prefabs)
        {
            terrainData.SetDetailResolution(profile.detailResolution, profile.detailResolutionPerPatch);
            terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            DetailPrototype[] prototypes = new DetailPrototype[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[i];
                prototypes[i] = new DetailPrototype
                {
                    prototype = prefabs[i],
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
                    healthyColor = profile.healthyColor,
                    dryColor = profile.dryColor
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
    }
}
