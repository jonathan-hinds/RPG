using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Abilities;
using RPGClone.Animation;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Loot;
using RPGClone.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOTrogAuthoringInstaller
    {
        private const string RawFolder = "Assets/Trog";
        private const string CreatureFolder = "Assets/Characters/Trog";
        private const string ModelFolder = CreatureFolder + "/Models";
        private const string AnimationSourceFolder = CreatureFolder + "/Animations/Source";
        private const string AnimationClipFolder = CreatureFolder + "/Animations/Clips";
        private const string TextureFolder = CreatureFolder + "/textures";
        private const string ModelPath = ModelFolder + "/Trog.fbx";
        private const string TexturePath = TextureFolder + "/Trog_BaseColor.jpg";
        private const string AnimationSetPath = AnimationClipFolder + "/Trog_AnimationSet.asset";
        private const string VisualDefinitionPath = CreatureFolder + "/Trog_Visual.asset";
        private const string ProfilePath = "Assets/_Project/Configs/Characters/Trog.asset";
        private const string EnemyDefinitionPath = "Assets/_Project/Configs/Enemies/Trog_Caster_Aggressive.asset";
        private const string LightningBoltPath = "Assets/_Project/Configs/Abilities/Trog_Lightning_Bolt.asset";
        private const string LootTablePath = "Assets/_Project/Configs/Loot/Trog_Trash_Loot.asset";
        private const string AutoAttackPath = "Assets/_Project/Configs/Abilities/Auto_Attack.asset";
        private const string SharedLightningVfxPath = "Assets/_Project/VFX/Definitions/Shaman_Lightning_Bolt_VFX.asset";
        private const string BaseControllerPath = "Assets/_Project/Animations/Creatures/MMOCreatureBase.controller";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";
        private const string SpawnParentName = "Placeholder Creature Spawns";
        private const string SpawnName = "Trog Caster 01";

        [MenuItem("Tools/RPG Clone/Creatures/Install Trog Caster")]
        public static void InstallTrog()
        {
            EnsureFolders();
            OrganizeRawAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimationClip idle = ExtractClip(ModelPath, "Trog_Idle", true);
            AnimationClip walk = ExtractClip(AnimationSourceFolder + "/Walk.fbx", "Trog_Walk", true);
            AnimationClip run = ExtractClip(AnimationSourceFolder + "/Run.fbx", "Trog_Run", true);
            AnimationClip attack = ExtractClip(AnimationSourceFolder + "/Attack.fbx", "Trog_Attack", false);
            AnimationClip damage = ExtractClip(AnimationSourceFolder + "/Damage.fbx", "Trog_Damage", false);
            AnimationClip death = ExtractClip(AnimationSourceFolder + "/Death.fbx", "Trog_Death", false);
            AnimationClip casting = ExtractClip(AnimationSourceFolder + "/Casting.fbx", "Trog_Casting", true);
            AnimationClip cast = ExtractClip(AnimationSourceFolder + "/Cast.fbx", "Trog_Cast", false);

            ValidateClipSet(idle, walk, run, attack, damage, death, casting, cast);
            MMOCreatureAnimationSet animationSet = CreateAnimationSet(idle, walk, run, attack, damage, death, casting, cast);
            MMOCharacterProfile profile = CreateProfile();
            MMOAbilityDefinition lightningBolt = CreateLightningBolt();
            MMOLootTable lootTable = CreateLootTable();
            MMOEnemyDefinition enemyDefinition = CreateEnemyDefinition(profile, lightningBolt, lootTable);
            MMOCreatureVisualDefinition visualDefinition = CreateVisualDefinition(animationSet, enemyDefinition);
            GameObject prefab = MMOCreatureVisualAuthoringInstaller.BuildCreaturePrefab(visualDefinition);
            if (prefab == null)
            {
                throw new InvalidOperationException("Trog prefab generation failed.");
            }

            PlaceInStarterWorld(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Trog caster content installed, prefab generated, and Trog Caster 01 placed in OrcishStarterValley.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(CreatureFolder);
            EnsureFolder(ModelFolder);
            EnsureFolder(AnimationSourceFolder);
            EnsureFolder(AnimationClipFolder);
            EnsureFolder(TextureFolder);
        }

        private static void OrganizeRawAssets()
        {
            MoveIfPresent(RawFolder + "/Idle.fbx", ModelPath);
            MoveIfPresent(RawFolder + "/Walk.fbx", AnimationSourceFolder + "/Walk.fbx");
            MoveIfPresent(RawFolder + "/Run.fbx", AnimationSourceFolder + "/Run.fbx");
            MoveIfPresent(RawFolder + "/Attack.fbx", AnimationSourceFolder + "/Attack.fbx");
            MoveIfPresent(RawFolder + "/Damage.fbx", AnimationSourceFolder + "/Damage.fbx");
            MoveIfPresent(RawFolder + "/Death.fbx", AnimationSourceFolder + "/Death.fbx");
            MoveIfPresent(RawFolder + "/Casting.fbx", AnimationSourceFolder + "/Casting.fbx");
            MoveIfPresent(RawFolder + "/Cast.fbx", AnimationSourceFolder + "/Cast.fbx");
            MoveIfPresent(RawFolder + "/goblin_creature_3d_model_basecolor.JPEG", TexturePath);

            if (Directory.Exists(RawFolder) && Directory.GetFileSystemEntries(RawFolder).Length == 0)
            {
                AssetDatabase.DeleteAsset(RawFolder);
            }
        }

        private static AnimationClip ExtractClip(string sourcePath, string outputName, bool loop)
        {
            ConfigureFbxImporter(sourcePath, loop);
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(IsUsableClip);
            if (source == null)
            {
                throw new InvalidOperationException($"No usable animation clip was found in {sourcePath}.");
            }

            string outputPath = AnimationClipFolder + "/" + outputName + ".anim";
            AnimationClip output = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (output == null)
            {
                output = new AnimationClip();
                AssetDatabase.CreateAsset(output, outputPath);
            }

            EditorUtility.CopySerialized(source, output);
            output.name = outputName;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(output);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(output, settings);
            EditorUtility.SetDirty(output);
            return output;
        }

        private static void ConfigureFbxImporter(string assetPath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Missing FBX importer for {assetPath}.");
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

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
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

        private static MMOCreatureAnimationSet CreateAnimationSet(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip attack,
            AnimationClip damage,
            AnimationClip death,
            AnimationClip casting,
            AnimationClip cast)
        {
            MMOCreatureAnimationSet set = AssetDatabase.LoadAssetAtPath<MMOCreatureAnimationSet>(AnimationSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<MMOCreatureAnimationSet>();
                AssetDatabase.CreateAsset(set, AnimationSetPath);
            }

            set.name = "Trog_AnimationSet";
            set.Configure(
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath),
                idle,
                walk,
                run,
                attack,
                attack,
                damage,
                death,
                1.45f,
                4.1f,
                0.75f,
                0.4f,
                0.1f,
                false,
                0f);
            set.ConfigureCasting(casting, cast);
            EditorUtility.SetDirty(set);
            return set;
        }

        private static MMOCharacterProfile CreateProfile()
        {
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            MMOCharacterStats stats = new();
            stats.Configure(12, 7, 7, 18, 15, 8, 7, 16, 4f, 7f, 2.5f, 3f);
            profile.Configure(
                "Trog Caster",
                5,
                130,
                120,
                new Color(0.38f, 0.62f, 0.32f),
                null,
                true,
                MMOEntityFaction.Hostile,
                stats);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOAbilityDefinition CreateLightningBolt()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(LightningBoltPath);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, LightningBoltPath);
            }

            MMOAbilityEffectDefinition damage = new();
            damage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Nature, 16f, 0.6f);
            ability.Configure(
                "trog_lightning_bolt",
                "Lightning Bolt",
                "Hurls a bolt of lightning at a hostile target.",
                MMOAbilityTargetType.Hostile,
                false,
                false,
                24f,
                6f,
                10,
                2.2f,
                true,
                false,
                new[] { damage });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            ability.SetVisualEffects(AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SharedLightningVfxPath));
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOLootTable CreateLootTable()
        {
            MMOLootTable table = AssetDatabase.LoadAssetAtPath<MMOLootTable>(LootTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<MMOLootTable>();
                AssetDatabase.CreateAsset(table, LootTablePath);
            }

            MMOLootTable reference = AssetDatabase.LoadAssetAtPath<MMOLootTable>("Assets/_Project/Configs/Loot/Wolf_Trash_Loot.asset");
            List<MMOLootTableEntry> entries = new();
            if (reference != null)
            {
                foreach (MMOLootTableEntry entry in reference.Entries.Take(3))
                {
                    if (entry != null && entry.Item != null)
                    {
                        entries.Add(new MMOLootTableEntry(entry.Item, entry.DropChance, entry.MinQuantity, entry.MaxQuantity));
                    }
                }
            }

            table.Configure(entries);
            EditorUtility.SetDirty(table);
            return table;
        }

        private static MMOEnemyDefinition CreateEnemyDefinition(
            MMOCharacterProfile profile,
            MMOAbilityDefinition lightningBolt,
            MMOLootTable lootTable)
        {
            MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(EnemyDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOEnemyDefinition>();
                AssetDatabase.CreateAsset(definition, EnemyDefinitionPath);
            }

            MMOAbilityDefinition autoAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AutoAttackPath);
            definition.Configure(
                profile,
                MMOEnemyDisposition.Aggressive,
                autoAttack,
                new[] { autoAttack, lightningBolt },
                16f,
                38f,
                0.25f,
                true,
                5f,
                2.5f,
                5.5f,
                1.45f,
                4.1f,
                2.5f,
                90,
                lootTable,
                2.5f,
                6f,
                120f,
                32f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static MMOCreatureVisualDefinition CreateVisualDefinition(
            MMOCreatureAnimationSet animationSet,
            MMOEnemyDefinition enemyDefinition)
        {
            MMOCreatureVisualDefinition definition = AssetDatabase.LoadAssetAtPath<MMOCreatureVisualDefinition>(VisualDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOCreatureVisualDefinition>();
                AssetDatabase.CreateAsset(definition, VisualDefinitionPath);
            }

            definition.Configure(
                "Trog",
                "Trog Caster",
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath),
                null,
                animationSet,
                enemyDefinition,
                new[] { enemyDefinition },
                new[] { "Trog", "Trog Caster" },
                MMOCreatureBodyType.Biped,
                1.9f,
                0.48f,
                1.9f,
                new Vector3(0f, 0.95f, 0f),
                Vector3.zero,
                Vector3.zero,
                0f,
                0.3f,
                0f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void PlaceInStarterWorld(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || scene.rootCount == 0)
            {
                throw new InvalidOperationException("OrcishStarterValley did not open with valid root objects.");
            }

            GameObject existing = GameObject.Find(SpawnName);
            if (existing != null)
            {
                return;
            }

            GameObject parentObject = GameObject.Find(SpawnParentName);
            Transform parent = parentObject != null ? parentObject.transform : null;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate the Trog prefab in OrcishStarterValley.");
            }

            instance.name = SpawnName;
            Vector3 desiredPosition = new(-60f, 2.4f, -185f);
            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            {
                desiredPosition = hit.position;
            }

            instance.transform.SetPositionAndRotation(desiredPosition, Quaternion.Euler(0f, 180f, 0f));
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Failed to save OrcishStarterValley after placing the Trog.");
            }
        }

        private static void ValidateClipSet(params AnimationClip[] clips)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || AnimationUtility.GetCurveBindings(clip).Length == 0)
                {
                    throw new InvalidOperationException("A Trog animation clip is missing animation curves.");
                }
            }
        }

        private static bool IsUsableClip(AnimationClip clip)
        {
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = clip.name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            return normalized.IndexOf("pose", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void MoveIfPresent(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                throw new FileNotFoundException($"Required Trog source asset was not found at {sourcePath}.");
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new IOException($"Could not move {sourcePath} to {destinationPath}: {error}");
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
