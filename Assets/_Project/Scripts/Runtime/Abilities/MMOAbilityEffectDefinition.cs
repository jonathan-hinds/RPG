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

        public MMOAbilityEffectType EffectType => effectType;
        public MMOAbilityAmountSource AmountSource => amountSource;
        public MMODamageSchool DamageSchool => damageSchool;
        public float FlatAmount => flatAmount;
        public float Coefficient => coefficient;

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
    }
}
