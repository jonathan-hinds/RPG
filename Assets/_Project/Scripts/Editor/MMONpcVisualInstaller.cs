using System;
using System.Collections.Generic;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using RPGClone.Quests;
using RPGClone.Trainers;
using RPGClone.Vendors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMONpcVisualInstaller
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PlayerCapsule.prefab";
        private const string PlayerModelPath = "Assets/Player/Models/Idle.fbx";
        private const string AnimationSetPath = "Assets/Player/Animations/Clips/CharacterTest_PlayerLocomotion.asset";
        private const string AppearanceCatalogPath = "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset";

        [MenuItem("Tools/RPG Clone/NPCs/Install Humanoid Visual Authoring In Active Scene")]
        public static void InstallInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Open and save a gameplay scene before installing NPC visuals.");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            MMOPlayerLocomotionAnimationSet animationSet =
                AssetDatabase.LoadAssetAtPath<MMOPlayerLocomotionAnimationSet>(AnimationSetPath);
            MMOCharacterAppearanceCatalog appearanceCatalog =
                AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(AppearanceCatalogPath);
            if (!TryReadPlayerVisualSetup(playerPrefab, modelPrefab, out PlayerVisualSetup playerVisualSetup)
                || animationSet == null
                || appearanceCatalog == null)
            {
                Debug.LogError(
                    $"NPC visual installation requires a configured player prefab at {PlayerPrefabPath}, " +
                    $"plus {PlayerModelPath}, {AnimationSetPath}, and {AppearanceCatalogPath}.");
                return;
            }

            List<GameObject> targets = FindHumanoidNpcTargets(scene);
            if (targets.Count == 0)
            {
                Debug.LogWarning($"No quest, vendor, or trainer NPCs were found in {scene.name}.");
                return;
            }

            Undo.SetCurrentGroupName("Install NPC Humanoid Visuals");
            int undoGroup = Undo.GetCurrentGroup();
            foreach (GameObject target in targets)
            {
                InstallOnNpc(target, playerVisualSetup, animationSet, appearanceCatalog);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Installed player-compatible idle visuals on {targets.Count} NPCs in {scene.name}.");
        }

        public static bool InstallOnNpc(GameObject target)
        {
            if (target == null)
            {
                Debug.LogError("A valid NPC GameObject is required to install humanoid visuals.");
                return false;
            }

            Scene scene = target.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("The NPC must belong to an open, saved gameplay scene.", target);
                return false;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            MMOPlayerLocomotionAnimationSet animationSet =
                AssetDatabase.LoadAssetAtPath<MMOPlayerLocomotionAnimationSet>(AnimationSetPath);
            MMOCharacterAppearanceCatalog appearanceCatalog =
                AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(AppearanceCatalogPath);
            if (!TryReadPlayerVisualSetup(playerPrefab, modelPrefab, out PlayerVisualSetup playerVisualSetup)
                || animationSet == null
                || appearanceCatalog == null)
            {
                Debug.LogError(
                    $"NPC visual installation requires a configured player prefab at {PlayerPrefabPath}, " +
                    $"plus {PlayerModelPath}, {AnimationSetPath}, and {AppearanceCatalogPath}.",
                    target);
                return false;
            }

            InstallOnNpc(target, playerVisualSetup, animationSet, appearanceCatalog);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void InstallOnNpc(
            GameObject target,
            PlayerVisualSetup playerVisualSetup,
            MMOPlayerLocomotionAnimationSet animationSet,
            MMOCharacterAppearanceCatalog appearanceCatalog)
        {
            Undo.RegisterFullObjectHierarchyUndo(target, "Install NPC Humanoid Visual");
            target.transform.localScale = playerVisualSetup.RootLocalScale;

            CapsuleCollider capsule = target.GetComponent<CapsuleCollider>()
                ?? Undo.AddComponent<CapsuleCollider>(target);
            capsule.center = playerVisualSetup.CapsuleCenter;
            capsule.radius = playerVisualSetup.CapsuleRadius;
            capsule.height = playerVisualSetup.CapsuleHeight;
            capsule.direction = playerVisualSetup.CapsuleDirection;

            MMONpcVisualAuthoring authoring = target.GetComponent<MMONpcVisualAuthoring>()
                ?? Undo.AddComponent<MMONpcVisualAuthoring>(target);
            MMOCharacterAppearanceVisuals appearanceVisuals = target.GetComponent<MMOCharacterAppearanceVisuals>();
            MMOPlayerEquipmentVisuals equipmentVisuals = target.GetComponent<MMOPlayerEquipmentVisuals>();
            MMOPlayerLocomotionAnimator locomotionAnimator = target.GetComponent<MMOPlayerLocomotionAnimator>()
                ?? Undo.AddComponent<MMOPlayerLocomotionAnimator>(target);

            SetAppearanceCatalog(authoring, appearanceCatalog);

            Transform visualTransform = FindDirectChild(target.transform, "Character Visual");
            if (visualTransform == null)
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(
                    playerVisualSetup.ModelPrefab,
                    target.transform) as GameObject;
                if (visual == null)
                {
                    Debug.LogError($"Could not create a humanoid visual for {target.name}.", target);
                    return;
                }

                Undo.RegisterCreatedObjectUndo(visual, "Create NPC Character Visual");
                visual.name = "Character Visual";
                visualTransform = visual.transform;
            }

            visualTransform.SetLocalPositionAndRotation(
                playerVisualSetup.VisualLocalPosition,
                playerVisualSetup.VisualLocalRotation);
            visualTransform.localScale = playerVisualSetup.VisualLocalScale;

            Animator animator = visualTransform.GetComponent<Animator>()
                ?? Undo.AddComponent<Animator>(visualTransform.gameObject);
            animator.runtimeAnimatorController = animationSet.BaseController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            locomotionAnimator.Configure(animationSet, animator, visualTransform, null);
            locomotionAnimator.ConfigureStrafePresentation(false, 0f, 0f, false, 0f, 0f, null);
            locomotionAnimator.SetLocomotionSource(authoring);

            equipmentVisuals.Configure(null, BuildBodyPartSlots(visualTransform));
            appearanceVisuals.Configure(
                appearanceCatalog,
                appearanceCatalog.DefaultHeadStyleId,
                authoring.FaceId,
                authoring.HairstyleId);
            authoring.ApplySelections();

            MeshRenderer capsuleRenderer = target.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null)
            {
                capsuleRenderer.enabled = false;
            }

            EditorUtility.SetDirty(target);
        }

        private static bool TryReadPlayerVisualSetup(
            GameObject playerPrefab,
            GameObject modelPrefab,
            out PlayerVisualSetup setup)
        {
            setup = default;
            if (playerPrefab == null || modelPrefab == null)
            {
                return false;
            }

            Transform sourceVisual = FindDirectChild(playerPrefab.transform, "Character Visual");
            CharacterController sourceCapsule = playerPrefab.GetComponent<CharacterController>();
            if (sourceVisual == null || sourceCapsule == null)
            {
                return false;
            }

            setup = new PlayerVisualSetup(
                modelPrefab,
                playerPrefab.transform.localScale,
                sourceVisual.localPosition,
                sourceVisual.localRotation,
                sourceVisual.localScale,
                sourceCapsule.center,
                sourceCapsule.radius,
                sourceCapsule.height,
                1);
            return true;
        }

        private static void SetAppearanceCatalog(
            MMONpcVisualAuthoring authoring,
            MMOCharacterAppearanceCatalog appearanceCatalog)
        {
            SerializedObject serializedAuthoring = new(authoring);
            serializedAuthoring.FindProperty("appearanceCatalog").objectReferenceValue = appearanceCatalog;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<GameObject> FindHumanoidNpcTargets(Scene scene)
        {
            HashSet<GameObject> targets = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AddTargets(root.GetComponentsInChildren<MMOQuestNpc>(true), targets);
                AddTargets(root.GetComponentsInChildren<MMOVendorNpc>(true), targets);
                AddTargets(root.GetComponentsInChildren<MMOClassTrainerNpc>(true), targets);
            }

            return targets.OrderBy(target => target.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddTargets<T>(IEnumerable<T> components, ISet<GameObject> targets)
            where T : Component
        {
            foreach (T component in components)
            {
                if (component != null)
                {
                    targets.Add(component.gameObject);
                }
            }
        }

        private static List<MMOBodyPartRendererSlot> BuildBodyPartSlots(Transform visualRoot)
        {
            Dictionary<MMOCharacterBodyPart, List<Renderer>> renderersByPart = new();
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                MMOCharacterBodyPart bodyPart = ResolveBodyPart(renderer);
                if (!renderersByPart.TryGetValue(bodyPart, out List<Renderer> renderers))
                {
                    renderers = new List<Renderer>();
                    renderersByPart[bodyPart] = renderers;
                }

                renderers.Add(renderer);
            }

            return renderersByPart
                .Select(pair => new MMOBodyPartRendererSlot(
                    pair.Key,
                    pair.Value.FirstOrDefault()?.transform ?? visualRoot,
                    pair.Value.ToArray()))
                .ToList();
        }

        private static MMOCharacterBodyPart ResolveBodyPart(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Texture texture = material != null ? material.mainTexture : null;
                if (TryResolveBodyPartName(texture != null ? texture.name : null, out MMOCharacterBodyPart bodyPart)
                    || TryResolveBodyPartName(material != null ? material.name : null, out bodyPart))
                {
                    return bodyPart;
                }
            }

            return TryResolveBodyPartName(renderer.name, out MMOCharacterBodyPart rendererPart)
                ? rendererPart
                : MMOCharacterBodyPart.Torso;
        }

        private static bool TryResolveBodyPartName(string candidate, out MMOCharacterBodyPart bodyPart)
        {
            string normalized = string.IsNullOrWhiteSpace(candidate)
                ? string.Empty
                : candidate.Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace(".", string.Empty)
                    .ToLowerInvariant();

            if (normalized.Contains("head"))
            {
                bodyPart = MMOCharacterBodyPart.Head;
                return true;
            }

            if (normalized.Contains("hand"))
            {
                bodyPart = MMOCharacterBodyPart.Hands;
                return true;
            }

            if (normalized.Contains("torso") || normalized.Contains("chest"))
            {
                bodyPart = MMOCharacterBodyPart.Torso;
                return true;
            }

            if (normalized.Contains("leg"))
            {
                bodyPart = MMOCharacterBodyPart.Legs;
                return true;
            }

            if (normalized.Contains("feet") || normalized.Contains("foot") || normalized.Contains("boot"))
            {
                bodyPart = MMOCharacterBodyPart.Feet;
                return true;
            }

            bodyPart = MMOCharacterBodyPart.Torso;
            return false;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private readonly struct PlayerVisualSetup
        {
            public PlayerVisualSetup(
                GameObject modelPrefab,
                Vector3 rootLocalScale,
                Vector3 visualLocalPosition,
                Quaternion visualLocalRotation,
                Vector3 visualLocalScale,
                Vector3 capsuleCenter,
                float capsuleRadius,
                float capsuleHeight,
                int capsuleDirection)
            {
                ModelPrefab = modelPrefab;
                RootLocalScale = rootLocalScale;
                VisualLocalPosition = visualLocalPosition;
                VisualLocalRotation = visualLocalRotation;
                VisualLocalScale = visualLocalScale;
                CapsuleCenter = capsuleCenter;
                CapsuleRadius = capsuleRadius;
                CapsuleHeight = capsuleHeight;
                CapsuleDirection = capsuleDirection;
            }

            public GameObject ModelPrefab { get; }
            public Vector3 RootLocalScale { get; }
            public Vector3 VisualLocalPosition { get; }
            public Quaternion VisualLocalRotation { get; }
            public Vector3 VisualLocalScale { get; }
            public Vector3 CapsuleCenter { get; }
            public float CapsuleRadius { get; }
            public float CapsuleHeight { get; }
            public int CapsuleDirection { get; }
        }
    }
}
