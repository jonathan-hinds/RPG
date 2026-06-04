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

            MMOAbilityDragState.BeginDrag(
                new MMOAbilityDragPayload(ability),
                eventData,
                transform,
                ability.DisplayName,
                ability.Icon);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MMOAbilityDragState.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            MMOAbilityDragState.EndDrag();
        }
    }
}
