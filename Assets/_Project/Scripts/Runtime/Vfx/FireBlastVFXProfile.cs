using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Fire Blast Profile", fileName = "FireBlastVFXProfile")]
    public sealed class FireBlastVFXProfile : ScriptableObject
    {
        [System.Serializable]
        public sealed class FirePalette
        {
            [ColorUsage(true, true)] public Color WhiteHot = new(1.8f, 1.55f, 1.1f, 1f);
            [ColorUsage(true, true)] public Color HotYellow = new(1.65f, 1.05f, 0.2f, 1f);
            [ColorUsage(true, true)] public Color GoldenOrange = new(1.45f, 0.46f, 0.045f, 1f);
            [ColorUsage(true, true)] public Color DeepOrange = new(1.05f, 0.16f, 0.012f, 0.9f);
            [ColorUsage(true, true)] public Color DarkRedOrange = new(0.62f, 0.045f, 0.005f, 0.75f);
            public Color Charcoal = new(0.18f, 0.095f, 0.065f, 0.48f);
        }

        [Header("Master")]
        [SerializeField, Min(0.1f)] private float overallScale = 1f;
        [SerializeField, Range(0.65f, 1.25f)] private float overallDuration = 1.05f;
        [SerializeField, Range(0f, 3f)] private float brightness = 1f;

        [Header("Instant Fire Streak (Not A Projectile)")]
        [Tooltip("1 spans exactly from the caster to the target. This only scales the fixed ribbon; it never controls travel time.")]
        [SerializeField, Range(0.85f, 1.15f)] private float fireStreakLength = 1f;
        [SerializeField, Range(0f, 3f)] private float fireStreakBrightness = 1.35f;

        [Header("Target Combustion")]
        [SerializeField, Min(0.1f)] private float explosionSize = 3.15f;
        [SerializeField, Range(0f, 3f)] private float explosionBrightness = 1.65f;
        [SerializeField, Range(1, 20)] private int flameCount = 12;
        [SerializeField, Min(0.05f)] private float flameSize = 1.12f;

        [Header("Lingering Fire")]
        [Tooltip("How long the secondary flames continue burning around the target after the initial detonation.")]
        [SerializeField, Range(0.35f, 1.1f)] private float lingeringFireDuration = 0.78f;

        [Header("Heat Ring")]
        [SerializeField, Min(0.1f)] private float heatRingSize = 4.1f;
        [SerializeField, Range(0.25f, 3f)] private float heatRingSpeed = 1.25f;

        [Header("Embers And Sparks")]
        [SerializeField, Range(0, 32)] private int emberCount = 18;
        [SerializeField, Min(0f)] private float emberSpeed = 2.65f;
        [SerializeField, Range(0, 24)] private int sparkCount = 14;

        [Header("Smoke Aftermath")]
        [SerializeField, Range(0, 12)] private int smokeAmount = 5;
        [SerializeField, Range(0.15f, 0.9f)] private float smokeDuration = 0.68f;

        [Header("Fire Color Palette")]
        [SerializeField] private FirePalette fireColors = new();
        [SerializeField, HideInInspector] private int contentVersion;

        public float OverallScale => overallScale;
        public float OverallDuration => overallDuration;
        public float Brightness => brightness;
        public float FireStreakLength => fireStreakLength;
        public float FireStreakBrightness => fireStreakBrightness;
        public float ExplosionSize => explosionSize;
        public float ExplosionBrightness => explosionBrightness;
        public int FlameCount => flameCount;
        public float FlameSize => flameSize;
        public float LingeringFireDuration => lingeringFireDuration;
        public float HeatRingSize => heatRingSize;
        public float HeatRingSpeed => heatRingSpeed;
        public int EmberCount => emberCount;
        public float EmberSpeed => emberSpeed;
        public int SparkCount => sparkCount;
        public int SmokeAmount => smokeAmount;
        public float SmokeDuration => smokeDuration;
        public FirePalette Colors => fireColors;

        public bool UpgradePolishDefaults()
        {
            if (contentVersion >= 1)
            {
                return false;
            }

            overallScale = 1.05f;
            overallDuration = 1.05f;
            brightness = 1.12f;
            fireStreakBrightness = 1.4f;
            explosionSize = 3.15f;
            explosionBrightness = 1.65f;
            flameCount = 12;
            flameSize = 1.12f;
            lingeringFireDuration = 0.78f;
            heatRingSize = 4.1f;
            heatRingSpeed = 1.15f;
            emberCount = 18;
            emberSpeed = 2.65f;
            sparkCount = 14;
            smokeAmount = 5;
            smokeDuration = 0.68f;
            contentVersion = 1;
            return true;
        }
    }
}
