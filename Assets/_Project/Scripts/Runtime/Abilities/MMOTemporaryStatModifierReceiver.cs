using System.Collections.Generic;
using RPGClone.Buffs;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Abilities
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOTemporaryStatModifierReceiver : MonoBehaviour
    {
        private readonly List<RuntimeModifier> modifiers = new();
        private MMOCharacterIdentity identity;

        private void Awake()
        {
            identity = GetComponent<MMOCharacterIdentity>();
        }

        private void Update()
        {
            float now = Time.time;
            bool changed = false;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].ExpiresAt <= now)
                {
                    modifiers.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                Recalculate();
            }
        }

        public void AddModifier(float durationSeconds, int attackPowerBonus, float attackPowerMultiplier, float attackSpeedMultiplier, float healthRegenMultiplier)
        {
            modifiers.Add(new RuntimeModifier
            {
                ExpiresAt = Time.time + Mathf.Max(0.1f, durationSeconds),
                AttackPowerBonus = Mathf.Max(0, attackPowerBonus),
                AttackPowerMultiplier = Mathf.Max(1f, attackPowerMultiplier),
                AttackSpeedMultiplier = Mathf.Max(1f, attackSpeedMultiplier),
                HealthRegenMultiplier = Mathf.Max(1f, healthRegenMultiplier)
            });
            Recalculate();
        }

        private void Recalculate()
        {
            int attackPowerBonus = 0;
            float attackPowerMultiplier = 1f;
            float attackSpeedMultiplier = 1f;
            float healthRegenMultiplier = 1f;

            foreach (RuntimeModifier modifier in modifiers)
            {
                attackPowerBonus += modifier.AttackPowerBonus;
                attackPowerMultiplier *= modifier.AttackPowerMultiplier;
                attackSpeedMultiplier *= modifier.AttackSpeedMultiplier;
                healthRegenMultiplier *= modifier.HealthRegenMultiplier;
            }

            identity.Stats.SetRuntimeModifiers(attackPowerBonus, attackPowerMultiplier, attackSpeedMultiplier, healthRegenMultiplier, 1f);
        }

        private sealed class RuntimeModifier
        {
            public float ExpiresAt;
            public int AttackPowerBonus;
            public float AttackPowerMultiplier = 1f;
            public float AttackSpeedMultiplier = 1f;
            public float HealthRegenMultiplier = 1f;
        }
    }
}
