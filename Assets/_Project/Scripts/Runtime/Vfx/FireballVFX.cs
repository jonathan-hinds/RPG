using System;
using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireballVFX : MonoBehaviour, IFireballVFX
    {
        private enum PlaybackState
        {
            Stopped,
            Casting,
            CastRelease,
            Projectile,
            Impact
        }

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");

        [Header("Configuration")]
        [SerializeField] private FireballVFXProfile profile;

        [Header("Casting Effect")]
        [SerializeField] private Transform castingEffectRoot;
        [SerializeField] private Renderer castGlow;
        [SerializeField] private ParticleSystem gatheringEmbers;
        [SerializeField] private ParticleSystem swirlingFlames;
        [SerializeField] private ParticleSystem launchFlash;
        [SerializeField] private ParticleSystem residualEmbers;

        [Header("Projectile Effect")]
        [SerializeField] private Transform projectileEffectRoot;
        [SerializeField] private Renderer hotCore;
        [SerializeField] private Renderer mainFlameBody;
        [SerializeField] private Renderer outerFlameShell;
        [SerializeField] private ParticleSystem projectileEmbers;

        [Header("Trail Effect")]
        [SerializeField] private Transform trailEffectRoot;
        [SerializeField] private TrailRenderer brightFlameTrail;
        [SerializeField] private TrailRenderer outerOrangeTrail;
        [SerializeField] private ParticleSystem trailEmbers;
        [SerializeField] private ParticleSystem smokeTrail;

        [Header("Impact Effect")]
        [SerializeField] private Transform impactEffectRoot;
        [SerializeField] private Renderer impactFlash;
        [SerializeField] private Renderer fireBurst;
        [SerializeField] private Renderer shockwave;
        [SerializeField] private ParticleSystem impactEmbers;
        [SerializeField] private ParticleSystem impactFlames;

        [Header("Aftermath Effect")]
        [SerializeField] private Transform aftermathEffectRoot;
        [SerializeField] private ParticleSystem impactSmoke;
        [SerializeField] private Renderer groundScorch;

        private MaterialPropertyBlock propertyBlock;
        private Transform castPoint;
        private Transform projectileAttachment;
        private PlaybackState state;
        private Camera cachedCamera;
        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private float stateStartedAt;
        private Vector3 previousProjectilePosition;
        private float estimatedSpeed;

        public event Action<FireballVFX> Completed;

        public bool IsPlaying => state != PlaybackState.Stopped;
        public bool ReadyForPool => state == PlaybackState.Stopped;
        public FireballVFXProfile Profile => profile;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            CacheParticleSystems();
            HideAllSections();
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

            UpdateAttachmentPosition();
            switch (state)
            {
                case PlaybackState.Casting:
                    AnimateCasting();
                    break;
                case PlaybackState.CastRelease:
                    AnimateCastRelease();
                    break;
                case PlaybackState.Projectile:
                    AnimateProjectile();
                    break;
                case PlaybackState.Impact:
                    AnimateImpact();
                    break;
            }
        }

        public void SetCastPoint(Transform newCastPoint)
        {
            castPoint = newCastPoint;
        }

        public void AttachToProjectile(Transform projectile)
        {
            projectileAttachment = projectile;
        }

        public void PlayCasting()
        {
            if (!ValidateProfile())
            {
                return;
            }

            StopImmediateInternal(false);
            state = PlaybackState.Casting;
            stateStartedAt = Time.time;
            SetSectionActive(castingEffectRoot, true);
            UpdateAttachmentPosition();
            ApplyParticleBudgets();
            Play(gatheringEmbers);
            Play(swirlingFlames);
            SetRenderer(castGlow, profile.HotColor, 0f, Vector2.zero, 0f);
        }

        public void ReleaseCasting()
        {
            if (state != PlaybackState.Casting)
            {
                return;
            }

            StopEmission(gatheringEmbers);
            StopEmission(swirlingFlames);
            Play(launchFlash);
            Play(residualEmbers);
            state = PlaybackState.CastRelease;
            stateStartedAt = Time.time;
        }

        public void PlayProjectile()
        {
            if (!ValidateProfile())
            {
                return;
            }

            StopImmediateInternal(false);
            state = PlaybackState.Projectile;
            stateStartedAt = Time.time;
            SetSectionActive(projectileEffectRoot, true);
            SetSectionActive(trailEffectRoot, true);
            UpdateAttachmentPosition();
            previousProjectilePosition = transform.position;
            estimatedSpeed = 0f;
            ApplyParticleBudgets();
            ConfigureTrails();
            brightFlameTrail?.Clear();
            outerOrangeTrail?.Clear();
            if (brightFlameTrail != null) brightFlameTrail.emitting = true;
            if (outerOrangeTrail != null) outerOrangeTrail.emitting = true;
            Play(projectileEmbers);
            Play(trailEmbers);
            Play(smokeTrail);
        }

        public void TriggerImpact(Vector3 position, Vector3 surfaceNormal)
        {
            if (!ValidateProfile())
            {
                return;
            }

            StopImmediateInternal(false);
            castPoint = null;
            projectileAttachment = null;
            transform.position = position;
            state = PlaybackState.Impact;
            stateStartedAt = Time.time;
            SetSectionActive(impactEffectRoot, true);
            SetSectionActive(aftermathEffectRoot, true);
            ApplyParticleBudgets();
            if (groundScorch != null)
            {
                groundScorch.gameObject.SetActive(profile.EnableScorch);
                groundScorch.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up);
            }
            Play(impactEmbers);
            Play(impactFlames);
            Play(impactSmoke);
        }

        public void StopImmediate()
        {
            StopImmediateInternal(true);
        }

        public void ResetForPool()
        {
            StopImmediateInternal(false);
            castPoint = null;
            projectileAttachment = null;
        }

        public void ConfigureAuthoring(
            FireballVFXProfile newProfile,
            Transform newCastingEffectRoot,
            Renderer newCastGlow,
            ParticleSystem newGatheringEmbers,
            ParticleSystem newSwirlingFlames,
            ParticleSystem newLaunchFlash,
            ParticleSystem newResidualEmbers,
            Transform newProjectileEffectRoot,
            Renderer newHotCore,
            Renderer newMainFlameBody,
            Renderer newOuterFlameShell,
            ParticleSystem newProjectileEmbers,
            Transform newTrailEffectRoot,
            TrailRenderer newBrightFlameTrail,
            TrailRenderer newOuterOrangeTrail,
            ParticleSystem newTrailEmbers,
            ParticleSystem newSmokeTrail,
            Transform newImpactEffectRoot,
            Renderer newImpactFlash,
            Renderer newFireBurst,
            Renderer newShockwave,
            ParticleSystem newImpactEmbers,
            ParticleSystem newImpactFlames,
            Transform newAftermathEffectRoot,
            ParticleSystem newImpactSmoke,
            Renderer newGroundScorch)
        {
            profile = newProfile;
            castingEffectRoot = newCastingEffectRoot;
            castGlow = newCastGlow;
            gatheringEmbers = newGatheringEmbers;
            swirlingFlames = newSwirlingFlames;
            launchFlash = newLaunchFlash;
            residualEmbers = newResidualEmbers;
            projectileEffectRoot = newProjectileEffectRoot;
            hotCore = newHotCore;
            mainFlameBody = newMainFlameBody;
            outerFlameShell = newOuterFlameShell;
            projectileEmbers = newProjectileEmbers;
            trailEffectRoot = newTrailEffectRoot;
            brightFlameTrail = newBrightFlameTrail;
            outerOrangeTrail = newOuterOrangeTrail;
            trailEmbers = newTrailEmbers;
            smokeTrail = newSmokeTrail;
            impactEffectRoot = newImpactEffectRoot;
            impactFlash = newImpactFlash;
            fireBurst = newFireBurst;
            shockwave = newShockwave;
            impactEmbers = newImpactEmbers;
            impactFlames = newImpactFlames;
            aftermathEffectRoot = newAftermathEffectRoot;
            impactSmoke = newImpactSmoke;
            groundScorch = newGroundScorch;
            CacheParticleSystems();
        }

        private bool ValidateProfile()
        {
            if (profile != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(FireballVFX)} on '{name}' has no profile assigned.", this);
            return false;
        }

        private void UpdateAttachmentPosition()
        {
            Transform attachment = state == PlaybackState.Projectile ? projectileAttachment : castPoint;
            if (attachment != null)
            {
                transform.SetPositionAndRotation(attachment.position, attachment.rotation);
            }
        }

        private void AnimateCasting()
        {
            float elapsed = Time.time - stateStartedAt;
            float normalized = Mathf.Clamp01(elapsed / profile.CastingDuration);
            float pulse = 1f + Mathf.Sin(elapsed * profile.FlickerSpeed) * 0.08f;
            if (castGlow != null)
            {
                castGlow.transform.localScale = Vector3.one * profile.FlameSize * Mathf.Lerp(0.35f, 0.9f, normalized) * pulse;
                Billboard(castGlow.transform, elapsed * 28f);
                SetRenderer(castGlow, profile.FlameColor, Mathf.SmoothStep(0f, 0.72f, normalized), profile.FlameScrollSpeed * elapsed, profile.DistortionAmount);
            }
        }

        private void AnimateCastRelease()
        {
            float normalized = Mathf.Clamp01((Time.time - stateStartedAt) / profile.CastReleaseDuration);
            if (castGlow != null)
            {
                castGlow.transform.localScale = Vector3.one * profile.FlameSize * Mathf.Lerp(0.9f, 1.35f, normalized);
                Billboard(castGlow.transform, normalized * 50f);
                SetRenderer(castGlow, profile.HotColor, 1f - normalized, profile.FlameScrollSpeed * Time.time, profile.DistortionAmount);
            }

            if (normalized >= 1f)
            {
                CompletePlayback();
            }
        }

        private void AnimateProjectile()
        {
            float elapsed = Time.time - stateStartedAt;
            if (Time.deltaTime > 0.0001f)
            {
                float speed = Vector3.Distance(transform.position, previousProjectilePosition) / Time.deltaTime;
                estimatedSpeed = Mathf.Lerp(estimatedSpeed, speed, 0.18f);
                previousProjectilePosition = transform.position;
            }

            float intensity = profile.OverallIntensity;
            float fastPulse = 1f + Mathf.Sin(elapsed * profile.FlickerSpeed) * 0.075f;
            float slowPulse = 1f + Mathf.Sin(elapsed * profile.FlickerSpeed * 0.57f + 1.7f) * 0.11f;
            float stretch = 1f + Mathf.Clamp01(estimatedSpeed / 18f) * profile.DirectionalStretch;

            AnimateProjectileLayer(hotCore, profile.CoreSize, fastPulse, stretch, elapsed * 52f, profile.HotColor, 1f, elapsed);
            AnimateProjectileLayer(mainFlameBody, profile.FlameSize, slowPulse, stretch, -elapsed * 31f, profile.FlameColor, 0.92f, elapsed * 0.82f);
            AnimateProjectileLayer(outerFlameShell, profile.OuterShellSize, 1f + (slowPulse - 1f) * 0.7f, stretch, elapsed * 19f, profile.OuterColor, 0.66f, elapsed * 0.46f);

            SetRenderer(brightFlameTrail, profile.HotColor * profile.TrailBrightness, 0.86f * intensity, new Vector2(-elapsed * 1.7f, 0f), profile.DistortionAmount);
            SetRenderer(outerOrangeTrail, profile.OuterColor * profile.TrailBrightness, 0.7f * intensity, new Vector2(-elapsed * 0.9f, 0f), profile.DistortionAmount * 0.7f);
        }

        private void AnimateProjectileLayer(Renderer renderer, float size, float pulse, float stretch, float roll, Color tint, float opacity, float scrollTime)
        {
            if (renderer == null)
            {
                return;
            }

            float scale = size * profile.ProjectileScale;
            renderer.transform.localScale = new Vector3(scale * pulse, scale * pulse * stretch, 1f);
            Billboard(renderer.transform, roll);
            SetRenderer(renderer, tint, opacity * profile.OverallIntensity, profile.FlameScrollSpeed * scrollTime, profile.DistortionAmount);
        }

        private void AnimateImpact()
        {
            float elapsed = Time.time - stateStartedAt;
            float burstT = Mathf.Clamp01(elapsed / profile.BurstDuration);
            float flashT = Mathf.Clamp01(elapsed / Mathf.Min(0.16f, profile.BurstDuration));
            float shockT = Mathf.Clamp01(elapsed / Mathf.Max(0.12f, profile.BurstDuration * 0.75f));
            AnimateImpactRenderer(impactFlash, profile.ImpactSize * Mathf.Lerp(0.18f, 1.05f, flashT), 1f - flashT, elapsed * 36f, profile.HotColor);
            AnimateImpactRenderer(fireBurst, profile.ImpactSize * Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, burstT)), (1f - burstT) * 0.95f, -elapsed * 42f, Color.Lerp(profile.HotColor, profile.FlameColor, burstT));
            AnimateImpactRenderer(shockwave, profile.ShockwaveSize * Mathf.Lerp(0.15f, 1f, shockT), (1f - shockT) * 0.72f, elapsed * 18f, profile.FlameColor);

            if (groundScorch != null && profile.EnableScorch)
            {
                float scorchT = Mathf.Clamp01(elapsed / profile.ScorchDuration);
                groundScorch.transform.localScale = Vector3.one * profile.ScorchSize;
                SetRenderer(groundScorch, profile.SmokeColor, Mathf.Sin(scorchT * Mathf.PI) * 0.56f, Vector2.zero, 0f);
            }

            float totalDuration = Mathf.Max(profile.AftermathDuration, profile.EnableScorch ? profile.ScorchDuration : 0f);
            if (elapsed >= totalDuration)
            {
                CompletePlayback();
            }
        }

        private void AnimateImpactRenderer(Renderer renderer, float size, float opacity, float roll, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.transform.localScale = Vector3.one * size;
            Billboard(renderer.transform, roll);
            SetRenderer(renderer, color, opacity * profile.OverallIntensity, profile.FlameScrollSpeed * Time.time, profile.DistortionAmount);
        }

        private void Billboard(Transform visual, float rollDegrees)
        {
            if (visual == null)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera != null)
            {
                Vector3 toCamera = cachedCamera.transform.position - visual.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    visual.rotation = Quaternion.LookRotation(toCamera.normalized, cachedCamera.transform.up) * Quaternion.Euler(0f, 0f, rollDegrees);
                }
            }
        }

        private void ConfigureTrails()
        {
            if (brightFlameTrail != null)
            {
                brightFlameTrail.time = profile.TrailLength * 0.65f;
                brightFlameTrail.widthMultiplier = profile.TrailWidth * profile.ProjectileScale * 0.62f;
            }
            if (outerOrangeTrail != null)
            {
                outerOrangeTrail.time = profile.TrailLength;
                outerOrangeTrail.widthMultiplier = profile.TrailWidth * profile.ProjectileScale;
            }
        }

        private void ApplyParticleBudgets()
        {
            SetMaxParticles(projectileEmbers, profile.EmberCount);
            SetMaxParticles(trailEmbers, Mathf.Max(1, profile.EmberCount / 2));
            SetMaxParticles(impactEmbers, profile.EmberCount);
            SetMaxParticles(smokeTrail, profile.SmokeAmount);
            SetMaxParticles(impactSmoke, profile.SmokeAmount);
        }

        private static void SetMaxParticles(ParticleSystem particleSystem, int count)
        {
            if (particleSystem == null) return;
            ParticleSystem.MainModule main = particleSystem.main;
            main.maxParticles = Mathf.Max(1, count);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = count > 0;
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, Vector2 scroll, float distortion)
        {
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetVector(ScrollId, scroll);
            propertyBlock.SetFloat(DistortionId, distortion);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void StopImmediateInternal(bool notify)
        {
            if (brightFlameTrail != null)
            {
                brightFlameTrail.emitting = false;
                brightFlameTrail.Clear();
            }
            if (outerOrangeTrail != null)
            {
                outerOrangeTrail.emitting = false;
                outerOrangeTrail.Clear();
            }

            CacheParticleSystems();
            foreach (ParticleSystem particleSystem in allParticles)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            HideAllSections();
            state = PlaybackState.Stopped;
            if (notify)
            {
                Completed?.Invoke(this);
            }
        }

        private void CompletePlayback()
        {
            StopImmediateInternal(false);
            Completed?.Invoke(this);
        }

        private void HideAllSections()
        {
            SetSectionActive(castingEffectRoot, false);
            SetSectionActive(projectileEffectRoot, false);
            SetSectionActive(trailEffectRoot, false);
            SetSectionActive(impactEffectRoot, false);
            SetSectionActive(aftermathEffectRoot, false);
        }

        private static void SetSectionActive(Transform section, bool active)
        {
            if (section != null) section.gameObject.SetActive(active);
        }

        private static void Play(ParticleSystem particleSystem)
        {
            if (particleSystem == null) return;
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        private static void StopEmission(ParticleSystem particleSystem)
        {
            particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void CacheParticleSystems()
        {
            if (allParticles.Length == 0)
            {
                allParticles = GetComponentsInChildren<ParticleSystem>(true);
            }
        }
    }
}
