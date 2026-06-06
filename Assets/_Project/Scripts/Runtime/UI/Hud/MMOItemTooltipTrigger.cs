using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public sealed class MMOItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MMOItemDefinition item;

        public static MMOItemTooltipTrigger Bind(GameObject target, MMOItemDefinition item)
        {
            if (target == null)
            {
                return null;
            }

            MMOItemTooltipTrigger trigger = target.GetComponent<MMOItemTooltipTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<MMOItemTooltipTrigger>();
            }

            trigger.Configure(item);
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = item != null;
            }

            return trigger;
        }

        public void Configure(MMOItemDefinition newItem)
        {
            item = newItem;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (item == null)
            {
                return;
            }

            MMOGameTooltipPresenter.ShowItem(item, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            MMOGameTooltipPresenter.HideTooltip();
        }

        private void OnDisable()
        {
            MMOGameTooltipPresenter.HideTooltip();
        }
    }
}
