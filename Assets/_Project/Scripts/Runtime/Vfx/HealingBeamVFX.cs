using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Vfx.Healing
{
    [DisallowMultipleComponent]
    public sealed class HealingBeamVFX : MonoBehaviour, IHealingBeamVFX
    {
        private enum PlaybackState
        {
            Stopped,
            FadingIn,
            Playing,
            FadingOut
        }

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");
        private static readonly int TilingId = Shader.PropertyToID("_Tiling");
        private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int PulseProgressId = Shader.PropertyToID("_PulseProgress");
        private static readonly int PulseWidthId = Shader.PropertyToID("_PulseWidth");
        private static readonly int PulseBrightnessId = Shader.PropertyToID("_PulseBrightness");

        [Header("Configuration")]
        [SerializeField] private HealingBeamVFXProfile profile;

        [Header("Beam Effect")]
        [SerializeField] private Transform beamEffectRoot;
        [SerializeField] private LineRenderer outerGlow;
        [SerializeField] private LineRenderer flowingRibbon;
        [SerializeField] private LineRenderer innerCore;

        [Header("Caster Effect")]
        [SerializeField] private Transform casterEffectRoot;
        [SerializeField] private Renderer casterGlow;
        [SerializeField] private ParticleSystem casterOriginFlash;
        [SerializeField] private ParticleSystem casterOrbitingStars;
        [SerializeField] private ParticleSystem casterInwardOrbs;

        [Header("Target Effect")]
        [SerializeField] private Transform targetEffectRoot;
        [SerializeField] private Renderer targetGlow;
        [SerializeField] private Transform groundRingTransform;
        [SerializeField] private Renderer groundRing;
        [SerializeField] private ParticleSystem targetRisingOrbs;
        [SerializeField] private ParticleSystem targetSparkles;

        [Header("Heal-Tick Burst Effect")]
        [SerializeField] private Transform healTickEffectRoot;
        [SerializeField] private ParticleSystem targetBurst;
        [SerializeField] private ParticleSystem tickSparks;

        private MaterialPropertyBlock outerGlowProperties;
        private MaterialPropertyBlock ribbonProperties;
        private MaterialPropertyBlock coreProperties;
        private MaterialPropertyBlock casterGlowProperties;
        private MaterialPropertyBlock targetGlowProperties;
        private MaterialPropertyBlock groundRingProperties;

        private Transform casterAttachment;
        private Transform targetAttachment;
        private ParticleSystem[] loopingParticles;
        private ParticleSystem[] allParticles;
        private Vector3[] beamPositions = Array.Empty<Vector3>();
        private PlaybackState state;
        private Camera cachedCamera;
        private float stateStartedAt;
        private float stateStartOpacity;
        private float opacity;
        private float pulseStartedAt;
        private float tickFlashStartedAt = float.NegativeInfinity;
        private bool pulseActive;
        private Vector3 casterGlowBaseLocalScale = Vector3.one;

        public event Action<HealingBeamVFX> Completed;

        public bool IsPlaying => state is PlaybackState.FadingIn or PlaybackState.Playing;
        public bool ReadyForPool => state == PlaybackState.Stopped;
        public HealingBeamVFXProfile Profile => profile;

        private void Awake()
        {
            CreatePropertyBlocks();
            CacheParticleSystems();
            CacheCasterGlowBaseScale();
            HidePersistentRenderers();
            state = PlaybackState.Stopped;
        }

        private void OnDisable()
        {
            StopImmediateInternal(false);
        }

        private void LateUpdate()
        {
            if (state == PlaybackState.Stopped || profile == null)
            {
                return;
            }

            UpdateAttachmentPositions();
            UpdatePlaybackState();
            UpdateBeam();
            UpdateBillboards();
            UpdatePersistentVisuals();
        }

        public void SetAttachmentPoints(Transform newCasterAttachment, Transform newTargetAttachment)
        {
            casterAttachment = newCasterAttachment;
            targetAttachment = newTargetAttachment;
        }

        public void Play(Transform newCasterAttachment, Transform newTargetAttachment)
        {
            SetAttachmentPoints(newCasterAttachment, newTargetAttachment);
            Play();
        }

        public void Play()
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(HealingBeamVFX)} on '{name}' has no profile assigned.", this);
                return;
            }

            if (casterAttachment == null || targetAttachment == null)
            {
                Debug.LogWarning($"{nameof(HealingBeamVFX)} requires both caster and target attachment points before Play().", this);
                return;
            }

            StopImmediateInternal(false);
            CacheParticleSystems();
            ApplyProfileSettings();
            SetVisualRootsActive(true);
            ShowPersistentRenderers();
            UpdateAttachmentPositions();

            opacity = 0f;
            stateStartOpacity = 0f;
            stateStartedAt = Time.time;
            state = PlaybackState.FadingIn;
            pulseActive = false;
            tickFlashStartedAt = float.NegativeInfinity;

            PlayLoopingParticles();
            PlayOneShot(casterOriginFlash);
            UpdateBeam();
            UpdatePersistentVisuals();
        }

        public void TriggerHealingTick()
        {
            if (!IsPlaying)
            {
                return;
            }

            pulseStartedAt = Time.time;
            tickFlashStartedAt = Time.time;
            pulseActive = true;
            PlayOneShot(targetBurst);
            PlayOneShot(tickSparks);
        }

        public void Stop()
        {
            if (state is PlaybackState.Stopped or PlaybackState.FadingOut)
            {
                return;
            }

            stateStartOpacity = opacity;
            stateStartedAt = Time.time;
            state = PlaybackState.FadingOut;
            pulseActive = false;
            StopLoopingEmission();
        }

        public void StopImmediate()
        {
            StopImmediateInternal(true);
        }

        public void ResetForPool()
        {
            StopImmediateInternal(false);
            casterAttachment = null;
            targetAttachment = null;
        }

        public void ConfigureAuthoring(
            HealingBeamVFXProfile newProfile,
            Transform newBeamEffectRoot,
            LineRenderer newOuterGlow,
            LineRenderer newFlowingRibbon,
            LineRenderer newInnerCore,
            Transform newCasterEffectRoot,
            Renderer newCasterGlow,
            ParticleSystem newCasterOriginFlash,
            ParticleSystem newCasterOrbitingStars,
            ParticleSystem newCasterInwardOrbs,
            Transform newTargetEffectRoot,
            Renderer newTargetGlow,
            Transform newGroundRingTransform,
            Renderer newGroundRing,
            ParticleSystem newTargetRisingOrbs,
            ParticleSystem newTargetSparkles,
            Transform newHealTickEffectRoot,
            ParticleSystem newTargetBurst,
            ParticleSystem newTickSparks)
        {
            profile = newProfile;
            beamEffectRoot = newBeamEffectRoot;
            outerGlow = newOuterGlow;
            flowingRibbon = newFlowingRibbon;
            innerCore = newInnerCore;
            casterEffectRoot = newCasterEffectRoot;
            casterGlow = newCasterGlow;
            casterOriginFlash = newCasterOriginFlash;
            casterOrbitingStars = newCasterOrbitingStars;
            casterInwardOrbs = newCasterInwardOrbs;
            targetEffectRoot = newTargetEffectRoot;
            targetGlow = newTargetGlow;
            groundRingTransform = newGroundRingTransform;
            groundRing = newGroundRing;
            targetRisingOrbs = newTargetRisingOrbs;
            targetSparkles = newTargetSparkles;
            healTickEffectRoot = newHealTickEffectRoot;
            targetBurst = newTargetBurst;
            tickSparks = newTickSparks;
            CacheCasterGlowBaseScale();
            CacheParticleSystems();
        }

        private void UpdatePlaybackState()
        {
            float elapsed = Time.time - stateStartedAt;
            switch (state)
            {
                case PlaybackState.FadingIn:
                    opacity = Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / profile.FadeInDuration));
                    if (elapsed >= profile.FadeInDuration)
                    {
                        opacity = 1f;
                        state = PlaybackState.Playing;
                    }
                    break;

                case PlaybackState.Playing:
                    opacity = 1f;
                    break;

                case PlaybackState.FadingOut:
                    opacity = Mathf.Lerp(stateStartOpacity, 0f, Mathf.Clamp01(elapsed / profile.FadeOutDuration));
                    if (elapsed >= profile.FadeOutDuration)
                    {
                        opacity = 0f;
                        HidePersistentRenderers();
                        if (!AnyParticlesAlive())
                        {
                            CompletePlayback();
                        }
                    }
                    break;
            }
        }

        private void UpdateAttachmentPositions()
        {
            if (casterAttachment != null && casterEffectRoot != null)
            {
                casterEffectRoot.SetPositionAndRotation(casterAttachment.position, casterAttachment.rotation);
            }

            if (targetAttachment != null)
            {
                if (targetEffectRoot != null)
                {
                    targetEffectRoot.SetPositionAndRotation(targetAttachment.position, Quaternion.identity);
                }

                if (healTickEffectRoot != null)
                {
                    healTickEffectRoot.SetPositionAndRotation(targetAttachment.position, Quaternion.identity);
                }
            }
        }

        private void UpdateBeam()
        {
            if (casterAttachment == null || targetAttachment == null)
            {
                return;
            }

            int segmentCount = Mathf.Max(2, profile.BeamSegments);
            if (beamPositions.Length != segmentCount)
            {
                beamPositions = new Vector3[segmentCount];
            }

            Vector3 start = casterAttachment.position;
            Vector3 end = targetAttachment.position;
            Vector3 delta = end - start;
            Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(Vector3.forward, direction);
            }
            side.Normalize();
            Vector3 up = Vector3.Cross(direction, side).normalized;

            float phase = Time.time * profile.BeamSwaySpeed;
            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / (float)(segmentCount - 1);
                float endpointEnvelope = Mathf.Sin(t * Mathf.PI);
                float primaryWave = Mathf.Sin((t * Mathf.PI * 2.25f) + phase) * profile.BeamSway;
                float secondaryWave = Mathf.Sin((t * Mathf.PI * 3.5f) - (phase * 0.73f)) * profile.BeamSway * 0.34f;
                float arc = Mathf.Sin(t * Mathf.PI) * profile.BeamArcHeight;
                beamPositions[i] = Vector3.LerpUnclamped(start, end, t)
                    + side * primaryWave * endpointEnvelope
                    + up * ((secondaryWave * endpointEnvelope) + arc);
            }

            float width = profile.BeamWidth * profile.OverallIntensity;
            float distanceTiling = Mathf.Max(1f, delta.magnitude / profile.TextureWorldLength);
            ConfigureLine(outerGlow, width * 2.35f, beamPositions);
            ConfigureLine(flowingRibbon, width, beamPositions);
            ConfigureLine(innerCore, width * 0.34f, beamPositions);

            float pulseProgress = -10f;
            if (pulseActive)
            {
                pulseProgress = (Time.time - pulseStartedAt) * profile.PulseSpeed;
                if (pulseProgress > 1f + profile.PulseWidth)
                {
                    pulseActive = false;
                    pulseProgress = -10f;
                }
            }

            ApplyBeamProperties(outerGlow, outerGlowProperties, profile.OuterGlowColor, profile.GlowFlowSpeed, distanceTiling, pulseProgress, 0.35f);
            ApplyBeamProperties(flowingRibbon, ribbonProperties, profile.RibbonColor, profile.RibbonFlowSpeed, distanceTiling, pulseProgress, 0.7f);
            ApplyBeamProperties(innerCore, coreProperties, profile.CoreColor, profile.CoreFlowSpeed, distanceTiling, pulseProgress, 1f);
        }

        private void ApplyBeamProperties(LineRenderer line, MaterialPropertyBlock properties, Color tint, float flowSpeed, float tiling, float pulseProgress, float layerPulseScale)
        {
            if (line == null)
            {
                return;
            }

            Color intensityTint = tint;
            intensityTint.r *= profile.GlowIntensity * profile.OverallIntensity;
            intensityTint.g *= profile.GlowIntensity * profile.OverallIntensity;
            intensityTint.b *= profile.GlowIntensity * profile.OverallIntensity;
            properties.SetColor(TintId, intensityTint);
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(ScrollOffsetId, Time.time * flowSpeed);
            properties.SetFloat(TilingId, tiling);
            properties.SetFloat(DistortionStrengthId, profile.DistortionStrength);
            properties.SetFloat(PulseProgressId, pulseProgress);
            properties.SetFloat(PulseWidthId, profile.PulseWidth);
            properties.SetFloat(PulseBrightnessId, profile.PulseBrightness * layerPulseScale);
            line.SetPropertyBlock(properties);
        }

        private void UpdatePersistentVisuals()
        {
            float tickT = Mathf.Clamp01((Time.time - tickFlashStartedAt) / profile.TickFlashDuration);
            float tickFlash = tickT < 1f ? 1f - tickT : 0f;
            float ambientPulse = 0.92f + (Mathf.Sin(Time.time * 2.1f) * 0.08f);
            float targetPulse = ambientPulse + (tickFlash * 0.75f);
            float masterOpacity = opacity * profile.OverallIntensity;

            ApplySpriteProperties(casterGlow, casterGlowProperties, new Color(1f, 0.78f, 0.28f, 0.48f), masterOpacity * ambientPulse);
            ApplySpriteProperties(targetGlow, targetGlowProperties, new Color(1f, 0.84f, 0.38f, 0.4f), masterOpacity * targetPulse);
            ApplySpriteProperties(groundRing, groundRingProperties, new Color(1f, 0.72f, 0.2f, 0.34f), masterOpacity * targetPulse);

            if (casterEffectRoot != null)
            {
                casterEffectRoot.localScale = Vector3.one * profile.CasterEffectScale;
            }

            if (casterGlow != null)
            {
                casterGlow.transform.localScale = casterGlowBaseLocalScale * profile.EndpointOrbSizeMultiplier;
            }

            if (targetGlow != null)
            {
                targetGlow.transform.localScale = casterGlowBaseLocalScale * profile.EndpointOrbSizeMultiplier;
            }

            if (targetEffectRoot != null)
            {
                targetEffectRoot.localScale = Vector3.one * profile.TargetEffectScale;
            }

            if (groundRingTransform != null)
            {
                float ringScale = profile.GroundRingSize * (1f + Mathf.Sin(Time.time * 1.6f) * 0.035f);
                groundRingTransform.localPosition = new Vector3(0f, profile.GroundRingVerticalOffset, 0f);
                groundRingTransform.localScale = new Vector3(ringScale, ringScale, ringScale);
                groundRingTransform.localRotation = Quaternion.Euler(90f, 0f, Time.time * 8f);
            }
        }

        private static void ApplySpriteProperties(Renderer targetRenderer, MaterialPropertyBlock properties, Color tint, float alpha)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color color = tint;
            color.a *= Mathf.Clamp01(alpha);
            properties.SetColor(TintId, color);
            properties.SetFloat(OpacityId, Mathf.Clamp01(alpha));
            targetRenderer.SetPropertyBlock(properties);
        }

        private void UpdateBillboards()
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return;
            }

            Billboard(casterGlow != null ? casterGlow.transform : null, cachedCamera.transform);
            Billboard(targetGlow != null ? targetGlow.transform : null, cachedCamera.transform);
        }

        private static void Billboard(Transform visual, Transform cameraTransform)
        {
            if (visual == null)
            {
                return;
            }

            visual.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
        }

        private void ApplyProfileSettings()
        {
            ConfigureLoopParticle(
                casterOrbitingStars,
                profile.CasterOrbitParticleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureLoopParticle(casterInwardOrbs, profile.CasterInwardParticleCount, profile.ParticleSize * 0.72f);
            ConfigureLoopParticle(targetRisingOrbs, profile.TargetRisingParticleCount, profile.ParticleSize * 0.72f);
            ConfigureLoopParticle(
                targetSparkles,
                profile.TargetSparkleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureBurstParticle(tickSparks, profile.TickSparkCount, profile.ParticleSize * 1.1f);
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

        private static void ConfigureBurstParticle(ParticleSystem particleSystem, int count, float size)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.maxParticles = Mathf.Max(1, count);
            main.startSize = size;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
        }

        private void CacheParticleSystems()
        {
            loopingParticles = new[] { casterOrbitingStars, casterInwardOrbs, targetRisingOrbs, targetSparkles };
            allParticles = new[] { casterOriginFlash, casterOrbitingStars, casterInwardOrbs, targetRisingOrbs, targetSparkles, targetBurst, tickSparks };
        }

        private void CacheCasterGlowBaseScale()
        {
            if (casterGlow != null)
            {
                casterGlowBaseLocalScale = casterGlow.transform.localScale;
            }
        }

        private void CreatePropertyBlocks()
        {
            outerGlowProperties ??= new MaterialPropertyBlock();
            ribbonProperties ??= new MaterialPropertyBlock();
            coreProperties ??= new MaterialPropertyBlock();
            casterGlowProperties ??= new MaterialPropertyBlock();
            targetGlowProperties ??= new MaterialPropertyBlock();
            groundRingProperties ??= new MaterialPropertyBlock();
        }

        private void PlayLoopingParticles()
        {
            if (loopingParticles == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in loopingParticles)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = true;
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private void StopLoopingEmission()
        {
            if (loopingParticles == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in loopingParticles)
            {
                particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
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

        private bool AnyParticlesAlive()
        {
            if (allParticles == null)
            {
                return false;
            }

            foreach (ParticleSystem particleSystem in allParticles)
            {
                if (particleSystem != null && particleSystem.IsAlive(true))
                {
                    return true;
                }
            }

            return false;
        }

        private void CompletePlayback()
        {
            state = PlaybackState.Stopped;
            SetVisualRootsActive(false);
            Completed?.Invoke(this);
        }

        private void StopImmediateInternal(bool notify)
        {
            if (allParticles != null)
            {
                foreach (ParticleSystem particleSystem in allParticles)
                {
                    particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            opacity = 0f;
            pulseActive = false;
            state = PlaybackState.Stopped;
            HidePersistentRenderers();
            SetVisualRootsActive(false);
            if (notify)
            {
                Completed?.Invoke(this);
            }
        }

        private void SetVisualRootsActive(bool active)
        {
            SetActive(beamEffectRoot, active);
            SetActive(casterEffectRoot, active);
            SetActive(targetEffectRoot, active);
            SetActive(healTickEffectRoot, active);
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private void ShowPersistentRenderers()
        {
            SetRendererEnabled(outerGlow, true);
            SetRendererEnabled(flowingRibbon, true);
            SetRendererEnabled(innerCore, true);
            SetRendererEnabled(casterGlow, true);
            SetRendererEnabled(targetGlow, true);
            SetRendererEnabled(groundRing, true);
        }

        private void HidePersistentRenderers()
        {
            SetRendererEnabled(outerGlow, false);
            SetRendererEnabled(flowingRibbon, false);
            SetRendererEnabled(innerCore, false);
            SetRendererEnabled(casterGlow, false);
            SetRendererEnabled(targetGlow, false);
            SetRendererEnabled(groundRing, false);
        }

        private static void SetRendererEnabled(Renderer targetRenderer, bool enabled)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = enabled;
            }
        }

        private static void ConfigureLine(LineRenderer line, float width, Vector3[] positions)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.widthMultiplier = width;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }
    }
}
