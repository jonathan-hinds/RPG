using UnityEngine;

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

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public MMOItemType ItemType => itemType;
        public MMOItemQuality Quality => quality;
        public Sprite Icon => icon;
        public int MaxStackSize => Mathf.Max(1, maxStackSize);
        public int VendorValueCopper => vendorValueCopper;

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
    }
}
