using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Flamestrike Profile", fileName = "FlamestrikeVFXProfile")]
    public sealed class FlamestrikeVFXProfile : ScriptableObject
    {
        [Header("Targeting")]
        [SerializeField, Min(0.1f)] private float targetRadius = 5f;
        [SerializeField, Min(0.005f)] private float ringWidth = 0.045f;
        [SerializeField, Range(0f, 3f)] private float ringBrightness = 0.8f;
        [SerializeField] private float runeRotationSpeed = 22f;
        [SerializeField, Range(0f, 1f)] private float groundTintOpacity = 0.34f;
        [SerializeField, Range(0, 30)] private int targetingEmberRate;
        [SerializeField, Min(0.05f)] private float centerMarkerSize = 0.55f;
        [SerializeField, ColorUsage(true, true)] private Color validTargetColor = new(0.08f, 0.46f, 1.35f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color invalidTargetColor = new(1.15f, 0.035f, 0.02f, 1f);

        [Header("Cast")]
        [SerializeField, Min(0.1f)] private float castDuration = 2f;
        [SerializeField, Min(0.05f)] private float handFlameScale = 0.52f;
        [SerializeField, Min(0.05f)] private float fireCoreScale = 0.7f;
        [SerializeField, Range(0f, 5f)] private float fireCoreBrightness = 1.55f;
        [SerializeField, Range(0f, 2f)] private float handConductionAmount = 1f;
        [SerializeField, Range(1, 8)] private int casterRibbonCount = 3;
        [SerializeField, Min(0f)] private float casterRibbonSpeed = 1.8f;
        [SerializeField, Range(0, 60)] private int castEmberAmount = 18;
        [SerializeField, Range(0f, 3f)] private float targetBuildupIntensity = 1.1f;

        [Header("Initial Impact")]
        [SerializeField, Min(0.1f)] private float centralFlashSize = 4.2f;
        [SerializeField, Range(0f, 6f)] private float centralFlashBrightness = 2.4f;
        [SerializeField, Min(0.1f)] private float mainColumnHeight = 9f;
        [SerializeField, Min(0.1f)] private float mainColumnWidth = 3.2f;
        [SerializeField, Range(0, 12)] private int secondaryPillarCount = 6;
        [SerializeField, Min(0.1f)] private float secondaryPillarSize = 2.4f;
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 5f;
        [SerializeField, Min(0.1f)] private float shockwaveSpeed = 13f;
        [SerializeField, Range(0, 24)] private int radialFlameBladeCount = 12;
        [SerializeField, Range(0, 160)] private int impactEmberCount = 72;
        [SerializeField, Range(0, 80)] private int impactDebrisCount = 24;
        [SerializeField, Range(0, 60)] private int smokeCrownAmount = 18;
        [SerializeField, Range(0f, 0.25f)] private float distortionStrength = 0.075f;

        [Header("Persistent Ground")]
        [SerializeField, Min(0.1f)] private float areaRadius = 5f;
        [SerializeField, Range(1, 24)] private int mainFlamePatchCount = 11;
        [SerializeField, Range(0, 40)] private int smallFlameCount = 16;
        [SerializeField] private Vector2 flameHeightRange = new(1.2f, 2.8f);
        [SerializeField, Range(0f, 3f)] private float centralVentIntensity = 1.15f;
        [SerializeField, Range(0f, 5f)] private float crackBrightness = 1.4f;
        [SerializeField, Range(0f, 2f)] private float crackDensity = 1f;
        [SerializeField, Range(0f, 1f)] private float scorchedGroundOpacity = 0.72f;
        [SerializeField, Range(0f, 2f)] private float perimeterFlameDensity = 0.85f;
        [SerializeField, Range(0, 80)] private int emberRate = 24;
        [SerializeField, Range(0, 50)] private int smokeAmount = 13;
        [SerializeField, Range(0, 50)] private int ashAmount = 10;
        [SerializeField, Range(0f, 2f)] private float heatDistortionAmount = 0.65f;

        [Header("Damage Pulses")]
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.42f;
        [SerializeField, Range(0f, 5f)] private float pulseBrightness = 1.65f;
        [SerializeField, Range(1f, 2f)] private float flameSurgeScale = 1.28f;
        [SerializeField, Range(0, 16)] private int localEruptionCount = 6;
        [SerializeField, Min(0.1f)] private float circularPulseSpeed = 15f;
        [SerializeField, Range(0, 100)] private int pulseEmberAmount = 32;
        [SerializeField, Range(0f, 5f)] private float crackBrighteningStrength = 2f;
        [SerializeField, Range(1f, 2f)] private float finalPulseMultiplier = 1.3f;

        [Header("Enemy Reactions")]
        [SerializeField, Min(0.05f)] private float initialHitFlashScale = 1.2f;
        [SerializeField, Min(0.05f)] private float initialTargetFlameHeight = 1.8f;
        [SerializeField, Range(0f, 4f)] private float tickReactionBrightness = 1.25f;
        [SerializeField, Range(0, 24)] private int tickSparkAmount = 6;
        [SerializeField, Range(0, 12)] private int tickSmokeAmount = 2;

        [Header("Expiration")]
        [SerializeField, Min(0.1f)] private float burnDuration = 8f;
        [SerializeField, Min(0.1f)] private float flameFadeDuration = 0.9f;
        [SerializeField, Min(0.1f)] private float crackCoolingDuration = 1.15f;
        [SerializeField, Range(0, 60)] private int finalSmokeAmount = 22;
        [SerializeField, Min(0.1f)] private float scorchedGroundFadeDuration = 1.4f;

        [Header("Global")]
        [SerializeField, Min(0.1f)] private float overallEffectScale = 1f;
        [SerializeField, Range(0f, 4f)] private float overallBrightness = 1.1f;
        [SerializeField, ColorUsage(true, true)] private Color hotColor = new(1.65f, 1.22f, 0.48f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color flameColor = new(1.45f, 0.38f, 0.025f, 0.96f);
        [SerializeField, ColorUsage(true, true)] private Color outerColor = new(0.78f, 0.07f, 0.008f, 0.72f);
        [SerializeField] private Color smokeColor = new(0.18f, 0.075f, 0.045f, 0.52f);
        [SerializeField, Range(0, 2)] private int distortionQuality = 1;
        [SerializeField, Range(0, 2)] private int particleQualityLevel = 1;
        [SerializeField, Min(1f)] private float distantReductionStart = 35f;
        [SerializeField, Min(1f)] private float cullDistance = 75f;

        public float TargetRadius => targetRadius; public float RingWidth => ringWidth; public float RingBrightness => ringBrightness;
        public float RuneRotationSpeed => runeRotationSpeed; public float GroundTintOpacity => groundTintOpacity; public int TargetingEmberRate => targetingEmberRate; public float CenterMarkerSize => centerMarkerSize;
        public Color ValidTargetColor => validTargetColor; public Color InvalidTargetColor => invalidTargetColor;
        public float CastDuration => castDuration; public float HandFlameScale => handFlameScale; public float FireCoreScale => fireCoreScale; public float FireCoreBrightness => fireCoreBrightness;
        public float HandConductionAmount => handConductionAmount; public int CasterRibbonCount => casterRibbonCount; public float CasterRibbonSpeed => casterRibbonSpeed; public int CastEmberAmount => castEmberAmount; public float TargetBuildupIntensity => targetBuildupIntensity;
        public float CentralFlashSize => centralFlashSize; public float CentralFlashBrightness => centralFlashBrightness; public float MainColumnHeight => mainColumnHeight; public float MainColumnWidth => mainColumnWidth;
        public int SecondaryPillarCount => secondaryPillarCount; public float SecondaryPillarSize => secondaryPillarSize; public float ShockwaveRadius => shockwaveRadius; public float ShockwaveSpeed => shockwaveSpeed;
        public int RadialFlameBladeCount => radialFlameBladeCount; public int ImpactEmberCount => impactEmberCount; public int ImpactDebrisCount => impactDebrisCount; public int SmokeCrownAmount => smokeCrownAmount; public float DistortionStrength => distortionStrength;
        public float AreaRadius => areaRadius; public int MainFlamePatchCount => mainFlamePatchCount; public int SmallFlameCount => smallFlameCount; public Vector2 FlameHeightRange => flameHeightRange;
        public float CentralVentIntensity => centralVentIntensity; public float CrackBrightness => crackBrightness; public float CrackDensity => crackDensity; public float ScorchedGroundOpacity => scorchedGroundOpacity;
        public float PerimeterFlameDensity => perimeterFlameDensity; public int EmberRate => emberRate; public int SmokeAmount => smokeAmount; public int AshAmount => ashAmount; public float HeatDistortionAmount => heatDistortionAmount;
        public float PulseDuration => pulseDuration; public float PulseBrightness => pulseBrightness; public float FlameSurgeScale => flameSurgeScale; public int LocalEruptionCount => localEruptionCount;
        public float CircularPulseSpeed => circularPulseSpeed; public int PulseEmberAmount => pulseEmberAmount; public float CrackBrighteningStrength => crackBrighteningStrength; public float FinalPulseMultiplier => finalPulseMultiplier;
        public float InitialHitFlashScale => initialHitFlashScale; public float InitialTargetFlameHeight => initialTargetFlameHeight; public float TickReactionBrightness => tickReactionBrightness; public int TickSparkAmount => tickSparkAmount; public int TickSmokeAmount => tickSmokeAmount;
        public float BurnDuration => burnDuration; public float FlameFadeDuration => flameFadeDuration; public float CrackCoolingDuration => crackCoolingDuration; public int FinalSmokeAmount => finalSmokeAmount; public float ScorchedGroundFadeDuration => scorchedGroundFadeDuration;
        public float OverallEffectScale => overallEffectScale; public float OverallBrightness => overallBrightness; public Color HotColor => hotColor; public Color FlameColor => flameColor; public Color OuterColor => outerColor; public Color SmokeColor => smokeColor;
        public int DistortionQuality => distortionQuality; public int ParticleQualityLevel => particleQualityLevel;
        public float DistantReductionStart => distantReductionStart; public float CullDistance => Mathf.Max(distantReductionStart + 1f, cullDistance);
        public float TotalLifetime => 0.85f + burnDuration + Mathf.Max(flameFadeDuration, crackCoolingDuration, scorchedGroundFadeDuration);
    }
}
