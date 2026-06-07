using System;
using System.IO;
using System.Linq;
using RPGClone.Abilities;
using RPGClone.Animation;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Loot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOBristlebackAnimationInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string AnimationFolder = RootFolder + "/Animations";
        private const string CreatureAnimationFolder = AnimationFolder + "/Creatures";
        private const string LegacyBristlebackAnimationFolder = CreatureAnimationFolder + "/Bristleback";
        private const string BristlebackAnimationFolder = "Assets/Characters/Bristleback/Animations/Clips";
        private const string EnemyPrefabFolder = RootFolder + "/Prefabs/Enemies";
        private const string EnemyConfigFolder = RootFolder + "/Configs/Enemies";
        private const string BristlebackMaterialFolder = "Assets/Characters/Bristleback/Materials";
        private const string BristlebackMaterialPath = BristlebackMaterialFolder + "/Bristleback_Body.mat";
        private const string BristlebackDiffusePath = "Assets/Characters/Bristleback/Bristleback.fbm/Material_1_Pbr_Diffuse.png";
        private const string BaseControllerPath = CreatureAnimationFolder + "/MMOCreatureBase.controller";
        private const string AnimationSetPath = BristlebackAnimationFolder + "/Bristleback_AnimationSet.asset";
        private const string BristlebackPrefabPath = EnemyPrefabFolder + "/BristlebackEnemy.prefab";
        private const string BaseModelPath = "Assets/Characters/Bristleback/Animations/Bristleback.fbx";
        private const float BristlebackTargetHeight = 2.25f;
        private const float BristlebackColliderRadius = 0.6f;

        [MenuItem("Tools/RPG Clone/Enemies/Install Bristleback Animations")]
        public static void InstallBristlebackAnimations()
        {
            InstallBristlebackAnimations(true);
        }

        private static void InstallBristlebackAnimations(bool refreshAssetDatabase)
        {
            EnsureFolders();
            MigrateLegacyBristlebackAnimationAssets();
            ConfigureBristlebackImporters();

            AnimationClip idle = ExtractClip(BaseModelPath, "idle", "Bristleback_Idle", true);
            AnimationClip walk = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_walk.fbx", "walk", "Bristleback_Walk", true);
            AnimationClip run = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_run.fbx", "run", "Bristleback_Run", true);
            AnimationClip attack1 = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_attack1.fbx", "atk1", "Bristleback_Attack1", false);
            AnimationClip attack2 = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_attack2.fbx", "atk2", "Bristleback_Attack2", false);
            AnimationClip damage = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_damage.fbx", "damage", "Bristleback_Damage", false);
            AnimationClip death = ExtractClip("Assets/Characters/Bristleback/Animations/Bristleback_death.fbx", "death", "Bristleback_Death", false);
            RuntimeAnimatorController controller = CreateOrUpdateCreatureController();
            MMOCreatureAnimationSet animationSet = CreateOrUpdateAnimationSet(controller, idle, walk, run, attack1, attack2, damage, death);

            CreateOrUpdateBristlebackPrefab(animationSet);
            AssetDatabase.SaveAssets();
            if (refreshAssetDatabase)
            {
                AssetDatabase.Refresh();
            }

            Debug.Log("Bristleback animations installed. Use Assets/_Project/Prefabs/Enemies/BristlebackEnemy.prefab for bristleback spawns.");
        }

        [MenuItem("Tools/RPG Clone/Enemies/Convert Scene Bristlebacks To Animated Prefab")]
        public static void ConvertSceneBristlebacksToAnimatedPrefab()
        {
            InstallBristlebackAnimations(false);

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

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(AnimationFolder);
            CreateFolderIfMissing(CreatureAnimationFolder);
            CreateFolderIfMissing(BristlebackAnimationFolder);
            CreateFolderIfMissing(BristlebackMaterialFolder);
            CreateFolderIfMissing(RootFolder + "/Prefabs");
            CreateFolderIfMissing(EnemyPrefabFolder);
        }

        private static void MigrateLegacyBristlebackAnimationAssets()
        {
            MoveLegacyAssetIfNeeded("Bristleback_AnimationSet.asset");
            MoveLegacyAssetIfNeeded("Bristleback_Idle.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Walk.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Run.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Attack1.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Attack2.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Damage.anim");
            MoveLegacyAssetIfNeeded("Bristleback_Death.anim");
        }

        private static void MoveLegacyAssetIfNeeded(string fileName)
        {
            string oldPath = $"{LegacyBristlebackAnimationFolder}/{fileName}";
            string newPath = $"{BristlebackAnimationFolder}/{fileName}";
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null || AssetDatabase.LoadMainAssetAtPath(oldPath) == null)
            {
                return;
            }

            string moveError = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogWarning($"Could not move {oldPath} to {newPath}: {moveError}");
            }
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

        private static void ConfigureBristlebackImporters()
        {
            ModelImporter baseImporter = AssetImporter.GetAtPath(BaseModelPath) as ModelImporter;
            if (baseImporter == null)
            {
                Debug.LogError($"Missing bristleback base model at {BaseModelPath}.");
                return;
            }

            ConfigureImporter(baseImporter, null, true, true);
            baseImporter.SaveAndReimport();

            Avatar sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(BaseModelPath).OfType<Avatar>().FirstOrDefault();
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_walk.fbx", sourceAvatar, true);
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_run.fbx", sourceAvatar, true);
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_attack1.fbx", sourceAvatar, false);
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_attack2.fbx", sourceAvatar, false);
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_damage.fbx", sourceAvatar, false);
            ConfigureAnimationImporter("Assets/Characters/Bristleback/Animations/Bristleback_death.fbx", sourceAvatar, false);
        }

        private static void ConfigureAnimationImporter(string path, Avatar sourceAvatar, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ConfigureImporter(importer, sourceAvatar, false, loop);
            importer.SaveAndReimport();
        }

        private static void ConfigureImporter(ModelImporter importer, Avatar sourceAvatar, bool createAvatar, bool loop)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = createAvatar ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = createAvatar ? null : sourceAvatar;
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
        }

        private static AnimationClip ExtractClip(string sourcePath, string sourceClipName, string outputName, bool loop)
        {
            string outputPath = $"{BristlebackAnimationFolder}/{outputName}.anim";
            AnimationClip outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            AnimationClip sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == sourceClipName);
            if (sourceClip == null)
            {
                if (outputClip != null)
                {
                    return outputClip;
                }

                Debug.LogError($"Could not find clip '{sourceClipName}' in {sourcePath}.");
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

        private static RuntimeAnimatorController CreateOrUpdateCreatureController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(BaseControllerPath);
            }

            AnimationClip idle = CreatePlaceholderClip(MMOCreatureAnimationSet.IdlePlaceholderName, true);
            AnimationClip walk = CreatePlaceholderClip(MMOCreatureAnimationSet.WalkPlaceholderName, true);
            AnimationClip run = CreatePlaceholderClip(MMOCreatureAnimationSet.RunPlaceholderName, true);
            AnimationClip attack1 = CreatePlaceholderClip(MMOCreatureAnimationSet.Attack1PlaceholderName, false);
            AnimationClip attack2 = CreatePlaceholderClip(MMOCreatureAnimationSet.Attack2PlaceholderName, false);
            AnimationClip damage = CreatePlaceholderClip(MMOCreatureAnimationSet.DamagePlaceholderName, false);
            AnimationClip death = CreatePlaceholderClip(MMOCreatureAnimationSet.DeathPlaceholderName, false);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            controller.AddParameter(MMOCreatureAnimationSet.MoveSpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(MMOCreatureAnimationSet.Attack1Parameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MMOCreatureAnimationSet.Attack2Parameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MMOCreatureAnimationSet.DamageParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MMOCreatureAnimationSet.DeathParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MMOCreatureAnimationSet.DeadParameter, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            BlendTree locomotionTree = new()
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = MMOCreatureAnimationSet.MoveSpeedParameter,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(locomotionTree, controller);
            locomotionTree.AddChild(idle, 0f);
            locomotionTree.AddChild(walk, 0.5f);
            locomotionTree.AddChild(run, 1f);

            AnimatorState locomotion = stateMachine.AddState("Locomotion", new Vector3(260f, 40f, 0f));
            locomotion.motion = locomotionTree;
            locomotion.writeDefaultValues = false;
            stateMachine.defaultState = locomotion;

            AnimatorState attack1State = AddActionState(stateMachine, "Attack1", "Attack", attack1, new Vector3(520f, -80f, 0f));
            AnimatorState attack2State = AddActionState(stateMachine, "Attack2", "Attack", attack2, new Vector3(520f, 20f, 0f));
            AnimatorState damageState = AddActionState(stateMachine, "Damage", "Damage", damage, new Vector3(520f, 130f, 0f));
            AnimatorState deathState = AddActionState(stateMachine, "Death", "Death", death, new Vector3(520f, 250f, 0f));

            AddAnyStateTriggerTransition(stateMachine, attack1State, MMOCreatureAnimationSet.Attack1Parameter, 0.05f);
            AddAnyStateTriggerTransition(stateMachine, attack2State, MMOCreatureAnimationSet.Attack2Parameter, 0.05f);
            AddAnyStateTriggerTransition(stateMachine, damageState, MMOCreatureAnimationSet.DamageParameter, 0.04f);
            AddAnyStateTriggerTransition(stateMachine, deathState, MMOCreatureAnimationSet.DeathParameter, 0.04f);

            AnimatorStateTransition deadTransition = stateMachine.AddAnyStateTransition(deathState);
            deadTransition.canTransitionToSelf = false;
            deadTransition.hasExitTime = false;
            deadTransition.duration = 0.04f;
            deadTransition.AddCondition(AnimatorConditionMode.If, 0f, MMOCreatureAnimationSet.DeadParameter);

            AddReturnTransition(attack1State, locomotion, 0.82f, 0.12f);
            AddReturnTransition(attack2State, locomotion, 0.82f, 0.12f);
            AddReturnTransition(damageState, locomotion, 0.78f, 0.1f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip CreatePlaceholderClip(string clipName, bool loop)
        {
            string path = $"{CreatureAnimationFolder}/{clipName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.name = clipName;
            clip.frameRate = 30f;
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, loop ? 1f : 0.5f, 0f));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorState AddActionState(AnimatorStateMachine stateMachine, string stateName, string tag, Motion motion, Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(stateName, position);
            state.motion = motion;
            state.tag = tag;
            state.writeDefaultValues = false;
            return state;
        }

        private static void AddAnyStateTriggerTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string trigger, float duration)
        {
            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
            transition.canTransitionToSelf = false;
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddReturnTransition(AnimatorState source, AnimatorState destination, float exitTime, float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
        }

        private static MMOCreatureAnimationSet CreateOrUpdateAnimationSet(
            RuntimeAnimatorController controller,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip attack1,
            AnimationClip attack2,
            AnimationClip damage,
            AnimationClip death)
        {
            MMOCreatureAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(AnimationSetPath);
            if (animationSet == null)
            {
                animationSet = ScriptableObject.CreateInstance<MMOCreatureAnimationSet>();
                AssetDatabase.CreateAsset(animationSet, AnimationSetPath);
            }

            animationSet.Configure(
                controller,
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
            return animationSet;
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

        private static void CreateOrUpdateBristlebackPrefab(MMOCreatureAnimationSet animationSet)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModelPath);
            MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>($"{EnemyConfigFolder}/Bristleback_Aggressive.asset");
            MMOAbilityDefinition autoAttackAbility = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{RootFolder}/Configs/Abilities/Auto_Attack.asset");
            Material bristlebackMaterial = CreateOrUpdateBristlebackMaterial();
            if (modelPrefab == null || definition == null)
            {
                Debug.LogError("Bristleback prefab creation failed because the base model or Bristleback_Aggressive definition is missing.");
                return;
            }

            GameObject root = new("BristlebackEnemy");
            root.isStatic = false;

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = BristlebackColliderRadius;
            collider.height = BristlebackTargetHeight;
            collider.center = new Vector3(0f, BristlebackTargetHeight * 0.5f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = BristlebackColliderRadius;
            agent.height = BristlebackTargetHeight;
            agent.baseOffset = 0f;
            agent.speed = definition.WalkSpeed;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = definition.StoppingDistance;

            root.AddComponent<MMOCharacterIdentity>();
            root.AddComponent<MMOCombatant>();
            root.AddComponent<MMOAbilitySystem>();
            root.AddComponent<MMOCharacterRegeneration>();
            root.AddComponent<MMOLootableCorpse>();
            MMOAutoAttackController autoAttack = root.AddComponent<MMOAutoAttackController>();
            autoAttack.SetHandleRightClickInput(false);
            if (autoAttackAbility != null)
            {
                autoAttack.SetAutoAttackAbility(autoAttackAbility);
            }

            MMOEnemyController enemyController = root.AddComponent<MMOEnemyController>();
            enemyController.SetDefinition(definition, true);

            GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (visual == null)
            {
                Object.DestroyImmediate(root);
                Debug.LogError("Could not instantiate Bristleback.fbx while building the bristleback enemy prefab.");
                return;
            }

            visual.name = "Bristleback Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = animationSet.BaseController;
            AssignMaterialToRenderers(visual, bristlebackMaterial);
            FitVisualToGroundedHeight(visual, BristlebackTargetHeight);

            MMOCreatureAnimator creatureAnimator = root.AddComponent<MMOCreatureAnimator>();
            creatureAnimator.Configure(animationSet, animator, visual.transform);

            PrefabUtility.SaveAsPrefabAsset(root, BristlebackPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static Material CreateOrUpdateBristlebackMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BristlebackMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BristlebackMaterialPath);
            }

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(BristlebackDiffusePath);
            if (diffuse != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", diffuse);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", diffuse);
                }
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            EditorUtility.SetDirty(material);
            return material;
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

        private static void FitVisualToGroundedHeight(GameObject visual, float targetHeight)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y > 0.001f)
            {
                float scale = targetHeight / bounds.size.y;
                visual.transform.localScale = Vector3.one * scale;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            visual.transform.position += new Vector3(0f, -bounds.min.y, 0f);
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
