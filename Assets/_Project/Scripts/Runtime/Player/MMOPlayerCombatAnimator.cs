using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Player
{
    [DisallowMultipleComponent]
    public sealed class MMOPlayerCombatAnimator : MonoBehaviour, IMMOAutoAttackPresentation
    {
        private static readonly int InCombatHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.InCombatParameter);
        private static readonly int ActionSpeedHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.ActionSpeedParameter);
        private static readonly int LocomotionStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.LocomotionStatePath);
        private static readonly int OneHandAttackStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.OneHandAttackStatePath);
        private static readonly int TwoHandAttackStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.TwoHandAttackStatePath);
        private static readonly int UnarmedAttackStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.UnarmedAttackStatePath);
        private static readonly int FullBodyDamageStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.FullBodyDamageStatePath);
        private static readonly int CastingStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.CastingStatePath);
        private static readonly int CastStateHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.CastStatePath);

        [SerializeField] private MMOPlayerCombatAnimationSet animationSet;
        [SerializeField] private Animator animator;
        [SerializeField] private MMOPlayerLocomotionAnimator locomotionAnimator;
        [SerializeField] private MMOPlayerMotor motor;
        [SerializeField] private MMOCombatant combatant;
        [SerializeField] private MMOAbilitySystem abilitySystem;
        [SerializeField] private MMOAutoAttackController autoAttackController;

        private bool lastAppliedInCombat;
        private bool hasAppliedCombatState;
        private string lastRequestedState = "<none>";
        private bool lastBaseLayerBusy;
        private bool lastAnimatorReady;
        private float lastObservedPlanarSpeed;
        private float activeActionEndTime = float.NegativeInfinity;
        private float attackPriorityUntil;
        private float finalCastPriorityUntil;
        private float nextDamageReactionTime;
        private float deferredDamageReactionUntil;
        private bool hasDeferredDamageReaction;
        private bool damageReactionActive;
        private bool castInProgress;

        public string DebugLastRequestedState => lastRequestedState;
        public bool DebugLastBaseLayerBusy => lastBaseLayerBusy;
        public bool DebugLastAnimatorReady => lastAnimatorReady;
        public float DebugLastObservedPlanarSpeed => lastObservedPlanarSpeed;
        public bool DebugHasCombatIdleState => HasBaseState(MMOPlayerCombatAnimationSet.CombatIdleStatePath);
        public bool DebugHasLocomotionState => HasBaseState(MMOPlayerCombatAnimationSet.LocomotionStatePath);
        public string DebugCurrentBaseState => ResolveCurrentBaseStateName();
        public string DebugCurrentBaseClips => ResolveCurrentBaseClips();
        public float DebugCurrentBaseNormalizedTime => animator != null && animator.isInitialized
            ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime
            : -1f;
        public float DebugTime => Time.time;
        public int DebugFrameCount => Time.frameCount;
        public string DebugCombatIdleClip => animationSet != null && animationSet.CombatIdle != null
            ? $"{animationSet.CombatIdle.name} ({animationSet.CombatIdle.length:0.###}s)"
            : "<none>";

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            ApplyCombatIdleState(true);
        }

        private void OnEnable()
        {
            EnsureReferences();
            Subscribe();
            ApplyCombatActionOverrides();
            ApplyCombatIdleState(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (locomotionAnimator != null)
            {
                locomotionAnimator.SetIdleOverride(null);
                locomotionAnimator.SetCombatIdleOverride(null);
                locomotionAnimator.SetCombatAnimationOverrideSet(null);
            }
        }

        private void Update()
        {
            ApplyCombatIdleState(false);
            UpdateDeferredDamageReaction();
            UpdateActionReturn();
        }

        public void Configure(
            MMOPlayerCombatAnimationSet newAnimationSet,
            Animator newAnimator,
            MMOPlayerMotor newMotor,
            MMOCombatant newCombatant,
            MMOAbilitySystem newAbilitySystem,
            MMOAutoAttackController newAutoAttackController)
        {
            Unsubscribe();
            animationSet = newAnimationSet;
            animator = newAnimator;
            locomotionAnimator = GetComponent<MMOPlayerLocomotionAnimator>();
            motor = newMotor;
            combatant = newCombatant;
            abilitySystem = newAbilitySystem;
            autoAttackController = newAutoAttackController;
            hasAppliedCombatState = false;

            if (Application.isPlaying)
            {
                ApplyCombatActionOverrides();
                ApplyCombatIdleState(true);
                Subscribe();
            }
        }

        private void Subscribe()
        {
            if (combatant == null)
            {
                return;
            }

            combatant.CombatStateChanged -= OnCombatStateChanged;
            combatant.CombatStateChanged += OnCombatStateChanged;
            combatant.Damaged -= OnDamaged;
            combatant.Damaged += OnDamaged;

            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.AbilityUsed -= OnAbilityUsed;
            abilitySystem.AbilityUsed += OnAbilityUsed;
            abilitySystem.CastStarted -= OnCastStarted;
            abilitySystem.CastStarted += OnCastStarted;
            abilitySystem.CastInterrupted -= OnCastInterrupted;
            abilitySystem.CastInterrupted += OnCastInterrupted;
            abilitySystem.CastCompleted -= OnCastCompleted;
            abilitySystem.CastCompleted += OnCastCompleted;
        }

        private void Unsubscribe()
        {
            if (combatant != null)
            {
                combatant.CombatStateChanged -= OnCombatStateChanged;
                combatant.Damaged -= OnDamaged;
            }

            if (abilitySystem != null)
            {
                abilitySystem.AbilityUsed -= OnAbilityUsed;
                abilitySystem.CastStarted -= OnCastStarted;
                abilitySystem.CastInterrupted -= OnCastInterrupted;
                abilitySystem.CastCompleted -= OnCastCompleted;
            }
        }

        private void OnCombatStateChanged(MMOCombatant changedCombatant, bool isInCombat)
        {
            if (changedCombatant != combatant)
            {
                return;
            }

            ApplyCombatIdleState(true);
        }

        public float GetAutoAttackLeadSeconds(float swingDurationSeconds)
        {
            EnsureReferences();
            if (animationSet == null || combatant == null || combatant.Identity == null)
            {
                return 0f;
            }

            MMOWeaponType weaponType = MMOCombatResolver.GetWeaponSnapshot(combatant.Identity).WeaponType;
            return Mathf.Clamp(
                animationSet.CalculateAttackLeadSeconds(weaponType, swingDurationSeconds),
                0f,
                Mathf.Max(0f, swingDurationSeconds));
        }

        public void NotifyAutoAttackWindup(
            MMOAutoAttackController controller,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float swingDurationSeconds,
            float impactTime)
        {
            if (controller != autoAttackController || ability == null || !ability.IsAutoAttack)
            {
                return;
            }

            if ((abilitySystem != null && abilitySystem.IsCasting) || IsActionTakingPriority())
            {
                return;
            }

            PlayWeaponAttack(swingDurationSeconds);
        }

        private void ApplyCombatIdleState(bool force)
        {
            if (locomotionAnimator == null || animator == null || combatant == null)
            {
                EnsureReferences();
            }

            lastAnimatorReady = animator != null && animator.runtimeAnimatorController != null && animator.isInitialized;
            lastObservedPlanarSpeed = motor != null ? motor.CurrentPlanarSpeed : -1f;
            lastBaseLayerBusy = IsBaseLayerBusy();

            if (locomotionAnimator == null)
            {
                return;
            }

            bool inCombat = combatant != null && combatant.IsInCombat;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(InCombatHash, inCombat);
            }

            if (!force && hasAppliedCombatState && inCombat == lastAppliedInCombat)
            {
                return;
            }

            AnimationClip targetIdle = inCombat && animationSet != null && animationSet.CombatIdle != null
                ? animationSet.CombatIdle
                : null;

            locomotionAnimator.SetIdleOverride(targetIdle);
            locomotionAnimator.SetCombatIdleOverride(animationSet != null ? animationSet.CombatIdle : null);
            lastRequestedState = targetIdle != null
                ? $"Idle clip override: {targetIdle.name}"
                : "Idle clip override: <normal idle>";
            lastAppliedInCombat = inCombat;
            hasAppliedCombatState = true;
        }

        private void ApplyCombatActionOverrides()
        {
            if (locomotionAnimator == null)
            {
                EnsureReferences();
            }

            if (locomotionAnimator != null)
            {
                locomotionAnimator.SetCombatAnimationOverrideSet(animationSet);
            }
        }

        private void OnAbilityUsed(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source != abilitySystem || ability == null || ability.IsAutoAttack)
            {
                return;
            }

            if (ability.CastTimeSeconds > 0f || ability.AnimationStyle == MMOAbilityAnimationStyle.None)
            {
                return;
            }

            switch (ResolveAnimationStyle(ability))
            {
                case MMOAbilityAnimationStyle.WeaponAttack:
                    PlayWeaponAttack(GetInstantWeaponAttackDuration());
                    break;
                case MMOAbilityAnimationStyle.SpellCast:
                    PlayCastRelease();
                    break;
            }
        }

        private void OnCastStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            if (source != abilitySystem || ability == null || ability.AnimationStyle == MMOAbilityAnimationStyle.None)
            {
                return;
            }

            if (ResolveAnimationStyle(ability) != MMOAbilityAnimationStyle.SpellCast)
            {
                return;
            }

            PlayCastingLoop(duration);
        }

        private void OnCastInterrupted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason)
        {
            if (source != abilitySystem)
            {
                return;
            }

            castInProgress = false;
            finalCastPriorityUntil = 0f;
            damageReactionActive = false;
            activeActionEndTime = Time.time;
            ReturnToLocomotion();
        }

        private void OnCastCompleted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source != abilitySystem || ability == null)
            {
                return;
            }

            castInProgress = false;
            if (ResolveAnimationStyle(ability) == MMOAbilityAnimationStyle.SpellCast)
            {
                PlayCastRelease();
            }
        }

        private void OnDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target != combatant || amount <= 0 || animationSet == null)
            {
                return;
            }

            if (Time.time < nextDamageReactionTime)
            {
                return;
            }

            if (IsActionTakingPriority())
            {
                hasDeferredDamageReaction = true;
                deferredDamageReactionUntil = Time.time + animationSet.DeferredDamageReactionWindowSeconds;
                return;
            }

            PlayDamageReaction();
        }

        private void PlayWeaponAttack(float swingDurationSeconds)
        {
            if (!IsAnimatorReady() || animationSet == null || combatant == null || combatant.Identity == null)
            {
                return;
            }

            MMOWeaponType weaponType = MMOCombatResolver.GetWeaponSnapshot(combatant.Identity).WeaponType;
            int stateHash = ResolveAttackStateHash(weaponType);
            if (!animator.HasState(0, stateHash))
            {
                return;
            }

            float playbackSpeed = animationSet.CalculateAttackPlaybackSpeed(weaponType, swingDurationSeconds);
            animator.SetFloat(ActionSpeedHash, playbackSpeed);
            animator.CrossFadeInFixedTime(stateHash, animationSet.AttackTransitionSeconds, 0, 0f);

            float duration = animationSet.GetAttackDurationSeconds(weaponType, playbackSpeed);
            activeActionEndTime = Time.time + duration;
            attackPriorityUntil = activeActionEndTime;
            finalCastPriorityUntil = 0f;
            damageReactionActive = false;
            lastRequestedState = $"Attack: {animationSet.GetAttackStatePath(weaponType)}";
        }

        private void PlayDamageReaction()
        {
            if (!IsAnimatorReady() || animationSet == null || !animator.HasState(0, FullBodyDamageStateHash))
            {
                return;
            }

            animator.SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(FullBodyDamageStateHash, animationSet.DamageTransitionSeconds, 0, 0f);
            activeActionEndTime = Time.time + animationSet.GetDamageDurationSeconds();
            nextDamageReactionTime = Time.time + animationSet.DamageReactionCooldownSeconds;
            hasDeferredDamageReaction = false;
            attackPriorityUntil = 0f;
            finalCastPriorityUntil = 0f;
            damageReactionActive = true;
            lastRequestedState = "Damage: CombatDamage";
        }

        private void PlayCastingLoop(float duration)
        {
            if (!IsAnimatorReady() || animationSet == null || !animator.HasState(0, CastingStateHash))
            {
                return;
            }

            castInProgress = true;
            animator.SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(CastingStateHash, animationSet.CastingTransitionSeconds, 0, 0f);
            activeActionEndTime = float.PositiveInfinity;
            finalCastPriorityUntil = 0f;
            damageReactionActive = false;
            lastRequestedState = "Cast: Casting";
        }

        private void PlayCastRelease()
        {
            if (!IsAnimatorReady() || animationSet == null || !animator.HasState(0, CastStateHash))
            {
                ReturnToLocomotion();
                return;
            }

            animator.SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(CastStateHash, animationSet.CastTransitionSeconds, 0, 0f);
            activeActionEndTime = Time.time + animationSet.GetCastDurationSeconds();
            finalCastPriorityUntil = activeActionEndTime;
            damageReactionActive = false;
            lastRequestedState = "Cast: Release";
        }

        private void UpdateDeferredDamageReaction()
        {
            if (!hasDeferredDamageReaction)
            {
                return;
            }

            if (Time.time > deferredDamageReactionUntil)
            {
                hasDeferredDamageReaction = false;
                return;
            }

            if (!IsActionTakingPriority())
            {
                PlayDamageReaction();
            }
        }

        private void UpdateActionReturn()
        {
            if (Time.time < activeActionEndTime)
            {
                return;
            }

            if (damageReactionActive)
            {
                damageReactionActive = false;
                if (TryResumeCastingLoop())
                {
                    return;
                }
            }

            activeActionEndTime = float.PositiveInfinity;
            ReturnToLocomotion();
        }

        private bool TryResumeCastingLoop()
        {
            if (!castInProgress || abilitySystem == null || !abilitySystem.IsCasting)
            {
                return false;
            }

            if (!IsAnimatorReady() || animationSet == null || !animator.HasState(0, CastingStateHash))
            {
                return false;
            }

            animator.SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(CastingStateHash, animationSet.CastingTransitionSeconds, 0, 0f);
            activeActionEndTime = float.PositiveInfinity;
            finalCastPriorityUntil = 0f;
            lastRequestedState = "Cast: Casting";
            return true;
        }

        private void ReturnToLocomotion()
        {
            if (!IsAnimatorReady() || !animator.HasState(0, LocomotionStateHash))
            {
                return;
            }

            animator.SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(LocomotionStateHash, ResolveReturnTransitionSeconds(), 0, 0f);
            lastRequestedState = "Return: Locomotion";
        }

        private float ResolveReturnTransitionSeconds()
        {
            bool inCombat = combatant != null && combatant.IsInCombat;
            return animationSet != null
                ? (inCombat ? animationSet.IdleEnterTransitionSeconds : animationSet.IdleExitTransitionSeconds)
                : 0.08f;
        }

        private bool IsActionTakingPriority()
        {
            return Time.time < attackPriorityUntil
                || Time.time < finalCastPriorityUntil;
        }

        private bool IsBaseLayerBusy()
        {
            if (!IsAnimatorReady())
            {
                return false;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            return currentState.IsTag("Attack")
                || currentState.IsTag("Damage")
                || currentState.IsTag("Cast")
                || animator.IsInTransition(0);
        }

        private bool IsAnimatorReady()
        {
            return animator != null && animator.runtimeAnimatorController != null && animator.isInitialized;
        }

        private int ResolveAttackStateHash(MMOWeaponType weaponType)
        {
            if (IsTwoHanded(weaponType) && animationSet.TwoHandAttack != null)
            {
                return TwoHandAttackStateHash;
            }

            if (weaponType == MMOWeaponType.Unarmed && animationSet.UnarmedAttack != null)
            {
                return UnarmedAttackStateHash;
            }

            return OneHandAttackStateHash;
        }

        private float GetInstantWeaponAttackDuration()
        {
            if (combatant == null || combatant.Identity == null)
            {
                return 1f;
            }

            return Mathf.Min(1f, MMOCombatResolver.GetAttackSpeed(combatant.Identity));
        }

        private static MMOAbilityAnimationStyle ResolveAnimationStyle(MMOAbilityDefinition ability)
        {
            if (ability == null)
            {
                return MMOAbilityAnimationStyle.None;
            }

            if (ability.AnimationStyle != MMOAbilityAnimationStyle.Automatic)
            {
                return ability.AnimationStyle;
            }

            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (effect.EffectType == MMOAbilityEffectType.Charge)
                {
                    return MMOAbilityAnimationStyle.Charge;
                }

                if (effect.DamageSchool == MMODamageSchool.Physical
                    && (effect.AmountSource == MMOAbilityAmountSource.WeaponDamage
                        || effect.AmountSource == MMOAbilityAmountSource.AttackPower))
                {
                    return MMOAbilityAnimationStyle.WeaponAttack;
                }
            }

            return MMOAbilityAnimationStyle.SpellCast;
        }

        private static bool IsTwoHanded(MMOWeaponType weaponType)
        {
            return weaponType == MMOWeaponType.TwoHandSword
                || weaponType == MMOWeaponType.TwoHandMace
                || weaponType == MMOWeaponType.Staff;
        }

        private bool HasBaseState(string stateName)
        {
            return animator != null && animator.HasState(0, Animator.StringToHash(stateName));
        }

        private string ResolveCurrentBaseStateName()
        {
            if (animator == null || !animator.isInitialized)
            {
                return "<animator unavailable>";
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(MMOPlayerCombatAnimationSet.CombatIdleStatePath))
            {
                return MMOPlayerCombatAnimationSet.CombatIdleStatePath;
            }

            if (state.IsName(MMOPlayerCombatAnimationSet.LocomotionStatePath))
            {
                return MMOPlayerCombatAnimationSet.LocomotionStatePath;
            }

            return $"<unknown {state.fullPathHash}>";
        }

        private string ResolveCurrentBaseClips()
        {
            if (animator == null || !animator.isInitialized)
            {
                return "<animator unavailable>";
            }

            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips == null || clips.Length == 0)
            {
                return "<none>";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < clips.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                AnimationClip clip = clips[i].clip;
                builder.Append(clip != null ? clip.name : "<null>");
                builder.Append("=");
                builder.Append(clips[i].weight.ToString("0.###"));
            }

            return builder.ToString();
        }

        private void EnsureReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (locomotionAnimator == null)
            {
                locomotionAnimator = GetComponent<MMOPlayerLocomotionAnimator>();
            }

            if (motor == null)
            {
                motor = GetComponent<MMOPlayerMotor>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }

            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (autoAttackController == null)
            {
                autoAttackController = GetComponent<MMOAutoAttackController>();
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
        }
    }
}
