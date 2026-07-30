using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Empower Weapon Profile", fileName = "EmpowerWeaponVFXProfile")]
    public sealed class EmpowerWeaponVFXProfile : ScriptableObject
    {
        [Header("Palette")]
        [SerializeField] private Color mainNatureColor = new(0.08f, 1f, 0.24f, 1f);
        [SerializeField] private Color goldenHighlightColor = new(1f, 0.68f, 0.08f, 1f);
        [SerializeField] private Color paleEnergyColor = new(0.72f, 1f, 0.82f, 1f);

        [Header("Persistent Weapon")]
        [SerializeField, Min(0f)] private float weaponEmissionIntensity = 4.2f;
        [SerializeField, Min(0f)] private float pulseSpeed = 3.4f;
        [SerializeField, Min(0.25f)] private float surfacePatternScale = 1.05f;
        [SerializeField, Min(0f)] private float runeIntensity = 1.8f;
        [SerializeField, Min(0f)] private float surfaceFlowIntensity = 2.1f;
        [SerializeField, Min(0f)] private float travellingPulseSpeed = 0.72f;
        [SerializeField, Min(0f)] private float edgeCoronaIntensity = 1.5f;
        [SerializeField, Range(0f, 0.012f)] private float surfaceLift = 0.0025f;
        [SerializeField, Range(1, 24)] private int particleAmount = 8;
        [SerializeField, Min(0f)] private float auraIntensity = 1.6f;
        [SerializeField, Min(0.01f)] private float auraWidth = 0.16f;
        [SerializeField, Min(0f)] private float arcFrequency = 0.8f;
        [SerializeField, Min(0f)] private float arcBrightness = 3f;
        [SerializeField, Min(0.001f)] private float trailWidth = 0.16f;
        [SerializeField, Min(0f)] private float trailIntensity = 3f;
        [SerializeField, Range(0f, 1f)] private float sheathedIntensity = 0.42f;

        [Header("Scale and Timing")]
        [SerializeField, Min(0.01f)] private float activationScale = 1f;
        [SerializeField, Min(0.01f)] private float meleeImpactScale = 0.65f;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float activationDuration = 1.15f;
        [SerializeField, Min(0.05f)] private float attackTrailDuration = 0.38f;
        [SerializeField, Min(0.05f)] private float impactDuration = 0.48f;

        public Color MainNatureColor => mainNatureColor;
        public Color GoldenHighlightColor => goldenHighlightColor;
        public Color PaleEnergyColor => paleEnergyColor;
        public float WeaponEmissionIntensity => weaponEmissionIntensity;
        public float PulseSpeed => pulseSpeed;
        public float SurfacePatternScale => surfacePatternScale;
        public float RuneIntensity => runeIntensity;
        public float SurfaceFlowIntensity => surfaceFlowIntensity;
        public float TravellingPulseSpeed => travellingPulseSpeed;
        public float EdgeCoronaIntensity => edgeCoronaIntensity;
        public float SurfaceLift => surfaceLift;
        public int ParticleAmount => Mathf.Clamp(particleAmount, 1, 24);
        public float AuraIntensity => auraIntensity;
        public float AuraWidth => auraWidth;
        public float ArcFrequency => arcFrequency;
        public float ArcBrightness => arcBrightness;
        public float TrailWidth => trailWidth;
        public float TrailIntensity => trailIntensity;
        public float SheathedIntensity => sheathedIntensity;
        public float ActivationScale => activationScale;
        public float MeleeImpactScale => meleeImpactScale;
        public float FadeInDuration => fadeInDuration;
        public float FadeOutDuration => fadeOutDuration;
        public float ActivationDuration => activationDuration;
        public float AttackTrailDuration => attackTrailDuration;
        public float ImpactDuration => impactDuration;
    }
}
