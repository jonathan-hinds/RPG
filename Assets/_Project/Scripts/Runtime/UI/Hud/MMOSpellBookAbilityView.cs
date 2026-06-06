using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOSpellBookAbilityView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private MMOAbilityDefinition ability;

        public void Configure(MMOAbilityDefinition newAbility)
        {
            ability = newAbility;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ability == null)
            {
                return;
            }

            MMOAbilityTooltipPresenter.HideAbility(ability);
            MMOActionBarDragState.BeginDrag(
                new MMOActionBarDragPayload(ability),
                eventData,
                transform,
                ability.DisplayName,
                ability.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MMOActionBarDragState.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            MMOActionBarDragState.EndDrag();
        }
    }
}
