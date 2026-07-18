using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Fireball Profile", fileName = "FireballVFXProfile")]
    public sealed class FireballVFXProfile : ScriptableObject
    {
        [Header("Projectile Shape")]
        [SerializeField, Min(0.05f)] private float projectileScale = 1f;
        [SerializeField, Min(0.02f)] private float coreSize = 0.42f;
        [SerializeField, Min(0.02f)] private float flameSize = 0.82f;
        [SerializeField, Min(0.02f)] private float outerShellSize = 1.08f;
        [SerializeField, Min(0.02f)] private float cometHeadSize = 1.18f;
        [SerializeField, Min(0.02f)] private float flameCoronaSize = 1.42f;
        [SerializeField, Min(0f)] private float directionalStretch = 0.16f;

        [Header("Trail")]
        [Tooltip("Trail lifetime in seconds. The existing projectile remains responsible for movement.")]
        [SerializeField, Min(0.02f)] private float trailLength = 0.26f;
        [SerializeField, Min(0.01f)] private float trailWidth = 0.46f;
        [SerializeField, Range(0f, 3f)] private float trailBrightness = 1.15f;

        [Header("Fire Color")]
        [SerializeField, ColorUsage(true, true)] private Color hotColor = new(1.45f, 1.12f, 0.55f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color flameColor = new(1.3f, 0.42f, 0.05f, 0.92f);
        [SerializeField, ColorUsage(true, true)] private Color outerColor = new(0.82f, 0.13f, 0.015f, 0.62f);
        [SerializeField] private Color smokeColor = new(0.22f, 0.12f, 0.085f, 0.48f);

        [Header("Procedural Motion")]
        [SerializeField, Min(0f)] private float flickerSpeed = 9f;
        [SerializeField, Range(0f, 0.25f)] private float distortionAmount = 0.065f;
        [SerializeField] private Vector2 flameScrollSpeed = new(-0.55f, 0.18f);
        [SerializeField, Min(0f)] private float coronaRotationSpeed = 74f;

        [Header("Particle Budgets")]
        [SerializeField, Range(0, 20)] private int emberCount = 8;
        [SerializeField, Range(0, 12)] private int smokeAmount = 4;

        [Header("Impact")]
        [SerializeField, Min(0.05f)] private float impactSize = 2.2f;
        [SerializeField, Min(0.05f)] private float shockwaveSize = 2.8f;
        [SerializeField, Min(0.05f)] private float impactCrownSize = 2.65f;
        [SerializeField, Min(0.05f)] private float heatRingSize = 3.5f;
        [SerializeField, Min(0.05f)] private float launchRingSize = 1.45f;
        [SerializeField, Min(0.05f)] private float burstDuration = 0.48f;
        [SerializeField, Range(0f, 0.2f)] private float impactHangTime = 0.065f;
        [SerializeField] private bool enableScorch = true;
        [SerializeField, Min(0.05f)] private float scorchSize = 1.7f;
        [SerializeField, Min(0.05f)] private float scorchDuration = 3.5f;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float castingDuration = 0.48f;
        [SerializeField, Min(0.05f)] private float castReleaseDuration = 0.38f;
        [SerializeField, Min(0.05f)] private float aftermathDuration = 1.8f;

        [Header("Master")]
        [SerializeField, Range(0f, 2f)] private float overallIntensity = 1f;
        [SerializeField, HideInInspector] private int contentVersion;

        public float ProjectileScale => projectileScale;
        public float CoreSize => coreSize;
        public float FlameSize => flameSize;
        public float OuterShellSize => outerShellSize;
        public float CometHeadSize => cometHeadSize;
        public float FlameCoronaSize => flameCoronaSize;
        public float DirectionalStretch => directionalStretch;
        public float TrailLength => trailLength;
        public float TrailWidth => trailWidth;
        public float TrailBrightness => trailBrightness;
        public Color HotColor => hotColor;
        public Color FlameColor => flameColor;
        public Color OuterColor => outerColor;
        public Color SmokeColor => smokeColor;
        public float FlickerSpeed => flickerSpeed;
        public float DistortionAmount => distortionAmount;
        public Vector2 FlameScrollSpeed => flameScrollSpeed;
        public float CoronaRotationSpeed => coronaRotationSpeed;
        public int EmberCount => emberCount;
        public int SmokeAmount => smokeAmount;
        public float ImpactSize => impactSize;
        public float ShockwaveSize => shockwaveSize;
        public float ImpactCrownSize => impactCrownSize;
        public float HeatRingSize => heatRingSize;
        public float LaunchRingSize => launchRingSize;
        public float BurstDuration => burstDuration;
        public float ImpactHangTime => impactHangTime;
        public bool EnableScorch => enableScorch;
        public float ScorchSize => scorchSize;
        public float ScorchDuration => scorchDuration;
        public float CastingDuration => castingDuration;
        public float CastReleaseDuration => castReleaseDuration;
        public float AftermathDuration => aftermathDuration;
        public float OverallIntensity => overallIntensity;

        public bool UpgradePolishDefaults()
        {
            if (contentVersion >= 2)
            {
                return false;
            }

            projectileScale = 1.1f;
            coreSize = 0.46f;
            flameSize = 0.9f;
            outerShellSize = 1.16f;
            cometHeadSize = 1.22f;
            flameCoronaSize = 1.48f;
            directionalStretch = 0.22f;
            trailLength = 0.31f;
            trailWidth = 0.54f;
            trailBrightness = 1.28f;
            hotColor = new Color(1.65f, 1.22f, 0.58f, 1f);
            flameColor = new Color(1.46f, 0.43f, 0.035f, 0.96f);
            outerColor = new Color(0.92f, 0.12f, 0.01f, 0.68f);
            smokeColor = new Color(0.23f, 0.11f, 0.07f, 0.44f);
            flickerSpeed = 10.5f;
            distortionAmount = 0.075f;
            coronaRotationSpeed = 74f;
            emberCount = 9;
            smokeAmount = 4;
            impactSize = 2.45f;
            shockwaveSize = 3.15f;
            impactCrownSize = 2.85f;
            heatRingSize = 3.75f;
            launchRingSize = 1.55f;
            burstDuration = 0.56f;
            impactHangTime = 0.065f;
            castingDuration = 0.5f;
            castReleaseDuration = 0.42f;
            aftermathDuration = 1.9f;
            overallIntensity = 1.1f;
            contentVersion = 2;
            return true;
        }
    }
}
