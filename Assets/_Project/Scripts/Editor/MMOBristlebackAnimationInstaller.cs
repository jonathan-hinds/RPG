using System;
using RPGClone.Animation;
using RPGClone.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOBristlebackAnimationInstaller
    {
        private const string BristlebackPrefabPath = "Assets/Characters/Bristleback/Prefabs/BristlebackEnemy.prefab";

        [MenuItem("Tools/RPG Clone/Enemies/Install Bristleback Animations")]
        public static void InstallBristlebackAnimations()
        {
            MMOCreatureVisualAuthoringInstaller.CreateStandardCreatureVisualDefinitions();
            MMOCreatureVisualAuthoringInstaller.RebuildCreatureVisualPrefabs();
            Debug.Log("Bristleback animations are managed by the shared creature visual workflow under Tools/RPG Clone/Creatures.");
        }

        [MenuItem("Tools/RPG Clone/Enemies/Convert Scene Bristlebacks To Animated Prefab")]
        public static void ConvertSceneBristlebacksToAnimatedPrefab()
        {
            MMOCreatureVisualAuthoringInstaller.CreateStandardCreatureVisualDefinitions();
            MMOCreatureVisualAuthoringInstaller.RebuildCreatureVisualPrefabs();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BristlebackPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Cannot convert scene bristlebacks because the BristlebackEnemy prefab is missing.");
                return;
            }

            int convertedCount = 0;
            int refreshedCount = 0;
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject sceneObject in sceneObjects)
            {
                MMOEnemyController controller = sceneObject.GetComponent<MMOEnemyController>();
                if (!IsBristlebackSceneEnemy(sceneObject, controller))
                {
                    continue;
                }

                if (PrefabUtility.GetCorrespondingObjectFromSource(sceneObject) == prefab)
                {
                    SnapRootToTerrain(sceneObject.transform);
                    refreshedCount++;
                    continue;
                }

                ReplaceSceneEnemy(sceneObject, prefab, controller.Definition);
                convertedCount++;
            }

            if (convertedCount > 0 || refreshedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"Converted {convertedCount} bristleback scene instance(s) and refreshed {refreshedCount} existing animated bristleback instance(s).");
        }

        private static bool IsBristlebackSceneEnemy(GameObject sceneObject, MMOEnemyController controller)
        {
            if (sceneObject == null || controller == null)
            {
                return false;
            }

            if (sceneObject.name.StartsWith("Bristleback Creature", StringComparison.Ordinal))
            {
                return true;
            }

            string definitionPath = controller.Definition != null
                ? AssetDatabase.GetAssetPath(controller.Definition)
                : string.Empty;
            return definitionPath.IndexOf("/Bristleback_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReplaceSceneEnemy(GameObject source, GameObject prefab, MMOEnemyDefinition definition)
        {
            Transform parent = source.transform.parent;
            Vector3 position = GetGroundedPosition(source);
            Quaternion rotation = source.transform.rotation;
            string instanceName = source.name;
            Object.DestroyImmediate(source);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return;
            }

            instance.name = instanceName;
            instance.transform.SetPositionAndRotation(position, rotation);
            MMOEnemyController controller = instance.GetComponent<MMOEnemyController>();
            controller.SetDefinition(definition, true);
            SnapRootToTerrain(instance.transform);
            EditorUtility.SetDirty(instance);
        }

        private static Vector3 GetGroundedPosition(GameObject source)
        {
            Vector3 position = source.transform.position;
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            }

            return position;
        }

        private static void SnapRootToTerrain(Transform transformToGround)
        {
            if (transformToGround == null)
            {
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                return;
            }

            Vector3 position = transformToGround.position;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            transformToGround.position = position;
            EditorUtility.SetDirty(transformToGround);
        }
    }
}
