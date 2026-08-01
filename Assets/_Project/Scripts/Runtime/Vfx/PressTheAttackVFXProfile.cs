using UnityEngine;

namespace RPGClone.Vfx
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Press the Attack Profile", fileName = "PressTheAttackVFXProfile")]
    public sealed class PressTheAttackVFXProfile : ScriptableObject
    {
        [Header("Palette")]
        [SerializeField] private Color mainCrimson = new(1f, 0.006f, 0.012f, 1f);
        [SerializeField] private Color darkRed = new(0.18f, 0f, 0.006f, 1f);
        [SerializeField] private Color highlight = new(1f, 0.22f, 0.16f, 1f);

        [Header("Character Overlay")]
        [SerializeField, Range(0f, 12f)] private float activationIntensity = 7.2f;
        [SerializeField, Range(0f, 12f)] private float persistentOverlayIntensity = 4.6f;
        [SerializeField, Range(0f, 8f)] private float lightningFrequency = 4.4f;
        [SerializeField, Range(0.01f, 0.3f)] private float lightningThickness = 0.115f;
        [SerializeField, Range(0f, 8f)] private float lightningSpeed = 2.25f;
        [SerializeField, Range(0f, 0.25f)] private float lightningDistortion = 0.062f;
        [SerializeField, Range(0.1f, 10f)] private float edgeGlowWidth = 3.4f;
        [SerializeField, Range(0f, 8f)] private float edgeGlowIntensity = 3.15f;
        [SerializeField, Range(0f, 8f)] private float surfaceStreakSpeed = 1.85f;
        [SerializeField, Range(0f, 0.02f)] private float surfaceLift = 0.0028f;
        [SerializeField, Range(0.25f, 6f)] private float surfacePatternScale = 2.05f;
        [SerializeField, Range(0f, 12f)] private float surfacePulseSpeed = 4.25f;
        [SerializeField, Range(0f, 5f)] private float travellingPulseSpeed = 0.82f;
        [SerializeField, Range(0f, 2f)] private float rageUndercoatIntensity = 0.4f;

        [Header("Response")]
        [SerializeField, Range(0f, 4f)] private float movementResponse = 1.1f;
        [SerializeField, Range(0f, 4f)] private float attackResponse = 1.5f;
        [SerializeField, Range(0f, 4f)] private float finalSecondInstability = 0.7f;
        [SerializeField, Min(0.01f)] private float movementSpeedForFullResponse = 4.5f;
        [SerializeField, Min(0.01f)] private float attackResponseDuration = 0.32f;

        [Header("Particles")]
        [SerializeField, Range(0, 30)] private int surfaceSparkAmount = 8;
        [SerializeField, Range(0, 30)] private int movementStreakAmount = 6;
        [SerializeField, Range(0, 12)] private int attackAccentAmount = 4;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float activationDuration = 0.95f;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.22f;
        [SerializeField, Min(0.1f)] private float authoritativeBuffHandshakeTimeout = 2f;

        [Header("Optional Presentation")]
        [SerializeField, Range(0f, 6f)] private float optionalLightIntensity = 2.4f;
        [SerializeField, Min(0.01f)] private float optionalLightDuration = 0.18f;
        [SerializeField] private bool optionalCameraImpulse;

        public Color MainCrimson => mainCrimson;
        public Color DarkRed => darkRed;
        public Color Highlight => highlight;
        public float ActivationIntensity => activationIntensity;
        public float PersistentOverlayIntensity => persistentOverlayIntensity;
        public float LightningFrequency => lightningFrequency;
        public float LightningThickness => lightningThickness;
        public float LightningSpeed => lightningSpeed;
        public float LightningDistortion => lightningDistortion;
        public float EdgeGlowWidth => edgeGlowWidth;
        public float EdgeGlowIntensity => edgeGlowIntensity;
        public float SurfaceStreakSpeed => surfaceStreakSpeed;
        public float SurfaceLift => surfaceLift;
        public float SurfacePatternScale => surfacePatternScale;
        public float SurfacePulseSpeed => surfacePulseSpeed;
        public float TravellingPulseSpeed => travellingPulseSpeed;
        public float RageUndercoatIntensity => rageUndercoatIntensity;
        public float MovementResponse => movementResponse;
        public float AttackResponse => attackResponse;
        public float FinalSecondInstability => finalSecondInstability;
        public float MovementSpeedForFullResponse => movementSpeedForFullResponse;
        public float AttackResponseDuration => attackResponseDuration;
        public int SurfaceSparkAmount => surfaceSparkAmount;
        public int MovementStreakAmount => movementStreakAmount;
        public int AttackAccentAmount => attackAccentAmount;
        public float ActivationDuration => activationDuration;
        public float FadeInDuration => fadeInDuration;
        public float FadeOutDuration => fadeOutDuration;
        public float AuthoritativeBuffHandshakeTimeout => authoritativeBuffHandshakeTimeout;
        public float OptionalLightIntensity => optionalLightIntensity;
        public float OptionalLightDuration => optionalLightDuration;
        public bool OptionalCameraImpulse => optionalCameraImpulse;

        public void ConfigureChargedRageDefaults()
        {
            mainCrimson = new Color(1f, 0.006f, 0.012f, 1f);
            darkRed = new Color(0.18f, 0f, 0.006f, 1f);
            highlight = new Color(1f, 0.22f, 0.16f, 1f);
            activationIntensity = 7.2f;
            persistentOverlayIntensity = 4.6f;
            lightningFrequency = 4.4f;
            lightningThickness = 0.115f;
            lightningSpeed = 2.25f;
            lightningDistortion = 0.062f;
            edgeGlowWidth = 3.4f;
            edgeGlowIntensity = 3.15f;
            surfaceStreakSpeed = 1.85f;
            surfaceLift = 0.0028f;
            surfacePatternScale = 2.05f;
            surfacePulseSpeed = 4.25f;
            travellingPulseSpeed = 0.82f;
            rageUndercoatIntensity = 0.4f;
            movementResponse = 1.35f;
            attackResponse = 1.9f;
            finalSecondInstability = 1.15f;
            surfaceSparkAmount = 12;
            movementStreakAmount = 9;
            attackAccentAmount = 6;
            optionalLightIntensity = 3.4f;
        }
    }
}
