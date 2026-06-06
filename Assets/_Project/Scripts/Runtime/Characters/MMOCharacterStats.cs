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

        [NonSerialized] private int runtimeAttackPowerBonus;
        [NonSerialized] private float runtimeAttackPowerMultiplier = 1f;
        [NonSerialized] private float runtimeAttackSpeedMultiplier = 1f;
        [NonSerialized] private float runtimeHealthRegenMultiplier = 1f;
        [NonSerialized] private float runtimeManaRegenMultiplier = 1f;

        public int Stamina => stamina;
        public int Strength => strength;
        public int Agility => agility;
        public int Intellect => intellect;
        public int Spirit => spirit;
        public int Armor => armor + Agility * 2;
        public int AttackPower => Mathf.RoundToInt((attackPower + Strength * 2 + Mathf.FloorToInt(Agility * 0.5f) + runtimeAttackPowerBonus) * Mathf.Max(1f, runtimeAttackPowerMultiplier));
        public int SpellPower => spellPower + Mathf.FloorToInt(Intellect * 0.5f);
        public float MeleeMinDamage => meleeMinDamage;
        public float MeleeMaxDamage => Mathf.Max(meleeMinDamage, meleeMaxDamage);
        public float MeleeAttackSpeed => meleeAttackSpeed / Mathf.Max(1f, runtimeAttackSpeedMultiplier);
        public float MeleeRange => meleeRange;
        public int MaxHealthBonus => Stamina * HealthPerStamina;
        public int MaxManaBonus => Intellect * ManaPerIntellect;
        public float HealthRegenPerSecond => Spirit * HealthRegenPerSpiritPerSecond * Mathf.Max(1f, runtimeHealthRegenMultiplier);
        public float ManaRegenPerSecond => (Spirit * ManaRegenPerSpiritPerSecond + Intellect * 0.03f) * Mathf.Max(1f, runtimeManaRegenMultiplier);
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
            SetRuntimeModifiers(0, 1f, 1f, 1f, 1f);
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

        public void Subtract(MMOCharacterStats source)
        {
            if (source == null)
            {
                return;
            }

            Configure(
                stamina - source.stamina,
                strength - source.strength,
                agility - source.agility,
                intellect - source.intellect,
                spirit - source.spirit,
                armor - source.armor,
                attackPower - source.attackPower,
                spellPower - source.spellPower,
                meleeMinDamage - source.meleeMinDamage,
                meleeMaxDamage - source.meleeMaxDamage,
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

        public void SetRuntimeModifiers(int attackPowerBonusValue, float attackPowerMultiplierValue, float attackSpeedMultiplierValue, float healthRegenMultiplierValue)
        {
            SetRuntimeModifiers(attackPowerBonusValue, attackPowerMultiplierValue, attackSpeedMultiplierValue, healthRegenMultiplierValue, 1f);
        }

        public void SetRuntimeModifiers(int attackPowerBonusValue, float attackPowerMultiplierValue, float attackSpeedMultiplierValue, float healthRegenMultiplierValue, float manaRegenMultiplierValue)
        {
            runtimeAttackPowerBonus = Mathf.Max(0, attackPowerBonusValue);
            runtimeAttackPowerMultiplier = Mathf.Max(1f, attackPowerMultiplierValue);
            runtimeAttackSpeedMultiplier = Mathf.Max(1f, attackSpeedMultiplierValue);
            runtimeHealthRegenMultiplier = Mathf.Max(1f, healthRegenMultiplierValue);
            runtimeManaRegenMultiplier = Mathf.Max(1f, manaRegenMultiplierValue);
        }
    }
}
