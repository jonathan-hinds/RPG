using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOBagSlotInteraction : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IMMOSlotDropTarget
    {
        private MMOBagBarPresenter bagBar;
        private MMOInventoryContainer inventory;
        private int bagSlotIndex;

        public void Configure(
            MMOBagBarPresenter newBagBar,
            MMOInventoryContainer newInventory,
            int newBagSlotIndex)
        {
            bagBar = newBagBar;
            inventory = newInventory;
            bagSlotIndex = newBagSlotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            bagBar?.ToggleBag(bagSlotIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            MMOItemDefinition bag = bagSlotIndex >= 0 && inventory != null
                ? inventory.GetEquippedBag(bagSlotIndex)
                : null;
            if (bag == null)
            {
                return;
            }

            MMOGameTooltipPresenter.HideTooltip();
            MMOSlotDragState.BeginDrag(
                MMOSlotDragPayload.EquippedBag(bag, bagBar, bagSlotIndex),
                eventData,
                transform,
                bag.DisplayName,
                bag.Icon);
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
            if (bagSlotIndex >= 0 && inventory != null && payload.FromInventory && payload.SourceInventory == inventory)
            {
                inventory.TryEquipBagFromInventory(payload.SourceSlotIndex, bagSlotIndex);
            }

            MMOSlotDragState.EndDrag();
        }

        public MMOSlotDropState EvaluateDrop(MMOSlotDragPayload payload)
        {
            if (bagSlotIndex < 0
                || inventory == null
                || !payload.FromInventory
                || payload.SourceInventory != inventory
                || payload.Item == null
                || !payload.Item.IsContainer)
            {
                return MMOSlotDropState.Invalid;
            }

            return inventory.CanEquipBagFromInventory(payload.SourceSlotIndex, bagSlotIndex)
                ? MMOSlotDropState.Valid
                : MMOSlotDropState.Invalid;
        }
    }
}
