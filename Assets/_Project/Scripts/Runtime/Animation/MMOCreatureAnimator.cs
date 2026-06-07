using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.Animation
{
    [DisallowMultipleComponent]
    public sealed class MMOCreatureAnimator : MonoBehaviour
    {
        private static readonly int MoveSpeedHash = Animator.StringToHash(MMOCreatureAnimationSet.MoveSpeedParameter);
        private static readonly int Attack1Hash = Animator.StringToHash(MMOCreatureAnimationSet.Attack1Parameter);
        private static readonly int Attack2Hash = Animator.StringToHash(MMOCreatureAnimationSet.Attack2Parameter);
        private static readonly int DamageHash = Animator.StringToHash(MMOCreatureAnimationSet.DamageParameter);
        private static readonly int DeathHash = Animator.StringToHash(MMOCreatureAnimationSet.DeathParameter);
        private static readonly int DeadHash = Animator.StringToHash(MMOCreatureAnimationSet.DeadParameter);
        private static readonly int LocomotionHash = Animator.StringToHash("Base Layer.Locomotion");

        [SerializeField] private MMOCreatureAnimationSet animationSet;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private MMOAbilitySystem abilitySystem;
        [SerializeField] private MMOCombatant combatant;

        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> clipOverrides = new();
        private AnimatorOverrideController overrideController;
        private Vector3 lastPosition;
        private float attackPriorityUntil;
        private float nextDamageReactionTime;
        private int nextAttackIndex;
        private bool dead;

        private void Awake()
        {
            EnsureReferences();
            ApplyAnimationSet();
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (abilitySystem != null)
            {
                abilitySystem.AbilityUsed -= OnAbilityUsed;
                abilitySystem.AbilityUsed += OnAbilityUsed;
            }

            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.Damaged += OnDamaged;
                combatant.Died -= OnDied;
                combatant.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (abilitySystem != null)
            {
                abilitySystem.AbilityUsed -= OnAbilityUsed;
            }

            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (animator == null || animationSet == null)
            {
                return;
            }

            if (dead && combatant != null && combatant.IsAlive)
            {
                ResetAfterRespawn();
            }

            float worldSpeed = dead ? 0f : GetWorldSpeed();
            animator.SetFloat(
                MoveSpeedHash,
                animationSet.NormalizeMoveSpeed(worldSpeed),
                animationSet.MovementDampSeconds,
                Time.deltaTime);
            lastPosition = transform.position;
        }

        public void Configure(MMOCreatureAnimationSet newAnimationSet, Animator newAnimator, Transform newVisualRoot)
        {
            animationSet = newAnimationSet;
            animator = newAnimator;
            visualRoot = newVisualRoot;
            ApplyAnimationSet();
        }

        private void ApplyAnimationSet()
        {
            if (animator == null || animationSet == null || animationSet.BaseController == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                animator.runtimeAnimatorController = animationSet.BaseController;
                animator.applyRootMotion = animationSet.ApplyRootMotion;
                if (visualRoot != null)
                {
                    visualRoot.localRotation = Quaternion.Euler(0f, animationSet.ModelYawOffsetDegrees, 0f);
                }

                return;
            }

            overrideController = new AnimatorOverrideController(animationSet.BaseController);
            overrideController.GetOverrides(clipOverrides);
            for (int i = 0; i < clipOverrides.Count; i++)
            {
                AnimationClip replacement = GetReplacementClip(clipOverrides[i].Key);
                if (replacement != null)
                {
                    clipOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(clipOverrides[i].Key, replacement);
                }
            }

            overrideController.ApplyOverrides(clipOverrides);
            animator.runtimeAnimatorController = overrideController;
            animator.applyRootMotion = animationSet.ApplyRootMotion;
            animator.SetBool(DeadHash, dead);

            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, animationSet.ModelYawOffsetDegrees, 0f);
            }
        }

        private AnimationClip GetReplacementClip(AnimationClip placeholder)
        {
            if (placeholder == null)
            {
                return null;
            }

            return placeholder.name switch
            {
                MMOCreatureAnimationSet.IdlePlaceholderName => animationSet.Idle,
                MMOCreatureAnimationSet.WalkPlaceholderName => animationSet.Walk,
                MMOCreatureAnimationSet.RunPlaceholderName => animationSet.Run,
                MMOCreatureAnimationSet.Attack1PlaceholderName => animationSet.Attack1,
                MMOCreatureAnimationSet.Attack2PlaceholderName => animationSet.Attack2,
                MMOCreatureAnimationSet.DamagePlaceholderName => animationSet.Damage,
                MMOCreatureAnimationSet.DeathPlaceholderName => animationSet.Death,
                _ => null
            };
        }

        private float GetWorldSpeed()
        {
            if (agent != null && agent.enabled)
            {
                Vector3 velocity = agent.velocity.sqrMagnitude > 0.0001f ? agent.velocity : agent.desiredVelocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }

            if (Time.deltaTime <= 0f)
            {
                return 0f;
            }

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            return delta.magnitude / Time.deltaTime;
        }

        private void OnAbilityUsed(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source != abilitySystem || ability == null || !ability.IsAutoAttack || dead)
            {
                return;
            }

            PlayAttack();
        }

        private void PlayAttack()
        {
            if (animator == null || animationSet == null)
            {
                return;
            }

            bool useSecondAttack = animationSet.Attack2 != null
                && (!animationSet.AlternateAttacks || nextAttackIndex % 2 == 1);
            nextAttackIndex++;

            animator.ResetTrigger(DamageHash);
            animator.SetTrigger(useSecondAttack ? Attack2Hash : Attack1Hash);
            attackPriorityUntil = Time.time + animationSet.AttackPrioritySeconds;
        }

        private void OnDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target != combatant || amount <= 0 || dead || animator == null || animationSet == null)
            {
                return;
            }

            if (Time.time < nextDamageReactionTime || IsAttackTakingPriority())
            {
                return;
            }

            animator.SetTrigger(DamageHash);
            nextDamageReactionTime = Time.time + animationSet.DamageReactionCooldownSeconds;
        }

        private bool IsAttackTakingPriority()
        {
            if (Time.time < attackPriorityUntil)
            {
                return true;
            }

            if (animator == null)
            {
                return false;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.IsTag("Attack") && currentState.normalizedTime < 0.9f)
            {
                return true;
            }

            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
        }

        private void OnDied(MMOCombatant deadCombatant)
        {
            if (deadCombatant != combatant || animator == null)
            {
                return;
            }

            dead = true;
            animator.ResetTrigger(Attack1Hash);
            animator.ResetTrigger(Attack2Hash);
            animator.ResetTrigger(DamageHash);
            animator.SetBool(DeadHash, true);
            animator.SetTrigger(DeathHash);
        }

        private void ResetAfterRespawn()
        {
            dead = false;
            nextAttackIndex = 0;
            attackPriorityUntil = 0f;
            nextDamageReactionTime = 0f;
            lastPosition = transform.position;

            animator.ResetTrigger(Attack1Hash);
            animator.ResetTrigger(Attack2Hash);
            animator.ResetTrigger(DamageHash);
            animator.ResetTrigger(DeathHash);
            animator.SetBool(DeadHash, false);
            animator.SetFloat(MoveSpeedHash, 0f);

            if (animator.HasState(0, LocomotionHash))
            {
                animator.Play(LocomotionHash, 0, 0f);
            }
            else
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void EnsureReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
            if (!Application.isPlaying)
            {
                ApplyAnimationSet();
            }
        }
    }
}
