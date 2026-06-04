using System.Collections.Generic;
using System;
using RPGClone.Characters;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Abilities
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCombatant))]
    public sealed class MMOAbilitySystem : MonoBehaviour
    {
        [SerializeField] private List<MMOAbilityDefinition> startingAbilities = new();

        private readonly Dictionary<MMOAbilityDefinition, float> cooldownReadyTimes = new();
        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;

        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> AbilityFailed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> AbilityUsed;

        public IReadOnlyList<MMOAbilityDefinition> KnownAbilities => startingAbilities;
        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureInitialized();
                return identity;
            }
        }

        public MMOCombatant Combatant
        {
            get
            {
                EnsureInitialized();
                return combatant;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public bool KnowsAbility(MMOAbilityDefinition ability)
        {
            return ability != null && startingAbilities.Contains(ability);
        }

        public void LearnAbility(MMOAbilityDefinition ability)
        {
            if (ability != null && !startingAbilities.Contains(ability))
            {
                startingAbilities.Add(ability);
            }
        }

        public bool TryUseAbility(MMOAbilityDefinition ability, MMOCharacterIdentity target, out string failureReason)
        {
            EnsureInitialized();
            failureReason = string.Empty;
            if (ability == null)
            {
                return Fail(null, target, "No ability was provided.", out failureReason);
            }

            if (!KnowsAbility(ability))
            {
                return Fail(ability, target, $"{identity.DisplayName} does not know {ability.DisplayName}.", out failureReason);
            }

            if (combatant == null || !combatant.IsAlive)
            {
                return Fail(ability, target, $"{identity.DisplayName} cannot act.", out failureReason);
            }

            MMOCharacterIdentity resolvedTarget = ResolveTarget(ability, target);
            if (resolvedTarget == null)
            {
                return Fail(ability, null, "You have no target.", out failureReason);
            }

            MMOCombatant targetCombatant = resolvedTarget.GetComponent<MMOCombatant>();
            if (targetCombatant == null || !targetCombatant.IsAlive)
            {
                return Fail(ability, resolvedTarget, "Invalid target.", out failureReason);
            }

            if (!IsTargetAllowed(ability, resolvedTarget))
            {
                return Fail(ability, resolvedTarget, "Cannot attack that target.", out failureReason);
            }

            float effectiveRange = ability.IsAutoAttack && identity.Stats != null
                ? identity.Stats.MeleeRange
                : ability.Range;
            if (!IsInRange(resolvedTarget, effectiveRange))
            {
                return Fail(ability, resolvedTarget, "Target is too far away.", out failureReason);
            }

            if (IsOnCooldown(ability, out float remainingSeconds))
            {
                return Fail(ability, resolvedTarget, $"{ability.DisplayName} is not ready yet.", out failureReason);
            }

            if (ability.ManaCost > identity.Mana.CurrentValue)
            {
                return Fail(ability, resolvedTarget, "Not enough mana.", out failureReason);
            }

            if (ability.ManaCost > 0)
            {
                identity.Mana.SetCurrent(identity.Mana.CurrentValue - ability.ManaCost);
            }

            ApplyEffects(ability, targetCombatant);
            AbilityUsed?.Invoke(this, ability, resolvedTarget);
            if (ability.CooldownSeconds > 0f)
            {
                cooldownReadyTimes[ability] = Time.time + ability.CooldownSeconds;
            }

            return true;
        }

        public bool IsInRange(MMOCharacterIdentity target, float range)
        {
            if (target == null)
            {
                return false;
            }

            float sqrRange = range * range;
            return (target.transform.position - transform.position).sqrMagnitude <= sqrRange;
        }

        public bool IsOnCooldown(MMOAbilityDefinition ability, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (ability == null || !cooldownReadyTimes.TryGetValue(ability, out float readyTime))
            {
                return false;
            }

            remainingSeconds = Mathf.Max(0f, readyTime - Time.time);
            return remainingSeconds > 0f;
        }

        private MMOCharacterIdentity ResolveTarget(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            return ability.TargetType == MMOAbilityTargetType.Self ? identity : target;
        }

        private bool IsTargetAllowed(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            return ability.TargetType switch
            {
                MMOAbilityTargetType.Self => target == identity,
                MMOAbilityTargetType.Friendly => MMOFactionRules.CanAssist(identity, target),
                MMOAbilityTargetType.Hostile => MMOFactionRules.CanDamage(identity, target),
                MMOAbilityTargetType.AnyCharacter => target != null,
                _ => false
            };
        }

        private void ApplyEffects(MMOAbilityDefinition ability, MMOCombatant target)
        {
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                int amount = effect.CalculateAmount(identity);
                if (effect.EffectType == MMOAbilityEffectType.Heal)
                {
                    target.ApplyHeal(combatant, ability, amount);
                }
                else
                {
                    target.ApplyDamage(combatant, ability, amount);
                }
            }
        }

        private bool Fail(MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason, out string failureReason)
        {
            failureReason = reason;
            AbilityFailed?.Invoke(this, ability, target, reason);
            return false;
        }

        private void EnsureInitialized()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }
        }
    }
}
