using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOTerrainPathPaintingConfigurator
    {
        public const string PathLayerName = "RockGroundPath";
        public const string PathTexturePath = "Assets/RockGroundPathTexture.png";
        public const string PathTerrainLayerPath = "Assets/_Project/Generated/Terrain/RockGroundPath.terrainlayer";
        public const string TerrainMaterialPath = "Assets/_Project/Generated/Materials/Barrens_Slope_Blend_Terrain.mat";
        public const int PathLayerIndex = 3;
        public const float PathTileSize = 8f;

        private static readonly Color PathDiffuseRemapMax = new(0.62f, 0.52f, 0.42f, 1f);

        [MenuItem("Tools/RPG Clone/Terrain/Configure Path Texture Painting")]
        public static void ConfigureActiveTerrainPathPainting()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Path texture painting could not be configured because no Terrain exists in the active scene.");
                return;
            }

            ConfigureTerrainForPathPainting(terrain);
            Selection.activeGameObject = terrain.gameObject;
            Debug.Log("Path texture painting configured. Select the Terrain, open Paint Texture, choose RockGroundPath, and paint with Unity's brush opacity/falloff controls.");
        }

        public static TerrainLayer ConfigureTerrainForPathPainting(Terrain terrain, Material sourceMaterial = null)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            Texture2D pathTexture = LoadPathTexture();
            TerrainLayer pathLayer = CreateOrUpdatePathTerrainLayer(pathTexture, sourceMaterial);
            AssignPathLayer(terrain.terrainData, pathLayer);
            ConfigureTerrainMaterial(terrain, pathTexture);

            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            AssetDatabase.SaveAssets();
            return pathLayer;
        }

        public static TerrainLayer CreateOrUpdatePathTerrainLayer(Material sourceMaterial = null)
        {
            return CreateOrUpdatePathTerrainLayer(LoadPathTexture(), sourceMaterial);
        }

        private static Texture2D LoadPathTexture()
        {
            ConfigurePathTextureImporter();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PathTexturePath);
            if (texture == null)
            {
                throw new InvalidOperationException($"Path texture is missing at {PathTexturePath}.");
            }

            return texture;
        }

        private static TerrainLayer CreateOrUpdatePathTerrainLayer(Texture2D pathTexture, Material sourceMaterial)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(PathTerrainLayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, PathTerrainLayerPath);
            }

            layer.name = PathLayerName;
            layer.diffuseTexture = pathTexture;
            layer.tileSize = Vector2.one * PathTileSize;
            layer.tileOffset = Vector2.zero;
            layer.smoothness = 0.08f;
            layer.metallic = 0f;
            layer.diffuseRemapMax = sourceMaterial != null && sourceMaterial.HasProperty("_BaseColor")
                ? sourceMaterial.GetColor("_BaseColor")
                : PathDiffuseRemapMax;

            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void AssignPathLayer(TerrainData terrainData, TerrainLayer pathLayer)
        {
            TerrainLayer[] layers = terrainData.terrainLayers;
            if (layers.Length <= PathLayerIndex)
            {
                Array.Resize(ref layers, PathLayerIndex + 1);
            }

            layers[PathLayerIndex] = pathLayer;
            terrainData.terrainLayers = layers;
        }

        private static void ConfigureTerrainMaterial(Terrain terrain, Texture2D pathTexture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"Path texture was added to the terrain layer, but terrain material was not found at {TerrainMaterialPath}.");
                return;
            }

            if (material.HasProperty("_PathTex"))
            {
                material.SetTexture("_PathTex", pathTexture);
            }

            if (material.HasProperty("_PathTilingSize"))
            {
                material.SetFloat("_PathTilingSize", PathTileSize);
            }

            if (material.HasProperty("_PathBlendStrength"))
            {
                material.SetFloat("_PathBlendStrength", 1f);
            }

            terrain.materialTemplate = material;
            EditorUtility.SetDirty(material);
        }

        private static void ConfigurePathTextureImporter()
        {
            if (AssetImporter.GetAtPath(PathTexturePath) is not TextureImporter importer)
            {
                return;
            }

            bool dirty = false;

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                dirty = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                dirty = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                dirty = true;
            }

            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                dirty = true;
            }

            if (!dirty)
            {
                return;
            }

            importer.SaveAndReimport();
        }
    }
}
