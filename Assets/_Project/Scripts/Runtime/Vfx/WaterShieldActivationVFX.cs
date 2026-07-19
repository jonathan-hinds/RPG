using System;
using UnityEngine;

namespace RPGClone.Vfx.Water
{
    [DisallowMultipleComponent]
    public sealed class WaterShieldActivationVFX : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private const int OrbCount = 3;

        [SerializeField] private WaterShieldVFXProfile profile;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField] private Renderer centralFlash;
        [SerializeField] private Renderer activationRing;
        [SerializeField] private ParticleSystem circularSplash;
        [SerializeField] private ParticleSystem softMist;
        [SerializeField] private ParticleSystem sparkles;
        [SerializeField] private ParticleSystem atmosphericDroplets;
        [SerializeField] private LineRenderer[] condensationStreams = new LineRenderer[OrbCount];

        private readonly Transform[] orbTargets = new Transform[OrbCount];
        private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();
        private DropletPath[] paths = Array.Empty<DropletPath>();
        private MaterialPropertyBlock propertyBlock;
        private float startedAt;
        private bool playing;

        public WaterShieldVFXProfile Profile => profile;
        public bool IsPlaying => playing;

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
            AnimateBurst(elapsed);
            UpdateStreams(elapsed);
            UpdateDroplets(elapsed);
            if (elapsed >= profile.ActivationDuration + 0.18f)
            {
                Complete();
            }
        }

        public void Play(WaterShieldVFXProfile newProfile, Transform[] newOrbTargets)
        {
            profile = newProfile != null ? newProfile : profile;
            if (profile == null)
            {
                Debug.LogError("WaterShieldActivationVFX requires a profile.", this);
                return;
            }

            for (int i = 0; i < OrbCount; i++)
            {
                orbTargets[i] = newOrbTargets != null && i < newOrbTargets.Length ? newOrbTargets[i] : null;
            }

            BuildDropletPaths();
            PlayBurst(circularSplash);
            PlayBurst(softMist);
            PlayBurst(sparkles);
            atmosphericDroplets?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            atmosphericDroplets?.Play(true);
            startedAt = Time.time;
            playing = true;
            UpdateDroplets(0f);
            AnimateBurst(0f);
        }

        public void StopImmediate()
        {
            playing = false;
            StopAndClear(circularSplash);
            StopAndClear(softMist);
            StopAndClear(sparkles);
            StopAndClear(atmosphericDroplets);
            SetRenderer(centralFlash, 0f, 0f, 1f);
            SetRenderer(activationRing, 0f, 0f, 1f);
            if (centralFlash != null) centralFlash.gameObject.SetActive(false);
            if (activationRing != null) activationRing.gameObject.SetActive(false);
            foreach (LineRenderer stream in condensationStreams)
            {
                if (stream != null) stream.enabled = false;
            }
        }

        public void ConfigureAuthoring(
            WaterShieldVFXProfile newProfile,
            bool newDestroyOnComplete,
            Renderer newCentralFlash,
            Renderer newActivationRing,
            ParticleSystem newCircularSplash,
            ParticleSystem newSoftMist,
            ParticleSystem newSparkles,
            ParticleSystem newAtmosphericDroplets,
            LineRenderer[] newCondensationStreams)
        {
            profile = newProfile;
            destroyOnComplete = newDestroyOnComplete;
            centralFlash = newCentralFlash;
            activationRing = newActivationRing;
            circularSplash = newCircularSplash;
            softMist = newSoftMist;
            sparkles = newSparkles;
            atmosphericDroplets = newAtmosphericDroplets;
            condensationStreams = newCondensationStreams ?? Array.Empty<LineRenderer>();
        }

        private void AnimateBurst(float elapsed)
        {
            float flash = Pulse(elapsed, 0f, 0.09f, 0.32f);
            float ring = Pulse(elapsed, 0.04f, 0.24f, 0.78f);
            if (centralFlash != null)
            {
                centralFlash.gameObject.SetActive(flash > 0.001f);
                centralFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.35f, Smooth01(Mathf.Clamp01(elapsed / 0.28f)));
            }

            if (activationRing != null)
            {
                activationRing.gameObject.SetActive(ring > 0.001f);
                activationRing.transform.localScale = Vector3.one * profile.ActivationRingSize * Mathf.Lerp(0.18f, 1f, Smooth01(Mathf.Clamp01(elapsed / 0.68f)));
                activationRing.transform.Rotate(Vector3.forward, 95f * Time.deltaTime, Space.Self);
            }

            SetRenderer(centralFlash, flash, profile.ActivationFlashBrightness * profile.OverallBrightness, 1f - flash);
            SetRenderer(activationRing, ring * 0.82f, 1.35f * profile.OverallBrightness, 1f - ring);
        }

        private void BuildDropletPaths()
        {
            int count = profile.DropletsPerOrb * OrbCount;
            particles = new ParticleSystem.Particle[count];
            paths = new DropletPath[count];
            System.Random random = new(8012026);
            for (int i = 0; i < count; i++)
            {
                int orb = i % OrbCount;
                float angle = NextFloat(random, 0f, Mathf.PI * 2f);
                float radius = NextFloat(random, profile.CollectionInnerRadius, profile.CollectionOuterRadius);
                float height = NextFloat(random, -profile.CollectionVerticalRange * 0.45f, profile.CollectionVerticalRange);
                Vector3 start = new(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                Vector3 tangent = new(-Mathf.Sin(angle), NextFloat(random, -0.2f, 0.2f), Mathf.Cos(angle));
                paths[i] = new DropletPath(start, tangent, orb, NextFloat(random, -0.08f, 0.11f), NextFloat(random, 0.72f, 1.35f), NextFloat(random, 0f, Mathf.PI * 2f));
                particles[i] = new ParticleSystem.Particle
                {
                    position = transform.TransformPoint(start),
                    startLifetime = 10f,
                    remainingLifetime = 10f,
                    startSize = profile.DropletSize * paths[i].Size,
                    startColor = profile.Colors.Aqua
                };
            }
        }

        private void UpdateDroplets(float elapsed)
        {
            if (atmosphericDroplets == null || particles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                DropletPath path = paths[i];
                float begin = profile.FirstOrbDelay + path.Orb * profile.OrbFormationInterval + path.Delay;
                float progress = Mathf.Clamp01((elapsed - begin) / profile.GatherDuration);
                float eased = Smooth01(progress);
                Vector3 start = transform.TransformPoint(path.Start);
                Vector3 target = orbTargets[path.Orb] != null ? orbTargets[path.Orb].position : transform.position;
                float spiral = Mathf.Sin((progress * 2.15f + path.Phase) * Mathf.PI * 2f) * profile.InwardSpiralStrength * (1f - eased);
                Vector3 position = Vector3.LerpUnclamped(start, target, eased) + transform.TransformDirection(path.Tangent) * spiral;
                if (progress <= 0f) position += Vector3.up * Mathf.Sin(elapsed * 2f + path.Phase) * 0.045f;

                Color color = Color.Lerp(profile.Colors.ClearBlue, profile.Colors.WhiteHighlight, path.Size * 0.35f);
                color.a = Mathf.Clamp01((elapsed + 0.06f) * 5f) * Mathf.Pow(1f - progress, 0.42f);
                ParticleSystem.Particle particle = particles[i];
                particle.position = position;
                particle.startColor = color;
                particle.startSize = profile.DropletSize * path.Size * Mathf.Lerp(1f, 0.35f, progress);
                Vector3 pull = target - position;
                particle.velocity = pull.sqrMagnitude > 0.0001f ? pull.normalized * Mathf.Lerp(0.4f, 1.8f, progress) : Vector3.zero;
                particle.remainingLifetime = 10f;
                particles[i] = particle;
            }

            atmosphericDroplets.SetParticles(particles, particles.Length);
        }

        private void UpdateStreams(float elapsed)
        {
            for (int i = 0; i < Mathf.Min(OrbCount, condensationStreams.Length); i++)
            {
                LineRenderer stream = condensationStreams[i];
                if (stream == null) continue;
                float begin = profile.FirstOrbDelay + i * profile.OrbFormationInterval;
                float progress = Mathf.Clamp01((elapsed - begin) / profile.GatherDuration);
                float alpha = Mathf.Sin(progress * Mathf.PI);
                stream.enabled = alpha > 0.01f && orbTargets[i] != null;
                if (!stream.enabled) continue;

                Vector3 end = orbTargets[i].position;
                Vector3 start = transform.TransformPoint(new Vector3(Mathf.Sin(i * 2.094f + 0.7f) * 1.6f, 0.5f + i * 0.24f, Mathf.Cos(i * 2.094f + 0.7f) * 1.6f));
                Vector3 side = Vector3.Cross(Vector3.up, end - start).normalized * (0.26f + i * 0.04f);
                for (int p = 0; p < stream.positionCount; p++)
                {
                    float t = p / (float)(stream.positionCount - 1);
                    Vector3 curve = Vector3.Lerp(start, end, t) + side * Mathf.Sin(t * Mathf.PI) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.18f;
                    stream.SetPosition(p, curve);
                }

                Color color = profile.Colors.PaleCyan;
                color.a = alpha;
                stream.startColor = color;
                color.a = 0f;
                stream.endColor = color;
            }
        }

        private void Complete()
        {
            playing = false;
            StopAndClear(atmosphericDroplets);
            if (destroyOnComplete && Application.isPlaying)
            {
                Destroy(gameObject, 0.1f);
            }
            else
            {
                StopImmediate();
            }
        }

        private void SetRenderer(Renderer renderer, float opacity, float brightness, float dissolve)
        {
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private static void PlayBurst(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void StopAndClear(ParticleSystem particles)
        {
            particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static float Pulse(float value, float start, float peak, float end)
        {
            if (value <= start || value >= end) return 0f;
            return value <= peak ? Smooth01((value - start) / Mathf.Max(0.001f, peak - start)) : 1f - Smooth01((value - peak) / Mathf.Max(0.001f, end - peak));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float NextFloat(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private readonly struct DropletPath
        {
            public DropletPath(Vector3 start, Vector3 tangent, int orb, float delay, float size, float phase)
            {
                Start = start;
                Tangent = tangent;
                Orb = orb;
                Delay = delay;
                Size = size;
                Phase = phase;
            }

            public Vector3 Start { get; }
            public Vector3 Tangent { get; }
            public int Orb { get; }
            public float Delay { get; }
            public float Size { get; }
            public float Phase { get; }
        }
    }
}
