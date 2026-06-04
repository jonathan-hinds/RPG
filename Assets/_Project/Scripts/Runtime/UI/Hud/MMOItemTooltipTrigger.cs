using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MMOItemDefinition item;

        public void Configure(MMOItemDefinition newItem)
        {
            item = newItem;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            MMOItemTooltipPresenter.ShowItem(item, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            MMOItemTooltipPresenter.HideItem(item);
        }

        private void OnDisable()
        {
            MMOItemTooltipPresenter.HideItem(item);
        }
    }
}
