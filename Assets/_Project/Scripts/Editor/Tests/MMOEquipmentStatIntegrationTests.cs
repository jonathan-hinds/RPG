using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Tests
{
    public sealed class MMOEquipmentStatIntegrationTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ConfigureProfile_UsesAuthoredResourceMaximumsWithoutDoubleCountingBaseStats()
        {
            MMOCharacterStats baseStats = CreateStats(15, 10, 8, 12, 10);
            MMOCharacterProfile profile = Track(ScriptableObject.CreateInstance<MMOCharacterProfile>());
            profile.Configure(
                "Profile Baseline",
                7,
                125,
                180,
                Color.white,
                newFaction: MMOEntityFaction.Player,
                newBaseStats: baseStats);

            GameObject character = CreateCharacter(MMOPlayableClass.Mage);
            MMOCharacterIdentity identity = character.GetComponent<MMOCharacterIdentity>();
            identity.Configure(profile);

            Assert.That(identity.Health.MaxValue, Is.EqualTo(125));
            Assert.That(identity.Mana.MaxValue, Is.EqualTo(180));
        }

        [Test]
        public void EquipAndUnequip_UpdatesResourceMaximumsAndPreservesCurrentPercent()
        {
            GameObject character = CreateCharacter(MMOPlayableClass.Mage);
            MMOCharacterIdentity identity = character.GetComponent<MMOCharacterIdentity>();
            identity.Configure(
                "Equipment Test",
                7,
                null,
                Color.white,
                MMOEntityFaction.Player,
                true,
                CreateStats(10, 5, 5, 10, 5),
                100,
                100);
            identity.Health.SetCurrent(50);
            identity.Mana.SetCurrent(50);

            MMOItemDefinition item = CreateArmor(
                "resource_test_vest",
                MMOEquipmentSlotType.Chest,
                MMOArmorWeight.Cloth,
                CreateStats(2, 0, 0, 2, 0));
            item.SetRequiredLevel(4);

            MMOCharacterEquipment equipment = character.GetComponent<MMOCharacterEquipment>();
            Assert.That(equipment.TryEquip(item), Is.True);
            Assert.That(identity.Stats.Stamina, Is.EqualTo(12));
            Assert.That(identity.Stats.Intellect, Is.EqualTo(12));
            Assert.That(identity.Health.MaxValue, Is.EqualTo(120));
            Assert.That(identity.Health.CurrentValue, Is.EqualTo(60));
            Assert.That(identity.Mana.MaxValue, Is.EqualTo(130));
            Assert.That(identity.Mana.CurrentValue, Is.EqualTo(65));

            equipment.ClearEquipment();
            Assert.That(identity.Stats.Stamina, Is.EqualTo(10));
            Assert.That(identity.Stats.Intellect, Is.EqualTo(10));
            Assert.That(identity.Health.MaxValue, Is.EqualTo(100));
            Assert.That(identity.Health.CurrentValue, Is.EqualTo(50));
            Assert.That(identity.Mana.MaxValue, Is.EqualTo(100));
            Assert.That(identity.Mana.CurrentValue, Is.EqualTo(50));
        }

        [Test]
        public void EquippingStatGains_PreservesActiveRuntimeModifiers()
        {
            MMOCharacterStats stats = CreateStats(0, 10, 0, 0, 0, attackPower: 5);
            stats.SetDerivedStatContext(MMOPlayableClass.Warrior);
            stats.SetRuntimeModifiers(10, 2f, 1f, 1f, 1f, 1f);

            Assert.That(stats.AttackPower, Is.EqualTo(70));

            stats.Add(CreateStats(0, 1, 0, 0, 0));
            Assert.That(stats.AttackPower, Is.EqualTo(74));

            stats.Subtract(CreateStats(0, 1, 0, 0, 0));
            Assert.That(stats.AttackPower, Is.EqualTo(70));
        }

        [Test]
        public void DerivedStats_ApplyClassicPrimaryStatContributions()
        {
            MMOCharacterStats stats = CreateStats(10, 10, 10, 10, 10, armor: 3, spellPower: 7);
            stats.SetDerivedStatContext(MMOPlayableClass.Warrior);

            Assert.That(stats.MaxHealthBonus, Is.EqualTo(100));
            Assert.That(stats.MaxManaBonus, Is.EqualTo(150));
            Assert.That(stats.AttackPower, Is.EqualTo(20));
            Assert.That(stats.Armor, Is.EqualTo(23));
            Assert.That(stats.SpellPower, Is.EqualTo(7));
            Assert.That(stats.CriticalStrikeChance, Is.GreaterThan(5f));
            Assert.That(stats.SpellCriticalStrikeChance, Is.GreaterThan(5f));
            Assert.That(stats.DodgeChance, Is.GreaterThan(3f));
            Assert.That(stats.HealthRegenPerSecond, Is.GreaterThan(0f));
            Assert.That(stats.ManaRegenPerSecond, Is.GreaterThan(0f));

            stats.SetDerivedStatContext(MMOPlayableClass.Mage);
            Assert.That(stats.AttackPower, Is.EqualTo(10));

            MMOCharacterStats moreIntellect = CreateStats(10, 10, 10, 20, 10, armor: 3, spellPower: 7);
            moreIntellect.SetDerivedStatContext(MMOPlayableClass.Mage);
            Assert.That(moreIntellect.SpellPower, Is.EqualTo(stats.SpellPower));
            Assert.That(moreIntellect.SpellCriticalStrikeChance, Is.GreaterThan(stats.SpellCriticalStrikeChance));
        }

        [Test]
        public void CanEquip_RejectsItemsAboveCharacterLevel()
        {
            GameObject character = CreateCharacter(MMOPlayableClass.Mage);
            MMOCharacterIdentity identity = character.GetComponent<MMOCharacterIdentity>();
            identity.Configure(
                "Level Test",
                4,
                null,
                Color.white,
                MMOEntityFaction.Player,
                true,
                CreateStats(10, 5, 5, 10, 5),
                100,
                100);

            MMOItemDefinition item = CreateArmor(
                "level_test_vest",
                MMOEquipmentSlotType.Chest,
                MMOArmorWeight.Cloth,
                CreateStats(1, 0, 0, 1, 0));
            item.SetRequiredLevel(5);

            Assert.That(character.GetComponent<MMOCharacterEquipment>().CanEquip(item), Is.False);
        }

        private GameObject CreateCharacter(MMOPlayableClass characterClass)
        {
            GameObject character = Track(new GameObject("Equipment Stat Test Character"));
            character.AddComponent<MMOCharacterCustomization>().Configure(MMOPlayableRace.Orc, characterClass);
            character.AddComponent<MMOCharacterIdentity>();
            character.AddComponent<MMOCharacterEquipment>().EnsureDefaultSlots();
            return character;
        }

        private MMOItemDefinition CreateArmor(
            string itemId,
            MMOEquipmentSlotType slot,
            MMOArmorWeight armorWeight,
            MMOCharacterStats stats)
        {
            MMOItemDefinition item = Track(ScriptableObject.CreateInstance<MMOItemDefinition>());
            item.ConfigureEquipment(
                itemId,
                itemId,
                string.Empty,
                MMOItemQuality.Uncommon,
                slot,
                armorWeight,
                stats,
                0);
            return item;
        }

        private static MMOCharacterStats CreateStats(
            int stamina,
            int strength,
            int agility,
            int intellect,
            int spirit,
            int armor = 0,
            int attackPower = 0,
            int spellPower = 0)
        {
            MMOCharacterStats stats = new();
            stats.Configure(
                stamina,
                strength,
                agility,
                intellect,
                spirit,
                armor,
                attackPower,
                spellPower,
                0f,
                0f,
                2f,
                3f);
            return stats;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
