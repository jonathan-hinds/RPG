using System;
using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Trainers
{
    [Serializable]
    public sealed class MMOTrainerOfferEntry
    {
        [SerializeField] private MMOAbilityDefinition ability;
        [SerializeField] private MMOPlayableClass requiredClass = MMOPlayableClass.Warrior;
        [SerializeField, Min(1)] private int requiredLevel = 3;
        [SerializeField, Min(0)] private int priceCopper = 75;

        public MMOAbilityDefinition Ability => ability;
        public MMOPlayableClass RequiredClass => requiredClass;
        public int RequiredLevel => Mathf.Max(1, requiredLevel);
        public int PriceCopper => Mathf.Max(0, priceCopper);
        public bool IsValid => ability != null;

        public MMOTrainerOfferEntry(MMOAbilityDefinition ability, MMOPlayableClass requiredClass, int requiredLevel, int priceCopper)
        {
            this.ability = ability;
            this.requiredClass = requiredClass;
            this.requiredLevel = Mathf.Max(1, requiredLevel);
            this.priceCopper = Mathf.Max(0, priceCopper);
        }
    }
}
