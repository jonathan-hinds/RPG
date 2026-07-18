using System;
using UnityEngine;

namespace RPGClone.Vfx.Arcane
{
    [DisallowMultipleComponent]
    public sealed class MageArmorApplyVFX : MonoBehaviour, IMageArmorApplyVFX, IMMOAbilityVfxInstance
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [Header("Configuration")]
        [SerializeField] private MageArmorVFXProfile profile;

        [Header("Visual Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform shellRoot;
        [SerializeField] private Renderer shellRenderer;

        [Header("Application Layers")]
        [SerializeField] private ParticleSystem centralFlash;
        [SerializeField] private ParticleSystem facetShield;
        [SerializeField] private ParticleSystem facetWing;
        [SerializeField] private ParticleSystem facetKite;
        [SerializeField] private ParticleSystem risingRingPrimary;
        [SerializeField] private ParticleSystem risingRingSecondary;
        [SerializeField] private ParticleSystem overheadFocus;
        [SerializeField] private ParticleSystem sparkles;
        [SerializeField] private ParticleSystem outwardParticles;
        [SerializeField] private ParticleSystem inwardParticles;

        private MaterialPropertyBlock shellProperties;
        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private Transform followAttachment;
        private Quaternion shellBaseRotation;
        private bool isPlaying;
        private float startedAt;

        public event Action<MageArmorApplyVFX> Completed;

        public bool IsPlaying => isPlaying;
        public bool ReadyForPool => !isPlaying;
        public MageArmorVFXProfile Profile => profile;

        private void Awake()
        {
            shellProperties = new MaterialPropertyBlock();
            CacheParticles();
            if (shellRoot != null)
            {
                shellBaseRotation = shellRoot.localRotation;
            }

            StopImmediateInternal(false);
        }

        private void OnDisable()
        {
            StopImmediateInternal(false);
        }

        private void LateUpdate()
        {
            if (!isPlaying || profile == null)
            {
                return;
            }

            if (followAttachment != null)
            {
                transform.SetPositionAndRotation(followAttachment.position, followAttachment.rotation);
            }

            float elapsed = Time.time - startedAt;
            float normalized = Mathf.Clamp01(elapsed / profile.EffectDuration);
            AnimateShell(normalized, elapsed);
            if (elapsed >= profile.EffectDuration)
            {
                CompletePlayback();
            }
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            Play(context.Source);
        }

        public void Play(Transform caster, Transform torsoAttachment = null)
        {
            if (!ValidateProfile())
            {
                return;
            }

            StopImmediateInternal(false);
            followAttachment = torsoAttachment;
            if (torsoAttachment != null)
            {
                transform.SetPositionAndRotation(torsoAttachment.position, caster != null ? caster.rotation : torsoAttachment.rotation);
            }
            else if (caster != null && transform.parent == null)
            {
                transform.rotation = caster.rotation;
            }

            ApplyProfile();
            isPlaying = true;
            startedAt = Time.time;
            if (shellRenderer != null)
            {
                shellRenderer.enabled = true;
            }

            PlayOneShot(centralFlash);
            PlayOneShot(facetShield);
            PlayOneShot(facetWing);
            PlayOneShot(facetKite);
            PlayOneShot(risingRingPrimary);
            PlayOneShot(risingRingSecondary);
            PlayOneShot(overheadFocus);
            PlayOneShot(sparkles);
            PlayOneShot(outwardParticles);
            PlayOneShot(inwardParticles);
        }

        public void StopImmediate()
        {
            StopImmediateInternal(true);
        }

        public void ResetForPool()
        {
            StopImmediateInternal(false);
            followAttachment = null;
        }

        public void ConfigureAuthoring(
            MageArmorVFXProfile newProfile,
            Transform newVisualRoot,
            Transform newShellRoot,
            Renderer newShellRenderer,
            ParticleSystem newCentralFlash,
            ParticleSystem newFacetShield,
            ParticleSystem newFacetWing,
            ParticleSystem newFacetKite,
            ParticleSystem newRisingRingPrimary,
            ParticleSystem newRisingRingSecondary,
            ParticleSystem newOverheadFocus,
            ParticleSystem newSparkles,
            ParticleSystem newOutwardParticles,
            ParticleSystem newInwardParticles)
        {
            profile = newProfile;
            visualRoot = newVisualRoot;
            shellRoot = newShellRoot;
            shellRenderer = newShellRenderer;
            centralFlash = newCentralFlash;
            facetShield = newFacetShield;
            facetWing = newFacetWing;
            facetKite = newFacetKite;
            risingRingPrimary = newRisingRingPrimary;
            risingRingSecondary = newRisingRingSecondary;
            overheadFocus = newOverheadFocus;
            sparkles = newSparkles;
            outwardParticles = newOutwardParticles;
            inwardParticles = newInwardParticles;
            CacheParticles(true);
        }

        private bool ValidateProfile()
        {
            if (profile != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(MageArmorApplyVFX)} on '{name}' has no profile assigned.", this);
            return false;
        }

        private void ApplyProfile()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * profile.OverallScale;
            }

            float duration = profile.EffectDuration;
            ConfigureBurst(centralFlash, 1, duration * 0.3f, duration * 0.02f, 0f, profile.CentralFlashSize, Tint(profile.BrightWhite, profile.CentralFlashBrightness));

            int firstFacetCount = (profile.FacetCount + 2) / 3;
            int secondFacetCount = (profile.FacetCount + 1) / 3;
            int thirdFacetCount = profile.FacetCount / 3;
            ConfigureBurst(facetShield, firstFacetCount, duration * 0.4f, duration * 0.14f, -0.42f, profile.FacetSize, Tint(profile.PaleCyan));
            ConfigureBurst(facetWing, secondFacetCount, duration * 0.42f, duration * 0.17f, -0.38f, profile.FacetSize * 0.9f, Tint(profile.Lavender));
            ConfigureBurst(facetKite, thirdFacetCount, duration * 0.38f, duration * 0.2f, -0.46f, profile.FacetSize * 0.78f, Tint(profile.LightBlue));

            ConfigureBurst(risingRingPrimary, profile.RingCount >= 1 ? 1 : 0, duration * 0.48f, duration * 0.14f, 0f, profile.RingWidth, Tint(profile.PaleCyan));
            ConfigureBurst(risingRingSecondary, profile.RingCount >= 2 ? 1 : 0, duration * 0.44f, duration * 0.27f, 0f, profile.RingWidth * 1.08f, Tint(profile.Violet));
            SetVerticalVelocity(risingRingPrimary, profile.RingRiseSpeed);
            SetVerticalVelocity(risingRingSecondary, profile.RingRiseSpeed * 0.9f);

            ConfigureBurst(overheadFocus, 1, duration * 0.38f, duration * 0.24f, 0f, profile.OverheadFocusSize, Tint(profile.Lavender, profile.PulseIntensity));
            ConfigureBurst(sparkles, profile.SparkleCount, duration * 0.34f, duration * 0.43f, 0.34f, 0.2f, Tint(profile.BrightWhite, profile.PulseIntensity));

            int outwardCount = Mathf.CeilToInt(profile.ParticleCount * 0.7f);
            int inwardCount = Mathf.Max(0, profile.ParticleCount - outwardCount);
            ConfigureBurst(outwardParticles, outwardCount, duration * 0.52f, duration * 0.03f, 1.15f, 0.12f, Tint(profile.LightBlue));
            ConfigureBurst(inwardParticles, inwardCount, duration * 0.3f, duration * 0.62f, -2.35f, 0.1f, Tint(profile.Lavender, profile.PulseIntensity));

            ConfigureShapeRadius(facetShield, profile.ShellSize * 0.58f);
            ConfigureShapeRadius(facetWing, profile.ShellSize * 0.62f);
            ConfigureShapeRadius(facetKite, profile.ShellSize * 0.54f);
            ConfigureShapeRadius(sparkles, profile.ShellSize * 0.54f);
            ConfigureShapeRadius(outwardParticles, profile.ShellSize * 0.2f);
            ConfigureShapeRadius(inwardParticles, profile.ShellSize * 0.72f);

            if (shellRoot != null)
            {
                shellRoot.localRotation = shellBaseRotation;
                shellRoot.localScale = ShellScale(1.08f);
            }

            ApplyShellProperties(0f, 0f, Vector2.zero);
        }

        private void AnimateShell(float normalized, float elapsed)
        {
            if (shellRoot == null || shellRenderer == null)
            {
                return;
            }

            float formation = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.28f, normalized));
            float dissolve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.92f, normalized));
            float pulse = 1f + Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.26f, 0.58f, normalized)) * Mathf.PI) * 0.08f * profile.PulseIntensity;
            float contraction = Mathf.Lerp(1f, 0.76f, dissolve);
            shellRoot.localScale = ShellScale(Mathf.Lerp(1.08f, 1f, formation) * pulse * contraction);
            shellRoot.Rotate(Vector3.up, (38f + profile.ShellDistortion * 180f) * Time.deltaTime, Space.Self);

            float opacity = formation * (1f - dissolve) * profile.ShellOpacity;
            Vector2 scroll = new(elapsed * (0.22f + profile.ShellDistortion), elapsed * -0.14f);
            ApplyShellProperties(opacity, dissolve, scroll);
        }

        private Vector3 ShellScale(float multiplier)
        {
            float radius = profile.ShellSize * multiplier;
            return new Vector3(radius, radius * 1.55f, radius);
        }

        private void ApplyShellProperties(float opacity, float dissolve, Vector2 scroll)
        {
            if (shellRenderer == null)
            {
                return;
            }

            shellProperties ??= new MaterialPropertyBlock();
            shellRenderer.GetPropertyBlock(shellProperties);
            shellProperties.SetColor(TintId, Tint(profile.ShellColor));
            shellProperties.SetFloat(OpacityId, opacity);
            shellProperties.SetFloat(DistortionId, profile.ShellDistortion);
            shellProperties.SetFloat(DissolveId, dissolve);
            shellProperties.SetVector(ScrollId, scroll);
            shellRenderer.SetPropertyBlock(shellProperties);
        }

        private Color Tint(Color color, float layerBrightness = 1f)
        {
            float brightness = profile.OverallBrightness * layerBrightness;
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }

        private static void ConfigureBurst(ParticleSystem system, int count, float lifetime, float delay, float speed, float size, Color color)
        {
            if (system == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 0, short.MaxValue);
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.startDelay = Mathf.Max(0f, delay);
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSpeed = speed;
            main.startSize = safeCount > 1
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, size * 0.72f), Mathf.Max(0.01f, size * 1.18f))
                : new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, size));
            main.startRotation = safeCount > 1
                ? new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI)
                : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, safeCount);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = safeCount > 0;
            emission.rateOverTime = 0f;
            emission.SetBursts(safeCount > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)safeCount) }
                : Array.Empty<ParticleSystem.Burst>());
        }

        private static void ConfigureShapeRadius(ParticleSystem system, float radius)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.radius = Mathf.Max(0.01f, radius);
        }

        private static void SetVerticalVelocity(ParticleSystem system, float speed)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = speed;
        }

        private static void PlayOneShot(ParticleSystem system)
        {
            if (system == null || !system.emission.enabled)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
        }

        private void CompletePlayback()
        {
            StopImmediateInternal(false);
            Completed?.Invoke(this);
        }

        private void StopImmediateInternal(bool notify)
        {
            CacheParticles();
            foreach (ParticleSystem system in allParticles)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            isPlaying = false;
            if (shellRenderer != null)
            {
                shellRenderer.enabled = false;
                shellRenderer.SetPropertyBlock(null);
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
            }

            if (shellRoot != null)
            {
                shellRoot.localRotation = shellBaseRotation;
                shellRoot.localScale = Vector3.one;
            }

            if (notify)
            {
                Completed?.Invoke(this);
            }
        }

        private void CacheParticles(bool force = false)
        {
            if (force || allParticles.Length == 0)
            {
                allParticles = GetComponentsInChildren<ParticleSystem>(true);
            }
        }
    }
}
