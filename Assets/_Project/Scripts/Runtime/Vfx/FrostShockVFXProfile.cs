using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    public enum FrostShockParticleQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Frost Shock VFX Profile", fileName = "FrostShockVFX_Default")]
    public sealed class FrostShockVFXProfile : ScriptableObject
    {
        [Header("Cast")]
        [SerializeField, Min(0.05f)] private float castDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float castHandEffectScale = 0.42f;
        [SerializeField, Min(0f)] private float handFlashBrightness = 3.8f;
        [SerializeField, Range(1, 4)] private int wristRibbonCount = 2;
        [SerializeField, Min(0.02f)] private float frostCoreSize = 0.22f;
        [SerializeField, Min(0f)] private float frostCoreBrightness = 4.4f;
        [SerializeField, Min(0.05f)] private float releaseBurstSize = 0.72f;

        [Header("Projectile")]
        [SerializeField, Min(1f)] private float projectileSpeed = 72f;
        [SerializeField, Min(0.1f)] private float projectileLength = 1.7f;
        [SerializeField, Min(0.01f)] private float coreWidth = 0.09f;
        [SerializeField, Min(0.01f)] private float mainBodyWidth = 0.24f;
        [SerializeField, Min(0.01f)] private float outerGlowWidth = 0.4f;
        [SerializeField, Min(0f)] private float projectileBrightness = 4.2f;
        [SerializeField, Range(0, 48)] private int iceFragmentCount = 14;
        [SerializeField, Min(0.05f)] private float vaporTrailLength = 1.1f;
        [SerializeField, Min(0.01f)] private float vaporTrailWidth = 0.32f;
        [SerializeField, Range(0, 64)] private int snowTrailAmount = 14;
        [SerializeField, Min(0.05f)] private float trailFadeDuration = 0.55f;

        [Header("Impact")]
        [SerializeField, Min(0.05f)] private float contactFlashScale = 1.05f;
        [SerializeField, Min(0f)] private float contactFlashBrightness = 5f;
        [SerializeField, Range(4, 24)] private int mainShardCount = 10;
        [SerializeField, Min(0.05f)] private float mainShardSize = 0.72f;
        [SerializeField, Range(0, 64)] private int secondaryFragmentCount = 26;
        [SerializeField, Min(0.1f)] private float frostExplosionScale = 1.65f;
        [SerializeField, Range(0, 16)] private int frostCrackDensity = 6;
        [SerializeField, Min(0.1f)] private float shockRingSize = 2.35f;
        [SerializeField, Min(0.1f)] private float groundFrostRadius = 1.35f;
        [SerializeField, Range(0, 16)] private int groundSpikeCount = 7;
        [SerializeField, Range(0, 96)] private int impactMistAmount = 18;
        [SerializeField, Range(0, 128)] private int snowBurstAmount = 42;

        [Header("Slow Debuff")]
        [SerializeField, Min(0.1f)] private float debuffDuration = 6f;
        [SerializeField, Min(0.1f)] private float footIceScale = 0.78f;
        [SerializeField, Min(0f)] private float footIceBrightness = 1.65f;
        [SerializeField, Range(0f, 1f)] private float lowerLegFrostCoverage = 0.68f;
        [SerializeField, Range(0f, 1f)] private float bodyFrostCoverage = 0.28f;
        [SerializeField, Range(0, 3)] private int energyBandCount = 2;
        [SerializeField] private float energyBandSpeed = 36f;
        [SerializeField, Range(0, 32)] private int mistEmissionRate = 7;
        [SerializeField, Range(0, 48)] private int snowEmissionRate = 10;
        [SerializeField, Min(0.05f)] private float crackFlickerFrequency = 0.72f;
        [SerializeField, Range(0, 24)] private int movementTrailDensity = 7;
        [SerializeField, Min(0.05f)] private float movementTrailLifetime = 0.85f;
        [SerializeField, Range(0f, 1f)] private float movementPulseIntensity = 0.36f;

        [Header("Expiration")]
        [SerializeField, Range(0.1f, 1.5f)] private float fractureDuration = 0.18f;
        [SerializeField, Range(0, 48)] private int shatterFragmentCount = 18;
        [SerializeField, Min(0f)] private float shatterVelocity = 2.4f;
        [SerializeField, Range(0, 64)] private int finalMistAmount = 14;
        [SerializeField, Range(0, 96)] private int finalSnowAmount = 22;
        [SerializeField, Range(0.1f, 1.5f)] private float frostDissolveDuration = 0.55f;

        [Header("Global")]
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Min(0f)] private float overallBrightness = 1f;
        [SerializeField] private Color whiteHotColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color paleCyanColor = new(0.58f, 0.96f, 1f, 1f);
        [SerializeField] private Color saturatedBlueColor = new(0.08f, 0.48f, 1f, 1f);
        [SerializeField] private Color deepBlueColor = new(0.025f, 0.12f, 0.48f, 0.95f);
        [SerializeField] private Color violetBlueAccent = new(0.32f, 0.22f, 0.78f, 0.7f);
        [SerializeField] private Color mistColor = new(0.72f, 0.9f, 1f, 0.48f);
        [SerializeField, Range(0f, 0.2f)] private float distortionStrength = 0.035f;
        [SerializeField] private FrostShockParticleQuality particleQuality = FrostShockParticleQuality.High;

        public float CastDuration => castDuration;
        public float CastHandEffectScale => castHandEffectScale;
        public float HandFlashBrightness => handFlashBrightness;
        public int WristRibbonCount => wristRibbonCount;
        public float FrostCoreSize => frostCoreSize;
        public float FrostCoreBrightness => frostCoreBrightness;
        public float ReleaseBurstSize => releaseBurstSize;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLength => projectileLength;
        public float CoreWidth => coreWidth;
        public float MainBodyWidth => mainBodyWidth;
        public float OuterGlowWidth => outerGlowWidth;
        public float ProjectileBrightness => projectileBrightness;
        public int IceFragmentCount => iceFragmentCount;
        public float VaporTrailLength => vaporTrailLength;
        public float VaporTrailWidth => vaporTrailWidth;
        public int SnowTrailAmount => snowTrailAmount;
        public float TrailFadeDuration => trailFadeDuration;
        public float ContactFlashScale => contactFlashScale;
        public float ContactFlashBrightness => contactFlashBrightness;
        public int MainShardCount => mainShardCount;
        public float MainShardSize => mainShardSize;
        public int SecondaryFragmentCount => secondaryFragmentCount;
        public float FrostExplosionScale => frostExplosionScale;
        public int FrostCrackDensity => frostCrackDensity;
        public float ShockRingSize => shockRingSize;
        public float GroundFrostRadius => groundFrostRadius;
        public int GroundSpikeCount => groundSpikeCount;
        public int ImpactMistAmount => impactMistAmount;
        public int SnowBurstAmount => snowBurstAmount;
        public float DebuffDuration => debuffDuration;
        public float FootIceScale => footIceScale;
        public float FootIceBrightness => footIceBrightness;
        public float LowerLegFrostCoverage => lowerLegFrostCoverage;
        public float BodyFrostCoverage => bodyFrostCoverage;
        public int EnergyBandCount => energyBandCount;
        public float EnergyBandSpeed => energyBandSpeed;
        public int MistEmissionRate => mistEmissionRate;
        public int SnowEmissionRate => snowEmissionRate;
        public float CrackFlickerFrequency => crackFlickerFrequency;
        public int MovementTrailDensity => movementTrailDensity;
        public float MovementTrailLifetime => movementTrailLifetime;
        public float MovementPulseIntensity => movementPulseIntensity;
        public float FractureDuration => fractureDuration;
        public int ShatterFragmentCount => shatterFragmentCount;
        public float ShatterVelocity => shatterVelocity;
        public int FinalMistAmount => finalMistAmount;
        public int FinalSnowAmount => finalSnowAmount;
        public float FrostDissolveDuration => frostDissolveDuration;
        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public Color WhiteHotColor => whiteHotColor;
        public Color PaleCyanColor => paleCyanColor;
        public Color SaturatedBlueColor => saturatedBlueColor;
        public Color DeepBlueColor => deepBlueColor;
        public Color VioletBlueAccent => violetBlueAccent;
        public Color MistColor => mistColor;
        public float DistortionStrength => distortionStrength;
        public FrostShockParticleQuality ParticleQuality => particleQuality;
        public float QualityMultiplier => particleQuality == FrostShockParticleQuality.Low ? 0.45f : particleQuality == FrostShockParticleQuality.Medium ? 0.72f : 1f;
    }
}
