using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOInventoryItemUseTrigger : MonoBehaviour, IPointerClickHandler
    {
        private MMOInventoryContainer inventory;
        private int slotIndex;

        public void Configure(MMOInventoryContainer newInventory, int newSlotIndex)
        {
            inventory = newInventory;
            slotIndex = newSlotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right || inventory == null)
            {
                return;
            }

            MMOItemStack stack = inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return;
            }

            if (MMOVendorPresenter.TrySellInventorySlot(inventory, slotIndex))
            {
                return;
            }

            MMOCharacterEquipment equipment = player.GetComponent<MMOCharacterEquipment>();
            if (equipment != null && stack.Item.IsEquipment && equipment.TryEquipFromInventory(inventory, slotIndex))
            {
                return;
            }

            MMOConsumableEffectController consumables = player.GetComponent<MMOConsumableEffectController>();
            if (consumables != null && stack.Item.IsConsumable && consumables.TryConsume(stack.Item))
            {
                inventory.TryRemoveItem(stack.Item, 1);
                return;
            }

            MMOQuestLog questLog = player.GetComponent<MMOQuestLog>();
            questLog?.TryBeginUseQuestItem(stack.Item);
        }
    }
}
