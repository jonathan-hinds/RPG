using System;
using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    /// <summary>
    /// Seeds upright vapor and ice formations along the moving Frost Wave front.
    /// This mirrors the proven Earthquake/Thunderclap wake pattern without
    /// turning the cloud into a ground-projected decal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrostWaveRadialFrontVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem radialCloud;
        [SerializeField] private ParticleSystem iceBreakers;

        private FrostWaveVFXProfile profile;
        private System.Random random;
        private Vector3 center;
        private float radius;
        private float startedAt;
        private int emittedCloud;
        private int emittedIce;
        private bool playing;

        public void ConfigureAuthoring(ParticleSystem newRadialCloud, ParticleSystem newIceBreakers)
        {
            radialCloud = newRadialCloud;
            iceBreakers = newIceBreakers;
        }

        public void Play(FrostWaveVFXProfile newProfile, float newRadius)
        {
            ResetForPool();
            profile = newProfile;
            radius = Mathf.Max(0.1f, newRadius);
            center = transform.position;
            startedAt = Time.time;
            emittedCloud = 0;
            emittedIce = 0;
            random = new System.Random(BuildSeed(center, startedAt));
            playing = profile != null;
            if (!playing)
            {
                return;
            }

            Prepare(radialCloud);
            Prepare(iceBreakers);
            EmitOpeningBurst();
        }

        public void ResetForPool()
        {
            playing = false;
            profile = null;
            StopAndClear(radialCloud);
            StopAndClear(iceBreakers);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float localTime = Time.time - startedAt - profile.PrimaryRingDelay;
            if (localTime < 0f)
            {
                return;
            }

            float progress = Mathf.Clamp01(localTime / Mathf.Max(0.01f, profile.RingExpansionDuration));
            EmitMovingFront(progress);
            if (progress >= 1f)
            {
                playing = false;
            }
        }

        private void EmitOpeningBurst()
        {
            int cloudCount = Mathf.Max(8, profile.RadialCloudDensity / 5);
            for (int i = 0; i < cloudCount; i++)
            {
                float angle = (i + Next01() * 0.35f) / cloudCount * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                EmitCloud(
                    center + radial * Mathf.Lerp(0.18f, 0.52f, Next01()) + Vector3.up * 0.12f,
                    radial * Mathf.Lerp(4.2f, 6.4f, Next01()) + Vector3.up * Mathf.Lerp(0.55f, 1.35f, Next01()),
                    profile.RadialCloudSize * Mathf.Lerp(0.42f, 0.68f, Next01()),
                    profile.RadialCloudLifetime * Mathf.Lerp(0.58f, 0.82f, Next01()));
            }
        }

        private void EmitMovingFront(float progress)
        {
            int desiredCloud = Mathf.FloorToInt(profile.RadialCloudDensity * progress);
            for (int index = emittedCloud; index < desiredCloud; index++)
            {
                float angle = (index + Next01() * 0.48f) / Mathf.Max(1, profile.RadialCloudDensity) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float frontRadius = Mathf.Max(0.28f, radius * progress + NextSigned() * 0.24f);
                Vector3 position = center + radial * frontRadius + Vector3.up * Mathf.Lerp(0.1f, 0.28f, Next01());
                Vector3 velocity = radial * profile.RadialCloudDrift * Mathf.Lerp(0.72f, 1.25f, Next01())
                    + Vector3.up * profile.RadialCloudLift * Mathf.Lerp(0.45f, 1.15f, Next01());
                EmitCloud(
                    position,
                    velocity,
                    profile.RadialCloudSize * Mathf.Lerp(0.78f, 1.24f, Next01()),
                    profile.RadialCloudLifetime * Mathf.Lerp(0.78f, 1.18f, Next01()));
            }
            emittedCloud = desiredCloud;

            int desiredIce = Mathf.FloorToInt(profile.IceBreakerDensity * progress);
            for (int index = emittedIce; index < desiredIce; index++)
            {
                float angle = (index + 0.35f + Next01() * 0.3f) / Mathf.Max(1, profile.IceBreakerDensity) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float frontRadius = Mathf.Max(0.35f, radius * progress - Mathf.Lerp(0.08f, 0.42f, Next01()));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * frontRadius + Vector3.up * 0.04f,
                    velocity = radial * Mathf.Lerp(0.3f, 0.8f, Next01()) + Vector3.up * Mathf.Lerp(0.08f, 0.32f, Next01()),
                    startSize = profile.IceBreakerSize * Mathf.Lerp(0.72f, 1.28f, Next01()),
                    startLifetime = profile.IceBreakerLifetime * Mathf.Lerp(0.82f, 1.16f, Next01()),
                    startColor = Color.Lerp(profile.SaturatedBlue, profile.WhiteHot, Mathf.Lerp(0.18f, 0.52f, Next01())),
                    rotation = NextSigned() * 0.08f
                };
                iceBreakers?.Emit(emit, 1);
            }
            emittedIce = desiredIce;
        }

        private void EmitCloud(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            if (radialCloud == null)
            {
                return;
            }

            Color tint = profile.MistTint;
            tint.a = Mathf.Lerp(0.72f, 0.96f, Next01());
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = tint,
                rotation = NextSigned() * 0.1f
            };
            radialCloud.Emit(emit, 1);
        }

        private static void Prepare(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
        }

        private static void StopAndClear(ParticleSystem system)
        {
            system?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private float Next01()
        {
            return random != null ? (float)random.NextDouble() : UnityEngine.Random.value;
        }

        private float NextSigned()
        {
            return Next01() * 2f - 1f;
        }

        private static int BuildSeed(Vector3 position, float time)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + Mathf.RoundToInt(position.x * 10f);
                seed = seed * 31 + Mathf.RoundToInt(position.z * 10f);
                seed = seed * 31 + Mathf.RoundToInt(time * 100f);
                return seed;
            }
        }
    }
}
