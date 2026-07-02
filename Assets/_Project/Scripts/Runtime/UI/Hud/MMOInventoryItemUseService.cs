using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Services;

namespace RPGClone.UI
{
    public static class MMOInventoryItemUseService
    {
        public static bool TryUseItem(MMOInventoryContainer inventory, MMOItemDefinition item)
        {
            if (inventory == null || item == null || !inventory.TryFindFirstSlotContaining(item, out int slotIndex))
            {
                return false;
            }

            return TryUseSlot(inventory, slotIndex);
        }

        public static bool TryUseSlot(MMOInventoryContainer inventory, int slotIndex)
        {
            if (inventory == null)
            {
                return false;
            }

            MMOItemStack stack = inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty)
            {
                return false;
            }

            if (!MMOInteractionContext.TryCreateForLocalPlayer(out MMOInteractionContext context))
            {
                return false;
            }

            if (MMOVendorPresenter.TrySellInventorySlot(inventory, slotIndex))
            {
                return true;
            }

            MMOCharacterEquipment equipment = context.ActorObject.GetComponent<MMOCharacterEquipment>();
            if (equipment != null && stack.Item.IsEquipment && equipment.TryEquipFromInventory(inventory, slotIndex))
            {
                return true;
            }

            MMOConsumableEffectController consumables = context.ActorObject.GetComponent<MMOConsumableEffectController>();
            if (consumables != null && stack.Item.IsConsumable && consumables.TryConsume(stack.Item))
            {
                inventory.TryRemoveItem(stack.Item, 1);
                return true;
            }

            return context.QuestLog != null && context.QuestLog.TryBeginUseQuestItem(stack.Item);
        }
    }
}
