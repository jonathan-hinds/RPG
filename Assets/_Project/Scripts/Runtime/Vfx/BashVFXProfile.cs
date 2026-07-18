using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Bash Profile", fileName = "BashVFXProfile")]
    public sealed class BashVFXProfile : ScriptableObject
    {
        [Header("Master")]
        [SerializeField, Min(0.01f)] private float overallScale = 1f;
        [SerializeField, Range(0f, 3f)] private float overallBrightness = 1f;
        [SerializeField, ColorUsage(true, true)] private Color colorTint = Color.white;

        [Header("Contact")]
        [SerializeField, Range(0f, 3f)] private float flashIntensity = 1.35f;
        [SerializeField, Min(0.05f)] private float flashSize = 0.9f;
        [SerializeField, Min(0.05f)] private float impactBurstSize = 1.35f;
        [SerializeField, Range(0, 24)] private int burstPieceCount = 9;
        [SerializeField, Min(0f)] private float directionalForceDistance = 1.25f;

        [Header("Impact Rhythm")]
        [SerializeField, Min(0.05f)] private float impactBackplateSize = 1.48f;
        [SerializeField, Min(0.05f)] private float secondaryBurstSize = 1.62f;
        [SerializeField, Min(0f)] private float secondaryBurstDelay = 0.055f;
        [SerializeField, Range(0, 16)] private int momentumStreakCount = 6;
        [SerializeField, Min(0f)] private float momentumStreakSpeed = 7.2f;
        [SerializeField, Range(0f, 0.4f)] private float impactPunchOvershoot = 0.18f;
        [SerializeField, Min(0.05f)] private float impactPunchDuration = 0.14f;

        [Header("Ground Reaction")]
        [SerializeField, Range(0, 24)] private int dustAmount = 8;
        [SerializeField, Min(0.05f)] private float dustSize = 0.52f;
        [SerializeField, Min(0.05f)] private float dustRingRadius = 2.25f;
        [SerializeField, Range(0.05f, 1f)] private float dustRingStartScale = 0.24f;
        [SerializeField, Range(0f, 1f)] private float dustRingOpacity = 0.72f;
        [SerializeField, Min(0.05f)] private float dustRingDuration = 0.42f;
        [SerializeField, Range(0, 16)] private int debrisCount = 6;
        [SerializeField, Min(0f)] private float debrisSpeed = 2.35f;
        [SerializeField, Min(0f)] private float groundReactionDelay = 0.035f;

        [Header("Environmental Impact")]
        [Tooltip("World-space painterly ground pieces layered beneath the existing Bash contact burst.")]
        [SerializeField, Range(0, 24)] private int environmentalBurstAmount = 8;
        [SerializeField, Min(0.05f)] private float environmentalBurstSize = 1.72f;
        [SerializeField, Min(0.05f)] private float environmentalBurstLifetime = 0.46f;
        [Tooltip("Rolling world-space dust that remains planted at the collision point.")]
        [SerializeField, Range(0, 48)] private int environmentalHeavyDustAmount = 18;
        [SerializeField, Min(0.05f)] private float environmentalHeavyDustSize = 0.9f;
        [SerializeField, Min(0.05f)] private float environmentalHeavyDustLifetime = 1.65f;
        [Tooltip("Lighter airborne recovery dust that outlives the contact flash.")]
        [SerializeField, Range(0, 48)] private int environmentalFineDustAmount = 12;
        [SerializeField, Min(0.05f)] private float environmentalFineDustSize = 0.55f;
        [SerializeField, Min(0.05f)] private float environmentalFineDustLifetime = 2.25f;
        [SerializeField, Min(0f)] private float environmentalDustDelay = 0.02f;

        [Header("Armor Sparks")]
        [SerializeField, Range(0, 20)] private int sparkCount = 6;
        [SerializeField, Min(0f)] private float sparkSpeed = 5.8f;
        [SerializeField, Min(0.01f)] private float sparkSize = 0.16f;

        [Header("Stun Accent")]
        [Tooltip("The generic Bash hit wrapper uses this value. Call Play(false) when gameplay reports an immune or resisted stun.")]
        [SerializeField] private bool showStunAccentByDefault = true;
        [SerializeField, Range(0, 12)] private int stunStarCount = 5;
        [SerializeField, Min(0.1f)] private float stunDuration = 1.15f;
        [SerializeField, Min(0.05f)] private float stunStarSize = 0.2f;
        [SerializeField, Min(0f)] private float stunHeight = 0.9f;
        [SerializeField, Min(0f)] private float stunOrbitRadius = 0.38f;
        [SerializeField] private float stunOrbitDegreesPerSecond = 82f;
        [SerializeField, Min(0f)] private float stunBobAmount = 0.055f;
        [SerializeField, Min(0f)] private float stunBobSpeed = 3.6f;
        [SerializeField, Min(0f)] private float stunDelay = 0.09f;

        [Header("Swing Accent")]
        [SerializeField, Min(0.05f)] private float swingArcSize = 1.15f;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float swingDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float flashDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float impactDuration = 0.34f;
        [SerializeField, Min(0.05f)] private float dustDuration = 0.48f;

        [Header("Physical Palette")]
        [SerializeField, ColorUsage(true, true)] private Color flashColor = new(1.45f, 1.28f, 0.78f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color impactColor = new(1.1f, 0.72f, 0.24f, 0.92f);
        [SerializeField] private Color impactBackplateColor = new(0.2f, 0.09f, 0.025f, 0.72f);
        [SerializeField, ColorUsage(true, true)] private Color secondaryImpactColor = new(1.18f, 0.42f, 0.08f, 0.84f);
        [SerializeField] private Color dustColor = new(0.62f, 0.58f, 0.5f, 0.7f);
        [SerializeField] private Color debrisColor = new(0.38f, 0.31f, 0.23f, 0.86f);
        [SerializeField] private Color environmentalBurstColor = new(0.72f, 0.48f, 0.25f, 0.86f);
        [SerializeField] private Color environmentalHeavyDustColor = new(0.62f, 0.48f, 0.31f, 0.72f);
        [SerializeField] private Color environmentalFineDustColor = new(0.82f, 0.7f, 0.5f, 0.48f);
        [SerializeField, ColorUsage(true, true)] private Color sparkColor = new(1.35f, 1.08f, 0.58f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color stunColor = new(1.2f, 0.88f, 0.26f, 0.84f);

        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public Color ColorTint => colorTint;
        public float FlashIntensity => flashIntensity;
        public float FlashSize => flashSize;
        public float ImpactBurstSize => impactBurstSize;
        public int BurstPieceCount => burstPieceCount;
        public float DirectionalForceDistance => directionalForceDistance;
        public float ImpactBackplateSize => impactBackplateSize;
        public float SecondaryBurstSize => secondaryBurstSize;
        public float SecondaryBurstDelay => secondaryBurstDelay;
        public int MomentumStreakCount => momentumStreakCount;
        public float MomentumStreakSpeed => momentumStreakSpeed;
        public float ImpactPunchOvershoot => impactPunchOvershoot;
        public float ImpactPunchDuration => impactPunchDuration;
        public int DustAmount => dustAmount;
        public float DustSize => dustSize;
        public float DustRingRadius => dustRingRadius;
        public float DustRingStartScale => dustRingStartScale;
        public float DustRingOpacity => dustRingOpacity;
        public float DustRingDuration => dustRingDuration;
        public int DebrisCount => debrisCount;
        public float DebrisSpeed => debrisSpeed;
        public float GroundReactionDelay => groundReactionDelay;
        public int EnvironmentalBurstAmount => environmentalBurstAmount;
        public float EnvironmentalBurstSize => environmentalBurstSize;
        public float EnvironmentalBurstLifetime => environmentalBurstLifetime;
        public int EnvironmentalHeavyDustAmount => environmentalHeavyDustAmount;
        public float EnvironmentalHeavyDustSize => environmentalHeavyDustSize;
        public float EnvironmentalHeavyDustLifetime => environmentalHeavyDustLifetime;
        public int EnvironmentalFineDustAmount => environmentalFineDustAmount;
        public float EnvironmentalFineDustSize => environmentalFineDustSize;
        public float EnvironmentalFineDustLifetime => environmentalFineDustLifetime;
        public float EnvironmentalDustDelay => environmentalDustDelay;
        public int SparkCount => sparkCount;
        public float SparkSpeed => sparkSpeed;
        public float SparkSize => sparkSize;
        public bool ShowStunAccentByDefault => showStunAccentByDefault;
        public int StunStarCount => stunStarCount;
        public float StunDuration => stunDuration;
        public float StunStarSize => stunStarSize;
        public float StunHeight => stunHeight;
        public float StunOrbitRadius => stunOrbitRadius;
        public float StunOrbitDegreesPerSecond => stunOrbitDegreesPerSecond;
        public float StunBobAmount => stunBobAmount;
        public float StunBobSpeed => stunBobSpeed;
        public float StunDelay => stunDelay;
        public float SwingArcSize => swingArcSize;
        public float SwingDuration => swingDuration;
        public float FlashDuration => flashDuration;
        public float ImpactDuration => impactDuration;
        public float DustDuration => dustDuration;
        public Color FlashColor => flashColor;
        public Color ImpactColor => impactColor;
        public Color ImpactBackplateColor => impactBackplateColor;
        public Color SecondaryImpactColor => secondaryImpactColor;
        public Color DustColor => dustColor;
        public Color DebrisColor => debrisColor;
        public Color EnvironmentalBurstColor => environmentalBurstColor;
        public Color EnvironmentalHeavyDustColor => environmentalHeavyDustColor;
        public Color EnvironmentalFineDustColor => environmentalFineDustColor;
        public Color SparkColor => sparkColor;
        public Color StunColor => stunColor;
    }
}
