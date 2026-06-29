using System.Linq;
using RPGClone.Player;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOCombatIdleRepairUtility
    {
        private const string SourceFbxPath = "Assets/Player/Models/CombatIdle.fbx";
        private const string TargetClipPath = "Assets/Player/Animations/Clips/CharacterTest_CombatIdle.anim";
        private const string CombatAnimationSetPath = "Assets/Player/Animations/Clips/CharacterTest_PlayerCombat.asset";

        [MenuItem("RPG Clone/Animation/Repair Player Combat Idle")]
        public static void RepairPlayerCombatIdle()
        {
            ConfigureSourceImporter();

            AnimationClip sourceClip = AssetDatabase.LoadAllAssetsAtPath(SourceFbxPath)
                .OfType<AnimationClip>()
                .Where(IsUsableClip)
                .OrderByDescending(clip => NormalizeName(clip.name).Contains("combatidle"))
                .FirstOrDefault();
            if (sourceClip == null)
            {
                Debug.LogError($"No usable animation clip found in {SourceFbxPath}.");
                return;
            }

            if (!HasTransformCurveBindings(sourceClip))
            {
                Debug.LogError(
                    $"{SourceFbxPath} produced '{sourceClip.name}', but it has no Transform curves. " +
                    "Player combat idle must be imported as a Generic player-skeleton clip, like normal idle.");
                return;
            }

            AnimationClip targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetClipPath);
            if (targetClip == null)
            {
                targetClip = new AnimationClip();
                AssetDatabase.CreateAsset(targetClip, TargetClipPath);
            }

            EditorUtility.CopySerialized(sourceClip, targetClip);
            targetClip.name = "CharacterTest_CombatIdle";
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(targetClip);
            settings.loopTime = true;
            settings.loopBlend = true;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(targetClip, settings);
            EditorUtility.SetDirty(targetClip);

            MMOPlayerCombatAnimationSet combatSet = AssetDatabase.LoadAssetAtPath<MMOPlayerCombatAnimationSet>(CombatAnimationSetPath);
            if (combatSet != null)
            {
                SerializedObject serializedCombatSet = new(combatSet);
                serializedCombatSet.FindProperty("combatIdle").objectReferenceValue = targetClip;
                serializedCombatSet.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(combatSet);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Repaired player combat idle from {SourceFbxPath} into {TargetClipPath}.");
        }

        private static void ConfigureSourceImporter()
        {
            if (AssetImporter.GetAtPath(SourceFbxPath) is not ModelImporter importer)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationWrapMode = WrapMode.Loop;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(SourceFbxPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length > 0)
            {
                clips[0].name = "CombatIdle";
                clips[0].loopTime = true;
                clips[0].loopPose = true;
                clips[0].lockRootRotation = true;
                clips[0].lockRootHeightY = true;
                clips[0].lockRootPositionXZ = true;
                clips[0].keepOriginalOrientation = true;
                clips[0].keepOriginalPositionY = true;
                clips[0].keepOriginalPositionXZ = true;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static bool IsUsableClip(AnimationClip clip)
        {
            return clip != null
                && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)
                && !clip.name.Contains("Default Take", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasTransformCurveBindings(AnimationClip clip)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Any(binding => binding.type == typeof(Transform));
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
    }
}
