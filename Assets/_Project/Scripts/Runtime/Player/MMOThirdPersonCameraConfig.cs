using UnityEngine;

namespace RPGClone.Player
{
    [CreateAssetMenu(menuName = "RPG Clone/Third Person Camera Config", fileName = "ThirdPersonCameraConfig")]
    public sealed class MMOThirdPersonCameraConfig : ScriptableObject
    {
        [Header("Orbit")]
        public float defaultDistance = 8.5f;
        public float minDistance = 2.2f;
        public float maxDistance = 16f;
        public float targetHeight = 1.45f;
        public float defaultPitch = 18f;
        public float minPitch = -18f;
        public float maxPitch = 66f;

        [Header("Input")]
        public float mouseYawSensitivity = 0.12f;
        public float mousePitchSensitivity = 0.1f;
        public float zoomUnitsPerScrollUnit = 0.02f;

        [Header("Smoothing")]
        public float idleYawFollowSharpness = 8f;
        public float positionSharpness = 18f;

        [Header("Collision")]
        public float collisionRadius = 0.28f;
        public float collisionPadding = 0.18f;
        public LayerMask collisionMask = ~0;
    }
}
