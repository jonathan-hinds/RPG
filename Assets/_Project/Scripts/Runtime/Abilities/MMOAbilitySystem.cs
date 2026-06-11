using System.Collections.Generic;
using System;
using System.Collections;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.Abilities
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCombatant))]
    public sealed class MMOAbilitySystem : MonoBehaviour
    {
        [SerializeField] private List<MMOAbilityDefinition> startingAbilities = new();
        [SerializeField, Min(0f)] private float castKnockbackSeconds = 0.5f;
        [SerializeField, Min(0f)] private float maxCastKnockbackSeconds = 2f;

        private readonly Dictionary<MMOAbilityDefinition, float> cooldownReadyTimes = new();
        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;
        private ActiveCast activeCast;
        private ActiveCharge activeCharge;

        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> AbilityFailed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> AbilityUsed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastStarted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastProgressed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> CastInterrupted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> CastCompleted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition> AbilityLearned;

        public IReadOnlyList<MMOAbilityDefinition> KnownAbilities => startingAbilities;
        public bool IsCasting => activeCast != null || activeCharge != null;
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

        private void OnEnable()
        {
            EnsureInitialized();
            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
            }
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
                AbilityLearned?.Invoke(this, ability);
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

            if (activeCast != null || activeCharge != null)
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
                ? StartOrExecuteAbility(ability, resolvedTarget, targetCombatant, out failureReason)
                : false;
        }

        private bool StartOrExecuteAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant, out string failureReason)
        {
            failureReason = string.Empty;
            if (TryGetChargeEffect(ability, out MMOAbilityEffectDefinition chargeEffect))
            {
                return TryStartChargeAbility(ability, resolvedTarget, targetCombatant, chargeEffect, out failureReason);
            }

            if (ability.CastTimeSeconds > 0f)
            {
                activeCast = new ActiveCast(ability, resolvedTarget, transform.position, Time.time, ability.CastTimeSeconds);
                CastStarted?.Invoke(this, ability, resolvedTarget, ability.CastTimeSeconds);
                return true;
            }

            ExecutePreparedAbility(ability, resolvedTarget, targetCombatant);
            return true;
        }

        private bool TryStartChargeAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant, MMOAbilityEffectDefinition chargeEffect, out string failureReason)
        {
            if (!TryBuildChargePath(resolvedTarget, out Vector3[] pathCorners))
            {
                return Fail(ability, resolvedTarget, "No valid path found.", out failureReason);
            }

            if (ability.ManaCost > 0)
            {
                identity.Mana.SetCurrent(identity.Mana.CurrentValue - ability.ManaCost);
            }

            if (ability.CooldownSeconds > 0f)
            {
                cooldownReadyTimes[ability] = Time.time + ability.CooldownSeconds;
            }

            activeCharge = new ActiveCharge(ability, resolvedTarget, targetCombatant, chargeEffect, pathCorners);
            StartCoroutine(RunCharge(activeCharge));
            AbilityUsed?.Invoke(this, ability, resolvedTarget);
            failureReason = string.Empty;
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
                    MMOCharacterBuffController buffController = target.GetComponent<MMOCharacterBuffController>();
                    if (buffController == null)
                    {
                        buffController = target.gameObject.AddComponent<MMOCharacterBuffController>();
                    }

                    buffController.ApplyBuff(MMOBuffApplication.FromAbility(ability, effect));
                    continue;
                }

                if (effect.EffectType == MMOAbilityEffectType.Charge)
                {
                    continue;
                }

                int amount = effect.CalculateAmount(identity);
                if (effect.EffectType == MMOAbilityEffectType.Heal)
                {
                    target.ApplyHeal(combatant, ability, amount);
                }
                else if (ShouldUseWeaponResolution(ability, effect))
                {
                    MMOCombatResolver.ApplyWeaponDamage(combatant, target, ability, effect);
                }
                else
                {
                    target.ApplyDamage(combatant, ability, amount);
                }
            }
        }

        private static bool ShouldUseWeaponResolution(MMOAbilityDefinition ability, MMOAbilityEffectDefinition effect)
        {
            return effect.EffectType == MMOAbilityEffectType.Damage
                && effect.DamageSchool == MMODamageSchool.Physical
                && (ability.IsAutoAttack || effect.AmountSource == MMOAbilityAmountSource.WeaponDamage);
        }

        private bool TryGetChargeEffect(MMOAbilityDefinition ability, out MMOAbilityEffectDefinition chargeEffect)
        {
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect.EffectType == MMOAbilityEffectType.Charge)
                {
                    chargeEffect = effect;
                    return true;
                }
            }

            chargeEffect = null;
            return false;
        }

        private bool TryBuildChargePath(MMOCharacterIdentity target, out Vector3[] pathCorners)
        {
            pathCorners = Array.Empty<Vector3>();
            if (target == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 4f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(target.transform.position, out NavMeshHit targetHit, 4f, NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path = new();
            if (!NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length == 0)
            {
                return false;
            }

            pathCorners = path.corners;
            return true;
        }

        private IEnumerator RunCharge(ActiveCharge charge)
        {
            MMOPlayerMotor playerMotor = GetComponent<MMOPlayerMotor>();
            bool restorePlayerMotor = playerMotor != null && playerMotor.enabled;
            if (restorePlayerMotor)
            {
                playerMotor.enabled = false;
            }

            CharacterController characterController = GetComponent<CharacterController>();
            int cornerIndex = charge.Corners.Length > 1 ? 1 : 0;

            while (activeCharge == charge && charge.Target != null && charge.TargetCombatant != null && charge.TargetCombatant.IsAlive)
            {
                float stopDistance = charge.Effect.ChargeStopDistance;
                Vector3 toTarget = charge.Target.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude <= stopDistance * stopDistance)
                {
                    break;
                }

                Vector3 destination = cornerIndex < charge.Corners.Length ? charge.Corners[cornerIndex] : charge.Target.transform.position;
                Vector3 toDestination = destination - transform.position;
                toDestination.y = 0f;
                if (toDestination.sqrMagnitude <= 0.04f)
                {
                    cornerIndex++;
                    continue;
                }

                Vector3 planarDirection = toDestination.normalized;
                float step = charge.Effect.ChargeSpeed * Time.deltaTime;
                Vector3 delta = planarDirection * Mathf.Min(step, toDestination.magnitude);
                float targetY = cornerIndex < charge.Corners.Length ? charge.Corners[cornerIndex].y : transform.position.y;
                delta.y = targetY - transform.position.y;

                MoveChargeStep(characterController, delta);
                FaceChargeDirection(planarDirection);
                yield return null;
            }

            if (activeCharge == charge && charge.TargetCombatant != null && charge.TargetCombatant.IsAlive && IsInRange(charge.Target, charge.Effect.ChargeStopDistance + 0.25f))
            {
                int amount = charge.Effect.CalculateAmount(identity);
                charge.TargetCombatant.ApplyDamage(combatant, charge.Ability, amount);
            }

            if (restorePlayerMotor && playerMotor != null)
            {
                playerMotor.enabled = true;
            }

            if (activeCharge == charge)
            {
                activeCharge = null;
            }
        }

        private void MoveChargeStep(CharacterController characterController, Vector3 delta)
        {
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(delta);
                return;
            }

            transform.position += delta;
        }

        private void FaceChargeDirection(Vector3 planarDirection)
        {
            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(planarDirection, Vector3.up);
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

        private void OnDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (activeCast == null || amount <= 0 || castKnockbackSeconds <= 0f)
            {
                return;
            }

            float remainingKnockbackBudget = Mathf.Max(0f, maxCastKnockbackSeconds - activeCast.AppliedKnockbackSeconds);
            if (remainingKnockbackBudget <= 0f)
            {
                return;
            }

            float elapsed = Mathf.Max(0f, Time.time - activeCast.StartTime);
            float knockback = Mathf.Min(castKnockbackSeconds, remainingKnockbackBudget, elapsed);
            if (knockback <= 0f)
            {
                return;
            }

            activeCast.StartTime += knockback;
            activeCast.AppliedKnockbackSeconds += knockback;
            CastProgressed?.Invoke(this, activeCast.Ability, activeCast.Target, CurrentCastNormalized);
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
            public readonly float Duration;
            public float StartTime;
            public float AppliedKnockbackSeconds;

            public ActiveCast(MMOAbilityDefinition ability, MMOCharacterIdentity target, Vector3 startPosition, float startTime, float duration)
            {
                Ability = ability;
                Target = target;
                StartPosition = startPosition;
                StartTime = startTime;
                Duration = Mathf.Max(0.01f, duration);
            }
        }

        private sealed class ActiveCharge
        {
            public readonly MMOAbilityDefinition Ability;
            public readonly MMOCharacterIdentity Target;
            public readonly MMOCombatant TargetCombatant;
            public readonly MMOAbilityEffectDefinition Effect;
            public readonly Vector3[] Corners;

            public ActiveCharge(MMOAbilityDefinition ability, MMOCharacterIdentity target, MMOCombatant targetCombatant, MMOAbilityEffectDefinition effect, Vector3[] corners)
            {
                Ability = ability;
                Target = target;
                TargetCombatant = targetCombatant;
                Effect = effect;
                Corners = corners ?? Array.Empty<Vector3>();
            }
        }
    }
}
