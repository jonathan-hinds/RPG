using System;
using RPGClone.World.Foliage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOTerrainNeighborTerrainSetupUtility
    {
        private const string SourceTerrainName = "Orcish Starter Valley Terrain";
        private const string SourceTerrainDataPath = "Assets/_Project/Generated/Terrain/OrcishStarterValleyTerrain.asset";
        private const string TerrainDataFolder = "Assets/_Project/Generated/Terrain";
        private const string CollisionRootName = "Generated Tree Trunk Blockers";
        private const string TreeTrunkLayerName = "TreeTrunk";
        private const float NeighborPositionTolerance = 0.5f;
        private const int SeamBlendRows = 12;

        [MenuItem("Tools/RPG Clone/Terrain/Add North Neighbor Terrain")]
        public static void AddNorthNeighborTerrain()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            Terrain source = FindSourceTerrain(terrains);
            if (source == null)
            {
                Debug.LogError($"Could not find source terrain '{SourceTerrainName}' or TerrainData at {SourceTerrainDataPath}.");
                return;
            }

            Terrain anchor = FindNorthernmostTerrain(terrains);
            if (anchor == null)
            {
                Debug.LogError("Could not add a north neighbor because no active Terrain exists in the scene.");
                return;
            }

            Vector3 newPosition = anchor.transform.position + Vector3.forward * anchor.terrainData.size.z;
            if (FindTerrainAtPosition(terrains, newPosition) != null)
            {
                Debug.LogWarning($"A terrain already exists at {FormatPositionName(newPosition)}.");
                return;
            }

            TerrainData terrainData = new();
            terrainData.name = $"TerrainData_{FormatPositionName(newPosition)}";
            terrainData.heightmapResolution = source.terrainData.heightmapResolution;
            terrainData.alphamapResolution = source.terrainData.alphamapResolution;
            terrainData.baseMapResolution = source.terrainData.baseMapResolution;
            terrainData.SetDetailResolution(source.terrainData.detailResolution, source.terrainData.detailResolutionPerPatch);
            terrainData.size = source.terrainData.size;

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainDataFolder}/{terrainData.name}.asset");
            AssetDatabase.CreateAsset(terrainData, assetPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = $"Terrain_{FormatPositionName(newPosition)}";
            terrainObject.transform.position = newPosition;
            terrainObject.isStatic = true;

            Selection.activeGameObject = terrainObject;
            Debug.Log($"Created north neighbor terrain {terrainObject.name} using TerrainData {assetPath}.");

            NormalizeNeighborTerrainsInActiveScene();
        }

        [MenuItem("Tools/RPG Clone/Terrain/Normalize Neighbor Terrains")]
        public static void NormalizeNeighborTerrainsInActiveScene()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);

            if (terrains.Length < 2)
            {
                Debug.LogWarning("Neighbor terrain normalization needs at least two active Terrain objects in the scene.");
                return;
            }

            Terrain source = FindSourceTerrain(terrains);
            if (source == null)
            {
                Debug.LogError($"Could not find source terrain '{SourceTerrainName}' or TerrainData at {SourceTerrainDataPath}.");
                return;
            }

            int normalizedCount = 0;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == source)
                {
                    continue;
                }

                NormalizeTerrain(source, terrain, terrains);
                normalizedCount++;
            }

            ConnectNeighbors(terrains);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Normalized {normalizedCount} neighbor terrain(s) against {source.name} and refreshed terrain neighbor links.");
        }

        private static Terrain FindSourceTerrain(Terrain[] terrains)
        {
            TerrainData expectedData = AssetDatabase.LoadAssetAtPath<TerrainData>(SourceTerrainDataPath);
            foreach (Terrain terrain in terrains)
            {
                if (terrain.name == SourceTerrainName || terrain.terrainData == expectedData)
                {
                    return terrain;
                }
            }

            return terrains.Length > 0 ? terrains[0] : null;
        }

        private static void NormalizeTerrain(Terrain source, Terrain target, Terrain[] allTerrains)
        {
            TerrainData sourceData = source.terrainData;
            TerrainData targetData = target.terrainData;
            if (sourceData == null || targetData == null)
            {
                Debug.LogWarning($"Skipped {target.name} because source or target TerrainData is missing.");
                return;
            }

            Undo.RecordObject(target, "Normalize Neighbor Terrain");
            Undo.RecordObject(targetData, "Normalize Neighbor Terrain Data");

            CopyTerrainComponentSettings(source, target);
            CopyTerrainDataAuthoringSettings(sourceData, targetData);
            BlendSharedHeightEdges(source, target, allTerrains);
            InitializeBaseAlphamap(targetData);
            EnsureTerrainCollider(target, targetData);
            EnsureTreeCollisionSynchronizer(target);

            MMOTerrainPathPaintingConfigurator.ConfigureTerrainForPathPainting(target);
            MMOClassicGrassFoliageBuilder.ApplyToTerrain(target);
            MMOTerrainTreePrototypeInstaller.ConfigureTerrainTrees(target);

            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(targetData);

            Debug.Log(
                $"Normalized {target.name}: material={target.materialTemplate?.name ?? "none"}, " +
                $"layers={targetData.terrainLayers.Length}, details={targetData.detailPrototypes.Length}, " +
                $"treePrototypes={targetData.treePrototypes.Length}, treeInstances={targetData.treeInstanceCount}.");
        }

        private static void CopyTerrainComponentSettings(Terrain source, Terrain target)
        {
            target.materialTemplate = source.materialTemplate;
            target.drawInstanced = source.drawInstanced;
            target.drawHeightmap = source.drawHeightmap;
            target.drawTreesAndFoliage = source.drawTreesAndFoliage;
            target.allowAutoConnect = false;
            target.groupingID = source.groupingID;
            target.heightmapPixelError = source.heightmapPixelError;
            target.heightmapMaximumLOD = source.heightmapMaximumLOD;
            target.basemapDistance = source.basemapDistance;
            target.detailObjectDensity = source.detailObjectDensity;
            target.detailObjectDistance = source.detailObjectDistance;
            target.treeDistance = source.treeDistance;
            target.treeBillboardDistance = source.treeBillboardDistance;
            target.treeCrossFadeLength = source.treeCrossFadeLength;
            target.treeMaximumFullLODCount = source.treeMaximumFullLODCount;
            target.preserveTreePrototypeLayers = source.preserveTreePrototypeLayers;
            target.shadowCastingMode = source.shadowCastingMode;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.renderingLayerMask = source.renderingLayerMask;
            target.patchBoundsMultiplier = source.patchBoundsMultiplier;
            target.treeLODBiasMultiplier = source.treeLODBiasMultiplier;
        }

        private static void CopyTerrainDataAuthoringSettings(TerrainData sourceData, TerrainData targetData)
        {
            if (targetData.heightmapResolution != sourceData.heightmapResolution)
            {
                targetData.heightmapResolution = sourceData.heightmapResolution;
            }

            targetData.size = sourceData.size;
            targetData.alphamapResolution = sourceData.alphamapResolution;
            targetData.baseMapResolution = sourceData.baseMapResolution;
            targetData.SetDetailResolution(sourceData.detailResolution, sourceData.detailResolutionPerPatch);
            targetData.SetDetailScatterMode(sourceData.detailScatterMode);
            targetData.terrainLayers = sourceData.terrainLayers;
            targetData.detailPrototypes = sourceData.detailPrototypes;
            targetData.treePrototypes = sourceData.treePrototypes;
            targetData.wavingGrassTint = sourceData.wavingGrassTint;
            targetData.wavingGrassStrength = sourceData.wavingGrassStrength;
            targetData.wavingGrassAmount = sourceData.wavingGrassAmount;
            targetData.wavingGrassSpeed = sourceData.wavingGrassSpeed;
            targetData.RefreshPrototypes();
        }

        private static void BlendSharedHeightEdges(Terrain source, Terrain target, Terrain[] allTerrains)
        {
            TerrainData targetData = target.terrainData;
            Vector3 targetPosition = target.transform.position;
            Vector3 targetSize = targetData.size;

            foreach (Terrain neighbor in allTerrains)
            {
                if (neighbor == target || !IsUsableSeamSource(source, neighbor))
                {
                    continue;
                }

                TerrainData neighborData = neighbor.terrainData;
                if (neighborData.heightmapResolution != targetData.heightmapResolution)
                {
                    continue;
                }

                int resolution = neighborData.heightmapResolution;
                Vector3 neighborPosition = neighbor.transform.position;
                Vector3 neighborSize = neighborData.size;

                if (Approximately(targetPosition.z, neighborPosition.z + neighborSize.z) && Approximately(targetPosition.x, neighborPosition.x))
                {
                    BlendRows(targetData, neighborData.GetHeights(0, resolution - 1, resolution, 1), true);
                }
                else if (Approximately(targetPosition.z + targetSize.z, neighborPosition.z) && Approximately(targetPosition.x, neighborPosition.x))
                {
                    BlendRows(targetData, neighborData.GetHeights(0, 0, resolution, 1), false);
                }

                if (Approximately(targetPosition.x, neighborPosition.x + neighborSize.x) && Approximately(targetPosition.z, neighborPosition.z))
                {
                    BlendColumns(targetData, neighborData.GetHeights(resolution - 1, 0, 1, resolution), true);
                }
                else if (Approximately(targetPosition.x + targetSize.x, neighborPosition.x) && Approximately(targetPosition.z, neighborPosition.z))
                {
                    BlendColumns(targetData, neighborData.GetHeights(0, 0, 1, resolution), false);
                }
            }
        }

        private static bool IsUsableSeamSource(Terrain source, Terrain candidate)
        {
            if (candidate == source)
            {
                return true;
            }

            TerrainData candidateData = candidate.terrainData;
            return candidate.materialTemplate == source.materialTemplate
                && candidateData != null
                && candidateData.terrainLayers.Length > 0;
        }

        private static void BlendRows(TerrainData targetData, float[,] sourceEdge, bool targetBottomEdge)
        {
            int resolution = targetData.heightmapResolution;
            int rows = Mathf.Min(SeamBlendRows, resolution);
            int startY = targetBottomEdge ? 0 : resolution - rows;
            float[,] heights = targetData.GetHeights(0, startY, resolution, rows);

            for (int row = 0; row < rows; row++)
            {
                int localRow = targetBottomEdge ? row : rows - 1 - row;
                float blend = rows <= 1 ? 1f : row / (float)(rows - 1);
                for (int x = 0; x < resolution; x++)
                {
                    heights[localRow, x] = Mathf.Lerp(sourceEdge[0, x], heights[localRow, x], blend);
                }
            }

            targetData.SetHeights(0, startY, heights);
        }

        private static void BlendColumns(TerrainData targetData, float[,] sourceEdge, bool targetLeftEdge)
        {
            int resolution = targetData.heightmapResolution;
            int columns = Mathf.Min(SeamBlendRows, resolution);
            int startX = targetLeftEdge ? 0 : resolution - columns;
            float[,] heights = targetData.GetHeights(startX, 0, columns, resolution);

            for (int column = 0; column < columns; column++)
            {
                int localColumn = targetLeftEdge ? column : columns - 1 - column;
                float blend = columns <= 1 ? 1f : column / (float)(columns - 1);
                for (int z = 0; z < resolution; z++)
                {
                    heights[z, localColumn] = Mathf.Lerp(sourceEdge[z, 0], heights[z, localColumn], blend);
                }
            }

            targetData.SetHeights(startX, 0, heights);
        }

        private static void InitializeBaseAlphamap(TerrainData terrainData)
        {
            int layerCount = terrainData.terrainLayers.Length;
            if (layerCount == 0)
            {
                return;
            }

            float[,,] alphamaps = new float[terrainData.alphamapHeight, terrainData.alphamapWidth, layerCount];
            for (int z = 0; z < terrainData.alphamapHeight; z++)
            {
                for (int x = 0; x < terrainData.alphamapWidth; x++)
                {
                    alphamaps[z, x, 0] = 1f;
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
        }

        private static void EnsureTerrainCollider(Terrain terrain, TerrainData terrainData)
        {
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null)
            {
                collider = terrain.gameObject.AddComponent<TerrainCollider>();
            }

            collider.terrainData = terrainData;
            EditorUtility.SetDirty(collider);
        }

        private static void EnsureTreeCollisionSynchronizer(Terrain terrain)
        {
            MMOTerrainTreeCollisionSynchronizer synchronizer = terrain.GetComponent<MMOTerrainTreeCollisionSynchronizer>();
            if (synchronizer == null)
            {
                synchronizer = terrain.gameObject.AddComponent<MMOTerrainTreeCollisionSynchronizer>();
            }

            synchronizer.Configure(CollisionRootName, TreeTrunkLayerName, true);
            synchronizer.SyncNow();
            EditorUtility.SetDirty(synchronizer);
        }

        private static void ConnectNeighbors(Terrain[] terrains)
        {
            foreach (Terrain terrain in terrains)
            {
                Terrain left = FindNeighbor(terrain, terrains, -1, 0);
                Terrain right = FindNeighbor(terrain, terrains, 1, 0);
                Terrain top = FindNeighbor(terrain, terrains, 0, 1);
                Terrain bottom = FindNeighbor(terrain, terrains, 0, -1);
                terrain.SetNeighbors(left, top, right, bottom);
                EditorUtility.SetDirty(terrain);
            }
        }

        private static Terrain FindNeighbor(Terrain terrain, Terrain[] terrains, int xDirection, int zDirection)
        {
            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 expectedPosition = new(
                position.x + xDirection * size.x,
                position.y,
                position.z + zDirection * size.z);

            foreach (Terrain candidate in terrains)
            {
                if (candidate == terrain)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.transform.position;
                if (Approximately(candidatePosition.x, expectedPosition.x)
                    && Approximately(candidatePosition.z, expectedPosition.z))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Terrain FindNorthernmostTerrain(Terrain[] terrains)
        {
            Terrain northernmost = null;
            foreach (Terrain terrain in terrains)
            {
                if (northernmost == null || terrain.transform.position.z > northernmost.transform.position.z)
                {
                    northernmost = terrain;
                }
            }

            return northernmost;
        }

        private static Terrain FindTerrainAtPosition(Terrain[] terrains, Vector3 position)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 terrainPosition = terrain.transform.position;
                if (Approximately(terrainPosition.x, position.x) && Approximately(terrainPosition.z, position.z))
                {
                    return terrain;
                }
            }

            return null;
        }

        private static string FormatPositionName(Vector3 position)
        {
            return $"{position.x:0.00}, {position.y:0.00}, {position.z:0.00}";
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= NeighborPositionTolerance;
        }
    }
}
