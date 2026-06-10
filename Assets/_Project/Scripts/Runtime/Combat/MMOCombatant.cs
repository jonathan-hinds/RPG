using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Combat
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCombatant : MonoBehaviour
    {
        private static readonly HashSet<MMOCombatant> ActiveCombatantSet = new();

        private MMOCharacterIdentity identity;

        public static event Action<MMOCombatant> CombatantEnabled;
        public static event Action<MMOCombatant> CombatantDisabled;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Damaged;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Healed;
        public event Action<MMOCombatant> Died;
        public event Action<MMOCombatant> CombatActivity;
        public static IReadOnlyCollection<MMOCombatant> ActiveCombatants => ActiveCombatantSet;

        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureInitialized();
                return identity;
            }
        }

        public bool IsAlive
        {
            get
            {
                EnsureInitialized();
                return identity != null && identity.Health.CurrentValue > 0;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (ActiveCombatantSet.Add(this))
            {
                CombatantEnabled?.Invoke(this);
            }
        }

        private void OnDisable()
        {
            if (ActiveCombatantSet.Remove(this))
            {
                CombatantDisabled?.Invoke(this);
            }
        }

        public void ApplyDamage(MMOCombatant source, MMOAbilityDefinition ability, int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            int mitigatedAmount = CalculatePhysicalMitigation(amount);
            MMOCharacterBuffController buffController = GetComponent<MMOCharacterBuffController>();
            int absorbedAmount = buffController != null ? buffController.AbsorbDamageAsMana(mitigatedAmount) : 0;
            int appliedAmount = Mathf.Max(0, mitigatedAmount - absorbedAmount);
            identity.Health.SetCurrent(identity.Health.CurrentValue - appliedAmount);
            source?.CombatActivity?.Invoke(source);
            CombatActivity?.Invoke(this);
            Damaged?.Invoke(source, this, ability, appliedAmount);

            if (identity.Health.CurrentValue <= 0)
            {
                Died?.Invoke(this);
            }
        }

        public void ApplyHeal(MMOCombatant source, MMOAbilityDefinition ability, int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            int missingHealth = identity.Health.MaxValue - identity.Health.CurrentValue;
            int appliedAmount = Mathf.Min(missingHealth, amount);
            if (appliedAmount <= 0)
            {
                return;
            }

            identity.Health.SetCurrent(identity.Health.CurrentValue + appliedAmount);
            Healed?.Invoke(source, this, ability, appliedAmount);
        }

        private int CalculatePhysicalMitigation(int amount)
        {
            EnsureInitialized();
            int armor = identity != null && identity.Stats != null ? identity.Stats.Armor : 0;
            if (armor <= 0)
            {
                return amount;
            }

            float reduction = armor / (armor + 400f + 85f * Mathf.Max(1, identity.Level));
            return Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Mathf.Clamp01(reduction))));
        }

        private void EnsureInitialized()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }
    }
}
