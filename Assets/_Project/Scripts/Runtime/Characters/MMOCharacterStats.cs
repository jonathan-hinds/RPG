using System;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOCharacterStats
    {
        private const float AttackPowerDamageDivisor = 14f;
        private const int HealthPerStamina = 10;
        private const int ManaPerIntellect = 15;
        private const float HealthRegenPerSpiritPerSecond = 0.35f;
        private const float ManaRegenPerSpiritPerSecond = 0.18f;

        [Header("Primary")]
        [SerializeField, Min(0)] private int stamina = 10;
        [SerializeField, Min(0)] private int strength = 10;
        [SerializeField, Min(0)] private int agility = 10;
        [SerializeField, Min(0)] private int intellect = 10;
        [SerializeField, Min(0)] private int spirit = 10;

        [Header("Combat")]
        [SerializeField, Min(0)] private int armor;
        [SerializeField, Min(0)] private int attackPower = 8;
        [SerializeField, Min(0)] private int spellPower;
        [SerializeField, Min(0f)] private float meleeMinDamage = 4f;
        [SerializeField, Min(0f)] private float meleeMaxDamage = 7f;
        [SerializeField, Min(0.1f)] private float meleeAttackSpeed = 2f;
        [SerializeField, Min(0.1f)] private float meleeRange = 3f;

        public int Stamina => stamina;
        public int Strength => strength;
        public int Agility => agility;
        public int Intellect => intellect;
        public int Spirit => spirit;
        public int Armor => armor + Agility * 2;
        public int AttackPower => attackPower + Strength * 2 + Mathf.FloorToInt(Agility * 0.5f);
        public int SpellPower => spellPower + Mathf.FloorToInt(Intellect * 0.5f);
        public float MeleeMinDamage => meleeMinDamage;
        public float MeleeMaxDamage => Mathf.Max(meleeMinDamage, meleeMaxDamage);
        public float MeleeAttackSpeed => meleeAttackSpeed;
        public float MeleeRange => meleeRange;
        public int MaxHealthBonus => Stamina * HealthPerStamina;
        public int MaxManaBonus => Intellect * ManaPerIntellect;
        public float HealthRegenPerSecond => Spirit * HealthRegenPerSpiritPerSecond;
        public float ManaRegenPerSecond => Spirit * ManaRegenPerSpiritPerSecond + Intellect * 0.03f;
        public float CriticalStrikeChance => Mathf.Clamp(5f + Agility * 0.03f, 0f, 75f);
        public float DodgeChance => Mathf.Clamp(3f + Agility * 0.05f, 0f, 75f);

        public float RollMeleeWeaponDamage()
        {
            float baseDamage = UnityEngine.Random.Range(MeleeMinDamage, MeleeMaxDamage);
            float attackPowerBonus = AttackPower / AttackPowerDamageDivisor * MeleeAttackSpeed;
            return baseDamage + attackPowerBonus;
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
            float newMeleeMaxDamage,
            float newMeleeAttackSpeed,
            float newMeleeRange)
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
            meleeAttackSpeed = Mathf.Max(0.1f, newMeleeAttackSpeed);
            meleeRange = Mathf.Max(0.1f, newMeleeRange);
        }

        public void CopyFrom(MMOCharacterStats source)
        {
            if (source == null)
            {
                return;
            }

            Configure(
                source.stamina,
                source.strength,
                source.agility,
                source.intellect,
                source.spirit,
                source.armor,
                source.attackPower,
                source.spellPower,
                source.meleeMinDamage,
                source.meleeMaxDamage,
                source.meleeAttackSpeed,
                source.meleeRange);
        }

        public void Add(MMOCharacterStats source)
        {
            if (source == null)
            {
                return;
            }

            Configure(
                stamina + source.stamina,
                strength + source.strength,
                agility + source.agility,
                intellect + source.intellect,
                spirit + source.spirit,
                armor + source.armor,
                attackPower + source.attackPower,
                spellPower + source.spellPower,
                meleeMinDamage + source.meleeMinDamage,
                meleeMaxDamage + source.meleeMaxDamage,
                meleeAttackSpeed,
                meleeRange);
        }

        public void AddValues(
            int staminaBonus,
            int strengthBonus,
            int agilityBonus,
            int intellectBonus,
            int spiritBonus,
            int armorBonus,
            int attackPowerBonus,
            int spellPowerBonus,
            float meleeMinDamageBonus,
            float meleeMaxDamageBonus)
        {
            Configure(
                stamina + staminaBonus,
                strength + strengthBonus,
                agility + agilityBonus,
                intellect + intellectBonus,
                spirit + spiritBonus,
                armor + armorBonus,
                attackPower + attackPowerBonus,
                spellPower + spellPowerBonus,
                meleeMinDamage + meleeMinDamageBonus,
                meleeMaxDamage + meleeMaxDamageBonus,
                meleeAttackSpeed,
                meleeRange);
        }
    }
}
