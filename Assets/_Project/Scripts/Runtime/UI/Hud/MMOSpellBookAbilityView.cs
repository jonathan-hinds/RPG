using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOSpellBookAbilityView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private MMOAbilityDefinition ability;
        private MMOSlotView sourceSlotView;

        public void Configure(MMOAbilityDefinition newAbility, MMOSlotView newSourceSlotView = null)
        {
            ability = newAbility;
            sourceSlotView = newSourceSlotView;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ability == null)
            {
                return;
            }

            MMOAbilityTooltipPresenter.HideAbility(ability);
            MMOSlotDragState.BeginDrag(
                MMOSlotDragPayload.AbilityBinding(ability),
                eventData,
                sourceSlotView != null ? sourceSlotView.transform : transform,
                ability.DisplayName,
                ability.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MMOSlotDragState.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            MMOSlotDragState.EndDrag();
        }
    }
}
