using UnityEngine;

namespace RPGClone.Vfx.Water
{
    public enum WaterShieldParticleQuality
    {
        Low,
        Medium,
        High
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Water Shield Profile", fileName = "WaterShieldVFXProfile")]
    public sealed class WaterShieldVFXProfile : ScriptableObject
    {
        [System.Serializable]
        public sealed class WaterPalette
        {
            [ColorUsage(true, true)] public Color WhiteHighlight = new(1.8f, 2f, 2.1f, 1f);
            [ColorUsage(true, true)] public Color PaleCyan = new(0.72f, 1.55f, 1.8f, 1f);
            [ColorUsage(true, true)] public Color Aqua = new(0.12f, 1.15f, 1.65f, 1f);
            [ColorUsage(true, true)] public Color ClearBlue = new(0.08f, 0.62f, 1.45f, 1f);
            [ColorUsage(true, true)] public Color DeepBlue = new(0.025f, 0.16f, 0.72f, 1f);
            [ColorUsage(true, true)] public Color Teal = new(0.04f, 0.72f, 0.8f, 1f);
            [ColorUsage(true, true)] public Color ManaViolet = new(0.42f, 0.34f, 1.45f, 1f);
            public Color Mist = new(0.55f, 0.78f, 0.9f, 0.32f);
        }

        [Header("Global")]
        [SerializeField, HideInInspector] private int authoringVersion;
        [SerializeField, Min(0.1f)] private float effectScale = 1f;
        [SerializeField, Range(0f, 4f)] private float overallBrightness = 1.15f;
        [SerializeField] private WaterPalette colors = new();
        [SerializeField] private WaterShieldParticleQuality particleQuality = WaterShieldParticleQuality.High;
        [SerializeField, Min(0.1f)] private float expirationDuration = 0.8f;

        [Header("Activation")]
        [SerializeField, Range(0.4f, 1.2f)] private float activationDuration = 0.62f;
        [SerializeField, Range(0f, 0.4f)] private float firstOrbDelay;
        [SerializeField, Range(0.03f, 0.4f)] private float orbFormationInterval = 0.055f;
        [SerializeField, Range(0.15f, 0.9f)] private float gatherDuration = 0.24f;
        [SerializeField, Range(6, 64)] private int dropletsPerOrb = 28;
        [SerializeField, Min(0.2f)] private float collectionInnerRadius = 1.15f;
        [SerializeField, Min(0.3f)] private float collectionOuterRadius = 2.75f;
        [SerializeField, Min(0f)] private float collectionVerticalRange = 1.45f;
        [SerializeField, Min(0f)] private float inwardSpiralStrength = 0.46f;
        [SerializeField, Range(0f, 4f)] private float activationFlashBrightness = 2.65f;
        [SerializeField, Min(0.1f)] private float activationRingSize = 2.35f;
        [SerializeField, Min(0f)] private float activationSweepDegrees = 210f;
        [SerializeField, Range(0f, 0.5f)] private float formationPopScale = 0.28f;
        [SerializeField, Range(0f, 4f)] private float formationPopBrightness = 1.4f;
        [SerializeField, Range(0, 32)] private int formationSplashAmount = 14;

        [Header("Orbit")]
        [SerializeField, Min(0.1f)] private float orbitRadius = 0.82f;
        [SerializeField] private float orbitHeight = 1.05f;
        [SerializeField, Range(-35f, 35f)] private float orbitTilt = 9f;
        [SerializeField] private float orbitSpeed = 58f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private float formationRotationOffset = 28f;
        [SerializeField, Range(0f, 0.3f)] private float verticalBobAmount = 0.08f;
        [SerializeField, Min(0f)] private float verticalBobSpeed = 1.8f;

        [Header("Orbs")]
        [SerializeField, Min(0.05f)] private float orbScale = 0.28f;
        [SerializeField, Range(0.1f, 1f)] private float innerCoreScale = 0.58f;
        [SerializeField, Range(0f, 5f)] private float innerCoreBrightness = 1.8f;
        [SerializeField, Range(0f, 1f)] private float mainWaterOpacity = 0.72f;
        [SerializeField, Range(0f, 1f)] private float outerShellOpacity = 0.42f;
        [SerializeField, Range(0f, 0.2f)] private float waterDistortionStrength = 0.035f;
        [SerializeField, Range(0f, 0.25f)] private float surfaceWobbleAmount = 0.07f;
        [SerializeField, Min(0f)] private float surfaceWobbleSpeed = 2.4f;
        [SerializeField, Range(0f, 5f)] private float highlightBrightness = 1.65f;
        [SerializeField, Range(0f, 2f)] private float deepWaterIntensity = 0.72f;
        [SerializeField, Range(0f, 3f)] private float manaEnergyIntensity = 0.62f;

        [Header("Material Animation")]
        [SerializeField] private Vector2 mainTextureScrollSpeed = new(0.11f, 0.06f);
        [SerializeField] private Vector2 secondaryTextureScrollSpeed = new(-0.07f, 0.13f);
        [SerializeField] private Vector2 surfaceHighlightSpeed = new(0.16f, -0.08f);
        [SerializeField] private Vector2 distortionScrollSpeed = new(0.09f, 0.12f);
        [SerializeField] private float internalRotationSpeed = 24f;
        [SerializeField] private float outerShellRotationSpeed = -31f;

        [Header("Trails")]
        [SerializeField, Range(0.1f, 1.5f)] private float trailLength = 0.48f;
        [SerializeField, Min(0.01f)] private float trailWidth = 0.22f;
        [SerializeField, Range(0f, 1f)] private float trailOpacity = 0.72f;
        [SerializeField] private float trailTextureSpeed = 0.85f;
        [SerializeField, Min(0.005f)] private float highlightTrailWidth = 0.07f;
        [SerializeField, Range(0f, 32f)] private float mistAmount = 7f;
        [SerializeField, Range(0f, 48f)] private float trailDropletFrequency = 10f;

        [Header("Particles")]
        [SerializeField, Range(0f, 48f)] private float dropletSpawnRate = 8f;
        [SerializeField, Min(0.005f)] private float dropletSize = 0.065f;
        [SerializeField, Min(0f)] private float dropletSpeed = 0.28f;
        [SerializeField, Range(0f, 12f)] private float splashFrequency = 1.2f;
        [SerializeField, Min(0.01f)] private float splashSize = 0.16f;
        [SerializeField, Range(0f, 32f)] private float fineSprayAmount = 7f;
        [SerializeField, Range(0, 24)] private int waterMoteCount = 7;
        [SerializeField, Range(0f, 4f)] private float waterMoteBrightness = 1.2f;

        [Header("Reactions")]
        [SerializeField, Range(0f, 6f)] private float absorbFlashIntensity = 2.3f;
        [SerializeField, Min(0.1f)] private float absorbReactionScale = 1.15f;
        [SerializeField, Range(0f, 0.6f)] private float orbCompressionAmount = 0.26f;
        [SerializeField, Range(0f, 2f)] private float rippleStrength = 1f;
        [SerializeField, Range(0, 32)] private int reactiveSplashAmount = 12;
        [SerializeField, Range(0f, 1f)] private float orbitDisturbanceAmount = 0.22f;
        [SerializeField, Range(0f, 6f)] private float manaTransferBrightness = 2f;
        [SerializeField, Min(0.1f)] private float manaTransferSpeed = 3.2f;
        [SerializeField, Range(0f, 6f)] private float chestPulseIntensity = 1.6f;
        [SerializeField, Range(1f, 12f)] private float persistentManaPulseInterval = 5f;

        public float EffectScale => effectScale;
        public float OverallBrightness => overallBrightness;
        public WaterPalette Colors => colors;
        public WaterShieldParticleQuality ParticleQuality => particleQuality;
        public float ExpirationDuration => expirationDuration;
        public float ActivationDuration => activationDuration;
        public float FirstOrbDelay => firstOrbDelay;
        public float OrbFormationInterval => orbFormationInterval;
        public float GatherDuration => gatherDuration;
        public int DropletsPerOrb => ScaledCount(dropletsPerOrb);
        public float CollectionInnerRadius => collectionInnerRadius;
        public float CollectionOuterRadius => Mathf.Max(collectionInnerRadius, collectionOuterRadius);
        public float CollectionVerticalRange => collectionVerticalRange;
        public float InwardSpiralStrength => inwardSpiralStrength;
        public float ActivationFlashBrightness => activationFlashBrightness;
        public float ActivationRingSize => activationRingSize;
        public float ActivationSweepDegrees => activationSweepDegrees;
        public float FormationPopScale => formationPopScale;
        public float FormationPopBrightness => formationPopBrightness;
        public int FormationSplashAmount => ScaledCount(formationSplashAmount);
        public float OrbitRadius => orbitRadius;
        public float OrbitHeight => orbitHeight;
        public float OrbitTilt => orbitTilt;
        public float OrbitSpeed => (clockwise ? 1f : -1f) * orbitSpeed;
        public float FormationRotationOffset => formationRotationOffset;
        public float VerticalBobAmount => verticalBobAmount;
        public float VerticalBobSpeed => verticalBobSpeed;
        public float OrbScale => orbScale;
        public float InnerCoreScale => innerCoreScale;
        public float InnerCoreBrightness => innerCoreBrightness;
        public float MainWaterOpacity => mainWaterOpacity;
        public float OuterShellOpacity => outerShellOpacity;
        public float WaterDistortionStrength => waterDistortionStrength;
        public float SurfaceWobbleAmount => surfaceWobbleAmount;
        public float SurfaceWobbleSpeed => surfaceWobbleSpeed;
        public float HighlightBrightness => highlightBrightness;
        public float DeepWaterIntensity => deepWaterIntensity;
        public float ManaEnergyIntensity => manaEnergyIntensity;
        public Vector2 MainTextureScrollSpeed => mainTextureScrollSpeed;
        public Vector2 SecondaryTextureScrollSpeed => secondaryTextureScrollSpeed;
        public Vector2 SurfaceHighlightSpeed => surfaceHighlightSpeed;
        public Vector2 DistortionScrollSpeed => distortionScrollSpeed;
        public float InternalRotationSpeed => internalRotationSpeed;
        public float OuterShellRotationSpeed => outerShellRotationSpeed;
        public float TrailLength => trailLength;
        public float TrailWidth => trailWidth;
        public float TrailOpacity => trailOpacity;
        public float TrailTextureSpeed => trailTextureSpeed;
        public float HighlightTrailWidth => highlightTrailWidth;
        public float MistAmount => mistAmount * QualityMultiplier;
        public float TrailDropletFrequency => trailDropletFrequency * QualityMultiplier;
        public float DropletSpawnRate => dropletSpawnRate * QualityMultiplier;
        public float DropletSize => dropletSize;
        public float DropletSpeed => dropletSpeed;
        public float SplashFrequency => splashFrequency * QualityMultiplier;
        public float SplashSize => splashSize;
        public float FineSprayAmount => fineSprayAmount * QualityMultiplier;
        public int WaterMoteCount => ScaledCount(waterMoteCount);
        public float WaterMoteBrightness => waterMoteBrightness;
        public float AbsorbFlashIntensity => absorbFlashIntensity;
        public float AbsorbReactionScale => absorbReactionScale;
        public float OrbCompressionAmount => orbCompressionAmount;
        public float RippleStrength => rippleStrength;
        public int ReactiveSplashAmount => ScaledCount(reactiveSplashAmount);
        public float OrbitDisturbanceAmount => orbitDisturbanceAmount;
        public float ManaTransferBrightness => manaTransferBrightness;
        public float ManaTransferSpeed => manaTransferSpeed;
        public float ChestPulseIntensity => chestPulseIntensity;
        public float PersistentManaPulseInterval => persistentManaPulseInterval;

        private float QualityMultiplier => particleQuality switch
        {
            WaterShieldParticleQuality.Low => 0.45f,
            WaterShieldParticleQuality.Medium => 0.72f,
            _ => 1f
        };

        private int ScaledCount(int value)
        {
            return value <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(value * QualityMultiplier));
        }
    }
}
