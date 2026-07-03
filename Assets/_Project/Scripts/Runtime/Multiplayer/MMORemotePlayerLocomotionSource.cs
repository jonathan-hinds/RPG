using System;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MMORemotePlayerLocomotionSource : MonoBehaviour, IMMOPlayerLocomotionSource
    {
        [SerializeField, Min(0f)] private float teleportDistance = 8f;
        [SerializeField, Min(0f)] private float interpolationDelaySeconds = 0.12f;
        [SerializeField, Min(0f)] private float maxExtrapolationSeconds = 0.12f;
        [SerializeField, Min(0f)] private float unsupportedAirborneDelaySeconds = 0.12f;
        [SerializeField, Min(0f)] private float jumpStartYVelocityThreshold = 1.25f;

        private readonly Snapshot[] snapshots = new Snapshot[8];
        private readonly RaycastHit[] groundProbeHits = new RaycastHit[8];
        private int snapshotCount;
        private CharacterController characterController;
        private MMOPlayerMovementConfig movementConfig;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private bool hasSnapshot;
        private bool isGrounded = true;
        private bool hadGroundContact = true;
        private float unsupportedSince = float.PositiveInfinity;

        public event Action Jumped;
        public event Action BecameAirborne;
        public event Action Landed;

        public float CurrentPlanarSpeed => new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
        public Vector3 CurrentPlanarVelocity => planarVelocity;
        public float VerticalVelocity => verticalVelocity;
        public bool IsGrounded => isGrounded;
        public bool IsAirborne => !isGrounded;
        public bool HasGroundContact => hadGroundContact;
        public Vector2 CurrentLocalPlanarVelocity
        {
            get
            {
                Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
                return new Vector2(localVelocity.x, localVelocity.z);
            }
        }

        public void ApplySnapshot(Vector3 position, Quaternion rotation, long utcTicks)
        {
            EnsureReferences();
            double sampleTime = utcTicks > 0
                ? utcTicks / (double)TimeSpan.TicksPerSecond
                : DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;

            if (!hasSnapshot)
            {
                hasSnapshot = true;
                transform.SetPositionAndRotation(position, rotation);
                PushSnapshot(new Snapshot(position, rotation, sampleTime));
                return;
            }

            Snapshot latest = snapshots[snapshotCount - 1];
            if (sampleTime <= latest.Time)
            {
                return;
            }

            PushSnapshot(new Snapshot(position, rotation, sampleTime));
        }

        private void Update()
        {
            EnsureReferences();
            if (!hasSnapshot || snapshotCount == 0)
            {
                return;
            }

            double renderTime = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond - interpolationDelaySeconds;
            Sample(renderTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity);
            if ((position - transform.position).sqrMagnitude >= teleportDistance * teleportDistance)
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            verticalVelocity = velocity.y;
            UpdateGroundedState();
        }

        private void PushSnapshot(Snapshot snapshot)
        {
            if (snapshotCount == snapshots.Length)
            {
                for (int i = 1; i < snapshots.Length; i++)
                {
                    snapshots[i - 1] = snapshots[i];
                }

                snapshotCount--;
            }

            snapshots[snapshotCount] = snapshot;
            snapshotCount++;
        }

        private void Sample(double renderTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            Snapshot newest = snapshots[snapshotCount - 1];
            if (snapshotCount == 1)
            {
                position = newest.Position;
                rotation = newest.Rotation;
                velocity = Vector3.zero;
                return;
            }

            Snapshot oldest = snapshots[0];
            if (renderTime <= oldest.Time)
            {
                position = oldest.Position;
                rotation = oldest.Rotation;
                velocity = Vector3.zero;
                return;
            }

            for (int i = 0; i < snapshotCount - 1; i++)
            {
                Snapshot from = snapshots[i];
                Snapshot to = snapshots[i + 1];
                if (renderTime < from.Time || renderTime > to.Time)
                {
                    continue;
                }

                float duration = Mathf.Max(0.001f, (float)(to.Time - from.Time));
                float t = Mathf.Clamp01((float)((renderTime - from.Time) / duration));
                position = Vector3.LerpUnclamped(from.Position, to.Position, SmoothInterpolation(t));
                rotation = Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, t);
                velocity = (to.Position - from.Position) / duration;
                return;
            }

            Snapshot previous = snapshots[snapshotCount - 2];
            float latestDuration = Mathf.Max(0.001f, (float)(newest.Time - previous.Time));
            Vector3 latestVelocity = (newest.Position - previous.Position) / latestDuration;
            float timePastNewestSnapshot = (float)(renderTime - newest.Time);
            if (timePastNewestSnapshot > maxExtrapolationSeconds)
            {
                position = newest.Position;
                rotation = newest.Rotation;
                velocity = Vector3.zero;
                return;
            }

            float extrapolation = Mathf.Clamp(timePastNewestSnapshot, 0f, maxExtrapolationSeconds);
            position = newest.Position + latestVelocity * extrapolation;
            rotation = newest.Rotation;
            velocity = extrapolation > 0f ? latestVelocity : Vector3.zero;
        }

        private static float SmoothInterpolation(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void UpdateGroundedState()
        {
            bool wasGrounded = isGrounded;
            bool hasSupportedGround = characterController != null
                && movementConfig != null
                && MMOPlayerGroundingProbe.TryFindSupportedGround(
                    characterController,
                    transform,
                    movementConfig,
                    groundProbeHits,
                    out _);

            hadGroundContact = hasSupportedGround;
            if (hasSupportedGround)
            {
                unsupportedSince = float.PositiveInfinity;
                isGrounded = true;
                if (!wasGrounded)
                {
                    Landed?.Invoke();
                }

                return;
            }

            if (float.IsPositiveInfinity(unsupportedSince))
            {
                unsupportedSince = Time.time;
            }

            if (wasGrounded && Time.time < unsupportedSince + unsupportedAirborneDelaySeconds)
            {
                isGrounded = true;
                return;
            }

            isGrounded = false;
            if (!wasGrounded)
            {
                return;
            }

            if (verticalVelocity > jumpStartYVelocityThreshold)
            {
                Jumped?.Invoke();
            }

            BecameAirborne?.Invoke();
        }

        private void EnsureReferences()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (movementConfig == null && TryGetComponent(out MMOPlayerMotor motor))
            {
                movementConfig = motor.MovementConfig;
            }
        }

        private readonly struct Snapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly double Time;

            public Snapshot(Vector3 position, Quaternion rotation, double time)
            {
                Position = position;
                Rotation = rotation;
                Time = time;
            }
        }
    }
}
