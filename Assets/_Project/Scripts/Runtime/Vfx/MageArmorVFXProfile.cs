using UnityEngine;

namespace RPGClone.Vfx.Arcane
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Mage Armor Profile", fileName = "MageArmorVFXProfile")]
    public sealed class MageArmorVFXProfile : ScriptableObject
    {
        [Header("Master")]
        [SerializeField, Min(0.01f)] private float overallScale = 1f;
        [SerializeField, Range(0f, 3f)] private float overallBrightness = 1f;
        [SerializeField, Min(0.2f)] private float effectDuration = 1.05f;

        [Header("Application Flash")]
        [SerializeField, Range(0f, 4f)] private float centralFlashBrightness = 1.65f;
        [SerializeField, Min(0.05f)] private float centralFlashSize = 1.2f;
        [SerializeField, Range(0f, 3f)] private float pulseIntensity = 1.25f;

        [Header("Protective Shell")]
        [SerializeField, Min(0.1f)] private float shellSize = 1.4f;
        [SerializeField, Range(0f, 1f)] private float shellOpacity = 0.38f;
        [SerializeField, Range(0f, 0.3f)] private float shellDistortion = 0.055f;
        [SerializeField, ColorUsage(true, true)] private Color shellColor = new(0.38f, 0.82f, 1.45f, 0.72f);

        [Header("Arcane Armor Facets")]
        [SerializeField, Range(0, 18)] private int facetCount = 7;
        [SerializeField, Min(0.05f)] private float facetSize = 0.72f;

        [Header("Rising Rings")]
        [SerializeField, Range(0, 2)] private int ringCount = 2;
        [SerializeField, Min(0f)] private float ringRiseSpeed = 2.9f;
        [SerializeField, Min(0.02f)] private float ringWidth = 1.75f;

        [Header("Finishing Accents")]
        [SerializeField, Range(0, 32)] private int sparkleCount = 12;
        [SerializeField, Range(0, 64)] private int particleCount = 24;
        [SerializeField, Min(0.05f)] private float overheadFocusSize = 0.78f;

        [Header("Arcane Palette")]
        [SerializeField, ColorUsage(true, true)] private Color brightWhite = new(1.65f, 1.65f, 1.65f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color paleCyan = new(0.58f, 1.3f, 1.55f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color lightBlue = new(0.32f, 0.74f, 1.35f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color lavender = new(0.82f, 0.58f, 1.45f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color violet = new(0.48f, 0.2f, 1.12f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color softIndigo = new(0.2f, 0.18f, 0.68f, 1f);

        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public float EffectDuration => effectDuration;
        public float CentralFlashBrightness => centralFlashBrightness;
        public float CentralFlashSize => centralFlashSize;
        public float PulseIntensity => pulseIntensity;
        public float ShellSize => shellSize;
        public float ShellOpacity => shellOpacity;
        public float ShellDistortion => shellDistortion;
        public Color ShellColor => shellColor;
        public int FacetCount => facetCount;
        public float FacetSize => facetSize;
        public int RingCount => ringCount;
        public float RingRiseSpeed => ringRiseSpeed;
        public float RingWidth => ringWidth;
        public int SparkleCount => sparkleCount;
        public int ParticleCount => particleCount;
        public float OverheadFocusSize => overheadFocusSize;
        public Color BrightWhite => brightWhite;
        public Color PaleCyan => paleCyan;
        public Color LightBlue => lightBlue;
        public Color Lavender => lavender;
        public Color Violet => violet;
        public Color SoftIndigo => softIndigo;
    }
}
