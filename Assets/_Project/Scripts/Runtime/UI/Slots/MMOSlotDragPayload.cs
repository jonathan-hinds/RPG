using RPGClone.Abilities;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.UI
{
    public enum MMOSlotContentCategory
    {
        None,
        Item,
        Ability,
        Equipment,
        Custom
    }

    public enum MMOSlotDragOperation
    {
        None,
        Move,
        Swap,
        Equip,
        Unequip,
        Assign,
        Purchase,
        Reject
    }

    /// <summary>
    /// Generalized cursor payload. Destinations inspect only the category and source
    /// context they support; concrete gameplay systems remain responsible for validation.
    /// </summary>
    public readonly struct MMOSlotDragPayload
    {
        public readonly MMOSlotContentCategory Category;
        public readonly Object Content;
        public readonly Object SourceContext;
        public readonly int SourceSlotIndex;
        public readonly MMOEquipmentSlotType SourceEquipmentSlot;
        public readonly int Quantity;
        public readonly MMOSlotDragOperation RequestedOperation;

        public bool IsValid => Content != null && Category != MMOSlotContentCategory.None;
        public MMOAbilityDefinition Ability => Content as MMOAbilityDefinition;
        public MMOItemDefinition Item => Content as MMOItemDefinition;
        public MMOActionBarPresenter SourceActionBar => SourceContext as MMOActionBarPresenter;
        public MMOInventoryContainer SourceInventory => SourceContext as MMOInventoryContainer;
        public MMOCharacterEquipment SourceEquipment => SourceContext as MMOCharacterEquipment;
        public bool FromActionBar => SourceActionBar != null && SourceSlotIndex >= 0;
        public bool FromInventory => SourceInventory != null && SourceSlotIndex >= 0;
        public bool FromEquipment => SourceEquipment != null && Item != null;

        private MMOSlotDragPayload(
            MMOSlotContentCategory category,
            Object content,
            Object sourceContext,
            int sourceSlotIndex,
            MMOEquipmentSlotType sourceEquipmentSlot,
            int quantity,
            MMOSlotDragOperation requestedOperation)
        {
            Category = category;
            Content = content;
            SourceContext = sourceContext;
            SourceSlotIndex = sourceSlotIndex;
            SourceEquipmentSlot = sourceEquipmentSlot;
            Quantity = Mathf.Max(0, quantity);
            RequestedOperation = requestedOperation;
        }

        public static MMOSlotDragPayload AbilityBinding(
            MMOAbilityDefinition ability,
            MMOActionBarPresenter sourceActionBar = null,
            int sourceSlotIndex = -1)
        {
            return new MMOSlotDragPayload(
                MMOSlotContentCategory.Ability,
                ability,
                sourceActionBar,
                sourceSlotIndex,
                default,
                0,
                sourceActionBar != null ? MMOSlotDragOperation.Swap : MMOSlotDragOperation.Assign);
        }

        public static MMOSlotDragPayload InventoryItem(MMOItemDefinition item, MMOInventoryContainer inventory, int sourceSlotIndex, int quantity)
        {
            return new MMOSlotDragPayload(
                MMOSlotContentCategory.Item,
                item,
                inventory,
                sourceSlotIndex,
                default,
                quantity,
                MMOSlotDragOperation.Move);
        }

        public static MMOSlotDragPayload ActionBarItem(MMOItemDefinition item, MMOActionBarPresenter actionBar, int sourceSlotIndex)
        {
            return new MMOSlotDragPayload(
                MMOSlotContentCategory.Item,
                item,
                actionBar,
                sourceSlotIndex,
                default,
                0,
                MMOSlotDragOperation.Swap);
        }

        public static MMOSlotDragPayload EquipmentItem(MMOItemDefinition item, MMOCharacterEquipment equipment, MMOEquipmentSlotType slot)
        {
            return new MMOSlotDragPayload(
                MMOSlotContentCategory.Equipment,
                item,
                equipment,
                -1,
                slot,
                1,
                MMOSlotDragOperation.Unequip);
        }

        public static MMOSlotDragPayload Custom(
            Object content,
            Object sourceContext,
            int sourceSlotIndex,
            int quantity,
            MMOSlotDragOperation operation)
        {
            return new MMOSlotDragPayload(
                MMOSlotContentCategory.Custom,
                content,
                sourceContext,
                sourceSlotIndex,
                default,
                quantity,
                operation);
        }
    }
}
