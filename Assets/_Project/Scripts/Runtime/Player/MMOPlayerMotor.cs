using RPGClone.Characters;
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
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool isGrounded;
        private bool wasGrounded;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpBufferedUntil = float.NegativeInfinity;

        public float CurrentPlanarSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        public Vector3 CurrentPlanarVelocity => horizontalVelocity;
        public float VerticalVelocity => verticalVelocity;
        public bool IsGrounded => isGrounded;
        public bool IsAirborne => !isGrounded;
        public event Action Jumped;
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
            wasGrounded = isGrounded;
        }

        private void Start()
        {
            if (cameraController == null && Camera.main != null)
            {
                cameraController = Camera.main.GetComponent<MMOThirdPersonCamera>();
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

            bool groundedForMovement = isGrounded || characterController.isGrounded;
            if (groundedForMovement)
            {
                lastGroundedTime = Time.time;
            }

            UpdateFacing(input, config);
            UpdateHorizontalVelocity(input, config, groundedForMovement);
            UpdateVerticalVelocity(config, groundedForMovement);

            Vector3 motion = horizontalVelocity;
            motion.y = verticalVelocity;
            CollisionFlags collisionFlags = characterController.Move(motion * Time.deltaTime);
            UpdateGrounding(collisionFlags);
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

        private void UpdateVerticalVelocity(MMOPlayerMovementConfig config, bool groundedForMovement)
        {
            if (groundedForMovement && verticalVelocity < 0f)
            {
                verticalVelocity = config.groundedStickVelocity;
            }

            if (Time.time <= jumpBufferedUntil && Time.time <= lastGroundedTime + config.jumpCoyoteSeconds)
            {
                verticalVelocity = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);
                jumpBufferedUntil = float.NegativeInfinity;
                isGrounded = false;
                Jumped?.Invoke();
            }

            verticalVelocity -= config.gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -config.maxFallSpeed);
        }

        private void UpdateGrounding(CollisionFlags collisionFlags)
        {
            wasGrounded = isGrounded;
            isGrounded = (collisionFlags & CollisionFlags.Below) != 0;

            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if (isGrounded)
            {
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
            }
        }

        private float GetMovementSpeedMultiplier()
        {
            return identity != null && identity.Stats != null ? identity.Stats.MovementSpeedMultiplier : 1f;
        }
    }
}
