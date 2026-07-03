using UnityEngine;

namespace RPGClone.Player
{
    public readonly struct MMOPlayerGroundingHit
    {
        public MMOPlayerGroundingHit(RaycastHit hit, float bottomGap)
        {
            Hit = hit;
            BottomGap = bottomGap;
        }

        public RaycastHit Hit { get; }
        public float BottomGap { get; }
        public Vector3 Normal => Hit.normal;
        public Collider Collider => Hit.collider;
    }

    public static class MMOPlayerGroundingProbe
    {
        public static bool TryFindSupportedGround(
            CharacterController characterController,
            Transform owner,
            MMOPlayerMovementConfig config,
            RaycastHit[] hits,
            out MMOPlayerGroundingHit groundingHit)
        {
            groundingHit = default;
            if (characterController == null
                || owner == null
                || config == null
                || hits == null
                || hits.Length == 0
                || config.groundProbeDistance <= 0f)
            {
                return false;
            }

            float radius = Mathf.Max(0.01f, characterController.radius * config.groundProbeRadiusScale);
            float halfHeight = Mathf.Max(characterController.height * 0.5f, radius);
            Vector3 center = owner.TransformPoint(characterController.center);
            Vector3 origin = center + Vector3.up * config.groundProbeDistance;
            float capsuleCenterToBottomSphereCenter = Mathf.Max(0f, halfHeight - radius);
            float castDistance = capsuleCenterToBottomSphereCenter
                + (config.groundProbeDistance * 2f)
                + characterController.skinWidth;
            float maxSlopeAngle = Mathf.Min(config.groundProbeMaxSlopeAngle, characterController.slopeLimit + 0.1f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                hits,
                castDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestGap = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null
                    || hitCollider == characterController
                    || hitCollider.transform.IsChildOf(owner))
                {
                    continue;
                }

                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle)
                {
                    continue;
                }

                float bottomGap = hit.distance - capsuleCenterToBottomSphereCenter - config.groundProbeDistance;
                if (bottomGap >= closestGap)
                {
                    continue;
                }

                closestGap = bottomGap;
                groundingHit = new MMOPlayerGroundingHit(hit, bottomGap);
                found = true;
            }

            return found;
        }
    }
}
