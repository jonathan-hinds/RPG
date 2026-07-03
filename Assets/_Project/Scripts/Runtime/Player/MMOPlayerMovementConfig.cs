using UnityEngine;

namespace RPGClone.Player
{
    [CreateAssetMenu(menuName = "RPG Clone/Player Movement Config", fileName = "PlayerMovementConfig")]
    public sealed class MMOPlayerMovementConfig : ScriptableObject
    {
        [Header("Locomotion")]
        [Min(0f)] public float forwardSpeed = 7.25f;
        [Min(0f)] public float backwardSpeed = 4.1f;
        [Min(0f)] public float strafeSpeed = 5.4f;
        [Min(0f)] public float acceleration = 34f;
        [Min(0f)] public float deceleration = 42f;

        [Header("Turning")]
        [Min(0f)] public float keyboardTurnDegreesPerSecond = 150f;
        [Min(0f)] public float mouseFacingSharpness = 24f;

        [Header("Vertical Motion")]
        [Min(0f)] public float jumpHeight = 1.55f;
        [Min(0f)] public float gravity = 28f;
        [Min(0f)] public float maxFallSpeed = 45f;
        public float groundedStickVelocity = -2f;
        [Min(0f)] public float groundSnapSpeed = 18f;

        [Header("Grounding")]
        [Min(0f)] public float groundedGraceSeconds = 0.08f;
        [Min(0f)] public float groundProbeDistance = 0.18f;
        [Range(0.1f, 1f)] public float groundProbeRadiusScale = 0.85f;
        [Range(0f, 89f)] public float groundProbeMaxSlopeAngle = 55f;

        [Header("Jump Feel")]
        [Min(0f)] public float jumpInputBufferSeconds = 0.12f;
        [Min(0f)] public float jumpCoyoteSeconds = 0.08f;
        [Min(0f)] public float jumpGroundingLockSeconds = 0.08f;
        [Min(0f)] public float airAcceleration = 14f;
        [Min(0f)] public float airDeceleration = 1.5f;
    }
}
