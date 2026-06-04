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
        private Vector3 horizontalVelocity;
        private float verticalVelocity;

        public float CurrentPlanarSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputReader = GetComponent<MMOInputReader>();
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

            UpdateFacing(input, config);
            UpdateHorizontalVelocity(input, config);
            UpdateVerticalVelocity(input, config);

            Vector3 motion = horizontalVelocity;
            motion.y = verticalVelocity;
            characterController.Move(motion * Time.deltaTime);
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

        private void UpdateHorizontalVelocity(MMOInputState input, MMOPlayerMovementConfig config)
        {
            Vector3 desiredVelocity = Vector3.zero;

            if (!Mathf.Approximately(input.Forward, 0f))
            {
                float speed = input.Forward > 0f ? config.forwardSpeed : config.backwardSpeed;
                desiredVelocity += transform.forward * (input.Forward * speed);
            }

            if (!Mathf.Approximately(input.Strafe, 0f))
            {
                desiredVelocity += transform.right * (input.Strafe * config.strafeSpeed);
            }

            float moveRate = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude
                ? config.acceleration
                : config.deceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                moveRate * Time.deltaTime);
        }

        private void UpdateVerticalVelocity(MMOInputState input, MMOPlayerMovementConfig config)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = config.groundedStickVelocity;
            }

            if (characterController.isGrounded && input.JumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);
            }

            verticalVelocity -= config.gravity * Time.deltaTime;
        }
    }
}
