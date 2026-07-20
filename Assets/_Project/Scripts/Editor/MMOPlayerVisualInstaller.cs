using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOPlayerVisualInstaller
    {
        private const string PlayerModelPath = "Assets/Player/Models/Idle.fbx";
        private const string PlayerModelFolder = "Assets/Player/Models";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PlayerCapsule.prefab";
        private const string BaseControllerPath = "Assets/_Project/Animations/Creatures/MMOCreatureBase.controller";
        private const string LayeredControllerPath = MMOLayeredAnimationInstaller.PlayerControllerPath;
        private const string AnimationClipFolder = "Assets/Player/Animations/Clips";
        private const string AnimationSetPath = AnimationClipFolder + "/CharacterTest_PlayerLocomotion.asset";
        private const string CombatAnimationSetPath = AnimationClipFolder + "/CharacterTest_PlayerCombat.asset";
        private const string UpperBodyMaskPath = AnimationClipFolder + "/CharacterTest_UpperBody.mask";
        private const string JumpStartPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_JumpStart.anim";
        private const string JumpEndPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_JumpEnd.anim";
        private const string CombatIdlePlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_CombatIdle.anim";
        private const string OneHandAttackPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_Attack1H.anim";
        private const string TwoHandAttackPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_Attack2H.anim";
        private const string UnarmedAttackPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_AttackUnarmed.anim";
        private const string CombatDamagePlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_CombatDamage.anim";
        private const string CastingPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_Casting.anim";
        private const string CastPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_Cast.anim";
        private const string UpperBodyEmptyPlaceholderPath = "Assets/_Project/Animations/Creatures/MMO_UpperBodyEmpty.anim";
        private const float TargetHeight = 2.05f;
        private const float StrafeVisualYawSharpness = 16f;
        private const float MaxStrafeVisualYawDegrees = 78f;
        private const float UpperBodyCounterYawWeight = 0.65f;
        private const float MaxUpperBodyCounterYawDegrees = 42f;
        private const float MovingLandingPlanarSpeedThreshold = 1.2f;
        private const float MovingLandingHoldSeconds = 0.08f;
        private const float MovingLandingTransitionSeconds = 0.12f;

        [MenuItem("Tools/RPG Clone/Player/Install CharacterTest Visual")]
        public static void InstallCharacterTestVisual()
        {
            CreateFolderIfMissing(AnimationClipFolder);
            ConfigurePlayerModelImporters();
            EnsureBaseControllerSupportsPlayerJump();
            EnsureBaseControllerSupportsPlayerCombat();
            MMOPlayerLocomotionAnimationSet animationSet = CreateOrUpdateAnimationSet();
            MMOPlayerCombatAnimationSet combatAnimationSet = CreateOrUpdateCombatAnimationSet();
            GameObject prefab = UpdatePlayerPrefab(animationSet, combatAnimationSet);
            RestoreActiveScenePlayerVisualInheritance();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Debug.Log($"Installed player visual prefab at {PlayerPrefabPath}.");
            }
        }

        [MenuItem("Tools/RPG Clone/Player/Restore Prefab Visual Inheritance")]
        public static void RestoreActiveScenePlayerVisualInheritance()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded || string.IsNullOrEmpty(activeScene.path))
            {
                Debug.LogWarning("Open a saved gameplay scene before restoring player prefab visual inheritance.");
                return;
            }

            GameObject player = activeScene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.CompareTag("Player"));
            if (player == null)
            {
                Debug.LogWarning($"Could not find a root player tagged Player in {activeScene.name}.");
                return;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
            if (!string.Equals(prefabPath, PlayerPrefabPath, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"The scene player in {activeScene.name} is not an instance of {PlayerPrefabPath}. " +
                    "Visual inheritance was not changed.",
                    player);
                return;
            }

            bool changed = RevertAddedCharacterVisuals(player);
            changed |= RevertRemovedCharacterVisuals(player);
            changed |= RevertVisualReferenceOverrides(player.GetComponent<MMOPlayerLocomotionAnimator>(),
                "animator", "visualRoot", "upperBodyCounterYawBones");
            changed |= RevertVisualReferenceOverrides(player.GetComponent<MMOPlayerEquipmentVisuals>(),
                "bodyPartSlots");
            changed |= RevertVisualReferenceOverrides(player.GetComponent<MMOPlayerCombatAnimator>(),
                "animator");

            if (!changed)
            {
                Debug.Log($"The scene player in {activeScene.name} already inherits its visual from {PlayerPrefabPath}.", player);
                return;
            }

            PrefabUtility.RemoveUnusedOverrides(new[] { player }, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"Restored prefab-owned player visuals in {activeScene.name} from {PlayerPrefabPath}.", player);
        }

        private static bool RevertAddedCharacterVisuals(GameObject player)
        {
            bool changed = false;
            foreach (AddedGameObject addedObject in PrefabUtility.GetAddedGameObjects(player).ToArray())
            {
                GameObject instance = addedObject.instanceGameObject;
                if (instance == null || !string.Equals(instance.name, "Character Visual", StringComparison.Ordinal))
                {
                    continue;
                }

                addedObject.Revert(InteractionMode.AutomatedAction);
                changed = true;
            }

            return changed;
        }

        private static bool RevertRemovedCharacterVisuals(GameObject player)
        {
            bool changed = false;
            foreach (RemovedGameObject removedObject in PrefabUtility.GetRemovedGameObjects(player).ToArray())
            {
                if (!IsCharacterVisualObject(removedObject.assetGameObject))
                {
                    continue;
                }

                removedObject.Revert(InteractionMode.AutomatedAction);
                changed = true;
            }

            return changed;
        }

        private static bool IsCharacterVisualObject(GameObject candidate)
        {
            for (Transform current = candidate != null ? candidate.transform : null; current != null; current = current.parent)
            {
                if (string.Equals(current.name, "Character Visual", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RevertVisualReferenceOverrides(Component component, params string[] propertyRoots)
        {
            if (component == null)
            {
                return false;
            }

            SerializedObject serializedObject = new(component);
            SerializedProperty iterator = serializedObject.GetIterator();
            List<string> propertyPaths = new();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference
                    || !iterator.prefabOverride
                    || !propertyRoots.Any(root => iterator.propertyPath == root
                        || iterator.propertyPath.StartsWith(root + ".", StringComparison.Ordinal)))
                {
                    continue;
                }

                propertyPaths.Add(iterator.propertyPath);
            }

            foreach (string propertyPath in propertyPaths)
            {
                serializedObject.Update();
                SerializedProperty property = serializedObject.FindProperty(propertyPath);
                if (property != null && property.prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                }
            }

            return propertyPaths.Count > 0;
        }

        private static MMOPlayerLocomotionAnimationSet CreateOrUpdateAnimationSet()
        {
            AnimationClip idle = ExtractBestAnimationClip(
                PlayerModelFolder + "/Idle.fbx",
                new[] { "idle" },
                AnimationClipFolder + "/CharacterTest_Idle.anim",
                "CharacterTest_Idle",
                true,
                0);
            AnimationClip walkBackwards = ExtractBestAnimationClip(
                PlayerModelFolder + "/WalkBackwards.fbx",
                new[] { "walkbackwards", "walkback", "backwards", "backpedal" },
                AnimationClipFolder + "/CharacterTest_WalkBackwards.anim",
                "CharacterTest_WalkBackwards",
                true,
                0);
            AnimationClip run = ExtractBestAnimationClip(
                PlayerModelFolder + "/Run.fbx",
                new[] { "run", "jog", "move" },
                AnimationClipFolder + "/CharacterTest_Run.anim",
                "CharacterTest_Run",
                true,
                1);
            AnimationClip jumpStart = ExtractBestAnimationClip(
                PlayerModelFolder + "/JumpStart.fbx",
                new[] { "jumpstart", "jump_start", "start", "jump" },
                AnimationClipFolder + "/CharacterTest_JumpStart.anim",
                "CharacterTest_JumpStart",
                false,
                0);
            AnimationClip jumpEnd = ExtractBestAnimationClip(
                PlayerModelFolder + "/JumpEnd.fbx",
                new[] { "jumpend", "jump_end", "land", "end" },
                AnimationClipFolder + "/CharacterTest_JumpEnd.anim",
                "CharacterTest_JumpEnd",
                false,
                0);

            if (run == null)
            {
                run = idle;
                Debug.LogWarning("CharacterTest run clip was not found. The run slot is temporarily using the idle clip.");
            }

            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LayeredControllerPath);
            MMOPlayerLocomotionAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<MMOPlayerLocomotionAnimationSet>(AnimationSetPath);
            if (animationSet == null)
            {
                animationSet = ScriptableObject.CreateInstance<MMOPlayerLocomotionAnimationSet>();
                AssetDatabase.CreateAsset(animationSet, AnimationSetPath);
            }

            animationSet.name = "CharacterTest_PlayerLocomotion";
            animationSet.Configure(
                baseController,
                idle,
                walkBackwards,
                run,
                run,
                jumpStart,
                jumpEnd,
                4.1f,
                1.45f,
                7.25f,
                0.12f,
                false,
                0f,
                MovingLandingPlanarSpeedThreshold,
                MovingLandingHoldSeconds,
                MovingLandingTransitionSeconds);
            EditorUtility.SetDirty(animationSet);
            return animationSet;
        }

        private static MMOPlayerCombatAnimationSet CreateOrUpdateCombatAnimationSet()
        {
            AnimationClip combatIdle = ExtractBestAnimationClip(
                PlayerModelFolder + "/CombatIdle.fbx",
                new[] { "combatidle", "combat_idle", "idle" },
                AnimationClipFolder + "/CharacterTest_CombatIdle.anim",
                "CharacterTest_CombatIdle",
                true,
                0);
            AnimationClip twoHandCombatIdle = ExtractBestAnimationClip(
                PlayerModelFolder + "/CombatIdle2H.fbx",
                new[] { "2h", "twohand", "combatidle", "combat_idle", "idle" },
                AnimationClipFolder + "/CharacterTest_CombatIdle2H.anim",
                "CharacterTest_CombatIdle2H",
                true,
                0);
            AnimationClip attack = ExtractBestAnimationClip(
                PlayerModelFolder + "/Attack.fbx",
                new[] { "attack", "1h", "onehand" },
                AnimationClipFolder + "/CharacterTest_Attack1H.anim",
                "CharacterTest_Attack1H",
                false,
                0);
            AnimationClip twoHandAttack = ExtractBestAnimationClip(
                PlayerModelFolder + "/Attack2H.fbx",
                new[] { "attack", "2h", "twohand" },
                AnimationClipFolder + "/CharacterTest_Attack2H.anim",
                "CharacterTest_Attack2H",
                false,
                0);
            AnimationClip damage = ExtractBestAnimationClip(
                PlayerModelFolder + "/CombatDamage.fbx",
                new[] { "damage", "hit", "hurt" },
                AnimationClipFolder + "/CharacterTest_CombatDamage.anim",
                "CharacterTest_CombatDamage",
                false,
                0);
            AnimationClip casting = ExtractBestAnimationClip(
                PlayerModelFolder + "/Casting.fbx",
                new[] { "casting", "channel", "loop" },
                AnimationClipFolder + "/CharacterTest_Casting.anim",
                "CharacterTest_Casting",
                true,
                0);
            AnimationClip cast = ExtractBestAnimationClip(
                PlayerModelFolder + "/Cast.fbx",
                new[] { "cast", "release" },
                AnimationClipFolder + "/CharacterTest_Cast.anim",
                "CharacterTest_Cast",
                false,
                0);

            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LayeredControllerPath);
            MMOPlayerCombatAnimationSet combatAnimationSet = AssetDatabase.LoadAssetAtPath<MMOPlayerCombatAnimationSet>(CombatAnimationSetPath);
            if (combatAnimationSet == null)
            {
                combatAnimationSet = ScriptableObject.CreateInstance<MMOPlayerCombatAnimationSet>();
                AssetDatabase.CreateAsset(combatAnimationSet, CombatAnimationSetPath);
            }

            combatAnimationSet.name = "CharacterTest_PlayerCombat";
            combatAnimationSet.Configure(
                baseController,
                combatIdle,
                twoHandCombatIdle,
                attack,
                twoHandAttack,
                null,
                damage,
                casting,
                cast,
                0.65f);
            EditorUtility.SetDirty(combatAnimationSet);
            return combatAnimationSet;
        }

        private static void EnsureBaseControllerSupportsPlayerJump()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (controller == null)
            {
                Debug.LogError($"Could not load base animator controller at {BaseControllerPath}.");
                return;
            }

            EnsureTriggerParameter(controller, MMOPlayerLocomotionAnimationSet.JumpStartParameter);
            EnsureTriggerParameter(controller, MMOPlayerLocomotionAnimationSet.JumpEndParameter);

            AnimationClip jumpStartPlaceholder = EnsurePlaceholderClip(
                JumpStartPlaceholderPath,
                MMOPlayerLocomotionAnimationSet.JumpStartPlaceholderName);
            AnimationClip jumpEndPlaceholder = EnsurePlaceholderClip(
                JumpEndPlaceholderPath,
                MMOPlayerLocomotionAnimationSet.JumpEndPlaceholderName);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = FindState(stateMachine, "Locomotion");
            AnimatorState jumpStartState = EnsureState(
                stateMachine,
                "JumpStart",
                jumpStartPlaceholder,
                new Vector3(520f, -190f, 0f));
            AnimatorState jumpEndState = EnsureState(
                stateMachine,
                "JumpEnd",
                jumpEndPlaceholder,
                new Vector3(520f, -310f, 0f));

            EnsureAnyStateTriggerTransition(
                stateMachine,
                jumpStartState,
                MMOPlayerLocomotionAnimationSet.JumpStartParameter,
                0.04f);
            EnsureAnyStateTriggerTransition(
                stateMachine,
                jumpEndState,
                MMOPlayerLocomotionAnimationSet.JumpEndParameter,
                0.04f);

            if (locomotionState != null)
            {
                RemoveExitTransition(jumpStartState, locomotionState);
                EnsureExitTransition(jumpEndState, locomotionState, 0.78f, 0.08f);
            }

            EditorUtility.SetDirty(controller);
        }

        private static void EnsureBaseControllerSupportsPlayerCombat()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (controller == null)
            {
                Debug.LogError($"Could not load base animator controller at {BaseControllerPath}.");
                return;
            }

            EnsureBoolParameter(controller, MMOPlayerCombatAnimationSet.InCombatParameter);
            EnsureFloatParameter(controller, MMOPlayerCombatAnimationSet.ActionSpeedParameter, 1f);

            AnimationClip combatIdlePlaceholder = EnsurePlaceholderClip(
                CombatIdlePlaceholderPath,
                MMOPlayerCombatAnimationSet.CombatIdlePlaceholderName,
                true);
            AnimationClip oneHandAttackPlaceholder = EnsurePlaceholderClip(
                OneHandAttackPlaceholderPath,
                MMOPlayerCombatAnimationSet.OneHandAttackPlaceholderName);
            AnimationClip twoHandAttackPlaceholder = EnsurePlaceholderClip(
                TwoHandAttackPlaceholderPath,
                MMOPlayerCombatAnimationSet.TwoHandAttackPlaceholderName);
            AnimationClip unarmedAttackPlaceholder = EnsurePlaceholderClip(
                UnarmedAttackPlaceholderPath,
                MMOPlayerCombatAnimationSet.UnarmedAttackPlaceholderName);
            AnimationClip damagePlaceholder = EnsurePlaceholderClip(
                CombatDamagePlaceholderPath,
                MMOPlayerCombatAnimationSet.DamagePlaceholderName);
            AnimationClip castingPlaceholder = EnsurePlaceholderClip(
                CastingPlaceholderPath,
                MMOPlayerCombatAnimationSet.CastingPlaceholderName,
                true);
            AnimationClip castPlaceholder = EnsurePlaceholderClip(
                CastPlaceholderPath,
                MMOPlayerCombatAnimationSet.CastPlaceholderName);
            AnimationClip emptyPlaceholder = EnsurePlaceholderClip(
                UpperBodyEmptyPlaceholderPath,
                "MMO_UpperBodyEmpty");

            AnimatorStateMachine baseStateMachine = controller.layers[0].stateMachine;
            EnsureState(
                baseStateMachine,
                "CombatIdle",
                combatIdlePlaceholder,
                new Vector3(250f, -430f, 0f),
                "Idle");
            EnsureActionState(baseStateMachine, "Attack1H", oneHandAttackPlaceholder, new Vector3(520f, -430f, 0f), "Attack");
            EnsureActionState(baseStateMachine, "Attack2H", twoHandAttackPlaceholder, new Vector3(520f, -540f, 0f), "Attack");
            EnsureActionState(baseStateMachine, "AttackUnarmed", unarmedAttackPlaceholder, new Vector3(520f, -650f, 0f), "Attack");
            EnsureActionState(baseStateMachine, "CombatDamage", damagePlaceholder, new Vector3(520f, -760f, 0f), "Damage");
            EnsureActionState(baseStateMachine, "Casting", castingPlaceholder, new Vector3(790f, -430f, 0f), "Cast");
            EnsureActionState(baseStateMachine, "Cast", castPlaceholder, new Vector3(790f, -540f, 0f), "Cast");

            AvatarMask upperBodyMask = CreateOrUpdateUpperBodyMask();
            AnimatorControllerLayer upperBodyLayer = EnsureLayer(
                controller,
                MMOPlayerCombatAnimationSet.UpperBodyLayerName,
                upperBodyMask);
            AnimatorStateMachine upperBodyStateMachine = upperBodyLayer.stateMachine;
            AnimatorState emptyState = EnsureState(
                upperBodyStateMachine,
                "Empty",
                emptyPlaceholder,
                new Vector3(240f, 40f, 0f),
                string.Empty);
            upperBodyStateMachine.defaultState = emptyState;

            RemoveStateIfPresent(upperBodyStateMachine, "Attack1H");
            RemoveStateIfPresent(upperBodyStateMachine, "Attack2H");
            RemoveStateIfPresent(upperBodyStateMachine, "AttackUnarmed");
            RemoveStateIfPresent(upperBodyStateMachine, "Casting");
            RemoveStateIfPresent(upperBodyStateMachine, "Cast");
            EnsureActionState(upperBodyStateMachine, "Damage", damagePlaceholder, new Vector3(520f, -40f, 0f), "Damage");

            EditorUtility.SetDirty(upperBodyStateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureTriggerParameter(AnimatorController controller, string parameterName)
        {
            if (controller.parameters.Any(parameter => parameter.name == parameterName))
            {
                return;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureBoolParameter(AnimatorController controller, string parameterName)
        {
            if (controller.parameters.Any(parameter => parameter.name == parameterName))
            {
                return;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
        }

        private static void EnsureFloatParameter(AnimatorController controller, string parameterName, float defaultValue)
        {
            AnimatorControllerParameter parameter = controller.parameters.FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter != null)
            {
                return;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Float);
            parameter = controller.parameters.FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter != null)
            {
                parameter.defaultFloat = defaultValue;
            }
        }

        private static AnimationClip EnsurePlaceholderClip(string path, string clipName)
        {
            return EnsurePlaceholderClip(path, clipName, false);
        }

        private static AnimationClip EnsurePlaceholderClip(string path, string clipName, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = clipName;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            AnimationClip motion,
            Vector3 position)
        {
            return EnsureState(stateMachine, stateName, motion, position, "Jump");
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            AnimationClip motion,
            Vector3 position,
            string stateTag)
        {
            AnimatorState state = FindState(stateMachine, stateName);
            if (state == null)
            {
                state = stateMachine.AddState(stateName, position);
            }

            state.motion = motion;
            state.tag = stateTag;
            state.writeDefaultValues = true;
            EditorUtility.SetDirty(state);
            return state;
        }

        private static AnimatorState EnsureActionState(
            AnimatorStateMachine stateMachine,
            string stateName,
            AnimationClip motion,
            Vector3 position,
            string stateTag)
        {
            AnimatorState state = EnsureState(stateMachine, stateName, motion, position, stateTag);
            state.speedParameter = MMOPlayerCombatAnimationSet.ActionSpeedParameter;
            state.speedParameterActive = true;
            return state;
        }

        private static void RemoveStateIfPresent(AnimatorStateMachine stateMachine, string stateName)
        {
            AnimatorState state = FindState(stateMachine, stateName);
            if (state != null)
            {
                stateMachine.RemoveState(state);
                EditorUtility.SetDirty(stateMachine);
            }
        }

        private static AnimatorControllerLayer EnsureLayer(
            AnimatorController controller,
            string layerName,
            AvatarMask avatarMask)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(layers, layer => layer.name == layerName);
            AnimatorControllerLayer existingLayer = layerIndex >= 0 ? layers[layerIndex] : null;
            if (existingLayer == null)
            {
                controller.AddLayer(layerName);
                layers = controller.layers;
                layerIndex = Array.FindIndex(layers, layer => layer.name == layerName);
                existingLayer = layers[layerIndex];
            }

            existingLayer.avatarMask = avatarMask;
            existingLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            existingLayer.defaultWeight = 0f;
            existingLayer.iKPass = false;
            layers[layerIndex] = existingLayer;
            controller.layers = layers;
            return existingLayer;
        }

        private static AvatarMask CreateOrUpdateUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            Transform root = modelPrefab != null ? modelPrefab.transform : null;
            if (root != null)
            {
                List<(string Path, bool Active)> paths = new();
                BuildUpperBodyMaskPaths(root, root, paths);
                mask.transformCount = paths.Count;
                for (int i = 0; i < paths.Count; i++)
                {
                    mask.SetTransformPath(i, paths[i].Path);
                    mask.SetTransformActive(i, paths[i].Active);
                }
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, true);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void BuildUpperBodyMaskPaths(Transform root, Transform current, List<(string Path, bool Active)> paths)
        {
            string path = AnimationUtility.CalculateTransformPath(current, root);
            paths.Add((path, IsUpperBodyMaskTransform(current.name)));

            for (int i = 0; i < current.childCount; i++)
            {
                BuildUpperBodyMaskPaths(root, current.GetChild(i), paths);
            }
        }

        private static bool IsUpperBodyMaskTransform(string transformName)
        {
            string normalizedName = NormalizeName(transformName);
            if (string.IsNullOrWhiteSpace(normalizedName)
                || normalizedName.Contains("root")
                || normalizedName.Contains("hips")
                || normalizedName.Contains("pelvis")
                || normalizedName.Contains("thigh")
                || normalizedName.Contains("calf")
                || normalizedName.Contains("leg")
                || normalizedName.Contains("foot")
                || normalizedName.Contains("toe"))
            {
                return false;
            }

            return normalizedName.Contains("spine")
                || normalizedName.Contains("chest")
                || normalizedName.Contains("neck")
                || normalizedName.Contains("head")
                || normalizedName.Contains("clavicle")
                || normalizedName.Contains("shoulder")
                || normalizedName.Contains("arm")
                || normalizedName.Contains("hand")
                || normalizedName.Contains("finger");
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state != null && state.name == stateName);
        }

        private static void EnsureAnyStateTriggerTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string triggerName,
            float duration)
        {
            AnimatorStateTransition transition = stateMachine.anyStateTransitions
                .FirstOrDefault(candidate => candidate.destinationState == destination && HasCondition(candidate, triggerName));
            if (transition == null)
            {
                transition = stateMachine.AddAnyStateTransition(destination);
                transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            }

            transition.hasExitTime = false;
            transition.duration = duration;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            EditorUtility.SetDirty(transition);
        }

        private static void EnsureExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.transitions
                .FirstOrDefault(candidate => candidate.destinationState == destination && candidate.hasExitTime);
            if (transition == null)
            {
                transition = source.AddTransition(destination);
            }

            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            EditorUtility.SetDirty(transition);
        }

        private static void RemoveExitTransition(AnimatorState source, AnimatorState destination)
        {
            AnimatorStateTransition transition = source.transitions
                .FirstOrDefault(candidate => candidate.destinationState == destination && candidate.hasExitTime);
            if (transition == null)
            {
                return;
            }

            source.RemoveTransition(transition);
            EditorUtility.SetDirty(source);
        }

        private static bool HasCondition(AnimatorStateTransition transition, string parameterName)
        {
            return transition.conditions.Any(condition => condition.parameter == parameterName);
        }

        private static AnimationClip ExtractBestAnimationClip(
            string sourcePath,
            IReadOnlyList<string> nameTokens,
            string outputPath,
            string outputName,
            bool loop,
            int fallbackIndex)
        {
            ConfigureAnimationImporter(sourcePath, loop);

            List<AnimationClip> sourceClips = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(IsUsableSourceClip)
                .Where(HasTransformCurveBindings)
                .ToList();
            if (sourceClips.Count == 0)
            {
                Debug.LogError($"No usable transform-bound animation clips were found in {sourcePath}.");
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            }

            AnimationClip sourceClip = sourceClips.FirstOrDefault(clip => MatchesAnyToken(clip.name, nameTokens));
            if (sourceClip == null)
            {
                if (sourceClips.Count > 1)
                {
                    Debug.LogError(
                        $"Could not find a CharacterTest clip matching [{string.Join(", ", nameTokens)}] in {sourcePath}. " +
                        $"Refusing to guess from multiple clips: {string.Join(", ", sourceClips.Select(clip => clip.name))}.");
                    return AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                }

                int clampedIndex = Mathf.Clamp(fallbackIndex, 0, sourceClips.Count - 1);
                sourceClip = sourceClips[clampedIndex];
            }

            AnimationClip outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (outputClip == null)
            {
                outputClip = new AnimationClip();
                AssetDatabase.CreateAsset(outputClip, outputPath);
            }

            EditorUtility.CopySerialized(sourceClip, outputClip);
            outputClip.name = outputName;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(outputClip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(outputClip, settings);
            EditorUtility.SetDirty(outputClip);
            return outputClip;
        }

        private static bool HasTransformCurveBindings(AnimationClip clip)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Any(binding => binding.type == typeof(Transform));
        }

        private static bool IsUsableSourceClip(AnimationClip clip)
        {
            if (clip == null
                || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)
                || clip.name.StartsWith("preview", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalizedName = NormalizeName(clip.name);
            return !normalizedName.Contains("tpose")
                && !normalizedName.Contains("bindpose")
                && !normalizedName.Contains("referencepose");
        }

        private static bool MatchesAnyToken(string candidateName, IReadOnlyList<string> nameTokens)
        {
            string normalizedName = NormalizeName(candidateName);
            foreach (string token in nameTokens)
            {
                if (normalizedName.Contains(NormalizeName(token)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
        }

        private static GameObject UpdatePlayerPrefab(MMOPlayerLocomotionAnimationSet animationSet, MMOPlayerCombatAnimationSet combatAnimationSet)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"Could not load player prefab at {PlayerPrefabPath}.");
                return null;
            }

            try
            {
                ApplyVisualSetup(prefabRoot, animationSet, combatAnimationSet);
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ApplyVisualSetup(GameObject root, MMOPlayerLocomotionAnimationSet animationSet, MMOPlayerCombatAnimationSet combatAnimationSet)
        {
            if (root == null || animationSet == null)
            {
                return;
            }

            RemoveLegacyVisualChildren(root.transform);

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab, root.transform) as GameObject;
            if (visual == null)
            {
                Debug.LogError($"Could not instantiate player model at {PlayerModelPath}.");
                return;
            }

            visual.name = "Character Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            FitVisualToGroundedHeight(visual, TargetHeight);

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = animationSet.BaseController;
            animator.applyRootMotion = animationSet.ApplyRootMotion;

            MMOPlayerMotor motor = root.GetComponent<MMOPlayerMotor>();
            MMOPlayerLocomotionAnimator playerAnimator = root.GetComponent<MMOPlayerLocomotionAnimator>();
            if (playerAnimator == null)
            {
                playerAnimator = root.AddComponent<MMOPlayerLocomotionAnimator>();
            }

            playerAnimator.Configure(animationSet, animator, visual.transform, motor);
            playerAnimator.ConfigureStrafePresentation(
                true,
                StrafeVisualYawSharpness,
                MaxStrafeVisualYawDegrees,
                true,
                UpperBodyCounterYawWeight,
                MaxUpperBodyCounterYawDegrees,
                BuildUpperBodyCounterYawBones(visual.transform));

            MMOPlayerCombatAnimator combatAnimator = root.GetComponent<MMOPlayerCombatAnimator>();
            if (combatAnimator == null)
            {
                combatAnimator = root.AddComponent<MMOPlayerCombatAnimator>();
            }

            combatAnimator.Configure(
                combatAnimationSet,
                animator,
                motor,
                root.GetComponent<RPGClone.Combat.MMOCombatant>(),
                root.GetComponent<RPGClone.Abilities.MMOAbilitySystem>(),
                root.GetComponent<RPGClone.Combat.MMOAutoAttackController>());

            MMOPlayerEquipmentVisuals equipmentVisuals = root.GetComponent<MMOPlayerEquipmentVisuals>();
            if (equipmentVisuals == null)
            {
                equipmentVisuals = root.AddComponent<MMOPlayerEquipmentVisuals>();
            }

            equipmentVisuals.Configure(root.GetComponent<MMOCharacterEquipment>(), BuildBodyPartSlots(visual));
        }

        private static List<MMOUpperBodyCounterYawBone> BuildUpperBodyCounterYawBones(Transform visualRoot)
        {
            List<MMOUpperBodyCounterYawBone> bones = new();
            AddCounterYawBone(bones, visualRoot, "spine_02.x", 0.8f, 24f);

            if (bones.Count == 0)
            {
                Debug.LogWarning(
                    "No ARP deform bones were found for upper-body counter-yaw. " +
                    "Strafing will still rotate the lower-body visual, but the torso will not counter-face until a deform spine bone is bound.");
            }

            return bones;
        }

        private static void AddCounterYawBone(
            List<MMOUpperBodyCounterYawBone> bones,
            Transform visualRoot,
            string boneName,
            float weight,
            float maxYawDegrees)
        {
            Transform bone = FindDeepChildByName(visualRoot, boneName);
            if (bone == null)
            {
                return;
            }

            bones.Add(new MMOUpperBodyCounterYawBone(boneName, bone, weight, maxYawDegrees));
        }

        private static void RemoveLegacyVisualChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == "Capsule Visual" || child.name == "Character Visual")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static List<MMOBodyPartRendererSlot> BuildBodyPartSlots(GameObject visual)
        {
            Dictionary<MMOCharacterBodyPart, List<Renderer>> renderersByPart = new();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                MMOCharacterBodyPart bodyPart = ResolveBodyPart(renderer);
                if (!renderersByPart.TryGetValue(bodyPart, out List<Renderer> renderers))
                {
                    renderers = new List<Renderer>();
                    renderersByPart[bodyPart] = renderers;
                }

                renderers.Add(renderer);
            }

            List<MMOBodyPartRendererSlot> slots = new();
            foreach (KeyValuePair<MMOCharacterBodyPart, List<Renderer>> pair in renderersByPart)
            {
                Renderer firstRenderer = pair.Value.FirstOrDefault();
                Transform anchor = firstRenderer != null ? firstRenderer.transform : visual.transform;
                slots.Add(new MMOBodyPartRendererSlot(pair.Key, anchor, pair.Value.ToArray()));
            }

            return slots;
        }

        private static MMOCharacterBodyPart ResolveBodyPart(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Texture texture = material != null ? material.mainTexture : null;
                if (TryResolveBodyPartName(texture != null ? texture.name : null, out MMOCharacterBodyPart bodyPart))
                {
                    return bodyPart;
                }

                if (TryResolveBodyPartName(material != null ? material.name : null, out bodyPart))
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
            string normalizedName = NormalizeName(candidate);
            if (normalizedName.Contains("head"))
            {
                bodyPart = MMOCharacterBodyPart.Head;
                return true;
            }

            if (normalizedName.Contains("hand"))
            {
                bodyPart = MMOCharacterBodyPart.Hands;
                return true;
            }

            if (normalizedName.Contains("torso") || normalizedName.Contains("chest"))
            {
                bodyPart = MMOCharacterBodyPart.Torso;
                return true;
            }

            if (normalizedName.Contains("leg"))
            {
                bodyPart = MMOCharacterBodyPart.Legs;
                return true;
            }

            if (normalizedName.Contains("feet") || normalizedName.Contains("foot") || normalizedName.Contains("boot"))
            {
                bodyPart = MMOCharacterBodyPart.Feet;
                return true;
            }

            bodyPart = MMOCharacterBodyPart.Torso;
            return false;
        }

        private static void FitVisualToGroundedHeight(GameObject visual, float targetHeight)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            visual.transform.localScale = Vector3.one;
            Bounds bounds = CalculateRendererBounds(renderers);
            if (bounds.size.y > 0.001f)
            {
                float scale = targetHeight / bounds.size.y;
                visual.transform.localScale = Vector3.one * scale;
            }

            bounds = CalculateRendererBounds(renderers);
            float desiredGroundY = visual.transform.parent != null ? visual.transform.parent.position.y : 0f;
            visual.transform.position += new Vector3(0f, desiredGroundY - bounds.min.y, 0f);
        }

        private static Bounds CalculateRendererBounds(IReadOnlyList<Renderer> renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void ConfigurePlayerModelImporters()
        {
            foreach (string sourcePath in Directory.GetFiles(PlayerModelFolder, "*.fbx").Select(path => path.Replace('\\', '/')))
            {
                ConfigurePlayerModelImporter(sourcePath);
            }
        }

        private static void ConfigurePlayerModelImporter(string sourcePath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not find a ModelImporter for {sourcePath}.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        private static void ConfigureAnimationImporter(string sourcePath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Transform FindDeepChildByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDeepChildByName(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
