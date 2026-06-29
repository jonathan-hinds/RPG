using System.Collections.Generic;
using System;
using UnityEngine;

namespace RPGClone.Player
{
    [Serializable]
    public sealed class MMOUpperBodyCounterYawBone
    {
        private const float AnimatedPoseMatchThreshold = 0.9995f;

        [SerializeField] private string boneName;
        [SerializeField] private Transform bone;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [SerializeField, Range(0f, 60f)] private float maxYawDegrees = 30f;

        [NonSerialized] private bool hasAppliedOffset;
        [NonSerialized] private Quaternion lastOffset = Quaternion.identity;
        [NonSerialized] private Quaternion lastFinalRotation = Quaternion.identity;

        public MMOUpperBodyCounterYawBone()
        {
        }

        public MMOUpperBodyCounterYawBone(string boneName, Transform bone, float weight, float maxYawDegrees)
        {
            this.boneName = boneName;
            this.bone = bone;
            this.weight = Mathf.Clamp01(weight);
            this.maxYawDegrees = Mathf.Max(0f, maxYawDegrees);
        }

        public string BoneName => boneName;
        public bool HasBone => bone != null;

        public void Resolve(Transform root)
        {
            if (bone != null || root == null || string.IsNullOrWhiteSpace(boneName))
            {
                return;
            }

            bone = FindDeepChildByName(root, boneName);
        }

        public void Apply(float yawDegrees, Transform axisRoot)
        {
            if (bone == null)
            {
                return;
            }

            Quaternion animatedRotation = ResolveAnimatedRotation();
            Vector3 parentSpaceUp = ResolveParentSpaceUp(axisRoot);
            float weightedYaw = Mathf.Clamp(yawDegrees * weight, -maxYawDegrees, maxYawDegrees);
            Quaternion offset = Mathf.Abs(weightedYaw) > 0.001f
                ? Quaternion.AngleAxis(weightedYaw, parentSpaceUp)
                : Quaternion.identity;

            bone.localRotation = offset * animatedRotation;
            lastOffset = offset;
            lastFinalRotation = bone.localRotation;
            hasAppliedOffset = true;
        }

        public void Clear()
        {
            if (bone != null && hasAppliedOffset && IsStillOurLastPose(bone.localRotation))
            {
                bone.localRotation = Quaternion.Inverse(lastOffset) * bone.localRotation;
            }

            hasAppliedOffset = false;
            lastOffset = Quaternion.identity;
            lastFinalRotation = Quaternion.identity;
        }

        private Quaternion ResolveAnimatedRotation()
        {
            if (!hasAppliedOffset || !IsStillOurLastPose(bone.localRotation))
            {
                return bone.localRotation;
            }

            return Quaternion.Inverse(lastOffset) * bone.localRotation;
        }

        private bool IsStillOurLastPose(Quaternion rotation)
        {
            return Mathf.Abs(Quaternion.Dot(rotation, lastFinalRotation)) >= AnimatedPoseMatchThreshold;
        }

        private Vector3 ResolveParentSpaceUp(Transform axisRoot)
        {
            Vector3 worldUp = axisRoot != null ? axisRoot.up : Vector3.up;
            Vector3 parentSpaceUp = bone.parent != null
                ? bone.parent.InverseTransformDirection(worldUp)
                : worldUp;

            return parentSpaceUp.sqrMagnitude > 0.0001f ? parentSpaceUp.normalized : Vector3.up;
        }

        private static Transform FindDeepChildByName(Transform root, string targetName)
        {
            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDeepChildByName(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MMOPlayerLocomotionAnimator : MonoBehaviour
    {
        private static readonly int MoveSpeedHash = Animator.StringToHash(MMOPlayerLocomotionAnimationSet.MoveSpeedParameter);
        private static readonly int JumpStartHash = Animator.StringToHash(MMOPlayerLocomotionAnimationSet.JumpStartParameter);
        private static readonly int JumpEndHash = Animator.StringToHash(MMOPlayerLocomotionAnimationSet.JumpEndParameter);
        private static readonly int LocomotionStateHash = Animator.StringToHash("Base Layer.Locomotion");

        [SerializeField] private MMOPlayerLocomotionAnimationSet animationSet;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private MMOPlayerMotor motor;

        [Header("Strafe Presentation")]
        [SerializeField] private bool enableDirectionalVisualYaw;
        [SerializeField, Min(0f)] private float visualYawSharpness = 16f;
        [SerializeField, Range(0f, 120f)] private float maxVisualYawDegrees = 78f;
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.05f;
        [SerializeField] private bool enableUpperBodyCounterYaw;
        [SerializeField, Range(0f, 1f)] private float upperBodyCounterYawWeight = 0.65f;
        [SerializeField, Range(0f, 80f)] private float maxUpperBodyCounterYawDegrees = 42f;
        [SerializeField] private List<MMOUpperBodyCounterYawBone> upperBodyCounterYawBones = new();

        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> clipOverrides = new();
        private AnimatorOverrideController overrideController;
        private AnimationClip idleOverride;
        private AnimationClip combatIdleOverride;
        private MMOPlayerCombatAnimationSet combatAnimationOverrideSet;
        private float currentVisualYaw;
        private float movingLandingReturnTime = float.PositiveInfinity;

        private void Awake()
        {
            EnsureReferences();
            ApplyAnimationSet();
        }

        private void OnEnable()
        {
            EnsureReferences();
            SubscribeToMotor();
        }

        private void Update()
        {
            if (animator == null || animationSet == null || motor == null)
            {
                return;
            }

            float planarSpeed = motor.CurrentPlanarSpeed;
            Vector2 localVelocity = motor.CurrentLocalPlanarVelocity;
            bool movingBackward = localVelocity.y < -movingSpeedThreshold;

            animator.SetFloat(
                MoveSpeedHash,
                animationSet.NormalizeMoveSpeed(planarSpeed, movingBackward),
                animationSet.MovementDampSeconds,
                Time.deltaTime);

            UpdateVisualYaw(planarSpeed);
            UpdateMovingLandingReturn();
        }

        private void LateUpdate()
        {
            ApplyUpperBodyCounterYaw();
        }

        private void OnDisable()
        {
            UnsubscribeFromMotor();
            ClearUpperBodyCounterYaw();
        }

        public void Configure(
            MMOPlayerLocomotionAnimationSet newAnimationSet,
            Animator newAnimator,
            Transform newVisualRoot,
            MMOPlayerMotor newMotor)
        {
            UnsubscribeFromMotor();
            animationSet = newAnimationSet;
            animator = newAnimator;
            visualRoot = newVisualRoot;
            motor = newMotor;
            currentVisualYaw = 0f;
            movingLandingReturnTime = float.PositiveInfinity;
            SubscribeToMotor();
            ApplyAnimationSet();
        }

        public void ConfigureStrafePresentation(
            bool shouldEnableDirectionalVisualYaw,
            float newVisualYawSharpness,
            float newMaxVisualYawDegrees,
            bool shouldEnableUpperBodyCounterYaw,
            float newUpperBodyCounterYawWeight,
            float newMaxUpperBodyCounterYawDegrees,
            IEnumerable<MMOUpperBodyCounterYawBone> newUpperBodyCounterYawBones)
        {
            ClearUpperBodyCounterYaw();
            enableDirectionalVisualYaw = shouldEnableDirectionalVisualYaw;
            visualYawSharpness = Mathf.Max(0f, newVisualYawSharpness);
            maxVisualYawDegrees = Mathf.Clamp(newMaxVisualYawDegrees, 0f, 120f);
            enableUpperBodyCounterYaw = shouldEnableUpperBodyCounterYaw;
            upperBodyCounterYawWeight = Mathf.Clamp01(newUpperBodyCounterYawWeight);
            maxUpperBodyCounterYawDegrees = Mathf.Clamp(newMaxUpperBodyCounterYawDegrees, 0f, 80f);
            upperBodyCounterYawBones.Clear();

            if (newUpperBodyCounterYawBones != null)
            {
                upperBodyCounterYawBones.AddRange(newUpperBodyCounterYawBones);
            }

            ResolveUpperBodyCounterYawBones();
        }

        public void SetIdleOverride(AnimationClip newIdleOverride)
        {
            if (idleOverride == newIdleOverride)
            {
                return;
            }

            idleOverride = newIdleOverride;
            ApplyClipOverrides();
        }

        public void SetCombatIdleOverride(AnimationClip newCombatIdleOverride)
        {
            if (combatIdleOverride == newCombatIdleOverride)
            {
                return;
            }

            combatIdleOverride = newCombatIdleOverride;
            ApplyClipOverrides();
        }

        public void SetCombatAnimationOverrideSet(MMOPlayerCombatAnimationSet newCombatAnimationOverrideSet)
        {
            if (combatAnimationOverrideSet == newCombatAnimationOverrideSet)
            {
                return;
            }

            combatAnimationOverrideSet = newCombatAnimationOverrideSet;
            ApplyClipOverrides();
        }

        private void UpdateVisualYaw(float planarSpeed)
        {
            float targetVisualYaw = 0f;
            if (enableDirectionalVisualYaw && planarSpeed > movingSpeedThreshold)
            {
                Vector2 localVelocity = motor.CurrentLocalPlanarVelocity;
                targetVisualYaw = ResolveLowerBodyYaw(localVelocity);
            }

            currentVisualYaw = Mathf.LerpAngle(
                currentVisualYaw,
                targetVisualYaw,
                1f - Mathf.Exp(-visualYawSharpness * Time.deltaTime));

            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, animationSet.ModelYawOffsetDegrees + currentVisualYaw, 0f);
            }
        }

        private void ApplyUpperBodyCounterYaw()
        {
            if (!enableUpperBodyCounterYaw || upperBodyCounterYawBones.Count == 0)
            {
                ClearUpperBodyCounterYaw();
                return;
            }

            ResolveUpperBodyCounterYawBones();
            float counterYaw = Mathf.Clamp(
                -currentVisualYaw * upperBodyCounterYawWeight,
                -maxUpperBodyCounterYawDegrees,
                maxUpperBodyCounterYawDegrees);

            for (int i = 0; i < upperBodyCounterYawBones.Count; i++)
            {
                upperBodyCounterYawBones[i]?.Apply(counterYaw, visualRoot);
            }
        }

        private void ClearUpperBodyCounterYaw()
        {
            for (int i = 0; i < upperBodyCounterYawBones.Count; i++)
            {
                upperBodyCounterYawBones[i]?.Clear();
            }
        }

        private void SubscribeToMotor()
        {
            if (motor == null)
            {
                return;
            }

            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
        }

        private void UnsubscribeFromMotor()
        {
            if (motor == null)
            {
                return;
            }

            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
        }

        private void OnJumped()
        {
            if (animator == null || animationSet == null || animationSet.JumpStart == null)
            {
                return;
            }

            animator.ResetTrigger(JumpEndHash);
            movingLandingReturnTime = float.PositiveInfinity;
            animator.SetTrigger(JumpStartHash);
        }

        private void OnLanded()
        {
            if (animator == null || animationSet == null)
            {
                return;
            }

            if (ShouldPrioritizeLocomotionOnLanding())
            {
                ResetJumpTriggers();
                animator.SetTrigger(JumpEndHash);
                movingLandingReturnTime = Time.time + animationSet.MovingLandingHoldSeconds;
                return;
            }

            if (animationSet.JumpEnd == null)
            {
                return;
            }

            ResetJumpTriggers();
            movingLandingReturnTime = float.PositiveInfinity;
            animator.SetTrigger(JumpEndHash);
        }

        private void UpdateMovingLandingReturn()
        {
            if (Time.time < movingLandingReturnTime)
            {
                return;
            }

            movingLandingReturnTime = float.PositiveInfinity;
            if (!ShouldPrioritizeLocomotionOnLanding())
            {
                return;
            }

            SetImmediateMoveSpeed();
            if (animator.HasState(0, LocomotionStateHash))
            {
                animator.CrossFadeInFixedTime(
                    LocomotionStateHash,
                    animationSet.MovingLandingTransitionSeconds,
                    0,
                    0f);
            }
        }

        private bool ShouldPrioritizeLocomotionOnLanding()
        {
            return motor != null
                && motor.CurrentPlanarSpeed >= animationSet.MovingLandingPlanarSpeedThreshold;
        }

        private void SetImmediateMoveSpeed()
        {
            if (motor == null)
            {
                return;
            }

            Vector2 localVelocity = motor.CurrentLocalPlanarVelocity;
            bool movingBackward = localVelocity.y < -movingSpeedThreshold;
            animator.SetFloat(MoveSpeedHash, animationSet.NormalizeMoveSpeed(motor.CurrentPlanarSpeed, movingBackward));
        }

        private void ResetJumpTriggers()
        {
            animator.ResetTrigger(JumpStartHash);
            animator.ResetTrigger(JumpEndHash);
        }

        private float ResolveLowerBodyYaw(Vector2 localVelocity)
        {
            if (localVelocity.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            if (localVelocity.y < -0.05f && Mathf.Abs(localVelocity.x) < 0.05f)
            {
                return 0f;
            }

            float strafe = localVelocity.y < -0.05f ? -localVelocity.x : localVelocity.x;
            float forward = Mathf.Abs(localVelocity.y) > 0.05f ? localVelocity.y : 0.001f;
            float yaw = Mathf.Atan2(strafe, forward) * Mathf.Rad2Deg;
            return Mathf.Clamp(yaw, -maxVisualYawDegrees, maxVisualYawDegrees);
        }

        private void ApplyAnimationSet()
        {
            if (animator == null || animationSet == null || animationSet.BaseController == null)
            {
                return;
            }

            animator.applyRootMotion = animationSet.ApplyRootMotion;
            if (!Application.isPlaying)
            {
                animator.runtimeAnimatorController = animationSet.BaseController;
                return;
            }

            overrideController = new AnimatorOverrideController(animationSet.BaseController);
            overrideController.GetOverrides(clipOverrides);
            ApplyClipOverrides();
            animator.runtimeAnimatorController = overrideController;
        }

        private void ApplyClipOverrides()
        {
            if (overrideController == null || animationSet == null)
            {
                return;
            }

            for (int i = 0; i < clipOverrides.Count; i++)
            {
                AnimationClip replacement = GetReplacementClip(clipOverrides[i].Key);
                clipOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(
                    clipOverrides[i].Key,
                    replacement != null ? replacement : clipOverrides[i].Key);
            }

            overrideController.ApplyOverrides(clipOverrides);
        }

        private AnimationClip GetReplacementClip(AnimationClip placeholder)
        {
            if (placeholder != null
                && placeholder.name == RPGClone.Animation.MMOCreatureAnimationSet.IdlePlaceholderName
                && idleOverride != null)
            {
                return idleOverride;
            }

            if (placeholder != null
                && placeholder.name == MMOPlayerCombatAnimationSet.CombatIdlePlaceholderName
                && combatIdleOverride != null)
            {
                return combatIdleOverride;
            }

            AnimationClip combatReplacement = combatAnimationOverrideSet != null
                ? combatAnimationOverrideSet.GetReplacementClip(placeholder)
                : null;
            if (combatReplacement != null)
            {
                return combatReplacement;
            }

            return animationSet.GetReplacementClip(placeholder);
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

            if (motor == null)
            {
                motor = GetComponent<MMOPlayerMotor>();
            }

            ResolveUpperBodyCounterYawBones();
        }

        private void ResolveUpperBodyCounterYawBones()
        {
            if (visualRoot == null)
            {
                return;
            }

            for (int i = 0; i < upperBodyCounterYawBones.Count; i++)
            {
                upperBodyCounterYawBones[i]?.Resolve(visualRoot);
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
