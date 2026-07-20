using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Animation;
using RPGClone.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOLayeredAnimationInstaller
    {
        public const string TemplateControllerPath = "Assets/_Project/Animations/Creatures/MMOCreatureBase.controller";
        public const string PlayerControllerPath = "Assets/Player/Animations/Controllers/CharacterTest_Layered.controller";
        public const string AshCanyonControllerPath = "Assets/Characters/AshCanyonCreature/Animations/Controllers/AshCanyonCreature_Layered.controller";
        public const string AshGeneralControllerPath = "Assets/Characters/AshLeader/Animations/Controllers/AshGeneral_Layered.controller";
        public const string BristlebackControllerPath = "Assets/Characters/Bristleback/Animations/Controllers/Bristleback_Layered.controller";
        public const string TrogControllerPath = "Assets/Characters/Trog/Animations/Controllers/Trog_Layered.controller";
        public const string OgreControllerPath = "Assets/Characters/Ogre/Animations/Controllers/Ogre_Layered.controller";
        public const string WolfControllerPath = "Assets/Characters/Wolf/Animations/Controllers/Wolf_Layered.controller";

        private const string EmptyClipPath = "Assets/_Project/Animations/Creatures/MMO_UpperBodyEmpty.anim";

        private static readonly string[] UpperBodyActionStateNames =
        {
            "Attack1",
            "Attack2",
            "Damage",
            "Attack1H",
            "Attack2H",
            "AttackUnarmed",
            "CombatDamage",
            "Casting",
            "Cast"
        };

        private static readonly RigDefinition[] RigDefinitions =
        {
            new RigDefinition(
                "Player",
                "Assets/Player/Models/Idle.fbx",
                "Assets/Player/Animations/Clips/CharacterTest_UpperBody.mask",
                PlayerControllerPath,
                new[] { "root/root.x/spine_01.x" },
                new[]
                {
                    "Assets/Player/Animations/Clips/CharacterTest_PlayerLocomotion.asset",
                    "Assets/Player/Animations/Clips/CharacterTest_PlayerCombat.asset"
                }),
            new RigDefinition(
                "Ash Canyon Creature",
                "Assets/Characters/AshCanyonCreature/Models/AshCanyonCreature.fbx",
                "Assets/Characters/AshCanyonCreature/Animations/Controllers/AshCanyonCreature_UpperBody.mask",
                AshCanyonControllerPath,
                new[] { "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01" },
                new[] { "Assets/Characters/AshCanyonCreature/Animations/Clips/AshCanyonCreature_AnimationSet.asset" }),
            new RigDefinition(
                "Ash General",
                "Assets/Characters/AshLeader/Models/AshGeneral.fbx",
                "Assets/Characters/AshLeader/Animations/Controllers/AshGeneral_UpperBody.mask",
                AshGeneralControllerPath,
                new[] { "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01" },
                new[] { "Assets/Characters/AshLeader/Animations/Clips/AshGeneral_AnimationSet.asset" }),
            new RigDefinition(
                "Bristleback",
                "Assets/Characters/Bristleback/Models/Bristleback.fbx",
                "Assets/Characters/Bristleback/Animations/Controllers/Bristleback_UpperBody.mask",
                BristlebackControllerPath,
                new[] { "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01" },
                new[] { "Assets/Characters/Shared/CreatureCombatAnimations/Clips/StandardCreature_AnimationSet.asset" }),
            new RigDefinition(
                "Trog",
                "Assets/Characters/Trog/Models/Trog.fbx",
                "Assets/Characters/Trog/Animations/Controllers/Trog_UpperBody.mask",
                TrogControllerPath,
                new[] { "root/root.x/spine_01.x" },
                new[] { "Assets/Characters/Trog/Animations/Clips/Trog_AnimationSet.asset" }),
            new RigDefinition(
                "Ogre",
                "Assets/Characters/Ogre/Models/Ogre.fbx",
                "Assets/Characters/Ogre/Animations/Controllers/Ogre_UpperBody.mask",
                OgreControllerPath,
                new[] { "root/root.x/spine_01.x" },
                new[] { "Assets/Characters/Ogre/Animations/Clips/Ogre_AnimationSet.asset" }),
            new RigDefinition(
                "Wolf",
                "Assets/Characters/Wolf/Models/wolf2.fbx",
                "Assets/Characters/Wolf/Animations/Controllers/Wolf_UpperBody.mask",
                WolfControllerPath,
                new[]
                {
                    "rig/c_pos/c_traj/c_root_master.x/c_spine_01.x",
                    "rig/c_pos/c_traj/head_scale_fix.x"
                },
                new[] { "Assets/Characters/Wolf/Animations/Clips/Wolf_AnimationSet.asset" })
        };

        [MenuItem("Tools/RPG Clone/Animation/Install Layered Action Animations")]
        public static void InstallLayeredActionAnimations()
        {
            AnimatorController template = AssetDatabase.LoadAssetAtPath<AnimatorController>(TemplateControllerPath);
            if (template == null)
            {
                throw new InvalidOperationException($"Layered animation template is missing at {TemplateControllerPath}.");
            }

            ConfigureUpperBodyLayer(template, template.layers.FirstOrDefault(layer => layer.name == MMOLayeredActionPlayer.UpperBodyLayerName)?.avatarMask);
            AssetDatabase.SaveAssets();

            foreach (RigDefinition rig in RigDefinitions)
            {
                AvatarMask mask = CreateOrUpdateMask(rig);
                AnimatorController controller = CreateOrUpdateController(rig, mask);
                AssignControllerToAnimationSets(rig, controller);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Installed layered upper-body action animation for {RigDefinitions.Length} player/creature rigs.");
        }

        private static AvatarMask CreateOrUpdateMask(RigDefinition rig)
        {
            EnsureParentFolder(rig.MaskPath);
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(rig.MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = Path.GetFileNameWithoutExtension(rig.MaskPath) };
                AssetDatabase.CreateAsset(mask, rig.MaskPath);
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(rig.ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"{rig.DisplayName} model is missing at {rig.ModelPath}.");
            }

            mask.transformCount = 0;
            mask.AddTransformPath(model.transform, true);
            HashSet<string> discoveredRoots = new HashSet<string>();
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                bool active = false;
                foreach (string upperBodyRootPath in rig.UpperBodyRootPaths)
                {
                    if (path == upperBodyRootPath || path.StartsWith(upperBodyRootPath + "/", StringComparison.Ordinal))
                    {
                        active = true;
                        discoveredRoots.Add(upperBodyRootPath);
                        break;
                    }
                }

                mask.SetTransformActive(i, active);
            }

            if (discoveredRoots.Count != rig.UpperBodyRootPaths.Length)
            {
                string missingRoots = string.Join(", ", rig.UpperBodyRootPaths.Where(root => !discoveredRoots.Contains(root)));
                throw new InvalidOperationException($"{rig.DisplayName} upper-body roots were not found: {missingRoots}.");
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorController CreateOrUpdateController(RigDefinition rig, AvatarMask mask)
        {
            EnsureParentFolder(rig.ControllerPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(rig.ControllerPath);
            if (controller == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateControllerPath, rig.ControllerPath))
                {
                    throw new InvalidOperationException($"Could not create {rig.DisplayName} controller at {rig.ControllerPath}.");
                }

                AssetDatabase.ImportAsset(rig.ControllerPath, ImportAssetOptions.ForceSynchronousImport);
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(rig.ControllerPath);
            }

            if (controller == null)
            {
                throw new InvalidOperationException($"Could not load {rig.DisplayName} controller at {rig.ControllerPath}.");
            }

            ConfigureUpperBodyLayer(controller, mask);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureUpperBodyLayer(AnimatorController controller, AvatarMask mask)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(layers, layer => layer.name == MMOLayeredActionPlayer.UpperBodyLayerName);
            if (layerIndex < 0)
            {
                controller.AddLayer(MMOLayeredActionPlayer.UpperBodyLayerName);
                layers = controller.layers;
                layerIndex = Array.FindIndex(layers, layer => layer.name == MMOLayeredActionPlayer.UpperBodyLayerName);
            }

            AnimatorControllerLayer layer = layers[layerIndex];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = false;

            AnimatorStateMachine upperBodyStateMachine = layer.stateMachine;
            foreach (ChildAnimatorState childState in upperBodyStateMachine.states.ToArray())
            {
                upperBodyStateMachine.RemoveState(childState.state);
            }

            AnimationClip emptyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EmptyClipPath);
            if (emptyClip == null)
            {
                throw new InvalidOperationException($"Upper-body empty clip is missing at {EmptyClipPath}.");
            }

            AnimatorState emptyState = upperBodyStateMachine.AddState("Empty", new Vector3(240f, 40f, 0f));
            emptyState.motion = emptyClip;
            emptyState.writeDefaultValues = false;
            upperBodyStateMachine.defaultState = emptyState;

            AnimatorStateMachine baseStateMachine = layers[0].stateMachine;
            for (int i = 0; i < UpperBodyActionStateNames.Length; i++)
            {
                AnimatorState source = FindState(baseStateMachine, UpperBodyActionStateNames[i]);
                if (source == null || source.motion == null)
                {
                    throw new InvalidOperationException(
                        $"Template controller is missing required base action state '{UpperBodyActionStateNames[i]}'.");
                }

                AnimatorState action = upperBodyStateMachine.AddState(
                    source.name,
                    new Vector3(520f + (i / 5) * 270f, -40f - (i % 5) * 110f, 0f));
                action.motion = source.motion;
                action.tag = source.tag;
                action.speed = source.speed;
                action.speedParameter = source.speedParameter;
                action.speedParameterActive = source.speedParameterActive;
                action.writeDefaultValues = false;
                EditorUtility.SetDirty(action);
            }

            layers[layerIndex] = layer;
            controller.layers = layers;
            EditorUtility.SetDirty(upperBodyStateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state != null && state.name == stateName);
        }

        private static void AssignControllerToAnimationSets(RigDefinition rig, RuntimeAnimatorController controller)
        {
            foreach (string animationSetPath in rig.AnimationSetPaths)
            {
                MMOPlayerLocomotionAnimationSet locomotionSet =
                    AssetDatabase.LoadAssetAtPath<MMOPlayerLocomotionAnimationSet>(animationSetPath);
                if (locomotionSet != null)
                {
                    locomotionSet.ConfigureBaseController(controller);
                    EditorUtility.SetDirty(locomotionSet);
                    continue;
                }

                MMOPlayerCombatAnimationSet combatSet =
                    AssetDatabase.LoadAssetAtPath<MMOPlayerCombatAnimationSet>(animationSetPath);
                if (combatSet != null)
                {
                    combatSet.ConfigureBaseController(controller);
                    EditorUtility.SetDirty(combatSet);
                    continue;
                }

                MMOCreatureAnimationSet creatureSet =
                    AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(animationSetPath);
                if (creatureSet != null)
                {
                    creatureSet.ConfigureBaseController(controller);
                    EditorUtility.SetDirty(creatureSet);
                    continue;
                }

                throw new InvalidOperationException($"Animation set is missing at {animationSetPath}.");
            }
        }

        private static void EnsureParentFolder(string assetPath)
        {
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolder(parent);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        private sealed class RigDefinition
        {
            public RigDefinition(
                string displayName,
                string modelPath,
                string maskPath,
                string controllerPath,
                string[] upperBodyRootPaths,
                string[] animationSetPaths)
            {
                DisplayName = displayName;
                ModelPath = modelPath;
                MaskPath = maskPath;
                ControllerPath = controllerPath;
                UpperBodyRootPaths = upperBodyRootPaths;
                AnimationSetPaths = animationSetPaths;
            }

            public string DisplayName { get; }
            public string ModelPath { get; }
            public string MaskPath { get; }
            public string ControllerPath { get; }
            public string[] UpperBodyRootPaths { get; }
            public string[] AnimationSetPaths { get; }
        }
    }
}
