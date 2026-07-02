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

        [SerializeField, Min(0.1f)] private float combatDropDelaySeconds = 5f;

        private readonly HashSet<MMOCombatant> combatOpponents = new();
        private MMOCharacterIdentity identity;
        private float lastCombatActivityTime = float.NegativeInfinity;
        private bool inCombat;

        public static event Action<MMOCombatant> CombatantEnabled;
        public static event Action<MMOCombatant> CombatantDisabled;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Damaged;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> CriticallyDamaged;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> CriticalDamageDealt;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Healed;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition> Missed;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Blocked;
        public event Action<MMOCombatant> Died;
        public event Action<MMOCombatant> CombatActivity;
        public event Action<MMOCombatant, bool> CombatStateChanged;
        public static IReadOnlyCollection<MMOCombatant> ActiveCombatants => ActiveCombatantSet;
        public bool IsInCombat => inCombat;

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
            ForceLeaveCombat();
            if (ActiveCombatantSet.Remove(this))
            {
                CombatantDisabled?.Invoke(this);
            }
        }

        private void Update()
        {
            if (!inCombat)
            {
                return;
            }

            if (!IsAlive)
            {
                ForceLeaveCombat();
                return;
            }

            PruneCombatOpponents();
            if (combatOpponents.Count == 0 && Time.time - lastCombatActivityTime >= combatDropDelaySeconds)
            {
                SetInCombat(false);
            }
        }

        public void ApplyDamage(MMOCombatant source, MMOAbilityDefinition ability, int amount, bool isCritical = false)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            if (isCritical)
            {
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * 2f));
            }

            int mitigatedAmount = CalculatePhysicalMitigation(amount);
            MMOCharacterBuffController buffController = GetComponent<MMOCharacterBuffController>();
            int absorbedAmount = buffController != null ? buffController.AbsorbDamageAsMana(mitigatedAmount) : 0;
            int appliedAmount = Mathf.Max(0, mitigatedAmount - absorbedAmount);
            identity.Health.SetCurrent(identity.Health.CurrentValue - appliedAmount);
            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Damaged?.Invoke(source, this, ability, appliedAmount);
            if (isCritical && appliedAmount > 0)
            {
                CriticallyDamaged?.Invoke(source, this, ability, appliedAmount);
                source?.CriticalDamageDealt?.Invoke(source, this, ability, appliedAmount);
            }

            if (identity.Health.CurrentValue <= 0)
            {
                ForceLeaveCombat();
                Died?.Invoke(this);
            }
        }

        public void NotifyMiss(MMOCombatant source, MMOAbilityDefinition ability)
        {
            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Missed?.Invoke(source, this, ability);
        }

        public void NotifyBlock(MMOCombatant source, MMOAbilityDefinition ability, int blockedAmount)
        {
            if (blockedAmount <= 0)
            {
                return;
            }

            Blocked?.Invoke(source, this, ability, blockedAmount);
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
            MMOCombatEventStream.PublishHealResolved(source, this, ability, appliedAmount);
        }

        public void RegisterCombatActivity(MMOCombatant opponent = null)
        {
            lastCombatActivityTime = Time.time;
            AddCombatOpponent(opponent);
            SetInCombat(true);
            CombatActivity?.Invoke(this);
        }

        public void EngageCombatWith(MMOCombatant opponent)
        {
            if (opponent == null || opponent == this)
            {
                RegisterCombatActivity();
                return;
            }

            RegisterCombatActivity(opponent);
            opponent.RegisterCombatActivity(this);
        }

        public void DisengageCombatWith(MMOCombatant opponent)
        {
            if (opponent == null)
            {
                return;
            }

            combatOpponents.Remove(opponent);
            opponent.combatOpponents.Remove(this);
            lastCombatActivityTime = Time.time;
            opponent.lastCombatActivityTime = Time.time;
        }

        private void AddCombatOpponent(MMOCombatant opponent)
        {
            if (opponent == null || opponent == this || !opponent.IsAlive)
            {
                return;
            }

            combatOpponents.Add(opponent);
        }

        private void PruneCombatOpponents()
        {
            combatOpponents.RemoveWhere(opponent => opponent == null || !opponent.IsAlive || !opponent.isActiveAndEnabled);
        }

        private void ForceLeaveCombat()
        {
            if (combatOpponents.Count > 0)
            {
                foreach (MMOCombatant opponent in combatOpponents)
                {
                    if (opponent != null)
                    {
                        opponent.combatOpponents.Remove(this);
                    }
                }

                combatOpponents.Clear();
            }

            SetInCombat(false);
        }

        private void SetInCombat(bool value)
        {
            if (inCombat == value)
            {
                return;
            }

            inCombat = value;
            CombatStateChanged?.Invoke(this, inCombat);
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
