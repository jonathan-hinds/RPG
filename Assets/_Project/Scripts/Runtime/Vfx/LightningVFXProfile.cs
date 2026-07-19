using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    public enum LightningParticleQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Lightning VFX Profile", fileName = "LightningVFX_Default")]
    public sealed class LightningVFXProfile : ScriptableObject
    {
        [Header("Charge")]
        [SerializeField, Min(0.1f)] private float presentationChargeDuration = 0.85f;
        [SerializeField, Range(1, 12)] private int handArcCount = 5;
        [SerializeField, Min(0.005f)] private float handArcThickness = 0.075f;
        [SerializeField, Min(1f)] private float handArcFlickerSpeed = 28f;
        [SerializeField, Min(0.05f)] private float electricalCoreSize = 0.52f;
        [SerializeField, Min(0f)] private float electricalCoreBrightness = 3.4f;
        [SerializeField, Min(0f)] private float groundCrawlerFrequency = 9f;
        [SerializeField, Min(0.1f)] private float groundCrawlerRange = 2.7f;
        [SerializeField, Range(0f, 2f)] private float pressureFieldStrength = 0.8f;

        [Header("Dust - reuses Charge/Bash atlases")]
        [SerializeField, Min(0.1f)] private float dustRingRadius = 3.8f;
        [SerializeField, Min(0.1f)] private float dustRingExpansionSpeed = 6.5f;
        [SerializeField, Min(0f)] private float dustDensity = 24f;
        [SerializeField, Min(0.1f)] private float inwardDustSpawnRadius = 4.1f;
        [SerializeField, Min(0f)] private float inwardDustSpeed = 3.2f;
        [SerializeField, Range(-3f, 3f)] private float dustSpiralAmount = 0.75f;
        [SerializeField, Range(0, 64)] private int dirtFragmentCount = 12;
        [SerializeField, Min(0.01f)] private float dirtFragmentSize = 0.16f;
        [SerializeField, Range(0, 128)] private int releaseDustAmount = 44;

        [Header("Beam")]
        [SerializeField, Range(0.08f, 0.3f)] private float beamDuration = 0.2f;
        [SerializeField, Min(0.02f)] private float beamWidth = 0.72f;
        [SerializeField, Min(0.005f)] private float coreWidth = 0.14f;
        [SerializeField, Min(0.01f)] private float mainBodyWidth = 0.38f;
        [SerializeField, Min(0.01f)] private float outerGlowWidth = 0.92f;
        [SerializeField, Min(0f)] private float beamBrightness = 4.2f;
        [SerializeField, Range(5, 24)] private int beamPathComplexity = 11;
        [SerializeField, Range(1, 6)] private int largeBendCount = 3;
        [SerializeField, Min(1f)] private float pathRefreshRate = 24f;
        [SerializeField, Range(0, 12)] private int secondaryBoltCount = 4;
        [SerializeField, Range(0, 20)] private int branchCount = 8;
        [SerializeField, Min(0.05f)] private float branchLength = 1.25f;
        [SerializeField, Range(0, 256)] private int beamParticleAmount = 54;
        [SerializeField, Range(0f, 2f)] private float distortionStrength = 0.55f;

        [Header("Impact")]
        [SerializeField, Min(0.05f)] private float contactFlashSize = 1.25f;
        [SerializeField, Range(0, 16)] private int impactBoltCount = 8;
        [SerializeField, Range(0, 16)] private int targetBodyArcCount = 6;
        [SerializeField, Min(0.1f)] private float shockRingSize = 3.1f;
        [SerializeField, Range(0, 12)] private int groundStrikeCount = 5;
        [SerializeField, Range(0, 256)] private int impactSparkCount = 58;
        [SerializeField, Range(0, 128)] private int impactDustAmount = 22;
        [SerializeField, Range(0, 64)] private int smokeAmount = 7;
        [SerializeField, Range(0.15f, 1f)] private float aftermathDuration = 0.44f;

        [Header("Global")]
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Min(0f)] private float overallBrightness = 1f;
        [SerializeField] private Color whiteHotColor = new(1f, 0.98f, 0.82f, 1f);
        [SerializeField] private Color cyanColor = new(0.2f, 0.96f, 1f, 1f);
        [SerializeField] private Color electricBlueColor = new(0.08f, 0.42f, 1f, 0.92f);
        [SerializeField] private Color violetColor = new(0.48f, 0.18f, 1f, 0.62f);
        [SerializeField] private Color dustColor = new(0.62f, 0.48f, 0.31f, 0.72f);
        [SerializeField] private LightningParticleQuality particleQuality = LightningParticleQuality.High;

        public float PresentationChargeDuration => presentationChargeDuration;
        public int HandArcCount => handArcCount;
        public float HandArcThickness => handArcThickness;
        public float HandArcFlickerSpeed => handArcFlickerSpeed;
        public float ElectricalCoreSize => electricalCoreSize;
        public float ElectricalCoreBrightness => electricalCoreBrightness;
        public float GroundCrawlerFrequency => groundCrawlerFrequency;
        public float GroundCrawlerRange => groundCrawlerRange;
        public float PressureFieldStrength => pressureFieldStrength;
        public float DustRingRadius => dustRingRadius;
        public float DustRingExpansionSpeed => dustRingExpansionSpeed;
        public float DustDensity => dustDensity;
        public float InwardDustSpawnRadius => inwardDustSpawnRadius;
        public float InwardDustSpeed => inwardDustSpeed;
        public float DustSpiralAmount => dustSpiralAmount;
        public int DirtFragmentCount => dirtFragmentCount;
        public float DirtFragmentSize => dirtFragmentSize;
        public int ReleaseDustAmount => releaseDustAmount;
        public float BeamDuration => beamDuration;
        public float BeamWidth => beamWidth;
        public float CoreWidth => coreWidth;
        public float MainBodyWidth => mainBodyWidth;
        public float OuterGlowWidth => outerGlowWidth;
        public float BeamBrightness => beamBrightness;
        public int BeamPathComplexity => beamPathComplexity;
        public int LargeBendCount => largeBendCount;
        public float PathRefreshRate => pathRefreshRate;
        public int SecondaryBoltCount => secondaryBoltCount;
        public int BranchCount => branchCount;
        public float BranchLength => branchLength;
        public int BeamParticleAmount => beamParticleAmount;
        public float DistortionStrength => distortionStrength;
        public float ContactFlashSize => contactFlashSize;
        public int ImpactBoltCount => impactBoltCount;
        public int TargetBodyArcCount => targetBodyArcCount;
        public float ShockRingSize => shockRingSize;
        public int GroundStrikeCount => groundStrikeCount;
        public int ImpactSparkCount => impactSparkCount;
        public int ImpactDustAmount => impactDustAmount;
        public int SmokeAmount => smokeAmount;
        public float AftermathDuration => aftermathDuration;
        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public Color WhiteHotColor => whiteHotColor;
        public Color CyanColor => cyanColor;
        public Color ElectricBlueColor => electricBlueColor;
        public Color VioletColor => violetColor;
        public Color DustColor => dustColor;
        public LightningParticleQuality ParticleQuality => particleQuality;
        public float QualityMultiplier => particleQuality == LightningParticleQuality.Low ? 0.5f : particleQuality == LightningParticleQuality.Medium ? 0.75f : 1f;

        public void ResetToProductionDefaults()
        {
            presentationChargeDuration = 0.85f;
            handArcCount = 5;
            handArcThickness = 0.075f;
            handArcFlickerSpeed = 28f;
            electricalCoreSize = 0.52f;
            electricalCoreBrightness = 3.4f;
            groundCrawlerFrequency = 9f;
            groundCrawlerRange = 2.7f;
            pressureFieldStrength = 0.8f;
            dustRingRadius = 3.8f;
            dustRingExpansionSpeed = 6.5f;
            dustDensity = 24f;
            inwardDustSpawnRadius = 4.1f;
            inwardDustSpeed = 3.2f;
            dustSpiralAmount = 0.75f;
            dirtFragmentCount = 12;
            dirtFragmentSize = 0.16f;
            releaseDustAmount = 44;
            beamDuration = 0.2f;
            beamWidth = 0.72f;
            coreWidth = 0.14f;
            mainBodyWidth = 0.38f;
            outerGlowWidth = 0.92f;
            beamBrightness = 4.2f;
            beamPathComplexity = 11;
            largeBendCount = 3;
            pathRefreshRate = 24f;
            secondaryBoltCount = 4;
            branchCount = 8;
            branchLength = 1.25f;
            beamParticleAmount = 54;
            distortionStrength = 0.55f;
            contactFlashSize = 1.25f;
            impactBoltCount = 8;
            targetBodyArcCount = 6;
            shockRingSize = 3.1f;
            groundStrikeCount = 5;
            impactSparkCount = 58;
            impactDustAmount = 22;
            smokeAmount = 7;
            aftermathDuration = 0.44f;
            overallScale = 1f;
            overallBrightness = 1f;
            whiteHotColor = new Color(1f, 0.98f, 0.82f, 1f);
            cyanColor = new Color(0.2f, 0.96f, 1f, 1f);
            electricBlueColor = new Color(0.08f, 0.42f, 1f, 0.92f);
            violetColor = new Color(0.48f, 0.18f, 1f, 0.62f);
            dustColor = new Color(0.62f, 0.48f, 0.31f, 0.72f);
            particleQuality = LightningParticleQuality.High;
        }
    }
}
