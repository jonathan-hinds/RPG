using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    /// <summary>
    /// Receiver-local presentation for the replicated Frost Shock release. Gameplay and hit authority stay in MMOAbilitySystem.
    /// </summary>
    public sealed class FrostShockProjectileVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private FrostShockVFXProfile profile;
        [SerializeField] private Transform castRoot;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private Renderer handFlash;
        [SerializeField] private Renderer handCore;
        [SerializeField] private Renderer[] wristRibbons;
        [SerializeField] private Renderer[] projectileLayers;
        [SerializeField] private ParticleSystem releaseFragments;
        [SerializeField] private ParticleSystem vaporTrail;
        [SerializeField] private ParticleSystem shardTrail;
        [SerializeField] private ParticleSystem snowTrail;
        [SerializeField] private bool destroyOnComplete = true;

        private MaterialPropertyBlock propertyBlock;
        private MMOAbilityVfxContext context;
        private Transform target;
        private Vector3 sourcePosition;
        private Vector3 targetPosition;
        private float startedAt;
        private float travelDuration;
        private bool initialized;
        private bool hitRequested;
        private bool trailsStopped;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            target = context.Target;
            sourcePosition = context.SourcePosition;
            targetPosition = ResolveTargetPosition(context.TargetPosition);
            float distance = Vector3.Distance(sourcePosition, targetPosition);
            travelDuration = Mathf.Clamp(distance / Mathf.Max(1f, profile.ProjectileSpeed), 0.05f, 0.2f);
            startedAt = Time.time;
            initialized = true;
            hitRequested = false;
            trailsStopped = false;

            transform.position = sourcePosition;
            castRoot.position = sourcePosition;
            projectileRoot.position = sourcePosition;
            projectileRoot.localScale = Vector3.one * profile.OverallScale;
            ApplyProjectileDimensions();
            PlayParticles();
            Animate(0f);
        }

        private void LateUpdate()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            targetPosition = ResolveTargetPosition(targetPosition);
            float progress = Mathf.Clamp01(elapsed / travelDuration);
            Vector3 direction = targetPosition - sourcePosition;
            Vector3 position = Vector3.Lerp(sourcePosition, targetPosition, Smooth01(progress));
            projectileRoot.position = position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                projectileRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (context.Source != null)
            {
                castRoot.position = ResolveSourcePosition();
            }

            Animate(elapsed);
            if (!hitRequested && progress >= 1f)
            {
                hitRequested = true;
                context.RequestHit?.Invoke();
                HideProjectile();
                StopTrails();
            }

            if (hitRequested && elapsed >= travelDuration + profile.TrailFadeDuration)
            {
                Complete();
            }
        }

        public void ConfigureAuthoring(
            FrostShockVFXProfile newProfile,
            Transform newCastRoot,
            Transform newProjectileRoot,
            Renderer newHandFlash,
            Renderer newHandCore,
            Renderer[] newWristRibbons,
            Renderer[] newProjectileLayers,
            ParticleSystem newReleaseFragments,
            ParticleSystem newVaporTrail,
            ParticleSystem newShardTrail,
            ParticleSystem newSnowTrail,
            bool newDestroyOnComplete)
        {
            profile = newProfile;
            castRoot = newCastRoot;
            projectileRoot = newProjectileRoot;
            handFlash = newHandFlash;
            handCore = newHandCore;
            wristRibbons = newWristRibbons;
            projectileLayers = newProjectileLayers;
            releaseFragments = newReleaseFragments;
            vaporTrail = newVaporTrail;
            shardTrail = newShardTrail;
            snowTrail = newSnowTrail;
            destroyOnComplete = newDestroyOnComplete;
        }

        private void Animate(float elapsed)
        {
            float castProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, profile.CastDuration));
            float handAlpha = 1f - Smooth01(castProgress);
            float corePulse = Mathf.Sin(Mathf.Clamp01(castProgress * 1.35f) * Mathf.PI);
            SetRenderer(handFlash, profile.WhiteHotColor, handAlpha, profile.HandFlashBrightness, castProgress, new Vector4(0.12f, -0.08f, 0.07f, 0.04f));
            SetRenderer(handCore, profile.PaleCyanColor, handAlpha * (0.65f + corePulse * 0.35f), profile.FrostCoreBrightness, castProgress * 0.35f, new Vector4(-0.18f, 0.06f, 0.08f, -0.11f));
            handFlash.transform.localScale = Vector3.one * profile.CastHandEffectScale * Mathf.Lerp(0.35f, 1.25f, Smooth01(castProgress));
            handCore.transform.localScale = Vector3.one * profile.FrostCoreSize * Mathf.Lerp(0.7f, 1.12f, corePulse);

            for (int i = 0; i < wristRibbons.Length; i++)
            {
                float phase = Mathf.Repeat(castProgress + i * 0.23f, 1f);
                Renderer ribbon = wristRibbons[i];
                ribbon.transform.localRotation = Quaternion.Euler(0f, elapsed * (260f + i * 70f), i * 22f);
                ribbon.transform.localScale = Vector3.one * profile.CastHandEffectScale * Mathf.Lerp(1f, 0.2f, phase);
                SetRenderer(ribbon, i == 0 ? profile.PaleCyanColor : profile.SaturatedBlueColor, handAlpha, profile.OverallBrightness * 2f, phase * 0.45f, new Vector4(0.35f, 0f, -0.15f, 0.08f));
            }

            float projectileAlpha = hitRequested ? 0f : Smooth01(Mathf.Clamp01(elapsed / 0.035f));
            for (int i = 0; i < projectileLayers.Length; i++)
            {
                Color tint = i == 0 ? profile.WhiteHotColor : i == 1 ? profile.SaturatedBlueColor : profile.PaleCyanColor;
                float brightness = profile.ProjectileBrightness * profile.OverallBrightness * (i == 0 ? 1.35f : i == 1 ? 0.9f : 0.55f);
                SetRenderer(projectileLayers[i], tint, projectileAlpha * (i == 2 ? 0.58f : 1f), brightness, 0f, new Vector4(-1.8f - i * 0.35f, 0.04f, 0.22f, -0.12f));
            }
        }

        private void ApplyProjectileDimensions()
        {
            if (projectileLayers.Length > 0)
            {
                projectileLayers[0].transform.localScale = new Vector3(profile.CoreWidth, profile.CoreWidth, profile.ProjectileLength);
            }

            if (projectileLayers.Length > 1)
            {
                projectileLayers[1].transform.localScale = new Vector3(profile.MainBodyWidth, profile.MainBodyWidth, profile.ProjectileLength * 0.94f);
            }

            if (projectileLayers.Length > 2)
            {
                projectileLayers[2].transform.localScale = new Vector3(profile.OuterGlowWidth, profile.OuterGlowWidth, profile.ProjectileLength * 1.06f);
            }
        }

        private Vector3 ResolveSourcePosition()
        {
            MMOAbilityVfxAnchors sourceAnchors = context.Source.GetComponent<MMOAbilityVfxAnchors>();
            return sourceAnchors != null && context.Definition != null
                ? sourceAnchors.ResolveCastOriginPosition(context.Definition)
                : context.SourcePosition;
        }

        private Vector3 ResolveTargetPosition(Vector3 fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            MMOAbilityVfxAnchors targetAnchors = target.GetComponent<MMOAbilityVfxAnchors>();
            return targetAnchors != null && context.Definition != null
                ? targetAnchors.ResolveHitPosition(context.Definition)
                : target.TransformPoint(context.Definition != null ? context.Definition.HitLocalOffset : new Vector3(0f, 1f, 0f));
        }

        private void PlayParticles()
        {
            ConfigureParticles(releaseFragments, Mathf.RoundToInt(profile.IceFragmentCount * profile.QualityMultiplier), false);
            ConfigureParticles(vaporTrail, Mathf.RoundToInt(18f * profile.QualityMultiplier), true);
            ConfigureParticles(shardTrail, Mathf.RoundToInt(profile.IceFragmentCount * profile.QualityMultiplier), true);
            ConfigureParticles(snowTrail, Mathf.RoundToInt(profile.SnowTrailAmount * profile.QualityMultiplier), true);
            releaseFragments?.Play(true);
            vaporTrail?.Play(true);
            shardTrail?.Play(true);
            snowTrail?.Play(true);
        }

        private static void ConfigureParticles(ParticleSystem system, int amount, bool rate)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate ? Mathf.Max(0, amount) : 0;
            if (!rate)
            {
                emission.SetBursts(amount > 0 ? new[] { new ParticleSystem.Burst(0f, (short)amount) } : System.Array.Empty<ParticleSystem.Burst>());
            }
        }

        private void StopTrails()
        {
            if (trailsStopped)
            {
                return;
            }

            trailsStopped = true;
            vaporTrail?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            shardTrail?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            snowTrail?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void HideProjectile()
        {
            foreach (Renderer renderer in projectileLayers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void Complete()
        {
            initialized = false;
            if (destroyOnComplete)
            {
                MMOAbilityVfxPool.Release(gameObject);
            }
        }

        public void ResetForPool()
        {
            initialized = false;
            hitRequested = false;
            trailsStopped = false;
            target = null;
            releaseFragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            vaporTrail?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            shardTrail?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            snowTrail?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            HideProjectile();
            if (handFlash != null) handFlash.enabled = false;
            if (handCore != null) handCore.enabled = false;
            foreach (Renderer ribbon in wristRibbons)
            {
                if (ribbon != null) ribbon.enabled = false;
            }
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float dissolve, Vector4 scroll)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            propertyBlock.SetVector(ScrollId, scroll);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
            renderer.enabled = opacity > 0.001f;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
