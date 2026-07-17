using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Services;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.Tests
{
    public sealed class MMOItemClassCompatibilityTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            MMOGameplaySessionService.LocalPlayer.ClearLocalPlayer();
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
        public void CanEquip_UsesClassArmorWeightLimits()
        {
            MMOItemDefinition cloth = CreateArmor("Cloth", MMOArmorWeight.Cloth);
            MMOItemDefinition leather = CreateArmor("Leather", MMOArmorWeight.Leather);
            MMOItemDefinition mail = CreateArmor("Mail", MMOArmorWeight.Mail);

            Assert.That(MMOItemClassCompatibility.CanEquip(cloth, MMOPlayableClass.Mage), Is.True);
            Assert.That(MMOItemClassCompatibility.CanEquip(leather, MMOPlayableClass.Mage), Is.False);
            Assert.That(MMOItemClassCompatibility.CanEquip(leather, MMOPlayableClass.Shaman), Is.True);
            Assert.That(MMOItemClassCompatibility.CanEquip(mail, MMOPlayableClass.Shaman), Is.False);
            Assert.That(MMOItemClassCompatibility.CanEquip(mail, MMOPlayableClass.Warrior), Is.True);
        }

        [Test]
        public void CanEquip_UsesExplicitWeaponClassRestrictions()
        {
            MMOItemDefinition staff = Track(ScriptableObject.CreateInstance<MMOItemDefinition>());
            staff.ConfigureWeapon(
                "staff",
                "Staff",
                string.Empty,
                MMOItemQuality.Common,
                MMOWeaponType.Staff,
                1f,
                2f,
                2f,
                new MMOCharacterStats(),
                0,
                new[] { MMOPlayableClass.Mage, MMOPlayableClass.Shaman });

            Assert.That(MMOItemClassCompatibility.CanEquip(staff, MMOPlayableClass.Mage), Is.True);
            Assert.That(MMOItemClassCompatibility.CanEquip(staff, MMOPlayableClass.Shaman), Is.True);
            Assert.That(MMOItemClassCompatibility.CanEquip(staff, MMOPlayableClass.Warrior), Is.False);
        }

        [Test]
        public void AddToSlot_TintsTextPlaceholderWhenLocalClassCannotEquipItem()
        {
            GameObject player = Track(new GameObject("Item Tint Test Player"));
            MMOCharacterCustomization customization = player.AddComponent<MMOCharacterCustomization>();
            customization.Configure(default, MMOPlayableClass.Mage);
            MMOGameplaySessionService.LocalPlayer.SetLocalPlayer(player, "test", "test");

            MMOItemDefinition mail = CreateArmor("Mail", MMOArmorWeight.Mail);
            GameObject slotObject = Track(new GameObject("Item Tint Test Slot", typeof(RectTransform)));
            RectTransform slot = (RectTransform)slotObject.transform;

            MMOItemIconView.AddToSlot(slot, mail, 0, false);

            Text placeholder = slotObject.GetComponentInChildren<Text>();
            Assert.That(MMOItemIconView.IsRestrictedForLocalPlayer(mail), Is.True);
            Assert.That(placeholder, Is.Not.Null);
            Assert.That(placeholder.color, Is.EqualTo(MMOItemIconView.GetIconTint(mail)));
        }

        private MMOItemDefinition CreateArmor(string displayName, MMOArmorWeight armorWeight)
        {
            MMOItemDefinition item = Track(ScriptableObject.CreateInstance<MMOItemDefinition>());
            item.ConfigureEquipment(
                displayName.ToLowerInvariant(),
                displayName,
                string.Empty,
                MMOItemQuality.Common,
                MMOEquipmentSlotType.Chest,
                armorWeight,
                new MMOCharacterStats(),
                0);
            return item;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
