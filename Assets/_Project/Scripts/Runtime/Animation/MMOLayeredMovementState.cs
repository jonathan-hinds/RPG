using UnityEngine;

namespace RPGClone.Animation
{
    /// <summary>
    /// Tunable movement policy for deciding when actions should preserve locomotion.
    /// Separate start/stop thresholds and a short hold prevent low-speed pathfinding
    /// or network samples from switching presentation back to full-body prematurely.
    /// </summary>
    public readonly struct MMOLayeredMovementPolicy
    {
        public const float DefaultStartSpeedThreshold = 0.025f;
        public const float DefaultStopSpeedThreshold = 0.01f;
        public const float DefaultMovementHoldSeconds = 0.2f;

        public static MMOLayeredMovementPolicy Default => new(
            DefaultStartSpeedThreshold,
            DefaultStopSpeedThreshold,
            DefaultMovementHoldSeconds);

        public MMOLayeredMovementPolicy(
            float startSpeedThreshold,
            float stopSpeedThreshold,
            float movementHoldSeconds)
        {
            StartSpeedThreshold = Mathf.Max(0f, startSpeedThreshold);
            StopSpeedThreshold = Mathf.Clamp(stopSpeedThreshold, 0f, StartSpeedThreshold);
            MovementHoldSeconds = Mathf.Max(0f, movementHoldSeconds);
        }

        public float StartSpeedThreshold { get; }
        public float StopSpeedThreshold { get; }
        public float MovementHoldSeconds { get; }
    }

    /// <summary>
    /// Stateful movement classifier shared by player and creature action presentation.
    /// </summary>
    public sealed class MMOLayeredMovementState
    {
        private bool moving;
        private float lastMovementTime = float.NegativeInfinity;

        public bool IsMoving => moving;

        public bool Evaluate(float worldSpeed, float timestamp, MMOLayeredMovementPolicy policy)
        {
            float safeSpeed = Mathf.Max(0f, worldSpeed);
            if (safeSpeed >= policy.StartSpeedThreshold)
            {
                moving = true;
                lastMovementTime = timestamp;
                return true;
            }

            if (!moving)
            {
                return false;
            }

            if (safeSpeed > policy.StopSpeedThreshold)
            {
                lastMovementTime = timestamp;
            }
            else if (timestamp < lastMovementTime
                || timestamp - lastMovementTime >= policy.MovementHoldSeconds)
            {
                moving = false;
            }

            return moving;
        }

        public void Reset()
        {
            moving = false;
            lastMovementTime = float.NegativeInfinity;
        }
    }
}
