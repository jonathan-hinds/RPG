using UnityEngine;

namespace RPGClone.Enemies
{
    public enum MMOEnemyLeashPhase
    {
        Idle = 0,
        Engaged = 1,
        ReturningHome = 2
    }

    /// <summary>
    /// Owns leash state independently from navigation and targeting. Combat activity moves the
    /// leash anchor, while returning home is a one-way state until the spawn point is reached.
    /// </summary>
    public sealed class MMOEnemyLeashStateMachine
    {
        public MMOEnemyLeashPhase Phase { get; private set; }
        public Vector3 AnchorPosition { get; private set; }
        public float LastCombatActivityTime { get; private set; }
        public bool IsReturningHome => Phase == MMOEnemyLeashPhase.ReturningHome;

        public void Reset(Vector3 homePosition)
        {
            Phase = MMOEnemyLeashPhase.Idle;
            AnchorPosition = homePosition;
            LastCombatActivityTime = float.NegativeInfinity;
        }

        public bool BeginEngagement(Vector3 position, float currentTime)
        {
            if (IsReturningHome)
            {
                return false;
            }

            if (Phase == MMOEnemyLeashPhase.Idle)
            {
                AnchorPosition = position;
                LastCombatActivityTime = currentTime;
            }

            Phase = MMOEnemyLeashPhase.Engaged;
            return true;
        }

        public bool RecordCombatActivity(Vector3 position, float currentTime)
        {
            if (IsReturningHome)
            {
                return false;
            }

            AnchorPosition = position;
            Phase = MMOEnemyLeashPhase.Engaged;
            LastCombatActivityTime = currentTime;
            return true;
        }

        public bool ShouldReturnHome(Vector3 targetPosition, float leashRadius, float graceSeconds, float currentTime)
        {
            if (Phase != MMOEnemyLeashPhase.Engaged)
            {
                return false;
            }

            if (currentTime < LastCombatActivityTime + Mathf.Max(0f, graceSeconds))
            {
                return false;
            }

            Vector3 offset = targetPosition - AnchorPosition;
            offset.y = 0f;
            float radius = Mathf.Max(0f, leashRadius);
            return offset.sqrMagnitude > radius * radius;
        }

        public void BeginReturnHome()
        {
            Phase = MMOEnemyLeashPhase.ReturningHome;
        }

        public bool IsAtHome(Vector3 position, Vector3 homePosition, float arrivalDistance)
        {
            Vector3 offset = position - homePosition;
            offset.y = 0f;
            float radius = Mathf.Max(0.01f, arrivalDistance);
            return offset.sqrMagnitude <= radius * radius;
        }

        public void CompleteReturnHome(Vector3 homePosition)
        {
            Reset(homePosition);
        }

        public void ApplyReplicatedState(bool inCombat, bool returningHome, Vector3 anchorPosition, Vector3 homePosition)
        {
            AnchorPosition = anchorPosition;
            Phase = returningHome
                ? MMOEnemyLeashPhase.ReturningHome
                : inCombat
                    ? MMOEnemyLeashPhase.Engaged
                    : MMOEnemyLeashPhase.Idle;

            if (Phase == MMOEnemyLeashPhase.Idle)
            {
                AnchorPosition = homePosition;
                LastCombatActivityTime = float.NegativeInfinity;
            }
        }
    }
}
