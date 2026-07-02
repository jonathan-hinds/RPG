using RPGClone.Characters;
using RPGClone.Services;
using System;
using UnityEngine;

namespace RPGClone.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MMOInputReader))]
    public sealed class MMOPlayerMotor : MonoBehaviour
    {
        [SerializeField] private MMOPlayerMovementConfig movementConfig;
        [SerializeField] private MMOThirdPersonCamera cameraController;

        private CharacterController characterController;
        private MMOInputReader inputReader;
        private MMOCharacterIdentity identity;
        private readonly RaycastHit[] groundProbeHits = new RaycastHit[8];
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isGrounded;
        private bool hasGroundContact;
        private bool wasGrounded;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpBufferedUntil = float.NegativeInfinity;
        private float ignoreGroundingUntil = float.NegativeInfinity;

        public float CurrentPlanarSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        public Vector3 CurrentPlanarVelocity => horizontalVelocity;
        public float VerticalVelocity => verticalVelocity;
        public bool IsGrounded => isGrounded;
        public bool IsAirborne => !isGrounded;
        public bool HasGroundContact => hasGroundContact;
        public event Action Jumped;
        public event Action BecameAirborne;
        public event Action Landed;
        public Vector2 CurrentLocalPlanarVelocity
        {
            get
            {
                Vector3 localVelocity = transform.InverseTransformDirection(horizontalVelocity);
                return new Vector2(localVelocity.x, localVelocity.z);
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputReader = GetComponent<MMOInputReader>();
            identity = GetComponent<MMOCharacterIdentity>();
            isGrounded = characterController.isGrounded;
            hasGroundContact = isGrounded;
            wasGrounded = isGrounded;
        }

        private void Start()
        {
            Camera localCamera = MMOGameplaySessionService.LocalPlayer.MainCamera;
            if (cameraController == null && localCamera != null)
            {
                cameraController = localCamera.GetComponent<MMOThirdPersonCamera>();
            }
        }

        private void Update()
        {
            MMOInputState input = inputReader.Current;
            MMOPlayerMovementConfig config = movementConfig;
            if (config == null)
            {
                Debug.LogWarning($"{nameof(MMOPlayerMotor)} on {name} has no movement config.", this);
                return;
            }

            if (input.JumpPressed)
            {
                jumpBufferedUntil = Time.time + config.jumpInputBufferSeconds;
            }

            bool canUseContactForMovement = Time.time >= ignoreGroundingUntil && verticalVelocity <= 0f;
            bool groundedForMovement = isGrounded
                || (canUseContactForMovement && (hasGroundContact || characterController.isGrounded));
            if (groundedForMovement)
            {
                lastGroundedTime = Time.time;
            }

            UpdateFacing(input, config);
            UpdateHorizontalVelocity(input, config, groundedForMovement);
            bool jumpedThisFrame = UpdateVerticalVelocity(config, groundedForMovement);

            Vector3 motion = horizontalVelocity;
            motion.y = verticalVelocity;
            CollisionFlags collisionFlags = characterController.Move(motion * Time.deltaTime);
            UpdateGrounding(collisionFlags, config, jumpedThisFrame);
        }

        private void UpdateFacing(MMOInputState input, MMOPlayerMovementConfig config)
        {
            if (input.RightMouseHeld && cameraController != null)
            {
                Quaternion targetRotation = Quaternion.Euler(0f, cameraController.PlanarYaw, 0f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-config.mouseFacingSharpness * Time.deltaTime));
                return;
            }

            if (!Mathf.Approximately(input.Turn, 0f))
            {
                transform.Rotate(0f, input.Turn * config.keyboardTurnDegreesPerSecond * Time.deltaTime, 0f);
            }
        }

        private void UpdateHorizontalVelocity(MMOInputState input, MMOPlayerMovementConfig config, bool groundedForMovement)
        {
            Vector3 desiredVelocity = Vector3.zero;

            if (!Mathf.Approximately(input.Forward, 0f))
            {
                float speed = (input.Forward > 0f ? config.forwardSpeed : config.backwardSpeed) * GetMovementSpeedMultiplier();
                desiredVelocity += transform.forward * (input.Forward * speed);
            }

            if (!Mathf.Approximately(input.Strafe, 0f))
            {
                desiredVelocity += transform.right * (input.Strafe * config.strafeSpeed * GetMovementSpeedMultiplier());
            }

            float moveRate = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                ? (groundedForMovement ? config.acceleration : config.airAcceleration)
                : (groundedForMovement ? config.deceleration : config.airDeceleration);

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                moveRate * Time.deltaTime);
        }

        private bool UpdateVerticalVelocity(MMOPlayerMovementConfig config, bool groundedForMovement)
        {
            if (groundedForMovement && verticalVelocity < 0f)
            {
                verticalVelocity = config.groundedStickVelocity;
            }

            bool jumpedThisFrame = false;
            if (Time.time <= jumpBufferedUntil && Time.time <= lastGroundedTime + config.jumpCoyoteSeconds)
            {
                verticalVelocity = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);
                jumpBufferedUntil = float.NegativeInfinity;
                isGrounded = false;
                hasGroundContact = false;
                ignoreGroundingUntil = Time.time + config.jumpGroundingLockSeconds;
                jumpedThisFrame = true;
                Jumped?.Invoke();
            }

            verticalVelocity -= config.gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -config.maxFallSpeed);
            return jumpedThisFrame;
        }

        private void UpdateGrounding(CollisionFlags collisionFlags, MMOPlayerMovementConfig config, bool jumpedThisFrame)
        {
            wasGrounded = isGrounded;
            hasGroundContact = !jumpedThisFrame && HasSupportedGround(collisionFlags, config);
            bool acceptsGrounding = hasGroundContact
                && Time.time >= ignoreGroundingUntil
                && verticalVelocity <= 0f;

            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if (acceptsGrounding)
            {
                isGrounded = true;
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = movementConfig != null
                        ? movementConfig.groundedStickVelocity
                        : -2f;
                }

                if (!wasGrounded)
                {
                    Landed?.Invoke();
                }

                return;
            }

            if (CanKeepGroundedDuringGrace(config))
            {
                isGrounded = true;
                return;
            }

            isGrounded = false;
            if (wasGrounded)
            {
                BecameAirborne?.Invoke();
            }
        }

        private bool HasSupportedGround(CollisionFlags collisionFlags, MMOPlayerMovementConfig config)
        {
            if ((collisionFlags & CollisionFlags.Below) != 0 || characterController.isGrounded)
            {
                return true;
            }

            return ProbeGround(config);
        }

        private bool ProbeGround(MMOPlayerMovementConfig config)
        {
            if (config.groundProbeDistance <= 0f || characterController == null)
            {
                return false;
            }

            float radius = Mathf.Max(0.01f, characterController.radius * config.groundProbeRadiusScale);
            float halfHeight = Mathf.Max(characterController.height * 0.5f, radius);
            Vector3 center = transform.TransformPoint(characterController.center);
            Vector3 origin = center + Vector3.up * config.groundProbeDistance;
            float castDistance = Mathf.Max(0f, halfHeight - radius)
                + config.groundProbeDistance
                + characterController.skinWidth;
            float maxSlopeAngle = Mathf.Min(config.groundProbeMaxSlopeAngle, characterController.slopeLimit + 0.1f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                groundProbeHits,
                castDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = groundProbeHits[i].collider;
                if (hitCollider == null
                    || hitCollider == characterController
                    || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (Vector3.Angle(groundProbeHits[i].normal, Vector3.up) <= maxSlopeAngle)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanKeepGroundedDuringGrace(MMOPlayerMovementConfig config)
        {
            return wasGrounded
                && Time.time >= ignoreGroundingUntil
                && verticalVelocity <= 0f
                && Time.time <= lastGroundedTime + config.groundedGraceSeconds;
        }

        private float GetMovementSpeedMultiplier()
        {
            return identity != null && identity.Stats != null ? identity.Stats.MovementSpeedMultiplier : 1f;
        }
    }
}
