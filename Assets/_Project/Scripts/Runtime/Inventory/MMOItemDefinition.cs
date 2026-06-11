using UnityEngine;
using System.Collections.Generic;
using RPGClone.Characters;

namespace RPGClone.Inventory
{
    [CreateAssetMenu(menuName = "RPG Clone/Inventory/Item", fileName = "Item")]
    public sealed class MMOItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item";
        [SerializeField] private string displayName = "Item";
        [SerializeField, TextArea] private string description;
        [SerializeField] private MMOItemType itemType = MMOItemType.Trash;
        [SerializeField] private MMOItemQuality quality = MMOItemQuality.Poor;
        [Header("Presentation")]
        [Tooltip("Square item thumbnail used by inventory, loot, rewards, vendor, equipment, and action bar UI.")]
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStackSize = 20;
        [SerializeField, Min(0)] private int vendorValueCopper;
        [Header("Consumable")]
        [SerializeField] private MMOConsumableType consumableType = MMOConsumableType.None;
        [SerializeField, Min(0)] private int restoreHealthAmount;
        [SerializeField, Min(0)] private int restoreManaAmount;
        [SerializeField, Min(0.1f)] private float consumeDurationSeconds = 10f;
        [SerializeField] private bool requiresStationary = true;
        [Header("Equipment")]
        [SerializeField] private MMOEquipmentSlotType equipmentSlot = MMOEquipmentSlotType.Chest;
        [SerializeField] private MMOArmorWeight armorWeight = MMOArmorWeight.Cloth;
        [SerializeField] private MMOCharacterStats statBonuses = new();
        [Header("Weapon And Shield")]
        [SerializeField] private MMOWeaponType weaponType = MMOWeaponType.None;
        [SerializeField, Min(0f)] private float weaponMinDamage;
        [SerializeField, Min(0f)] private float weaponMaxDamage;
        [SerializeField, Min(0.1f)] private float weaponSpeedSeconds = 2f;
        [SerializeField, Min(0)] private int shieldBlockValue;
        [Tooltip("Leave empty to let every class equip this item. Use this for class-specific weapon and shield rewards.")]
        [SerializeField] private List<MMOPlayableClass> allowedClasses = new();

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public MMOItemType ItemType => itemType;
        public MMOItemQuality Quality => quality;
        public Sprite Icon => icon;
        public int MaxStackSize => Mathf.Max(1, maxStackSize);
        public int VendorValueCopper => vendorValueCopper;
        public MMOConsumableType ConsumableType => consumableType;
        public int RestoreHealthAmount => Mathf.Max(0, restoreHealthAmount);
        public int RestoreManaAmount => Mathf.Max(0, restoreManaAmount);
        public float ConsumeDurationSeconds => Mathf.Max(0.1f, consumeDurationSeconds);
        public bool RequiresStationary => requiresStationary;
        public MMOEquipmentSlotType EquipmentSlot => equipmentSlot;
        public MMOArmorWeight ArmorWeight => armorWeight;
        public MMOCharacterStats StatBonuses => statBonuses;
        public MMOWeaponType WeaponType => weaponType;
        public float WeaponMinDamage => Mathf.Max(0f, weaponMinDamage);
        public float WeaponMaxDamage => Mathf.Max(WeaponMinDamage, weaponMaxDamage);
        public float WeaponSpeedSeconds => Mathf.Max(0.1f, weaponSpeedSeconds);
        public int ShieldBlockValue => Mathf.Max(0, shieldBlockValue);
        public IReadOnlyList<MMOPlayableClass> AllowedClasses => allowedClasses;
        public bool IsEquipment => itemType == MMOItemType.Equipment;
        public bool IsConsumable => itemType == MMOItemType.Consumable && consumableType != MMOConsumableType.None;
        public bool IsWeapon => IsEquipment && weaponType != MMOWeaponType.None && weaponType != MMOWeaponType.Shield;
        public bool IsShield => IsEquipment && weaponType == MMOWeaponType.Shield;
        public bool IsTwoHandedWeapon => weaponType == MMOWeaponType.TwoHandSword
            || weaponType == MMOWeaponType.TwoHandMace
            || weaponType == MMOWeaponType.Staff;
        public float WeaponDps => IsWeapon ? (WeaponMinDamage + WeaponMaxDamage) * 0.5f / WeaponSpeedSeconds : 0f;

        public void Configure(
            string newItemId,
            string newDisplayName,
            string newDescription,
            MMOItemType newItemType,
            MMOItemQuality newQuality,
            int newMaxStackSize,
            int newVendorValueCopper,
            Sprite newIcon = null)
        {
            itemId = string.IsNullOrWhiteSpace(newItemId) ? name : newItemId;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? itemId : newDisplayName;
            description = newDescription;
            itemType = newItemType;
            quality = newQuality;
            maxStackSize = Mathf.Max(1, newMaxStackSize);
            vendorValueCopper = Mathf.Max(0, newVendorValueCopper);
            icon = newIcon;
            if (itemType != MMOItemType.Consumable)
            {
                consumableType = MMOConsumableType.None;
                restoreHealthAmount = 0;
                restoreManaAmount = 0;
            }
        }

        public void ConfigureConsumable(
            string newItemId,
            string newDisplayName,
            string newDescription,
            MMOItemQuality newQuality,
            int newMaxStackSize,
            int newVendorValueCopper,
            MMOConsumableType newConsumableType,
            int newRestoreHealthAmount,
            int newRestoreManaAmount,
            float newConsumeDurationSeconds,
            bool newRequiresStationary = true,
            Sprite newIcon = null)
        {
            Configure(newItemId, newDisplayName, newDescription, MMOItemType.Consumable, newQuality, newMaxStackSize, newVendorValueCopper, newIcon);
            consumableType = newConsumableType;
            restoreHealthAmount = Mathf.Max(0, newRestoreHealthAmount);
            restoreManaAmount = Mathf.Max(0, newRestoreManaAmount);
            consumeDurationSeconds = Mathf.Max(0.1f, newConsumeDurationSeconds);
            requiresStationary = newRequiresStationary;
        }

        public void ConfigureEquipment(
            string newItemId,
            string newDisplayName,
            string newDescription,
            MMOItemQuality newQuality,
            MMOEquipmentSlotType newEquipmentSlot,
            MMOArmorWeight newArmorWeight,
            MMOCharacterStats newStatBonuses,
            int newVendorValueCopper,
            Sprite newIcon = null)
        {
            Configure(newItemId, newDisplayName, newDescription, MMOItemType.Equipment, newQuality, 1, newVendorValueCopper, newIcon);
            equipmentSlot = newEquipmentSlot;
            armorWeight = newArmorWeight;
            statBonuses ??= new MMOCharacterStats();
            if (newStatBonuses != null)
            {
                statBonuses.CopyFrom(newStatBonuses);
            }
            else
            {
                statBonuses.Configure(0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, 2f, 3f);
            }

            weaponType = MMOWeaponType.None;
            weaponMinDamage = 0f;
            weaponMaxDamage = 0f;
            weaponSpeedSeconds = 2f;
            shieldBlockValue = 0;
            allowedClasses = new List<MMOPlayableClass>();
        }

        public void ConfigureWeapon(
            string newItemId,
            string newDisplayName,
            string newDescription,
            MMOItemQuality newQuality,
            MMOWeaponType newWeaponType,
            float newMinDamage,
            float newMaxDamage,
            float newSpeedSeconds,
            MMOCharacterStats newStatBonuses,
            int newVendorValueCopper,
            IEnumerable<MMOPlayableClass> newAllowedClasses = null,
            Sprite newIcon = null)
        {
            MMOEquipmentSlotType slot = IsTwoHandedWeaponType(newWeaponType) || newWeaponType != MMOWeaponType.Shield
                ? MMOEquipmentSlotType.MainHand
                : MMOEquipmentSlotType.OffHand;
            ConfigureEquipment(newItemId, newDisplayName, newDescription, newQuality, slot, MMOArmorWeight.Cloth, newStatBonuses, newVendorValueCopper, newIcon);
            weaponType = newWeaponType;
            weaponMinDamage = Mathf.Max(0f, newMinDamage);
            weaponMaxDamage = Mathf.Max(weaponMinDamage, newMaxDamage);
            weaponSpeedSeconds = Mathf.Max(0.1f, newSpeedSeconds);
            shieldBlockValue = 0;
            SetAllowedClasses(newAllowedClasses);
        }

        public void ConfigureShield(
            string newItemId,
            string newDisplayName,
            string newDescription,
            MMOItemQuality newQuality,
            int newArmor,
            int newBlockValue,
            MMOCharacterStats newStatBonuses,
            int newVendorValueCopper,
            IEnumerable<MMOPlayableClass> newAllowedClasses = null,
            Sprite newIcon = null)
        {
            MMOCharacterStats combinedStats = new();
            combinedStats.Configure(0, 0, 0, 0, 0, 0, 0, 0, 0f, 0f, 2f, 3f);
            if (newStatBonuses != null)
            {
                combinedStats.CopyFrom(newStatBonuses);
            }

            combinedStats.AddValues(0, 0, 0, 0, 0, newArmor, 0, 0, 0f, 0f);
            ConfigureEquipment(newItemId, newDisplayName, newDescription, newQuality, MMOEquipmentSlotType.OffHand, MMOArmorWeight.Mail, combinedStats, newVendorValueCopper, newIcon);
            weaponType = MMOWeaponType.Shield;
            weaponMinDamage = 0f;
            weaponMaxDamage = 0f;
            weaponSpeedSeconds = 2f;
            shieldBlockValue = Mathf.Max(0, newBlockValue);
            SetAllowedClasses(newAllowedClasses);
        }

        public bool CanClassEquip(MMOPlayableClass characterClass)
        {
            return allowedClasses == null || allowedClasses.Count == 0 || allowedClasses.Contains(characterClass);
        }

        private void SetAllowedClasses(IEnumerable<MMOPlayableClass> newAllowedClasses)
        {
            allowedClasses = newAllowedClasses != null ? new List<MMOPlayableClass>(newAllowedClasses) : new List<MMOPlayableClass>();
        }

        private static bool IsTwoHandedWeaponType(MMOWeaponType type)
        {
            return type == MMOWeaponType.TwoHandSword || type == MMOWeaponType.TwoHandMace || type == MMOWeaponType.Staff;
        }
    }
}
