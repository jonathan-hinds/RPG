using UnityEngine;

namespace RPGClone.Animation
{
    /// <summary>
    /// Routes an action to either the full-body base layer or a masked upper-body layer.
    /// An action that starts while stationary remains full-body, but is promoted to the
    /// upper-body layer if locomotion begins so the base layer can resume its run cycle.
    /// </summary>
    public sealed class MMOLayeredActionPlayer
    {
        public const string UpperBodyLayerName = "Upper Body";
        public const string UpperBodyEmptyStatePath = UpperBodyLayerName + ".Empty";

        private static readonly int UpperBodyEmptyStateHash = Animator.StringToHash(UpperBodyEmptyStatePath);

        private Animator animator;
        private int baseLocomotionStateHash;
        private int upperBodyLayerIndex = -1;
        private int activeBaseStateHash;
        private int activeUpperBodyStateHash;
        private float activeTransitionSeconds;
        private bool actionActive;
        private bool usingUpperBodyLayer;

        public bool ActionActive => actionActive;
        public bool UsingUpperBodyLayer => actionActive && usingUpperBodyLayer;
        public int UpperBodyLayerIndex => upperBodyLayerIndex;

        public void Configure(Animator targetAnimator, int locomotionStateHash)
        {
            if (animator == targetAnimator && baseLocomotionStateHash == locomotionStateHash)
            {
                RefreshLayerIndex();
                return;
            }

            Reset();
            animator = targetAnimator;
            baseLocomotionStateHash = locomotionStateHash;
            RefreshLayerIndex();
        }

        public bool Play(
            int baseStateHash,
            int upperBodyStateHash,
            bool moving,
            float transitionSeconds)
        {
            if (!IsAnimatorReady())
            {
                return false;
            }

            RefreshLayerIndex();
            bool canUseUpperBody = moving && HasUpperBodyState(upperBodyStateHash);
            int layerIndex = canUseUpperBody ? upperBodyLayerIndex : 0;
            int stateHash = canUseUpperBody ? upperBodyStateHash : baseStateHash;
            if (!animator.HasState(layerIndex, stateHash))
            {
                return false;
            }

            if (canUseUpperBody)
            {
                PrepareUpperBodyLayer();
            }
            else
            {
                DeactivateUpperBodyLayer();
            }

            float safeTransitionSeconds = Mathf.Max(0f, transitionSeconds);
            animator.CrossFadeInFixedTime(stateHash, safeTransitionSeconds, layerIndex, 0f);
            activeBaseStateHash = baseStateHash;
            activeUpperBodyStateHash = upperBodyStateHash;
            activeTransitionSeconds = safeTransitionSeconds;
            actionActive = true;
            usingUpperBodyLayer = canUseUpperBody;
            return true;
        }

        public void PromoteToUpperBodyIfMoving(bool moving)
        {
            if (!moving
                || !actionActive
                || usingUpperBodyLayer
                || !IsAnimatorReady()
                || !HasUpperBodyState(activeUpperBodyStateHash))
            {
                return;
            }

            float normalizedTime = ResolveActiveBaseNormalizedTime();
            PrepareUpperBodyLayer();
            animator.Play(activeUpperBodyStateHash, upperBodyLayerIndex, normalizedTime);

            if (animator.HasState(0, baseLocomotionStateHash))
            {
                animator.CrossFadeInFixedTime(
                    baseLocomotionStateHash,
                    activeTransitionSeconds,
                    0,
                    0f);
            }

            usingUpperBodyLayer = true;
        }

        public void Stop(float transitionSeconds)
        {
            if (!IsAnimatorReady())
            {
                ClearState();
                return;
            }

            if (usingUpperBodyLayer)
            {
                DeactivateUpperBodyLayer();
            }
            else if (actionActive && animator.HasState(0, baseLocomotionStateHash))
            {
                animator.CrossFadeInFixedTime(
                    baseLocomotionStateHash,
                    Mathf.Max(0f, transitionSeconds),
                    0,
                    0f);
            }

            ClearState();
        }

        public void Reset()
        {
            if (animator != null)
            {
                RefreshLayerIndex();
                DeactivateUpperBodyLayer();
            }

            ClearState();
        }

        private void PrepareUpperBodyLayer()
        {
            if (upperBodyLayerIndex < 0)
            {
                return;
            }

            if (!usingUpperBodyLayer && animator.HasState(upperBodyLayerIndex, UpperBodyEmptyStateHash))
            {
                animator.Play(UpperBodyEmptyStateHash, upperBodyLayerIndex, 0f);
            }

            animator.SetLayerWeight(upperBodyLayerIndex, 1f);
        }

        private void DeactivateUpperBodyLayer()
        {
            if (animator == null || upperBodyLayerIndex < 0 || upperBodyLayerIndex >= animator.layerCount)
            {
                return;
            }

            if (animator.HasState(upperBodyLayerIndex, UpperBodyEmptyStateHash))
            {
                animator.Play(UpperBodyEmptyStateHash, upperBodyLayerIndex, 0f);
            }

            animator.SetLayerWeight(upperBodyLayerIndex, 0f);
        }

        private float ResolveActiveBaseNormalizedTime()
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash == activeBaseStateHash)
            {
                return NormalizePlaybackTime(current.normalizedTime);
            }

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash == activeBaseStateHash)
                {
                    return NormalizePlaybackTime(next.normalizedTime);
                }
            }

            return 0f;
        }

        private static float NormalizePlaybackTime(float normalizedTime)
        {
            return normalizedTime <= 1f
                ? Mathf.Clamp01(normalizedTime)
                : Mathf.Repeat(normalizedTime, 1f);
        }

        private bool HasUpperBodyState(int stateHash)
        {
            return upperBodyLayerIndex >= 0
                && upperBodyLayerIndex < animator.layerCount
                && animator.HasState(upperBodyLayerIndex, stateHash);
        }

        private void RefreshLayerIndex()
        {
            upperBodyLayerIndex = animator != null
                ? animator.GetLayerIndex(UpperBodyLayerName)
                : -1;
        }

        private bool IsAnimatorReady()
        {
            return animator != null
                && animator.runtimeAnimatorController != null
                && animator.isInitialized;
        }

        private void ClearState()
        {
            activeBaseStateHash = 0;
            activeUpperBodyStateHash = 0;
            activeTransitionSeconds = 0f;
            actionActive = false;
            usingUpperBodyLayer = false;
        }
    }
}
