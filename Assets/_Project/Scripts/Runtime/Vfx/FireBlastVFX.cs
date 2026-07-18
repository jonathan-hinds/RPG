using System;
using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireBlastVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");

        [Header("Configuration")]
        [SerializeField] private FireBlastVFXProfile profile;
        [SerializeField] private bool destroyOnComplete = true;

        [Header("Casting Ignition")]
        [SerializeField] private Transform casterEffectRoot;
        [SerializeField] private Renderer casterGlow;
        [SerializeField] private ParticleSystem castingEmbers;

        [Header("Instant Connection")]
        [SerializeField] private LineRenderer fireStreak;

        [Header("Target Combustion")]
        [SerializeField] private Transform impactEffectRoot;
        [SerializeField] private Renderer compressionFlash;
        [SerializeField] private Renderer outerCombustion;
        [SerializeField] private Renderer explosionCore;
        [SerializeField] private ParticleSystem flameBurst;
        [SerializeField] private Renderer heatRing;
        [SerializeField] private Renderer secondaryHeatRing;
        [SerializeField] private ParticleSystem emberBurst;
        [SerializeField] private ParticleSystem sparkBurst;

        [Header("Aftermath")]
        [SerializeField] private Renderer lingeringGlow;
        [SerializeField] private ParticleSystem lingeringFlames;
        [SerializeField] private ParticleSystem smokeBloom;
        [SerializeField] private ParticleSystem lingeringEmbers;

        private MaterialPropertyBlock propertyBlock;
        private Renderer[] allRenderers = Array.Empty<Renderer>();
        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private Transform sourceAttachment;
        private Transform targetAttachment;
        private MMOAbilityVfxDefinition definition;
        private Vector3 sourcePosition;
        private Vector3 targetPosition;
        private float startedAt;
        private bool playing;
        private Camera cachedCamera;

        public event Action<FireBlastVFX> Completed;

        public FireBlastVFXProfile Profile => profile;
        public bool IsPlaying => playing;
        public bool ReadyForPool => !playing;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            CacheComponents();
            HideRenderers();
            StopParticles();
        }

        private void OnDisable()
        {
            playing = false;
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            UpdateAttachmentPositions();
            Animate(Time.time - startedAt);
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            sourceAttachment = context.Source;
            targetAttachment = context.Target;
            definition = context.Definition;
            Play(context.SourcePosition, ResolveTargetPosition(context.TargetPosition));
        }

        public void Play(Vector3 sourceWorldPosition, Vector3 targetWorldPosition)
        {
            if (profile == null)
            {
                Debug.LogError("FireBlastVFX requires a FireBlastVFXProfile.", this);
                return;
            }

            CancelInvoke();
            CacheComponents();
            sourcePosition = sourceWorldPosition;
            targetPosition = targetWorldPosition;
            transform.position = sourcePosition;
            casterEffectRoot.position = sourcePosition;
            impactEffectRoot.position = targetPosition;
            casterEffectRoot.localScale = Vector3.one * profile.OverallScale;
            impactEffectRoot.localScale = Vector3.one;
            ConfigureParticleBudgets();
            ApplyStaticRendererSettings();
            PlayParticles();
            startedAt = Time.time;
            playing = true;
            UpdateAttachmentPositions();
            Animate(0f);
        }

        public void StopImmediate()
        {
            playing = false;
            StopParticles();
            HideRenderers();
        }

        public void ResetForPool()
        {
            StopImmediate();
            sourceAttachment = null;
            targetAttachment = null;
            definition = null;
        }

        public void ConfigureAuthoring(
            FireBlastVFXProfile newProfile,
            bool newDestroyOnComplete,
            Transform newCasterEffectRoot,
            Renderer newCasterGlow,
            ParticleSystem newCastingEmbers,
            LineRenderer newFireStreak,
            Transform newImpactEffectRoot,
            Renderer newCompressionFlash,
            Renderer newOuterCombustion,
            Renderer newExplosionCore,
            ParticleSystem newFlameBurst,
            Renderer newHeatRing,
            Renderer newSecondaryHeatRing,
            ParticleSystem newEmberBurst,
            ParticleSystem newSparkBurst,
            Renderer newLingeringGlow,
            ParticleSystem newLingeringFlames,
            ParticleSystem newSmokeBloom,
            ParticleSystem newLingeringEmbers)
        {
            profile = newProfile;
            destroyOnComplete = newDestroyOnComplete;
            casterEffectRoot = newCasterEffectRoot;
            casterGlow = newCasterGlow;
            castingEmbers = newCastingEmbers;
            fireStreak = newFireStreak;
            impactEffectRoot = newImpactEffectRoot;
            compressionFlash = newCompressionFlash;
            outerCombustion = newOuterCombustion;
            explosionCore = newExplosionCore;
            flameBurst = newFlameBurst;
            heatRing = newHeatRing;
            secondaryHeatRing = newSecondaryHeatRing;
            emberBurst = newEmberBurst;
            sparkBurst = newSparkBurst;
            lingeringGlow = newLingeringGlow;
            lingeringFlames = newLingeringFlames;
            smokeBloom = newSmokeBloom;
            lingeringEmbers = newLingeringEmbers;
            CacheComponents();
        }

        private void Animate(float elapsed)
        {
            float duration = Mathf.Max(0.05f, profile.OverallDuration);
            float impactElapsed = elapsed - 0.035f;

            float casterAlpha = Pulse(elapsed, 0f, 0.035f, 0.16f);
            SetRenderer(casterGlow, profile.Colors.HotYellow, casterAlpha, profile.Brightness * 1.15f, Vector2.zero);
            float casterScale = profile.OverallScale * Mathf.Lerp(0.38f, 1.18f, Mathf.Clamp01(elapsed / 0.075f));
            casterGlow.transform.localScale = Vector3.one * casterScale;

            float streakAlpha = Pulse(elapsed, 0.012f, 0.025f, 0.115f);
            SetRenderer(fireStreak, Color.white, streakAlpha, profile.Brightness * profile.FireStreakBrightness, new Vector2(-7.5f, 0f));
            fireStreak.widthMultiplier = profile.OverallScale * Mathf.Lerp(0.22f, 0.36f, Mathf.Sin(Mathf.Clamp01(streakAlpha) * Mathf.PI));

            float compressionAlpha = Pulse(impactElapsed, 0f, 0.018f, 0.12f);
            float compressionProgress = Mathf.Clamp01(impactElapsed / 0.1f);
            compressionFlash.transform.localScale = Vector3.one
                * profile.ExplosionSize
                * profile.OverallScale
                * Mathf.Lerp(0.22f, 1.06f, Smooth01(compressionProgress));
            SetRenderer(
                compressionFlash,
                profile.Colors.WhiteHot,
                compressionAlpha,
                profile.Brightness * profile.ExplosionBrightness * 1.45f,
                Vector2.zero);

            float outerAlpha = Pulse(impactElapsed, 0.012f, 0.065f, 0.56f);
            float outerProgress = Mathf.Clamp01(impactElapsed / 0.56f);
            outerCombustion.transform.localScale = Vector3.one
                * profile.ExplosionSize
                * profile.OverallScale
                * Mathf.Lerp(0.58f, 1.34f, Smooth01(outerProgress));
            SetRenderer(
                outerCombustion,
                profile.Colors.DeepOrange,
                outerAlpha * 0.82f,
                profile.Brightness * profile.ExplosionBrightness * 0.74f,
                Vector2.zero);

            float coreAlpha = Pulse(impactElapsed, 0f, 0.028f, 0.4f);
            float coreProgress = Mathf.Clamp01(impactElapsed / 0.4f);
            float corePulse = coreProgress < 0.38f
                ? Mathf.Lerp(0.28f, 1.18f, Smooth01(coreProgress / 0.38f))
                : Mathf.Lerp(1.18f, 0.68f, Smooth01((coreProgress - 0.38f) / 0.62f));
            explosionCore.transform.localScale = Vector3.one * (profile.ExplosionSize * profile.OverallScale * corePulse);
            SetRenderer(explosionCore, Color.white, coreAlpha, profile.Brightness * profile.ExplosionBrightness, Vector2.zero);

            float ringDuration = 0.26f / Mathf.Max(0.25f, profile.HeatRingSpeed);
            float ringProgress = Mathf.Clamp01(impactElapsed / ringDuration);
            float ringAlpha = impactElapsed >= 0f && impactElapsed <= ringDuration ? 1f - Smooth01(ringProgress) : 0f;
            heatRing.transform.localScale = Vector3.one * Mathf.Lerp(
                profile.ExplosionSize * 0.28f,
                profile.HeatRingSize,
                Smooth01(ringProgress)) * profile.OverallScale;
            SetRenderer(heatRing, profile.Colors.GoldenOrange, ringAlpha * 0.74f, profile.Brightness * 1.05f, Vector2.zero);

            float secondaryElapsed = impactElapsed - 0.045f;
            float secondaryRingDuration = 0.34f / Mathf.Max(0.25f, profile.HeatRingSpeed);
            float secondaryProgress = Mathf.Clamp01(secondaryElapsed / secondaryRingDuration);
            float secondaryAlpha = secondaryElapsed >= 0f && secondaryElapsed <= secondaryRingDuration
                ? (1f - Smooth01(secondaryProgress)) * 0.52f
                : 0f;
            secondaryHeatRing.transform.localScale = Vector3.one * Mathf.Lerp(
                profile.ExplosionSize * 0.32f,
                profile.HeatRingSize * 1.12f,
                Smooth01(secondaryProgress)) * profile.OverallScale;
            SetRenderer(
                secondaryHeatRing,
                profile.Colors.DeepOrange,
                secondaryAlpha,
                profile.Brightness * 0.88f,
                Vector2.zero);

            float glowStart = 0.1f;
            float glowProgress = Mathf.Clamp01((impactElapsed - glowStart) / Mathf.Max(0.05f, duration - glowStart));
            float glowAlpha = impactElapsed >= glowStart ? (1f - Smooth01(glowProgress)) * 0.34f : 0f;
            lingeringGlow.transform.localScale = Vector3.one * profile.ExplosionSize * profile.OverallScale * Mathf.Lerp(0.72f, 0.42f, glowProgress);
            SetRenderer(lingeringGlow, profile.Colors.DeepOrange, glowAlpha, profile.Brightness * 0.7f, Vector2.zero);

            BillboardQuads(elapsed);

            if (elapsed >= duration)
            {
                Complete();
            }
        }

        private void UpdateAttachmentPositions()
        {
            if (sourceAttachment != null && definition != null)
            {
                MMOAbilityVfxAnchors sourceAnchors = sourceAttachment.GetComponent<MMOAbilityVfxAnchors>();
                sourcePosition = sourceAnchors != null
                    ? sourceAnchors.ResolveCastOriginPosition(definition)
                    : sourceAttachment.TransformPoint(definition.CastOriginLocalOffset);
            }

            targetPosition = ResolveTargetPosition(targetPosition);
            casterEffectRoot.position = sourcePosition;
            impactEffectRoot.position = targetPosition;
            Vector3 streakEnd = Vector3.LerpUnclamped(sourcePosition, targetPosition, profile.FireStreakLength);
            fireStreak.SetPosition(0, sourcePosition);
            fireStreak.SetPosition(1, streakEnd);
            float distance = Vector3.Distance(sourcePosition, streakEnd);
            fireStreak.textureScale = new Vector2(Mathf.Max(1f, distance * 1.5f), 1f);
        }

        private Vector3 ResolveTargetPosition(Vector3 fallback)
        {
            if (targetAttachment == null)
            {
                return fallback;
            }

            MMOAbilityVfxAnchors targetAnchors = targetAttachment.GetComponent<MMOAbilityVfxAnchors>();
            if (targetAnchors != null && definition != null)
            {
                return targetAnchors.ResolveHitPosition(definition);
            }

            return definition != null
                ? targetAttachment.TransformPoint(definition.HitLocalOffset)
                : targetAttachment.TransformPoint(new Vector3(0f, 1.05f, 0f));
        }

        private void ConfigureParticleBudgets()
        {
            SetBurst(castingEmbers, Mathf.Clamp(profile.EmberCount / 3, 2, 8), 0.5f, 0.07f);
            SetBurst(flameBurst, profile.FlameCount, 1.1f, profile.FlameSize);
            SetBurst(
                lingeringFlames,
                Mathf.Clamp(profile.FlameCount / 2, 4, 10),
                0.55f,
                profile.FlameSize * 0.72f,
                profile.LingeringFireDuration);
            SetBurst(emberBurst, profile.EmberCount, profile.EmberSpeed, 0.12f);
            SetBurst(sparkBurst, profile.SparkCount, profile.EmberSpeed * 1.35f, 0.16f);
            SetBurst(smokeBloom, profile.SmokeAmount, 0.22f, profile.ExplosionSize * 0.34f, profile.SmokeDuration);
            SetBurst(lingeringEmbers, Mathf.Clamp(profile.EmberCount / 4, 1, 6), 0.34f, 0.065f);
        }

        private void ApplyStaticRendererSettings()
        {
            SetRenderer(flameBurst.GetComponent<ParticleSystemRenderer>(), Color.white, 1f, profile.Brightness * 1.1f, Vector2.zero);
            SetRenderer(lingeringFlames.GetComponent<ParticleSystemRenderer>(), Color.white, 1f, profile.Brightness * 0.92f, Vector2.zero);
            SetRenderer(emberBurst.GetComponent<ParticleSystemRenderer>(), Color.white, 1f, profile.Brightness * 1.15f, Vector2.zero);
            SetRenderer(sparkBurst.GetComponent<ParticleSystemRenderer>(), Color.white, 1f, profile.Brightness * 1.3f, Vector2.zero);
            SetRenderer(castingEmbers.GetComponent<ParticleSystemRenderer>(), Color.white, 1f, profile.Brightness, Vector2.zero);
            SetRenderer(lingeringEmbers.GetComponent<ParticleSystemRenderer>(), profile.Colors.GoldenOrange, 1f, profile.Brightness * 0.8f, Vector2.zero);
            SetRenderer(smokeBloom.GetComponent<ParticleSystemRenderer>(), profile.Colors.Charcoal, 1f, 0.82f, Vector2.zero);
        }

        private void BillboardQuads(float elapsed)
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return;
            }

            Quaternion rotation = cachedCamera.transform.rotation;
            casterGlow.transform.rotation = rotation;
            compressionFlash.transform.rotation = rotation;
            outerCombustion.transform.rotation = rotation * Quaternion.Euler(0f, 0f, elapsed * -32f);
            explosionCore.transform.rotation = rotation;
            heatRing.transform.rotation = rotation * Quaternion.Euler(0f, 0f, elapsed * 46f);
            secondaryHeatRing.transform.rotation = rotation * Quaternion.Euler(0f, 0f, elapsed * -34f);
            lingeringGlow.transform.rotation = rotation;
        }

        private void Complete()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            HideRenderers();
            Completed?.Invoke(this);
            if (destroyOnComplete && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, Vector2 scrollSpeed)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetVector(ScrollSpeedId, scrollSpeed);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private void CacheComponents()
        {
            allRenderers = GetComponentsInChildren<Renderer>(true);
            allParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void HideRenderers()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            foreach (Renderer renderer in allRenderers)
            {
                SetRenderer(renderer, Color.white, 0f, 1f, Vector2.zero);
            }
        }

        private void PlayParticles()
        {
            foreach (ParticleSystem particles in allParticles)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(true);
            }
        }

        private void StopParticles()
        {
            foreach (ParticleSystem particles in allParticles)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void SetBurst(ParticleSystem particles, int count, float speed, float size, float? lifetime = null)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = Mathf.Max(1, count);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.72f, speed * 1.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.72f, size * 1.16f);
            if (lifetime.HasValue)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Value * 0.82f, lifetime.Value);
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(count > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)count) }
                : Array.Empty<ParticleSystem.Burst>());
        }

        private static float Pulse(float value, float start, float peak, float end)
        {
            if (value < start || value >= end)
            {
                return 0f;
            }

            if (value <= peak)
            {
                return Smooth01(Mathf.InverseLerp(start, peak, value));
            }

            return 1f - Smooth01(Mathf.InverseLerp(peak, end, value));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
