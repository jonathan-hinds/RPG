using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;

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

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return false;
            }

            if (MMOVendorPresenter.TrySellInventorySlot(inventory, slotIndex))
            {
                return true;
            }

            MMOCharacterEquipment equipment = player.GetComponent<MMOCharacterEquipment>();
            if (equipment != null && stack.Item.IsEquipment && equipment.TryEquipFromInventory(inventory, slotIndex))
            {
                return true;
            }

            MMOConsumableEffectController consumables = player.GetComponent<MMOConsumableEffectController>();
            if (consumables != null && stack.Item.IsConsumable && consumables.TryConsume(stack.Item))
            {
                inventory.TryRemoveItem(stack.Item, 1);
                return true;
            }

            MMOQuestLog questLog = player.GetComponent<MMOQuestLog>();
            return questLog != null && questLog.TryBeginUseQuestItem(stack.Item);
        }
    }
}
