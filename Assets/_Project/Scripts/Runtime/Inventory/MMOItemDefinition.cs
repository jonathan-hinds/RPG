using UnityEngine;
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
        public bool IsEquipment => itemType == MMOItemType.Equipment;
        public bool IsConsumable => itemType == MMOItemType.Consumable && consumableType != MMOConsumableType.None;

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
        }
    }
}
