using System;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Abilities
{
    [Serializable]
    public sealed class MMOAbilityEffectDefinition
    {
        [SerializeField] private MMOAbilityEffectType effectType = MMOAbilityEffectType.Damage;
        [SerializeField] private MMOAbilityAmountSource amountSource = MMOAbilityAmountSource.Flat;
        [SerializeField] private MMODamageSchool damageSchool = MMODamageSchool.Physical;
        [SerializeField, Min(0f)] private float flatAmount = 1f;
        [SerializeField, Min(0f)] private float coefficient = 1f;
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField, Min(0)] private int attackPowerBonus;
        [SerializeField, Min(1f)] private float attackPowerMultiplier = 1f;
        [SerializeField, Min(1f)] private float attackSpeedMultiplier = 1f;
        [SerializeField, Min(1f)] private float healthRegenMultiplier = 1f;

        public MMOAbilityEffectType EffectType => effectType;
        public MMOAbilityAmountSource AmountSource => amountSource;
        public MMODamageSchool DamageSchool => damageSchool;
        public float FlatAmount => flatAmount;
        public float Coefficient => coefficient;
        public float DurationSeconds => durationSeconds;
        public int AttackPowerBonus => attackPowerBonus;
        public float AttackPowerMultiplier => attackPowerMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float HealthRegenMultiplier => healthRegenMultiplier;

        public int CalculateAmount(MMOCharacterIdentity caster)
        {
            MMOCharacterStats stats = caster != null ? caster.Stats : null;
            float amount = flatAmount;

            if (stats != null)
            {
                amount += amountSource switch
                {
                    MMOAbilityAmountSource.WeaponDamage => stats.RollMeleeWeaponDamage() * coefficient,
                    MMOAbilityAmountSource.AttackPower => stats.AttackPower * coefficient,
                    MMOAbilityAmountSource.SpellPower => stats.SpellPower * coefficient,
                    _ => 0f
                };
            }

            return Mathf.Max(0, Mathf.RoundToInt(amount));
        }

        public void Configure(
            MMOAbilityEffectType newEffectType,
            MMOAbilityAmountSource newAmountSource,
            MMODamageSchool newDamageSchool,
            float newFlatAmount,
            float newCoefficient)
        {
            effectType = newEffectType;
            amountSource = newAmountSource;
            damageSchool = newDamageSchool;
            flatAmount = Mathf.Max(0f, newFlatAmount);
            coefficient = Mathf.Max(0f, newCoefficient);
        }

        public void ConfigureTemporaryStatModifier(
            float newDurationSeconds,
            int newAttackPowerBonus,
            float newAttackPowerMultiplier,
            float newAttackSpeedMultiplier,
            float newHealthRegenMultiplier)
        {
            effectType = MMOAbilityEffectType.TemporaryStatModifier;
            amountSource = MMOAbilityAmountSource.Flat;
            damageSchool = MMODamageSchool.Physical;
            flatAmount = 0f;
            coefficient = 0f;
            durationSeconds = Mathf.Max(0.1f, newDurationSeconds);
            attackPowerBonus = Mathf.Max(0, newAttackPowerBonus);
            attackPowerMultiplier = Mathf.Max(1f, newAttackPowerMultiplier);
            attackSpeedMultiplier = Mathf.Max(1f, newAttackSpeedMultiplier);
            healthRegenMultiplier = Mathf.Max(1f, newHealthRegenMultiplier);
        }
    }
}
