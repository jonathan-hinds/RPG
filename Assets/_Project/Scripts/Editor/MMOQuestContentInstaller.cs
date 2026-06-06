using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.CharacterSelection;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using RPGClone.Trainers;
using RPGClone.Vendors;
using RPGClone.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class MMOQuestContentInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string QuestFolder = ConfigFolder + "/Quests";
        private const string ItemFolder = ConfigFolder + "/Items";
        private const string LootFolder = ConfigFolder + "/Loot";
        private const string EnemyFolder = ConfigFolder + "/Enemies";
        private const string CharacterFolder = ConfigFolder + "/Characters";
        private const string AbilityFolder = ConfigFolder + "/Abilities";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        [MenuItem("Tools/RPG Clone/Quests/Install Starter Quest Content")]
        public static void InstallStarterQuestContent()
        {
            EnsureFolders();
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            MMOEnemyAuthoringInstaller.CreateEnemyAuthoringAssets();

            StarterItems items = CreateItems();
            CreateItemCatalog(items.All);
            TrainerAbilities trainerAbilities = CreateTrainerAbilities();
            CreateAbilityCatalog(trainerAbilities.All);
            StarterEnemies enemies = CreateEnemies(items);
            StarterQuests quests = CreateQuests(items, enemies);
            MMOCharacterProfile friendlyNpcProfile = GetOrCreateFriendlyNpcProfile();
            CreateQuestCatalog(quests.All);
            UpdateLootTables(items, quests, enemies);
            InstallSceneObjects(items, quests, enemies, trainerAbilities, friendlyNpcProfile);
            MMOHudSceneInstaller.InstallIntoOpenScene(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Installed starter quest content: 4 chains, 12 quests, quest NPCs, world objectives, quest loot, currency rewards, and class-filtered gear rewards.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(QuestFolder);
            CreateFolderIfMissing(ItemFolder);
            CreateFolderIfMissing(LootFolder);
            CreateFolderIfMissing(EnemyFolder);
            CreateFolderIfMissing(CharacterFolder);
            CreateFolderIfMissing(AbilityFolder);
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

        private static StarterItems CreateItems()
        {
            StarterItems items = new()
            {
                MattedPelt = GetOrCreateBasicItem("Matted Pelt", "matted_pelt", "A rough hide matted with dust and burrs.", MMOItemType.Trash, MMOItemQuality.Poor, 10, 9),
                CrackedTusk = GetOrCreateBasicItem("Cracked Tusk", "cracked_tusk", "A chipped tusk with little use beyond vendor coin.", MMOItemType.Trash, MMOItemQuality.Poor, 20, 6),
                GreasySnout = GetOrCreateBasicItem("Greasy Snout", "greasy_snout", "An unpleasant trophy that only a vendor would want.", MMOItemType.Trash, MMOItemQuality.Poor, 5, 13),
                BristlebackCharm = GetOrCreateBasicItem("Bristleback War Charm", "bristleback_war_charm", "A crude charm carried by bristleback raiders.", MMOItemType.Quest, MMOItemQuality.Common, 10, 0),
                ValleyRuneShard = GetOrCreateBasicItem("Valley Rune Shard", "valley_rune_shard", "A warm sliver of carved valley stone.", MMOItemType.Quest, MMOItemQuality.Common, 10, 0),
                EmberCore = GetOrCreateBasicItem("Ember Core", "ember_core", "A coal-bright core pulled from an ash canyon beast.", MMOItemType.Quest, MMOItemQuality.Common, 10, 0),
                RepairHammer = GetOrCreateBasicItem("Razorcrag Repair Hammer", "razorcrag_repair_hammer", "A compact hammer used to shore up damaged palisade posts.", MMOItemType.Quest, MMOItemQuality.Common, 1, 0),
                WaterGourd = GetOrCreateBasicItem("Cooled Water Gourd", "cooled_water_gourd", "A hide-bound gourd filled with cool spring water.", MMOItemType.Quest, MMOItemQuality.Common, 1, 0)
            };

            items.RazorcragJerky = GetOrCreateConsumable("Razorcrag Jerky", "razorcrag_jerky", "Salted trail meat from the camp stores.", MMOConsumableType.Food, 250, 0, 10f, 20, 8);
            items.SpringwaterFlask = GetOrCreateConsumable("Springwater Flask", "springwater_flask", "A stoppered flask of clean valley springwater.", MMOConsumableType.Water, 0, 250, 10f, 20, 8);

            items.BootRewards = CreateGearSet("Trailbreaker's Boots", MMOEquipmentSlotType.Feet, 2);
            items.GloveRewards = CreateGearSet("Razorcrag Grips", MMOEquipmentSlotType.Hands, 2);
            items.ChestRewards = CreateGearSet("Ashguard Vest", MMOEquipmentSlotType.Chest, 4);
            items.LegRewards = CreateGearSet("Valley Watch Leggings", MMOEquipmentSlotType.Legs, 3);
            return items;
        }

        private static MMOItemDefinition[] CreateGearSet(string baseName, MMOEquipmentSlotType slot, int armorBase)
        {
            return new[]
            {
                GetOrCreateGear($"{baseName} (Cloth)", $"{Sanitize(baseName).ToLowerInvariant()}_cloth", $"Light cloth {MMOUiFactoryName(slot)} for new spellcasters.", slot, MMOArmorWeight.Cloth, armorBase),
                GetOrCreateGear($"{baseName} (Leather)", $"{Sanitize(baseName).ToLowerInvariant()}_leather", $"Supple leather {MMOUiFactoryName(slot)} for scouts and shamans.", slot, MMOArmorWeight.Leather, armorBase + 1),
                GetOrCreateGear($"{baseName} (Mail)", $"{Sanitize(baseName).ToLowerInvariant()}_mail", $"Linked mail {MMOUiFactoryName(slot)} for frontline warriors.", slot, MMOArmorWeight.Mail, armorBase + 2)
            };
        }

        private static MMOItemDefinition GetOrCreateBasicItem(string displayName, string itemId, string description, MMOItemType type, MMOItemQuality quality, int maxStack, int vendorValueCopper)
        {
            string path = $"{ItemFolder}/{Sanitize(displayName)}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.Configure(itemId, displayName, description, type, quality, maxStack, vendorValueCopper);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static MMOItemDefinition GetOrCreateConsumable(
            string displayName,
            string itemId,
            string description,
            MMOConsumableType consumableType,
            int restoreHealth,
            int restoreMana,
            float durationSeconds,
            int maxStack,
            int vendorValueCopper)
        {
            string path = $"{ItemFolder}/{Sanitize(displayName)}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.ConfigureConsumable(itemId, displayName, description, MMOItemQuality.Common, maxStack, vendorValueCopper, consumableType, restoreHealth, restoreMana, durationSeconds);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static MMOItemDefinition GetOrCreateGear(string displayName, string itemId, string description, MMOEquipmentSlotType slot, MMOArmorWeight armorWeight, int armor)
        {
            string path = $"{ItemFolder}/{Sanitize(displayName)}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.ConfigureEquipment(itemId, displayName, description, MMOItemQuality.Common, slot, armorWeight, CreateStats(0, 0, 0, 0, 0, armor, 0, 0), Math.Max(1, armor * 4));
            EditorUtility.SetDirty(item);
            return item;
        }

        private static StarterEnemies CreateEnemies(StarterItems items)
        {
            MMOAbilityDefinition autoAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{AbilityFolder}/Auto_Attack.asset");
            MMOCharacterProfile bristlebackProfile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>($"{CharacterFolder}/Hostile_Creature.asset");
            MMOLootTable bristlebackLoot = AssetDatabase.LoadAssetAtPath<MMOLootTable>($"{LootFolder}/Bristleback_Trash_Loot.asset");
            MMOLootTable ashLoot = GetOrCreateLootTable("Ash_Canyon_Quest_Loot");

            MMOEnemyDefinition bristleback = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>($"{EnemyFolder}/Bristleback_Aggressive.asset");
            MMOEnemyDefinition ash = GetOrCreateEnemyDefinition("Ash_Canyon_Aggressive", bristlebackProfile, autoAttack, ashLoot);

            return new StarterEnemies
            {
                Bristleback = bristleback,
                AshCanyon = ash,
                BristlebackLoot = bristlebackLoot,
                AshCanyonLoot = ashLoot
            };
        }

        private static MMOEnemyDefinition GetOrCreateEnemyDefinition(string assetName, MMOCharacterProfile profile, MMOAbilityDefinition autoAttack, MMOLootTable lootTable)
        {
            string path = $"{EnemyFolder}/{assetName}.asset";
            MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOEnemyDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(profile, MMOEnemyDisposition.Aggressive, autoAttack, new[] { autoAttack }, 15f, 36f, 0.25f, true, 7f, 2.5f, 5.5f, 1.5f, 4.4f, 2.4f, 65, lootTable);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static MMOCharacterProfile GetOrCreateFriendlyNpcProfile()
        {
            string path = $"{CharacterFolder}/Friendly_NPC.asset";
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.Configure("Razorcrag NPC", 4, 120, 90, new Color(0.95f, 0.66f, 0.22f), null, true, MMOEntityFaction.Friendly, CreateStats(12, 9, 12, 11, 10, 12, 8, 4));
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static StarterQuests CreateQuests(StarterItems items, StarterEnemies enemies)
        {
            StarterQuests quests = new();

            quests.A1 = GetOrCreateQuest(
                "razorcrag_supplies_01",
                "Scraps for the Stores",
                "scout_gorrek",
                "scout_gorrek",
                "The camp eats through supplies faster than we can count them. The bristlebacks tear up good hides and leave them to rot. Bring me pelts before the quartermaster starts using tent cloth for bandages.",
                "The stores still look thin.",
                "Good. Rough work, but it keeps the camp moving.",
                "Collect 5 Matted Pelts.",
                new[] { Objective("pelts", MMOQuestObjectiveType.CollectItem, "Matted Pelts", 5, items.MattedPelt) },
                Reward(60, 35));

            quests.A2 = GetOrCreateQuest(
                "razorcrag_supplies_02",
                "Bristleback Pressure",
                "scout_gorrek",
                "scout_gorrek",
                "Those raiders are testing the edge of our camp. Push them back before they learn where our sentries are weakest.",
                "They are still too bold.",
                "That should buy our watch some breathing room.",
                "Kill 6 Bristleback creatures.",
                new[] { Objective("kill_bristlebacks", MMOQuestObjectiveType.KillCreature, "Bristlebacks slain", 6, null, null, enemies.Bristleback) },
                Reward(75, 45),
                quests.A1);

            quests.A3 = GetOrCreateQuest(
                "razorcrag_supplies_03",
                "Charms of Challenge",
                "scout_gorrek",
                "scout_gorrek",
                "The bold ones carry war charms. Bring me a few and the others will know we are not prey.",
                "The charms matter more than they look.",
                "You have a hunter's hands. Choose something from our stores.",
                "Collect 3 Bristleback War Charms.",
                new[] { Objective("war_charms", MMOQuestObjectiveType.CollectQuestItem, "War Charms", 3, items.BristlebackCharm, null, enemies.Bristleback) },
                Reward(95, 70, items.BootRewards),
                quests.A2);

            quests.B1 = GetOrCreateQuest(
                "ancestral_watch_01",
                "Stones That Remember",
                "seer_mahka",
                "seer_mahka",
                "The valley remembers old oaths. Its rune stones are broken and scattered. Gather the shards before the ash wind buries them.",
                "Listen for the stone hum.",
                "The old marks still have a voice.",
                "Collect 4 Valley Rune Shards.",
                new[] { Objective("rune_shards", MMOQuestObjectiveType.CollectQuestItem, "Rune Shards", 4, items.ValleyRuneShard, null, null, "", "valley_rune_shard") },
                Reward(60, 35));

            quests.B2 = GetOrCreateQuest(
                "ancestral_watch_02",
                "Hammer the Line",
                "seer_mahka",
                "seer_mahka",
                "A memory is worth little if the camp falls around it. Take this hammer and set the fence posts firm again.",
                "Use the hammer from your pack, then work the damaged posts.",
                "The line will hold a little longer.",
                "Use the Razorcrag Repair Hammer on 5 damaged fence posts.",
                new[] { Objective("fence_posts", MMOQuestObjectiveType.UseItemOnWorldObject, "Fence posts repaired", 5, null, items.RepairHammer, null, "", "damaged_fence_post") },
                Reward(75, 45),
                quests.B1,
                new[] { new MMOItemStack(items.RepairHammer, 1) });

            quests.B3 = GetOrCreateQuest(
                "ancestral_watch_03",
                "Words for the Warchief",
                "seer_mahka",
                "warchief_korga",
                "Tell Korga the valley stones still answer us. He trusts scouts and steel. Today he should trust memory too.",
                "The message is for the warchief.",
                "Mahka hears more than most. I will not ignore this.",
                "Speak to Warchief Korga.",
                new[] { Objective("speak_warchief", MMOQuestObjectiveType.SpeakToNpc, "Warchief Korga spoken to", 1, null, null, null, "", "", "warchief_korga") },
                Reward(95, 70, items.GloveRewards),
                quests.B2);

            quests.C1 = GetOrCreateQuest(
                "ember_watch_01",
                "Find the Canyon Scout",
                "warchief_korga",
                "canyon_scout_rakka",
                "If Mahka is right, the ash canyon is waking. Find Rakka at the canyon edge and hear what she has seen.",
                "Rakka watches the canyon trail.",
                "Korga sent you? Then listen closely.",
                "Speak to Canyon Scout Rakka.",
                new[] { Objective("speak_rakka", MMOQuestObjectiveType.SpeakToNpc, "Canyon Scout Rakka spoken to", 1, null, null, null, "", "", "canyon_scout_rakka") },
                Reward(80, 55),
                quests.A3,
                null,
                quests.B3);

            quests.C2 = GetOrCreateQuest(
                "ember_watch_02",
                "Cores in the Ash",
                "canyon_scout_rakka",
                "canyon_scout_rakka",
                "The canyon beasts burn from inside. Bring me their ember cores and I can tell whether this is sickness or sorcery.",
                "The cores cool quickly. Keep moving.",
                "These embers are wrong. They pulse like a drumbeat.",
                "Collect 3 Ember Cores from ash canyon creatures.",
                new[] { Objective("ember_cores", MMOQuestObjectiveType.CollectQuestItem, "Ember Cores", 3, items.EmberCore, null, enemies.AshCanyon) },
                Reward(105, 80),
                quests.C1);

            quests.C3 = GetOrCreateQuest(
                "ember_watch_03",
                "Cool the Vents",
                "canyon_scout_rakka",
                "canyon_scout_rakka",
                "The vents are feeding the beasts. Take this gourd and smother the worst of them before the canyon spills fire down the trail.",
                "Use the gourd from your pack at the smoking vents.",
                "The canyon still burns, but it no longer screams.",
                "Use the Cooled Water Gourd on 3 smoldering vents.",
                new[] { Objective("cool_vents", MMOQuestObjectiveType.UseItemOnWorldObject, "Smoldering vents cooled", 3, null, items.WaterGourd, null, "", "smoldering_vent") },
                Reward(120, 95, items.ChestRewards),
                quests.C2,
                new[] { new MMOItemStack(items.WaterGourd, 1) });

            quests.D1 = GetOrCreateQuest(
                "warchiefs_mandate_01",
                "Break the Ash Line",
                "warchief_korga",
                "warchief_korga",
                "Now we act. Cut down the canyon creatures nearest the road and show the camp that the valley still belongs to us.",
                "The road is not clear yet.",
                "The watch reports the path is quieter.",
                "Kill 4 Ash Canyon creatures.",
                new[] { Objective("kill_ash", MMOQuestObjectiveType.KillCreature, "Ash creatures slain", 4, null, null, enemies.AshCanyon) },
                Reward(95, 80),
                quests.A3,
                null,
                quests.B3);

            quests.D2 = GetOrCreateQuest(
                "warchiefs_mandate_02",
                "Trophies for Doubters",
                "warchief_korga",
                "warchief_korga",
                "Some in camp only believe what they can hold. Bring cracked tusks from the raiders. Let the doubters count them.",
                "A leader needs proof as much as courage.",
                "Good. Doubt shrinks when placed beside trophies.",
                "Collect 3 Cracked Tusks.",
                new[] { Objective("cracked_tusks", MMOQuestObjectiveType.CollectItem, "Cracked Tusks", 3, items.CrackedTusk) },
                Reward(105, 85),
                quests.D1);

            quests.D3 = GetOrCreateQuest(
                "warchiefs_mandate_03",
                "Report to the Warchief",
                "warchief_korga",
                "warchief_korga",
                "Stand before me and speak plainly. Tell me what the valley needs next.",
                "I am listening.",
                "You have done enough for your first day. Take your reward and keep your weapon close.",
                "Speak to Warchief Korga.",
                new[] { Objective("final_report", MMOQuestObjectiveType.SpeakToNpc, "Report delivered", 1, null, null, null, "", "", "warchief_korga") },
                Reward(135, 120, items.LegRewards),
                quests.D2);

            return quests;
        }

        private static MMOQuestObjectiveDefinition Objective(
            string id,
            MMOQuestObjectiveType type,
            string summary,
            int count,
            MMOItemDefinition item = null,
            MMOItemDefinition usableItem = null,
            MMOEnemyDefinition creature = null,
            string creatureId = "",
            string worldObjectId = "",
            string npcId = "")
        {
            MMOQuestObjectiveDefinition objective = new();
            objective.Configure(id, type, summary, count, item, usableItem, creature, creatureId, worldObjectId, npcId, true);
            return objective;
        }

        private static MMOQuestRewardDefinition Reward(int xp, int copper, MMOItemDefinition[] choices = null)
        {
            MMOQuestRewardDefinition reward = new();
            reward.Configure(xp, copper, null, choices);
            return reward;
        }

        private static MMOQuestDefinition GetOrCreateQuest(
            string questId,
            string title,
            string offerNpc,
            string turnInNpc,
            string offerText,
            string progressText,
            string completionText,
            string summary,
            MMOQuestObjectiveDefinition[] objectives,
            MMOQuestRewardDefinition reward,
            MMOQuestDefinition prerequisite = null,
            MMOItemStack[] startItems = null,
            MMOQuestDefinition additionalPrerequisite = null)
        {
            string path = $"{QuestFolder}/{Sanitize(title)}.asset";
            MMOQuestDefinition quest = AssetDatabase.LoadAssetAtPath<MMOQuestDefinition>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<MMOQuestDefinition>();
                AssetDatabase.CreateAsset(quest, path);
            }

            List<MMOQuestDefinition> prerequisites = new();
            if (prerequisite != null)
            {
                prerequisites.Add(prerequisite);
            }

            if (additionalPrerequisite != null)
            {
                prerequisites.Add(additionalPrerequisite);
            }

            quest.Configure(questId, title, 1, offerNpc, turnInNpc, offerText, progressText, completionText, summary, objectives, reward, prerequisites, startItems);
            EditorUtility.SetDirty(quest);
            return quest;
        }

        private static void CreateQuestCatalog(MMOQuestDefinition[] quests)
        {
            string path = $"{QuestFolder}/Starter_Quest_Catalog.asset";
            MMOQuestCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOQuestCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOQuestCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.Configure(quests);
            EditorUtility.SetDirty(catalog);
        }

        private static void CreateItemCatalog(MMOItemDefinition[] items)
        {
            string path = $"{ItemFolder}/Starter_Item_Catalog.asset";
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOItemCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.Configure(items);
            EditorUtility.SetDirty(catalog);
        }

        private static TrainerAbilities CreateTrainerAbilities()
        {
            return new TrainerAbilities
            {
                Berzerkitis = GetOrCreateBuffAbility("Warrior_Berzerkitis", "warrior_berzerkitis", "Berzerkitis", "Increases attack speed by 50%.", 60f, 15f, 0, 1f, 1.5f, 1f, 1f, 0f),
                Charge = GetOrCreateChargeAbility("Warrior_Charge", "warrior_charge", "Charge", "Charges a hostile target if a valid path exists, then strikes with physical force.", 25f, 15f, 18f, 2.5f, MMOAbilityAmountSource.AttackPower, MMODamageSchool.Physical, 10f, 0.35f),
                MageArmor = GetOrCreateBuffAbility("Mage_Mage_Armor", "mage_mage_armor", "Mage Armor", "Increases out of combat mana regeneration by 50%.", 0f, 1800f, 0, 1f, 1f, 1f, 1.5f, 0f),
                FireBlast = GetOrCreateDamageAbility("Mage_Fire_Blast", "mage_fire_blast", "Fire Blast", "Blasts a hostile target with instant fire damage.", MMOAbilityTargetType.Hostile, 20f, 8f, 12, 0f, false, false, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Fire, 18f, 0.45f),
                WaterShield = GetOrCreateBuffAbility("Shaman_Water_Shield", "shaman_water_shield", "Water Shield", "Absorbs 20% of incoming damage and restores that amount as mana.", 0f, 600f, 0, 1f, 1f, 1f, 1f, 0.2f),
                LightningBolt = GetOrCreateDamageAbility("Shaman_Lightning_Bolt", "shaman_lightning_bolt", "Lightning Bolt", "Calls down nature damage on a hostile target.", MMOAbilityTargetType.Hostile, 30f, 0f, 14, 2f, true, false, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Nature, 22f, 0.75f)
            };
        }

        private static MMOAbilityDefinition GetOrCreateDamageAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            MMOAbilityTargetType targetType,
            float range,
            float cooldown,
            int manaCost,
            float castTime,
            bool interruptOnMovement,
            bool fallbackSelf,
            MMOAbilityAmountSource amountSource,
            MMODamageSchool school,
            float flatAmount,
            float coefficient)
        {
            string path = $"{AbilityFolder}/{assetName}.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            MMOAbilityEffectDefinition effect = new();
            effect.Configure(MMOAbilityEffectType.Damage, amountSource, school, flatAmount, coefficient);
            ability.Configure(abilityId, displayName, description, targetType, false, false, range, cooldown, manaCost, castTime, interruptOnMovement, fallbackSelf, new[] { effect });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateChargeAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float range,
            float cooldown,
            float chargeSpeed,
            float stopDistance,
            MMOAbilityAmountSource amountSource,
            MMODamageSchool school,
            float flatAmount,
            float coefficient)
        {
            string path = $"{AbilityFolder}/{assetName}.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            MMOAbilityEffectDefinition effect = new();
            effect.ConfigureCharge(chargeSpeed, stopDistance, amountSource, school, flatAmount, coefficient);
            ability.Configure(abilityId, displayName, description, MMOAbilityTargetType.Hostile, false, false, range, cooldown, 0, 0f, false, false, new[] { effect });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateBuffAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float cooldown,
            float duration,
            int attackPowerBonus,
            float attackPowerMultiplier,
            float attackSpeedMultiplier,
            float healthRegenMultiplier,
            float manaRegenMultiplier,
            float damageTakenAsManaPercent)
        {
            string path = $"{AbilityFolder}/{assetName}.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            MMOAbilityEffectDefinition effect = new();
            effect.ConfigureTemporaryStatModifier(duration, attackPowerBonus, attackPowerMultiplier, attackSpeedMultiplier, healthRegenMultiplier, manaRegenMultiplier, damageTakenAsManaPercent);
            ability.Configure(abilityId, displayName, description, MMOAbilityTargetType.Self, false, false, 0f, cooldown, 0, 0f, false, false, new[] { effect });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static void CreateAbilityCatalog(MMOAbilityDefinition[] trainerAbilities)
        {
            string path = $"{AbilityFolder}/Starter_Ability_Catalog.asset";
            MMOAbilityCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOAbilityCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOAbilityCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            List<MMOAbilityDefinition> abilities = new();
            foreach (string assetName in new[]
            {
                "Auto_Attack",
                "Orc_Blood_Fury",
                "Troll_Regeneration",
                "Warrior_Bash",
                "Mage_Fireball",
                "Shaman_Healing_Beam"
            })
            {
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{AbilityFolder}/{assetName}.asset");
                if (ability != null)
                {
                    abilities.Add(ability);
                }
            }

            foreach (MMOAbilityDefinition ability in trainerAbilities)
            {
                if (ability != null && !abilities.Contains(ability))
                {
                    abilities.Add(ability);
                }
            }

            catalog.Configure(abilities);
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdateLootTables(StarterItems items, StarterQuests quests, StarterEnemies enemies)
        {
            enemies.BristlebackLoot.Configure(new[]
            {
                new MMOLootTableEntry(items.CrackedTusk, 0.42f, 1, 2),
                new MMOLootTableEntry(items.MattedPelt, 0.32f, 1, 1),
                new MMOLootTableEntry(items.GreasySnout, 0.08f, 1, 1),
                new MMOLootTableEntry(items.BristlebackCharm, 0.75f, 1, 1, quests.A3, true)
            });

            enemies.AshCanyonLoot.Configure(new[]
            {
                new MMOLootTableEntry(items.CrackedTusk, 0.18f, 1, 1),
                new MMOLootTableEntry(items.EmberCore, 0.85f, 1, 1, quests.C2, true)
            });

            EditorUtility.SetDirty(enemies.BristlebackLoot);
            EditorUtility.SetDirty(enemies.AshCanyonLoot);
        }

        private static void InstallSceneObjects(StarterItems items, StarterQuests quests, StarterEnemies enemies, TrainerAbilities trainerAbilities, MMOCharacterProfile friendlyNpcProfile)
        {
            ConvertEnemyPlaceholders(enemies);

            EnsureQuestNpc("Quest Giver - Warchief", "warchief_korga", "Warchief Korga", new Vector3(-35f, 2f, -124f), new[] { quests.C1, quests.D1, quests.D2, quests.D3 }, friendlyNpcProfile);
            EnsureQuestNpc("Quest Giver - Scout", "scout_gorrek", "Scout Gorrek", new Vector3(-18f, 2f, -111f), new[] { quests.A1, quests.A2, quests.A3 }, friendlyNpcProfile);
            EnsureQuestNpc("Quest Giver - Seer", "seer_mahka", "Seer Mahka", new Vector3(-51f, 2f, -105f), new[] { quests.B1, quests.B2, quests.B3 }, friendlyNpcProfile);
            EnsureQuestNpc("Quest Giver - Canyon Scout", "canyon_scout_rakka", "Canyon Scout Rakka", new Vector3(128f, 2f, 55f), new[] { quests.C2, quests.C3 }, friendlyNpcProfile);
            EnsureVendorNpc("Vendor - Quartermaster", "quartermaster_grakka", "Quartermaster Grakka", "General Goods Merchant", new Vector3(-13f, 2f, -128f), new[]
            {
                new MMOVendorStockEntry(items.RazorcragJerky, 1, 16),
                new MMOVendorStockEntry(items.SpringwaterFlask, 1, 16)
            }, friendlyNpcProfile);
            EnsureTrainerNpc("Trainer - Warrior", "trainer_warrior_gorvak", "Gorvak Steelarm", "Warrior Trainer", MMOPlayableClass.Warrior, new Vector3(-14f, 2f, -102f), new[]
            {
                new MMOTrainerOfferEntry(trainerAbilities.Berzerkitis, MMOPlayableClass.Warrior, 3, 75),
                new MMOTrainerOfferEntry(trainerAbilities.Charge, MMOPlayableClass.Warrior, 3, 75)
            }, friendlyNpcProfile);
            EnsureTrainerNpc("Trainer - Mage", "trainer_mage_zunari", "Zunari Embermind", "Mage Trainer", MMOPlayableClass.Mage, new Vector3(-57f, 2f, -126f), new[]
            {
                new MMOTrainerOfferEntry(trainerAbilities.MageArmor, MMOPlayableClass.Mage, 3, 75),
                new MMOTrainerOfferEntry(trainerAbilities.FireBlast, MMOPlayableClass.Mage, 3, 75)
            }, friendlyNpcProfile);
            EnsureTrainerNpc("Trainer - Shaman", "trainer_shaman_mahru", "Mahru Raincaller", "Shaman Trainer", MMOPlayableClass.Shaman, new Vector3(-46f, 2f, -99f), new[]
            {
                new MMOTrainerOfferEntry(trainerAbilities.WaterShield, MMOPlayableClass.Shaman, 3, 75),
                new MMOTrainerOfferEntry(trainerAbilities.LightningBolt, MMOPlayableClass.Shaman, 3, 75)
            }, friendlyNpcProfile);
            Physics.SyncTransforms();

            for (int i = 0; i < 4; i++)
            {
                Vector3 position = Grounded(new Vector3(-88f + i * 8f, 2f, -126f + i * 3f));
                EnsureWorldInteractable($"Valley Rune Shard {i + 1}", "valley_rune_shard", "Valley Rune Shard", PrimitiveType.Sphere, position, new Vector3(0.55f, 0.55f, 0.55f), items.ValleyRuneShard, 1);
            }

            for (int i = 0; i < 5; i++)
            {
                Vector3 position = Grounded(new Vector3(-49f + i * 5f, 2f, -88f + i * 2f));
                EnsureWorldInteractable($"Damaged Fence Post {i + 1}", "damaged_fence_post", "Damaged Fence Post", PrimitiveType.Cylinder, position, new Vector3(0.28f, 1.4f, 0.28f), null, 1);
            }

            for (int i = 0; i < 3; i++)
            {
                Vector3 position = Grounded(new Vector3(138f + i * 8f, 2f, 72f + i * 6f));
                EnsureWorldInteractable($"Smoldering Vent {i + 1}", "smoldering_vent", "Smoldering Vent", PrimitiveType.Cylinder, position, new Vector3(0.8f, 0.25f, 0.8f), null, 1);
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                MMOQuestLog questLog = player.GetComponent<MMOQuestLog>() ?? player.AddComponent<MMOQuestLog>();
                questLog.Configure(AssetDatabase.LoadAssetAtPath<MMOQuestCatalog>($"{QuestFolder}/Starter_Quest_Catalog.asset"));
                if (player.GetComponent<MMOCurrencyWallet>() == null)
                {
                    player.AddComponent<MMOCurrencyWallet>();
                }

                if (player.GetComponent<MMOConsumableEffectController>() == null)
                {
                    player.AddComponent<MMOConsumableEffectController>();
                }

                MMOCharacterPersistenceAgent persistence = player.GetComponent<MMOCharacterPersistenceAgent>();
                if (persistence != null)
                {
                    persistence.SetItemCatalog(AssetDatabase.LoadAssetAtPath<MMOItemCatalog>($"{ItemFolder}/Starter_Item_Catalog.asset"));
                    persistence.SetAbilityCatalog(AssetDatabase.LoadAssetAtPath<MMOAbilityCatalog>($"{AbilityFolder}/Starter_Ability_Catalog.asset"));
                    EditorUtility.SetDirty(persistence);
                }

                EditorUtility.SetDirty(player);
            }
        }

        private static void ConvertEnemyPlaceholders(StarterEnemies enemies)
        {
            MMOEnemyAuthoringInstaller.ConvertStarterWorldEnemyPlaceholders();
            foreach (MMOEnemyController controller in Object.FindObjectsByType<MMOEnemyController>(FindObjectsInactive.Include))
            {
                if (controller.name.StartsWith("Ash Canyon Creature", StringComparison.Ordinal))
                {
                    controller.SetDefinition(enemies.AshCanyon, true);
                }
                else if (controller.name.StartsWith("Bristleback Creature", StringComparison.Ordinal))
                {
                    controller.SetDefinition(enemies.Bristleback, true);
                }

                EditorUtility.SetDirty(controller);
            }
        }

        private static void EnsureQuestNpc(string objectName, string npcId, string displayName, Vector3 fallbackPosition, MMOQuestDefinition[] offeredQuests, MMOCharacterProfile profile)
        {
            GameObject npc = GameObject.Find(objectName) ?? GameObject.Find(objectName + " Placeholder");
            if (npc == null)
            {
                npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = objectName;
            }
            else
            {
                npc.name = objectName;
            }

            npc.transform.SetParent(null, true);
            npc.transform.position = Grounded(fallbackPosition);
            MMOGroundingUtility.SnapTransformToGround(npc.transform, npc.GetComponent<Collider>());
            npc.isStatic = false;
            MMOQuestNpc questNpc = npc.GetComponent<MMOQuestNpc>() ?? npc.AddComponent<MMOQuestNpc>();
            questNpc.Configure(npcId, displayName, offeredQuests);
            MMOStandardNpcIdentity standardIdentity = npc.GetComponent<MMOStandardNpcIdentity>() ?? npc.AddComponent<MMOStandardNpcIdentity>();
            standardIdentity.Configure(profile, displayName, MMONpcIdentityRole.QuestGiver, true);

            EditorUtility.SetDirty(npc);
            EditorUtility.SetDirty(questNpc);
            EditorUtility.SetDirty(standardIdentity);
            EditorUtility.SetDirty(standardIdentity.Identity);
        }

        private static void EnsureVendorNpc(string objectName, string vendorId, string displayName, Vector3 fallbackPosition, MMOVendorStockEntry[] stock, MMOCharacterProfile profile)
        {
            EnsureVendorNpc(objectName, vendorId, displayName, "General Goods Merchant", fallbackPosition, stock, profile);
        }

        private static void EnsureVendorNpc(string objectName, string vendorId, string displayName, string title, Vector3 fallbackPosition, MMOVendorStockEntry[] stock, MMOCharacterProfile profile)
        {
            GameObject vendor = GameObject.Find(objectName);
            if (vendor == null)
            {
                vendor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                vendor.name = objectName;
            }

            vendor.transform.SetParent(null, true);
            vendor.transform.position = Grounded(fallbackPosition);
            MMOGroundingUtility.SnapTransformToGround(vendor.transform, vendor.GetComponent<Collider>());
            vendor.isStatic = false;
            MMOVendorNpc vendorNpc = vendor.GetComponent<MMOVendorNpc>() ?? vendor.AddComponent<MMOVendorNpc>();
            vendorNpc.Configure(vendorId, displayName, title, stock, true);
            MMOStandardNpcIdentity standardIdentity = vendor.GetComponent<MMOStandardNpcIdentity>() ?? vendor.AddComponent<MMOStandardNpcIdentity>();
            standardIdentity.Configure(profile, displayName, title, MMONpcIdentityRole.Vendor, true);

            EditorUtility.SetDirty(vendor);
            EditorUtility.SetDirty(vendorNpc);
            EditorUtility.SetDirty(standardIdentity);
            EditorUtility.SetDirty(standardIdentity.Identity);
        }

        private static void EnsureTrainerNpc(string objectName, string trainerId, string displayName, MMOPlayableClass trainerClass, Vector3 fallbackPosition, MMOTrainerOfferEntry[] offers, MMOCharacterProfile profile)
        {
            EnsureTrainerNpc(objectName, trainerId, displayName, $"{trainerClass} Trainer", trainerClass, fallbackPosition, offers, profile);
        }

        private static void EnsureTrainerNpc(string objectName, string trainerId, string displayName, string title, MMOPlayableClass trainerClass, Vector3 fallbackPosition, MMOTrainerOfferEntry[] offers, MMOCharacterProfile profile)
        {
            GameObject trainer = GameObject.Find(objectName);
            if (trainer == null)
            {
                trainer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                trainer.name = objectName;
            }

            trainer.transform.SetParent(null, true);
            trainer.transform.position = Grounded(fallbackPosition);
            MMOGroundingUtility.SnapTransformToGround(trainer.transform, trainer.GetComponent<Collider>());
            trainer.isStatic = false;
            MMOClassTrainerNpc trainerNpc = trainer.GetComponent<MMOClassTrainerNpc>() ?? trainer.AddComponent<MMOClassTrainerNpc>();
            trainerNpc.Configure(trainerId, displayName, title, trainerClass, offers);
            MMOStandardNpcIdentity standardIdentity = trainer.GetComponent<MMOStandardNpcIdentity>() ?? trainer.AddComponent<MMOStandardNpcIdentity>();
            standardIdentity.Configure(profile, displayName, title, MMONpcIdentityRole.Trainer, true);

            EditorUtility.SetDirty(trainer);
            EditorUtility.SetDirty(trainerNpc);
            EditorUtility.SetDirty(standardIdentity);
            EditorUtility.SetDirty(standardIdentity.Identity);
        }

        private static void EnsureWorldInteractable(string objectName, string worldObjectId, string displayName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, MMOItemDefinition lootItem, int quantity)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                target = GameObject.CreatePrimitive(primitiveType);
                target.name = objectName;
            }

            target.transform.position = position;
            target.transform.localScale = scale;
            target.isStatic = false;

            MMOQuestWorldInteractable interactable = target.GetComponent<MMOQuestWorldInteractable>() ?? target.AddComponent<MMOQuestWorldInteractable>();
            interactable.Configure(worldObjectId, displayName, lootItem, quantity);
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(interactable);
        }

        private static Vector3 Grounded(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 0.5f;
            }

            return position;
        }

        private static MMOLootTable GetOrCreateLootTable(string assetName)
        {
            string path = $"{LootFolder}/{assetName}.asset";
            MMOLootTable lootTable = AssetDatabase.LoadAssetAtPath<MMOLootTable>(path);
            if (lootTable == null)
            {
                lootTable = ScriptableObject.CreateInstance<MMOLootTable>();
                AssetDatabase.CreateAsset(lootTable, path);
            }

            return lootTable;
        }

        private static MMOCharacterStats CreateStats(int stamina, int strength, int agility, int intellect, int spirit, int armor, int attackPower, int spellPower)
        {
            MMOCharacterStats stats = new();
            stats.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, 0f, 0f, 2f, 3f);
            return stats;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_').Replace("(", string.Empty).Replace(")", string.Empty);
        }

        private static string MMOUiFactoryName(MMOEquipmentSlotType slot)
        {
            return slot switch
            {
                MMOEquipmentSlotType.Feet => "boots",
                MMOEquipmentSlotType.Hands => "gloves",
                MMOEquipmentSlotType.Chest => "chest armor",
                MMOEquipmentSlotType.Legs => "leggings",
                _ => "armor"
            };
        }

        private sealed class StarterItems
        {
            public MMOItemDefinition MattedPelt;
            public MMOItemDefinition CrackedTusk;
            public MMOItemDefinition GreasySnout;
            public MMOItemDefinition BristlebackCharm;
            public MMOItemDefinition ValleyRuneShard;
            public MMOItemDefinition EmberCore;
            public MMOItemDefinition RepairHammer;
            public MMOItemDefinition WaterGourd;
            public MMOItemDefinition RazorcragJerky;
            public MMOItemDefinition SpringwaterFlask;
            public MMOItemDefinition[] BootRewards;
            public MMOItemDefinition[] GloveRewards;
            public MMOItemDefinition[] ChestRewards;
            public MMOItemDefinition[] LegRewards;

            public MMOItemDefinition[] All
            {
                get
                {
                    List<MMOItemDefinition> items = new()
                    {
                        MattedPelt,
                        CrackedTusk,
                        GreasySnout,
                        BristlebackCharm,
                        ValleyRuneShard,
                        EmberCore,
                        RepairHammer,
                        WaterGourd,
                        RazorcragJerky,
                        SpringwaterFlask
                    };
                    AddRange(items, BootRewards);
                    AddRange(items, GloveRewards);
                    AddRange(items, ChestRewards);
                    AddRange(items, LegRewards);
                    return items.ToArray();
                }
            }

            private static void AddRange(List<MMOItemDefinition> items, MMOItemDefinition[] range)
            {
                if (range == null)
                {
                    return;
                }

                foreach (MMOItemDefinition item in range)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }
        }

        private sealed class TrainerAbilities
        {
            public MMOAbilityDefinition Berzerkitis;
            public MMOAbilityDefinition Charge;
            public MMOAbilityDefinition MageArmor;
            public MMOAbilityDefinition FireBlast;
            public MMOAbilityDefinition WaterShield;
            public MMOAbilityDefinition LightningBolt;

            public MMOAbilityDefinition[] All => new[] { Berzerkitis, Charge, MageArmor, FireBlast, WaterShield, LightningBolt };
        }

        private sealed class StarterEnemies
        {
            public MMOEnemyDefinition Bristleback;
            public MMOEnemyDefinition AshCanyon;
            public MMOLootTable BristlebackLoot;
            public MMOLootTable AshCanyonLoot;
        }

        private sealed class StarterQuests
        {
            public MMOQuestDefinition A1;
            public MMOQuestDefinition A2;
            public MMOQuestDefinition A3;
            public MMOQuestDefinition B1;
            public MMOQuestDefinition B2;
            public MMOQuestDefinition B3;
            public MMOQuestDefinition C1;
            public MMOQuestDefinition C2;
            public MMOQuestDefinition C3;
            public MMOQuestDefinition D1;
            public MMOQuestDefinition D2;
            public MMOQuestDefinition D3;

            public MMOQuestDefinition[] All => new[] { A1, A2, A3, B1, B2, B3, C1, C2, C3, D1, D2, D3 };
        }
    }
}
