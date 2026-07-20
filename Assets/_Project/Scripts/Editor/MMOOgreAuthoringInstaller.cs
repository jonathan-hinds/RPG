using System;
using System.Collections.Generic;
using System.Linq;
using RPGClone.Abilities;
using RPGClone.Animation;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOOgreAuthoringInstaller
    {
        private const string CharacterFolder = "Assets/Characters/Ogre";
        private const string ModelPath = CharacterFolder + "/Models/Ogre.fbx";
        private const string TexturePath = CharacterFolder + "/textures/Ogre_BaseColor.jpg";
        private const string ClipFolder = CharacterFolder + "/Animations/Clips";
        private const string AnimationSetPath = ClipFolder + "/Ogre_AnimationSet.asset";
        private const string VisualDefinitionPath = CharacterFolder + "/Ogre_Visual.asset";
        private const string PrefabPath = CharacterFolder + "/Prefabs/OgreEnemy.prefab";
        private const string ProfilePath = "Assets/_Project/Configs/Characters/Ogre.asset";
        private const string LootPath = "Assets/_Project/Configs/Loot/Ogre_Trash_Loot.asset";
        private const string EnemyDefinitionPath = "Assets/_Project/Configs/Enemies/Ogre_Aggressive.asset";
        private const string BaseControllerPath = MMOLayeredAnimationInstaller.OgreControllerPath;
        private const string AutoAttackPath = "Assets/_Project/Configs/Abilities/Auto_Attack.asset";
        private const string SpawnParentPath = "Starter World/Placeholder Creature Spawns";
        private const string SceneInstanceName = "Ogre 01";

        private static readonly AnimationSource[] AnimationSources =
        {
            new(ModelPath, "Ogre_Idle", true),
            new(CharacterFolder + "/Animations/Source/Ogre_Walk.fbx", "Ogre_Walk", true),
            new(CharacterFolder + "/Animations/Source/Ogre_Run.fbx", "Ogre_Run", true),
            new(CharacterFolder + "/Animations/Source/Ogre_Attack1.fbx", "Ogre_Attack1", false),
            new(CharacterFolder + "/Animations/Source/Ogre_Attack2.fbx", "Ogre_Attack2", false),
            new(CharacterFolder + "/Animations/Source/Ogre_Damage.fbx", "Ogre_Damage", false),
            new(CharacterFolder + "/Animations/Source/Ogre_Death.fbx", "Ogre_Death", false)
        };

        [MenuItem("Tools/RPG Clone/Creatures/Install Ogre Creature")]
        public static void InstallOgreCreature()
        {
            EnsureRequiredSourcesExist();
            ConfigureTextureImporter();

            Dictionary<string, AnimationClip> clips = new();
            foreach (AnimationSource source in AnimationSources)
            {
                ConfigureAnimationImporter(source);
                clips[source.OutputName] = ExtractAnimationClip(source);
            }

            ValidateAnimationBindings(clips.Values);
            MMOEnemyDefinition enemyDefinition = CreateOrUpdateEnemyDefinition();
            MMOCreatureAnimationSet animationSet = CreateOrUpdateAnimationSet(clips);
            MMOCreatureVisualDefinition visualDefinition = CreateOrUpdateVisualDefinition(animationSet, enemyDefinition);
            GameObject prefab = MMOCreatureVisualAuthoringInstaller.BuildCreaturePrefab(visualDefinition);
            if (prefab == null)
            {
                throw new InvalidOperationException("The Ogre prefab could not be generated.");
            }

            PlaceOrUpdateSceneInstance(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Installed the level 5 Ogre creature, rebuilt its prefab, and placed Ogre 01 in the active scene.");
        }

        private static void EnsureRequiredSourcesExist()
        {
            foreach (AnimationSource source in AnimationSources)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(source.SourcePath) == null)
                {
                    throw new InvalidOperationException($"Missing Ogre FBX: {source.SourcePath}");
                }
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                throw new InvalidOperationException($"The canonical Ogre model has no renderers: {ModelPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
            {
                throw new InvalidOperationException($"Missing Ogre base-color texture: {TexturePath}");
            }
        }

        private static void ConfigureAnimationImporter(AnimationSource source)
        {
            ModelImporter importer = AssetImporter.GetAtPath(source.SourcePath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Could not access the model importer for {source.SourcePath}");
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] importedClips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in importedClips)
            {
                clip.loopTime = source.Loop;
                clip.loopPose = source.Loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = importedClips;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter()
        {
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static AnimationClip ExtractAnimationClip(AnimationSource source)
        {
            AnimationClip sourceClip = AssetDatabase.LoadAllAssetsAtPath(source.SourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(IsUsableSourceClip);
            if (sourceClip == null)
            {
                throw new InvalidOperationException($"No usable animation clip was found in {source.SourcePath}");
            }

            string outputPath = $"{ClipFolder}/{source.OutputName}.anim";
            AnimationClip outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (outputClip == null)
            {
                outputClip = new AnimationClip();
                AssetDatabase.CreateAsset(outputClip, outputPath);
            }

            EditorUtility.CopySerialized(sourceClip, outputClip);
            outputClip.name = source.OutputName;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(outputClip);
            settings.loopTime = source.Loop;
            settings.loopBlend = source.Loop;
            AnimationUtility.SetAnimationClipSettings(outputClip, settings);
            EditorUtility.SetDirty(outputClip);
            return outputClip;
        }

        private static bool IsUsableSourceClip(AnimationClip clip)
        {
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = clip.name.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.IndexOf("tpose", StringComparison.OrdinalIgnoreCase) < 0
                && normalized.IndexOf("bindpose", StringComparison.OrdinalIgnoreCase) < 0
                && normalized.IndexOf("referencepose", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void ValidateAnimationBindings(IEnumerable<AnimationClip> clips)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            HashSet<string> modelPaths = model.GetComponentsInChildren<Transform>(true)
                .Select(transform => AnimationUtility.CalculateTransformPath(transform, model.transform))
                .ToHashSet(StringComparer.Ordinal);

            foreach (AnimationClip clip in clips)
            {
                if (clip == null || clip.length <= 0.01f)
                {
                    throw new InvalidOperationException("An Ogre animation clip is missing or empty.");
                }

                string[] missingPaths = AnimationUtility.GetCurveBindings(clip)
                    .Select(binding => binding.path)
                    .Where(path => !modelPaths.Contains(path))
                    .Distinct()
                    .Take(8)
                    .ToArray();
                if (missingPaths.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"{clip.name} targets ARP rig paths that are absent from the Ogre model: {string.Join(", ", missingPaths)}");
                }
            }
        }

        private static MMOEnemyDefinition CreateOrUpdateEnemyDefinition()
        {
            MMOCharacterProfile profile = LoadOrCreate<MMOCharacterProfile>(ProfilePath);
            MMOCharacterStats stats = new();
            stats.Configure(18, 22, 6, 3, 7, 22, 24, 0, 8f, 13f, 2.8f, 3.2f);
            profile.Configure("Ogre", 5, 200, 0, new Color(0.48f, 0.68f, 0.34f), null, true, MMOEntityFaction.Hostile, stats);
            EditorUtility.SetDirty(profile);

            MMOLootTable lootTable = LoadOrCreate<MMOLootTable>(LootPath);
            List<MMOLootTableEntry> entries = new();
            AddLoot(entries, "Assets/_Project/Configs/Items/Cracked_Tusk.asset", 0.55f, 1, 2);
            AddLoot(entries, "Assets/_Project/Configs/Items/Greasy_Snout.asset", 0.20f, 1, 1);
            lootTable.Configure(entries);
            EditorUtility.SetDirty(lootTable);

            MMOAbilityDefinition autoAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AutoAttackPath);
            MMOEnemyDefinition definition = LoadOrCreate<MMOEnemyDefinition>(EnemyDefinitionPath);
            definition.Configure(
                profile,
                MMOEnemyDisposition.Aggressive,
                autoAttack,
                autoAttack != null ? new[] { autoAttack } : Array.Empty<MMOAbilityDefinition>(),
                14f,
                38f,
                0.25f,
                true,
                5f,
                3f,
                6f,
                1.25f,
                MMOClassicEnemyPursuitDefaults.StandardChaseSpeed,
                2.8f,
                90,
                lootTable,
                2.5f,
                6f,
                120f,
                35f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static MMOCreatureAnimationSet CreateOrUpdateAnimationSet(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            MMOCreatureAnimationSet animationSet = LoadOrCreate<MMOCreatureAnimationSet>(AnimationSetPath);
            animationSet.Configure(
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath),
                clips["Ogre_Idle"],
                clips["Ogre_Walk"],
                clips["Ogre_Run"],
                clips["Ogre_Attack1"],
                clips["Ogre_Attack2"],
                clips["Ogre_Damage"],
                clips["Ogre_Death"],
                1.25f,
                3.8f,
                0.95f,
                0.5f,
                0.14f,
                false,
                0f);
            EditorUtility.SetDirty(animationSet);
            return animationSet;
        }

        private static MMOCreatureVisualDefinition CreateOrUpdateVisualDefinition(
            MMOCreatureAnimationSet animationSet,
            MMOEnemyDefinition enemyDefinition)
        {
            MMOCreatureVisualDefinition visual = LoadOrCreate<MMOCreatureVisualDefinition>(VisualDefinitionPath);
            visual.Configure(
                "Ogre",
                "Ogre",
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath),
                null,
                animationSet,
                enemyDefinition,
                new[] { enemyDefinition },
                new[] { "Ogre" },
                MMOCreatureBodyType.Biped,
                3.4f,
                0.8f,
                3.4f,
                new Vector3(0f, 1.7f, 0f),
                Vector3.zero,
                Vector3.zero,
                0f,
                0.28f,
                0f);
            EditorUtility.SetDirty(visual);
            return visual;
        }

        private static void PlaceOrUpdateSceneInstance(GameObject prefab)
        {
            if (!EditorSceneManager.GetActiveScene().isLoaded)
            {
                throw new InvalidOperationException("No active scene is loaded for Ogre placement.");
            }

            GameObject parent = GameObject.Find(SpawnParentPath);
            if (parent == null)
            {
                throw new InvalidOperationException($"Could not find the creature spawn parent '{SpawnParentPath}'.");
            }

            Transform existing = parent.transform.Find(SceneInstanceName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.transform) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate OgreEnemy.prefab in the active scene.");
            }

            instance.name = SceneInstanceName;
            instance.transform.SetPositionAndRotation(new Vector3(-69f, 2f, -185f), Quaternion.Euler(0f, 90f, 0f));
            if (!MMOGroundingUtility.SnapTransformToGround(instance.transform, instance.GetComponent<Collider>()))
            {
                Object.DestroyImmediate(instance);
                throw new InvalidOperationException("Could not find valid ground for Ogre 01 at the intended test location.");
            }

            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = instance;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void AddLoot(List<MMOLootTableEntry> entries, string itemPath, float chance, int minimum, int maximum)
        {
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(itemPath);
            if (item != null)
            {
                entries.Add(new MMOLootTableEntry(item, chance, minimum, maximum));
            }
        }

        private readonly struct AnimationSource
        {
            public AnimationSource(string sourcePath, string outputName, bool loop)
            {
                SourcePath = sourcePath;
                OutputName = outputName;
                Loop = loop;
            }

            public string SourcePath { get; }
            public string OutputName { get; }
            public bool Loop { get; }
        }
    }
}
