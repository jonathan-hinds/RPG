using System;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOEnemyAuthoringInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string CharacterProfileFolder = ConfigFolder + "/Characters";
        private const string AbilityFolder = ConfigFolder + "/Abilities";
        private const string EnemyDefinitionFolder = ConfigFolder + "/Enemies";
        private const string ItemFolder = ConfigFolder + "/Items";
        private const string LootFolder = ConfigFolder + "/Loot";
        private const string EnemyPrefabFolder = RootFolder + "/Prefabs/Enemies";
        private const string EnemyPrefabPath = EnemyPrefabFolder + "/EnemyCapsule.prefab";
        private const string BristlebackPrefabPath = EnemyPrefabFolder + "/BristlebackEnemy.prefab";

        [MenuItem("Tools/RPG Clone/Enemies/Create Enemy Authoring Assets")]
        public static void CreateEnemyAuthoringAssets()
        {
            EnsureFolders();
            MMOAbilityDefinition autoAttack = GetOrCreateAutoAttackAbility();
            MMOLootTable bristlebackLootTable = GetOrCreateBristlebackLootTable();
            MMOCharacterProfile bristlebackProfile = GetOrCreateProfile(
                "Hostile Creature",
                "Bristleback",
                3,
                90,
                45,
                new Color(0.72f, 0.18f, 0.16f),
                MMOEntityFaction.Hostile,
                CreateStats(9, 11, 8, 4, 5, 8, 10, 0, 4f, 7f, 2.4f, 3f));
            MMOCharacterProfile dummyProfile = GetOrCreateProfile(
                "Training Dummy",
                "Training Dummy",
                1,
                180,
                0,
                new Color(0.34f, 0.22f, 0.13f),
                MMOEntityFaction.Hostile,
                CreateStats(18, 1, 1, 1, 1, 0, 0, 0, 1f, 1f, 2f, 3f));

            MMOEnemyDefinition aggressiveBristleback = GetOrCreateEnemyDefinition(
                "Bristleback_Aggressive",
                bristlebackProfile,
                MMOEnemyDisposition.Aggressive,
                autoAttack,
                14f,
                34f,
                true,
                6f,
                1.45f,
                4.25f,
                2.4f,
                55,
                bristlebackLootTable);
            GetOrCreateEnemyDefinition(
                "Bristleback_Docile",
                bristlebackProfile,
                MMOEnemyDisposition.Docile,
                autoAttack,
                0f,
                30f,
                true,
                5f,
                1.25f,
                4f,
                2.4f,
                45,
                bristlebackLootTable);
            GetOrCreateEnemyDefinition(
                "Training_Dummy_Docile",
                dummyProfile,
                MMOEnemyDisposition.Docile,
                autoAttack,
                0f,
                12f,
                false,
                0f,
                0.1f,
                0.1f,
                3f,
                0,
                null,
                1f,
                1f,
                1f,
                10f);

            CreateOrUpdateEnemyPrefab(aggressiveBristleback);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Enemy authoring assets created under Assets/_Project/Configs/Enemies and Assets/_Project/Prefabs/Enemies.");
        }

        [MenuItem("Tools/RPG Clone/Enemies/Convert Starter World Enemy Placeholders")]
        public static void ConvertStarterWorldEnemyPlaceholders()
        {
            CreateEnemyAuthoringAssets();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            GameObject bristlebackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BristlebackPrefabPath);
            if (bristlebackPrefab == null && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Bristleback/Animations/Bristleback.fbx") != null)
            {
                MMOBristlebackAnimationInstaller.InstallBristlebackAnimations();
                bristlebackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BristlebackPrefabPath);
            }

            MMOEnemyDefinition bristleback = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>($"{EnemyDefinitionFolder}/Bristleback_Aggressive.asset");
            MMOEnemyDefinition dummy = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>($"{EnemyDefinitionFolder}/Training_Dummy_Docile.asset");
            if (prefab == null || bristleback == null || dummy == null)
            {
                Debug.LogError("Enemy placeholder conversion failed because required enemy assets are missing.");
                return;
            }

            int convertedCount = 0;
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject.GetComponent<MMOEnemyController>() != null)
                {
                    continue;
                }

                if (!IsStarterWorldEnemyPlaceholder(sceneObject.name))
                {
                    continue;
                }

                MMOEnemyDefinition definition = sceneObject.name.StartsWith("Training Dummy", StringComparison.Ordinal)
                    ? dummy
                    : bristleback;
                GameObject selectedPrefab = definition == bristleback && bristlebackPrefab != null
                    ? bristlebackPrefab
                    : prefab;
                ReplaceWithEnemyPrefab(sceneObject, selectedPrefab, definition);
                convertedCount++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Converted {convertedCount} starter world enemy placeholders.");
        }

        [MenuItem("Tools/RPG Clone/Enemies/Rebuild Active Scene NavMesh")]
        public static void RebuildActiveSceneNavMesh()
        {
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include);
            if (surfaces.Length == 0)
            {
                Debug.LogWarning("No NavMeshSurface was found in the active scene.");
                return;
            }

            foreach (NavMeshSurface surface in surfaces)
            {
                surface.BuildNavMesh();
                EditorUtility.SetDirty(surface);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Rebuilt {surfaces.Length} NavMesh surface(s) in the active scene.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(CharacterProfileFolder);
            CreateFolderIfMissing(AbilityFolder);
            CreateFolderIfMissing(EnemyDefinitionFolder);
            CreateFolderIfMissing(ItemFolder);
            CreateFolderIfMissing(LootFolder);
            CreateFolderIfMissing(RootFolder + "/Prefabs");
            CreateFolderIfMissing(EnemyPrefabFolder);
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

        private static MMOEnemyDefinition GetOrCreateEnemyDefinition(
            string assetName,
            MMOCharacterProfile profile,
            MMOEnemyDisposition disposition,
            MMOAbilityDefinition autoAttack,
            float aggroRadius,
            float leashRadius,
            bool canRoam,
            float roamRadius,
            float walkSpeed,
            float chaseSpeed,
            float stoppingDistance,
            int experienceReward,
            MMOLootTable lootTable,
            float lootedCorpseDespawnSeconds = 2.5f,
            float emptyCorpseDespawnSeconds = 6f,
            float unlootedCorpseDespawnSeconds = 120f,
            float respawnSeconds = 30f)
        {
            string path = $"{EnemyDefinitionFolder}/{assetName}.asset";
            MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOEnemyDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(
                profile,
                disposition,
                autoAttack,
                new[] { autoAttack },
                aggroRadius,
                leashRadius,
                0.25f,
                canRoam,
                roamRadius,
                2.5f,
                6f,
                walkSpeed,
                chaseSpeed,
                stoppingDistance,
                experienceReward,
                lootTable,
                lootedCorpseDespawnSeconds,
                emptyCorpseDespawnSeconds,
                unlootedCorpseDespawnSeconds,
                respawnSeconds);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static MMOLootTable GetOrCreateBristlebackLootTable()
        {
            MMOItemDefinition crackedTusk = GetOrCreateTrashItem("Cracked Tusk", "cracked_tusk", "A chipped tusk with little use beyond vendor coin.", 20, 6);
            MMOItemDefinition mattedPelt = GetOrCreateTrashItem("Matted Pelt", "matted_pelt", "A rough hide matted with dust and burrs.", 10, 9);
            MMOItemDefinition bentQuill = GetOrCreateTrashItem("Bent Quill", "bent_quill", "A dull quill bent out of shape.", 20, 4);
            MMOItemDefinition crackedHoof = GetOrCreateTrashItem("Cracked Hoof", "cracked_hoof", "A splintered hoof fragment.", 10, 7);
            MMOItemDefinition greasySnout = GetOrCreateTrashItem("Greasy Snout", "greasy_snout", "An unpleasant trophy that only a vendor would want.", 5, 13);

            string path = $"{LootFolder}/Bristleback_Trash_Loot.asset";
            MMOLootTable lootTable = AssetDatabase.LoadAssetAtPath<MMOLootTable>(path);
            if (lootTable == null)
            {
                lootTable = ScriptableObject.CreateInstance<MMOLootTable>();
                AssetDatabase.CreateAsset(lootTable, path);
            }

            lootTable.Configure(new[]
            {
                new MMOLootTableEntry(crackedTusk, 0.42f, 1, 2),
                new MMOLootTableEntry(mattedPelt, 0.32f, 1, 1),
                new MMOLootTableEntry(bentQuill, 0.28f, 1, 3),
                new MMOLootTableEntry(crackedHoof, 0.18f, 1, 1),
                new MMOLootTableEntry(greasySnout, 0.08f, 1, 1)
            });
            EditorUtility.SetDirty(lootTable);
            return lootTable;
        }

        private static MMOItemDefinition GetOrCreateTrashItem(
            string displayName,
            string itemId,
            string description,
            int maxStackSize,
            int vendorValueCopper)
        {
            string path = $"{ItemFolder}/{Sanitize(displayName)}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.Configure(itemId, displayName, description, MMOItemType.Trash, MMOItemQuality.Poor, maxStackSize, vendorValueCopper);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void CreateOrUpdateEnemyPrefab(MMOEnemyDefinition defaultDefinition)
        {
            GameObject root = new("EnemyCapsule");
            root.isStatic = false;

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.36f;
            collider.height = 2f;
            collider.center = new Vector3(0f, 1f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.36f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.speed = 1.4f;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 2.4f;

            root.AddComponent<MMOCharacterIdentity>();
            root.AddComponent<MMOCombatant>();
            root.AddComponent<MMOAbilitySystem>();
            root.AddComponent<MMOCharacterRegeneration>();
            root.AddComponent<MMOLootableCorpse>();
            MMOAutoAttackController autoAttack = root.AddComponent<MMOAutoAttackController>();
            autoAttack.SetHandleRightClickInput(false);
            MMOEnemyController controller = root.AddComponent<MMOEnemyController>();
            controller.SetDefinition(defaultDefinition, true);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Capsule Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.72f, 1f, 0.72f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            Material hostileMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Generated/Materials/Hostile_Capsule_Red.mat");
            if (hostileMaterial != null)
            {
                visual.GetComponent<Renderer>().sharedMaterial = hostileMaterial;
            }

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void ReplaceWithEnemyPrefab(GameObject source, GameObject prefab, MMOEnemyDefinition definition)
        {
            Vector3 position = GetGroundedPosition(source);
            Quaternion rotation = Quaternion.Euler(0f, source.transform.eulerAngles.y, 0f);
            Transform parent = source.transform.parent;
            string newName = source.name.Replace(" Placeholder", string.Empty);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return;
            }

            Renderer sourceRenderer = source.GetComponent<Renderer>();
            Renderer instanceRenderer = instance.GetComponentInChildren<Renderer>();
            if (sourceRenderer != null && instanceRenderer != null)
            {
                instanceRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }

            instance.name = newName;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.isStatic = false;
            MMOEnemyController controller = instance.GetComponent<MMOEnemyController>();
            controller.SetDefinition(definition, true);
            EditorUtility.SetDirty(instance);
            Object.DestroyImmediate(source);
        }

        private static Vector3 GetGroundedPosition(GameObject source)
        {
            if (MMOGroundingUtility.TryGetGroundedPosition(source.transform, source.GetComponent<Collider>(), out Vector3 groundedPosition))
            {
                return groundedPosition;
            }

            return source.transform.position;
        }

        private static bool IsStarterWorldEnemyPlaceholder(string objectName)
        {
            return objectName.StartsWith("Bristleback Creature", StringComparison.Ordinal)
                || objectName.StartsWith("Ash Canyon Creature", StringComparison.Ordinal)
                || objectName.StartsWith("Training Dummy", StringComparison.Ordinal);
        }

        private static MMOCharacterProfile GetOrCreateProfile(string assetName, string displayName, int level, int health, int mana, Color portraitTint, MMOEntityFaction faction, MMOCharacterStats stats)
        {
            string path = $"{CharacterProfileFolder}/{Sanitize(assetName)}.asset";
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.Configure(displayName, level, health, mana, portraitTint, null, true, faction, stats);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOCharacterStats CreateStats(
            int stamina,
            int strength,
            int agility,
            int intellect,
            int spirit,
            int armor,
            int attackPower,
            int spellPower,
            float meleeMinDamage,
            float meleeMaxDamage,
            float meleeAttackSpeed,
            float meleeRange)
        {
            MMOCharacterStats stats = new();
            stats.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, meleeMinDamage, meleeMaxDamage, meleeAttackSpeed, meleeRange);
            return stats;
        }

        private static MMOAbilityDefinition GetOrCreateAutoAttackAbility()
        {
            string path = $"{AbilityFolder}/Auto_Attack.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            MMOAbilityEffectDefinition weaponDamage = new();
            weaponDamage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.WeaponDamage, MMODamageSchool.Physical, 0f, 1f);
            ability.Configure(
                "auto_attack",
                "Auto Attack",
                "Repeated weapon swings against a hostile target.",
                MMOAbilityTargetType.Hostile,
                true,
                true,
                3f,
                0f,
                0,
                new[] { weaponDamage });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
