using System;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOCharacterStatGrowth
    {
        [Header("Primary")]
        [SerializeField, Min(0)] private int stamina;
        [SerializeField, Min(0)] private int strength;
        [SerializeField, Min(0)] private int agility;
        [SerializeField, Min(0)] private int intellect;
        [SerializeField, Min(0)] private int spirit;

        [Header("Combat")]
        [SerializeField, Min(0)] private int armor;
        [SerializeField, Min(0)] private int attackPower;
        [SerializeField, Min(0)] private int spellPower;
        [SerializeField, Min(0f)] private float meleeMinDamage;
        [SerializeField, Min(0f)] private float meleeMaxDamage;

        public void ApplyTo(MMOCharacterStats stats)
        {
            stats?.AddValues(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, meleeMinDamage, meleeMaxDamage);
        }

        public void Configure(
            int newStamina,
            int newStrength,
            int newAgility,
            int newIntellect,
            int newSpirit,
            int newArmor,
            int newAttackPower,
            int newSpellPower,
            float newMeleeMinDamage,
            float newMeleeMaxDamage)
        {
            stamina = Mathf.Max(0, newStamina);
            strength = Mathf.Max(0, newStrength);
            agility = Mathf.Max(0, newAgility);
            intellect = Mathf.Max(0, newIntellect);
            spirit = Mathf.Max(0, newSpirit);
            armor = Mathf.Max(0, newArmor);
            attackPower = Mathf.Max(0, newAttackPower);
            spellPower = Mathf.Max(0, newSpellPower);
            meleeMinDamage = Mathf.Max(0f, newMeleeMinDamage);
            meleeMaxDamage = Mathf.Max(meleeMinDamage, newMeleeMaxDamage);
        }
    }
}
