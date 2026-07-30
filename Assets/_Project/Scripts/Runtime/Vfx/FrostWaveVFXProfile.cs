using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    public enum FrostWaveParticleQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Frost Wave Profile", fileName = "FrostWaveVFX_Default")]
    public sealed class FrostWaveVFXProfile : ScriptableObject
    {
        [Header("Wave")]
        [SerializeField, Min(0.5f)] private float effectRadius = 8f;
        [SerializeField, Range(0.2f, 0.65f)] private float ringExpansionDuration = 0.38f;
        [SerializeField, Range(0f, 0.2f)] private float primaryRingDelay = 0.03f;
        [SerializeField, Range(0f, 0.25f)] private float secondaryRingDelay = 0.06f;
        [SerializeField, Range(0.5f, 2f)] private float overallIntensity = 1f;

        [Header("Radial Front")]
        [SerializeField, Range(16, 96)] private int radialCloudDensity = 56;
        [SerializeField, Range(0.75f, 4f)] private float radialCloudSize = 2.75f;
        [SerializeField, Range(0.25f, 1.5f)] private float radialCloudLifetime = 0.72f;
        [SerializeField, Range(0f, 5f)] private float radialCloudDrift = 1.8f;
        [SerializeField, Range(0f, 3f)] private float radialCloudLift = 1.15f;
        [SerializeField, Range(6, 32)] private int iceBreakerDensity = 16;
        [SerializeField, Range(0.75f, 4f)] private float iceBreakerSize = 2.1f;
        [SerializeField, Range(0.2f, 1.2f)] private float iceBreakerLifetime = 0.58f;

        [Header("Ground Frost")]
        [SerializeField, Range(0.5f, 2.5f)] private float groundFrostDuration = 1.15f;
        [SerializeField, Range(0f, 0.3f)] private float groundRevealDelay = 0.1f;
        [SerializeField, Range(0.001f, 0.1f)] private float groundOffset = 0.035f;

        [Header("Particles")]
        [SerializeField, Range(0.25f, 2f)] private float particleAmount = 1f;
        [SerializeField, Range(0, 96)] private int openingSnowAmount = 28;
        [SerializeField, Range(0, 64)] private int outwardShardAmount = 18;
        [SerializeField, Range(0, 128)] private int waveSnowAmount = 46;
        [SerializeField, Range(0, 64)] private int frostStreakAmount = 22;
        [SerializeField, Range(0, 64)] private int mistAmount = 20;

        [Header("Light")]
        [SerializeField, Min(0f)] private float lightIntensity = 2.2f;
        [SerializeField, Min(0.1f)] private float lightRadius = 5f;
        [SerializeField, Range(0.05f, 0.5f)] private float lightDuration = 0.22f;

        [Header("Enemy Impact")]
        [SerializeField, Range(0.25f, 2f)] private float enemyImpactScale = 1f;
        [SerializeField, Range(0.2f, 1.5f)] private float impactDuration = 0.72f;
        [SerializeField, Range(0.5f, 6f)] private float rootIndicatorDuration = 3f;
        [SerializeField, Range(0, 48)] private int impactSnowAmount = 15;
        [SerializeField, Range(0, 32)] private int impactShardAmount = 8;
        [SerializeField, Range(0, 32)] private int impactMistAmount = 7;

        [Header("Placement")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.5f)] private float groundProbeHeight = 2.5f;
        [SerializeField, Min(0.5f)] private float groundProbeDistance = 8f;

        [Header("Scalability")]
        [SerializeField] private FrostWaveParticleQuality particleQuality = FrostWaveParticleQuality.High;
        [SerializeField, Range(4, 32)] private int targetReactionPoolSize = 16;
        [SerializeField, Min(10f)] private float distantReductionStart = 24f;
        [SerializeField, Min(15f)] private float cullDistance = 55f;
        [SerializeField] private bool optionalCameraImpulse;

        [Header("Palette")]
        [SerializeField] private Color whiteHot = new(1f, 1.15f, 1.3f, 1f);
        [SerializeField] private Color paleCyan = new(0.48f, 1f, 1.25f, 1f);
        [SerializeField] private Color saturatedBlue = new(0.08f, 0.48f, 1f, 1f);
        [SerializeField] private Color deepBlue = new(0.015f, 0.12f, 0.55f, 1f);
        [SerializeField] private Color mistTint = new(0.42f, 0.78f, 1f, 0.68f);

        public float EffectRadius => effectRadius;
        public float RingExpansionDuration => ringExpansionDuration;
        public float PrimaryRingDelay => primaryRingDelay;
        public float SecondaryRingDelay => secondaryRingDelay;
        public float OverallIntensity => overallIntensity;
        public int RadialCloudDensity => ScaleCount(radialCloudDensity);
        public float RadialCloudSize => radialCloudSize * overallIntensity;
        public float RadialCloudLifetime => radialCloudLifetime;
        public float RadialCloudDrift => radialCloudDrift;
        public float RadialCloudLift => radialCloudLift;
        public int IceBreakerDensity => ScaleCount(iceBreakerDensity);
        public float IceBreakerSize => iceBreakerSize * overallIntensity;
        public float IceBreakerLifetime => iceBreakerLifetime;
        public float GroundFrostDuration => groundFrostDuration;
        public float GroundRevealDelay => groundRevealDelay;
        public float GroundOffset => groundOffset;
        public float ParticleAmount => particleAmount;
        public int OpeningSnowAmount => ScaleCount(openingSnowAmount);
        public int OutwardShardAmount => ScaleCount(outwardShardAmount);
        public int WaveSnowAmount => ScaleCount(waveSnowAmount);
        public int FrostStreakAmount => ScaleCount(frostStreakAmount);
        public int MistAmount => ScaleCount(mistAmount);
        public float LightIntensity => lightIntensity * overallIntensity;
        public float LightRadius => lightRadius;
        public float LightDuration => lightDuration;
        public float EnemyImpactScale => enemyImpactScale;
        public float ImpactDuration => impactDuration;
        public float RootIndicatorDuration => rootIndicatorDuration;
        public int ImpactSnowAmount => ScaleCount(impactSnowAmount);
        public int ImpactShardAmount => ScaleCount(impactShardAmount);
        public int ImpactMistAmount => ScaleCount(impactMistAmount);
        public LayerMask GroundLayers => groundLayers;
        public float GroundProbeHeight => groundProbeHeight;
        public float GroundProbeDistance => groundProbeDistance;
        public int TargetReactionPoolSize => targetReactionPoolSize;
        public float DistantReductionStart => distantReductionStart;
        public float CullDistance => Mathf.Max(distantReductionStart + 1f, cullDistance);
        public bool OptionalCameraImpulse => optionalCameraImpulse;
        public Color WhiteHot => whiteHot;
        public Color PaleCyan => paleCyan;
        public Color SaturatedBlue => saturatedBlue;
        public Color DeepBlue => deepBlue;
        public Color MistTint => mistTint;
        public float TotalLifetime => Mathf.Max(groundFrostDuration + groundRevealDelay, ringExpansionDuration + secondaryRingDelay + 0.75f);
        public float ControllerLifetime => Mathf.Max(TotalLifetime, rootIndicatorDuration + 1.25f);

        public float ResolveRadius(RPGClone.Abilities.MMOAbilityDefinition ability)
        {
            return ability != null && ability.HasArea ? ability.AreaRadius : effectRadius;
        }

        public void ResetToProductionDefaults()
        {
            effectRadius = 8f;
            ringExpansionDuration = 0.38f;
            primaryRingDelay = 0.03f;
            secondaryRingDelay = 0.06f;
            overallIntensity = 1f;
            radialCloudDensity = 56;
            radialCloudSize = 2.75f;
            radialCloudLifetime = 0.72f;
            radialCloudDrift = 1.8f;
            radialCloudLift = 1.15f;
            iceBreakerDensity = 16;
            iceBreakerSize = 2.1f;
            iceBreakerLifetime = 0.58f;
            groundFrostDuration = 1.15f;
            groundRevealDelay = 0.1f;
            groundOffset = 0.035f;
            particleAmount = 1f;
            openingSnowAmount = 28;
            outwardShardAmount = 24;
            waveSnowAmount = 46;
            frostStreakAmount = 22;
            mistAmount = 26;
            lightIntensity = 2.2f;
            lightRadius = 5f;
            lightDuration = 0.22f;
            enemyImpactScale = 1.12f;
            impactDuration = 0.72f;
            rootIndicatorDuration = 3f;
            impactSnowAmount = 15;
            impactShardAmount = 8;
            impactMistAmount = 7;
            groundLayers = ~0;
            groundProbeHeight = 2.5f;
            groundProbeDistance = 8f;
            particleQuality = FrostWaveParticleQuality.High;
            targetReactionPoolSize = 16;
            distantReductionStart = 24f;
            cullDistance = 55f;
            optionalCameraImpulse = false;
            whiteHot = new Color(1f, 1.15f, 1.3f, 1f);
            paleCyan = new Color(0.48f, 1f, 1.25f, 1f);
            saturatedBlue = new Color(0.08f, 0.48f, 1f, 1f);
            deepBlue = new Color(0.015f, 0.12f, 0.55f, 1f);
            mistTint = new Color(0.42f, 0.78f, 1f, 0.68f);
        }

        private int ScaleCount(int count)
        {
            float quality = particleQuality == FrostWaveParticleQuality.Low
                ? 0.45f
                : particleQuality == FrostWaveParticleQuality.Medium ? 0.72f : 1f;
            return Mathf.RoundToInt(Mathf.Max(0, count) * particleAmount * quality);
        }
    }
}
