using RPGClone.Abilities;
using UnityEngine;

namespace RPGClone.Buffs
{
    public sealed class MMOBuffApplication
    {
        public string BuffId;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public float DurationSeconds;
        public bool BreakOnMovement;
        public int AttackPowerBonus;
        public float AttackPowerMultiplier = 1f;
        public float AttackSpeedMultiplier = 1f;
        public float HealthRegenMultiplier = 1f;
        public float ManaRegenMultiplier = 1f;
        public float DamageTakenAsManaPercent;
        public int RestoreHealthTotal;
        public int RestoreManaTotal;
        public float TickSeconds = 1f;

        public static MMOBuffApplication FromAbility(MMOAbilityDefinition ability, MMOAbilityEffectDefinition effect)
        {
            if (ability == null || effect == null)
            {
                return null;
            }

            return new MMOBuffApplication
            {
                BuffId = ability.AbilityId,
                DisplayName = ability.DisplayName,
                Description = ability.Description,
                Icon = ability.Icon,
                DurationSeconds = effect.DurationSeconds,
                AttackPowerBonus = effect.AttackPowerBonus,
                AttackPowerMultiplier = effect.AttackPowerMultiplier,
                AttackSpeedMultiplier = effect.AttackSpeedMultiplier,
                HealthRegenMultiplier = effect.HealthRegenMultiplier,
                ManaRegenMultiplier = effect.ManaRegenMultiplier,
                DamageTakenAsManaPercent = effect.DamageTakenAsManaPercent
            };
        }
    }
}
