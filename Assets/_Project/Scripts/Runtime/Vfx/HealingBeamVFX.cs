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
        [SerializeField] private Renderer launchHeadGlow;

        [Header("Caster Effect")]
        [SerializeField] private Transform casterEffectRoot;
        [SerializeField] private Renderer casterGlow;
        [SerializeField] private ParticleSystem casterOriginFlash;
        [SerializeField] private ParticleSystem casterOrbitingStars;
        [SerializeField] private ParticleSystem casterInwardOrbs;
        [SerializeField] private ParticleSystem casterLeaves;

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
        [SerializeField] private ParticleSystem impactLeaves;
        [SerializeField] private Transform impactHaloTransform;
        [SerializeField] private Renderer impactHalo;

        [Header("Target Impact Echo")]
        [SerializeField] private Transform targetImpactEchoRoot;
        [SerializeField] private Renderer targetImpactOrb;
        [SerializeField] private Transform targetImpactInnerRingTransform;
        [SerializeField] private Renderer targetImpactInnerRing;
        [SerializeField] private Transform targetImpactOuterRingTransform;
        [SerializeField] private Renderer targetImpactOuterRing;
        [SerializeField] private ParticleSystem targetImpactSparkles;
        [SerializeField] private ParticleSystem targetImpactDust;

        private MaterialPropertyBlock outerGlowProperties;
        private MaterialPropertyBlock ribbonProperties;
        private MaterialPropertyBlock coreProperties;
        private MaterialPropertyBlock launchHeadProperties;
        private MaterialPropertyBlock casterGlowProperties;
        private MaterialPropertyBlock targetGlowProperties;
        private MaterialPropertyBlock groundRingProperties;
        private MaterialPropertyBlock impactHaloProperties;
        private MaterialPropertyBlock targetImpactOrbProperties;
        private MaterialPropertyBlock targetImpactInnerRingProperties;
        private MaterialPropertyBlock targetImpactOuterRingProperties;

        private Transform casterAttachment;
        private Transform targetAttachment;
        private ParticleSystem[] loopingParticles;
        private ParticleSystem[] casterLoopingParticles;
        private ParticleSystem[] targetLoopingParticles;
        private ParticleSystem[] allParticles;
        private Vector3[] beamPositions = Array.Empty<Vector3>();
        private PlaybackState state;
        private Camera cachedCamera;
        private float stateStartedAt;
        private float stateStartOpacity;
        private float opacity;
        private float pulseStartedAt;
        private float launchStartedAt;
        private float impactStartedAt = float.NegativeInfinity;
        private float tickFlashStartedAt = float.NegativeInfinity;
        private bool pulseActive;
        private bool impactPending;
        private bool targetArrived;
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
            impactPending = false;
            targetArrived = false;
            launchStartedAt = Time.time;
            impactStartedAt = float.NegativeInfinity;
            tickFlashStartedAt = float.NegativeInfinity;

            PlayLoopingParticles(casterLoopingParticles);
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
            pulseActive = true;
            impactPending = true;
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
            Renderer newLaunchHeadGlow,
            Transform newCasterEffectRoot,
            Renderer newCasterGlow,
            ParticleSystem newCasterOriginFlash,
            ParticleSystem newCasterOrbitingStars,
            ParticleSystem newCasterInwardOrbs,
            ParticleSystem newCasterLeaves,
            Transform newTargetEffectRoot,
            Renderer newTargetGlow,
            Transform newGroundRingTransform,
            Renderer newGroundRing,
            ParticleSystem newTargetRisingOrbs,
            ParticleSystem newTargetSparkles,
            Transform newHealTickEffectRoot,
            ParticleSystem newTargetBurst,
            ParticleSystem newTickSparks,
            ParticleSystem newImpactLeaves,
            Transform newImpactHaloTransform,
            Renderer newImpactHalo,
            Transform newTargetImpactEchoRoot,
            Renderer newTargetImpactOrb,
            Transform newTargetImpactInnerRingTransform,
            Renderer newTargetImpactInnerRing,
            Transform newTargetImpactOuterRingTransform,
            Renderer newTargetImpactOuterRing,
            ParticleSystem newTargetImpactSparkles,
            ParticleSystem newTargetImpactDust)
        {
            profile = newProfile;
            beamEffectRoot = newBeamEffectRoot;
            outerGlow = newOuterGlow;
            flowingRibbon = newFlowingRibbon;
            innerCore = newInnerCore;
            launchHeadGlow = newLaunchHeadGlow;
            casterEffectRoot = newCasterEffectRoot;
            casterGlow = newCasterGlow;
            casterOriginFlash = newCasterOriginFlash;
            casterOrbitingStars = newCasterOrbitingStars;
            casterInwardOrbs = newCasterInwardOrbs;
            casterLeaves = newCasterLeaves;
            targetEffectRoot = newTargetEffectRoot;
            targetGlow = newTargetGlow;
            groundRingTransform = newGroundRingTransform;
            groundRing = newGroundRing;
            targetRisingOrbs = newTargetRisingOrbs;
            targetSparkles = newTargetSparkles;
            healTickEffectRoot = newHealTickEffectRoot;
            targetBurst = newTargetBurst;
            tickSparks = newTickSparks;
            impactLeaves = newImpactLeaves;
            impactHaloTransform = newImpactHaloTransform;
            impactHalo = newImpactHalo;
            targetImpactEchoRoot = newTargetImpactEchoRoot;
            targetImpactOrb = newTargetImpactOrb;
            targetImpactInnerRingTransform = newTargetImpactInnerRingTransform;
            targetImpactInnerRing = newTargetImpactInnerRing;
            targetImpactOuterRingTransform = newTargetImpactOuterRingTransform;
            targetImpactOuterRing = newTargetImpactOuterRing;
            targetImpactSparkles = newTargetImpactSparkles;
            targetImpactDust = newTargetImpactDust;
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
            float launchProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((Time.time - launchStartedAt) / profile.BeamLaunchDuration));
            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / (float)(segmentCount - 1);
                beamPositions[i] = EvaluateBeamPoint(start, end, side, up, Mathf.Min(t, launchProgress), phase);
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
                if (impactPending && pulseProgress >= 1f)
                {
                    TriggerTargetImpact();
                }

                if (pulseProgress > 1f + profile.PulseWidth)
                {
                    pulseActive = false;
                    pulseProgress = -10f;
                }
            }

            ApplyBeamProperties(outerGlow, outerGlowProperties, profile.OuterGlowColor, profile.GlowFlowSpeed, distanceTiling, pulseProgress, 0.35f);
            ApplyBeamProperties(flowingRibbon, ribbonProperties, profile.RibbonColor, profile.RibbonFlowSpeed, distanceTiling, pulseProgress, 0.7f);
            ApplyBeamProperties(innerCore, coreProperties, profile.CoreColor, profile.CoreFlowSpeed, distanceTiling, pulseProgress, 1f);

            if (launchHeadGlow != null)
            {
                launchHeadGlow.transform.position = beamPositions[segmentCount - 1];
                launchHeadGlow.transform.localScale = Vector3.one * profile.BeamWidth * Mathf.Lerp(3.8f, 5.4f, launchProgress);
                float headOpacity = launchProgress < 1f ? opacity * Mathf.Lerp(0.65f, 1f, launchProgress) : 0f;
                launchHeadGlow.enabled = headOpacity > 0.001f;
                ApplySpriteProperties(
                    launchHeadGlow,
                    launchHeadProperties,
                    new Color(0.78f, 1f, 0.58f, 0.8f),
                    headOpacity * profile.OverallIntensity);
            }
        }

        private Vector3 EvaluateBeamPoint(
            Vector3 start,
            Vector3 end,
            Vector3 side,
            Vector3 up,
            float t,
            float phase)
        {
            float endpointEnvelope = Mathf.Sin(t * Mathf.PI);
            float primaryWave = Mathf.Sin((t * Mathf.PI * 2.25f) + phase) * profile.BeamSway;
            float secondaryWave = Mathf.Sin((t * Mathf.PI * 3.5f) - (phase * 0.73f)) * profile.BeamSway * 0.34f;
            float arc = Mathf.Sin(t * Mathf.PI) * profile.BeamArcHeight;
            return Vector3.LerpUnclamped(start, end, t)
                + side * primaryWave * endpointEnvelope
                + up * ((secondaryWave * endpointEnvelope) + arc);
        }

        private void TriggerTargetImpact()
        {
            impactPending = false;
            targetArrived = true;
            impactStartedAt = Time.time;
            tickFlashStartedAt = Time.time;

            SetRendererEnabled(targetGlow, true);
            SetRendererEnabled(groundRing, true);
            SetRendererEnabled(impactHalo, true);
            PlayLoopingParticles(targetLoopingParticles);
            PlayOneShot(targetBurst);
            PlayOneShot(tickSparks);
            PlayOneShot(impactLeaves);
            PlayOneShot(targetImpactSparkles);
            PlayOneShot(targetImpactDust);
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
            float arrivalOpacity = targetArrived
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - impactStartedAt) / profile.TargetArrivalFadeDuration))
                : 0f;

            ApplySpriteProperties(casterGlow, casterGlowProperties, new Color(1f, 0.78f, 0.28f, 0.48f), masterOpacity * ambientPulse);
            ApplySpriteProperties(targetGlow, targetGlowProperties, new Color(0.72f, 1f, 0.48f, 0.46f), masterOpacity * targetPulse * arrivalOpacity);
            ApplySpriteProperties(groundRing, groundRingProperties, new Color(0.82f, 1f, 0.38f, 0.36f), masterOpacity * targetPulse * arrivalOpacity);

            float impactT = Mathf.Clamp01((Time.time - impactStartedAt) / profile.ImpactHaloDuration);
            if (targetArrived && impactT < 1f)
            {
                float easedImpact = 1f - Mathf.Pow(1f - impactT, 3f);
                float haloOpacity = Mathf.Sin(impactT * Mathf.PI) * masterOpacity;
                if (impactHaloTransform != null)
                {
                    float haloSize = Mathf.Lerp(profile.ImpactHaloStartSize, profile.ImpactHaloEndSize, easedImpact);
                    impactHaloTransform.localScale = Vector3.one * haloSize;
                }

                ApplySpriteProperties(
                    impactHalo,
                    impactHaloProperties,
                    new Color(0.7f, 1f, 0.4f, 0.82f),
                    haloOpacity);
            }
            else if (impactHalo != null)
            {
                impactHalo.enabled = false;
            }

            UpdateTargetImpactEcho(masterOpacity);

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

        private void UpdateTargetImpactEcho(float masterOpacity)
        {
            float impactT = Mathf.Clamp01((Time.time - impactStartedAt) / profile.TargetImpactEchoDuration);
            bool echoVisible = targetArrived && impactT < 1f;
            SetRendererEnabled(targetImpactOrb, echoVisible);
            SetRendererEnabled(targetImpactInnerRing, echoVisible);
            SetRendererEnabled(targetImpactOuterRing, echoVisible);
            if (!echoVisible)
            {
                return;
            }

            float easedT = Mathf.SmoothStep(0f, 1f, impactT);
            float flashEnvelope = Mathf.Pow(Mathf.Sin(impactT * Mathf.PI), 0.55f) * masterOpacity;
            float cylinderHeight = profile.CasterBuildupCylinderHeight;
            float groundOffset = profile.GroundRingVerticalOffset;
            float baseRingSize = profile.CasterGroundRingSize * profile.TargetEffectScale;

            if (targetImpactEchoRoot != null)
            {
                targetImpactEchoRoot.localScale = Vector3.one;
            }

            if (targetImpactOrb != null)
            {
                float orbExpansion = Mathf.Lerp(0.78f, 1.35f, Mathf.Sin(impactT * Mathf.PI));
                targetImpactOrb.transform.localScale = casterGlowBaseLocalScale
                    * profile.EndpointOrbSizeMultiplier
                    * profile.TargetEffectScale
                    * orbExpansion;
            }

            if (targetImpactInnerRingTransform != null)
            {
                targetImpactInnerRingTransform.localPosition = Vector3.up * (groundOffset + (easedT * cylinderHeight));
                targetImpactInnerRingTransform.localScale = Vector3.one * baseRingSize * Mathf.Lerp(0.7f, 0.88f, easedT);
                targetImpactInnerRingTransform.localRotation = Quaternion.Euler(90f, 0f, impactT * 150f);
            }

            if (targetImpactOuterRingTransform != null)
            {
                targetImpactOuterRingTransform.localPosition = Vector3.up * (groundOffset + (easedT * cylinderHeight * 0.82f));
                targetImpactOuterRingTransform.localScale = Vector3.one * baseRingSize * Mathf.Lerp(0.88f, 1.06f, easedT);
                targetImpactOuterRingTransform.localRotation = Quaternion.Euler(90f, 0f, impactT * -110f);
            }

            ApplySpriteProperties(
                targetImpactOrb,
                targetImpactOrbProperties,
                new Color(1f, 0.82f, 0.3f, 0.96f),
                flashEnvelope);
            ApplySpriteProperties(
                targetImpactInnerRing,
                targetImpactInnerRingProperties,
                new Color(0.68f, 1f, 0.38f, 0.96f),
                flashEnvelope);
            ApplySpriteProperties(
                targetImpactOuterRing,
                targetImpactOuterRingProperties,
                new Color(1f, 0.8f, 0.28f, 0.92f),
                flashEnvelope * 0.95f);
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
            Billboard(launchHeadGlow != null ? launchHeadGlow.transform : null, cachedCamera.transform);
            Billboard(impactHalo != null ? impactHalo.transform : null, cachedCamera.transform);
            Billboard(targetImpactOrb != null ? targetImpactOrb.transform : null, cachedCamera.transform);
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
            ConfigureLoopParticle(casterLeaves, profile.CasterLeafParticleCount, profile.LeafParticleSize);
            ConfigureLoopParticle(targetRisingOrbs, profile.TargetRisingParticleCount, profile.ParticleSize * 0.72f);
            ConfigureLoopParticle(
                targetSparkles,
                profile.TargetSparkleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureBurstParticle(
                targetBurst,
                1,
                profile.ParticleSize * 5.4f * profile.ImpactSparkSizeMultiplier);
            ConfigureBurstParticle(
                tickSparks,
                profile.TickSparkCount,
                profile.ParticleSize * 1.1f * profile.ImpactSparkSizeMultiplier);
            ConfigureBurstParticle(impactLeaves, profile.ImpactLeafCount, profile.LeafParticleSize);
            ConfigureBurstParticle(
                targetImpactSparkles,
                profile.CasterOrbitParticleCount + profile.TargetSparkleCount,
                profile.ParticleSize * profile.EndpointSparkleSizeMultiplier);
            ConfigureTargetImpactDust();
        }

        private void ConfigureTargetImpactDust()
        {
            if (targetImpactDust == null)
            {
                return;
            }

            ConfigureBurstParticle(
                targetImpactDust,
                profile.CasterDustParticleCount,
                profile.CasterDustParticleSize);
            ParticleSystem.MainModule main = targetImpactDust.main;
            main.startLifetime = profile.TargetImpactEchoDuration;
            ParticleSystem.ShapeModule shape = targetImpactDust.shape;
            shape.radius = profile.CasterDustRingRadius;
            shape.radiusThickness = 0.05f;
            ParticleSystem.VelocityOverLifetimeModule velocity = targetImpactDust.velocityOverLifetime;
            float impactRiseSpeed = profile.CasterBuildupCylinderHeight / profile.TargetImpactEchoDuration;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(impactRiseSpeed * 0.68f, impactRiseSpeed * 1.05f);
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
            casterLoopingParticles = new[] { casterOrbitingStars, casterInwardOrbs, casterLeaves };
            targetLoopingParticles = new[] { targetRisingOrbs, targetSparkles };
            loopingParticles = new[] { casterOrbitingStars, casterInwardOrbs, casterLeaves, targetRisingOrbs, targetSparkles };
            allParticles = new[]
            {
                casterOriginFlash,
                casterOrbitingStars,
                casterInwardOrbs,
                casterLeaves,
                targetRisingOrbs,
                targetSparkles,
                targetBurst,
                tickSparks,
                impactLeaves,
                targetImpactSparkles,
                targetImpactDust
            };
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
            launchHeadProperties ??= new MaterialPropertyBlock();
            casterGlowProperties ??= new MaterialPropertyBlock();
            targetGlowProperties ??= new MaterialPropertyBlock();
            groundRingProperties ??= new MaterialPropertyBlock();
            impactHaloProperties ??= new MaterialPropertyBlock();
            targetImpactOrbProperties ??= new MaterialPropertyBlock();
            targetImpactInnerRingProperties ??= new MaterialPropertyBlock();
            targetImpactOuterRingProperties ??= new MaterialPropertyBlock();
        }

        private static void PlayLoopingParticles(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
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
            impactPending = false;
            targetArrived = false;
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
            SetRendererEnabled(launchHeadGlow, true);
            SetRendererEnabled(targetGlow, false);
            SetRendererEnabled(groundRing, false);
            SetRendererEnabled(impactHalo, false);
            SetRendererEnabled(targetImpactOrb, false);
            SetRendererEnabled(targetImpactInnerRing, false);
            SetRendererEnabled(targetImpactOuterRing, false);
        }

        private void HidePersistentRenderers()
        {
            SetRendererEnabled(outerGlow, false);
            SetRendererEnabled(flowingRibbon, false);
            SetRendererEnabled(innerCore, false);
            SetRendererEnabled(casterGlow, false);
            SetRendererEnabled(launchHeadGlow, false);
            SetRendererEnabled(targetGlow, false);
            SetRendererEnabled(groundRing, false);
            SetRendererEnabled(impactHalo, false);
            SetRendererEnabled(targetImpactOrb, false);
            SetRendererEnabled(targetImpactInnerRing, false);
            SetRendererEnabled(targetImpactOuterRing, false);
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
