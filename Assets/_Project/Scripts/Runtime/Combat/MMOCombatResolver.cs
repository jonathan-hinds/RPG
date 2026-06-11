using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Combat
{
    public static class MMOCombatResolver
    {
        private const float AttackPowerDamageDivisor = 14f;
        private const float BaseMissChance = 5f;
        private const float BaseBlockChance = 5f;
        private const float UnarmedMinDamage = 1f;
        private const float UnarmedMaxDamage = 2f;
        private const float UnarmedSpeedSeconds = 2f;

        public static int ApplyWeaponDamage(
            MMOCombatant source,
            MMOCombatant target,
            MMOAbilityDefinition ability,
            MMOAbilityEffectDefinition effect)
        {
            if (source == null || target == null || source.Identity == null || target.Identity == null || effect == null)
            {
                return 0;
            }

            MMOWeaponSnapshot weapon = GetWeaponSnapshot(source.Identity);
            int weaponSkill = GetWeaponSkill(source.Identity, weapon.WeaponType);
            int targetDefense = GetDefenseSkill(target.Identity);
            if (RollMiss(weaponSkill, targetDefense))
            {
                target.NotifyMiss(source, ability);
                return 0;
            }

            float amount = effect.FlatAmount + CalculateWeaponDamage(source.Identity, weapon, effect.Coefficient, weaponSkill);
            int roundedAmount = Mathf.Max(0, Mathf.RoundToInt(amount));
            int blockedAmount = TryApplyBlock(source.Identity, target.Identity, weaponSkill, ref roundedAmount);
            if (blockedAmount > 0)
            {
                target.NotifyBlock(source, ability, blockedAmount);
            }

            if (roundedAmount > 0)
            {
                target.ApplyDamage(source, ability, roundedAmount);
            }

            MMOWeaponSkillController skillController = GetOrAddWeaponSkills(source.Identity);
            skillController?.TryAwardSkillUp(weapon.WeaponType);
            return roundedAmount;
        }

        public static MMOWeaponSnapshot GetWeaponSnapshot(MMOCharacterIdentity identity)
        {
            MMOCharacterEquipment equipment = identity != null ? identity.GetComponent<MMOCharacterEquipment>() : null;
            MMOItemDefinition mainHand = equipment != null ? equipment.GetEquippedItem(MMOEquipmentSlotType.MainHand) : null;
            if (mainHand != null && mainHand.IsWeapon)
            {
                return new MMOWeaponSnapshot(mainHand.WeaponType, mainHand.WeaponMinDamage, mainHand.WeaponMaxDamage, mainHand.WeaponSpeedSeconds);
            }

            return new MMOWeaponSnapshot(MMOWeaponType.Unarmed, UnarmedMinDamage, UnarmedMaxDamage, UnarmedSpeedSeconds);
        }

        public static float GetAttackSpeed(MMOCharacterIdentity identity)
        {
            MMOWeaponSnapshot weapon = GetWeaponSnapshot(identity);
            float multiplier = identity != null && identity.Stats != null
                ? identity.Stats.AttackSpeedMultiplier
                : 1f;
            return weapon.SpeedSeconds / Mathf.Max(1f, multiplier);
        }

        public static bool CanBlock(MMOCharacterIdentity identity)
        {
            if (identity == null)
            {
                return false;
            }

            MMOCharacterCustomization customization = identity.GetComponent<MMOCharacterCustomization>();
            MMOPlayableClass characterClass = customization != null ? customization.CharacterClass : MMOPlayableClass.Warrior;
            if (characterClass != MMOPlayableClass.Warrior && characterClass != MMOPlayableClass.Shaman)
            {
                return false;
            }

            MMOCharacterEquipment equipment = identity.GetComponent<MMOCharacterEquipment>();
            MMOItemDefinition shield = equipment != null ? equipment.GetEquippedItem(MMOEquipmentSlotType.OffHand) : null;
            return shield != null && shield.IsShield;
        }

        public static int GetBlockValue(MMOCharacterIdentity identity)
        {
            if (!CanBlock(identity))
            {
                return 0;
            }

            MMOCharacterEquipment equipment = identity.GetComponent<MMOCharacterEquipment>();
            MMOItemDefinition shield = equipment != null ? equipment.GetEquippedItem(MMOEquipmentSlotType.OffHand) : null;
            int shieldValue = shield != null ? shield.ShieldBlockValue : 0;
            int strengthValue = identity.Stats != null ? Mathf.FloorToInt(identity.Stats.Strength / 20f) : 0;
            return Mathf.Max(0, shieldValue + strengthValue);
        }

        private static float CalculateWeaponDamage(MMOCharacterIdentity source, MMOWeaponSnapshot weapon, float coefficient, int weaponSkill)
        {
            MMOCharacterStats stats = source.Stats;
            float baseDamage = Random.Range(weapon.MinDamage, weapon.MaxDamage);
            float attackPowerBonus = stats != null ? stats.AttackPower / AttackPowerDamageDivisor * weapon.SpeedSeconds : 0f;
            float skillMultiplier = Mathf.Clamp(weaponSkill / (float)Mathf.Max(1, GetDefenseSkill(source)), 0.5f, 1.15f);
            return (baseDamage + attackPowerBonus) * Mathf.Max(0f, coefficient) * skillMultiplier;
        }

        private static bool RollMiss(int weaponSkill, int defenseSkill)
        {
            float missChance = CalculateMissChance(weaponSkill, defenseSkill);
            return Random.value < missChance / 100f;
        }

        private static float CalculateMissChance(int weaponSkill, int defenseSkill)
        {
            int difference = Mathf.Max(0, defenseSkill - weaponSkill);
            float chance = difference <= 10
                ? BaseMissChance + difference * 0.1f
                : BaseMissChance + 1f + (difference - 10) * 0.4f;
            return Mathf.Clamp(chance, 0f, 60f);
        }

        private static int TryApplyBlock(MMOCharacterIdentity source, MMOCharacterIdentity target, int attackerWeaponSkill, ref int amount)
        {
            if (amount <= 0 || !CanBlock(target))
            {
                return 0;
            }

            int shieldSkill = GetWeaponSkill(target, MMOWeaponType.Shield);
            float blockChance = BaseBlockChance + (shieldSkill - attackerWeaponSkill) * 0.1f;
            blockChance = Mathf.Clamp(blockChance, 0f, 75f);
            if (Random.value >= blockChance / 100f)
            {
                return 0;
            }

            int blockedAmount = Mathf.Min(amount, GetBlockValue(target));
            amount = Mathf.Max(0, amount - blockedAmount);
            MMOWeaponSkillController targetSkillController = GetOrAddWeaponSkills(target);
            targetSkillController?.TryAwardSkillUp(MMOWeaponType.Shield);
            return blockedAmount;
        }

        private static int GetWeaponSkill(MMOCharacterIdentity identity, MMOWeaponType weaponType)
        {
            MMOWeaponSkillController skills = GetOrAddWeaponSkills(identity);
            if (skills == null)
            {
                return GetDefenseSkill(identity);
            }

            return skills.GetSkill(weaponType);
        }

        private static MMOWeaponSkillController GetOrAddWeaponSkills(MMOCharacterIdentity identity)
        {
            if (identity == null)
            {
                return null;
            }

            MMOWeaponSkillController skills = identity.GetComponent<MMOWeaponSkillController>();
            return skills != null ? skills : identity.gameObject.AddComponent<MMOWeaponSkillController>();
        }

        private static int GetDefenseSkill(MMOCharacterIdentity identity)
        {
            return Mathf.Max(5, (identity != null ? identity.Level : 1) * 5);
        }
    }

    public readonly struct MMOWeaponSnapshot
    {
        public readonly MMOWeaponType WeaponType;
        public readonly float MinDamage;
        public readonly float MaxDamage;
        public readonly float SpeedSeconds;

        public MMOWeaponSnapshot(MMOWeaponType weaponType, float minDamage, float maxDamage, float speedSeconds)
        {
            WeaponType = weaponType;
            MinDamage = Mathf.Max(0f, minDamage);
            MaxDamage = Mathf.Max(MinDamage, maxDamage);
            SpeedSeconds = Mathf.Max(0.1f, speedSeconds);
        }
    }
}
