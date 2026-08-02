using RPGClone.Inventory;
using RPGClone.PlayerInteraction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOInventoryItemUseTrigger : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IMMOSlotDropTarget
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

            if (MMOPlayerInteractionService.TryHandleInventoryRightClickForTrade(inventory, slotIndex))
            {
                return;
            }

            MMOInventoryItemUseService.TryUseSlot(inventory, slotIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            MMOItemStack stack = inventory != null ? inventory.GetSlot(slotIndex) : null;
            if (stack == null || stack.IsEmpty)
            {
                return;
            }

            MMOGameTooltipPresenter.HideTooltip();
            MMOSlotDragState.BeginDrag(
                MMOSlotDragPayload.InventoryItem(stack.Item, inventory, slotIndex, stack.Quantity),
                eventData,
                transform,
                stack.Item.DisplayName,
                stack.Item.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MMOSlotDragState.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            MMOSlotDragState.EndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            MMOSlotDragPayload payload = MMOSlotDragState.Current;
            if (inventory == null)
            {
                MMOSlotDragState.EndDrag();
                return;
            }

            if (payload.FromInventory && payload.SourceInventory == inventory)
            {
                inventory.TryMoveSlot(payload.SourceSlotIndex, slotIndex);
            }
            else if (payload.FromEquipment)
            {
                payload.SourceEquipment.TryUnequipToInventory(inventory, payload.SourceEquipmentSlot, slotIndex);
            }
            else if (payload.FromEquippedBag)
            {
                payload.SourceBagBar.TryUnequipBag(payload.SourceSlotIndex, slotIndex);
            }

            MMOSlotDragState.EndDrag();
        }

        public MMOSlotDropState EvaluateDrop(MMOSlotDragPayload payload)
        {
            if (inventory == null || !payload.IsValid)
            {
                return MMOSlotDropState.Invalid;
            }

            if (payload.FromInventory && payload.SourceInventory == inventory)
            {
                return MMOSlotDropState.Valid;
            }

            if (payload.FromEquipment)
            {
                return MMOSlotDropState.Valid;
            }

            return payload.FromEquippedBag && payload.SourceBagBar.CanUnequipBag(payload.SourceSlotIndex, slotIndex)
                ? MMOSlotDropState.Valid
                : MMOSlotDropState.Invalid;
        }
    }
}
