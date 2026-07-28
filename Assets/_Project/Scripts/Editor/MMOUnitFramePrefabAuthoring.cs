using System.Collections.Generic;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOUnitFramePrefabAuthoring
    {
        public const string PrefabFolder = "Assets/Resources/RPGClone/UI/UnitFrames";
        public const string PlayerPrefabPath = PrefabFolder + "/PlayerUnitFrame.prefab";
        public const string TargetPrefabPath = PrefabFolder + "/TargetUnitFrame.prefab";
        public const string PartyPrefabPath = PrefabFolder + "/PartyUnitFrame.prefab";

        private const float PrimaryFrameSpacing = 32f;
        private const float PartyFrameTopSpacing = 32f;
        private const float PartyFrameSpacing = 16f;

        [MenuItem("Tools/RPG Clone/UI/Sync Unit Frame Scene Instances From Prefabs")]
        public static void SyncUnitFrameSceneInstancesFromPrefabs()
        {
            SyncActiveSceneFromPrefabs();
            Debug.Log("Unit-frame scene instances synchronized from the editable prefabs.");
        }

        private static void SyncActiveSceneFromPrefabs()
        {
            MMOUnitFramePresenter presenter = Object.FindAnyObjectByType<MMOUnitFramePresenter>(
                FindObjectsInactive.Include);
            if (presenter == null)
            {
                Debug.LogWarning("No MMOUnitFramePresenter was found in the active scene.");
                return;
            }

            SerializedObject serializedPresenter = new(presenter);
            MMOUnitFrameView existingPlayer =
                serializedPresenter.FindProperty("playerFrame").objectReferenceValue as MMOUnitFrameView;
            MMOUnitFrameView existingTarget =
                serializedPresenter.FindProperty("targetFrame").objectReferenceValue as MMOUnitFrameView;
            Transform frameParent = existingPlayer != null
                ? existingPlayer.transform.parent
                : presenter.transform;

            MMOUnitFrameView player = ReplaceFrame(
                existingPlayer,
                PlayerPrefabPath,
                "Player Unit Frame",
                frameParent);
            MMOUnitFrameView target = ReplaceFrame(
                existingTarget,
                TargetPrefabPath,
                "Target Unit Frame",
                frameParent);
            MMOUnitFrameView partyPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath)
                    ?.GetComponent<MMOUnitFrameView>();

            serializedPresenter.Update();
            serializedPresenter.FindProperty("playerFrame").objectReferenceValue = player;
            serializedPresenter.FindProperty("targetFrame").objectReferenceValue = target;
            serializedPresenter.FindProperty("partyFramePrefab").objectReferenceValue = partyPrefab;
            serializedPresenter.FindProperty("primaryFrameSpacing").floatValue = PrimaryFrameSpacing;
            serializedPresenter.FindProperty("partyFrameTopSpacing").floatValue = PartyFrameTopSpacing;
            serializedPresenter.FindProperty("partyFrameSpacing").floatValue = PartyFrameSpacing;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            presenter.ApplyConfiguredSpacing();
            EditorUtility.SetDirty(presenter);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static MMOUnitFrameView ReplaceFrame(
            MMOUnitFrameView existing,
            string prefabPath,
            string objectName,
            Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new UnityException($"Unit-frame prefab is missing at '{prefabPath}'.");
            }

            if (existing != null
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject) == prefabPath)
            {
                RemoveSceneAppearanceOverrides(existing);
                return existing;
            }

            RectTransform existingRect = existing != null ? existing.transform as RectTransform : null;
            Vector2 anchorMin = existingRect != null ? existingRect.anchorMin : new Vector2(0f, 1f);
            Vector2 anchorMax = existingRect != null ? existingRect.anchorMax : new Vector2(0f, 1f);
            Vector2 pivot = existingRect != null ? existingRect.pivot : new Vector2(0f, 1f);
            Vector2 position = existingRect != null ? existingRect.anchoredPosition : new Vector2(32f, -32f);
            bool wasActive = existing == null || existing.gameObject.activeSelf;
            int siblingIndex = existing != null ? existing.transform.GetSiblingIndex() : parent.childCount;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = objectName;
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.SetSiblingIndex(siblingIndex);
            instance.SetActive(wasActive);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            return instance.GetComponent<MMOUnitFrameView>();
        }

        private static void RemoveSceneAppearanceOverrides(MMOUnitFrameView frame)
        {
            RectTransform rect = frame.transform as RectTransform;
            if (rect == null || !PrefabUtility.IsPartOfPrefabInstance(rect))
            {
                return;
            }

            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(frame.gameObject);
            if (modifications == null)
            {
                return;
            }

            Object sourceGameObject =
                PrefabUtility.GetCorrespondingObjectFromSource(frame.gameObject);
            Object sourceRect = PrefabUtility.GetCorrespondingObjectFromSource(rect);
            HashSet<Object> revertedObjects = new();
            foreach (PropertyModification modification in modifications)
            {
                if (IsScenePlacementOverride(modification, sourceGameObject, sourceRect))
                {
                    continue;
                }

                Object instanceTarget = FindInstanceObject(frame.transform, modification.target);
                if (instanceTarget == null)
                {
                    continue;
                }

                if (instanceTarget != rect && revertedObjects.Add(instanceTarget))
                {
                    PrefabUtility.RevertObjectOverride(
                        instanceTarget,
                        InteractionMode.AutomatedAction);
                    continue;
                }

                SerializedObject serializedTarget = new(instanceTarget);
                string propertyPath = GetTopLevelPropertyPath(modification.propertyPath);
                SerializedProperty instanceProperty =
                    serializedTarget.FindProperty(propertyPath);
                if (instanceProperty != null)
                {
                    PrefabUtility.RevertPropertyOverride(
                        instanceProperty,
                        InteractionMode.AutomatedAction);
                }
            }
        }

        private static string GetTopLevelPropertyPath(string propertyPath)
        {
            int separator = propertyPath.IndexOf('.');
            return separator >= 0 ? propertyPath.Substring(0, separator) : propertyPath;
        }

        private static Object FindInstanceObject(Transform instanceRoot, Object sourceObject)
        {
            foreach (Transform candidate in instanceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject) == sourceObject)
                {
                    return candidate.gameObject;
                }

                foreach (Component component in candidate.GetComponents<Component>())
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSource(component) == sourceObject)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static bool IsScenePlacementOverride(
            PropertyModification modification,
            Object sourceGameObject,
            Object sourceRect)
        {
            if (modification.target == sourceGameObject)
            {
                return modification.propertyPath == "m_Name"
                    || modification.propertyPath == "m_IsActive";
            }

            if (modification.target != sourceRect)
            {
                return false;
            }

            string path = modification.propertyPath;
            return path.StartsWith("m_Anchor")
                || path.StartsWith("m_Pivot")
                || path.StartsWith("m_AnchoredPosition")
                || path.StartsWith("m_LocalPosition")
                || path.StartsWith("m_LocalRotation")
                || path.StartsWith("m_LocalEulerAnglesHint")
                || path.StartsWith("m_LocalScale")
                || path == "m_ConstrainProportionsScale";
        }

    }
}
