using System;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class BashVFX : MonoBehaviour, IBashVFX, IMMOAbilityVfxInstance
    {
        [Header("Configuration")]
        [SerializeField] private BashVFXProfile profile;

        [Header("Visual Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform impactLayersRoot;
        [SerializeField] private Transform groundReactionRoot;
        [SerializeField] private Transform stunAccentRoot;

        [Header("Impact Layers")]
        [SerializeField] private ParticleSystem swingAccent;
        [SerializeField] private ParticleSystem impactBackplate;
        [SerializeField] private ParticleSystem impactFlash;
        [SerializeField] private ParticleSystem heavyImpactBurst;
        [SerializeField] private ParticleSystem secondaryImpactBurst;
        [SerializeField] private ParticleSystem directionalForceBurst;
        [SerializeField] private ParticleSystem momentumStreaks;
        [SerializeField] private ParticleSystem armorSparks;

        [Header("Ground Layers")]
        [SerializeField] private ParticleSystem dustBurst;
        [SerializeField] private ParticleSystem radialDustRing;
        [SerializeField] private ParticleSystem groundDebris;

        [Header("Stun Layer")]
        [SerializeField] private ParticleSystem stunStars;

        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private bool isPlaying;
        private bool stunPlaying;
        private float startedAt;
        private float baseStunHeight;
        private Vector3 impactDirection = Vector3.forward;

        public event Action<BashVFX> Completed;

        public bool IsPlaying => isPlaying;
        public bool ReadyForPool => !isPlaying;
        public BashVFXProfile Profile => profile;

        private void Awake()
        {
            CacheParticles();
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

            float elapsed = Time.time - startedAt;
            AnimateImpactPunch(elapsed);
            if (stunPlaying && stunAccentRoot != null)
            {
                float bob = Mathf.Sin(elapsed * profile.StunBobSpeed) * profile.StunBobAmount;
                Vector3 position = stunAccentRoot.localPosition;
                position.y = baseStunHeight + bob;
                stunAccentRoot.localPosition = position;
                stunAccentRoot.Rotate(Vector3.up, profile.StunOrbitDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            float totalDuration = stunPlaying
                ? Mathf.Max(profile.DustDuration + profile.GroundReactionDelay, profile.StunDuration + profile.StunDelay)
                : Mathf.Max(profile.DustDuration, profile.DustRingDuration);
            if (elapsed >= totalDuration + 0.08f)
            {
                CompletePlayback();
            }
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            Vector3 direction = context.TargetPosition - context.SourcePosition;
            SetImpactDirection(direction);
            Play(profile != null && profile.ShowStunAccentByDefault);
        }

        public void Play(bool stunApplied)
        {
            if (!ValidateProfile())
            {
                return;
            }

            StopImmediateInternal(false);
            ApplyProfile();
            isPlaying = true;
            stunPlaying = stunApplied && profile.StunStarCount > 0;
            startedAt = Time.time;

            PlayOneShot(swingAccent);
            PlayOneShot(impactBackplate);
            PlayOneShot(impactFlash);
            PlayOneShot(heavyImpactBurst);
            PlayOneShot(secondaryImpactBurst);
            PlayOneShot(directionalForceBurst);
            PlayOneShot(momentumStreaks);
            PlayOneShot(armorSparks);
            PlayOneShot(dustBurst);
            PlayOneShot(radialDustRing);
            PlayOneShot(groundDebris);
            if (stunPlaying)
            {
                PlayOneShot(stunStars);
            }
        }

        public void SetImpactDirection(Vector3 direction)
        {
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planar.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            impactDirection = planar.normalized;
            transform.rotation = Quaternion.LookRotation(impactDirection, Vector3.up);
        }

        public void StopImmediate()
        {
            StopImmediateInternal(true);
        }

        public void ResetForPool()
        {
            StopImmediateInternal(false);
            impactDirection = Vector3.forward;
        }

        public void ConfigureAuthoring(
            BashVFXProfile newProfile,
            Transform newVisualRoot,
            Transform newImpactLayersRoot,
            Transform newGroundReactionRoot,
            Transform newStunAccentRoot,
            ParticleSystem newSwingAccent,
            ParticleSystem newImpactBackplate,
            ParticleSystem newImpactFlash,
            ParticleSystem newHeavyImpactBurst,
            ParticleSystem newSecondaryImpactBurst,
            ParticleSystem newDirectionalForceBurst,
            ParticleSystem newMomentumStreaks,
            ParticleSystem newArmorSparks,
            ParticleSystem newDustBurst,
            ParticleSystem newRadialDustRing,
            ParticleSystem newGroundDebris,
            ParticleSystem newStunStars)
        {
            profile = newProfile;
            visualRoot = newVisualRoot;
            impactLayersRoot = newImpactLayersRoot;
            groundReactionRoot = newGroundReactionRoot;
            stunAccentRoot = newStunAccentRoot;
            swingAccent = newSwingAccent;
            impactBackplate = newImpactBackplate;
            impactFlash = newImpactFlash;
            heavyImpactBurst = newHeavyImpactBurst;
            secondaryImpactBurst = newSecondaryImpactBurst;
            directionalForceBurst = newDirectionalForceBurst;
            momentumStreaks = newMomentumStreaks;
            armorSparks = newArmorSparks;
            dustBurst = newDustBurst;
            radialDustRing = newRadialDustRing;
            groundDebris = newGroundDebris;
            stunStars = newStunStars;
            CacheParticles(true);
        }

        private bool ValidateProfile()
        {
            if (profile != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(BashVFX)} on '{name}' has no profile assigned.", this);
            return false;
        }

        private void ApplyProfile()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * profile.OverallScale;
            }

            Color flash = ApplyMaster(profile.FlashColor, profile.FlashIntensity);
            Color impact = ApplyMaster(profile.ImpactColor, 1f);
            Color backplate = ApplyMaster(profile.ImpactBackplateColor, 1f);
            Color secondary = ApplyMaster(profile.SecondaryImpactColor, 1f);
            Color dust = ApplyMaster(profile.DustColor, 1f);
            Color debris = ApplyMaster(profile.DebrisColor, 1f);
            Color spark = ApplyMaster(profile.SparkColor, 1f);
            Color stun = ApplyMaster(profile.StunColor, 1f);

            ConfigureOneShot(swingAccent, 1, profile.SwingDuration, 0f, profile.SwingArcSize, impact, 0f);
            ConfigureOneShot(impactBackplate, Mathf.Max(3, profile.BurstPieceCount / 2), profile.ImpactDuration, 0.62f, profile.ImpactBackplateSize, backplate, 0.008f);
            ConfigureOneShot(impactFlash, 1, profile.FlashDuration, 0f, profile.FlashSize, flash, 0.012f);
            ConfigureOneShot(heavyImpactBurst, profile.BurstPieceCount, profile.ImpactDuration, 1.15f, profile.ImpactBurstSize, impact, 0.018f);
            ConfigureOneShot(
                secondaryImpactBurst,
                Mathf.Max(3, profile.BurstPieceCount / 2),
                profile.ImpactDuration * 0.9f,
                1.45f,
                profile.SecondaryBurstSize,
                secondary,
                profile.SecondaryBurstDelay);
            ConfigureOneShot(
                directionalForceBurst,
                Mathf.Max(1, profile.BurstPieceCount / 2),
                profile.ImpactDuration,
                profile.DirectionalForceDistance / Mathf.Max(0.05f, profile.ImpactDuration),
                profile.ImpactBurstSize * 0.58f,
                impact,
                0.026f);
            ConfigureOneShot(momentumStreaks, profile.MomentumStreakCount, profile.ImpactDuration * 0.72f, profile.MomentumStreakSpeed, profile.SparkSize * 0.72f, spark, 0.03f);
            ConfigureOneShot(armorSparks, profile.SparkCount, profile.ImpactDuration, profile.SparkSpeed, profile.SparkSize, spark, 0.022f);
            ConfigureOneShot(dustBurst, profile.DustAmount, profile.DustDuration, 0.9f, profile.DustSize, dust, profile.GroundReactionDelay);

            Color ringColor = dust;
            ringColor.a *= profile.DustRingOpacity;
            ConfigureOneShot(radialDustRing, 1, profile.DustRingDuration, 0f, profile.DustRingRadius, ringColor, profile.GroundReactionDelay);
            ConfigureScaleCurve(radialDustRing, profile.DustRingStartScale, 1f, 1.08f);
            ConfigureOneShot(groundDebris, profile.DebrisCount, profile.DustDuration * 0.9f, profile.DebrisSpeed, profile.DustSize * 0.34f, debris, profile.GroundReactionDelay);

            ConfigureOneShot(stunStars, profile.StunStarCount, profile.StunDuration, 0f, profile.StunStarSize, stun, profile.StunDelay);
            if (stunStars != null)
            {
                ParticleSystem.ShapeModule shape = stunStars.shape;
                shape.radius = profile.StunOrbitRadius;
            }

            if (stunAccentRoot != null)
            {
                Vector3 position = stunAccentRoot.localPosition;
                position.y = profile.StunHeight;
                stunAccentRoot.localPosition = position;
                stunAccentRoot.localRotation = Quaternion.identity;
                baseStunHeight = position.y;
            }

            if (impactLayersRoot != null)
            {
                impactLayersRoot.localScale = Vector3.one * (1f - profile.ImpactPunchOvershoot * 0.4f);
            }
        }

        private void AnimateImpactPunch(float elapsed)
        {
            if (impactLayersRoot == null || profile.ImpactPunchDuration <= 0f)
            {
                return;
            }

            float normalized = Mathf.Clamp01(elapsed / profile.ImpactPunchDuration);
            float peak = 0.34f;
            float scale;
            if (normalized < peak)
            {
                float rise = Mathf.SmoothStep(0f, 1f, normalized / peak);
                scale = Mathf.Lerp(1f - profile.ImpactPunchOvershoot * 0.4f, 1f + profile.ImpactPunchOvershoot, rise);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, (normalized - peak) / (1f - peak));
                scale = Mathf.Lerp(1f + profile.ImpactPunchOvershoot, 1f, settle);
            }

            impactLayersRoot.localScale = Vector3.one * scale;
        }

        private Color ApplyMaster(Color color, float layerIntensity)
        {
            Color tint = profile.ColorTint;
            Color result = new(
                color.r * tint.r,
                color.g * tint.g,
                color.b * tint.b,
                color.a * tint.a);
            float brightness = profile.OverallBrightness * layerIntensity;
            result.r *= brightness;
            result.g *= brightness;
            result.b *= brightness;
            return result;
        }

        private static void ConfigureOneShot(
            ParticleSystem particleSystem,
            int count,
            float lifetime,
            float speed,
            float size,
            Color color,
            float delay)
        {
            if (particleSystem == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 0, short.MaxValue);
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.startDelay = Mathf.Max(0f, delay);
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSpeed = Mathf.Max(0f, speed);
            float safeSize = Mathf.Max(0.01f, size);
            main.startSize = safeCount > 1
                ? new ParticleSystem.MinMaxCurve(safeSize * 0.72f, safeSize * 1.18f)
                : new ParticleSystem.MinMaxCurve(safeSize);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, safeCount);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = safeCount > 0;
            emission.rateOverTime = 0f;
            emission.SetBursts(safeCount > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)safeCount) }
                : Array.Empty<ParticleSystem.Burst>());
        }

        private static void ConfigureScaleCurve(ParticleSystem particleSystem, float start, float middle, float end)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(0.58f, middle),
                new Keyframe(1f, end)));
        }

        private static void PlayOneShot(ParticleSystem particleSystem)
        {
            if (particleSystem == null || !particleSystem.emission.enabled)
            {
                return;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }

        private void CompletePlayback()
        {
            StopImmediateInternal(false);
            Completed?.Invoke(this);
        }

        private void StopImmediateInternal(bool notify)
        {
            CacheParticles();
            foreach (ParticleSystem particleSystem in allParticles)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            isPlaying = false;
            stunPlaying = false;
            if (impactLayersRoot != null)
            {
                impactLayersRoot.localScale = Vector3.one;
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
