using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Combat
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOWeaponSkillController : MonoBehaviour
    {
        private const int SkillPerLevel = 5;

        [SerializeField] private List<MMOWeaponSkillEntry> weaponSkills = new();
        [SerializeField, Range(0f, 1f)] private float minimumSkillUpChance = 0.05f;
        [SerializeField, Range(0f, 1f)] private float maximumSkillUpChance = 1f;

        private MMOCharacterIdentity identity;

        public event Action<MMOWeaponSkillController, MMOWeaponType, int> SkillIncreased;
        public IReadOnlyList<MMOWeaponSkillEntry> WeaponSkills => weaponSkills;
        public int SkillCap => Mathf.Max(SkillPerLevel, (Identity != null ? Identity.Level : 1) * SkillPerLevel);
        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureReferences();
                return identity;
            }
        }

        private void Awake()
        {
            EnsureReferences();
            ClampSkillsToCap();
        }

        private void OnValidate()
        {
            ClampSkillsToCap();
        }

        public int GetSkill(MMOWeaponType weaponType)
        {
            if (weaponType == MMOWeaponType.None)
            {
                return SkillCap;
            }

            MMOWeaponSkillEntry entry = GetOrCreateEntry(weaponType);
            return Mathf.Clamp(entry.SkillValue, 1, SkillCap);
        }

        public void SetSkill(MMOWeaponType weaponType, int value)
        {
            if (weaponType == MMOWeaponType.None)
            {
                return;
            }

            MMOWeaponSkillEntry entry = GetOrCreateEntry(weaponType);
            entry.SetSkill(Mathf.Clamp(value, 1, SkillCap));
        }

        public void SetSkillToCap(MMOWeaponType weaponType)
        {
            SetSkill(weaponType, SkillCap);
        }

        public void LearnAtCap(IEnumerable<MMOWeaponType> weaponTypes)
        {
            if (weaponTypes == null)
            {
                return;
            }

            foreach (MMOWeaponType weaponType in weaponTypes)
            {
                if (weaponType != MMOWeaponType.None)
                {
                    SetSkillToCap(weaponType);
                }
            }
        }

        public bool TryAwardSkillUp(MMOWeaponType weaponType)
        {
            if (weaponType == MMOWeaponType.None)
            {
                return false;
            }

            MMOWeaponSkillEntry entry = GetOrCreateEntry(weaponType);
            int cap = SkillCap;
            int current = Mathf.Clamp(entry.SkillValue, 1, cap);
            if (current >= cap)
            {
                entry.SetSkill(cap);
                return false;
            }

            float capProgress = Mathf.Clamp01(current / (float)cap);
            float chance = Mathf.Lerp(maximumSkillUpChance, minimumSkillUpChance, capProgress * capProgress);
            if (UnityEngine.Random.value > chance)
            {
                return false;
            }

            entry.SetSkill(current + 1);
            SkillIncreased?.Invoke(this, weaponType, entry.SkillValue);
            return true;
        }

        public void RestoreSkills(IEnumerable<MMOWeaponSkillSaveEntry> savedSkills)
        {
            weaponSkills.Clear();
            if (savedSkills == null)
            {
                return;
            }

            foreach (MMOWeaponSkillSaveEntry savedSkill in savedSkills)
            {
                if (savedSkill.weaponType == MMOWeaponType.None)
                {
                    continue;
                }

                weaponSkills.Add(new MMOWeaponSkillEntry(savedSkill.weaponType, Mathf.Clamp(savedSkill.skillValue, 1, SkillCap)));
            }
        }

        private MMOWeaponSkillEntry GetOrCreateEntry(MMOWeaponType weaponType)
        {
            weaponSkills ??= new List<MMOWeaponSkillEntry>();
            foreach (MMOWeaponSkillEntry entry in weaponSkills)
            {
                if (entry != null && entry.WeaponType == weaponType)
                {
                    return entry;
                }
            }

            MMOWeaponSkillEntry created = new(weaponType, weaponType == MMOWeaponType.Unarmed ? SkillCap : 1);
            weaponSkills.Add(created);
            return created;
        }

        private void ClampSkillsToCap()
        {
            int cap = SkillCap;
            weaponSkills ??= new List<MMOWeaponSkillEntry>();
            foreach (MMOWeaponSkillEntry entry in weaponSkills)
            {
                entry?.SetSkill(Mathf.Clamp(entry.SkillValue, 1, cap));
            }
        }

        private void EnsureReferences()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }
    }

    [Serializable]
    public sealed class MMOWeaponSkillEntry
    {
        [SerializeField] private MMOWeaponType weaponType = MMOWeaponType.Unarmed;
        [SerializeField, Min(1)] private int skillValue = 1;

        public MMOWeaponType WeaponType => weaponType;
        public int SkillValue => Mathf.Max(1, skillValue);

        public MMOWeaponSkillEntry(MMOWeaponType weaponType, int skillValue)
        {
            this.weaponType = weaponType;
            this.skillValue = Mathf.Max(1, skillValue);
        }

        public void SetSkill(int value)
        {
            skillValue = Mathf.Max(1, value);
        }
    }

    [Serializable]
    public sealed class MMOWeaponSkillSaveEntry
    {
        public MMOWeaponType weaponType;
        public int skillValue;
    }
}
