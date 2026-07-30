using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Services;
using System;
using UnityEngine;

namespace RPGClone.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(MMOInputReader))]
    public sealed class MMOPlayerMotor : MonoBehaviour, IMMOPlayerLocomotionSource
    {
        [SerializeField] private MMOPlayerMovementConfig movementConfig;
        [SerializeField] private MMOThirdPersonCamera cameraController;

        private CharacterController characterController;
        private MMOInputReader inputReader;
        private MMOCharacterIdentity identity;
        private MMOCharacterBuffController buffController;
        private readonly RaycastHit[] groundProbeHits = new RaycastHit[8];
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isGrounded;
        private bool hasGroundContact;
        private bool wasGrounded;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpBufferedUntil = float.NegativeInfinity;
        private float ignoreGroundingUntil = float.NegativeInfinity;
        private float groundContactGap = float.PositiveInfinity;

        public MMOPlayerMovementConfig MovementConfig => movementConfig;
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
            buffController = GetComponent<MMOCharacterBuffController>();
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

            bool movementPrevented = IsMovementPrevented();
            if (input.JumpPressed && !movementPrevented)
            {
                jumpBufferedUntil = Time.time + config.jumpInputBufferSeconds;
            }
            else if (movementPrevented)
            {
                jumpBufferedUntil = float.NegativeInfinity;
            }

            bool canUseContactForMovement = Time.time >= ignoreGroundingUntil && verticalVelocity <= 0f;
            bool groundedForMovement = isGrounded
                || (canUseContactForMovement && (hasGroundContact || characterController.isGrounded));
            if (groundedForMovement)
            {
                lastGroundedTime = Time.time;
            }

            UpdateFacing(input, config);
            UpdateHorizontalVelocity(input, config, groundedForMovement, movementPrevented);
            bool jumpedThisFrame = UpdateVerticalVelocity(config, groundedForMovement, movementPrevented);

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

        private void UpdateHorizontalVelocity(
            MMOInputState input,
            MMOPlayerMovementConfig config,
            bool groundedForMovement,
            bool movementPrevented)
        {
            if (movementPrevented)
            {
                horizontalVelocity = Vector3.zero;
                return;
            }

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

        private bool UpdateVerticalVelocity(
            MMOPlayerMovementConfig config,
            bool groundedForMovement,
            bool movementPrevented)
        {
            if (groundedForMovement && verticalVelocity < 0f)
            {
                bool hasImmediateSupport = characterController.isGrounded
                    || (hasGroundContact && groundContactGap <= characterController.skinWidth + 0.01f);
                verticalVelocity = hasImmediateSupport
                    ? config.groundedStickVelocity
                    : -Mathf.Max(Mathf.Abs(config.groundedStickVelocity), config.groundSnapSpeed);
            }

            bool jumpedThisFrame = false;
            if (!movementPrevented
                && Time.time <= jumpBufferedUntil
                && Time.time <= lastGroundedTime + config.jumpCoyoteSeconds)
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
                groundContactGap = 0f;
                return true;
            }

            if (ProbeGround(config, out MMOPlayerGroundingHit hit))
            {
                groundContactGap = Mathf.Max(0f, hit.BottomGap);
                return true;
            }

            groundContactGap = float.PositiveInfinity;
            return false;
        }

        private bool ProbeGround(MMOPlayerMovementConfig config, out MMOPlayerGroundingHit hit)
        {
            return MMOPlayerGroundingProbe.TryFindSupportedGround(
                characterController,
                transform,
                config,
                groundProbeHits,
                out hit);
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

        private bool IsMovementPrevented()
        {
            if (buffController == null)
            {
                buffController = GetComponent<MMOCharacterBuffController>();
            }

            return buffController != null && buffController.IsMovementPrevented;
        }
    }
}
