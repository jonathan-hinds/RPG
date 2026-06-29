using RPGClone.Abilities;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Player
{
    [DisallowMultipleComponent]
    public sealed class MMOPlayerCombatAnimator : MonoBehaviour
    {
        private static readonly int InCombatHash = Animator.StringToHash(MMOPlayerCombatAnimationSet.InCombatParameter);

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
            ApplyCombatIdleState(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (locomotionAnimator != null)
            {
                locomotionAnimator.SetIdleOverride(null);
                locomotionAnimator.SetCombatIdleOverride(null);
            }
        }

        private void Update()
        {
            ApplyCombatIdleState(false);
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
        }

        private void Unsubscribe()
        {
            if (combatant == null)
            {
                return;
            }

            combatant.CombatStateChanged -= OnCombatStateChanged;
        }

        private void OnCombatStateChanged(MMOCombatant changedCombatant, bool isInCombat)
        {
            if (changedCombatant != combatant)
            {
                return;
            }

            ApplyCombatIdleState(true);
        }

        private void ApplyCombatIdleState(bool force)
        {
            if (locomotionAnimator == null || animator == null || combatant == null)
            {
                EnsureReferences();
            }

            lastAnimatorReady = animator != null && animator.runtimeAnimatorController != null && animator.isInitialized;
            lastObservedPlanarSpeed = motor != null ? motor.CurrentPlanarSpeed : -1f;
            lastBaseLayerBusy = false;

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
