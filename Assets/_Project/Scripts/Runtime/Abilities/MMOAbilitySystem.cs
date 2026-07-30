using System.Collections.Generic;
using System;
using System.Collections;
using RPGClone.Buffs;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Player;
using RPGClone.Services;
using RPGClone.Vfx;
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
        private ReplicatedCastPresentation replicatedCast;

        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> AbilityFailed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> AbilityUsed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastStarted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> CastProgressed;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, string> CastInterrupted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> CastCompleted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, Vector3, bool> AbilityReleased;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> ChargeStarted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity, float> ChargeImpactStarted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition, MMOCharacterIdentity> ChargeCompleted;
        public event Action<MMOAbilitySystem, MMOAbilityDefinition> AbilityLearned;

        public IReadOnlyList<MMOAbilityDefinition> KnownAbilities => startingAbilities;
        public bool IsCasting => activeCast != null || activeCharge != null || replicatedCast != null;
        public MMOAbilityDefinition CurrentCastAbility => activeCast != null ? activeCast.Ability : replicatedCast?.Ability;
        public MMOCharacterIdentity CurrentCastTarget => activeCast != null ? activeCast.Target : replicatedCast?.Target;
        public bool CurrentCastHasGroundTarget => activeCast != null ? activeCast.HasGroundTarget : replicatedCast != null && replicatedCast.HasGroundTarget;
        public Vector3 CurrentCastGroundTargetPosition => activeCast != null && activeCast.HasGroundTarget
            ? activeCast.GroundTargetPosition
            : replicatedCast != null && replicatedCast.HasGroundTarget
                ? replicatedCast.GroundTargetPosition
                : transform.position;
        public float CurrentCastDuration => activeCast != null ? activeCast.Duration : replicatedCast != null ? replicatedCast.Duration : 0f;
        public float CurrentCastNormalized => activeCast != null
            ? Mathf.Clamp01((Time.time - activeCast.StartTime) / activeCast.Duration)
            : replicatedCast != null
                ? Mathf.Clamp01((Time.time - replicatedCast.StartTime) / replicatedCast.Duration)
                : 0f;
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
            EnsureVfxController();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.Damaged += OnDamaged;
                combatant.CriticalDamageDealt -= OnCriticalDamageDealt;
                combatant.CriticalDamageDealt += OnCriticalDamageDealt;
            }
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.CriticalDamageDealt -= OnCriticalDamageDealt;
            }
        }

        private void Update()
        {
            UpdateCast();
            UpdateReplicatedCastPresentation();
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

        public void ReplaceKnownAbilities(IEnumerable<MMOAbilityDefinition> abilities)
        {
            startingAbilities.Clear();
            cooldownReadyTimes.Clear();
            activeCast = null;
            activeCharge = null;

            if (abilities == null)
            {
                return;
            }

            foreach (MMOAbilityDefinition ability in abilities)
            {
                if (ability != null && !startingAbilities.Contains(ability))
                {
                    startingAbilities.Add(ability);
                }
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

        public bool TryUseAbilityAtPosition(MMOAbilityDefinition ability, Vector3 targetPosition, out string failureReason)
        {
            EnsureInitialized();
            failureReason = string.Empty;
            if (ability == null)
            {
                return Fail(null, null, "No ability was provided.", out failureReason);
            }

            if (!ability.RequiresGroundTarget)
            {
                return Fail(ability, null, $"{ability.DisplayName} does not target the ground.", out failureReason);
            }

            if (!KnowsAbility(ability))
            {
                return Fail(ability, null, $"{identity.DisplayName} does not know {ability.DisplayName}.", out failureReason);
            }

            if (combatant == null || !combatant.IsAlive)
            {
                return Fail(ability, null, $"{identity.DisplayName} cannot act.", out failureReason);
            }

            if (activeCast != null || activeCharge != null)
            {
                return Fail(ability, null, "Another action is in progress.", out failureReason);
            }

            return TryPrepareGroundAbility(ability, targetPosition, out failureReason)
                ? StartOrExecuteGroundAbility(ability, targetPosition, out failureReason)
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
                activeCast = new ActiveCast(ability, resolvedTarget, targetCombatant, transform.position, Time.time, ability.CastTimeSeconds, ability.IsChanneled);
                if (ability.IsChanneled)
                {
                    SpendResourceCost(ability);
                    StartCooldown(ability);
                    AbilityUsed?.Invoke(this, ability, resolvedTarget);
                    AbilityReleased?.Invoke(
                        this,
                        ability,
                        resolvedTarget,
                        resolvedTarget != null ? resolvedTarget.transform.position : transform.position,
                        false);
                    activeCast.AuthorityRouted = TrySubmitHostAuthorityRequest(
                        ability,
                        resolvedTarget,
                        resolvedTarget != null ? resolvedTarget.transform.position : transform.position,
                        false,
                        out _,
                        CombatActionRequestKind.ChannelStart);
                }

                CastStarted?.Invoke(this, ability, resolvedTarget, ability.CastTimeSeconds);
                PublishAuthorityEnemyCastEvent(CombatEventType.CastStarted, ability, resolvedTarget, ability.CastTimeSeconds);
                return true;
            }

            ExecutePreparedAbility(ability, resolvedTarget, targetCombatant);
            return true;
        }

        private bool StartOrExecuteGroundAbility(MMOAbilityDefinition ability, Vector3 targetPosition, out string failureReason)
        {
            failureReason = string.Empty;
            if (TryGetChargeEffect(ability, out _))
            {
                return Fail(ability, null, "Charge abilities require a target.", out failureReason);
            }

            if (ability.CastTimeSeconds > 0f)
            {
                activeCast = new ActiveCast(ability, null, null, transform.position, Time.time, ability.CastTimeSeconds, ability.IsChanneled, targetPosition, true);
                if (ability.IsChanneled)
                {
                    SpendResourceCost(ability);
                    StartCooldown(ability);
                    AbilityUsed?.Invoke(this, ability, null);
                    AbilityReleased?.Invoke(this, ability, null, targetPosition, true);
                    activeCast.AuthorityRouted = TrySubmitHostAuthorityRequest(
                        ability,
                        null,
                        targetPosition,
                        true,
                        out _,
                        CombatActionRequestKind.ChannelStart);
                }

                CastStarted?.Invoke(this, ability, null, ability.CastTimeSeconds);
                PublishAuthorityEnemyCastEvent(CombatEventType.CastStarted, ability, null, ability.CastTimeSeconds);
                return true;
            }

            ExecutePreparedGroundAbility(ability, targetPosition);
            return true;
        }

        private bool TryStartChargeAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant, MMOAbilityEffectDefinition chargeEffect, out string failureReason)
        {
            if (!TryBuildChargePath(resolvedTarget, out Vector3[] pathCorners))
            {
                return Fail(ability, resolvedTarget, "No valid path found.", out failureReason);
            }

            SpendResourceCost(ability);
            StartCooldown(ability);
            combatant.EngageCombatWith(targetCombatant);

            activeCharge = new ActiveCharge(ability, resolvedTarget, targetCombatant, chargeEffect, pathCorners);
            StartCoroutine(RunCharge(activeCharge));
            ChargeStarted?.Invoke(this, ability, resolvedTarget);
            AbilityUsed?.Invoke(this, ability, resolvedTarget);
            failureReason = string.Empty;
            return true;
        }

        private bool TryPrepareAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, out string failureReason, out MMOCombatant targetCombatant)
        {
            failureReason = string.Empty;
            targetCombatant = null;
            if (ability.RequiresGroundTarget)
            {
                return Fail(ability, null, "Choose an area to target.", out failureReason);
            }

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

            if (ability.CalculateManaCost(identity) > identity.Mana.CurrentValue)
            {
                return Fail(ability, resolvedTarget, "Not enough mana.", out failureReason);
            }

            return true;
        }

        private bool TryPrepareGroundAbility(MMOAbilityDefinition ability, Vector3 targetPosition, out string failureReason)
        {
            failureReason = string.Empty;
            if (!IsPositionInRange(targetPosition, ability.Range))
            {
                return Fail(ability, null, "Target area is too far away.", out failureReason);
            }

            if (IsOnCooldown(ability, out _))
            {
                return Fail(ability, null, $"{ability.DisplayName} is not ready yet.", out failureReason);
            }

            if (ability.CalculateManaCost(identity) > identity.Mana.CurrentValue)
            {
                return Fail(ability, null, "Not enough mana.", out failureReason);
            }

            return true;
        }

        private void ExecutePreparedAbility(MMOAbilityDefinition ability, MMOCharacterIdentity resolvedTarget, MMOCombatant targetCombatant)
        {
            SpendResourceCost(ability);
            StartCooldown(ability);

            if (ability.HasArea)
            {
                Vector3 center = resolvedTarget != null ? resolvedTarget.transform.position : transform.position;
                AbilityReleased?.Invoke(this, ability, resolvedTarget, center, true);
                PublishAuthorityEnemyAbilityReleased(ability, resolvedTarget, center, true);
                if (TrySubmitHostAuthorityRequest(ability, resolvedTarget, center, true, out _))
                {
                    AbilityUsed?.Invoke(this, ability, resolvedTarget);
                    return;
                }

                ApplyAreaEffects(ability, center);
            }
            else
            {
                Vector3 targetPosition = resolvedTarget != null ? resolvedTarget.transform.position : transform.position;
                AbilityReleased?.Invoke(
                    this,
                    ability,
                    resolvedTarget,
                    targetPosition,
                    false);
                PublishAuthorityEnemyAbilityReleased(ability, resolvedTarget, targetPosition, false);
                if (TrySubmitHostAuthorityRequest(ability, resolvedTarget, targetPosition, false, out _))
                {
                    AbilityUsed?.Invoke(this, ability, resolvedTarget);
                    return;
                }

                ApplyEffects(ability, targetCombatant);
            }

            AbilityUsed?.Invoke(this, ability, resolvedTarget);
        }

        private void ExecutePreparedGroundAbility(MMOAbilityDefinition ability, Vector3 targetPosition)
        {
            SpendResourceCost(ability);
            StartCooldown(ability);

            AbilityReleased?.Invoke(this, ability, null, targetPosition, true);
            PublishAuthorityEnemyAbilityReleased(ability, null, targetPosition, true);
            if (TrySubmitHostAuthorityRequest(ability, null, targetPosition, true, out _))
            {
                AbilityUsed?.Invoke(this, ability, null);
                return;
            }

            ApplyAreaEffects(ability, targetPosition);
            AbilityUsed?.Invoke(this, ability, null);
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

        public bool IsPositionInRange(Vector3 targetPosition, float range)
        {
            Vector3 offset = targetPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
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

        public void ResetCooldown(MMOAbilityDefinition ability)
        {
            if (ability != null)
            {
                cooldownReadyTimes.Remove(ability);
            }
        }

        public void PlayReplicatedCastStarted(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float durationSeconds,
            Vector3 groundTargetPosition = default,
            bool hasGroundTarget = false)
        {
            if (ability == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            if (replicatedCast != null && replicatedCast.Ability == ability)
            {
                replicatedCast.Target = target;
                replicatedCast.Duration = duration;
                replicatedCast.GroundTargetPosition = groundTargetPosition;
                replicatedCast.HasGroundTarget = hasGroundTarget;
                return;
            }

            replicatedCast = new ReplicatedCastPresentation(ability, target, Time.time, duration, groundTargetPosition, hasGroundTarget);
            CastStarted?.Invoke(this, ability, target, Mathf.Max(0.01f, durationSeconds));
        }

        public void ApplyReplicatedCastSnapshot(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float durationSeconds,
            float normalizedProgress)
        {
            if (ability == null || durationSeconds <= 0f)
            {
                if (replicatedCast != null)
                {
                    PlayReplicatedCastInterrupted(replicatedCast.Ability, replicatedCast.Target, "Casting stopped.");
                }

                return;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            float synchronizedStartTime = Time.time - Mathf.Clamp01(normalizedProgress) * duration;
            if (replicatedCast != null && replicatedCast.Ability == ability)
            {
                replicatedCast.Target = target;
                replicatedCast.Duration = duration;
                replicatedCast.StartTime = synchronizedStartTime;
                return;
            }

            replicatedCast = new ReplicatedCastPresentation(ability, target, synchronizedStartTime, duration, Vector3.zero, false);
            CastStarted?.Invoke(this, ability, target, duration);
            CastProgressed?.Invoke(this, ability, target, normalizedProgress);
        }

        public void PlayReplicatedCastInterrupted(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            string reason)
        {
            MMOAbilityDefinition interruptedAbility = replicatedCast != null ? replicatedCast.Ability : ability;
            MMOCharacterIdentity interruptedTarget = replicatedCast != null ? replicatedCast.Target : target;
            replicatedCast = null;
            if (interruptedAbility != null)
            {
                CastInterrupted?.Invoke(this, interruptedAbility, interruptedTarget, reason);
            }
        }

        public void PlayReplicatedAbilityReleased(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (ability == null)
            {
                return;
            }

            AbilityReleased?.Invoke(this, ability, target, targetPosition, hasGroundTarget);
            AbilityUsed?.Invoke(this, ability, target);
        }

        public void PlayReplicatedCastCompleted(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (ability == null)
            {
                return;
            }

            replicatedCast = null;
            CastCompleted?.Invoke(this, ability, target);
        }

        public void PlayReplicatedChargeStarted(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (ability == null)
            {
                return;
            }

            ChargeStarted?.Invoke(this, ability, target);
            AbilityUsed?.Invoke(this, ability, target);
        }

        public void PlayReplicatedChargeImpactStarted(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float impactDelaySeconds)
        {
            if (ability != null)
            {
                ChargeImpactStarted?.Invoke(this, ability, target, Mathf.Max(0f, impactDelaySeconds));
            }
        }

        public void PlayReplicatedChargeCompleted(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (ability != null)
            {
                ChargeCompleted?.Invoke(this, ability, target);
            }
        }

        public MMOAbilityDefinition FindKnownAbilityById(string abilityId)
        {
            return FindKnownAbility(abilityId);
        }

        public void CancelActiveCast(string reason)
        {
            if (activeCast != null)
            {
                InterruptCast(string.IsNullOrWhiteSpace(reason) ? "Casting interrupted." : reason);
            }
        }

        public bool TryResolveAuthorityRequest(CombatActionRequest request, MMOCharacterIdentity target, out string failureReason)
        {
            EnsureInitialized();
            failureReason = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.abilityId))
            {
                failureReason = "Invalid combat request.";
                return false;
            }

            MMOAbilityDefinition ability = FindKnownAbility(request.abilityId);
            if (ability == null)
            {
                failureReason = $"{identity.DisplayName} does not know the requested ability.";
                return false;
            }

            if (request.requestKind == CombatActionRequestKind.ChannelCancel)
            {
                if (activeCast != null && activeCast.Ability == ability && activeCast.IsChanneled)
                {
                    InterruptCast("Channel canceled by the casting player.");
                }

                return true;
            }

            if (request.hasGroundTarget)
            {
                Vector3 groundTarget = request.requestedTargetPosition.ToVector3();
                if (!TryPrepareGroundAbility(ability, groundTarget, out failureReason))
                {
                    return false;
                }

                if (request.requestKind == CombatActionRequestKind.ChannelStart)
                {
                    if (!ability.IsChanneled)
                    {
                        failureReason = $"{ability.DisplayName} is not a channeled ability.";
                        return false;
                    }

                    return StartOrExecuteGroundAbility(ability, groundTarget, out failureReason);
                }

                ExecutePreparedGroundAbility(ability, groundTarget);
                return true;
            }

            MMOCharacterIdentity resolvedTarget = ResolveTarget(ability, target);
            if (ability.TargetType == MMOAbilityTargetType.Hostile && resolvedTarget == null)
            {
                resolvedTarget = target;
            }

            if (!TryPrepareAbility(ability, resolvedTarget, out failureReason, out MMOCombatant targetCombatant))
            {
                return false;
            }

            if (request.requestKind == CombatActionRequestKind.ChannelStart)
            {
                if (!ability.IsChanneled)
                {
                    failureReason = $"{ability.DisplayName} is not a channeled ability.";
                    return false;
                }

                return StartOrExecuteAbility(ability, resolvedTarget, targetCombatant, out failureReason);
            }

            if (request.requestKind == CombatActionRequestKind.ChargeImpact)
            {
                if (!TryGetChargeEffect(ability, out MMOAbilityEffectDefinition chargeEffect))
                {
                    failureReason = $"{ability.DisplayName} has no charge impact effect.";
                    return false;
                }

                if (!IsInRange(resolvedTarget, chargeEffect.ChargeStopDistance + 0.75f))
                {
                    failureReason = "Charge impact target is out of range.";
                    return false;
                }

                SpendResourceCost(ability);
                StartCooldown(ability);
                targetCombatant.ApplyDamage(combatant, ability, chargeEffect.CalculateAmount(identity));
                return true;
            }

            if (ability.IsChanneled || TryGetChargeEffect(ability, out _))
            {
                failureReason = $"{ability.DisplayName} requires a specialized authority request.";
                return false;
            }

            ExecutePreparedAbility(ability, resolvedTarget, targetCombatant);
            return true;
        }

        private void SpendResourceCost(MMOAbilityDefinition ability)
        {
            int manaCost = ability != null ? ability.CalculateManaCost(identity) : 0;
            if (manaCost > 0)
            {
                identity.Mana.SetCurrent(identity.Mana.CurrentValue - manaCost);
            }
        }

        private void StartCooldown(MMOAbilityDefinition ability)
        {
            if (ability != null && ability.CooldownSeconds > 0f)
            {
                cooldownReadyTimes[ability] = Time.time + ability.CooldownSeconds;
            }
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
            bool appliedTemporaryModifiers = false;
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier)
                {
                    if (!appliedTemporaryModifiers)
                    {
                        MMOCharacterBuffController buffController = target.GetComponent<MMOCharacterBuffController>();
                        if (buffController == null)
                        {
                            buffController = target.gameObject.AddComponent<MMOCharacterBuffController>();
                        }

                        appliedTemporaryModifiers = buffController.ApplyTemporaryModifiers(ability, combatant);
                    }

                    continue;
                }

                if (effect.EffectType == MMOAbilityEffectType.PeriodicDamage)
                {
                    MMOCharacterBuffController buffController = target.GetComponent<MMOCharacterBuffController>();
                    if (buffController == null)
                    {
                        buffController = target.gameObject.AddComponent<MMOCharacterBuffController>();
                    }

                    buffController.ApplyBuff(MMOBuffApplication.FromAbility(ability, effect, combatant));
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
                    MMOCombatResolver.ApplyAbilityDamage(combatant, target, ability, effect, amount);
                }
            }

            if (appliedTemporaryModifiers)
            {
                PublishBuffAppliedEvent(ability, target);
            }
        }

        private void PublishBuffAppliedEvent(MMOAbilityDefinition ability, MMOCombatant target)
        {
            if (!MMOGameplaySessionService.IsHostAuthority || ability == null || target == null)
            {
                return;
            }

            CombatEventRecord record = CombatEventRecord.Create(CombatEventType.BuffApplied);
            record.sessionId = MMOGameplaySessionService.SessionId ?? string.Empty;
            record.abilityId = ability.AbilityId;
            record.targetPosition = new Vector3SaveData(target.transform.position);
            PopulateCombatEndpoint(record, combatant, true);
            PopulateCombatEndpoint(record, target, false);
            MMOCombatEventStream.PublishCombatEvent(record, combatant, target, ability);
        }

        private void ApplyAreaEffects(MMOAbilityDefinition ability, Vector3 center)
        {
            float radius = Mathf.Max(0.1f, ability.AreaRadius);
            float sqrRadius = radius * radius;
            foreach (MMOCombatant candidate in MMOCombatant.ActiveCombatants)
            {
                if (candidate == null || !candidate.IsAlive || candidate.Identity == null)
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrRadius || !IsAreaTargetAllowed(ability, candidate.Identity))
                {
                    continue;
                }

                ApplyEffects(ability, candidate);
            }
        }

        private void ApplyAreaEffect(MMOAbilityDefinition ability, MMOAbilityEffectDefinition effect, Vector3 center, int amount)
        {
            float radius = Mathf.Max(0.1f, ability.AreaRadius);
            float sqrRadius = radius * radius;
            foreach (MMOCombatant candidate in MMOCombatant.ActiveCombatants)
            {
                if (candidate == null || !candidate.IsAlive || candidate.Identity == null)
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrRadius || !IsAreaTargetAllowed(ability, candidate.Identity))
                {
                    continue;
                }

                if (effect.EffectType == MMOAbilityEffectType.Heal)
                {
                    candidate.ApplyHeal(combatant, ability, amount);
                }
                else
                {
                    MMOCombatResolver.ApplyAbilityDamage(combatant, candidate, ability, effect, amount);
                }
            }
        }

        private bool IsAreaTargetAllowed(MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (target == null)
            {
                return false;
            }

            return ability.AreaTargetFilter switch
            {
                MMOAbilityAreaTargetFilter.Hostile => MMOFactionRules.CanDamage(identity, target),
                MMOAbilityAreaTargetFilter.Friendly => MMOFactionRules.CanAssist(identity, target),
                MMOAbilityAreaTargetFilter.AnyCharacter => true,
                _ => false
            };
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
                float impactDelaySeconds = charge.Effect.ChargeImpactDelaySeconds;
                ChargeImpactStarted?.Invoke(this, charge.Ability, charge.Target, impactDelaySeconds);
                if (impactDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(impactDelaySeconds);
                }

                if (activeCharge == charge && charge.TargetCombatant != null && charge.TargetCombatant.IsAlive)
                {
                    int amount = charge.Effect.CalculateAmount(identity);
                    if (!TrySubmitHostAuthorityRequest(
                            charge.Ability,
                            charge.Target,
                            charge.Target.transform.position,
                            false,
                            out _,
                            CombatActionRequestKind.ChargeImpact))
                    {
                        charge.TargetCombatant.ApplyDamage(combatant, charge.Ability, amount);
                    }

                    ChargeCompleted?.Invoke(this, charge.Ability, charge.Target);
                }
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

            if (activeCast.IsChanneled)
            {
                if (!TryValidateChannel(activeCast, out string channelFailureReason))
                {
                    InterruptCast(channelFailureReason);
                    return;
                }

                TickChanneledEffects(activeCast);
                CastProgressed?.Invoke(this, activeCast.Ability, activeCast.Target, CurrentCastNormalized);
                if (Time.time - activeCast.StartTime < activeCast.Duration)
                {
                    return;
                }

                MMOAbilityDefinition completedChannel = activeCast.Ability;
                MMOCharacterIdentity channelTarget = activeCast.Target;
                activeCast = null;
                CastCompleted?.Invoke(this, completedChannel, channelTarget);
                PublishAuthorityEnemyCastEvent(CombatEventType.CastCompleted, completedChannel, channelTarget, 0f);
                return;
            }

            CastProgressed?.Invoke(this, activeCast.Ability, activeCast.Target, CurrentCastNormalized);
            if (Time.time - activeCast.StartTime < activeCast.Duration)
            {
                return;
            }

            MMOAbilityDefinition ability = activeCast.Ability;
            MMOCharacterIdentity target = activeCast.Target;
            Vector3 groundTargetPosition = activeCast.GroundTargetPosition;
            bool hasGroundTarget = activeCast.HasGroundTarget;
            activeCast = null;

            if (hasGroundTarget)
            {
                if (!TryPrepareGroundAbility(ability, groundTargetPosition, out string groundFailureReason))
                {
                    NotifyCastInterrupted(ability, null, groundFailureReason);
                    return;
                }

                ExecutePreparedGroundAbility(ability, groundTargetPosition);
                CastCompleted?.Invoke(this, ability, null);
                PublishAuthorityEnemyCastEvent(CombatEventType.CastCompleted, ability, null, 0f);
                return;
            }

            if (!TryPrepareAbility(ability, target, out string failureReason, out MMOCombatant targetCombatant))
            {
                NotifyCastInterrupted(ability, target, failureReason);
                return;
            }

            ExecutePreparedAbility(ability, target, targetCombatant);
            CastCompleted?.Invoke(this, ability, target);
            PublishAuthorityEnemyCastEvent(CombatEventType.CastCompleted, ability, target, 0f);
        }

        private void UpdateReplicatedCastPresentation()
        {
            if (replicatedCast == null)
            {
                return;
            }

            CastProgressed?.Invoke(this, replicatedCast.Ability, replicatedCast.Target, CurrentCastNormalized);
        }

        private bool TryValidateChannel(ActiveCast channel, out string failureReason)
        {
            failureReason = string.Empty;
            if (channel == null || channel.Ability == null)
            {
                failureReason = "Channel interrupted.";
                return false;
            }

            if (channel.HasGroundTarget)
            {
                return true;
            }

            if (channel.TargetCombatant == null || !channel.TargetCombatant.IsAlive)
            {
                failureReason = "Target is invalid.";
                return false;
            }

            if (!IsTargetAllowed(channel.Ability, channel.Target))
            {
                failureReason = "Cannot attack that target.";
                return false;
            }

            if (!IsInRange(channel.Target, channel.Ability.Range))
            {
                failureReason = "Target is too far away.";
                return false;
            }

            return true;
        }

        private void TickChanneledEffects(ActiveCast channel)
        {
            if (channel.AuthorityRouted)
            {
                return;
            }

            for (int i = 0; i < channel.Ability.Effects.Count; i++)
            {
                MMOAbilityEffectDefinition effect = channel.Ability.Effects[i];
                if (effect == null || effect.EffectType != MMOAbilityEffectType.PeriodicDamage || Time.time < channel.NextEffectTickTimes[i])
                {
                    continue;
                }

                int tickAmount = CalculateChannelTickAmount(channel, effect, i);
                if (tickAmount > 0)
                {
                    ApplyChanneledTick(channel, effect, tickAmount);
                }

                channel.NextEffectTickTimes[i] = Time.time + effect.TickSeconds;
            }
        }

        private int CalculateChannelTickAmount(ActiveCast channel, MMOAbilityEffectDefinition effect, int effectIndex)
        {
            int totalAmount = effect.CalculateAmount(identity);
            if (totalAmount <= 0)
            {
                return 0;
            }

            float elapsedAfterTick = Mathf.Clamp(Time.time - channel.StartTime + effect.TickSeconds, 0f, channel.Duration);
            int expectedTotal = Mathf.RoundToInt(totalAmount * (elapsedAfterTick / channel.Duration));
            int tickAmount = Mathf.Max(0, expectedTotal - channel.AppliedChannelAmounts[effectIndex]);
            channel.AppliedChannelAmounts[effectIndex] += tickAmount;
            return tickAmount;
        }

        private void ApplyChanneledTick(ActiveCast channel, MMOAbilityEffectDefinition effect, int amount)
        {
            if (channel.Ability.HasArea)
            {
                Vector3 center = channel.HasGroundTarget
                    ? channel.GroundTargetPosition
                    : channel.Target != null
                        ? channel.Target.transform.position
                        : transform.position;
                ApplyAreaEffect(channel.Ability, effect, center, amount);
                return;
            }

            if (TrySubmitHostAuthorityRequest(channel.Ability, channel.Target, channel.Target.transform.position, false, out _))
            {
                return;
            }

            channel.TargetCombatant.ApplyDamage(combatant, channel.Ability, amount);
        }

        private MMOAbilityDefinition FindKnownAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            foreach (MMOAbilityDefinition ability in startingAbilities)
            {
                if (ability != null && ability.AbilityId == abilityId)
                {
                    return ability;
                }
            }

            return null;
        }

        private bool TrySubmitHostAuthorityRequest(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget,
            out string failureReason,
            CombatActionRequestKind? requestKindOverride = null)
        {
            CombatActionRequestKind requestKind = requestKindOverride
                ?? (ability != null && ability.IsAutoAttack
                    ? CombatActionRequestKind.AutoAttack
                    : CombatActionRequestKind.Ability);
            if (!MMOSessionCombatAuthority.ShouldRouteThroughHost(combatant, ability, target))
            {
                failureReason = string.Empty;
                return false;
            }

            bool submitted = MMOSessionCombatAuthority.TrySubmitRequest(
                combatant,
                ability,
                target,
                targetPosition,
                hasGroundTarget,
                requestKind,
                out failureReason);
            if (!submitted && !string.IsNullOrWhiteSpace(failureReason))
            {
                AbilityFailed?.Invoke(this, ability, target, failureReason);
            }

            return true;
        }

        private void PublishAuthorityEnemyAbilityReleased(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (!MMOGameplaySessionService.IsHostAuthority
                || ability == null
                || combatant == null
                || combatant.GetComponent<MMOEnemyController>() == null)
            {
                return;
            }

            MMOCombatant targetCombatant = target != null ? target.GetComponent<MMOCombatant>() : null;
            CombatEventRecord record = CombatEventRecord.Create(CombatEventType.AbilityReleased);
            record.sessionId = MMOGameplaySessionService.SessionId ?? string.Empty;
            record.abilityId = ability.AbilityId;
            record.targetPosition = new Vector3SaveData(targetPosition);
            record.hasGroundTarget = hasGroundTarget;
            PopulateCombatEndpoint(record, combatant, true);
            PopulateCombatEndpoint(record, targetCombatant, false);
            MMOCombatEventStream.PublishCombatEvent(record, combatant, targetCombatant, ability);
        }

        private void PublishAuthorityEnemyCastEvent(
            CombatEventType eventType,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float durationSeconds)
        {
            if (!MMOGameplaySessionService.IsHostAuthority
                || ability == null
                || combatant == null
                || combatant.GetComponent<MMOEnemyController>() == null)
            {
                return;
            }

            MMOCombatant targetCombatant = target != null ? target.GetComponent<MMOCombatant>() : null;
            CombatEventRecord record = CombatEventRecord.Create(eventType);
            record.sessionId = MMOGameplaySessionService.SessionId ?? string.Empty;
            record.abilityId = ability.AbilityId;
            record.castDurationSeconds = Mathf.Max(0f, durationSeconds);
            record.targetPosition = new Vector3SaveData(CurrentCastGroundTargetPosition);
            record.hasGroundTarget = CurrentCastHasGroundTarget;
            PopulateCombatEndpoint(record, combatant, true);
            PopulateCombatEndpoint(record, targetCombatant, false);
            MMOCombatEventStream.PublishCombatEvent(record, combatant, targetCombatant, ability);
        }

        private static void PopulateCombatEndpoint(CombatEventRecord record, MMOCombatant endpoint, bool sourceEndpoint)
        {
            if (record == null || endpoint == null || endpoint.Identity == null)
            {
                return;
            }

            if (MMOGameplaySessionService.Players.TryGetParticipant(endpoint.Identity, out MMOPlayerParticipant participant))
            {
                if (sourceEndpoint)
                {
                    record.sourceCharacterId = participant.CharacterId;
                }
                else
                {
                    record.targetCharacterId = participant.CharacterId;
                }
            }

            MMOEnemyController enemy = endpoint.GetComponent<MMOEnemyController>();
            if (enemy == null)
            {
                return;
            }

            if (sourceEndpoint)
            {
                record.sourceEnemySpawnId = enemy.SpawnId;
            }
            else
            {
                record.targetEnemySpawnId = enemy.SpawnId;
            }
        }

        private void OnCriticalDamageDealt(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (ability != null && ability.ResetCooldownOnCriticalHit)
            {
                ResetCooldown(ability);
            }
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

            ActiveCast interruptedCast = activeCast;
            MMOAbilityDefinition ability = interruptedCast.Ability;
            MMOCharacterIdentity target = interruptedCast.Target;
            activeCast = null;
            if (interruptedCast.AuthorityRouted)
            {
                TrySubmitHostAuthorityRequest(
                    ability,
                    target,
                    interruptedCast.HasGroundTarget ? interruptedCast.GroundTargetPosition : target != null ? target.transform.position : transform.position,
                    interruptedCast.HasGroundTarget,
                    out _,
                    CombatActionRequestKind.ChannelCancel);
            }

            NotifyCastInterrupted(ability, target, reason);
            AbilityFailed?.Invoke(this, ability, target, reason);
        }

        private void NotifyCastInterrupted(MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason)
        {
            CastInterrupted?.Invoke(this, ability, target, reason);
            PublishAuthorityEnemyCastEvent(CombatEventType.CastInterrupted, ability, target, 0f);
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

        private void EnsureVfxController()
        {
            if (GetComponent<MMOAbilityVfxController>() == null)
            {
                gameObject.AddComponent<MMOAbilityVfxController>();
            }
        }

        private sealed class ActiveCast
        {
            public readonly MMOAbilityDefinition Ability;
            public readonly MMOCharacterIdentity Target;
            public readonly MMOCombatant TargetCombatant;
            public readonly Vector3 StartPosition;
            public readonly Vector3 GroundTargetPosition;
            public readonly bool HasGroundTarget;
            public readonly bool IsChanneled;
            public readonly float Duration;
            public readonly float[] NextEffectTickTimes;
            public readonly int[] AppliedChannelAmounts;
            public float StartTime;
            public float AppliedKnockbackSeconds;
            public bool AuthorityRouted;

            public ActiveCast(MMOAbilityDefinition ability, MMOCharacterIdentity target, Vector3 startPosition, float startTime, float duration)
                : this(ability, target, null, startPosition, startTime, duration, false, Vector3.zero, false)
            {
            }

            public ActiveCast(
                MMOAbilityDefinition ability,
                MMOCharacterIdentity target,
                MMOCombatant targetCombatant,
                Vector3 startPosition,
                float startTime,
                float duration,
                bool isChanneled)
                : this(ability, target, targetCombatant, startPosition, startTime, duration, isChanneled, Vector3.zero, false)
            {
            }

            public ActiveCast(
                MMOAbilityDefinition ability,
                MMOCharacterIdentity target,
                MMOCombatant targetCombatant,
                Vector3 startPosition,
                float startTime,
                float duration,
                bool isChanneled,
                Vector3 groundTargetPosition,
                bool hasGroundTarget)
            {
                Ability = ability;
                Target = target;
                TargetCombatant = targetCombatant;
                StartPosition = startPosition;
                GroundTargetPosition = groundTargetPosition;
                HasGroundTarget = hasGroundTarget;
                IsChanneled = isChanneled;
                StartTime = startTime;
                Duration = Mathf.Max(0.01f, duration);
                int effectCount = ability != null ? ability.Effects.Count : 0;
                NextEffectTickTimes = new float[effectCount];
                AppliedChannelAmounts = new int[effectCount];
                for (int i = 0; i < effectCount; i++)
                {
                    MMOAbilityEffectDefinition effect = ability.Effects[i];
                    NextEffectTickTimes[i] = startTime + (effect != null ? effect.TickSeconds : 1f);
                }
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

        private sealed class ReplicatedCastPresentation
        {
            public readonly MMOAbilityDefinition Ability;
            public MMOCharacterIdentity Target;
            public float StartTime;
            public float Duration;
            public Vector3 GroundTargetPosition;
            public bool HasGroundTarget;

            public ReplicatedCastPresentation(
                MMOAbilityDefinition ability,
                MMOCharacterIdentity target,
                float startTime,
                float duration,
                Vector3 groundTargetPosition,
                bool hasGroundTarget)
            {
                Ability = ability;
                Target = target;
                StartTime = startTime;
                Duration = Mathf.Max(0.01f, duration);
                GroundTargetPosition = groundTargetPosition;
                HasGroundTarget = hasGroundTarget;
            }
        }
    }
}
