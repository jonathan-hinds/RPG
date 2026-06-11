using System;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.World.Foliage
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public sealed class MMOTerrainTreeCollisionSynchronizer : MonoBehaviour
    {
        [SerializeField] private string collisionRootName = "Generated Tree Trunk Blockers";
        [SerializeField] private string collisionLayerName = "TreeTrunk";
        [SerializeField] private bool syncAutomatically = true;
        [SerializeField] private bool createNavMeshObstacles = true;
        [SerializeField, Min(0.05f)] private float automaticSyncCheckInterval = 1f;

        private Terrain terrain;
        private int lastSyncedHash;
        private float nextSyncTime;

        private void OnEnable()
        {
            CacheTerrain();
            SyncNow();
        }

        private void OnValidate()
        {
            CacheTerrain();
            lastSyncedHash = 0;
        }

        private void Update()
        {
            if (!syncAutomatically)
            {
                return;
            }

            if (Time.realtimeSinceStartup < nextSyncTime)
            {
                return;
            }

            nextSyncTime = Time.realtimeSinceStartup + automaticSyncCheckInterval;
            SyncIfNeeded();
        }

        public void Configure(string newCollisionRootName, string newCollisionLayerName, bool newCreateNavMeshObstacles)
        {
            collisionRootName = string.IsNullOrWhiteSpace(newCollisionRootName) ? collisionRootName : newCollisionRootName;
            collisionLayerName = string.IsNullOrWhiteSpace(newCollisionLayerName) ? collisionLayerName : newCollisionLayerName;
            createNavMeshObstacles = newCreateNavMeshObstacles;
            lastSyncedHash = 0;
        }

        public void SyncIfNeeded()
        {
            int currentHash = ComputeTreeHash();
            if (currentHash == lastSyncedHash)
            {
                return;
            }

            SyncNow(currentHash);
        }

        public void SyncNow()
        {
            SyncNow(ComputeTreeHash());
        }

        private void SyncNow(int treeHash)
        {
            CacheTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            int collisionLayer = LayerMask.NameToLayer(collisionLayerName);
            if (collisionLayer < 0)
            {
                Debug.LogWarning($"{nameof(MMOTerrainTreeCollisionSynchronizer)} on {name} could not find layer '{collisionLayerName}'.", this);
                return;
            }

            Transform root = RecreateCollisionRoot(collisionLayer);
            TerrainData terrainData = terrain.terrainData;
            TreePrototype[] prototypes = terrainData.treePrototypes;
            TreeInstance[] treeInstances = terrainData.treeInstances;

            for (int i = 0; i < treeInstances.Length; i++)
            {
                TreeInstance treeInstance = treeInstances[i];
                if (!TryGetCollisionDefinition(prototypes, treeInstance.prototypeIndex, out MMOTerrainTreeCollisionDefinition definition))
                {
                    continue;
                }

                CreateTrunkBlocker(root, collisionLayer, terrainData, definition, treeInstance, i);
            }

            lastSyncedHash = treeHash;
        }

        private void CacheTerrain()
        {
            if (terrain == null)
            {
                terrain = GetComponent<Terrain>();
            }
        }

        private Transform RecreateCollisionRoot(int collisionLayer)
        {
            Transform existingRoot = transform.Find(collisionRootName);
            if (existingRoot != null)
            {
                DestroyObject(existingRoot.gameObject);
            }

            GameObject root = new(collisionRootName)
            {
                layer = collisionLayer,
                isStatic = true
            };
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private void CreateTrunkBlocker(
            Transform root,
            int collisionLayer,
            TerrainData terrainData,
            MMOTerrainTreeCollisionDefinition definition,
            TreeInstance treeInstance,
            int index)
        {
            GameObject prefab = terrainData.treePrototypes[treeInstance.prototypeIndex].prefab;
            Vector3 prefabScale = prefab != null ? prefab.transform.localScale : Vector3.one;
            float horizontalPrefabScale = Mathf.Max(0.01f, (Mathf.Abs(prefabScale.x) + Mathf.Abs(prefabScale.z)) * 0.5f);
            float verticalPrefabScale = Mathf.Max(0.01f, Mathf.Abs(prefabScale.y));
            float radius = Mathf.Max(0.05f, definition.TrunkRadius * treeInstance.widthScale * horizontalPrefabScale);
            float height = Mathf.Max(radius * 2f, definition.TrunkHeight * treeInstance.heightScale * verticalPrefabScale);
            Vector3 center = Vector3.up * Mathf.Clamp(definition.TrunkCenterYOffset * treeInstance.heightScale * verticalPrefabScale, radius, height - radius);
            float embedDepth = definition.GroundEmbedDepth * verticalPrefabScale;

            GameObject blocker = new($"{prefab.name} Trunk Blocker {index:0000}")
            {
                layer = collisionLayer,
                isStatic = true
            };
            blocker.transform.SetParent(root, true);
            blocker.transform.SetPositionAndRotation(
                GetTreeWorldPosition(terrainData, treeInstance) - Vector3.up * embedDepth,
                Quaternion.Euler(0f, treeInstance.rotation * Mathf.Rad2Deg, 0f));

            CapsuleCollider collider = blocker.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = height;
            collider.center = center;

            if (!createNavMeshObstacles)
            {
                return;
            }

            NavMeshObstacle obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = radius;
            obstacle.height = height;
            obstacle.center = center;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        private Vector3 GetTreeWorldPosition(TerrainData terrainData, TreeInstance treeInstance)
        {
            Vector3 terrainSize = terrainData.size;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 worldPosition = new(
                terrainPosition.x + treeInstance.position.x * terrainSize.x,
                terrainPosition.y,
                terrainPosition.z + treeInstance.position.z * terrainSize.z);
            worldPosition.y = terrain.SampleHeight(worldPosition) + terrainPosition.y;
            return worldPosition;
        }

        private int ComputeTreeHash()
        {
            CacheTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                return 0;
            }

            unchecked
            {
                TerrainData terrainData = terrain.terrainData;
                int hash = 17;
                hash = hash * 31 + terrainData.treeInstanceCount;
                hash = hash * 31 + terrainData.treePrototypes.Length;

                TreePrototype[] prototypes = terrainData.treePrototypes;
                for (int i = 0; i < prototypes.Length; i++)
                {
                    GameObject prefab = prototypes[i].prefab;
                    hash = hash * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
                    if (prefab == null)
                    {
                        continue;
                    }

                    Vector3 scale = prefab.transform.localScale;
                    hash = hash * 31 + Quantize(scale.x);
                    hash = hash * 31 + Quantize(scale.y);
                    hash = hash * 31 + Quantize(scale.z);

                    if (prefab.TryGetComponent(out MMOTerrainTreeCollisionDefinition definition))
                    {
                        hash = hash * 31 + Quantize(definition.TrunkRadius);
                        hash = hash * 31 + Quantize(definition.TrunkHeight);
                        hash = hash * 31 + Quantize(definition.TrunkCenterYOffset);
                        hash = hash * 31 + Quantize(definition.GroundEmbedDepth);
                    }
                }

                TreeInstance[] treeInstances = terrainData.treeInstances;
                for (int i = 0; i < treeInstances.Length; i++)
                {
                    TreeInstance treeInstance = treeInstances[i];
                    hash = hash * 31 + treeInstance.prototypeIndex;
                    hash = hash * 31 + Quantize(treeInstance.position.x);
                    hash = hash * 31 + Quantize(treeInstance.position.y);
                    hash = hash * 31 + Quantize(treeInstance.position.z);
                    hash = hash * 31 + Quantize(treeInstance.widthScale);
                    hash = hash * 31 + Quantize(treeInstance.heightScale);
                    hash = hash * 31 + Quantize(treeInstance.rotation);
                }

                return hash;
            }
        }

        private static bool TryGetCollisionDefinition(
            TreePrototype[] prototypes,
            int prototypeIndex,
            out MMOTerrainTreeCollisionDefinition definition)
        {
            definition = null;
            if (prototypeIndex < 0 || prototypeIndex >= prototypes.Length)
            {
                return false;
            }

            GameObject prefab = prototypes[prototypeIndex].prefab;
            return prefab != null && prefab.TryGetComponent(out definition);
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 10000f);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
