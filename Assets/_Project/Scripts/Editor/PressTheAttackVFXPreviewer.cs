#if UNITY_EDITOR
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Vfx;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class PressTheAttackVFXPreviewer
    {
        private const string PreviewName = "PressTheAttack V2 Visual QA";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Press_The_Attack.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/PressTheAttack/PressTheAttack_VFX.asset";
        private const string PrefabPath = "Assets/_Project/VFX/PressTheAttack/Prefabs/PressTheAttackVFX.prefab";
        private const string ModularPlayerModelPath = "Assets/Player/Models/Idle.fbx";

        [MenuItem("Tools/RPG Clone/VFX/Press the Attack/Preview Modular Skinned Character (Play Mode)")]
        public static void PreviewChargedSurface()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Press the Attack visual QA preview requires Play Mode.");
                return;
            }

            ClearPreview();
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModularPlayerModelPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"Press the Attack modular player model is missing at {ModularPlayerModelPath}.");
                return;
            }

            GameObject caster = new(PreviewName);
            caster.name = PreviewName;
            caster.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject modularVisual = Object.Instantiate(modelPrefab, caster.transform);
            modularVisual.name = "Actual Modular Player Visual";
            modularVisual.transform.SetLocalPositionAndRotation(new Vector3(0f, -0.13f, 0f), Quaternion.identity);
            modularVisual.transform.localScale = Vector3.one;
            MMOCharacterIdentity identity = caster.AddComponent<MMOCharacterIdentity>();
            MMOAbilitySystem abilitySystem = caster.AddComponent<MMOAbilitySystem>();
            MMOCharacterBuffController buffs = caster.AddComponent<MMOCharacterBuffController>();
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (ability == null || definition == null || prefab == null)
            {
                Object.Destroy(caster);
                Debug.LogError("Press the Attack visual QA assets are incomplete.");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = PreviewName + " Effect";
            PressTheAttackVFX vfx = instance.GetComponent<PressTheAttackVFX>();
            vfx.Initialize(new MMOAbilityVfxContext(
                abilitySystem,
                ability,
                definition,
                caster.transform,
                caster.transform,
                caster.transform.position,
                caster.transform.position,
                false,
                null));
            buffs.ApplyBuff(new MMOBuffApplication
            {
                BuffId = ability.AbilityId,
                DisplayName = ability.DisplayName + " Visual QA",
                DurationSeconds = 60f,
                Ability = ability
            });

            Selection.activeGameObject = caster;
            SceneView.lastActiveSceneView?.FrameSelected();
            int skinnedPartCount = caster.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            Debug.Log($"Press the Attack modular visual QA preview started with {skinnedPartCount} skinned body parts. This diagnostic preview is not a multiplayer validation path.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Press the Attack/Clear Visual QA Preview")]
        public static void ClearPreview()
        {
            DestroyNamedObject(PreviewName + " Effect");
            DestroyNamedObject(PreviewName);
        }

        private static void DestroyNamedObject(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.Destroy(existing);
            }
        }
    }
}
#endif
