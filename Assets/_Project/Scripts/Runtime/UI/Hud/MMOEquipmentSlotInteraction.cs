using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOEquipmentSlotInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private MMOCharacterEquipment equipment;
        private MMOInventoryContainer inventory;
        private MMOEquipmentSlotType slotType;

        public void Configure(MMOCharacterEquipment newEquipment, MMOInventoryContainer newInventory, MMOEquipmentSlotType newSlotType)
        {
            equipment = newEquipment;
            inventory = newInventory;
            slotType = newSlotType;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            equipment?.TryUnequipToInventory(inventory, slotType);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            MMOItemDefinition item = equipment != null ? equipment.GetEquippedItem(slotType) : null;
            if (item == null)
            {
                return;
            }

            MMOGameTooltipPresenter.HideTooltip();
            MMOActionBarDragState.BeginDrag(
                new MMOActionBarDragPayload(item, equipment, slotType),
                eventData,
                transform,
                item.DisplayName,
                item.Icon);
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
            if (payload.FromInventory)
            {
                equipment?.TryEquipFromInventory(payload.SourceInventory, payload.SourceSlotIndex);
            }

            MMOActionBarDragState.EndDrag();
        }
    }
}
