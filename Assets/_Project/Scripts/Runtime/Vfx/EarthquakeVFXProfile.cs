using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    public enum EarthquakeParticleQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Earthquake Profile", fileName = "EarthquakeVFX_Default")]
    public sealed class EarthquakeVFXProfile : ScriptableObject
    {
        [Header("Area")]
        [SerializeField, Min(0.5f)] private float radius = 6f;
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Min(1f)] private float waveSpeed = 11.5f;
        [SerializeField, Range(0f, 0.5f)] private float ringIrregularity = 0.18f;
        [SerializeField] private float groundLayerOffset = 0.035f;

        [Header("Timing")]
        [SerializeField, Range(0.08f, 0.25f)] private float anticipationDuration = 0.14f;
        [SerializeField, Range(0.2f, 1.25f)] private float settleDuration = 0.82f;
        [SerializeField, Range(0.8f, 3f)] private float aftermathDuration = 1.45f;
        [SerializeField, Range(0f, 0.3f)] private float waveTimingVariation = 0.12f;

        [Header("Ground Shift")]
        [SerializeField, Range(8, 64)] private int groundSectionCount = 30;
        [SerializeField, Range(0.05f, 1.2f)] private float liftHeight = 0.48f;
        [SerializeField, Range(0f, 0.35f)] private float sinkDepth = 0.08f;
        [SerializeField, Range(0f, 35f)] private float tiltAmount = 17f;

        [Header("Ground Cubes")]
        [SerializeField] private Vector2 cubeSizeRange = new(0.45f, 1.35f);
        [SerializeField] private Vector2 cubeLiftHeightRange = new(0.18f, 0.58f);
        [SerializeField, Range(0f, 1f)] private float clusterDensity = 0.68f;
        [SerializeField, Range(0f, 1f)] private float terrainTintStrength = 0.78f;
        [SerializeField] private bool grassTopSupport = true;

        [Header("Cracks")]
        [SerializeField, Range(1, 16)] private int crackCount = 7;
        [SerializeField, Range(0.25f, 2f)] private float crackWidth = 0.82f;
        [SerializeField, Range(0f, 1f)] private float branchingAmount = 0.72f;
        [SerializeField, Range(0.1f, 2f)] private float crackExpansionSpeed = 0.7f;
        [SerializeField, Range(0f, 1f)] private float crackDarkness = 0.82f;
        [SerializeField, Range(0f, 0.3f)] private float warmHighlightStrength = 0.08f;
        [SerializeField, Range(0.2f, 2.5f)] private float crackFadeDuration = 1.25f;

        [Header("Dirt Ring")]
        [SerializeField, Range(0.1f, 2f)] private float dirtRingWidth = 0.72f;
        [SerializeField, Min(1f)] private float dirtHorizontalForce = 8.2f;
        [SerializeField, Range(0f, 1.2f)] private float dirtVerticalForceLimit = 0.52f;
        [SerializeField, Range(0, 160)] private int dirtClumpCount = 48;
        [SerializeField, Range(0, 200)] private int fineDirtAmount = 64;
        [SerializeField, Range(0f, 1f)] private float dirtArcBreakup = 0.38f;
        [SerializeField, Range(0.2f, 2.5f)] private float dirtLifetime = 1.15f;

        [Header("Dust Ring")]
        [SerializeField, Range(0.05f, 1f)] private float leadingEdgeWidth = 0.28f;
        [SerializeField, Range(0.2f, 2.5f)] private float mainRingWidth = 1.15f;
        [SerializeField, Range(0.02f, 1f)] private float dustGroundHeight = 0.18f;
        [SerializeField, Range(0, 256)] private int dustDensity = 92;
        [SerializeField, Range(0f, 1f)] private float dustOpacity = 0.64f;
        [SerializeField, Range(1, 16)] private int dustArcCount = 9;
        [SerializeField, Range(0, 160)] private int wakeAmount = 56;
        [SerializeField, Range(0.5f, 3f)] private float dustLifetime = 1.65f;

        [Header("Smoke Ring")]
        [SerializeField, Range(0.2f, 2.5f)] private float smokeRingWidth = 1.35f;
        [SerializeField, Min(0.1f)] private float smokeHorizontalSpeed = 5.4f;
        [SerializeField, Range(0f, 2f)] private float smokeCurlAmount = 0.42f;
        [SerializeField, Range(0.05f, 1.2f)] private float smokeMaximumHeight = 0.62f;
        [SerializeField, Range(0, 128)] private int smokeDensity = 42;
        [SerializeField, Range(0.2f, 3f)] private float smokeFadeDuration = 1.9f;

        [Header("Rocks And Debris")]
        [SerializeField, Range(0, 96)] private int rockCount = 24;
        [SerializeField, Min(0.1f)] private float rockHorizontalVelocity = 7.2f;
        [SerializeField, Range(0f, 2f)] private float rockMaximumVerticalVelocity = 0.9f;
        [SerializeField, Range(0f, 1f)] private float rockBounceAmount = 0.24f;
        [SerializeField, Range(0.2f, 2.5f)] private float rockLifetime = 1.35f;

        [Header("Enemy Impact")]
        [SerializeField, Range(0.2f, 2f)] private float localCrackScale = 0.9f;
        [SerializeField, Range(0.2f, 2.5f)] private float localDustRingSize = 1.35f;
        [SerializeField, Range(0, 24)] private int localRockCount = 7;
        [SerializeField, Range(0f, 3f)] private float impactPulseStrength = 1.2f;

        [Header("Global")]
        [SerializeField, Min(0f)] private float overallBrightness = 1f;
        [SerializeField] private Color dirtColor = new(0.5f, 0.31f, 0.16f, 0.95f);
        [SerializeField] private Color dustColor = new(0.76f, 0.58f, 0.36f, 0.64f);
        [SerializeField] private Color smokeColor = new(0.27f, 0.24f, 0.22f, 0.48f);
        [SerializeField] private Color stoneColor = new(0.46f, 0.45f, 0.42f, 0.96f);
        [SerializeField] private Color impactColor = new(1f, 0.82f, 0.43f, 0.78f);
        [SerializeField, Range(0f, 1f)] private float distortionStrength = 0.16f;
        [SerializeField] private EarthquakeParticleQuality particleQuality = EarthquakeParticleQuality.High;

        [Header("Performance LOD")]
        [SerializeField, Min(1f)] private float distantReductionStart = 24f;
        [SerializeField, Min(2f)] private float cullDistance = 58f;
        [SerializeField, Range(0.15f, 1f)] private float minimumDistantDensity = 0.32f;

        public float Radius => radius * overallScale;
        public float OverallScale => overallScale;
        public float WaveSpeed => waveSpeed * overallScale;
        public float RingIrregularity => ringIrregularity;
        public float GroundLayerOffset => groundLayerOffset;
        public float AnticipationDuration => anticipationDuration;
        public float SettleDuration => settleDuration;
        public float AftermathDuration => aftermathDuration;
        public float WaveTimingVariation => waveTimingVariation;
        public int GroundSectionCount => ScaleCount(groundSectionCount);
        public float LiftHeight => liftHeight * overallScale;
        public float SinkDepth => sinkDepth * overallScale;
        public float TiltAmount => tiltAmount;
        public Vector2 CubeSizeRange => cubeSizeRange * overallScale;
        public Vector2 CubeLiftHeightRange => cubeLiftHeightRange * overallScale;
        public float ClusterDensity => clusterDensity;
        public float TerrainTintStrength => terrainTintStrength;
        public bool GrassTopSupport => grassTopSupport;
        public int CrackCount => ScaleCount(crackCount);
        public float CrackWidth => crackWidth * overallScale;
        public float BranchingAmount => branchingAmount;
        public float CrackExpansionSpeed => crackExpansionSpeed;
        public float CrackDarkness => crackDarkness;
        public float WarmHighlightStrength => warmHighlightStrength;
        public float CrackFadeDuration => crackFadeDuration;
        public float DirtRingWidth => dirtRingWidth * overallScale;
        public float DirtHorizontalForce => dirtHorizontalForce * overallScale;
        public float DirtVerticalForceLimit => dirtVerticalForceLimit * overallScale;
        public int DirtClumpCount => ScaleCount(dirtClumpCount);
        public int FineDirtAmount => ScaleCount(fineDirtAmount);
        public float DirtArcBreakup => dirtArcBreakup;
        public float DirtLifetime => dirtLifetime;
        public float LeadingEdgeWidth => leadingEdgeWidth * overallScale;
        public float MainRingWidth => mainRingWidth * overallScale;
        public float DustGroundHeight => dustGroundHeight * overallScale;
        public int DustDensity => ScaleCount(dustDensity);
        public float DustOpacity => dustOpacity;
        public int DustArcCount => ScaleCount(dustArcCount);
        public int WakeAmount => ScaleCount(wakeAmount);
        public float DustLifetime => dustLifetime;
        public float SmokeRingWidth => smokeRingWidth * overallScale;
        public float SmokeHorizontalSpeed => smokeHorizontalSpeed * overallScale;
        public float SmokeCurlAmount => smokeCurlAmount;
        public float SmokeMaximumHeight => smokeMaximumHeight * overallScale;
        public int SmokeDensity => ScaleCount(smokeDensity);
        public float SmokeFadeDuration => smokeFadeDuration;
        public int RockCount => ScaleCount(rockCount);
        public float RockHorizontalVelocity => rockHorizontalVelocity * overallScale;
        public float RockMaximumVerticalVelocity => rockMaximumVerticalVelocity * overallScale;
        public float RockBounceAmount => rockBounceAmount;
        public float RockLifetime => rockLifetime;
        public float LocalCrackScale => localCrackScale * overallScale;
        public float LocalDustRingSize => localDustRingSize * overallScale;
        public int LocalRockCount => ScaleCount(localRockCount);
        public float ImpactPulseStrength => impactPulseStrength;
        public float OverallBrightness => overallBrightness;
        public Color DirtColor => dirtColor;
        public Color DustColor => dustColor;
        public Color SmokeColor => smokeColor;
        public Color StoneColor => stoneColor;
        public Color ImpactColor => impactColor;
        public float DistortionStrength => distortionStrength;
        public EarthquakeParticleQuality ParticleQuality => particleQuality;
        public float DistantReductionStart => distantReductionStart;
        public float CullDistance => Mathf.Max(distantReductionStart + 1f, cullDistance);
        public float MinimumDistantDensity => minimumDistantDensity;
        public float WaveDuration => Radius / Mathf.Max(0.1f, WaveSpeed);
        public float TotalLifetime => anticipationDuration + WaveDuration + settleDuration + aftermathDuration;

        public void ResetToProductionDefaults()
        {
            radius = 6f;
            overallScale = 1f;
            waveSpeed = 11.5f;
            anticipationDuration = 0.14f;
            settleDuration = 0.82f;
            aftermathDuration = 1.45f;
            particleQuality = EarthquakeParticleQuality.High;
        }

        private int ScaleCount(int value)
        {
            float multiplier = particleQuality == EarthquakeParticleQuality.Low ? 0.45f
                : particleQuality == EarthquakeParticleQuality.Medium ? 0.72f : 1f;
            return value <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
        }
    }
}
