using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOInventoryItemUseTrigger : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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
            MMOActionBarDragState.BeginDrag(
                new MMOActionBarDragPayload(stack.Item, inventory, slotIndex),
                eventData,
                transform,
                stack.Item.DisplayName,
                stack.Item.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MMOActionBarDragState.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            MMOActionBarDragState.EndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            MMOActionBarDragPayload payload = MMOActionBarDragState.Current;
            if (!payload.FromInventory || inventory == null || payload.SourceInventory != inventory)
            {
                return;
            }

            inventory.TryMoveSlot(payload.SourceSlotIndex, slotIndex);
            MMOActionBarDragState.EndDrag();
        }
    }
}
