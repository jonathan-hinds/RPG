#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTests
{
    public sealed class MMOBristlebackEncroachmentQuestContentTests
    {
        private const string QuestFolder = "Assets/_Project/Configs/Quests";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        [Test]
        public void BristlebackEncroachment_IsOneOrderedFourQuestChain()
        {
            MMOQuestDefinition[] quests =
            {
                LoadQuest("Wolves_at_the_Palisade"),
                LoadQuest("Break_the_First_Wave"),
                LoadQuest("The_Horn_That_Calls"),
                LoadQuest("Orders_in_the_Ash")
            };

            Assert.That(quests, Has.All.Not.Null);
            Assert.That(quests.Select(quest => quest.QuestLevel), Is.EqualTo(new[] { 3, 3, 4, 5 }));
            Assert.That(quests.Select(quest => quest.MinimumLevel), Is.EqualTo(new[] { 3, 3, 4, 5 }));
            Assert.That(
                quests[0].PrerequisiteQuests.Single().QuestId,
                Is.EqualTo("razorcrag_supplies_03"));
            for (int index = 1; index < quests.Length; index++)
            {
                Assert.That(quests[index].PrerequisiteQuests, Has.Count.EqualTo(1));
                Assert.That(quests[index].PrerequisiteQuests[0], Is.SameAs(quests[index - 1]));
            }

            Assert.That(quests[0].OfferedByNpcId, Is.EqualTo("beastwarden_torak"));
            Assert.That(quests[1].OfferedByNpcId, Is.EqualTo("beastwarden_torak"));
            Assert.That(quests[1].TurnedInToNpcId, Is.EqualTo("outrider_vesha"));
            Assert.That(quests[2].OfferedByNpcId, Is.EqualTo("outrider_vesha"));
            Assert.That(quests[3].OfferedByNpcId, Is.EqualTo("outrider_vesha"));
        }

        [Test]
        public void BristlebackEncroachment_UsesRequestedObjectivesAndCurrencyExperienceRewardsOnly()
        {
            MMOQuestDefinition wolves = LoadQuest("Wolves_at_the_Palisade");
            MMOQuestDefinition invaders = LoadQuest("Break_the_First_Wave");
            MMOQuestDefinition horn = LoadQuest("The_Horn_That_Calls");
            MMOQuestDefinition ledger = LoadQuest("Orders_in_the_Ash");

            AssertObjective(wolves, MMOQuestObjectiveType.KillCreature, 6, "Wolf_Aggressive");
            AssertObjective(invaders, MMOQuestObjectiveType.KillCreature, 3, "Bristleback_Invader_Aggressive");
            AssertObjective(horn, MMOQuestObjectiveType.CollectQuestItem, 1, "Bristleback_Invader_Aggressive");
            Assert.That(horn.Objectives[0].RequiredItem.ItemId, Is.EqualTo("bristleback_signal_horn"));
            AssertObjective(ledger, MMOQuestObjectiveType.CollectQuestItem, 1, null);
            Assert.That(ledger.Objectives[0].RequiredItem.ItemId, Is.EqualTo("bristleback_march_ledger"));
            Assert.That(ledger.Objectives[0].RequiredWorldObjectId, Is.EqualTo("bristleback_command_cache"));

            AssertRewards(wolves, 520, 65);
            AssertRewards(invaders, 680, 90);
            AssertRewards(horn, 760, 120);
            AssertRewards(ledger, 1050, 175);
        }

        [Test]
        public void InvaderLoot_HasQuestGatedGuaranteedSignalHorn()
        {
            MMOEnemyDefinition invader = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(
                "Assets/_Project/Configs/Enemies/Bristleback_Invader_Aggressive.asset");
            Assert.That(invader, Is.Not.Null);

            SerializedObject serializedInvader = new(invader);
            MMOLootTable lootTable = serializedInvader.FindProperty("lootTable").objectReferenceValue as MMOLootTable;
            Assert.That(lootTable, Is.Not.Null);
            Assert.That(lootTable.name, Is.EqualTo("Bristleback_Invader_Quest_Loot"));

            MMOLootTableEntry horn = lootTable.Entries.Single(entry =>
                entry.Item != null && entry.Item.ItemId == "bristleback_signal_horn");
            Assert.That(horn.DropChance, Is.EqualTo(1f));
            Assert.That(horn.RequiredQuest.QuestId, Is.EqualTo("bristleback_encroachment_03"));
            Assert.That(horn.OnlyDropWhileQuestNeedsItem, Is.True);
        }

        [Test]
        public void Scene_ContainsTwoDressedQuestNpcsAndCommandCache()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                MMOQuestNpc[] questNpcs = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MMOQuestNpc>(true))
                    .ToArray();
                AssertNpcAppearance(
                    questNpcs.Single(npc => npc.NpcId == "beastwarden_torak"),
                    "hair_6",
                    "hair_copper",
                    "face_6");
                AssertNpcAppearance(
                    questNpcs.Single(npc => npc.NpcId == "outrider_vesha"),
                    "hair_4",
                    "hair_raven_black",
                    "face_3");

                MMOQuestWorldInteractable commandCache = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MMOQuestWorldInteractable>(true))
                    .Single(interactable => interactable.WorldObjectId == "bristleback_command_cache");
                Assert.That(commandCache.LootItem, Is.Not.Null);
                Assert.That(commandCache.LootItem.ItemId, Is.EqualTo("bristleback_march_ledger"));
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void StarterCatalogs_ContainAllNewQuestAndItemAssets()
        {
            MMOQuestCatalog questCatalog = AssetDatabase.LoadAssetAtPath<MMOQuestCatalog>(
                $"{QuestFolder}/Starter_Quest_Catalog.asset");
            MMOItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(
                "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset");

            Assert.That(questCatalog, Is.Not.Null);
            Assert.That(itemCatalog, Is.Not.Null);
            Assert.That(
                questCatalog.Quests.Select(quest => quest.QuestId),
                Is.SupersetOf(new[]
                {
                    "bristleback_encroachment_01",
                    "bristleback_encroachment_02",
                    "bristleback_encroachment_03",
                    "bristleback_encroachment_04"
                }));
            Assert.That(
                itemCatalog.Items.Select(item => item.ItemId),
                Is.SupersetOf(new[] { "bristleback_signal_horn", "bristleback_march_ledger" }));
        }

        private static MMOQuestDefinition LoadQuest(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<MMOQuestDefinition>($"{QuestFolder}/{assetName}.asset");
        }

        private static void AssertObjective(
            MMOQuestDefinition quest,
            MMOQuestObjectiveType expectedType,
            int expectedCount,
            string expectedCreatureName)
        {
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.Objectives, Has.Count.EqualTo(1));
            MMOQuestObjectiveDefinition objective = quest.Objectives[0];
            Assert.That(objective.ObjectiveType, Is.EqualTo(expectedType));
            Assert.That(objective.RequiredCount, Is.EqualTo(expectedCount));
            if (expectedCreatureName == null)
            {
                Assert.That(objective.RequiredCreature, Is.Null);
            }
            else
            {
                Assert.That(objective.RequiredCreature, Is.Not.Null);
                Assert.That(objective.RequiredCreature.name, Is.EqualTo(expectedCreatureName));
            }
        }

        private static void AssertRewards(MMOQuestDefinition quest, int experience, int copper)
        {
            Assert.That(quest.Rewards.Experience, Is.EqualTo(experience));
            Assert.That(quest.Rewards.MoneyCopper, Is.EqualTo(copper));
            Assert.That(quest.Rewards.GuaranteedItems, Is.Empty);
            Assert.That(quest.Rewards.ChoiceItems, Is.Empty);
        }

        private static void AssertNpcAppearance(
            MMOQuestNpc npc,
            string hairstyleId,
            string hairColorId,
            string faceId)
        {
            Assert.That(npc, Is.Not.Null);
            MMONpcVisualAuthoring appearance = npc.GetComponent<MMONpcVisualAuthoring>();
            Assert.That(appearance, Is.Not.Null);
            Assert.That(appearance.HairstyleId, Is.EqualTo(hairstyleId));
            Assert.That(appearance.HairColorId, Is.EqualTo(hairColorId));
            Assert.That(appearance.FaceId, Is.EqualTo(faceId));
            Assert.That(appearance.ChestArmor, Is.Not.Null);
            Assert.That(appearance.Gloves, Is.Not.Null);
            Assert.That(appearance.Pants, Is.Not.Null);
            Assert.That(appearance.Boots, Is.Not.Null);
        }
    }
}
#endif
