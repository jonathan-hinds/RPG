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

        private MaterialPropertyBlock glowProperties;
        private Camera cachedCamera;
        private float transitionStartedAt;
        private float transitionStartOpacity;
        private float opacity;
        private bool playing;
        private bool releasing;
        private Vector3 chargeGlowBaseLocalScale = Vector3.one;

        private void Awake()
        {
            glowProperties = new MaterialPropertyBlock();
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
            }

            UpdateBillboard();
            UpdateGlow();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            _ = context;
            if (profile == null)
            {
                Debug.LogError("HealingBeamChargeVFX has no profile assigned.", this);
                Destroy(gameObject);
                return;
            }

            if (chargeEffectRoot != null)
            {
                chargeEffectRoot.gameObject.SetActive(true);
                chargeEffectRoot.localScale = Vector3.one * profile.CasterEffectScale;
            }

            ConfigureLoopParticle(
                orbitingStars,
                profile.CasterOrbitParticleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureLoopParticle(inwardOrbs, profile.CasterInwardParticleCount, profile.ParticleSize * 0.72f);

            playing = true;
            releasing = false;
            opacity = 0f;
            transitionStartOpacity = 0f;
            transitionStartedAt = Time.time;

            if (chargeGlow != null)
            {
                chargeGlow.transform.localScale = chargeGlowBaseLocalScale * profile.EndpointOrbSizeMultiplier;
                chargeGlow.enabled = true;
            }

            PlayOneShot(originFlash);
            PlayLooping(orbitingStars);
            PlayLooping(inwardOrbs);
            UpdateGlow();
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
        }

        public void ConfigureAuthoring(
            HealingBeamVFXProfile newProfile,
            Transform newChargeEffectRoot,
            Renderer newChargeGlow,
            ParticleSystem newOriginFlash,
            ParticleSystem newOrbitingStars,
            ParticleSystem newInwardOrbs)
        {
            profile = newProfile;
            chargeEffectRoot = newChargeEffectRoot;
            chargeGlow = newChargeGlow;
            originFlash = newOriginFlash;
            orbitingStars = newOrbitingStars;
            inwardOrbs = newInwardOrbs;
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
            float masterOpacity = Mathf.Clamp01(opacity * profile.OverallIntensity * ambientPulse);
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

            if (chargeGlow != null)
            {
                chargeGlow.enabled = false;
            }

            if (chargeEffectRoot != null)
            {
                chargeEffectRoot.gameObject.SetActive(false);
            }

            opacity = 0f;
            playing = false;
            releasing = false;
        }

        private bool AnyParticlesAlive()
        {
            return IsAlive(originFlash) || IsAlive(orbitingStars) || IsAlive(inwardOrbs);
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
    }
}
