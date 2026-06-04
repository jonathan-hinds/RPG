using UnityEngine;

namespace RPGClone.Player
{
    public sealed class MMOThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private MMOInputReader inputReader;
        [SerializeField] private MMOThirdPersonCameraConfig cameraConfig;

        private float yaw;
        private float pitch;
        private float distance;
        private Vector3 smoothedPosition;
        private bool initialized;

        public float PlanarYaw => yaw;

        private void Start()
        {
            InitializeState();
        }

        private void LateUpdate()
        {
            if (target == null || cameraConfig == null)
            {
                return;
            }

            InitializeState();

            MMOInputState input = inputReader != null ? inputReader.Current : default;
            UpdateOrbit(input);
            UpdateZoom(input);
            ApplyCameraTransform(input);
        }

        public void SetTarget(Transform newTarget, MMOInputReader newInputReader)
        {
            target = newTarget;
            inputReader = newInputReader;
            initialized = false;
        }

        private void InitializeState()
        {
            if (initialized || target == null || cameraConfig == null)
            {
                return;
            }

            yaw = target.eulerAngles.y;
            pitch = cameraConfig.defaultPitch;
            distance = Mathf.Clamp(cameraConfig.defaultDistance, cameraConfig.minDistance, cameraConfig.maxDistance);
            smoothedPosition = transform.position;
            initialized = true;
        }

        private void UpdateOrbit(MMOInputState input)
        {
            if (input.IsMouseLooking)
            {
                yaw += input.MouseDelta.x * cameraConfig.mouseYawSensitivity;
                pitch -= input.MouseDelta.y * cameraConfig.mousePitchSensitivity;
                pitch = Mathf.Clamp(pitch, cameraConfig.minPitch, cameraConfig.maxPitch);
                return;
            }

            yaw = Mathf.LerpAngle(
                yaw,
                target.eulerAngles.y,
                1f - Mathf.Exp(-cameraConfig.idleYawFollowSharpness * Time.deltaTime));
        }

        private void UpdateZoom(MMOInputState input)
        {
            if (Mathf.Approximately(input.ZoomDelta, 0f))
            {
                return;
            }

            distance = Mathf.Clamp(
                distance - input.ZoomDelta * cameraConfig.zoomUnitsPerScrollUnit,
                cameraConfig.minDistance,
                cameraConfig.maxDistance);
        }

        private void ApplyCameraTransform(MMOInputState input)
        {
            Vector3 pivot = target.position + Vector3.up * cameraConfig.targetHeight;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredOffset = rotation * new Vector3(0f, 0f, -distance);
            Vector3 desiredPosition = pivot + desiredOffset;
            Vector3 correctedPosition = ResolveCollision(pivot, desiredPosition);

            float sharpness = input.IsMouseLooking ? 1000f : cameraConfig.positionSharpness;
            smoothedPosition = Vector3.Lerp(
                smoothedPosition,
                correctedPosition,
                1f - Mathf.Exp(-sharpness * Time.deltaTime));

            transform.SetPositionAndRotation(smoothedPosition, rotation);
        }

        private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPosition)
        {
            Vector3 toCamera = desiredPosition - pivot;
            float desiredDistance = toCamera.magnitude;
            if (desiredDistance <= Mathf.Epsilon)
            {
                return desiredPosition;
            }

            Vector3 direction = toCamera / desiredDistance;
            if (Physics.SphereCast(
                    pivot,
                    cameraConfig.collisionRadius,
                    direction,
                    out RaycastHit hit,
                    desiredDistance,
                    cameraConfig.collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                float correctedDistance = Mathf.Max(hit.distance - cameraConfig.collisionPadding, cameraConfig.minDistance);
                return pivot + direction * correctedDistance;
            }

            return desiredPosition;
        }
    }
}
