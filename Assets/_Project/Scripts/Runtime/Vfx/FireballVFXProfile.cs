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

        [Header("Particle Budgets")]
        [SerializeField, Range(0, 20)] private int emberCount = 8;
        [SerializeField, Range(0, 12)] private int smokeAmount = 4;

        [Header("Impact")]
        [SerializeField, Min(0.05f)] private float impactSize = 2.2f;
        [SerializeField, Min(0.05f)] private float shockwaveSize = 2.8f;
        [SerializeField, Min(0.05f)] private float burstDuration = 0.48f;
        [SerializeField] private bool enableScorch = true;
        [SerializeField, Min(0.05f)] private float scorchSize = 1.7f;
        [SerializeField, Min(0.05f)] private float scorchDuration = 3.5f;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float castingDuration = 0.48f;
        [SerializeField, Min(0.05f)] private float castReleaseDuration = 0.38f;
        [SerializeField, Min(0.05f)] private float aftermathDuration = 1.8f;

        [Header("Master")]
        [SerializeField, Range(0f, 2f)] private float overallIntensity = 1f;

        public float ProjectileScale => projectileScale;
        public float CoreSize => coreSize;
        public float FlameSize => flameSize;
        public float OuterShellSize => outerShellSize;
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
        public int EmberCount => emberCount;
        public int SmokeAmount => smokeAmount;
        public float ImpactSize => impactSize;
        public float ShockwaveSize => shockwaveSize;
        public float BurstDuration => burstDuration;
        public bool EnableScorch => enableScorch;
        public float ScorchSize => scorchSize;
        public float ScorchDuration => scorchDuration;
        public float CastingDuration => castingDuration;
        public float CastReleaseDuration => castReleaseDuration;
        public float AftermathDuration => aftermathDuration;
        public float OverallIntensity => overallIntensity;
    }
}
