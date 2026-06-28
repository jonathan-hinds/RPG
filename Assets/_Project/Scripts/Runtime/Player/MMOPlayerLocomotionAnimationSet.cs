using RPGClone.Animation;
using UnityEngine;

namespace RPGClone.Player
{
    [CreateAssetMenu(menuName = "RPG Clone/Player/Locomotion Animation Set", fileName = "PlayerLocomotionAnimationSet")]
    public sealed class MMOPlayerLocomotionAnimationSet : ScriptableObject
    {
        public const string MoveSpeedParameter = MMOCreatureAnimationSet.MoveSpeedParameter;
        public const string JumpStartParameter = "JumpStart";
        public const string JumpEndParameter = "JumpEnd";
        public const string JumpStartPlaceholderName = "MMO_JumpStart";
        public const string JumpEndPlaceholderName = "MMO_JumpEnd";

        [Header("Controller")]
        [SerializeField] private RuntimeAnimatorController baseController;

        [Header("Locomotion")]
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip walkBackwards;
        [SerializeField] private AnimationClip walk;
        [SerializeField] private AnimationClip run;
        [SerializeField] private AnimationClip jumpStart;
        [SerializeField] private AnimationClip jumpEnd;
        [SerializeField, Min(0.01f)] private float backwardWalkSpeed = 4.1f;
        [SerializeField, Min(0.01f)] private float walkSpeed = 1.45f;
        [SerializeField, Min(0.01f)] private float runSpeed = 7.25f;
        [SerializeField, Min(0f)] private float movementDampSeconds = 0.12f;

        [Header("Presentation")]
        [SerializeField] private bool applyRootMotion;
        [SerializeField] private float modelYawOffsetDegrees;

        [Header("Jump Presentation")]
        [SerializeField, Min(0f)] private float movingLandingPlanarSpeedThreshold = 1.2f;
        [SerializeField, Min(0f)] private float movingLandingHoldSeconds = 0.08f;
        [SerializeField, Min(0f)] private float movingLandingTransitionSeconds = 0.12f;

        public RuntimeAnimatorController BaseController => baseController;
        public AnimationClip Idle => idle;
        public AnimationClip WalkBackwards => walkBackwards != null ? walkBackwards : Walk;
        public AnimationClip Walk => walk != null ? walk : run;
        public AnimationClip Run => run;
        public AnimationClip JumpStart => jumpStart;
        public AnimationClip JumpEnd => jumpEnd;
        public float MovementDampSeconds => movementDampSeconds;
        public bool ApplyRootMotion => applyRootMotion;
        public float ModelYawOffsetDegrees => modelYawOffsetDegrees;
        public float MovingLandingPlanarSpeedThreshold => movingLandingPlanarSpeedThreshold;
        public float MovingLandingHoldSeconds => movingLandingHoldSeconds;
        public float MovingLandingTransitionSeconds => movingLandingTransitionSeconds;

        public float NormalizeMoveSpeed(float worldSpeed, bool movingBackward)
        {
            if (worldSpeed <= 0.03f)
            {
                return 0f;
            }

            if (movingBackward)
            {
                float safeBackwardWalkSpeed = Mathf.Max(0.01f, backwardWalkSpeed);
                return Mathf.Lerp(0f, -0.5f, Mathf.Clamp01(worldSpeed / safeBackwardWalkSpeed));
            }

            float safeWalkSpeed = Mathf.Max(0.01f, walkSpeed);
            float safeRunSpeed = Mathf.Max(safeWalkSpeed + 0.01f, runSpeed);
            if (worldSpeed <= safeWalkSpeed)
            {
                return Mathf.Lerp(0f, 0.5f, worldSpeed / safeWalkSpeed);
            }

            float runBlend = Mathf.InverseLerp(safeWalkSpeed, safeRunSpeed, worldSpeed);
            return Mathf.Lerp(0.5f, 1f, runBlend);
        }

        public float NormalizeMoveSpeed(float worldSpeed)
        {
            return NormalizeMoveSpeed(worldSpeed, false);
        }

        public AnimationClip GetReplacementClip(AnimationClip placeholder)
        {
            if (placeholder == null)
            {
                return null;
            }

            return placeholder.name switch
            {
                MMOCreatureAnimationSet.IdlePlaceholderName => Idle,
                MMOCreatureAnimationSet.WalkBackwardsPlaceholderName => WalkBackwards,
                MMOCreatureAnimationSet.WalkPlaceholderName => Walk,
                MMOCreatureAnimationSet.RunPlaceholderName => Run,
                JumpStartPlaceholderName => JumpStart,
                JumpEndPlaceholderName => JumpEnd,
                _ => null
            };
        }

        public void Configure(
            RuntimeAnimatorController newBaseController,
            AnimationClip newIdle,
            AnimationClip newWalkBackwards,
            AnimationClip newWalk,
            AnimationClip newRun,
            float newBackwardWalkSpeed,
            float newWalkSpeed,
            float newRunSpeed,
            float newMovementDampSeconds,
            bool newApplyRootMotion,
            float newModelYawOffsetDegrees)
        {
            Configure(
                newBaseController,
                newIdle,
                newWalkBackwards,
                newWalk,
                newRun,
                null,
                null,
                newBackwardWalkSpeed,
                newWalkSpeed,
                newRunSpeed,
                newMovementDampSeconds,
                newApplyRootMotion,
                newModelYawOffsetDegrees,
                1.2f,
                0.08f,
                0.12f);
        }

        public void Configure(
            RuntimeAnimatorController newBaseController,
            AnimationClip newIdle,
            AnimationClip newWalkBackwards,
            AnimationClip newWalk,
            AnimationClip newRun,
            AnimationClip newJumpStart,
            AnimationClip newJumpEnd,
            float newBackwardWalkSpeed,
            float newWalkSpeed,
            float newRunSpeed,
            float newMovementDampSeconds,
            bool newApplyRootMotion,
            float newModelYawOffsetDegrees)
        {
            Configure(
                newBaseController,
                newIdle,
                newWalkBackwards,
                newWalk,
                newRun,
                newJumpStart,
                newJumpEnd,
                newBackwardWalkSpeed,
                newWalkSpeed,
                newRunSpeed,
                newMovementDampSeconds,
                newApplyRootMotion,
                newModelYawOffsetDegrees,
                movingLandingPlanarSpeedThreshold,
                movingLandingHoldSeconds,
                movingLandingTransitionSeconds);
        }

        public void Configure(
            RuntimeAnimatorController newBaseController,
            AnimationClip newIdle,
            AnimationClip newWalkBackwards,
            AnimationClip newWalk,
            AnimationClip newRun,
            AnimationClip newJumpStart,
            AnimationClip newJumpEnd,
            float newBackwardWalkSpeed,
            float newWalkSpeed,
            float newRunSpeed,
            float newMovementDampSeconds,
            bool newApplyRootMotion,
            float newModelYawOffsetDegrees,
            float newMovingLandingPlanarSpeedThreshold,
            float newMovingLandingHoldSeconds,
            float newMovingLandingTransitionSeconds)
        {
            baseController = newBaseController;
            idle = newIdle;
            walkBackwards = newWalkBackwards;
            walk = newWalk != null ? newWalk : newRun;
            run = newRun;
            jumpStart = newJumpStart;
            jumpEnd = newJumpEnd;
            backwardWalkSpeed = Mathf.Max(0.01f, newBackwardWalkSpeed);
            walkSpeed = Mathf.Max(0.01f, newWalkSpeed);
            runSpeed = Mathf.Max(walkSpeed + 0.01f, newRunSpeed);
            movementDampSeconds = Mathf.Max(0f, newMovementDampSeconds);
            applyRootMotion = newApplyRootMotion;
            modelYawOffsetDegrees = newModelYawOffsetDegrees;
            movingLandingPlanarSpeedThreshold = Mathf.Max(0f, newMovingLandingPlanarSpeedThreshold);
            movingLandingHoldSeconds = Mathf.Max(0f, newMovingLandingHoldSeconds);
            movingLandingTransitionSeconds = Mathf.Max(0f, newMovingLandingTransitionSeconds);
        }
    }
}
