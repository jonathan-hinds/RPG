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
        private ActiveCast activeCast;

        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> AbilityFailed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> AbilityUsed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastStarted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastProgressed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> CastInterrupted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> CastCompleted;

        public IReadOnlyList<MMOAbilityDefinition> KnownAbilities => startingAbilities;
        public bool IsCasting => activeCast != null;
        public float CurrentCastNormalized => activeCast == null ? 0f : Mathf.Clamp01((Time.time - activeCast.StartTime) / activeCast.Duration);
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

        private void Update()
        {
            UpdateCast();
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

            if (activeCast != null)
            {
                return Fail(ability, target, "Another action is in progress.", out failureReason);
            }

            MMOCharacterIdentity resolvedTarget = ResolveTarget(ability, target);
            if (ability.TargetType == MMOAbilityTargetType.Friendly && ability.CastOnSelfWhenFriendlyTargetInvalid && !IsFriendlyTarget(resolvedTarget))
            {
                resolvedTarget = identity;
            }

            if (ability.TargetType == MMOAbilityTargetType.Hostile && resolvedTarget == null)
            {
                resolvedTarget = target;
            }

            return TryPrepareAbility(ability, resolvedTarget, out failureReason, out MMOCombatant targetCombatant)
                ? StartOrExecuteAbility(ability, resolvedTarget, targetCombatant)
                : false;
        }

        private bool StartOrExecuteAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant)
        {
            if (ability.CastTimeSeconds > 0f)
            {
                activeCast = new ActiveCast(ability, resolvedTarget, transform.position, Time.time, ability.CastTimeSeconds);
                CastStarted?.Invoke(this, ability, resolvedTarget, ability.CastTimeSeconds);
                return true;
            }

            ExecutePreparedAbility(ability, resolvedTarget, targetCombatant);
            return true;
        }

        private bool TryPrepareAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, out string failureReason, out MMOCombatant targetCombatant)
        {
            failureReason = string.Empty;
            targetCombatant = null;
            if (resolvedTarget == null)
            {
                return Fail(ability, null, "You have no target.", out failureReason);
            }

            targetCombatant = resolvedTarget.GetComponent<MMOCombatant>();
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

            return true;
        }

        private void ExecutePreparedAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant)
        {
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
            remainingSeconds = GetCooldownRemaining(ability);
            return remainingSeconds > 0f;
        }

        public float GetCooldownRemaining(MMOAbilityDefinition ability)
        {
            if (ability == null || !cooldownReadyTimes.TryGetValue(ability, out float readyTime))
            {
                return 0f;
            }

            return Mathf.Max(0f, readyTime - Time.time);
        }

        public float GetCooldownNormalized(MMOAbilityDefinition ability)
        {
            if (ability == null || ability.CooldownSeconds <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetCooldownRemaining(ability) / ability.CooldownSeconds);
        }

        private MMOCharacterIdentity ResolveTarget(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            return ability.TargetType == MMOAbilityTargetType.Self ? identity : target;
        }

        private bool IsFriendlyTarget(MMOCharacterIdentity target)
        {
            return target != null && MMOFactionRules.CanAssist(identity, target);
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
                if (effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier)
                {
                    MMOTemporaryStatModifierReceiver modifierReceiver = target.GetComponent<MMOTemporaryStatModifierReceiver>();
                    if (modifierReceiver == null)
                    {
                        modifierReceiver = target.gameObject.AddComponent<MMOTemporaryStatModifierReceiver>();
                    }

                    modifierReceiver.AddModifier(
                        effect.DurationSeconds,
                        effect.AttackPowerBonus,
                        effect.AttackPowerMultiplier,
                        effect.AttackSpeedMultiplier,
                        effect.HealthRegenMultiplier);
                    continue;
                }

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

        private void UpdateCast()
        {
            if (activeCast == null)
            {
                return;
            }

            if (activeCast.Ability.InterruptOnMovement && (transform.position - activeCast.StartPosition).sqrMagnitude > 0.0004f)
            {
                InterruptCast("Casting interrupted.");
                return;
            }

            CastProgressed?.Invoke(this, activeCast.Ability, activeCast.Target, CurrentCastNormalized);
            if (Time.time - activeCast.StartTime < activeCast.Duration)
            {
                return;
            }

            MMOAbilityDefinition ability = activeCast.Ability;
            MMOCharacterIdentity target = activeCast.Target;
            activeCast = null;

            if (!TryPrepareAbility(ability, target, out string failureReason, out MMOCombatant targetCombatant))
            {
                CastInterrupted?.Invoke(this, ability, target, failureReason);
                return;
            }

            ExecutePreparedAbility(ability, target, targetCombatant);
            CastCompleted?.Invoke(this, ability, target);
        }

        private void InterruptCast(string reason)
        {
            if (activeCast == null)
            {
                return;
            }

            MMOAbilityDefinition ability = activeCast.Ability;
            MMOCharacterIdentity target = activeCast.Target;
            activeCast = null;
            CastInterrupted?.Invoke(this, ability, target, reason);
            AbilityFailed?.Invoke(this, ability, target, reason);
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

        private sealed class ActiveCast
        {
            public readonly MMOAbilityDefinition Ability;
            public readonly MMOCharacterIdentity Target;
            public readonly Vector3 StartPosition;
            public readonly float StartTime;
            public readonly float Duration;

            public ActiveCast(MMOAbilityDefinition ability, MMOCharacterIdentity target, Vector3 startPosition, float startTime, float duration)
            {
                Ability = ability;
                Target = target;
                StartPosition = startPosition;
                StartTime = startTime;
                Duration = Mathf.Max(0.01f, duration);
            }
        }
    }
}
