using System;
using UnityEngine;

namespace RPGClone.Vfx.Physical
{
    [DisallowMultipleComponent]
    public sealed class GougeCastVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        [Header("Configuration")]
        [SerializeField] private GougeVFXProfile profile;

        [Header("Physical Attack Layers")]
        [SerializeField] private Transform motionRoot;
        [SerializeField] private ParticleSystem mainTrail;
        [SerializeField] private ParticleSystem tearingTrail;
        [SerializeField] private ParticleSystem[] weaponGlints = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem motionFragments;
        [SerializeField] private ParticleSystem armDust;

        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private Transform source;
        private Transform handAnchor;
        private Vector3 targetPosition;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            CacheParticles();
            StopAll();
        }

        private void OnDisable()
        {
            StopAll();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            source = context.Source;
            targetPosition = context.TargetPosition;
            if (profile == null || source == null)
            {
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            MMOAbilityVfxAnchors anchors = source.GetComponent<MMOAbilityVfxAnchors>();
            handAnchor = anchors != null ? anchors.RightHandAnchor : source;
            ApplyProfile();
            UpdateAttachment();
            startedAt = Time.time;
            playing = true;
            PlayOneShot(mainTrail);
            PlayOneShot(tearingTrail);
            foreach (ParticleSystem glint in weaponGlints)
            {
                PlayOneShot(glint);
            }

            PlayOneShot(motionFragments);
            PlayOneShot(armDust);
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            UpdateAttachment();
            if (Time.time - startedAt >= profile.AttackMotionDuration + 0.12f)
            {
                playing = false;
                MMOAbilityVfxPool.Release(gameObject);
            }
        }

        public void ConfigureAuthoring(
            GougeVFXProfile newProfile,
            Transform newMotionRoot,
            ParticleSystem newMainTrail,
            ParticleSystem newTearingTrail,
            ParticleSystem[] newWeaponGlints,
            ParticleSystem newMotionFragments,
            ParticleSystem newArmDust)
        {
            profile = newProfile;
            motionRoot = newMotionRoot;
            mainTrail = newMainTrail;
            tearingTrail = newTearingTrail;
            weaponGlints = newWeaponGlints ?? Array.Empty<ParticleSystem>();
            motionFragments = newMotionFragments;
            armDust = newArmDust;
            CacheParticles();
        }

        public void ResetForPool()
        {
            playing = false;
            source = null;
            handAnchor = null;
            StopAll();
        }

        private void ApplyProfile()
        {
            ConfigureOneShot(mainTrail, 1, profile.AttackMotionDuration, profile.MainTrailLength, profile.MainTrailWidth,
                Brighten(profile.Colors.ImpactWhite, profile.MainTrailBrightness));
            ConfigureOneShot(tearingTrail, 1, profile.AttackMotionDuration * 0.72f, profile.MainTrailLength * 0.82f,
                profile.TearingTrailWidth, Brighten(profile.TearingTrailColor, 1f));
            for (int i = 0; i < weaponGlints.Length; i++)
            {
                int count = i < profile.WeaponGlintCount ? 1 : 0;
                ConfigureOneShot(weaponGlints[i], count, 0.12f, 0.32f, 0.16f,
                    Brighten(profile.Colors.Metallic, 1.35f));
            }

            ConfigureOneShot(motionFragments, profile.MotionFragmentAmount, 0.34f, 0.18f, 0.1f,
                new Color(0.32f, 0.24f, 0.18f, 0.66f));
            ConfigureOneShot(armDust, profile.DustBurstAmount, 0.32f, 0.24f, 0.2f,
                new Color(0.52f, 0.42f, 0.32f, 0.48f));
        }

        private void UpdateAttachment()
        {
            if (motionRoot == null || source == null)
            {
                return;
            }

            Vector3 origin = handAnchor != null ? handAnchor.position : source.position + Vector3.up;
            Vector3 direction = targetPosition - origin;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = source.forward;
            }

            motionRoot.SetPositionAndRotation(origin, Quaternion.LookRotation(direction.normalized, Vector3.up));
        }

        private Color Brighten(Color color, float multiplier)
        {
            float brightness = profile.OverallBrightness * multiplier;
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            return color;
        }

        private static void ConfigureOneShot(
            ParticleSystem system,
            int count,
            float lifetime,
            float length,
            float width,
            Color color)
        {
            if (system == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 0, short.MaxValue);
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, safeCount);
            main.startSize3D = true;
            main.startSizeX = Mathf.Max(0.01f, length);
            main.startSizeY = Mathf.Max(0.01f, width);
            main.startSizeZ = 1f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = safeCount > 0;
            emission.rateOverTime = 0f;
            emission.SetBursts(safeCount > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)safeCount) }
                : Array.Empty<ParticleSystem.Burst>());
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

        private void CacheParticles()
        {
            allParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void StopAll()
        {
            foreach (ParticleSystem system in allParticles)
            {
                system?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
