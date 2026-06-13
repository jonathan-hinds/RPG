using RPGClone.Abilities;
using RPGClone.Combat;
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
        public bool IsHarmful;
        public int AttackPowerBonus;
        public float AttackPowerMultiplier = 1f;
        public float AttackSpeedMultiplier = 1f;
        public float HealthRegenMultiplier = 1f;
        public float ManaRegenMultiplier = 1f;
        public float MovementSpeedMultiplier = 1f;
        public float DamageTakenAsManaPercent;
        public int RestoreHealthTotal;
        public int RestoreManaTotal;
        public int PeriodicDamageTotal;
        public float TickSeconds = 1f;
        public int MaxStacks = 1;
        public MMOCombatant Source;
        public MMOAbilityDefinition Ability;

        public static MMOBuffApplication FromAbility(MMOAbilityDefinition ability, MMOAbilityEffectDefinition effect, MMOCombatant source = null)
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
                IsHarmful = effect.HarmfulEffect,
                AttackPowerBonus = effect.AttackPowerBonus,
                AttackPowerMultiplier = effect.AttackPowerMultiplier,
                AttackSpeedMultiplier = effect.AttackSpeedMultiplier,
                HealthRegenMultiplier = effect.HealthRegenMultiplier,
                ManaRegenMultiplier = effect.ManaRegenMultiplier,
                MovementSpeedMultiplier = effect.MovementSpeedMultiplier,
                DamageTakenAsManaPercent = effect.DamageTakenAsManaPercent,
                PeriodicDamageTotal = effect.EffectType == MMOAbilityEffectType.PeriodicDamage && source != null ? effect.CalculateAmount(source.Identity) : 0,
                TickSeconds = effect.TickSeconds,
                MaxStacks = effect.StackLimit,
                Source = source,
                Ability = ability
            };
        }
    }
}
