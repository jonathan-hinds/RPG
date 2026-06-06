using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Abilities
{
    [CreateAssetMenu(menuName = "RPG Clone/Abilities/Ability Catalog", fileName = "AbilityCatalog")]
    public sealed class MMOAbilityCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOAbilityDefinition> abilities = new();

        public IReadOnlyList<MMOAbilityDefinition> Abilities => abilities;

        public void Configure(IEnumerable<MMOAbilityDefinition> newAbilities)
        {
            abilities = newAbilities != null ? new List<MMOAbilityDefinition>(newAbilities) : new List<MMOAbilityDefinition>();
        }

        public MMOAbilityDefinition FindById(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            foreach (MMOAbilityDefinition ability in abilities)
            {
                if (ability != null && ability.AbilityId == abilityId)
                {
                    return ability;
                }
            }

            return null;
        }
    }
}
