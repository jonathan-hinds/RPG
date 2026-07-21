using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    public enum ThunderClapParticleQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Thunder Clap Profile", fileName = "ThunderClapVFX_Default")]
    public sealed class ThunderClapVFXProfile : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Range(0.08f, 0.25f)] private float anticipationDuration = 0.14f;
        [SerializeField, Range(0.15f, 0.65f)] private float expansionDuration = 0.42f;
        [SerializeField, Range(1f, 2.5f)] private float aftermathDuration = 1.65f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float flashBrightness = 3.2f;
        [SerializeField, Min(0.1f)] private float earthExplosionSize = 1.25f;
        [SerializeField, Range(0, 128)] private int heavyDustAmount = 34;
        [SerializeField, Range(0, 192)] private int fineDustAmount = 48;
        [SerializeField, Range(0, 96)] private int dirtChunkCount = 22;
        [SerializeField, Range(0, 64)] private int rockCount = 12;
        [SerializeField, Min(0f)] private float debrisVelocity = 5.4f;

        [Header("Shockwave")]
        [SerializeField, Min(0.5f)] private float ringRadius = 6f;
        [SerializeField, Min(1f)] private float expansionSpeed = 14.3f;
        [SerializeField, Min(0.1f)] private float dustWallHeight = 1.15f;
        [SerializeField, Range(0f, 2f)] private float pressureIntensity = 0.75f;
        [SerializeField, Range(0, 128)] private int dirtWakeDensity = 42;

        [Header("Lightning")]
        [SerializeField, Min(0f)] private float ringBrightness = 2.4f;
        [SerializeField, Min(0.01f)] private float ringThickness = 0.16f;
        [SerializeField, Range(0, 16)] private int branchCount = 6;
        [SerializeField, Min(0.1f)] private float branchLength = 1.35f;
        [SerializeField, Range(0, 16)] private int groundCrawlerDensity = 8;
        [SerializeField, Range(0, 256)] private int sparkAmount = 72;
        [SerializeField, Min(0f)] private float flashIntensity = 2.8f;

        [Header("Enemy Reactions")]
        [SerializeField, Range(0, 12)] private int bodyArcCount = 5;
        [SerializeField, Range(0.08f, 0.8f)] private float arcLifetime = 0.38f;
        [SerializeField, Min(0.1f)] private float debuffRingSize = 1.35f;
        [SerializeField, Min(0f)] private float debuffRingBrightness = 1.35f;

        [Header("Global")]
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Min(0f)] private float overallBrightness = 1f;
        [SerializeField] private Color earthColor = new(0.52f, 0.34f, 0.18f, 0.86f);
        [SerializeField] private Color warmDustColor = new(0.76f, 0.58f, 0.35f, 0.68f);
        [SerializeField] private Color stoneColor = new(0.42f, 0.43f, 0.46f, 0.92f);
        [SerializeField] private Color lightningColor = new(0.25f, 0.9f, 1f, 0.95f);
        [SerializeField] private Color lightningCoreColor = new(1f, 0.98f, 0.82f, 1f);
        [SerializeField] private Color lightningVioletColor = new(0.52f, 0.28f, 1f, 0.62f);
        [SerializeField] private ThunderClapParticleQuality particleQuality = ThunderClapParticleQuality.High;

        public float AnticipationDuration => anticipationDuration;
        public float ExpansionDuration => expansionDuration;
        public float AftermathDuration => aftermathDuration;
        public float FlashBrightness => flashBrightness;
        public float EarthExplosionSize => earthExplosionSize;
        public int HeavyDustAmount => ScaleCount(heavyDustAmount);
        public int FineDustAmount => ScaleCount(fineDustAmount);
        public int DirtChunkCount => ScaleCount(dirtChunkCount);
        public int RockCount => ScaleCount(rockCount);
        public float DebrisVelocity => debrisVelocity * overallScale;
        public float RingRadius => ringRadius * overallScale;
        public float ExpansionSpeed => expansionSpeed * overallScale;
        public float DustWallHeight => dustWallHeight * overallScale;
        public float PressureIntensity => pressureIntensity;
        public int DirtWakeDensity => ScaleCount(dirtWakeDensity);
        public float RingBrightness => ringBrightness;
        public float RingThickness => ringThickness * overallScale;
        public int BranchCount => ScaleCount(branchCount);
        public float BranchLength => branchLength * overallScale;
        public int GroundCrawlerDensity => ScaleCount(groundCrawlerDensity);
        public int SparkAmount => ScaleCount(sparkAmount);
        public float FlashIntensity => flashIntensity;
        public int BodyArcCount => ScaleCount(bodyArcCount);
        public float ArcLifetime => arcLifetime;
        public float DebuffRingSize => debuffRingSize * overallScale;
        public float DebuffRingBrightness => debuffRingBrightness;
        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public Color EarthColor => earthColor;
        public Color WarmDustColor => warmDustColor;
        public Color StoneColor => stoneColor;
        public Color LightningColor => lightningColor;
        public Color LightningCoreColor => lightningCoreColor;
        public Color LightningVioletColor => lightningVioletColor;
        public ThunderClapParticleQuality ParticleQuality => particleQuality;
        public float TotalLifetime => anticipationDuration + expansionDuration + aftermathDuration + 0.35f;

        public void ResetToProductionDefaults()
        {
            anticipationDuration = 0.14f;
            expansionDuration = 0.42f;
            aftermathDuration = 1.65f;
            flashBrightness = 3.2f;
            earthExplosionSize = 1.25f;
            heavyDustAmount = 34;
            fineDustAmount = 48;
            dirtChunkCount = 22;
            rockCount = 12;
            debrisVelocity = 5.4f;
            ringRadius = 6f;
            expansionSpeed = 14.3f;
            dustWallHeight = 1.15f;
            pressureIntensity = 0.75f;
            dirtWakeDensity = 42;
            ringBrightness = 2.4f;
            ringThickness = 0.16f;
            branchCount = 6;
            branchLength = 1.35f;
            groundCrawlerDensity = 8;
            sparkAmount = 72;
            flashIntensity = 2.8f;
            bodyArcCount = 5;
            arcLifetime = 0.38f;
            debuffRingSize = 1.35f;
            debuffRingBrightness = 1.35f;
            overallScale = 1f;
            overallBrightness = 1f;
            earthColor = new Color(0.52f, 0.34f, 0.18f, 0.86f);
            warmDustColor = new Color(0.76f, 0.58f, 0.35f, 0.68f);
            stoneColor = new Color(0.42f, 0.43f, 0.46f, 0.92f);
            lightningColor = new Color(0.25f, 0.9f, 1f, 0.95f);
            lightningCoreColor = new Color(1f, 0.98f, 0.82f, 1f);
            lightningVioletColor = new Color(0.52f, 0.28f, 1f, 0.62f);
            particleQuality = ThunderClapParticleQuality.High;
        }

        private int ScaleCount(int count)
        {
            float multiplier = particleQuality == ThunderClapParticleQuality.Low ? 0.45f
                : particleQuality == ThunderClapParticleQuality.Medium ? 0.72f : 1f;
            return count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * multiplier));
        }
    }
}
