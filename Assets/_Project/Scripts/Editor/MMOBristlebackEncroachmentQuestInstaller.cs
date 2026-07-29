using System;
using System.Collections.Generic;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using RPGClone.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOBristlebackEncroachmentQuestInstaller
    {
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";
        private const string QuestFolder = "Assets/_Project/Configs/Quests";
        private const string ItemFolder = "Assets/_Project/Configs/Items";
        private const string LootFolder = "Assets/_Project/Configs/Loot";
        private const string EnemyFolder = "Assets/_Project/Configs/Enemies";
        private const string CharacterFolder = "Assets/_Project/Configs/Characters";
        private const string AppearanceCatalogPath =
            "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset";
        private const string CommandCacheMaterialPath =
            "Assets/_Project/Generated/Materials/Bristleback_Battle_Plans_World.mat";

        private const string BeastwardenNpcId = "beastwarden_torak";
        private const string OutriderNpcId = "outrider_vesha";
        private const string CommandCacheWorldObjectId = "bristleback_command_cache";

        [MenuItem("Tools/RPG Clone/Quests/Install Bristleback Encroachment Quest Chain")]
        public static void Install()
        {
            RequiredContent required = LoadRequiredContent();
            if (!required.IsValid)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            MMOItemDefinition signalHorn = GetOrCreateQuestItem(
                "Bristleback Signal Horn",
                "bristleback_signal_horn",
                "A scarred war horn carved with the marching calls of the bristleback invaders.");
            MMOItemDefinition marchLedger = GetOrCreateQuestItem(
                "Bristleback March Ledger",
                "bristleback_march_ledger",
                "A hide-bound ledger mapping the invaders' routes, signals, and planned attacks.");

            MMOQuestDefinition wolvesAtThePalisade = GetOrCreateQuest(
                "bristleback_encroachment_01",
                "Wolves at the Palisade",
                3,
                BeastwardenNpcId,
                BeastwardenNpcId,
                "Gorrek's raids bloodied the bristlebacks, but their new invaders have driven the wolf packs toward our supply trail. Thin the pack before hunger turns the beasts on every runner leaving Razorcrag.",
                "The supply trail is still full of howls.",
                "The road can breathe again. Now we can deal with what drove them here.",
                "Kill 6 Wolves.",
                Objective(
                    "thin_displaced_wolves",
                    MMOQuestObjectiveType.KillCreature,
                    "Wolves slain",
                    6,
                    creature: required.Wolf),
                Reward(520, 65),
                required.CharmsOfChallenge);

            MMOQuestDefinition breakTheFirstWave = GetOrCreateQuest(
                "bristleback_encroachment_02",
                "Break the First Wave",
                3,
                BeastwardenNpcId,
                OutriderNpcId,
                "Those wolves were fleeing armored bristlebacks, not hunting on their own. Three invaders are probing the western approach. Break them, then report to Outrider Vesha at the forward watch.",
                "If Torak sent you, the first wave still stands between us.",
                "I watched them fall. Their armor marks them as a spearhead, not common raiders.",
                "Kill 3 Bristleback Invaders, then report to Outrider Vesha.",
                Objective(
                    "break_bristleback_first_wave",
                    MMOQuestObjectiveType.KillCreature,
                    "Bristleback Invaders slain",
                    3,
                    creature: required.Invader),
                Reward(680, 90),
                wolvesAtThePalisade);

            MMOQuestDefinition theHornThatCalls = GetOrCreateQuest(
                "bristleback_encroachment_03",
                "The Horn That Calls",
                4,
                OutriderNpcId,
                OutriderNpcId,
                "The spearhead moves to horn signals. Take a signal horn from one of their invaders. If we learn the call, we learn where the next attack gathers.",
                "No horn, no proof. Search the next invader you bring down.",
                "This carving is a marching call. Their commander left orders in the command cache below.",
                "Collect a Bristleback Signal Horn from a Bristleback Invader.",
                Objective(
                    "recover_signal_horn",
                    MMOQuestObjectiveType.CollectQuestItem,
                    "Bristleback Signal Horn",
                    1,
                    signalHorn,
                    creature: required.Invader),
                Reward(760, 120),
                breakTheFirstWave);

            MMOQuestDefinition ordersInTheAsh = GetOrCreateQuest(
                "bristleback_encroachment_04",
                "Orders in the Ash",
                5,
                OutriderNpcId,
                OutriderNpcId,
                "The horn points to a command cache inside their line. Recover the march ledger before the bristlebacks move it. We need names, routes, and timing, not another trophy.",
                "The ledger should be inside the bristleback command cache among the invaders.",
                "These routes join the raids Gorrek already broke to a larger push. Razorcrag will be ready before the next tusk crosses the ridge.",
                "Recover the Bristleback March Ledger from the command cache.",
                Objective(
                    "recover_march_ledger",
                    MMOQuestObjectiveType.CollectQuestItem,
                    "Bristleback March Ledger",
                    1,
                    marchLedger,
                    worldObjectId: CommandCacheWorldObjectId),
                Reward(1050, 175),
                theHornThatCalls);

            MMOQuestDefinition[] chain =
            {
                wolvesAtThePalisade,
                breakTheFirstWave,
                theHornThatCalls,
                ordersInTheAsh
            };

            UpsertItemCatalog(signalHorn, marchLedger);
            UpsertQuestCatalog(chain);
            ConfigureInvaderQuestLoot(required.BaseBristlebackLoot, required.Invader, signalHorn, theHornThatCalls);

            GameObject beastwarden = EnsureQuestNpc(
                "Quest Giver - Beastwarden",
                BeastwardenNpcId,
                "Beastwarden Torak",
                new Vector3(4f, 2f, -129f),
                245f,
                new[] { wolvesAtThePalisade, breakTheFirstWave },
                required.FriendlyNpcProfile);
            GameObject outrider = EnsureQuestNpc(
                "Quest Giver - Outrider",
                OutriderNpcId,
                "Outrider Vesha",
                new Vector3(-59f, 2f, -55f),
                35f,
                new[] { theHornThatCalls, ordersInTheAsh },
                required.FriendlyNpcProfile);
            EnsureCommandCache(marchLedger);

            if (!MMONpcVisualInstaller.InstallOnNpc(beastwarden)
                || !MMONpcVisualInstaller.InstallOnNpc(outrider))
            {
                Debug.LogError("Bristleback Encroachment could not install both NPC visual hierarchies.");
                return;
            }

            ConfigureNpcAppearance(
                BeastwardenNpcId,
                required.AppearanceCatalog,
                "hair_6",
                "hair_copper",
                "face_6",
                required.BeastwardenArmor);
            ConfigureNpcAppearance(
                OutriderNpcId,
                required.AppearanceCatalog,
                "hair_4",
                "hair_raven_black",
                "face_3",
                required.OutriderArmor);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "Installed Bristleback Encroachment: 4 chained quests, Beastwarden Torak, " +
                "Outrider Vesha, quest-gated invader loot, and the bristleback command cache.");
        }

        private static RequiredContent LoadRequiredContent()
        {
            RequiredContent content = new()
            {
                Wolf = LoadRequired<MMOEnemyDefinition>($"{EnemyFolder}/Wolf_Aggressive.asset"),
                Invader = LoadRequired<MMOEnemyDefinition>($"{EnemyFolder}/Bristleback_Invader_Aggressive.asset"),
                BaseBristlebackLoot = LoadRequired<MMOLootTable>($"{LootFolder}/Bristleback_Trash_Loot.asset"),
                CharmsOfChallenge = LoadRequired<MMOQuestDefinition>($"{QuestFolder}/Charms_of_Challenge.asset"),
                FriendlyNpcProfile = LoadRequired<MMOCharacterProfile>($"{CharacterFolder}/Friendly_NPC.asset"),
                AppearanceCatalog = LoadRequired<MMOCharacterAppearanceCatalog>(AppearanceCatalogPath),
                BeastwardenArmor = new ArmorSet(
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Wolfpelt Vest/EV_Wolfpelt_Vest.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Wolfpelt Grips/EV_Wolfpelt_Grips.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Wolfpelt Leggings/EV_Wolfpelt_Leggings.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Wolfpelt Treads/EV_Wolfpelt_Treads.asset")),
                OutriderArmor = new ArmorSet(
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Mail/Scaleguard Hauberk/EV_Scaleguard_Hauberk.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Scalehunter Grips/EV_Scalehunter_Grips.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Mail/Scaleguard Legguards/EV_Scaleguard_Legguards.asset"),
                    LoadRequired<MMOEquipmentVisualDefinition>(
                        "Assets/_Project/Equipment/Armor/Leather/Scalehunter Treads/EV_Scalehunter_Treads.asset"))
            };

            content.IsValid = content.Wolf != null
                && content.Invader != null
                && content.BaseBristlebackLoot != null
                && content.CharmsOfChallenge != null
                && content.FriendlyNpcProfile != null
                && content.AppearanceCatalog != null
                && content.BeastwardenArmor.IsValid
                && content.OutriderArmor.IsValid;
            return content;
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"Bristleback Encroachment requires {typeof(T).Name} at {path}.");
            }

            return asset;
        }

        private static MMOItemDefinition GetOrCreateQuestItem(
            string displayName,
            string itemId,
            string description)
        {
            string path = $"{ItemFolder}/{Sanitize(displayName)}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.Configure(
                itemId,
                displayName,
                description,
                MMOItemType.Quest,
                MMOItemQuality.Common,
                1,
                0);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static MMOQuestDefinition GetOrCreateQuest(
            string questId,
            string displayName,
            int level,
            string offeredByNpcId,
            string turnedInToNpcId,
            string offerText,
            string progressText,
            string completionText,
            string objectiveSummary,
            MMOQuestObjectiveDefinition objective,
            MMOQuestRewardDefinition reward,
            MMOQuestDefinition prerequisite)
        {
            string path = $"{QuestFolder}/{Sanitize(displayName)}.asset";
            MMOQuestDefinition quest = AssetDatabase.LoadAssetAtPath<MMOQuestDefinition>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<MMOQuestDefinition>();
                AssetDatabase.CreateAsset(quest, path);
            }

            quest.Configure(
                questId,
                displayName,
                level,
                level,
                offeredByNpcId,
                turnedInToNpcId,
                offerText,
                progressText,
                completionText,
                objectiveSummary,
                new[] { objective },
                reward,
                prerequisite != null ? new[] { prerequisite } : null);
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static MMOQuestObjectiveDefinition Objective(
            string objectiveId,
            MMOQuestObjectiveType objectiveType,
            string summary,
            int requiredCount,
            MMOItemDefinition requiredItem = null,
            MMOItemDefinition usableItem = null,
            MMOEnemyDefinition creature = null,
            string worldObjectId = "")
        {
            MMOQuestObjectiveDefinition objective = new();
            objective.Configure(
                objectiveId,
                objectiveType,
                summary,
                requiredCount,
                requiredItem,
                usableItem,
                creature,
                string.Empty,
                worldObjectId,
                string.Empty,
                true);
            return objective;
        }

        private static MMOQuestRewardDefinition Reward(int experience, int moneyCopper)
        {
            MMOQuestRewardDefinition reward = new();
            reward.Configure(experience, moneyCopper);
            return reward;
        }

        private static void UpsertItemCatalog(params MMOItemDefinition[] additions)
        {
            string path = $"{ItemFolder}/Starter_Item_Catalog.asset";
            MMOItemCatalog catalog = LoadRequired<MMOItemCatalog>(path);
            if (catalog == null)
            {
                return;
            }

            List<MMOItemDefinition> items = catalog.Items.Where(item => item != null).ToList();
            foreach (MMOItemDefinition addition in additions)
            {
                items.RemoveAll(item => item.ItemId == addition.ItemId);
                items.Add(addition);
            }

            catalog.Configure(items);
            EditorUtility.SetDirty(catalog);
        }

        private static void UpsertQuestCatalog(IEnumerable<MMOQuestDefinition> additions)
        {
            string path = $"{QuestFolder}/Starter_Quest_Catalog.asset";
            MMOQuestCatalog catalog = LoadRequired<MMOQuestCatalog>(path);
            if (catalog == null)
            {
                return;
            }

            List<MMOQuestDefinition> quests = catalog.Quests.Where(quest => quest != null).ToList();
            foreach (MMOQuestDefinition addition in additions)
            {
                quests.RemoveAll(quest => quest.QuestId == addition.QuestId);
                quests.Add(addition);
            }

            catalog.Configure(quests);
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureInvaderQuestLoot(
            MMOLootTable baseLoot,
            MMOEnemyDefinition invader,
            MMOItemDefinition signalHorn,
            MMOQuestDefinition hornQuest)
        {
            string path = $"{LootFolder}/Bristleback_Invader_Quest_Loot.asset";
            MMOLootTable invaderLoot = AssetDatabase.LoadAssetAtPath<MMOLootTable>(path);
            if (invaderLoot == null)
            {
                invaderLoot = ScriptableObject.CreateInstance<MMOLootTable>();
                AssetDatabase.CreateAsset(invaderLoot, path);
            }

            List<MMOLootTableEntry> entries = baseLoot.Entries
                .Where(entry => entry?.Item != null && entry.Item.ItemId != signalHorn.ItemId)
                .Select(entry => new MMOLootTableEntry(
                    entry.Item,
                    entry.DropChance,
                    entry.MinQuantity,
                    entry.MaxQuantity,
                    entry.RequiredQuest,
                    entry.OnlyDropWhileQuestNeedsItem))
                .ToList();
            entries.Add(new MMOLootTableEntry(signalHorn, 1f, 1, 1, hornQuest, true));
            invaderLoot.Configure(entries);
            EditorUtility.SetDirty(invaderLoot);

            SerializedObject serializedInvader = new(invader);
            SerializedProperty lootTable = serializedInvader.FindProperty("lootTable");
            if (lootTable == null)
            {
                Debug.LogError("Bristleback Invader definition does not expose the expected lootTable property.");
                return;
            }

            lootTable.objectReferenceValue = invaderLoot;
            serializedInvader.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(invader);
        }

        private static GameObject EnsureQuestNpc(
            string objectName,
            string npcId,
            string displayName,
            Vector3 position,
            float rotationY,
            MMOQuestDefinition[] offeredQuests,
            MMOCharacterProfile profile)
        {
            GameObject npc = GameObject.Find(objectName);
            if (npc == null)
            {
                npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = objectName;
            }

            npc.transform.SetParent(null, true);
            npc.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
            MMOGroundingUtility.SnapTransformToGround(npc.transform, npc.GetComponent<Collider>());
            npc.isStatic = false;

            MMOQuestNpc questNpc = npc.GetComponent<MMOQuestNpc>() ?? npc.AddComponent<MMOQuestNpc>();
            questNpc.Configure(npcId, displayName, offeredQuests);
            MMOStandardNpcIdentity identity =
                npc.GetComponent<MMOStandardNpcIdentity>() ?? npc.AddComponent<MMOStandardNpcIdentity>();
            identity.Configure(profile, displayName, MMONpcIdentityRole.QuestGiver, true);

            EditorUtility.SetDirty(npc);
            EditorUtility.SetDirty(questNpc);
            EditorUtility.SetDirty(identity);
            EditorUtility.SetDirty(identity.Identity);
            return npc;
        }

        private static void EnsureCommandCache(MMOItemDefinition marchLedger)
        {
            const string objectName = "Bristleback Command Cache";
            GameObject commandCache = GameObject.Find(objectName);
            if (commandCache == null)
            {
                commandCache = GameObject.CreatePrimitive(PrimitiveType.Cube);
                commandCache.name = objectName;
            }

            commandCache.transform.SetParent(null, true);
            commandCache.transform.position = new Vector3(-33f, 2f, -7f);
            commandCache.transform.localScale = new Vector3(1.2f, 0.45f, 0.85f);
            MMOGroundingUtility.SnapTransformToGround(
                commandCache.transform,
                commandCache.GetComponent<Collider>());
            commandCache.isStatic = false;

            MMOQuestWorldInteractable interactable =
                commandCache.GetComponent<MMOQuestWorldInteractable>()
                ?? commandCache.AddComponent<MMOQuestWorldInteractable>();
            interactable.Configure(
                CommandCacheWorldObjectId,
                "Bristleback Command Cache",
                marchLedger,
                1);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(CommandCacheMaterialPath);
            Renderer renderer = commandCache.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            EditorUtility.SetDirty(commandCache);
            EditorUtility.SetDirty(interactable);
        }

        private static void ConfigureNpcAppearance(
            string npcId,
            MMOCharacterAppearanceCatalog appearanceCatalog,
            string hairstyleId,
            string hairColorId,
            string faceId,
            ArmorSet armor)
        {
            MMOQuestNpc questNpc = UnityEngine.Object
                .FindObjectsByType<MMOQuestNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.NpcId == npcId);
            MMONpcVisualAuthoring authoring =
                questNpc != null ? questNpc.GetComponent<MMONpcVisualAuthoring>() : null;
            if (authoring == null)
            {
                Debug.LogError($"Could not configure the authored appearance for quest NPC '{npcId}'.");
                return;
            }

            authoring.Configure(
                appearanceCatalog,
                hairstyleId,
                hairColorId,
                faceId,
                armor.Chest,
                armor.Gloves,
                armor.Pants,
                armor.Boots);
            authoring.ApplySelections();
            EditorUtility.SetDirty(authoring);
        }

        private static string Sanitize(string value)
        {
            return value
                .Replace("'", string.Empty)
                .Replace(" ", "_")
                .Replace("-", "_");
        }

        private readonly struct ArmorSet
        {
            public ArmorSet(
                MMOEquipmentVisualDefinition chest,
                MMOEquipmentVisualDefinition gloves,
                MMOEquipmentVisualDefinition pants,
                MMOEquipmentVisualDefinition boots)
            {
                Chest = chest;
                Gloves = gloves;
                Pants = pants;
                Boots = boots;
            }

            public MMOEquipmentVisualDefinition Chest { get; }
            public MMOEquipmentVisualDefinition Gloves { get; }
            public MMOEquipmentVisualDefinition Pants { get; }
            public MMOEquipmentVisualDefinition Boots { get; }
            public bool IsValid => Chest != null && Gloves != null && Pants != null && Boots != null;
        }

        private sealed class RequiredContent
        {
            public MMOEnemyDefinition Wolf;
            public MMOEnemyDefinition Invader;
            public MMOLootTable BaseBristlebackLoot;
            public MMOQuestDefinition CharmsOfChallenge;
            public MMOCharacterProfile FriendlyNpcProfile;
            public MMOCharacterAppearanceCatalog AppearanceCatalog;
            public ArmorSet BeastwardenArmor;
            public ArmorSet OutriderArmor;
            public bool IsValid;
        }
    }
}
