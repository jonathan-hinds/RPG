using RPGClone.Abilities;
using UnityEngine;

namespace RPGClone.Vfx.Healing
{
    /// <summary>
    /// Owns the caster-only charge presentation shown before Healing Beam is released.
    /// Gameplay timing remains owned by MMOAbilitySystem and MMOAbilityVfxController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealingBeamChargeVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [SerializeField] private HealingBeamVFXProfile profile;
        [SerializeField] private Transform chargeEffectRoot;
        [SerializeField] private Renderer chargeGlow;
        [SerializeField] private ParticleSystem originFlash;
        [SerializeField] private ParticleSystem orbitingStars;
        [SerializeField] private ParticleSystem inwardOrbs;
        [SerializeField] private ParticleSystem gatheringLeaves;

        [Header("Caster Ground Buildup")]
        [SerializeField] private Transform groundEffectRoot;
        [SerializeField] private Transform innerNatureRingTransform;
        [SerializeField] private Renderer innerNatureRing;
        [SerializeField] private Transform outerNatureRingTransform;
        [SerializeField] private Renderer outerNatureRing;
        [SerializeField] private ParticleSystem groundDust;

        private MaterialPropertyBlock glowProperties;
        private MaterialPropertyBlock innerRingProperties;
        private MaterialPropertyBlock outerRingProperties;
        private Camera cachedCamera;
        private Transform casterRoot;
        private float transitionStartedAt;
        private float transitionStartOpacity;
        private float opacity;
        private float chargeStartedAt;
        private float chargeDuration = 1f;
        private float chargeProgress;
        private bool playing;
        private bool releasing;
        private Vector3 chargeGlowBaseLocalScale = Vector3.one;

        public HealingBeamVFXProfile Profile => profile;

        private void Awake()
        {
            glowProperties = new MaterialPropertyBlock();
            innerRingProperties = new MaterialPropertyBlock();
            outerRingProperties = new MaterialPropertyBlock();
            CacheChargeGlowBaseScale();
            StopVisualsImmediately();
        }

        private void OnDisable()
        {
            StopVisualsImmediately();
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - transitionStartedAt;
            if (releasing)
            {
                opacity = Mathf.Lerp(transitionStartOpacity, 0f, Mathf.Clamp01(elapsed / profile.FadeOutDuration));
                if (elapsed >= profile.FadeOutDuration && !AnyParticlesAlive())
                {
                    playing = false;
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                opacity = Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / profile.FadeInDuration));
                UpdateChargeBuildup();
            }

            UpdateBillboard();
            UpdateGlow();
            UpdateGroundBuildup();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            if (profile == null)
            {
                Debug.LogError("HealingBeamChargeVFX has no profile assigned.", this);
                Destroy(gameObject);
                return;
            }

            if (chargeEffectRoot != null)
            {
                chargeEffectRoot.gameObject.SetActive(true);
                chargeEffectRoot.localScale = Vector3.one * profile.CasterEffectScale * profile.ChargeStartScale;
            }

            if (groundEffectRoot != null)
            {
                groundEffectRoot.gameObject.SetActive(true);
            }

            ConfigureLoopParticle(
                orbitingStars,
                profile.CasterOrbitParticleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureLoopParticle(inwardOrbs, profile.CasterInwardParticleCount, profile.ParticleSize * 0.72f);
            ConfigureLoopParticle(gatheringLeaves, profile.CasterLeafParticleCount, profile.LeafParticleSize);
            ConfigureGroundDust();

            playing = true;
            releasing = false;
            opacity = 0f;
            transitionStartOpacity = 0f;
            transitionStartedAt = Time.time;
            chargeStartedAt = Time.time;
            chargeDuration = Mathf.Max(0.1f, context.Ability != null ? context.Ability.CastTimeSeconds : 1f);
            chargeProgress = 0f;
            casterRoot = context.Source;

            if (chargeGlow != null)
            {
                chargeGlow.transform.localScale = chargeGlowBaseLocalScale * profile.EndpointOrbSizeMultiplier;
                chargeGlow.enabled = true;
            }

            PlayOneShot(originFlash);
            PlayLooping(orbitingStars);
            PlayLooping(inwardOrbs);
            PlayLooping(gatheringLeaves);
            PlayLooping(groundDust);

            SetRendererEnabled(innerNatureRing, true);
            SetRendererEnabled(outerNatureRing, true);
            UpdateGlow();
            UpdateGroundBuildup();
        }

        public void Release(bool immediate)
        {
            if (!playing)
            {
                Destroy(gameObject);
                return;
            }

            if (immediate)
            {
                StopVisualsImmediately();
                Destroy(gameObject);
                return;
            }

            if (releasing)
            {
                return;
            }

            releasing = true;
            transitionStartOpacity = opacity;
            transitionStartedAt = Time.time;
            StopEmitting(orbitingStars);
            StopEmitting(inwardOrbs);
            StopEmitting(gatheringLeaves);
            StopEmitting(groundDust);
        }

        public void ConfigureAuthoring(
            HealingBeamVFXProfile newProfile,
            Transform newChargeEffectRoot,
            Renderer newChargeGlow,
            ParticleSystem newOriginFlash,
            ParticleSystem newOrbitingStars,
            ParticleSystem newInwardOrbs,
            ParticleSystem newGatheringLeaves,
            Transform newGroundEffectRoot,
            Transform newInnerNatureRingTransform,
            Renderer newInnerNatureRing,
            Transform newOuterNatureRingTransform,
            Renderer newOuterNatureRing,
            ParticleSystem newGroundDust)
        {
            profile = newProfile;
            chargeEffectRoot = newChargeEffectRoot;
            chargeGlow = newChargeGlow;
            originFlash = newOriginFlash;
            orbitingStars = newOrbitingStars;
            inwardOrbs = newInwardOrbs;
            gatheringLeaves = newGatheringLeaves;
            groundEffectRoot = newGroundEffectRoot;
            innerNatureRingTransform = newInnerNatureRingTransform;
            innerNatureRing = newInnerNatureRing;
            outerNatureRingTransform = newOuterNatureRingTransform;
            outerNatureRing = newOuterNatureRing;
            groundDust = newGroundDust;
            CacheChargeGlowBaseScale();
        }

        private void UpdateBillboard()
        {
            if (chargeGlow == null)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera != null)
            {
                chargeGlow.transform.rotation = Quaternion.LookRotation(cachedCamera.transform.forward, cachedCamera.transform.up);
            }
        }

        private void CacheChargeGlowBaseScale()
        {
            if (chargeGlow != null)
            {
                chargeGlowBaseLocalScale = chargeGlow.transform.localScale;
            }
        }

        private void UpdateGlow()
        {
            if (chargeGlow == null)
            {
                return;
            }

            float ambientPulse = 0.92f + (Mathf.Sin(Time.time * 2.1f) * 0.08f);
            float buildupIntensity = Mathf.Lerp(0.58f, 1.18f, chargeProgress);
            float masterOpacity = Mathf.Clamp01(opacity * profile.OverallIntensity * ambientPulse * buildupIntensity);
            Color tint = new(1f, 0.78f, 0.28f, 0.48f * masterOpacity);
            tint.r *= profile.GlowIntensity;
            tint.g *= profile.GlowIntensity;
            tint.b *= profile.GlowIntensity;
            glowProperties.SetColor(TintId, tint);
            glowProperties.SetFloat(OpacityId, masterOpacity);
            chargeGlow.SetPropertyBlock(glowProperties);
        }

        private void StopVisualsImmediately()
        {
            StopAndClear(originFlash);
            StopAndClear(orbitingStars);
            StopAndClear(inwardOrbs);
            StopAndClear(gatheringLeaves);
            StopAndClear(groundDust);

            if (chargeGlow != null)
            {
                chargeGlow.enabled = false;
            }

            if (chargeEffectRoot != null)
            {
                chargeEffectRoot.gameObject.SetActive(false);
            }

            SetRendererEnabled(innerNatureRing, false);
            SetRendererEnabled(outerNatureRing, false);
            if (groundEffectRoot != null)
            {
                groundEffectRoot.gameObject.SetActive(false);
            }

            opacity = 0f;
            playing = false;
            releasing = false;
        }

        private bool AnyParticlesAlive()
        {
            return IsAlive(originFlash)
                || IsAlive(orbitingStars)
                || IsAlive(inwardOrbs)
                || IsAlive(gatheringLeaves)
                || IsAlive(groundDust);
        }

        private void UpdateChargeBuildup()
        {
            chargeProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - chargeStartedAt) / chargeDuration));
            float pulse = 1f + (Mathf.Sin(Time.time * Mathf.Lerp(3.5f, 7f, chargeProgress))
                * profile.ChargePulseAmount
                * chargeProgress);

            if (chargeEffectRoot != null)
            {
                float buildupScale = Mathf.Lerp(profile.ChargeStartScale, 1f, chargeProgress);
                chargeEffectRoot.localScale = Vector3.one * profile.CasterEffectScale * buildupScale * pulse;
            }

            SetEmissionRate(orbitingStars, profile.CasterOrbitParticleCount * Mathf.Lerp(0.28f, 0.82f, chargeProgress));
            SetEmissionRate(inwardOrbs, profile.CasterInwardParticleCount * Mathf.Lerp(0.22f, 0.78f, chargeProgress));
            SetEmissionRate(gatheringLeaves, profile.CasterLeafParticleCount * Mathf.Lerp(0.12f, 0.58f, chargeProgress));
            SetOrbitalSpeed(orbitingStars, 1.2f * Mathf.Lerp(0.7f, profile.ChargeOrbitSpeedMultiplier, chargeProgress));
            SetOrbitalSpeed(inwardOrbs, 0.7f * Mathf.Lerp(0.7f, profile.ChargeOrbitSpeedMultiplier, chargeProgress));
            SetOrbitalSpeed(gatheringLeaves, 0.85f * Mathf.Lerp(0.65f, profile.ChargeOrbitSpeedMultiplier, chargeProgress));
        }

        private void UpdateGroundBuildup()
        {
            if (groundEffectRoot == null)
            {
                return;
            }

            Transform followTarget = casterRoot != null ? casterRoot : transform;
            groundEffectRoot.position = followTarget.position + (Vector3.up * profile.CasterGroundVerticalOffset);
            groundEffectRoot.rotation = Quaternion.identity;

            float buildup = Mathf.SmoothStep(0f, 1f, chargeProgress);
            float groundOpacity = opacity * profile.OverallIntensity * profile.CasterGroundRingOpacity * Mathf.Lerp(0.35f, 1f, buildup);
            float pulse = 1f + (Mathf.Sin(Time.time * 3.2f) * 0.035f * buildup);
            float baseSize = profile.CasterGroundRingSize;
            float ringTravel = Mathf.Max(0.25f, profile.CasterBuildupCylinderHeight);
            float innerPhase = Mathf.Repeat((Time.time - chargeStartedAt) * profile.CasterRingRiseSpeed, 1f);
            float outerPhase = Mathf.Repeat(innerPhase + 0.5f, 1f);
            float innerFade = Mathf.Sin(innerPhase * Mathf.PI);
            float outerFade = Mathf.Sin(outerPhase * Mathf.PI);

            if (innerNatureRingTransform != null)
            {
                float innerSize = baseSize * Mathf.Lerp(0.68f, 0.82f, buildup) * Mathf.Lerp(0.94f, 1.06f, innerPhase) * pulse;
                innerNatureRingTransform.localPosition = Vector3.up * innerPhase * ringTravel;
                innerNatureRingTransform.localScale = Vector3.one * innerSize;
                innerNatureRingTransform.localRotation = Quaternion.Euler(90f, 0f, Time.time * 18f);
            }

            if (outerNatureRingTransform != null)
            {
                float outerSize = baseSize * Mathf.Lerp(0.82f, 1f, buildup) * Mathf.Lerp(0.92f, 1.04f, outerPhase) / pulse;
                outerNatureRingTransform.localPosition = Vector3.up * outerPhase * ringTravel;
                outerNatureRingTransform.localScale = Vector3.one * outerSize;
                outerNatureRingTransform.localRotation = Quaternion.Euler(90f, 0f, Time.time * -11f);
            }

            ApplyRingProperties(
                innerNatureRing,
                innerRingProperties,
                new Color(0.68f, 1f, 0.38f, 0.92f),
                groundOpacity * innerFade);
            ApplyRingProperties(
                outerNatureRing,
                outerRingProperties,
                new Color(1f, 0.8f, 0.28f, 0.82f),
                groundOpacity * outerFade * 0.85f);

            if (!releasing)
            {
                SetEmissionRate(groundDust, profile.CasterDustParticleCount * Mathf.Lerp(0.55f, 1.1f, buildup));
            }
        }

        private void ConfigureGroundDust()
        {
            if (groundDust == null)
            {
                return;
            }

            ParticleSystem.MainModule main = groundDust.main;
            main.maxParticles = Mathf.Max(1, profile.CasterDustParticleCount);
            main.startSize = profile.CasterDustParticleSize;
            main.startLifetime = Mathf.Max(0.35f, profile.CasterBuildupCylinderHeight / profile.CasterDustRiseSpeed);
            ParticleSystem.ShapeModule shape = groundDust.shape;
            shape.enabled = true;
            shape.radius = profile.CasterDustRingRadius;
            shape.radiusThickness = 0.05f;
            ParticleSystem.VelocityOverLifetimeModule velocity = groundDust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(
                profile.CasterDustRiseSpeed * 0.82f,
                profile.CasterDustRiseSpeed * 1.18f);
        }

        private static void ApplyRingProperties(
            Renderer ring,
            MaterialPropertyBlock properties,
            Color tint,
            float alpha)
        {
            if (ring == null)
            {
                return;
            }

            Color color = tint;
            color.a *= Mathf.Clamp01(alpha);
            properties.SetColor(TintId, color);
            properties.SetFloat(OpacityId, Mathf.Clamp01(alpha));
            ring.SetPropertyBlock(properties);
        }

        private static bool IsAlive(ParticleSystem particleSystem)
        {
            return particleSystem != null && particleSystem.IsAlive(true);
        }

        private static void ConfigureLoopParticle(ParticleSystem particleSystem, int count, float size)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.maxParticles = Mathf.Max(1, count);
            main.startSize = size;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = Mathf.Max(0f, count * 0.7f);
            emission.enabled = count > 0;
        }

        private static void SetEmissionRate(ParticleSystem particleSystem, float rate)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
            emission.enabled = rate > 0f;
        }

        private static void SetOrbitalSpeed(ParticleSystem particleSystem, float speed)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = speed;
        }

        private static void PlayOneShot(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }

        private static void PlayLooping(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        private static void StopEmitting(ParticleSystem particleSystem)
        {
            particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void StopAndClear(ParticleSystem particleSystem)
        {
            particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void SetRendererEnabled(Renderer targetRenderer, bool enabled)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = enabled;
            }
        }
    }
}
