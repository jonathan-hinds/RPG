using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGClone.UI
{
    public sealed class MMOAbilityTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MMOAbilityDefinition ability;

        public void Configure(MMOAbilityDefinition newAbility)
        {
            ability = newAbility;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ability == null)
            {
                return;
            }

            MMOGameTooltipPresenter.ShowAbility(ability, eventData.position);
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
