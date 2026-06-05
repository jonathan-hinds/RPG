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
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStackSize = 20;
        [SerializeField, Min(0)] private int vendorValueCopper;
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
        public MMOEquipmentSlotType EquipmentSlot => equipmentSlot;
        public MMOArmorWeight ArmorWeight => armorWeight;
        public MMOCharacterStats StatBonuses => statBonuses;
        public bool IsEquipment => itemType == MMOItemType.Equipment;

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
