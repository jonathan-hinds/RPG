using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    public enum BerzerkitisParticleQuality
    {
        Low,
        Medium,
        High
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Berzerkitis Profile", fileName = "BerzerkitisVFXProfile")]
    public sealed class BerzerkitisVFXProfile : ScriptableObject
    {
        [System.Serializable]
        public sealed class FirePalette
        {
            [ColorUsage(true, true)] public Color WhiteHot = new(2.4f, 2.05f, 1.15f, 1f);
            [ColorUsage(true, true)] public Color HotYellow = new(2f, 1.25f, 0.18f, 1f);
            [ColorUsage(true, true)] public Color GoldenOrange = new(1.6f, 0.62f, 0.08f, 1f);
            [ColorUsage(true, true)] public Color DeepOrange = new(1.18f, 0.22f, 0.025f, 1f);
            [ColorUsage(true, true)] public Color BloodRed = new(0.74f, 0.045f, 0.018f, 1f);
            [ColorUsage(true, true)] public Color DarkCrimson = new(0.2f, 0.008f, 0.008f, 1f);
            public Color Charcoal = new(0.105f, 0.08f, 0.075f, 0.62f);
            public Color Dust = new(0.5f, 0.31f, 0.18f, 0.68f);
        }

        [Header("Global")]
        [SerializeField, Range(0f, 4f)] private float overallBrightness = 1.15f;
        [SerializeField] private FirePalette colors = new();
        [SerializeField] private BerzerkitisParticleQuality particleQuality = BerzerkitisParticleQuality.High;
        [SerializeField, Min(0.05f)] private float buffFadeOutDuration = 0.4f;

        [Header("Activation")]
        [SerializeField, Min(0.05f)] private float overallActivationScale = 1f;
        [SerializeField, Range(1f, 1.5f)] private float activationDuration = 1.25f;
        [SerializeField, Range(0f, 5f)] private float chestFlashBrightness = 2.3f;
        [SerializeField, Min(0.1f)] private float rageEnvelopeSize = 2.65f;
        [SerializeField, Range(0f, 1f)] private float rageEnvelopeOpacity = 0.68f;
        [SerializeField, Range(1, 12)] private int flameColumnCount = 6;
        [SerializeField, Min(0.1f)] private float flameColumnHeight = 3.5f;
        [SerializeField, Min(0.1f)] private float rageSilhouetteScale = 1.35f;
        [SerializeField, Min(0.1f)] private float shockwaveSize = 4.4f;
        [SerializeField, Min(0.1f)] private float shockwaveSpeed = 1f;
        [SerializeField, Range(0, 128)] private int activationEmberCount = 44;
        [SerializeField, Range(0, 64)] private int dustAmount = 18;
        [SerializeField, Range(0f, 0.2f)] private float heatDistortionStrength = 0.045f;

        [Header("Buff Emblem")]
        [SerializeField, Min(0.1f)] private float emblemScale = 1.25f;
        [SerializeField, Min(0f)] private float emblemHeight = 1.55f;
        [SerializeField, Min(0.01f)] private float emblemEmergenceSpeed = 1f;
        [SerializeField] private bool emblemFacesGameplayCamera = true;
        [SerializeField, Range(0f, 1f)] private float emblemPulseIntensity = 0.16f;
        [SerializeField, Range(0.4f, 1.4f)] private float emblemLifetime = 1.05f;
        [SerializeField, Range(0f, 5f)] private float emblemGlowBrightness = 1.8f;
        [SerializeField, Min(0.01f)] private float emblemDissolveSpeed = 1f;

        [Header("Persistent Hands")]
        [SerializeField, Min(0.05f)] private float handFlameScale = 0.21f;
        [SerializeField, Min(0.05f)] private float flameHeight = 0.36f;
        [SerializeField, Range(1, 24)] private int flameDensity = 9;
        [SerializeField, Range(0f, 5f)] private float coreBrightness = 2.15f;
        [SerializeField, Range(0f, 3f)] private float outerFlameIntensity = 1.05f;
        [SerializeField] private float wristRibbonSpeed = 115f;
        [SerializeField, Range(0f, 64f)] private float emberSpawnRate = 16f;
        [SerializeField, Min(0.01f)] private float motionTrailWidth = 0.22f;
        [SerializeField, Range(0.03f, 0.5f)] private float motionTrailLifetime = 0.14f;
        [SerializeField, Range(0f, 4f)] private float attackPulseIntensity = 1.75f;
        [SerializeField, Range(0.03f, 0.5f)] private float attackPulseDuration = 0.16f;
        [SerializeField] private Vector3 leftHandPositionOffset = new(-0.015f, 0.015f, 0f);
        [SerializeField] private Vector3 rightHandPositionOffset = new(0.015f, 0.015f, 0f);

        public float OverallBrightness => overallBrightness;
        public FirePalette Colors => colors;
        public BerzerkitisParticleQuality ParticleQuality => particleQuality;
        public float BuffFadeOutDuration => buffFadeOutDuration;
        public float OverallActivationScale => overallActivationScale;
        public float ActivationDuration => activationDuration;
        public float ChestFlashBrightness => chestFlashBrightness;
        public float RageEnvelopeSize => rageEnvelopeSize;
        public float RageEnvelopeOpacity => rageEnvelopeOpacity;
        public int FlameColumnCount => ScaledCount(flameColumnCount);
        public float FlameColumnHeight => flameColumnHeight;
        public float RageSilhouetteScale => rageSilhouetteScale;
        public float ShockwaveSize => shockwaveSize;
        public float ShockwaveSpeed => shockwaveSpeed;
        public int ActivationEmberCount => ScaledCount(activationEmberCount);
        public int DustAmount => ScaledCount(dustAmount);
        public float HeatDistortionStrength => heatDistortionStrength;
        public float EmblemScale => emblemScale;
        public float EmblemHeight => emblemHeight;
        public float EmblemEmergenceSpeed => emblemEmergenceSpeed;
        public bool EmblemFacesGameplayCamera => emblemFacesGameplayCamera;
        public float EmblemPulseIntensity => emblemPulseIntensity;
        public float EmblemLifetime => emblemLifetime;
        public float EmblemGlowBrightness => emblemGlowBrightness;
        public float EmblemDissolveSpeed => emblemDissolveSpeed;
        public float HandFlameScale => handFlameScale;
        public float FlameHeight => flameHeight;
        public int FlameDensity => ScaledCount(flameDensity);
        public float CoreBrightness => coreBrightness;
        public float OuterFlameIntensity => outerFlameIntensity;
        public float WristRibbonSpeed => wristRibbonSpeed;
        public float EmberSpawnRate => emberSpawnRate * QualityMultiplier;
        public float MotionTrailWidth => motionTrailWidth;
        public float MotionTrailLifetime => motionTrailLifetime;
        public float AttackPulseIntensity => attackPulseIntensity;
        public float AttackPulseDuration => attackPulseDuration;
        public Vector3 LeftHandPositionOffset => leftHandPositionOffset;
        public Vector3 RightHandPositionOffset => rightHandPositionOffset;

        private float QualityMultiplier => particleQuality switch
        {
            BerzerkitisParticleQuality.Low => 0.45f,
            BerzerkitisParticleQuality.Medium => 0.72f,
            _ => 1f
        };

        private int ScaledCount(int value)
        {
            return value <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(value * QualityMultiplier));
        }
    }
}
