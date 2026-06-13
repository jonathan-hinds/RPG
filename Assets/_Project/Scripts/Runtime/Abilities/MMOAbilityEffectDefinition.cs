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
        [SerializeField, Min(0.1f)] private float attackPowerMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float attackSpeedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float healthRegenMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float manaRegenMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float movementSpeedMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float damageTakenAsManaPercent;
        [SerializeField] private bool harmfulEffect;
        [SerializeField, Min(0.1f)] private float tickSeconds = 1f;
        [SerializeField, Min(1)] private int stackLimit = 1;
        [SerializeField, Min(0.1f)] private float chargeSpeed = 18f;
        [SerializeField, Min(0.1f)] private float chargeStopDistance = 2.5f;

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
        public float ManaRegenMultiplier => Mathf.Max(0.1f, manaRegenMultiplier);
        public float MovementSpeedMultiplier => Mathf.Max(0.1f, movementSpeedMultiplier);
        public float DamageTakenAsManaPercent => Mathf.Clamp01(damageTakenAsManaPercent);
        public bool HarmfulEffect => harmfulEffect;
        public float TickSeconds => Mathf.Max(0.1f, tickSeconds);
        public int StackLimit => Mathf.Max(1, stackLimit);
        public float ChargeSpeed => Mathf.Max(0.1f, chargeSpeed);
        public float ChargeStopDistance => Mathf.Max(0.1f, chargeStopDistance);

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
            harmfulEffect = effectType == MMOAbilityEffectType.Damage || effectType == MMOAbilityEffectType.PeriodicDamage;
            stackLimit = 1;
        }

        public void ConfigureTemporaryStatModifier(
            float newDurationSeconds,
            int newAttackPowerBonus,
            float newAttackPowerMultiplier,
            float newAttackSpeedMultiplier,
            float newHealthRegenMultiplier)
        {
            ConfigureTemporaryStatModifier(
                newDurationSeconds,
                newAttackPowerBonus,
                newAttackPowerMultiplier,
                newAttackSpeedMultiplier,
                newHealthRegenMultiplier,
                1f,
                0f);
        }

        public void ConfigureTemporaryStatModifier(
            float newDurationSeconds,
            int newAttackPowerBonus,
            float newAttackPowerMultiplier,
            float newAttackSpeedMultiplier,
            float newHealthRegenMultiplier,
            float newManaRegenMultiplier,
            float newDamageTakenAsManaPercent)
        {
            ConfigureTemporaryStatModifier(
                newDurationSeconds,
                newAttackPowerBonus,
                newAttackPowerMultiplier,
                newAttackSpeedMultiplier,
                newHealthRegenMultiplier,
                newManaRegenMultiplier,
                newDamageTakenAsManaPercent,
                1f,
                false);
        }

        public void ConfigureTemporaryStatModifier(
            float newDurationSeconds,
            int newAttackPowerBonus,
            float newAttackPowerMultiplier,
            float newAttackSpeedMultiplier,
            float newHealthRegenMultiplier,
            float newManaRegenMultiplier,
            float newDamageTakenAsManaPercent,
            float newMovementSpeedMultiplier,
            bool newHarmfulEffect)
        {
            effectType = MMOAbilityEffectType.TemporaryStatModifier;
            amountSource = MMOAbilityAmountSource.Flat;
            damageSchool = MMODamageSchool.Physical;
            flatAmount = 0f;
            coefficient = 0f;
            durationSeconds = Mathf.Max(0.1f, newDurationSeconds);
            attackPowerBonus = Mathf.Max(0, newAttackPowerBonus);
            attackPowerMultiplier = Mathf.Max(0.1f, newAttackPowerMultiplier);
            attackSpeedMultiplier = Mathf.Max(0.1f, newAttackSpeedMultiplier);
            healthRegenMultiplier = Mathf.Max(0.1f, newHealthRegenMultiplier);
            manaRegenMultiplier = Mathf.Max(0.1f, newManaRegenMultiplier);
            damageTakenAsManaPercent = Mathf.Clamp01(newDamageTakenAsManaPercent);
            movementSpeedMultiplier = Mathf.Max(0.1f, newMovementSpeedMultiplier);
            harmfulEffect = newHarmfulEffect;
            tickSeconds = 1f;
            stackLimit = 1;
        }

        public void ConfigurePeriodicDamage(
            float newDurationSeconds,
            float newTickSeconds,
            MMOAbilityAmountSource newAmountSource,
            MMODamageSchool newDamageSchool,
            float newFlatAmount,
            float newCoefficient)
        {
            ConfigurePeriodicDamage(newDurationSeconds, newTickSeconds, newAmountSource, newDamageSchool, newFlatAmount, newCoefficient, 1);
        }

        public void ConfigurePeriodicDamage(
            float newDurationSeconds,
            float newTickSeconds,
            MMOAbilityAmountSource newAmountSource,
            MMODamageSchool newDamageSchool,
            float newFlatAmount,
            float newCoefficient,
            int newStackLimit)
        {
            Configure(MMOAbilityEffectType.PeriodicDamage, newAmountSource, newDamageSchool, newFlatAmount, newCoefficient);
            durationSeconds = Mathf.Max(0.1f, newDurationSeconds);
            tickSeconds = Mathf.Max(0.1f, newTickSeconds);
            stackLimit = Mathf.Max(1, newStackLimit);
            harmfulEffect = true;
        }

        public void ConfigureCharge(
            float newSpeed,
            float newStopDistance,
            MMOAbilityAmountSource newAmountSource,
            MMODamageSchool newDamageSchool,
            float newFlatAmount,
            float newCoefficient)
        {
            Configure(MMOAbilityEffectType.Charge, newAmountSource, newDamageSchool, newFlatAmount, newCoefficient);
            chargeSpeed = Mathf.Max(0.1f, newSpeed);
            chargeStopDistance = Mathf.Max(0.1f, newStopDistance);
        }
    }
}
