using System;
using System.Collections.Generic;
using RPGClone.Characters;
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
        private MMOPlayerMotor motor;

        public event Action<MMOCharacterBuffController> BuffsChanged;
        public event Action<MMOCharacterBuffController> BuffsUpdated;
        public IReadOnlyList<MMOActiveBuff> ActiveBuffs => activeBuffs;

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
            RemoveBuff(buffId, false);
            MMOActiveBuff buff = new(application);
            activeBuffs.Add(buff);
            RecalculateRuntimeModifiers();
            BuffsChanged?.Invoke(this);
            return buff;
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
            return absorbed;
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
                if (healthAmount > 0)
                {
                    identity.Health.SetCurrent(identity.Health.CurrentValue + healthAmount);
                }

                if (manaAmount > 0)
                {
                    identity.Mana.SetCurrent(identity.Mana.CurrentValue + manaAmount);
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

            foreach (MMOActiveBuff buff in activeBuffs)
            {
                attackPowerBonus += buff.AttackPowerBonus;
                attackPowerMultiplier *= buff.AttackPowerMultiplier;
                attackSpeedMultiplier *= buff.AttackSpeedMultiplier;
                healthRegenMultiplier *= buff.HealthRegenMultiplier;
                manaRegenMultiplier *= buff.ManaRegenMultiplier;
            }

            identity.Stats.SetRuntimeModifiers(attackPowerBonus, attackPowerMultiplier, attackSpeedMultiplier, healthRegenMultiplier, manaRegenMultiplier);
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

            if (motor == null)
            {
                motor = GetComponent<MMOPlayerMotor>();
            }
        }
    }
}
