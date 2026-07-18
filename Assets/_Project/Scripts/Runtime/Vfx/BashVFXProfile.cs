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

        [Header("Ground Reaction")]
        [SerializeField, Range(0, 24)] private int dustAmount = 8;
        [SerializeField, Min(0.05f)] private float dustSize = 0.52f;
        [SerializeField, Min(0.05f)] private float dustRingRadius = 2.25f;
        [SerializeField, Range(0.05f, 1f)] private float dustRingStartScale = 0.24f;
        [SerializeField, Range(0f, 1f)] private float dustRingOpacity = 0.72f;
        [SerializeField, Min(0.05f)] private float dustRingDuration = 0.42f;

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
        [SerializeField] private Color dustColor = new(0.62f, 0.58f, 0.5f, 0.7f);
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
        public int DustAmount => dustAmount;
        public float DustSize => dustSize;
        public float DustRingRadius => dustRingRadius;
        public float DustRingStartScale => dustRingStartScale;
        public float DustRingOpacity => dustRingOpacity;
        public float DustRingDuration => dustRingDuration;
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
        public float SwingArcSize => swingArcSize;
        public float SwingDuration => swingDuration;
        public float FlashDuration => flashDuration;
        public float ImpactDuration => impactDuration;
        public float DustDuration => dustDuration;
        public Color FlashColor => flashColor;
        public Color ImpactColor => impactColor;
        public Color DustColor => dustColor;
        public Color SparkColor => sparkColor;
        public Color StunColor => stunColor;
    }
}
