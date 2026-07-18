using UnityEngine;
using UnityEngine.Serialization;

namespace RPGClone.Vfx.Healing
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Healing Beam Profile", fileName = "HealingBeamVFXProfile")]
    public sealed class HealingBeamVFXProfile : ScriptableObject
    {
        [Header("Beam Shape")]
        [SerializeField, Min(0.02f)] private float beamWidth = 0.24f;
        [SerializeField, Range(6, 24)] private int beamSegments = 12;
        [SerializeField, Min(0.1f)] private float textureWorldLength = 1.1f;
        [SerializeField, Min(0f)] private float beamSway = 0.11f;
        [SerializeField, Min(0f)] private float beamSwaySpeed = 1.25f;
        [SerializeField, Min(0f)] private float beamArcHeight = 0.12f;

        [Header("Beam Color")]
        [SerializeField, ColorUsage(true, true)] private Color outerGlowColor = new(1f, 0.68f, 0.18f, 0.32f);
        [SerializeField, ColorUsage(true, true)] private Color ribbonColor = new(1f, 0.82f, 0.34f, 0.82f);
        [SerializeField, ColorUsage(true, true)] private Color coreColor = new(1.25f, 1.15f, 0.82f, 0.92f);
        [SerializeField, Min(0f)] private float glowIntensity = 1.15f;

        [Header("Beam Flow")]
        [SerializeField] private float glowFlowSpeed = -0.28f;
        [SerializeField] private float ribbonFlowSpeed = -0.82f;
        [SerializeField] private float coreFlowSpeed = -1.18f;
        [SerializeField, Range(0f, 0.25f)] private float distortionStrength = 0.045f;

        [Header("Healing Pulse")]
        [SerializeField, Min(0.1f)] private float pulseSpeed = 1.7f;
        [SerializeField, Range(0.03f, 0.4f)] private float pulseWidth = 0.16f;
        [SerializeField, Min(0f)] private float pulseBrightness = 2.2f;
        [SerializeField, Min(0.01f)] private float tickFlashDuration = 0.3f;

        [Header("Cast Buildup")]
        [SerializeField, Range(0.25f, 1f)] private float chargeStartScale = 0.68f;
        [SerializeField, Range(0f, 0.2f)] private float chargePulseAmount = 0.07f;
        [SerializeField, Min(1f)] private float chargeOrbitSpeedMultiplier = 1.65f;
        [SerializeField, Min(0.25f)] private float casterGroundRingSize = 3.6f;
        [SerializeField] private float casterGroundVerticalOffset = 0.04f;
        [SerializeField, Range(0f, 1f)] private float casterGroundRingOpacity = 0.92f;
        [SerializeField, Min(0.25f)] private float casterBuildupCylinderHeight = 1.9f;
        [SerializeField, Min(0.05f)] private float casterRingRiseSpeed = 0.58f;
        [SerializeField, Range(0, 64)] private int casterDustParticleCount = 40;
        [SerializeField, Min(0.01f)] private float casterDustParticleSize = 0.18f;
        [SerializeField, Min(0.1f)] private float casterDustRingRadius = 1.55f;
        [SerializeField, Min(0.05f)] private float casterDustRiseSpeed = 1.1f;

        [Header("Launch and Impact")]
        [SerializeField, Min(0.05f)] private float beamLaunchDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float targetArrivalFadeDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float impactHaloDuration = 0.52f;
        [SerializeField, Min(0.01f)] private float impactHaloStartSize = 0.38f;
        [SerializeField, Min(0.01f)] private float impactHaloEndSize = 2.35f;
        [SerializeField, Min(0.1f)] private float impactSparkSizeMultiplier = 1.45f;
        [SerializeField, Min(0.1f)] private float targetImpactEchoDuration = 0.58f;

        [Header("Particles")]
        [SerializeField, Range(0, 16)] private int casterOrbitParticleCount = 7;
        [SerializeField, Range(0, 16)] private int casterInwardParticleCount = 6;
        [SerializeField, Range(0, 12)] private int casterLeafParticleCount = 4;
        [SerializeField, Range(0, 24)] private int targetRisingParticleCount = 9;
        [SerializeField, Range(0, 16)] private int targetSparkleCount = 5;
        [SerializeField, Range(0, 16)] private int tickSparkCount = 7;
        [SerializeField, Range(0, 16)] private int impactLeafCount = 7;
        [SerializeField, Min(0.01f)] private float particleSize = 0.14f;
        [SerializeField, Min(0.01f)] private float leafParticleSize = 0.22f;

        [Header("Effect Scale")]
        [SerializeField, Min(0.01f)] private float casterEffectScale = 1f;
        [FormerlySerializedAs("casterOrbSizeMultiplier")]
        [SerializeField, Min(0.01f)] private float endpointOrbSizeMultiplier = 1.5f;
        [FormerlySerializedAs("casterSparkleSizeMultiplier")]
        [SerializeField, Min(0.01f)] private float endpointSparkleSizeMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float targetEffectScale = 1f;
        [SerializeField, Min(0.05f)] private float groundRingSize = 2.2f;
        [SerializeField] private float groundRingVerticalOffset = -1.05f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.38f;

        [Header("Master")]
        [SerializeField, Range(0f, 2f)] private float overallIntensity = 1f;

        public float BeamWidth => beamWidth;
        public int BeamSegments => beamSegments;
        public float TextureWorldLength => textureWorldLength;
        public float BeamSway => beamSway;
        public float BeamSwaySpeed => beamSwaySpeed;
        public float BeamArcHeight => beamArcHeight;
        public Color OuterGlowColor => outerGlowColor;
        public Color RibbonColor => ribbonColor;
        public Color CoreColor => coreColor;
        public float GlowIntensity => glowIntensity;
        public float GlowFlowSpeed => glowFlowSpeed;
        public float RibbonFlowSpeed => ribbonFlowSpeed;
        public float CoreFlowSpeed => coreFlowSpeed;
        public float DistortionStrength => distortionStrength;
        public float PulseSpeed => pulseSpeed;
        public float PulseWidth => pulseWidth;
        public float PulseBrightness => pulseBrightness;
        public float TickFlashDuration => tickFlashDuration;
        public float ChargeStartScale => chargeStartScale;
        public float ChargePulseAmount => chargePulseAmount;
        public float ChargeOrbitSpeedMultiplier => chargeOrbitSpeedMultiplier;
        public float CasterGroundRingSize => casterGroundRingSize;
        public float CasterGroundVerticalOffset => casterGroundVerticalOffset;
        public float CasterGroundRingOpacity => casterGroundRingOpacity;
        public float CasterBuildupCylinderHeight => casterBuildupCylinderHeight;
        public float CasterRingRiseSpeed => casterRingRiseSpeed;
        public int CasterDustParticleCount => casterDustParticleCount;
        public float CasterDustParticleSize => casterDustParticleSize;
        public float CasterDustRingRadius => casterDustRingRadius;
        public float CasterDustRiseSpeed => casterDustRiseSpeed;
        public float BeamLaunchDuration => beamLaunchDuration;
        public float TargetArrivalFadeDuration => targetArrivalFadeDuration;
        public float ImpactHaloDuration => impactHaloDuration;
        public float ImpactHaloStartSize => impactHaloStartSize;
        public float ImpactHaloEndSize => impactHaloEndSize;
        public float ImpactSparkSizeMultiplier => impactSparkSizeMultiplier;
        public float TargetImpactEchoDuration => targetImpactEchoDuration;
        public int CasterOrbitParticleCount => casterOrbitParticleCount;
        public int CasterInwardParticleCount => casterInwardParticleCount;
        public int CasterLeafParticleCount => casterLeafParticleCount;
        public int TargetRisingParticleCount => targetRisingParticleCount;
        public int TargetSparkleCount => targetSparkleCount;
        public int TickSparkCount => tickSparkCount;
        public int ImpactLeafCount => impactLeafCount;
        public float ParticleSize => particleSize;
        public float LeafParticleSize => leafParticleSize;
        public float CasterEffectScale => casterEffectScale;
        public float EndpointOrbSizeMultiplier => endpointOrbSizeMultiplier;
        public float EndpointSparkleSizeMultiplier => endpointSparkleSizeMultiplier;
        public float TargetEffectScale => targetEffectScale;
        public float GroundRingSize => groundRingSize;
        public float GroundRingVerticalOffset => groundRingVerticalOffset;
        public float FadeInDuration => fadeInDuration;
        public float FadeOutDuration => fadeOutDuration;
        public float OverallIntensity => overallIntensity;
    }
}
