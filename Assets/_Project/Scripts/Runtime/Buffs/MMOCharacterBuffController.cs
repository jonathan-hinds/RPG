using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Buffs
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCharacterBuffController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stationarySpeedThreshold = 0.05f;

        private readonly List<MMOActiveBuff> activeBuffs = new();
        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;
        private MMOPlayerMotor motor;

        public event Action<MMOCharacterBuffController> BuffsChanged;
        public event Action<MMOCharacterBuffController> BuffsUpdated;
        public event Action<MMOCharacterBuffController, int> DamageAbsorbedAsMana;
        public IReadOnlyList<MMOActiveBuff> ActiveBuffs => activeBuffs;
        public bool IsMovementPrevented
        {
            get
            {
                foreach (MMOActiveBuff buff in activeBuffs)
                {
                    if (buff.PreventsMovement)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            EnsureReferences();
            bool changed = RemoveExpiredOrBrokenBuffs();
            TickPeriodicBuffs();
            if (changed)
            {
                RecalculateRuntimeModifiers();
                BuffsChanged?.Invoke(this);
            }

            if (activeBuffs.Count > 0)
            {
                BuffsUpdated?.Invoke(this);
            }
        }

        public MMOActiveBuff ApplyBuff(MMOBuffApplication application)
        {
            if (application == null)
            {
                return null;
            }

            EnsureReferences();
            string buffId = string.IsNullOrWhiteSpace(application.BuffId) ? application.DisplayName : application.BuffId;
            MMOActiveBuff existingBuff = FindBuff(buffId);
            if (existingBuff != null && application.MaxStacks > 1)
            {
                existingBuff.RefreshStack(application);
                RecalculateRuntimeModifiers();
                BuffsChanged?.Invoke(this);
                return existingBuff;
            }

            RemoveBuff(buffId, false);
            MMOActiveBuff buff = new(application);
            activeBuffs.Add(buff);
            RecalculateRuntimeModifiers();
            BuffsChanged?.Invoke(this);
            return buff;
        }

        public bool ApplyTemporaryModifiers(MMOAbilityDefinition ability, MMOCombatant source)
        {
            if (ability == null)
            {
                return false;
            }

            bool applied = false;
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect == null || effect.EffectType != MMOAbilityEffectType.TemporaryStatModifier)
                {
                    continue;
                }

                ApplyBuff(MMOBuffApplication.FromAbility(ability, effect, source));
                applied = true;
            }

            return applied;
        }

        public void RemoveBuff(string buffId)
        {
            if (RemoveBuff(buffId, true))
            {
                RecalculateRuntimeModifiers();
                BuffsChanged?.Invoke(this);
            }
        }

        public MMOActiveBuff FindBuff(string buffId)
        {
            if (string.IsNullOrWhiteSpace(buffId))
            {
                return null;
            }

            foreach (MMOActiveBuff buff in activeBuffs)
            {
                if (buff.BuffId == buffId)
                {
                    return buff;
                }
            }

            return null;
        }

        public int AbsorbDamageAsMana(int incomingDamage)
        {
            if (incomingDamage <= 0 || identity == null || identity.Mana.MaxValue <= 0)
            {
                return 0;
            }

            float absorbPercent = 0f;
            foreach (MMOActiveBuff buff in activeBuffs)
            {
                absorbPercent += buff.DamageTakenAsManaPercent;
            }

            int absorbed = Mathf.Clamp(Mathf.RoundToInt(incomingDamage * Mathf.Clamp01(absorbPercent)), 0, incomingDamage);
            if (absorbed <= 0)
            {
                return 0;
            }

            identity.Mana.SetCurrent(identity.Mana.CurrentValue + absorbed);
            DamageAbsorbedAsMana?.Invoke(this, absorbed);
            return absorbed;
        }

        public int RemoveHarmfulEffectsFrom(MMOCombatant source)
        {
            int removed = 0;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                MMOActiveBuff buff = activeBuffs[i];
                if (!buff.IsHarmful || buff.Source != source)
                {
                    continue;
                }

                activeBuffs.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                RecalculateRuntimeModifiers();
                BuffsChanged?.Invoke(this);
            }

            return removed;
        }

        public void NotifyReplicatedDamageAbsorbedAsMana(int absorbedAmount)
        {
            if (absorbedAmount > 0)
            {
                DamageAbsorbedAsMana?.Invoke(this, absorbedAmount);
            }
        }

        public int CalculateMeleeAttackBonusDamage()
        {
            if (identity == null)
            {
                EnsureReferences();
            }

            if (identity == null || identity.Mana.MaxValue <= 0)
            {
                return 0;
            }

            float maximumManaPercent = 0f;
            foreach (MMOActiveBuff buff in activeBuffs)
            {
                maximumManaPercent += buff.MeleeDamageFromMaximumManaPercent;
            }

            return Mathf.Max(0, Mathf.RoundToInt(identity.Mana.MaxValue * maximumManaPercent));
        }

        private bool RemoveBuff(string buffId, bool stopAfterFirst)
        {
            bool removed = false;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                if (activeBuffs[i].BuffId != buffId)
                {
                    continue;
                }

                activeBuffs.RemoveAt(i);
                removed = true;
                if (stopAfterFirst)
                {
                    break;
                }
            }

            return removed;
        }

        private bool RemoveExpiredOrBrokenBuffs()
        {
            bool moving = IsMoving();
            bool changed = false;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                MMOActiveBuff buff = activeBuffs[i];
                if (!buff.IsExpired && (!buff.BreakOnMovement || !moving))
                {
                    continue;
                }

                activeBuffs.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        private void TickPeriodicBuffs()
        {
            foreach (MMOActiveBuff buff in activeBuffs)
            {
                if (!buff.IsTickReady)
                {
                    continue;
                }

                int healthAmount = buff.ConsumeHealthTick();
                int manaAmount = buff.ConsumeManaTick();
                int damageAmount = buff.ConsumeDamageTick();
                if (healthAmount > 0)
                {
                    identity.Health.SetCurrent(identity.Health.CurrentValue + healthAmount);
                }

                if (manaAmount > 0)
                {
                    identity.Mana.SetCurrent(identity.Mana.CurrentValue + manaAmount);
                }

                if (damageAmount > 0 && combatant != null)
                {
                    combatant.ApplyDamage(buff.Source, buff.Ability, damageAmount);
                }

                buff.ScheduleNextTick();
            }
        }

        private void RecalculateRuntimeModifiers()
        {
            if (identity == null || identity.Stats == null)
            {
                return;
            }

            int attackPowerBonus = 0;
            float attackPowerMultiplier = 1f;
            float attackSpeedMultiplier = 1f;
            float healthRegenMultiplier = 1f;
            float manaRegenMultiplier = 1f;
            float movementSpeedMultiplier = 1f;

            foreach (MMOActiveBuff buff in activeBuffs)
            {
                attackPowerBonus += buff.AttackPowerBonus;
                attackPowerMultiplier *= buff.AttackPowerMultiplier;
                attackSpeedMultiplier *= buff.AttackSpeedMultiplier;
                healthRegenMultiplier *= buff.HealthRegenMultiplier;
                manaRegenMultiplier *= buff.ManaRegenMultiplier;
                movementSpeedMultiplier *= buff.MovementSpeedMultiplier;
            }

            identity.Stats.SetRuntimeModifiers(attackPowerBonus, attackPowerMultiplier, attackSpeedMultiplier, healthRegenMultiplier, manaRegenMultiplier, movementSpeedMultiplier);
        }

        private bool IsMoving()
        {
            return motor != null && motor.CurrentPlanarSpeed > stationarySpeedThreshold;
        }

        private void EnsureReferences()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }

            if (motor == null)
            {
                motor = GetComponent<MMOPlayerMotor>();
            }
        }
    }
}
