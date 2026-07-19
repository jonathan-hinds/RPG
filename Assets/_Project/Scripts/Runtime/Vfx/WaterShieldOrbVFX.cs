using UnityEngine;

namespace RPGClone.Vfx.Water
{
    [DisallowMultipleComponent]
    public sealed class WaterShieldOrbVFX : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int SecondaryTintId = Shader.PropertyToID("_SecondaryTint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int SecondaryScrollSpeedId = Shader.PropertyToID("_SecondaryScrollSpeed");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int WobbleId = Shader.PropertyToID("_WobbleAmount");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int PulseId = Shader.PropertyToID("_PulseAmount");

        [Header("Configuration")]
        [SerializeField] private WaterShieldVFXProfile profile;
        [SerializeField, Range(0, 2)] private int orbIndex;

        [Header("Layered Water Orb")]
        [SerializeField] private Renderer innerCore;
        [SerializeField] private Renderer mainWaterBody;
        [SerializeField] private Renderer secondaryWaterBody;
        [SerializeField] private Renderer outerWaterShell;
        [SerializeField] private Renderer whiteWaterHighlights;
        [SerializeField] private Renderer deepWaterShadow;
        [SerializeField] private Renderer manaEnergy;
        [SerializeField] private Renderer distortionShell;

        [Header("Wake And Particles")]
        [SerializeField] private TrailRenderer mainTrail;
        [SerializeField] private TrailRenderer highlightTrail;
        [SerializeField] private ParticleSystem droplets;
        [SerializeField] private ParticleSystem fineSpray;
        [SerializeField] private ParticleSystem mist;
        [SerializeField] private ParticleSystem waterMotes;
        [SerializeField] private ParticleSystem surfaceSplashes;

        private MaterialPropertyBlock propertyBlock;
        private float startedAt;
        private float reactionStartedAt = float.NegativeInfinity;
        private float manaPulseStartedAt = float.NegativeInfinity;
        private float formationImpactStartedAt = float.NegativeInfinity;
        private float formationProgress;
        private float fade = 1f;
        private bool formationImpactPlayed;
        private bool playing;

        public int OrbIndex => orbIndex;
        public Transform OrbTransform => transform;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            StopImmediate();
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float phase = orbIndex * 1.73f;
            float reaction = EvaluatePulse(reactionStartedAt, 0.42f);
            float manaPulse = EvaluatePulse(manaPulseStartedAt, 0.65f);
            float formationImpact = EvaluatePulse(formationImpactStartedAt, 0.26f);
            float life = Mathf.Clamp01(formationProgress) * fade;
            float wobble = 1f + Mathf.Sin(elapsed * profile.SurfaceWobbleSpeed + phase) * profile.SurfaceWobbleAmount;
            float compression = reaction * profile.OrbCompressionAmount;
            transform.localScale = new Vector3(
                wobble * (1f + compression * 0.55f),
                Mathf.Max(0.65f, 1f / wobble - compression),
                (1f + (wobble - 1f) * 0.72f) * (1f + compression * 0.35f))
                * Mathf.Max(0.001f, profile.OrbScale * BackOut(formationProgress) * (1f + formationImpact * profile.FormationPopScale) * fade);

            if (innerCore != null)
            {
                innerCore.transform.localScale = Vector3.one * profile.InnerCoreScale * (1f + reaction * 0.18f + manaPulse * 0.12f + formationImpact * 0.22f);
                innerCore.transform.Rotate(Vector3.up, profile.InternalRotationSpeed * Time.deltaTime, Space.Self);
            }

            outerWaterShell?.transform.Rotate(Vector3.up, profile.OuterShellRotationSpeed * Time.deltaTime, Space.Self);
            whiteWaterHighlights?.transform.Rotate(new Vector3(0.3f, 1f, 0.2f), -profile.OuterShellRotationSpeed * 0.7f * Time.deltaTime, Space.Self);
            secondaryWaterBody?.transform.Rotate(new Vector3(0.25f, 0.8f, 0.4f), profile.InternalRotationSpeed * 0.63f * Time.deltaTime, Space.Self);

            float brightness = profile.OverallBrightness * (1f + formationImpact * profile.FormationPopBrightness);
            ApplyLayer(innerCore, profile.Colors.PaleCyan, profile.Colors.Aqua, life, profile.InnerCoreBrightness * brightness * (1f + reaction + manaPulse * 0.65f), profile.MainTextureScrollSpeed, profile.SecondaryTextureScrollSpeed, 0.02f, 0.025f, 1f - life, manaPulse);
            ApplyLayer(mainWaterBody, profile.Colors.ClearBlue, profile.Colors.Aqua, profile.MainWaterOpacity * life, brightness * (1f + reaction * 0.48f), profile.MainTextureScrollSpeed, profile.SecondaryTextureScrollSpeed, profile.WaterDistortionStrength, profile.SurfaceWobbleAmount, 1f - life, 0.08f);
            ApplyLayer(secondaryWaterBody, profile.Colors.Teal, profile.Colors.ClearBlue, profile.MainWaterOpacity * 0.46f * life, brightness, profile.SecondaryTextureScrollSpeed, -profile.MainTextureScrollSpeed, profile.WaterDistortionStrength * 0.7f, profile.SurfaceWobbleAmount * 0.8f, 1f - life, 0.12f);
            ApplyLayer(outerWaterShell, profile.Colors.Aqua, profile.Colors.PaleCyan, profile.OuterShellOpacity * life, brightness * 1.15f, profile.SecondaryTextureScrollSpeed, profile.SurfaceHighlightSpeed, profile.WaterDistortionStrength, profile.SurfaceWobbleAmount * 1.25f, 1f - life, 0.18f);
            ApplyLayer(whiteWaterHighlights, profile.Colors.WhiteHighlight, profile.Colors.PaleCyan, 0.72f * life, profile.HighlightBrightness * brightness * (1f + reaction * 0.7f), profile.SurfaceHighlightSpeed, -profile.SurfaceHighlightSpeed, 0.015f, profile.SurfaceWobbleAmount, 1f - life, 0.14f);
            ApplyLayer(deepWaterShadow, profile.Colors.DeepBlue, profile.Colors.ClearBlue, 0.58f * life, profile.DeepWaterIntensity * brightness, profile.MainTextureScrollSpeed * 0.38f, profile.SecondaryTextureScrollSpeed * 0.25f, 0.02f, profile.SurfaceWobbleAmount * 0.5f, 1f - life, 0.04f);
            ApplyLayer(manaEnergy, profile.Colors.Aqua, profile.Colors.ManaViolet, 0.44f * life, profile.ManaEnergyIntensity * brightness * (1f + manaPulse), profile.SecondaryTextureScrollSpeed * 1.3f, profile.SurfaceHighlightSpeed, 0.025f, 0.025f, 1f - life, manaPulse * 0.5f);
            ApplyLayer(distortionShell, Color.white, Color.white, profile.OuterShellOpacity * 0.28f * life, 1f, profile.DistortionScrollSpeed, -profile.DistortionScrollSpeed, profile.WaterDistortionStrength * (1f + reaction), profile.SurfaceWobbleAmount, 1f - life, 0f);
            UpdateTrails(life, reaction);
            UpdateParticleRates(life);
        }

        public void Play(WaterShieldVFXProfile newProfile, int newOrbIndex)
        {
            profile = newProfile != null ? newProfile : profile;
            orbIndex = Mathf.Clamp(newOrbIndex, 0, 2);
            if (profile == null)
            {
                Debug.LogError("WaterShieldOrbVFX requires a profile.", this);
                return;
            }

            startedAt = Time.time;
            reactionStartedAt = float.NegativeInfinity;
            manaPulseStartedAt = float.NegativeInfinity;
            formationImpactStartedAt = float.NegativeInfinity;
            formationProgress = 0f;
            fade = 1f;
            formationImpactPlayed = false;
            playing = true;
            transform.localScale = Vector3.zero;
            PlayParticles(droplets);
            PlayParticles(fineSpray);
            PlayParticles(mist);
            PlayParticles(waterMotes);
            if (surfaceSplashes != null)
            {
                surfaceSplashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            ConfigureTrail(mainTrail, profile.TrailLength, profile.TrailWidth, profile.TrailOpacity);
            ConfigureTrail(highlightTrail, profile.TrailLength * 0.78f, profile.HighlightTrailWidth, 0.9f);
        }

        public void SetFormationProgress(float value)
        {
            formationProgress = Mathf.Clamp01(value);
            if (!formationImpactPlayed && formationProgress >= 0.68f)
            {
                formationImpactPlayed = true;
                formationImpactStartedAt = Time.time;
                int splashCount = profile != null ? profile.FormationSplashAmount : 10;
                surfaceSplashes?.Emit(splashCount);
                droplets?.Emit(Mathf.Max(4, splashCount / 2));
                fineSpray?.Emit(Mathf.Max(3, splashCount / 3));
            }
        }

        public void PulseAbsorb()
        {
            if (!playing)
            {
                return;
            }

            reactionStartedAt = Time.time;
            surfaceSplashes?.Emit(profile != null ? profile.ReactiveSplashAmount : 8);
            droplets?.Emit(profile != null ? Mathf.Max(4, profile.ReactiveSplashAmount / 2) : 5);
        }

        public void PulseMana()
        {
            if (playing)
            {
                manaPulseStartedAt = Time.time;
            }
        }

        public void SetFade(float value)
        {
            fade = Mathf.Clamp01(value);
            if (fade <= 0f)
            {
                StopEmission(droplets);
                StopEmission(fineSpray);
                StopEmission(mist);
                StopEmission(waterMotes);
            }
        }

        public void StopImmediate()
        {
            playing = false;
            StopAndClear(droplets);
            StopAndClear(fineSpray);
            StopAndClear(mist);
            StopAndClear(waterMotes);
            StopAndClear(surfaceSplashes);
            ClearTrail(mainTrail);
            ClearTrail(highlightTrail);
            transform.localScale = Vector3.zero;
        }

        public void ConfigureAuthoring(
            WaterShieldVFXProfile newProfile,
            Renderer newInnerCore,
            Renderer newMainWaterBody,
            Renderer newSecondaryWaterBody,
            Renderer newOuterWaterShell,
            Renderer newWhiteWaterHighlights,
            Renderer newDeepWaterShadow,
            Renderer newManaEnergy,
            Renderer newDistortionShell,
            TrailRenderer newMainTrail,
            TrailRenderer newHighlightTrail,
            ParticleSystem newDroplets,
            ParticleSystem newFineSpray,
            ParticleSystem newMist,
            ParticleSystem newWaterMotes,
            ParticleSystem newSurfaceSplashes)
        {
            profile = newProfile;
            innerCore = newInnerCore;
            mainWaterBody = newMainWaterBody;
            secondaryWaterBody = newSecondaryWaterBody;
            outerWaterShell = newOuterWaterShell;
            whiteWaterHighlights = newWhiteWaterHighlights;
            deepWaterShadow = newDeepWaterShadow;
            manaEnergy = newManaEnergy;
            distortionShell = newDistortionShell;
            mainTrail = newMainTrail;
            highlightTrail = newHighlightTrail;
            droplets = newDroplets;
            fineSpray = newFineSpray;
            mist = newMist;
            waterMotes = newWaterMotes;
            surfaceSplashes = newSurfaceSplashes;
        }

        private void UpdateTrails(float life, float reaction)
        {
            if (mainTrail != null)
            {
                mainTrail.emitting = playing && formationProgress > 0.72f && fade > 0.02f;
                mainTrail.time = profile.TrailLength * (1f + reaction * 0.48f);
                mainTrail.widthMultiplier = profile.TrailWidth * life * (1f + reaction * 0.25f);
            }

            if (highlightTrail != null)
            {
                highlightTrail.emitting = playing && formationProgress > 0.78f && fade > 0.02f;
                highlightTrail.time = profile.TrailLength * 0.78f * (1f + reaction * 0.35f);
                highlightTrail.widthMultiplier = profile.HighlightTrailWidth * life;
            }
        }

        private void UpdateParticleRates(float life)
        {
            SetEmission(droplets, profile.DropletSpawnRate * life);
            SetEmission(fineSpray, profile.FineSprayAmount * life);
            SetEmission(mist, profile.MistAmount * life);
            SetEmission(waterMotes, profile.WaterMoteCount * 0.55f * life);
            SetEmission(surfaceSplashes, profile.SplashFrequency * life);
        }

        private void ApplyLayer(Renderer renderer, Color tint, Color secondaryTint, float opacity, float brightness, Vector2 scroll, Vector2 secondaryScroll, float distortion, float wobble, float dissolve, float pulse)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetColor(SecondaryTintId, secondaryTint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetVector(ScrollSpeedId, scroll);
            propertyBlock.SetVector(SecondaryScrollSpeedId, secondaryScroll);
            propertyBlock.SetFloat(DistortionId, Mathf.Max(0f, distortion));
            propertyBlock.SetFloat(WobbleId, Mathf.Max(0f, wobble));
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            propertyBlock.SetFloat(PulseId, Mathf.Clamp01(pulse));
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private static float EvaluatePulse(float startedAt, float duration)
        {
            float t = (Time.time - startedAt) / Mathf.Max(0.01f, duration);
            return t is >= 0f and < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
        }

        private static float BackOut(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.28f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private static void ConfigureTrail(TrailRenderer trail, float time, float width, float opacity)
        {
            if (trail == null)
            {
                return;
            }

            trail.Clear();
            trail.time = time;
            trail.widthMultiplier = width;
            trail.emitting = false;
            Color color = Color.white;
            color.a = opacity;
            trail.startColor = color;
            color.a = 0f;
            trail.endColor = color;
        }

        private static void SetEmission(ParticleSystem particles, float rate)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        private static void PlayParticles(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void StopEmission(ParticleSystem particles)
        {
            particles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void StopAndClear(ParticleSystem particles)
        {
            particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ClearTrail(TrailRenderer trail)
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }
    }
}
