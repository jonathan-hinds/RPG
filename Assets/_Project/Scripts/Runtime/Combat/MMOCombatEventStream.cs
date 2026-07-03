using System;
using RPGClone.Abilities;

namespace RPGClone.Combat
{
    public static class MMOCombatEventStream
    {
        public static event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> HealResolved;
        public static event Action<CombatEventRecord, MMOCombatant, MMOCombatant, MMOAbilityDefinition> CombatEventResolved;

        public static void PublishHealResolved(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            HealResolved?.Invoke(source, target, ability, amount);
        }

        public static void PublishCombatEvent(
            CombatEventRecord record,
            MMOCombatant source,
            MMOCombatant target,
            MMOAbilityDefinition ability)
        {
            if (record == null)
            {
                return;
            }

            CombatEventResolved?.Invoke(record, source, target, ability);
        }
    }
}
