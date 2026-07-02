using System;
using RPGClone.Abilities;

namespace RPGClone.Combat
{
    public static class MMOCombatEventStream
    {
        public static event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> HealResolved;

        public static void PublishHealResolved(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            HealResolved?.Invoke(source, target, ability, amount);
        }
    }
}
