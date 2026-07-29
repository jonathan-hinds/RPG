using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Loot;
using UnityEditor;
using UnityEngine;

namespace RPGClone.Tests
{
    public sealed class MMOTribalScaleArmorContentTests
    {
        private const string CatalogPath = "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset";

        private static readonly ArmorExpectation[] ArmorExpectations =
        {
            new("Cloth/Tribal Seer's Vestments/Tribal_Seers_Vestments.asset", "tribal_seers_vestments", 4, MMOArmorWeight.Cloth, MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso, 6, 1, 0, 0, 2, 1),
            new("Cloth/Tribal Seer's Legwraps/Tribal_Seers_Legwraps.asset", "tribal_seers_legwraps", 5, MMOArmorWeight.Cloth, MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, 5, 0, 0, 0, 2, 1),
            new("Leather/Tribal Mystic's Grips/Tribal_Mystics_Grips.asset", "tribal_mystics_grips", 6, MMOArmorWeight.Leather, MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands, 5, 1, 0, 0, 2, 1),
            new("Leather/Tribal Mystic's Treads/Tribal_Mystics_Treads.asset", "tribal_mystics_treads", 7, MMOArmorWeight.Leather, MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet, 6, 1, 0, 0, 2, 2),
            new("Leather/Scalehunter Grips/Scalehunter_Grips.asset", "scalehunter_grips", 4, MMOArmorWeight.Leather, MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands, 4, 0, 1, 1, 0, 0),
            new("Leather/Scalehunter Treads/Scalehunter_Treads.asset", "scalehunter_treads", 5, MMOArmorWeight.Leather, MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet, 5, 1, 1, 1, 0, 0),
            new("Mail/Scaleguard Legguards/Scaleguard_Legguards.asset", "scaleguard_legguards", 6, MMOArmorWeight.Mail, MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, 8, 1, 1, 1, 0, 0),
            new("Mail/Scaleguard Hauberk/Scaleguard_Hauberk.asset", "scaleguard_hauberk", 7, MMOArmorWeight.Mail, MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso, 10, 2, 2, 0, 0, 0)
        };

        private static readonly LootExpectation[] LootExpectations =
        {
            new("Assets/_Project/Configs/Loot/Wolf_Trash_Loot.asset", "tribal_mystics_treads", 0.03f),
            new("Assets/_Project/Configs/Loot/Bristleback_Trash_Loot.asset", "scalehunter_grips", 0.04f),
            new("Assets/_Project/Configs/Loot/Trog_Trash_Loot.asset", "tribal_seers_vestments", 0.05f),
            new("Assets/_Project/Configs/Loot/Trog_Trash_Loot.asset", "tribal_seers_legwraps", 0.02f),
            new("Assets/_Project/Configs/Loot/Ogre_Trash_Loot.asset", "scaleguard_legguards", 0.03f),
            new("Assets/_Project/Configs/Loot/Ash_Canyon_Quest_Loot.asset", "scalehunter_treads", 0.04f),
            new("Assets/_Project/Configs/Loot/AshGeneral_Trash_Loot.asset", "scaleguard_hauberk", 0.01f),
            new("Assets/_Project/Configs/Loot/AshGeneral_Trash_Loot.asset", "tribal_mystics_grips", 0.02f)
        };

        [Test]
        public void ArmorItems_HaveExpectedStatsAndCompleteItemOwnedVisualBundles()
        {
            foreach (ArmorExpectation expected in ArmorExpectations)
            {
                MMOItemDefinition item = LoadItem(expected);
                Assert.That(item.ItemId, Is.EqualTo(expected.ItemId));
                Assert.That(item.Quality, Is.EqualTo(MMOItemQuality.Uncommon));
                Assert.That(item.RequiredLevel, Is.EqualTo(expected.RequiredLevel));
                Assert.That(item.ArmorWeight, Is.EqualTo(expected.ArmorWeight));
                Assert.That(item.EquipmentSlot, Is.EqualTo(expected.Slot));
                Assert.That(item.StatBonuses.BaseArmor, Is.EqualTo(expected.Armor));
                Assert.That(item.StatBonuses.Stamina, Is.EqualTo(expected.Stamina));
                Assert.That(item.StatBonuses.Strength, Is.EqualTo(expected.Strength));
                Assert.That(item.StatBonuses.Agility, Is.EqualTo(expected.Agility));
                Assert.That(item.StatBonuses.Intellect, Is.EqualTo(expected.Intellect));
                Assert.That(item.StatBonuses.Spirit, Is.EqualTo(expected.Spirit));
                Assert.That(item.Icon, Is.Not.Null);
                Assert.That(item.EquipmentVisual, Is.Not.Null);

                Sprite icon = item.Icon;
                Assert.That(icon.texture.width, Is.EqualTo(256));
                Assert.That(icon.texture.height, Is.EqualTo(256));
                Assert.That(icon.border, Is.EqualTo(Vector4.zero));

                MMOEquipmentVisualDefinition visual = item.EquipmentVisual;
                Assert.That(visual.BindingMode, Is.EqualTo(MMOEquipmentVisualBindingMode.BodyPart));
                Assert.That(visual.EquipmentSlot, Is.EqualTo(expected.Slot));
                Assert.That(visual.BodyPart, Is.EqualTo(expected.BodyPart));
                Assert.That(visual.HideBaseBodyPart, Is.True);
                Assert.That(visual.ModelPrefab, Is.Not.Null);
                Assert.That(visual.ModelPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Not.Null);
                Assert.That(visual.MaterialOverride, Is.Not.Null);
                Assert.That(visual.DiffuseTexture, Is.Not.Null);
                Assert.That(visual.MaterialOverride.mainTexture, Is.SameAs(visual.DiffuseTexture));
            }
        }

        [Test]
        public void ItemCatalog_ContainsEveryNewArmorItemExactlyOnce()
        {
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            foreach (ArmorExpectation expected in ArmorExpectations)
            {
                Assert.That(
                    catalog.Items.Count(item => item != null && item.ItemId == expected.ItemId),
                    Is.EqualTo(1),
                    expected.ItemId);
            }
        }

        [Test]
        public void CreatureLootTables_ContainEveryNewPieceOnceAtConfiguredLowChance()
        {
            HashSet<string> newItemIds = ArmorExpectations
                .Select(expected => expected.ItemId)
                .ToHashSet();
            List<MMOLootTableEntry> installedEntries = new();

            foreach (IGrouping<string, LootExpectation> tableExpectations in LootExpectations.GroupBy(value => value.TablePath))
            {
                MMOLootTable table = AssetDatabase.LoadAssetAtPath<MMOLootTable>(tableExpectations.Key);
                Assert.That(table, Is.Not.Null, tableExpectations.Key);

                foreach (LootExpectation expected in tableExpectations)
                {
                    List<MMOLootTableEntry> matching = table.Entries
                        .Where(entry => entry?.Item != null && entry.Item.ItemId == expected.ItemId)
                        .ToList();
                    Assert.That(matching, Has.Count.EqualTo(1), $"{tableExpectations.Key}: {expected.ItemId}");
                    Assert.That(matching[0].DropChance, Is.EqualTo(expected.DropChance).Within(0.0001f));
                    Assert.That(matching[0].MinQuantity, Is.EqualTo(1));
                    Assert.That(matching[0].MaxQuantity, Is.EqualTo(1));
                    installedEntries.Add(matching[0]);
                }
            }

            Assert.That(installedEntries, Has.Count.EqualTo(ArmorExpectations.Length));
            Assert.That(
                installedEntries.Select(entry => entry.Item.ItemId).Distinct(),
                Is.EquivalentTo(newItemIds));
            Assert.That(installedEntries.All(entry => entry.DropChance is >= 0.01f and <= 0.05f), Is.True);
        }

        private static MMOItemDefinition LoadItem(ArmorExpectation expected)
        {
            string path = $"Assets/_Project/Equipment/Armor/{expected.RelativePath}";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(path);
            Assert.That(item, Is.Not.Null, path);
            return item;
        }

        private sealed class ArmorExpectation
        {
            public ArmorExpectation(
                string relativePath,
                string itemId,
                int requiredLevel,
                MMOArmorWeight armorWeight,
                MMOEquipmentSlotType slot,
                MMOCharacterBodyPart bodyPart,
                int armor,
                int stamina,
                int strength,
                int agility,
                int intellect,
                int spirit)
            {
                RelativePath = relativePath;
                ItemId = itemId;
                RequiredLevel = requiredLevel;
                ArmorWeight = armorWeight;
                Slot = slot;
                BodyPart = bodyPart;
                Armor = armor;
                Stamina = stamina;
                Strength = strength;
                Agility = agility;
                Intellect = intellect;
                Spirit = spirit;
            }

            public string RelativePath { get; }
            public string ItemId { get; }
            public int RequiredLevel { get; }
            public MMOArmorWeight ArmorWeight { get; }
            public MMOEquipmentSlotType Slot { get; }
            public MMOCharacterBodyPart BodyPart { get; }
            public int Armor { get; }
            public int Stamina { get; }
            public int Strength { get; }
            public int Agility { get; }
            public int Intellect { get; }
            public int Spirit { get; }
        }

        private sealed class LootExpectation
        {
            public LootExpectation(string tablePath, string itemId, float dropChance)
            {
                TablePath = tablePath;
                ItemId = itemId;
                DropChance = dropChance;
            }

            public string TablePath { get; }
            public string ItemId { get; }
            public float DropChance { get; }
        }
    }
}
