using UnityEngine;

namespace RPGClone.Vfx
{
    [DisallowMultipleComponent]
    public sealed class MMOAbilityVfxAnchors : MonoBehaviour
    {
        [SerializeField] private bool autoResolveHandAnchors = true;
        [SerializeField] private Transform castingAnchor;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Transform castOriginAnchor;
        [SerializeField] private Transform hitAnchor;
        [SerializeField] private Transform centerAnchor;

        public Transform CastingAnchor => castingAnchor != null ? castingAnchor : centerAnchor;
        public bool HasExplicitCastingAnchor => castingAnchor != null;
        public Vector3 ExplicitCastingPosition => castingAnchor != null ? castingAnchor.position : transform.position;
        public Transform LeftHandAnchor
        {
            get
            {
                EnsureHandAnchors();
                return leftHandAnchor;
            }
        }

        public Transform RightHandAnchor
        {
            get
            {
                EnsureHandAnchors();
                return rightHandAnchor;
            }
        }

        public Transform CastOriginAnchor => castOriginAnchor != null ? castOriginAnchor : CastingAnchor;
        public Transform HitAnchor => hitAnchor != null ? hitAnchor : CenterAnchor;
        public Transform CenterAnchor => centerAnchor != null ? centerAnchor : transform;

        private void Awake()
        {
            EnsureHandAnchors();
        }

        public Vector3 ResolveCastingPosition(MMOAbilityVfxDefinition definition)
        {
            return ResolveLocalPosition(CastingAnchor, definition != null ? definition.CastingLocalOffset : Vector3.zero);
        }

        public Vector3 ResolveLeftHandCastingPosition(MMOAbilityVfxDefinition definition)
        {
            return ResolveLocalPosition(LeftHandAnchor, definition != null ? definition.HandCastingLocalOffset : Vector3.zero);
        }

        public Vector3 ResolveRightHandCastingPosition(MMOAbilityVfxDefinition definition)
        {
            return ResolveLocalPosition(RightHandAnchor, definition != null ? definition.HandCastingLocalOffset : Vector3.zero);
        }

        public Vector3 ResolveCastOriginPosition(MMOAbilityVfxDefinition definition)
        {
            if (castOriginAnchor != null)
            {
                return castOriginAnchor.position;
            }

            if (LeftHandAnchor != null && RightHandAnchor != null)
            {
                return Vector3.Lerp(LeftHandAnchor.position, RightHandAnchor.position, 0.5f);
            }

            return ResolveLocalPosition(CastOriginAnchor, definition != null ? definition.CastOriginLocalOffset : Vector3.zero);
        }

        public Vector3 ResolveHitPosition(MMOAbilityVfxDefinition definition)
        {
            return ResolveLocalPosition(HitAnchor, definition != null ? definition.HitLocalOffset : Vector3.zero);
        }

        public void Configure(
            Transform newCastingAnchor,
            Transform newCastOriginAnchor,
            Transform newHitAnchor = null,
            Transform newCenterAnchor = null,
            bool newAutoResolveHandAnchors = true)
        {
            autoResolveHandAnchors = newAutoResolveHandAnchors;
            castingAnchor = newCastingAnchor;
            castOriginAnchor = newCastOriginAnchor;
            hitAnchor = newHitAnchor;
            centerAnchor = newCenterAnchor;
            if (!autoResolveHandAnchors)
            {
                leftHandAnchor = null;
                rightHandAnchor = null;
            }
        }

        private Vector3 ResolveLocalPosition(Transform anchor, Vector3 localOffset)
        {
            Transform resolvedAnchor = anchor != null ? anchor : transform;
            return resolvedAnchor.TransformPoint(localOffset);
        }

        private void OnDrawGizmosSelected()
        {
            DrawAnchor(CastingAnchor, Color.yellow);
            DrawAnchor(LeftHandAnchor, Color.green);
            DrawAnchor(RightHandAnchor, Color.green);
            DrawAnchor(CastOriginAnchor, new Color(1f, 0.45f, 0.05f, 1f));
            DrawAnchor(HitAnchor, Color.cyan);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureHandAnchors();
            }
        }

        private void EnsureHandAnchors()
        {
            if (!autoResolveHandAnchors || (leftHandAnchor != null && rightHandAnchor != null))
            {
                return;
            }

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                leftHandAnchor ??= animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHandAnchor ??= animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            leftHandAnchor ??= FindBestHand(transforms, true);
            rightHandAnchor ??= FindBestHand(transforms, false);
        }

        private static Transform FindBestHand(Transform[] transforms, bool left)
        {
            Transform best = null;
            int bestScore = 0;
            foreach (Transform candidate in transforms)
            {
                string normalizedName = NormalizeName(candidate.name);
                int score = ScoreHandName(normalizedName, left);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static int ScoreHandName(string normalizedName, bool left)
        {
            if (!normalizedName.Contains("hand"))
            {
                return 0;
            }

            string side = left ? "left" : "right";
            string shortSide = left ? "l" : "r";
            string opposite = left ? "right" : "left";
            string oppositeShort = left ? "r" : "l";

            if (normalizedName.Contains(opposite + "hand") || normalizedName.Contains("hand" + opposite))
            {
                return 0;
            }

            if (normalizedName.Contains(side + "hand") || normalizedName.Contains("hand" + side))
            {
                return 100;
            }

            if (normalizedName.Contains(shortSide + "hand") || normalizedName.Contains("hand" + shortSide))
            {
                return normalizedName.Contains(oppositeShort + "hand") || normalizedName.Contains("hand" + oppositeShort) ? 0 : 70;
            }

            return 0;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .Replace(":", string.Empty);
        }

        private static void DrawAnchor(Transform anchor, Color color)
        {
            if (anchor == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(anchor.position, 0.08f);
        }
    }
}
