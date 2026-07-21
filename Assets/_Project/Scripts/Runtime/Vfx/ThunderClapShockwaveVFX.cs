using System;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapShockwaveVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem pressureRing;
        [SerializeField] private ParticleSystem physicalShockwave;
        [SerializeField] private ParticleSystem rollingDustWall;
        [SerializeField] private ParticleSystem lightningRing;
        [SerializeField] private ParticleSystem airDistortion;
        [SerializeField] private ParticleSystem dirtWake;
        [SerializeField] private ParticleSystem electricalSparks;
        [SerializeField] private LineRenderer[] groundCrawlers = Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] secondaryStrikes = Array.Empty<LineRenderer>();

        private ThunderClapVFXProfile profile;
        private readonly Vector3[][] crawlerPaths = new Vector3[12][];
        private readonly Vector3[][] strikePaths = new Vector3[12][];
        private System.Random random;
        private Vector3 center;
        private float startedAt;
        private float nextRefresh;
        private int emittedWake;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(
            ParticleSystem[] systems,
            LineRenderer[] newGroundCrawlers,
            LineRenderer[] newSecondaryStrikes)
        {
            if (systems == null || systems.Length != 7)
            {
                throw new ArgumentException("Thunder Clap shockwave requires exactly seven particle layers.");
            }

            pressureRing = systems[0];
            physicalShockwave = systems[1];
            rollingDustWall = systems[2];
            lightningRing = systems[3];
            airDistortion = systems[4];
            dirtWake = systems[5];
            electricalSparks = systems[6];
            groundCrawlers = newGroundCrawlers ?? Array.Empty<LineRenderer>();
            secondaryStrikes = newSecondaryStrikes ?? Array.Empty<LineRenderer>();
        }

        public void Play(ThunderClapVFXProfile newProfile, Vector3 position)
        {
            profile = newProfile;
            if (profile == null)
            {
                return;
            }

            gameObject.SetActive(true);
            center = position;
            transform.position = position;
            startedAt = Time.time;
            nextRefresh = 0f;
            emittedWake = 0;
            playing = true;
            random = new System.Random(GetHashCode() ^ System.Environment.TickCount);
            EnsurePathPools();
            SetLines(false);

            float diameter = profile.RingRadius * 2f;
            EmitExpandingRing(pressureRing, diameter * 1.05f, new Color(0.8f, 0.94f, 1f, 0.26f * profile.PressureIntensity));
            EmitExpandingRing(physicalShockwave, diameter, profile.WarmDustColor);
            EmitExpandingRing(rollingDustWall, diameter * 0.97f, profile.WarmDustColor);
            EmitExpandingRing(lightningRing, diameter * 0.92f, profile.LightningColor);
            EmitExpandingRing(airDistortion, diameter * 1.08f, new Color(1f, 1f, 1f, 0.2f * profile.PressureIntensity));
            ThunderClapVFXUtility.EmitRadial(electricalSparks, center, profile.SparkAmount, 8.2f * profile.OverallScale, 1.1f, 0.095f * profile.OverallScale, profile.LightningCoreColor, random, 0.35f);
        }

        public void ResetForPool()
        {
            playing = false;
            profile = null;
            SetLines(false);
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ThunderClapVFXUtility.StopAndClear(system);
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float progress = Mathf.Clamp01(elapsed / profile.ExpansionDuration);
            EmitDirtWake(progress);

            if (Time.time >= nextRefresh)
            {
                RefreshElectricity(progress, elapsed);
                nextRefresh = Time.time + 0.045f;
            }

            ApplyElectricity(progress, elapsed);
            if (progress >= 1f)
            {
                playing = false;
                SetLines(false);
            }
        }

        private void EmitExpandingRing(ParticleSystem system, float diameter, Color color)
        {
            if (system == null)
            {
                return;
            }

            ThunderClapVFXUtility.ConfigureMain(system, profile.ExpansionDuration, diameter, color);
            ParticleSystem.EmitParams emit = new()
            {
                position = center + Vector3.up * 0.035f,
                startSize = diameter,
                startLifetime = profile.ExpansionDuration,
                startColor = color
            };
            system.Emit(emit, 1);
        }

        private void EmitDirtWake(float progress)
        {
            int desired = Mathf.FloorToInt(profile.DirtWakeDensity * progress);
            int count = desired - emittedWake;
            if (count <= 0)
            {
                return;
            }

            float radius = Mathf.Max(0.1f, progress * profile.RingRadius);
            ThunderClapVFXUtility.EmitRing(
                dirtWake,
                center,
                count,
                radius,
                0.32f * profile.OverallScale,
                0.72f * profile.DustWallHeight,
                0.28f * profile.OverallScale,
                profile.WarmDustColor,
                random);
            emittedWake = desired;
        }

        private void RefreshElectricity(float progress, float elapsed)
        {
            float radius = Mathf.Max(0.35f, progress * profile.RingRadius * 0.92f);
            int crawlerCount = Mathf.Min(profile.GroundCrawlerDensity, groundCrawlers.Length);
            for (int i = 0; i < crawlerCount; i++)
            {
                float angle = (i + MMOProceduralVfxUtility.Next01(random) * 0.4f) / Mathf.Max(1, crawlerCount) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                Vector3 start = center + radial * Mathf.Max(0.12f, radius - profile.BranchLength) + Vector3.up * 0.055f;
                Vector3 end = center + radial * radius + tangent * MMOProceduralVfxUtility.NextSigned(random) * 0.38f + Vector3.up * 0.055f;
                MMOProceduralVfxUtility.BuildJaggedPath(crawlerPaths[i], start, end, 0.14f, 2, random, elapsed + i * 0.2f);
            }

            int strikeCount = Mathf.Min(profile.BranchCount, secondaryStrikes.Length);
            for (int i = 0; i < strikeCount; i++)
            {
                float angle = (i + MMOProceduralVfxUtility.Next01(random)) / Mathf.Max(1, strikeCount) * Mathf.PI * 2f;
                float strikeRadius = Mathf.Min(profile.RingRadius, radius + profile.BranchLength * Mathf.Lerp(0.3f, 0.95f, MMOProceduralVfxUtility.Next01(random)));
                Vector3 ground = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * strikeRadius + Vector3.up * 0.04f;
                Vector3 sky = ground + new Vector3(MMOProceduralVfxUtility.NextSigned(random) * 0.4f, Mathf.Lerp(0.8f, 1.65f, MMOProceduralVfxUtility.Next01(random)), MMOProceduralVfxUtility.NextSigned(random) * 0.4f);
                MMOProceduralVfxUtility.BuildJaggedPath(strikePaths[i], sky, ground, 0.12f, 2, random, elapsed + i * 0.31f);
            }
        }

        private void ApplyElectricity(float progress, float elapsed)
        {
            float envelope = Mathf.Sin(progress * Mathf.PI);
            int crawlerCount = Mathf.Min(profile.GroundCrawlerDensity, groundCrawlers.Length);
            for (int i = 0; i < groundCrawlers.Length; i++)
            {
                if (i >= crawlerCount)
                {
                    groundCrawlers[i].enabled = false;
                    continue;
                }

                float flicker = Mathf.Repeat(elapsed * 19f + i * 0.23f, 1f) > 0.23f ? 1f : 0.08f;
                MMOProceduralVfxUtility.SetLine(groundCrawlers[i], crawlerPaths[i], profile.RingThickness, profile.LightningColor, envelope * flicker, -elapsed * 8f, 1.5f);
            }

            int strikeCount = Mathf.Min(profile.BranchCount, secondaryStrikes.Length);
            for (int i = 0; i < secondaryStrikes.Length; i++)
            {
                if (i >= strikeCount)
                {
                    secondaryStrikes[i].enabled = false;
                    continue;
                }

                float flicker = Mathf.Repeat(elapsed * 13f + i * 0.37f, 1f) < 0.18f ? 1f : 0f;
                MMOProceduralVfxUtility.SetLine(secondaryStrikes[i], strikePaths[i], profile.RingThickness * 0.72f, profile.LightningCoreColor, envelope * flicker, elapsed * 10f, 1.2f);
            }
        }

        private void EnsurePathPools()
        {
            for (int i = 0; i < crawlerPaths.Length; i++)
            {
                crawlerPaths[i] ??= new Vector3[7];
                strikePaths[i] ??= new Vector3[6];
            }
        }

        private void SetLines(bool enabled)
        {
            foreach (LineRenderer line in groundCrawlers)
            {
                if (line != null) line.enabled = enabled;
            }

            foreach (LineRenderer line in secondaryStrikes)
            {
                if (line != null) line.enabled = enabled;
            }
        }
    }
}
