using UnityEngine;

namespace RPGClone.Vfx
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Ability VFX", fileName = "AbilityVfx")]
    public sealed class MMOAbilityVfxDefinition : ScriptableObject
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject targetingPrefab;
        [SerializeField] private GameObject castingPrefab;
        [SerializeField] private GameObject castPrefab;
        [SerializeField] private GameObject hitPrefab;

        [Header("Attachment")]
        [SerializeField] private bool attachCastingToCaster = true;
        [SerializeField] private bool useHandCastingAnchors = true;
        [SerializeField] private bool attachHitToTarget = true;
        [SerializeField] private bool alignCastPrefabToTarget = true;

        [Header("Offsets")]
        [SerializeField] private Vector3 castingLocalOffset = new(0f, 1.25f, 0.45f);
        [SerializeField] private Vector3 handCastingLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 castOriginLocalOffset = new(0f, 1.25f, 0.45f);
        [SerializeField] private Vector3 hitLocalOffset = new(0f, 1.05f, 0f);

        [Header("Timing")]
        [SerializeField, Min(0f)] private float hitDelaySeconds = 0.05f;
        [SerializeField] private bool castPrefabControlsHitTiming;

        public GameObject TargetingPrefab => targetingPrefab;
        public GameObject CastingPrefab => castingPrefab;
        public GameObject CastPrefab => castPrefab;
        public GameObject HitPrefab => hitPrefab;
        public bool AttachCastingToCaster => attachCastingToCaster;
        public bool UseHandCastingAnchors => useHandCastingAnchors;
        public bool AttachHitToTarget => attachHitToTarget;
        public bool AlignCastPrefabToTarget => alignCastPrefabToTarget;
        public Vector3 CastingLocalOffset => castingLocalOffset;
        public Vector3 HandCastingLocalOffset => handCastingLocalOffset;
        public Vector3 CastOriginLocalOffset => castOriginLocalOffset;
        public Vector3 HitLocalOffset => hitLocalOffset;
        public float HitDelaySeconds => hitDelaySeconds;
        public bool CastPrefabControlsHitTiming => castPrefabControlsHitTiming;

        public void Configure(
            GameObject newCastingPrefab,
            GameObject newCastPrefab,
            GameObject newHitPrefab,
            bool newAttachCastingToCaster,
            bool newUseHandCastingAnchors,
            bool newAttachHitToTarget,
            bool newAlignCastPrefabToTarget,
            Vector3 newCastingLocalOffset,
            Vector3 newHandCastingLocalOffset,
            Vector3 newCastOriginLocalOffset,
            Vector3 newHitLocalOffset,
            float newHitDelaySeconds,
            bool newCastPrefabControlsHitTiming)
        {
            castingPrefab = newCastingPrefab;
            castPrefab = newCastPrefab;
            hitPrefab = newHitPrefab;
            attachCastingToCaster = newAttachCastingToCaster;
            useHandCastingAnchors = newUseHandCastingAnchors;
            attachHitToTarget = newAttachHitToTarget;
            alignCastPrefabToTarget = newAlignCastPrefabToTarget;
            castingLocalOffset = newCastingLocalOffset;
            handCastingLocalOffset = newHandCastingLocalOffset;
            castOriginLocalOffset = newCastOriginLocalOffset;
            hitLocalOffset = newHitLocalOffset;
            hitDelaySeconds = Mathf.Max(0f, newHitDelaySeconds);
            castPrefabControlsHitTiming = newCastPrefabControlsHitTiming;
        }

        public void ConfigureTargetingPrefab(GameObject newTargetingPrefab)
        {
            targetingPrefab = newTargetingPrefab;
        }
    }
}
