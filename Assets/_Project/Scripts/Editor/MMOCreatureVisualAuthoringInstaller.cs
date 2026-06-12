using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Abilities;
using RPGClone.Animation;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOCreatureVisualAuthoringInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string AutoAttackPath = RootFolder + "/Configs/Abilities/Auto_Attack.asset";
        private const string BaseControllerPath = RootFolder + "/Animations/Creatures/MMOCreatureBase.controller";
        private const string SharedAnimationSetPath = "Assets/Characters/Shared/CreatureCombatAnimations/Clips/StandardCreature_AnimationSet.asset";
        private const string AshCanyonAnimationSourceFolder = "Assets/Characters/AshCanyonCreature/Animations/Source";
        private const string AshCanyonAnimationClipFolder = "Assets/Characters/AshCanyonCreature/Animations/Clips";
        private const string AshCanyonAnimationSetPath = AshCanyonAnimationClipFolder + "/AshCanyonCreature_AnimationSet.asset";
        private const string WolfModelPath = "Assets/Characters/Wolf/Models/wolf2.fbx";
        private const string WolfAnimationClipFolder = "Assets/Characters/Wolf/Animations/Clips";
        private const string WolfAnimationSetPath = WolfAnimationClipFolder + "/Wolf_AnimationSet.asset";
        private const string WolfEnemyDefinitionPath = RootFolder + "/Configs/Enemies/Wolf_Aggressive.asset";
        private const string WolfProfilePath = RootFolder + "/Configs/Characters/Wolf.asset";
        private const string WolfLootPath = RootFolder + "/Configs/Loot/Wolf_Trash_Loot.asset";

        [MenuItem("Tools/RPG Clone/Creatures/Create Standard Creature Visual Definitions")]
        public static void CreateStandardCreatureVisualDefinitions()
        {
            CreateOrUpdateAshCanyonAnimationSet();
            CreateOrUpdateWolfAnimationSet();
            CreateOrUpdateWolfEnemyDefinition();

            CreateOrUpdateVisualDefinition(
                "Assets/Characters/Bristleback/Bristleback_Visual.asset",
                "Bristleback",
                "Bristleback",
                "Assets/Characters/Bristleback/Models/Bristleback.fbx",
                "Assets/Characters/Bristleback/Models/Bristleback.fbm/Material_1_Pbr_Diffuse.png",
                null,
                SharedAnimationSetPath,
                "Assets/_Project/Configs/Enemies/Bristleback_Aggressive.asset",
                new[]
                {
                    "Assets/_Project/Configs/Enemies/Bristleback_Aggressive.asset",
                    "Assets/_Project/Configs/Enemies/Bristleback_Docile.asset"
                },
                new[] { "Bristleback Creature" },
                MMOCreatureBodyType.Biped,
                2.25f,
                0.6f,
                2.25f,
                new Vector3(0f, 1.125f, 0f),
                Vector3.zero,
                Vector3.zero,
                0f,
                0.35f,
                0f);

            CreateOrUpdateVisualDefinition(
                "Assets/Characters/AshCanyonCreature/AshCanyonCreature_Visual.asset",
                "AshCanyonCreature",
                "Ash Canyon Creature",
                "Assets/Characters/AshCanyonCreature/Models/AshCanyonCreature.fbx",
                "Assets/Characters/AshCanyonCreature/Models/AshCanyonCreature.fbm/Material_001_Pbr_Diffuse.jpg",
                "Assets/Characters/AshCanyonCreature/Models/AshCanyonCreature.fbm/Material_001_Pbr_Normal.png",
                AshCanyonAnimationSetPath,
                "Assets/_Project/Configs/Enemies/Ash_Canyon_Aggressive.asset",
                new[] { "Assets/_Project/Configs/Enemies/Ash_Canyon_Aggressive.asset" },
                new[] { "Ash Canyon Creature" },
                MMOCreatureBodyType.Biped,
                2.25f,
                0.6f,
                2.25f,
                new Vector3(0f, 1.125f, 0f),
                Vector3.zero,
                Vector3.zero,
                0f,
                0.4f,
                0f);

            CreateOrUpdateVisualDefinition(
                "Assets/Characters/Wolf/Wolf_Visual.asset",
                "Wolf",
                "Wolf",
                WolfModelPath,
                "Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture.png",
                "Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture_normal.png",
                WolfAnimationSetPath,
                WolfEnemyDefinitionPath,
                new[] { WolfEnemyDefinitionPath },
                new[] { "Wolf" },
                MMOCreatureBodyType.Quadruped,
                1.15f,
                0.38f,
                1.75f,
                new Vector3(0f, 0.56f, 0f),
                Vector3.zero,
                Vector3.zero,
                0f,
                0.25f,
                0f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Standard creature visual definitions created or updated under Assets/Characters.");
        }

        [MenuItem("Tools/RPG Clone/Creatures/Rebuild Ash Canyon Animations")]
        public static void RebuildAshCanyonAnimations()
        {
            CreateOrUpdateAshCanyonAnimationSet();
            CreateStandardCreatureVisualDefinitions();
            RebuildCreatureVisualPrefabs();
            Debug.Log("Ash Canyon creature animations rebuilt from the Ash-specific source FBXs.");
        }

        [MenuItem("Tools/RPG Clone/Creatures/Rebuild Creature Visual Prefabs")]
        public static void RebuildCreatureVisualPrefabs()
        {
            int builtCount = 0;
            foreach (MMOCreatureVisualDefinition visualDefinition in LoadAllVisualDefinitions())
            {
                if (BuildCreaturePrefab(visualDefinition) != null)
                {
                    builtCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt {builtCount} creature visual prefab(s).");
        }

        [MenuItem("Tools/RPG Clone/Creatures/Convert Active Scene Creature Visuals")]
        public static void ConvertActiveSceneCreatureVisuals()
        {
            ConvertActiveSceneCreatureVisualsInternal(true);
        }

        public static void ConvertActiveSceneCreatureVisualsInternal(bool saveScene)
        {
            List<MMOCreatureVisualDefinition> visualDefinitions = LoadAllVisualDefinitions();
            Dictionary<MMOCreatureVisualDefinition, GameObject> prefabs = new();
            foreach (MMOCreatureVisualDefinition visualDefinition in visualDefinitions)
            {
                GameObject prefab = BuildCreaturePrefab(visualDefinition);
                if (prefab != null)
                {
                    prefabs[visualDefinition] = prefab;
                }
            }

            int convertedCount = 0;
            int refreshedCount = 0;
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject sceneObject in sceneObjects)
            {
                MMOEnemyController enemyController = sceneObject.GetComponent<MMOEnemyController>();
                if (enemyController == null)
                {
                    continue;
                }

                MMOCreatureVisualDefinition visualDefinition = ResolveVisualDefinition(sceneObject, enemyController, visualDefinitions);
                if (visualDefinition == null || !prefabs.TryGetValue(visualDefinition, out GameObject prefab))
                {
                    continue;
                }

                if (PrefabUtility.GetCorrespondingObjectFromSource(sceneObject) == prefab)
                {
                    SnapRootToGround(sceneObject.transform);
                    refreshedCount++;
                    continue;
                }

                ReplaceSceneEnemy(sceneObject, prefab, enemyController.Definition);
                convertedCount++;
            }

            if (convertedCount > 0 || refreshedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                if (saveScene)
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Converted {convertedCount} creature scene instance(s) and refreshed {refreshedCount} existing creature visual instance(s).");
        }

        public static GameObject BuildCreaturePrefab(MMOCreatureVisualDefinition visualDefinition)
        {
            if (visualDefinition == null || visualDefinition.ModelPrefab == null || visualDefinition.AnimationSet == null || visualDefinition.DefaultEnemyDefinition == null)
            {
                Debug.LogWarning($"Skipping creature visual '{(visualDefinition != null ? visualDefinition.name : "null")}' because model, animation set, or default enemy definition is missing.");
                return null;
            }

            string creatureFolder = GetAssetFolder(visualDefinition);
            string materialsFolder = $"{creatureFolder}/Materials";
            string prefabsFolder = $"{creatureFolder}/Prefabs";
            CreateFolderIfMissing(materialsFolder);
            CreateFolderIfMissing(prefabsFolder);

            Material material = CreateOrUpdateMaterial(visualDefinition, materialsFolder);
            string prefabPath = $"{prefabsFolder}/{visualDefinition.CreatureId}Enemy.prefab";

            GameObject root = new($"{visualDefinition.CreatureId}Enemy");
            root.isStatic = false;

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = visualDefinition.ColliderRadius;
            collider.height = visualDefinition.ColliderLength;
            collider.direction = visualDefinition.ColliderDirection;
            collider.center = visualDefinition.ColliderCenter;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = visualDefinition.ColliderRadius;
            agent.height = visualDefinition.TargetHeight;
            agent.baseOffset = 0f;
            agent.speed = visualDefinition.DefaultEnemyDefinition.WalkSpeed;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = visualDefinition.DefaultEnemyDefinition.StoppingDistance;

            root.AddComponent<MMOCharacterIdentity>();
            root.AddComponent<MMOCombatant>();
            root.AddComponent<MMOAbilitySystem>();
            root.AddComponent<MMOCharacterRegeneration>();
            root.AddComponent<MMOLootableCorpse>();
            MMOAutoAttackController autoAttack = root.AddComponent<MMOAutoAttackController>();
            autoAttack.SetHandleRightClickInput(false);
            MMOAbilityDefinition autoAttackAbility = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AutoAttackPath);
            if (autoAttackAbility != null)
            {
                autoAttack.SetAutoAttackAbility(autoAttackAbility);
            }

            MMOEnemyController enemyController = root.AddComponent<MMOEnemyController>();
            enemyController.SetDefinition(visualDefinition.DefaultEnemyDefinition, true);

            GameObject visual = PrefabUtility.InstantiatePrefab(visualDefinition.ModelPrefab) as GameObject;
            if (visual == null)
            {
                Object.DestroyImmediate(root);
                Debug.LogError($"Could not instantiate model for {visualDefinition.DisplayName}.");
                return null;
            }

            visual.name = $"{visualDefinition.DisplayName} Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = visualDefinition.VisualLocalOffset;
            visual.transform.localRotation = Quaternion.Euler(visualDefinition.VisualLocalEulerAngles);
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = visualDefinition.AnimationSet.BaseController;
            AssignMaterialToRenderers(visual, material);
            FitVisualToGroundedHeight(visual, visualDefinition.TargetHeight, visualDefinition.VisualLocalOffset);

            MMOCreatureAnimator creatureAnimator = root.AddComponent<MMOCreatureAnimator>();
            creatureAnimator.Configure(visualDefinition.AnimationSet, animator, visual.transform, visualDefinition.ModelYawOffsetDegrees);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void CreateOrUpdateVisualDefinition(
            string assetPath,
            string creatureId,
            string displayName,
            string modelPath,
            string diffusePath,
            string normalPath,
            string animationSetPath,
            string defaultEnemyDefinitionPath,
            IEnumerable<string> matchingEnemyDefinitionPaths,
            IEnumerable<string> sceneNamePrefixes,
            MMOCreatureBodyType bodyType,
            float targetHeight,
            float colliderRadius,
            float colliderLength,
            Vector3 colliderCenter,
            Vector3 visualLocalOffset,
            Vector3 visualLocalEulerAngles,
            float modelYawOffsetDegrees,
            float smoothness,
            float metallic)
        {
            CreateFolderIfMissing(Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets/Characters");
            ConfigureModelImporter(modelPath);
            ConfigureTextureImporter(diffusePath, false);
            ConfigureTextureImporter(normalPath, true);

            MMOCreatureVisualDefinition definition = AssetDatabase.LoadAssetAtPath<MMOCreatureVisualDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOCreatureVisualDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            List<MMOEnemyDefinition> matchingEnemyDefinitions = new();
            foreach (string path in matchingEnemyDefinitionPaths)
            {
                MMOEnemyDefinition enemyDefinition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(path);
                if (enemyDefinition != null)
                {
                    matchingEnemyDefinitions.Add(enemyDefinition);
                }
            }

            definition.Configure(
                creatureId,
                displayName,
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath),
                string.IsNullOrWhiteSpace(normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(animationSetPath),
                AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(defaultEnemyDefinitionPath),
                matchingEnemyDefinitions,
                sceneNamePrefixes,
                bodyType,
                targetHeight,
                colliderRadius,
                colliderLength,
                colliderCenter,
                visualLocalOffset,
                visualLocalEulerAngles,
                modelYawOffsetDegrees,
                smoothness,
                metallic);
            EditorUtility.SetDirty(definition);
        }

        private static MMOCreatureAnimationSet CreateOrUpdateAshCanyonAnimationSet()
        {
            CreateFolderIfMissing(AshCanyonAnimationClipFolder);
            OrganizeAshCanyonAnimationSources();

            AnimationClip idle = ExtractAnimationClip("AshCanyonCreature_idle.fbx", "AshCanyonCreature_Idle", true);
            AnimationClip walk = ExtractAnimationClip("AshCanyonCreature_walk.fbx", "AshCanyonCreature_Walk", true);
            AnimationClip run = ExtractAnimationClip("AshCanyonCreature_run.fbx", "AshCanyonCreature_Run", true);
            AnimationClip attack1 = ExtractAnimationClip("AshCanyonCreature_attack1.fbx", "AshCanyonCreature_Attack1", false);
            AnimationClip attack2 = ExtractAnimationClip("AshCanyonCreature_attack2.fbx", "AshCanyonCreature_Attack2", false);
            AnimationClip damage = ExtractAnimationClip("AshCanyonCreature_damage.fbx", "AshCanyonCreature_Damage", false);
            AnimationClip death = ExtractAnimationClip("AshCanyonCreature_death.fbx", "AshCanyonCreature_Death", false);
            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);

            MMOCreatureAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(AshCanyonAnimationSetPath);
            if (animationSet == null)
            {
                animationSet = ScriptableObject.CreateInstance<MMOCreatureAnimationSet>();
                AssetDatabase.CreateAsset(animationSet, AshCanyonAnimationSetPath);
            }

            animationSet.name = "AshCanyonCreature_AnimationSet";
            animationSet.Configure(
                baseController,
                idle,
                walk,
                run,
                attack1,
                attack2,
                damage,
                death,
                1.45f,
                4.25f,
                0.85f,
                0.45f,
                0.12f,
                false,
                0f);
            EditorUtility.SetDirty(animationSet);
            DeleteAssetIfPresent(AshCanyonAnimationSourceFolder);
            return animationSet;
        }

        private static MMOCreatureAnimationSet CreateOrUpdateWolfAnimationSet()
        {
            CreateFolderIfMissing(WolfAnimationClipFolder);

            AnimationClip idle = ExtractAnimationClip(WolfModelPath, "IdleFinal", "Wolf_Idle", true);
            AnimationClip walk = ExtractAnimationClip(WolfModelPath, "WalkFinal", "Wolf_Walk", true);
            AnimationClip run = ExtractAnimationClip(WolfModelPath, "RunFinal", "Wolf_Run", true);
            AnimationClip attack1 = ExtractAnimationClip(WolfModelPath, "AttackFinal", "Wolf_Attack1", false);
            AnimationClip attack2 = ExtractAnimationClip(WolfModelPath, "Attack2Final", "Wolf_Attack2", false);
            AnimationClip damage = ExtractAnimationClip(WolfModelPath, "DamageFinal", "Wolf_Damage", false);
            AnimationClip death = ExtractAnimationClip(WolfModelPath, "DeathFinal", "Wolf_Death", false);
            RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);

            MMOCreatureAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(WolfAnimationSetPath);
            if (animationSet == null)
            {
                animationSet = ScriptableObject.CreateInstance<MMOCreatureAnimationSet>();
                AssetDatabase.CreateAsset(animationSet, WolfAnimationSetPath);
            }

            animationSet.name = "Wolf_AnimationSet";
            animationSet.Configure(
                baseController,
                idle,
                walk,
                run,
                attack1,
                attack2,
                damage,
                death,
                1.65f,
                5.1f,
                0.75f,
                0.35f,
                0.1f,
                false,
                0f);
            EditorUtility.SetDirty(animationSet);
            return animationSet;
        }

        private static MMOEnemyDefinition CreateOrUpdateWolfEnemyDefinition()
        {
            CreateFolderIfMissing(Path.GetDirectoryName(WolfProfilePath)?.Replace('\\', '/') ?? RootFolder + "/Configs/Characters");
            CreateFolderIfMissing(Path.GetDirectoryName(WolfEnemyDefinitionPath)?.Replace('\\', '/') ?? RootFolder + "/Configs/Enemies");
            CreateFolderIfMissing(Path.GetDirectoryName(WolfLootPath)?.Replace('\\', '/') ?? RootFolder + "/Configs/Loot");

            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(WolfProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, WolfProfilePath);
            }

            MMOCharacterStats stats = new();
            stats.Configure(8, 10, 13, 3, 5, 5, 11, 0, 3.5f, 6.5f, 2.1f, 2.8f);
            profile.Configure("Wolf", 3, 80, 0, new Color(0.55f, 0.62f, 0.64f), null, true, MMOEntityFaction.Hostile, stats);
            EditorUtility.SetDirty(profile);

            MMOLootTable lootTable = AssetDatabase.LoadAssetAtPath<MMOLootTable>(WolfLootPath);
            if (lootTable == null)
            {
                lootTable = ScriptableObject.CreateInstance<MMOLootTable>();
                AssetDatabase.CreateAsset(lootTable, WolfLootPath);
            }

            MMOItemDefinition mattedPelt = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(RootFolder + "/Configs/Items/Matted_Pelt.asset");
            lootTable.Configure(mattedPelt != null
                ? new[] { new MMOLootTableEntry(mattedPelt, 0.45f, 1, 1) }
                : Array.Empty<MMOLootTableEntry>());
            EditorUtility.SetDirty(lootTable);

            MMOAbilityDefinition autoAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AutoAttackPath);
            MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(WolfEnemyDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOEnemyDefinition>();
                AssetDatabase.CreateAsset(definition, WolfEnemyDefinitionPath);
            }

            definition.Configure(
                profile,
                MMOEnemyDisposition.Aggressive,
                autoAttack,
                autoAttack != null ? new[] { autoAttack } : Array.Empty<MMOAbilityDefinition>(),
                13f,
                34f,
                0.25f,
                true,
                6.5f,
                2f,
                5f,
                1.65f,
                5.1f,
                2.2f,
                50,
                lootTable);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void OrganizeAshCanyonAnimationSources()
        {
            MoveAssetIfNeeded("Assets/AshCanyonCreature.json", "Assets/Characters/AshCanyonCreature/Models/AshCanyonCreature.json");
            DeleteAssetIfPresent("Assets/textures/AshCanyonCreature");

            string[] animationNames =
            {
                "idle",
                "walk",
                "run",
                "attack1",
                "attack2",
                "damage",
                "death"
            };

            foreach (string animationName in animationNames)
            {
                string assetName = $"AshCanyonCreature_{animationName}";
                MoveAssetIfNeeded($"Assets/{assetName}.fbx", $"{AshCanyonAnimationSourceFolder}/{assetName}.fbx");
                MoveAssetIfNeeded($"Assets/{assetName}.json", $"{AshCanyonAnimationSourceFolder}/{assetName}.json");
                DeleteAssetIfPresent($"Assets/{assetName}.fbm");
                DeleteAssetIfPresent($"Assets/textures/{assetName}");
            }
        }

        private static void MoveAssetIfNeeded(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                return;
            }

            CreateFolderIfMissing(Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "Assets");
            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"Could not move {sourcePath} to {destinationPath}: {error}");
            }
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                return;
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                Debug.LogWarning($"Could not delete duplicate animation texture folder at {assetPath}.");
            }
        }

        private static AnimationClip ExtractAnimationClip(string sourceFileName, string outputName, bool loop)
        {
            string sourcePath = $"{AshCanyonAnimationSourceFolder}/{sourceFileName}";
            string outputPath = $"{AshCanyonAnimationClipFolder}/{outputName}.anim";
            return ExtractAnimationClip(sourcePath, null, outputPath, outputName, loop);
        }

        private static AnimationClip ExtractAnimationClip(string sourcePath, string sourceClipName, string outputName, bool loop)
        {
            string outputPath = $"{WolfAnimationClipFolder}/{outputName}.anim";
            return ExtractAnimationClip(sourcePath, sourceClipName, outputPath, outputName, loop);
        }

        private static AnimationClip ExtractAnimationClip(string sourcePath, string sourceClipName, string outputPath, string outputName, bool loop)
        {
            ConfigureAnimationImporter(sourcePath, loop);

            AnimationClip outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            IEnumerable<AnimationClip> sourceClips = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(IsUsableSourceClip);
            AnimationClip sourceClip = string.IsNullOrWhiteSpace(sourceClipName)
                ? sourceClips.FirstOrDefault()
                : sourceClips.FirstOrDefault(clip => string.Equals(clip.name, sourceClipName, StringComparison.OrdinalIgnoreCase));
            if (sourceClip == null)
            {
                if (outputClip != null)
                {
                    return outputClip;
                }

                Debug.LogError(string.IsNullOrWhiteSpace(sourceClipName)
                    ? $"Could not find a usable animation clip in {sourcePath}."
                    : $"Could not find animation clip '{sourceClipName}' in {sourcePath}.");
                return null;
            }

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

        private static bool IsUsableSourceClip(AnimationClip clip)
        {
            if (clip == null
                || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)
                || clip.name.StartsWith("preview", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalizedName = clip.name.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            return normalizedName.IndexOf("tpose", StringComparison.OrdinalIgnoreCase) < 0
                && normalizedName.IndexOf("bindpose", StringComparison.OrdinalIgnoreCase) < 0
                && normalizedName.IndexOf("referencepose", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void ConfigureAnimationImporter(string sourcePath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            if (importer == null)
            {
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
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool isPose = clip.name.IndexOf("pose", StringComparison.OrdinalIgnoreCase) >= 0
                    || clip.takeName.IndexOf("pose", StringComparison.OrdinalIgnoreCase) >= 0;
                clip.loopTime = loop && !isPose;
                clip.loopPose = loop && !isPose;
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

        private static void ConfigureModelImporter(string modelPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string texturePath, bool normalMap)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.SaveAndReimport();
        }

        private static List<MMOCreatureVisualDefinition> LoadAllVisualDefinitions()
        {
            List<MMOCreatureVisualDefinition> definitions = new();
            string[] guids = AssetDatabase.FindAssets("t:MMOCreatureVisualDefinition", new[] { "Assets/Characters" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MMOCreatureVisualDefinition definition = AssetDatabase.LoadAssetAtPath<MMOCreatureVisualDefinition>(path);
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        private static MMOCreatureVisualDefinition ResolveVisualDefinition(
            GameObject sceneObject,
            MMOEnemyController enemyController,
            IReadOnlyList<MMOCreatureVisualDefinition> visualDefinitions)
        {
            foreach (MMOCreatureVisualDefinition visualDefinition in visualDefinitions)
            {
                if (MatchesSceneObject(sceneObject, enemyController, visualDefinition))
                {
                    return visualDefinition;
                }
            }

            return null;
        }

        private static bool MatchesSceneObject(GameObject sceneObject, MMOEnemyController enemyController, MMOCreatureVisualDefinition visualDefinition)
        {
            foreach (MMOEnemyDefinition definition in visualDefinition.MatchingEnemyDefinitions)
            {
                if (definition != null && enemyController.Definition == definition)
                {
                    return true;
                }
            }

            foreach (string prefix in visualDefinition.SceneNamePrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && sceneObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Material CreateOrUpdateMaterial(MMOCreatureVisualDefinition visualDefinition, string materialsFolder)
        {
            string materialPath = $"{materialsFolder}/{visualDefinition.CreatureId}_Body.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            SetTextureIfPresent(material, "_BaseMap", visualDefinition.DiffuseTexture);
            SetTextureIfPresent(material, "_MainTex", visualDefinition.DiffuseTexture);
            SetTextureIfPresent(material, "_BumpMap", visualDefinition.NormalTexture);

            if (visualDefinition.NormalTexture != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            SetFloatIfPresent(material, "_Smoothness", visualDefinition.Smoothness);
            SetFloatIfPresent(material, "_Metallic", visualDefinition.Metallic);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void AssignMaterialToRenderers(GameObject visual, Material material)
        {
            if (visual == null || material == null)
            {
                return;
            }

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void FitVisualToGroundedHeight(GameObject visual, float targetHeight, Vector3 localOffset)
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
            visual.transform.position += new Vector3(0f, localOffset.y - bounds.min.y, 0f);
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
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
            SnapRootToGround(instance.transform);
            EditorUtility.SetDirty(instance);
        }

        private static Vector3 GetGroundedPosition(GameObject source)
        {
            if (MMOGroundingUtility.TryGetGroundedPosition(source.transform, source.GetComponent<Collider>(), out Vector3 groundedPosition))
            {
                return groundedPosition;
            }

            return source.transform.position;
        }

        private static void SnapRootToGround(Transform transformToGround)
        {
            if (transformToGround == null)
            {
                return;
            }

            MMOGroundingUtility.SnapTransformToGround(transformToGround, transformToGround.GetComponent<Collider>());
            EditorUtility.SetDirty(transformToGround);
        }

        private static string GetAssetFolder(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets/Characters";
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
    }
}
