using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.Player;
using RPGClone.World.Foliage;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOTerrainTreePrototypeInstaller
    {
        private const string GeneratedFolder = "Assets/_Project/Generated/Foliage";
        private const string MeshFolder = GeneratedFolder + "/Meshes";
        private const string PrefabFolder = GeneratedFolder + "/Prefabs";
        private const string CollisionRootName = "Generated Tree Trunk Blockers";
        private const string RemovedSynchronizerTypeName = "RPGClone.World.Foliage.MMOTerrainTreeCollisionSynchronizer";
        private const string TreeTrunkLayerName = "TreeTrunk";
        private const string CameraConfigPath = "Assets/_Project/Configs/DefaultThirdPersonCamera.asset";
        private const float TreeDrawDistance = 5000f;
        private const float SingleLodScreenRelativeTransitionHeight = 0.001f;
        private const float DefaultTreeGroundEmbedDepth = 0.18f;
        private const float FullRotationRadians = 6.28318530718f;
        private const int MaxFullLodTrees = 10000;
        private const int TreeRotationSeed = 0x4D4D4F54;

        private static readonly TreeDefinition[] Trees =
        {
            new(
                "Orcish Ember Tree 01",
                "tree",
                "Assets/tree/tree.blend",
                $"{PrefabFolder}/Orcish_Ember_Tree_01_TerrainTree.prefab",
                $"{MeshFolder}/Orcish_Ember_Tree_01_TerrainTreeMesh.asset",
                0.48f,
                4.2f,
                2.1f,
                DefaultTreeGroundEmbedDepth),
            new(
                "Orcish Ember Tree 02",
                "tree2",
                "Assets/tree2/tree2.blend",
                $"{PrefabFolder}/Orcish_Ember_Tree_02_TerrainTree.prefab",
                $"{MeshFolder}/Orcish_Ember_Tree_02_TerrainTreeMesh.asset",
                0.5f,
                4.4f,
                2.2f,
                DefaultTreeGroundEmbedDepth),
            new(
                "Orcish Ember Tree 03",
                "tree3",
                "Assets/tree3/tree3.blend",
                $"{PrefabFolder}/Orcish_Ember_Tree_03_TerrainTree.prefab",
                $"{MeshFolder}/Orcish_Ember_Tree_03_TerrainTreeMesh.asset",
                0.44f,
                4f,
                2f,
                DefaultTreeGroundEmbedDepth)
        };

        [MenuItem("Tools/RPG Clone/Foliage/Configure Terrain Trees")]
        public static void ConfigureActiveTerrainTrees()
        {
            Terrain terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("Terrain trees could not be configured because no Terrain exists in the active scene.");
                return;
            }

            ConfigureTerrainTrees(terrain);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ConfigureTerrainTrees(Terrain terrain)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            EnsureFolders();
            int treeTrunkLayer = EnsureLayer(TreeTrunkLayerName);
            CleanupRemovedTreeSystems(terrain);

            GameObject[] treePrefabs = BuildTreePrefabs(treeTrunkLayer);
            TerrainData terrainData = terrain.terrainData;
            RemoveBrokenTreeDetails(terrainData, treePrefabs);
            ApplyTreePrototypes(terrainData, treePrefabs);
            EnsurePaintedTreeRotationVariation(terrainData);
            EnsureCollisionSynchronizer(terrain);

            ConfigureTerrainTreeRendering(terrain);
            ConfigureCameraCollisionMask(treeTrunkLayer);
            ConfigureNavMeshSurfaces(treeTrunkLayer);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(terrainData);

            Debug.Log($"Configured Unity Terrain Paint Trees on {terrain.name}: {string.Join(", ", Array.ConvertAll(treePrefabs, prefab => prefab.name))}.");
        }

        private static GameObject[] BuildTreePrefabs(int treeTrunkLayer)
        {
            GameObject[] prefabs = new GameObject[Trees.Length];
            for (int i = 0; i < Trees.Length; i++)
            {
                prefabs[i] = BuildTreePrefab(Trees[i], treeTrunkLayer);
            }

            return prefabs;
        }

        private static GameObject BuildTreePrefab(TreeDefinition tree, int treeTrunkLayer)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tree.PrefabPath);
            Vector3 preservedLocalPosition = existingPrefab != null ? existingPrefab.transform.localPosition : Vector3.zero;
            Quaternion preservedLocalRotation = existingPrefab != null ? existingPrefab.transform.localRotation : Quaternion.identity;
            Vector3 preservedLocalScale = existingPrefab != null ? existingPrefab.transform.localScale : Vector3.one;
            TreeCollisionSettings collisionSettings = GetCollisionSettings(existingPrefab, tree);

            GameObject source = GameObject.Find(tree.SceneObjectName);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(tree.ModelPath);
            GameObject renderSource = source != null ? source : modelAsset;
            if (renderSource == null)
            {
                throw new InvalidOperationException($"Missing tree source model at {tree.ModelPath}.");
            }

            MeshFilter sourceMeshFilter = renderSource.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = renderSource.GetComponent<MeshRenderer>();
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null || sourceRenderer == null)
            {
                throw new InvalidOperationException($"{renderSource.name} must have a MeshFilter and MeshRenderer.");
            }

            GameObject root = new(tree.DisplayName);
            root.layer = treeTrunkLayer;
            root.isStatic = true;
            root.transform.SetLocalPositionAndRotation(preservedLocalPosition, preservedLocalRotation);
            root.transform.localScale = preservedLocalScale;

            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateOrUpdateBakedMesh(tree, sourceMeshFilter.sharedMesh, renderSource.transform);

            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = BuildRendererMaterials(sourceRenderer.sharedMaterials);
            meshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            meshRenderer.receiveShadows = sourceRenderer.receiveShadows;
            ConfigureSingleLodGroup(root, meshRenderer);

            MMOTerrainTreeCollisionDefinition collisionDefinition = root.AddComponent<MMOTerrainTreeCollisionDefinition>();
            collisionDefinition.Configure(
                collisionSettings.TrunkRadius,
                collisionSettings.TrunkHeight,
                collisionSettings.TrunkCenterYOffset,
                tree.GroundEmbedDepth);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, tree.PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureSingleLodGroup(GameObject root, Renderer renderer)
        {
            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = root.AddComponent<LODGroup>();
            }

            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[]
            {
                new LOD(SingleLodScreenRelativeTransitionHeight, new[] { renderer })
            });
            lodGroup.RecalculateBounds();
        }

        private static Mesh CreateOrUpdateBakedMesh(TreeDefinition tree, Mesh sourceMesh, Transform sourceTransform)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(tree.MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, tree.MeshPath);
            }

            Matrix4x4 matrix = sourceTransform != null
                ? Matrix4x4.TRS(Vector3.zero, sourceTransform.rotation, sourceTransform.lossyScale)
                : Matrix4x4.identity;

            Vector3[] vertices = sourceMesh.vertices;
            float minY = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                minY = Mathf.Min(minY, vertices[i].y);
            }

            if (!float.IsInfinity(minY))
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].y -= minY + tree.GroundEmbedDepth;
                }
            }

            Vector3[] normals = sourceMesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = matrix.MultiplyVector(normals[i]).normalized;
            }

            Vector4[] tangents = sourceMesh.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangent = matrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }

            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(tree.MeshPath);
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = sourceMesh.uv;
            mesh.uv2 = sourceMesh.uv2;
            mesh.uv3 = sourceMesh.uv3;
            mesh.uv4 = sourceMesh.uv4;
            mesh.colors = sourceMesh.colors;
            mesh.subMeshCount = sourceMesh.subMeshCount;
            for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                mesh.SetTriangles(sourceMesh.GetTriangles(subMesh), subMesh);
            }

            if (mesh.normals == null || mesh.normals.Length == 0)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material[] BuildRendererMaterials(Material[] sourceMaterials)
        {
            Material fallback = null;
            if (sourceMaterials != null)
            {
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    if (sourceMaterials[i] != null)
                    {
                        fallback = sourceMaterials[i];
                        break;
                    }
                }
            }

            fallback ??= AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                return new[] { fallback };
            }

            Material[] materials = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                materials[i] = sourceMaterials[i] != null ? sourceMaterials[i] : fallback;
            }

            return materials;
        }

        private static void RemoveBrokenTreeDetails(TerrainData terrainData, GameObject[] treePrefabs)
        {
            List<DetailPrototype> details = new(terrainData.detailPrototypes);
            details.RemoveAll(detail =>
            {
                if (detail == null || detail.prototype == null)
                {
                    return false;
                }

                for (int i = 0; i < treePrefabs.Length; i++)
                {
                    if (detail.prototype == treePrefabs[i] || detail.prototype.name == treePrefabs[i].name)
                    {
                        return true;
                    }
                }

                return detail.prototype.name.Contains("Orcish Ember Tree", StringComparison.OrdinalIgnoreCase);
            });

            terrainData.detailPrototypes = details.ToArray();
            terrainData.RefreshPrototypes();
        }

        private static void ApplyTreePrototypes(TerrainData terrainData, GameObject[] treePrefabs)
        {
            List<TreePrototype> prototypes = new(terrainData.treePrototypes);
            prototypes.RemoveAll(prototype =>
            {
                if (prototype == null || prototype.prefab == null)
                {
                    return false;
                }

                for (int i = 0; i < treePrefabs.Length; i++)
                {
                    if (prototype.prefab == treePrefabs[i] || prototype.prefab.name == treePrefabs[i].name)
                    {
                        return true;
                    }
                }

                return prototype.prefab.name.Contains("Orcish Ember Tree", StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < treePrefabs.Length; i++)
            {
                prototypes.Add(new TreePrototype
                {
                    prefab = treePrefabs[i],
                    bendFactor = 0.15f,
                    navMeshLod = 0
                });
            }

            terrainData.treePrototypes = prototypes.ToArray();
            terrainData.RefreshPrototypes();
        }

        private static void EnsurePaintedTreeRotationVariation(TerrainData terrainData)
        {
            TreeInstance[] treeInstances = terrainData.treeInstances;
            if (treeInstances == null || treeInstances.Length == 0 || !ShouldSeedTreeRotations(treeInstances))
            {
                return;
            }

            for (int i = 0; i < treeInstances.Length; i++)
            {
                TreeInstance treeInstance = treeInstances[i];
                treeInstance.rotation = DeterministicTreeRotation(treeInstance, i);
                treeInstances[i] = treeInstance;
            }

            terrainData.treeInstances = treeInstances;
            EditorUtility.SetDirty(terrainData);
        }

        private static bool ShouldSeedTreeRotations(TreeInstance[] treeInstances)
        {
            int zeroRotationCount = 0;
            for (int i = 0; i < treeInstances.Length; i++)
            {
                if (Mathf.Abs(treeInstances[i].rotation) <= 0.0001f)
                {
                    zeroRotationCount++;
                }
            }

            return zeroRotationCount >= Mathf.CeilToInt(treeInstances.Length * 0.95f);
        }

        private static float DeterministicTreeRotation(TreeInstance treeInstance, int index)
        {
            unchecked
            {
                int hash = TreeRotationSeed;
                hash = hash * 397 ^ treeInstance.prototypeIndex;
                hash = hash * 397 ^ index;
                hash = hash * 397 ^ Mathf.RoundToInt(treeInstance.position.x * 100000f);
                hash = hash * 397 ^ Mathf.RoundToInt(treeInstance.position.z * 100000f);
                uint positiveHash = (uint)hash & 0x7FFFFFFF;
                return positiveHash / (float)0x7FFFFFFF * FullRotationRadians;
            }
        }

        private static void ConfigureTerrainTreeRendering(Terrain terrain)
        {
            terrain.drawTreesAndFoliage = true;
            terrain.treeDistance = TreeDrawDistance;
            terrain.treeBillboardDistance = TreeDrawDistance;
            terrain.treeMaximumFullLODCount = MaxFullLodTrees;
            terrain.preserveTreePrototypeLayers = true;
        }

        private static void ConfigureCameraCollisionMask(int treeTrunkLayer)
        {
            MMOThirdPersonCameraConfig cameraConfig = AssetDatabase.LoadAssetAtPath<MMOThirdPersonCameraConfig>(CameraConfigPath);
            if (cameraConfig == null)
            {
                return;
            }

            cameraConfig.ResetCollisionMaskToDefault();
            EditorUtility.SetDirty(cameraConfig);
        }

        private static void ConfigureNavMeshSurfaces(int treeTrunkLayer)
        {
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include);
            foreach (NavMeshSurface surface in surfaces)
            {
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = surface.layerMask.value | (1 << treeTrunkLayer);
                surface.ignoreNavMeshObstacle = false;
                surface.BuildNavMesh();
                EditorUtility.SetDirty(surface);
            }
        }

        private static void EnsureCollisionSynchronizer(Terrain terrain)
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

        private static void CleanupRemovedTreeSystems(Terrain terrain)
        {
            Transform collisionRoot = terrain.transform.Find(CollisionRootName);
            if (collisionRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(collisionRoot.gameObject);
            }

            Component[] components = terrain.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component != null && component.GetType().FullName == RemovedSynchronizerTypeName)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(terrain.gameObject);
        }

        private static TreeCollisionSettings GetCollisionSettings(GameObject existingPrefab, TreeDefinition fallback)
        {
            if (existingPrefab != null && existingPrefab.TryGetComponent(out MMOTerrainTreeCollisionDefinition collisionDefinition))
            {
                return new TreeCollisionSettings(
                    collisionDefinition.TrunkRadius,
                    collisionDefinition.TrunkHeight,
                    collisionDefinition.TrunkCenterYOffset);
            }

            return new TreeCollisionSettings(
                fallback.TrunkRadius,
                fallback.TrunkHeight,
                fallback.TrunkCenterYOffset);
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(GeneratedFolder);
            CreateFolderIfMissing(MeshFolder);
            CreateFolderIfMissing(PrefabFolder);
        }

        private static int EnsureLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                return layer;
            }

            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            throw new InvalidOperationException($"No free Unity layer slot is available for {layerName}.");
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

        private readonly struct TreeDefinition
        {
            public TreeDefinition(
                string displayName,
                string sceneObjectName,
                string modelPath,
                string prefabPath,
                string meshPath,
                float trunkRadius,
                float trunkHeight,
                float trunkCenterYOffset,
                float groundEmbedDepth)
            {
                DisplayName = displayName;
                SceneObjectName = sceneObjectName;
                ModelPath = modelPath;
                PrefabPath = prefabPath;
                MeshPath = meshPath;
                TrunkRadius = trunkRadius;
                TrunkHeight = trunkHeight;
                TrunkCenterYOffset = trunkCenterYOffset;
                GroundEmbedDepth = Mathf.Max(0f, groundEmbedDepth);
            }

            public string DisplayName { get; }
            public string SceneObjectName { get; }
            public string ModelPath { get; }
            public string PrefabPath { get; }
            public string MeshPath { get; }
            public float TrunkRadius { get; }
            public float TrunkHeight { get; }
            public float TrunkCenterYOffset { get; }
            public float GroundEmbedDepth { get; }
        }

        private readonly struct TreeCollisionSettings
        {
            public TreeCollisionSettings(float trunkRadius, float trunkHeight, float trunkCenterYOffset)
            {
                TrunkRadius = trunkRadius;
                TrunkHeight = trunkHeight;
                TrunkCenterYOffset = trunkCenterYOffset;
            }

            public float TrunkRadius { get; }
            public float TrunkHeight { get; }
            public float TrunkCenterYOffset { get; }
        }
    }
}
