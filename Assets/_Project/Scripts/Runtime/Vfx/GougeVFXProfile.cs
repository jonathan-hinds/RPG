using UnityEngine;

namespace RPGClone.Vfx.Physical
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Gouge Profile", fileName = "GougeVFXProfile")]
    public sealed class GougeVFXProfile : ScriptableObject
    {
        private const int LatestProfileVersion = 4;

        [System.Serializable]
        public sealed class GougePalette
        {
            [ColorUsage(true, true)] public Color ImpactWhite = new(1.8f, 1.65f, 1.25f, 1f);
            [ColorUsage(true, true)] public Color ImpactYellow = new(1.45f, 0.92f, 0.28f, 1f);
            [ColorUsage(true, true)] public Color Crimson = new(1.25f, 0.055f, 0.025f, 1f);
            [ColorUsage(true, true)] public Color DeepRed = new(0.62f, 0.018f, 0.012f, 1f);
            public Color Maroon = new(0.25f, 0.008f, 0.01f, 0.95f);
            public Color BrownRed = new(0.3f, 0.065f, 0.035f, 0.9f);
            public Color Charcoal = new(0.11f, 0.085f, 0.075f, 0.8f);
            [ColorUsage(true, true)] public Color Metallic = new(0.92f, 0.88f, 0.72f, 1f);
        }

        public enum ParticleQuality
        {
            Low,
            Medium,
            High
        }

        [Header("Global")]
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Range(0f, 3f)] private float overallBrightness = 1f;
        [SerializeField, Range(0f, 1f)] private float goreIntensity = 0.72f;
        [SerializeField] private ParticleQuality particleQuality = ParticleQuality.High;
        [SerializeField] private GougePalette palette = new();
        [SerializeField, HideInInspector] private int profileVersion;

        [Header("Attack Motion")]
        [SerializeField, Range(0.05f, 0.12f)] private float anticipationDuration = 0.08f;
        [SerializeField, Range(0.12f, 0.35f)] private float attackMotionDuration = 0.24f;
        [SerializeField, Min(0.05f)] private float mainTrailWidth = 0.72f;
        [SerializeField, Min(0.1f)] private float mainTrailLength = 2.15f;
        [SerializeField, Range(0f, 3f)] private float mainTrailBrightness = 1.25f;
        [SerializeField, Min(0.05f)] private float tearingTrailWidth = 0.42f;
        [SerializeField] private Color tearingTrailColor = new(0.72f, 0.015f, 0.01f, 0.92f);
        [SerializeField, Range(0, 4)] private int weaponGlintCount = 2;
        [SerializeField, Range(0, 18)] private int motionFragmentAmount = 7;

        [Header("Initial Impact")]
        [SerializeField, Min(0.05f)] private float impactDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float contactFlashSize = 0.82f;
        [SerializeField, Range(0f, 3f)] private float contactFlashBrightness = 1.55f;
        [SerializeField, Min(0.05f)] private float woundSize = 0.86f;
        [SerializeField, Range(-180f, 180f)] private float woundOrientation = -22f;
        [SerializeField, Range(0, 64)] private int mainBloodSprayAmount = 13;
        [SerializeField, Min(0.05f)] private float sprayLength = 1.35f;
        [SerializeField, Range(0f, 90f)] private float sprayAngle = 26f;
        [SerializeField, Range(0, 48)] private int closeBurstAmount = 9;
        [SerializeField, Range(0, 20)] private int tornFragmentCount = 7;
        [SerializeField, Range(0, 24)] private int dustBurstAmount = 11;
        [SerializeField, Min(0.1f)] private float dustBurstSize = 0.56f;
        [SerializeField, Range(0, 96)] private int environmentalHeavyDustAmount = 36;
        [SerializeField, Min(0.1f)] private float environmentalHeavyDustSize = 1.35f;
        [SerializeField, Min(0.1f)] private float environmentalHeavyDustLifetime = 2.4f;
        [SerializeField, Range(0, 96)] private int environmentalFineDustAmount = 28;
        [SerializeField, Min(0.1f)] private float environmentalFineDustSize = 0.82f;
        [SerializeField, Min(0.1f)] private float environmentalFineDustLifetime = 2.8f;
        [SerializeField, Min(0.1f)] private float dustRingSize = 1.65f;
        [SerializeField, Range(0, 16)] private int groundDebrisCount = 6;
        [SerializeField, Min(0.05f)] private float groundDebrisSize = 0.14f;

        [Header("Persistent Wound")]
        [SerializeField, Min(0.1f)] private float bleedDuration = 9f;
        [SerializeField, Range(0f, 1f)] private float woundBaseOpacity = 0.9f;
        [SerializeField, Range(0f, 3f)] private float innerCutBrightness = 1.2f;
        [SerializeField, Range(0f, 1f)] private float wetHighlightAmount = 0.58f;
        [SerializeField, Min(0f)] private float seepageSpeed = 0.19f;
        [SerializeField, Range(0.05f, 3f)] private float dripFrequency = 0.58f;
        [SerializeField, Min(0.01f)] private float dripSize = 0.12f;
        [SerializeField, Range(0f, 1f)] private float woundMistIntensity = 0.18f;
        [SerializeField, Range(0.05f, 0.75f)] private float woundCameraOffset = 0.36f;
        [SerializeField, Range(0f, 1f)] private float woundPulseStrength = 0.18f;

        [Header("Bleed Ticks")]
        [SerializeField, Range(0.2f, 0.5f)] private float tickDuration = 0.36f;
        [SerializeField, Range(0f, 3f)] private float tickFlashBrightness = 1.8f;
        [SerializeField, Min(0.1f)] private float tickPulseSize = 1.05f;
        [SerializeField, Range(0, 24)] private int tickSprayAmount = 7;
        [SerializeField, Range(0, 16)] private int tickDropletCount = 5;
        [SerializeField, Range(0f, 1f)] private float tickBodyAccentStrength = 0.28f;
        [SerializeField, Range(1f, 2f)] private float finalTickMultiplier = 1.22f;

        [Header("Stack Scaling")]
        [SerializeField] private Vector3 woundScaleByStack = new(1f, 1.14f, 1.28f);
        [SerializeField] private Vector3 brightnessByStack = new(1f, 1.12f, 1.24f);
        [SerializeField] private Vector3 seepageByStack = new(1f, 1.35f, 1.7f);
        [SerializeField] private Vector3 dropletByStack = new(1f, 1.35f, 1.7f);
        [SerializeField] private Vector3 tickIntensityByStack = new(1f, 1.2f, 1.42f);
        [SerializeField] private Vector3 mistByStack = new(1f, 1.3f, 1.62f);

        [Header("Critical Hit")]
        [SerializeField, Range(1f, 3f)] private float criticalFlashMultiplier = 1.65f;
        [SerializeField, Range(1, 3)] private int criticalTrailCount = 2;
        [SerializeField, Range(1f, 3f)] private float criticalBloodMultiplier = 1.55f;
        [SerializeField, Range(0, 24)] private int criticalSparkAmount = 11;
        [SerializeField, Min(0.1f)] private float resetRingSize = 0.78f;
        [SerializeField, Range(0f, 3f)] private float resetRingBrightness = 1.55f;
        [SerializeField, Range(0.08f, 0.35f)] private float resetRingDuration = 0.22f;
        [SerializeField] private bool enableLocalPlayerScreenAccent;

        [Header("Expiration")]
        [SerializeField, Range(0.1f, 0.5f)] private float woundDimmingDuration = 0.24f;
        [SerializeField, Range(0, 4)] private int finalDropletAmount = 2;
        [SerializeField, Range(0.2f, 0.8f)] private float woundDissolveDuration = 0.48f;
        [SerializeField, Range(0.2f, 0.8f)] private float mistFadeDuration = 0.52f;

        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public float GoreIntensity => goreIntensity;
        public ParticleQuality Quality => particleQuality;
        public GougePalette Colors => palette;
        public float AnticipationDuration => anticipationDuration;
        public float AttackMotionDuration => attackMotionDuration;
        public float MainTrailWidth => mainTrailWidth;
        public float MainTrailLength => mainTrailLength;
        public float MainTrailBrightness => mainTrailBrightness;
        public float TearingTrailWidth => tearingTrailWidth;
        public Color TearingTrailColor => tearingTrailColor;
        public int WeaponGlintCount => weaponGlintCount;
        public int MotionFragmentAmount => ScaleCount(motionFragmentAmount);
        public float ImpactDuration => impactDuration;
        public float ContactFlashSize => contactFlashSize;
        public float ContactFlashBrightness => contactFlashBrightness;
        public float WoundSize => woundSize;
        public float WoundOrientation => woundOrientation;
        public int MainBloodSprayAmount => ScaleCount(mainBloodSprayAmount);
        public float SprayLength => sprayLength;
        public float SprayAngle => sprayAngle;
        public int CloseBurstAmount => ScaleCount(closeBurstAmount);
        public int TornFragmentCount => ScaleCount(tornFragmentCount);
        public int DustBurstAmount => ScaleCount(dustBurstAmount);
        public float DustBurstSize => dustBurstSize;
        public int EnvironmentalHeavyDustAmount => ScaleCount(environmentalHeavyDustAmount);
        public float EnvironmentalHeavyDustSize => environmentalHeavyDustSize;
        public float EnvironmentalHeavyDustLifetime => environmentalHeavyDustLifetime;
        public int EnvironmentalFineDustAmount => ScaleCount(environmentalFineDustAmount);
        public float EnvironmentalFineDustSize => environmentalFineDustSize;
        public float EnvironmentalFineDustLifetime => environmentalFineDustLifetime;
        public float DustRingSize => dustRingSize;
        public int GroundDebrisCount => ScaleCount(groundDebrisCount);
        public float GroundDebrisSize => groundDebrisSize;
        public float BleedDuration => bleedDuration;
        public float WoundBaseOpacity => woundBaseOpacity;
        public float InnerCutBrightness => innerCutBrightness;
        public float WetHighlightAmount => wetHighlightAmount;
        public float SeepageSpeed => seepageSpeed;
        public float DripFrequency => dripFrequency;
        public float DripSize => dripSize;
        public float WoundMistIntensity => woundMistIntensity;
        public float WoundCameraOffset => woundCameraOffset;
        public float WoundPulseStrength => woundPulseStrength;
        public float TickDuration => tickDuration;
        public float TickFlashBrightness => tickFlashBrightness;
        public float TickPulseSize => tickPulseSize;
        public int TickSprayAmount => ScaleCount(tickSprayAmount);
        public int TickDropletCount => ScaleCount(tickDropletCount);
        public float TickBodyAccentStrength => tickBodyAccentStrength;
        public float FinalTickMultiplier => finalTickMultiplier;
        public float CriticalFlashMultiplier => criticalFlashMultiplier;
        public int CriticalTrailCount => criticalTrailCount;
        public float CriticalBloodMultiplier => criticalBloodMultiplier;
        public int CriticalSparkAmount => ScaleCount(criticalSparkAmount);
        public float ResetRingSize => resetRingSize;
        public float ResetRingBrightness => resetRingBrightness;
        public float ResetRingDuration => resetRingDuration;
        public bool EnableLocalPlayerScreenAccent => enableLocalPlayerScreenAccent;
        public float WoundDimmingDuration => woundDimmingDuration;
        public int FinalDropletAmount => ScaleCount(finalDropletAmount);
        public float WoundDissolveDuration => woundDissolveDuration;
        public float MistFadeDuration => mistFadeDuration;

        public float StackWoundScale(int stack) => StackValue(woundScaleByStack, stack);
        public float StackBrightness(int stack) => StackValue(brightnessByStack, stack);
        public float StackSeepage(int stack) => StackValue(seepageByStack, stack);
        public float StackDroplets(int stack) => StackValue(dropletByStack, stack);
        public float StackTickIntensity(int stack) => StackValue(tickIntensityByStack, stack);
        public float StackMist(int stack) => StackValue(mistByStack, stack);

        public bool UpgradeToLatestDefaults()
        {
            if (profileVersion >= LatestProfileVersion)
            {
                return false;
            }

            overallBrightness = 1.12f;
            goreIntensity = 0.98f;
            mainTrailWidth = 0.82f;
            mainTrailLength = 2.35f;
            mainTrailBrightness = 1.55f;
            tearingTrailWidth = 0.5f;
            tearingTrailColor = new Color(0.92f, 0.025f, 0.012f, 0.96f);
            motionFragmentAmount = 9;

            impactDuration = 0.44f;
            contactFlashSize = 1.18f;
            contactFlashBrightness = 2.05f;
            woundSize = 1.08f;
            mainBloodSprayAmount = 48;
            sprayLength = 2.3f;
            closeBurstAmount = 34;
            tornFragmentCount = 8;
            dustBurstAmount = 20;
            dustBurstSize = 1.45f;
            environmentalHeavyDustAmount = 42;
            environmentalHeavyDustSize = 1.5f;
            environmentalHeavyDustLifetime = 2.5f;
            environmentalFineDustAmount = 32;
            environmentalFineDustSize = 0.92f;
            environmentalFineDustLifetime = 3f;
            dustRingSize = 3.35f;
            groundDebrisCount = 14;
            groundDebrisSize = 0.24f;

            woundBaseOpacity = 0.98f;
            innerCutBrightness = 1.68f;
            wetHighlightAmount = 0.66f;
            dripFrequency = 0.9f;
            dripSize = 0.15f;
            woundMistIntensity = 0.32f;
            woundCameraOffset = 0.36f;
            tickFlashBrightness = 2.05f;
            tickPulseSize = 1.22f;
            tickSprayAmount = 10;
            tickDropletCount = 7;
            tickBodyAccentStrength = 0.34f;
            woundScaleByStack = new Vector3(1f, 1.18f, 1.36f);
            brightnessByStack = new Vector3(1f, 1.18f, 1.34f);
            seepageByStack = new Vector3(1f, 1.42f, 1.9f);
            dropletByStack = new Vector3(1f, 1.45f, 1.95f);
            tickIntensityByStack = new Vector3(1f, 1.28f, 1.58f);
            mistByStack = new Vector3(1f, 1.38f, 1.78f);
            criticalFlashMultiplier = 1.9f;
            criticalBloodMultiplier = 1.72f;
            criticalSparkAmount = 14;
            resetRingSize = 0.92f;
            resetRingBrightness = 1.85f;
            profileVersion = LatestProfileVersion;
            return true;
        }

        private int ScaleCount(int value)
        {
            float multiplier = particleQuality == ParticleQuality.Low ? 0.5f : particleQuality == ParticleQuality.Medium ? 0.75f : 1f;
            return Mathf.RoundToInt(value * multiplier);
        }

        private static float StackValue(Vector3 values, int stack)
        {
            return stack <= 1 ? values.x : stack == 2 ? values.y : values.z;
        }
    }
}
