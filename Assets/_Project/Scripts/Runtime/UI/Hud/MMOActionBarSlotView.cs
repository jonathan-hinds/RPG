using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOActionBarSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private MMOActionBarPresenter presenter;
        private int slotIndex = -1;

        public void Configure(MMOActionBarPresenter newPresenter, int newSlotIndex)
        {
            presenter = newPresenter;
            slotIndex = newSlotIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            presenter?.BeginSlotDrag(slotIndex, eventData, transform);
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
            presenter?.AcceptDrop(slotIndex, MMOActionBarDragState.Current);
            MMOActionBarDragState.EndDrag();
        }
    }
}
