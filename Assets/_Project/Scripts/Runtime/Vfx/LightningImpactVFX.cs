using System;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class LightningImpactVFX : MonoBehaviour, ILightningVFX, IMMOAbilityVfxInstance
    {
        [SerializeField] private LightningVFXProfile profile;
        [SerializeField] private LineRenderer[] bodyArcs = Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] groundStrikes = Array.Empty<LineRenderer>();
        [SerializeField] private ParticleSystem contactFlash;
        [SerializeField] private ParticleSystem electricalBurst;
        [SerializeField] private ParticleSystem shockRing;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem impactHeavyDust;
        [SerializeField] private ParticleSystem impactFineDust;
        [SerializeField] private ParticleSystem dirtDebris;
        [SerializeField] private ParticleSystem smoke;

        private MMOAbilityVfxContext context;
        private Vector3[][] bodyPaths = Array.Empty<Vector3[]>();
        private Vector3[][] groundPaths = Array.Empty<Vector3[]>();
        private System.Random random;
        private float startedAt;
        private float nextRefresh;
        private bool initialized;
        private bool smokeEmitted;

        public bool IsPlaying => initialized;
        public LightningVFXProfile Profile => profile;

        private void Awake()
        {
            SetLines(false);
        }

        private void Update()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float electricalLifetime = 0.32f + profile.AftermathDuration;
            if (!smokeEmitted && elapsed >= 0.1f)
            {
                EmitAt(smoke, ResolveImpactPoint() + Vector3.up * 0.05f, profile.SmokeAmount, 0.48f, profile.ElectricBlueColor);
                smokeEmitted = true;
            }

            if (elapsed < electricalLifetime && Time.time >= nextRefresh)
            {
                RefreshArcs(elapsed);
                nextRefresh = Time.time + 0.055f;
            }

            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, electricalLifetime, elapsed));
            ApplyArcPresentation(elapsed, fade);

            if (elapsed >= 2.6f)
            {
                initialized = false;
                SetLines(false);
                Destroy(gameObject);
            }
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(LightningImpactVFX)} on '{name}' requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            random = new System.Random(GetHashCode() ^ 0x2f4d);
            bodyPaths = CreatePathPool(bodyArcs.Length, 5);
            groundPaths = CreatePathPool(groundStrikes.Length, 5);
            startedAt = Time.time;
            nextRefresh = 0f;
            initialized = true;
            smokeEmitted = false;
            ApplyProfile();
            PlayImpactStack();
            RefreshArcs(0f);
        }

        public void ConfigureAuthoring(
            LightningVFXProfile newProfile,
            LineRenderer[] newBodyArcs,
            LineRenderer[] newGroundStrikes,
            ParticleSystem[] impactParticles)
        {
            if (impactParticles == null || impactParticles.Length != 8)
            {
                throw new ArgumentException("Lightning impact authoring requires exactly eight particle systems.");
            }

            profile = newProfile;
            bodyArcs = newBodyArcs ?? Array.Empty<LineRenderer>();
            groundStrikes = newGroundStrikes ?? Array.Empty<LineRenderer>();
            contactFlash = impactParticles[0];
            electricalBurst = impactParticles[1];
            shockRing = impactParticles[2];
            sparks = impactParticles[3];
            impactHeavyDust = impactParticles[4];
            impactFineDust = impactParticles[5];
            dirtDebris = impactParticles[6];
            smoke = impactParticles[7];
        }

        public void StopImmediate()
        {
            initialized = false;
            SetLines(false);
            foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            Destroy(gameObject);
        }

        private void ApplyProfile()
        {
            ConfigureParticle(contactFlash, 0.16f, profile.ContactFlashSize, LightningVFXMath.Brighten(profile.WhiteHotColor, profile.OverallBrightness * 2.4f));
            ConfigureParticle(electricalBurst, 0.28f, 0.7f * profile.OverallScale, profile.CyanColor);
            ConfigureParticle(shockRing, 0.34f, profile.ShockRingSize, profile.CyanColor);
            ConfigureParticle(sparks, 0.42f, 0.11f, profile.WhiteHotColor);
            ConfigureParticle(impactHeavyDust, 1.65f, 0.9f, profile.DustColor);
            ConfigureParticle(impactFineDust, 2.25f, 0.55f, new Color(profile.DustColor.r, profile.DustColor.g, profile.DustColor.b, profile.DustColor.a * 0.72f));
            ConfigureParticle(dirtDebris, 1.25f, profile.DirtFragmentSize, profile.DustColor);
            ConfigureParticle(smoke, 1.4f, 0.72f, new Color(0.22f, 0.3f, 0.43f, 0.62f));
        }

        private void PlayImpactStack()
        {
            Vector3 point = ResolveImpactPoint();
            EmitAt(contactFlash, point, 2, profile.ContactFlashSize, profile.WhiteHotColor);
            EmitAt(electricalBurst, point, Mathf.Max(4, profile.ImpactBoltCount), 0.72f, profile.CyanColor);
            EmitAt(shockRing, point, 1, profile.ShockRingSize, profile.CyanColor);
            EmitRadial(sparks, point, profile.ImpactSparkCount, 5.2f, 2.1f, 0.09f, profile.WhiteHotColor);

            Vector3 ground = GroundPoint();
            EmitRadial(impactHeavyDust, ground, profile.ImpactDustAmount, 2.2f, 0.45f, 0.9f, profile.DustColor);
            EmitRadial(impactFineDust, ground + Vector3.up * 0.08f, Mathf.Max(4, profile.ImpactDustAmount / 2), 1.4f, 0.72f, 0.52f, profile.DustColor);
            EmitRadial(dirtDebris, ground, Mathf.Max(3, profile.DirtFragmentCount), 3.8f, 2.7f, profile.DirtFragmentSize, profile.DustColor);
        }

        private void RefreshArcs(float elapsed)
        {
            Vector3 center = ResolveImpactPoint();
            int activeBody = Mathf.Min(profile.TargetBodyArcCount, bodyArcs.Length);
            for (int i = 0; i < bodyArcs.Length; i++)
            {
                if (i >= activeBody || bodyArcs[i] == null)
                {
                    if (bodyArcs[i] != null) bodyArcs[i].enabled = false;
                    continue;
                }

                Vector3 a = center + new Vector3(LightningVFXMath.NextSigned(random) * 0.36f, LightningVFXMath.NextSigned(random) * 0.62f, LightningVFXMath.NextSigned(random) * 0.28f);
                Vector3 b = center + new Vector3(LightningVFXMath.NextSigned(random) * 0.48f, LightningVFXMath.NextSigned(random) * 0.78f, LightningVFXMath.NextSigned(random) * 0.35f);
                LightningVFXMath.BuildJaggedPath(bodyPaths[i], a, b, 0.12f, 2, random, elapsed + i * 0.2f);
            }

            Vector3 ground = GroundPoint() + Vector3.up * 0.035f;
            int activeGround = Mathf.Min(profile.GroundStrikeCount, groundStrikes.Length);
            for (int i = 0; i < groundStrikes.Length; i++)
            {
                if (i >= activeGround || groundStrikes[i] == null)
                {
                    if (groundStrikes[i] != null) groundStrikes[i].enabled = false;
                    continue;
                }

                float angle = LightningVFXMath.Next01(random) * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0.75f, 1.85f, LightningVFXMath.Next01(random));
                Vector3 end = ground + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                LightningVFXMath.BuildJaggedPath(groundPaths[i], center, end, 0.18f, 2, random, elapsed + i * 0.33f);
            }
        }

        private void ApplyArcPresentation(float elapsed, float fade)
        {
            int activeBody = Mathf.Min(profile.TargetBodyArcCount, bodyArcs.Length);
            for (int i = 0; i < bodyArcs.Length; i++)
            {
                if (i < activeBody)
                {
                    float flicker = Mathf.Repeat(elapsed * 13f + i * 0.31f, 1f) > 0.18f ? 1f : 0f;
                    LightningVFXMath.SetLine(bodyArcs[i], bodyPaths[i], profile.CoreWidth * 0.62f, i % 3 == 0 ? profile.VioletColor : profile.CyanColor, fade * flicker, elapsed * 6f, 1.2f);
                }
            }

            int activeGround = Mathf.Min(profile.GroundStrikeCount, groundStrikes.Length);
            for (int i = 0; i < groundStrikes.Length; i++)
            {
                if (i < activeGround)
                {
                    float flicker = Mathf.Repeat(elapsed * 16f + i * 0.19f, 1f) > 0.3f ? 1f : 0f;
                    LightningVFXMath.SetLine(groundStrikes[i], groundPaths[i], profile.CoreWidth * 0.48f, profile.ElectricBlueColor, fade * flicker, -elapsed * 8f, 1.4f);
                }
            }
        }

        private Vector3 ResolveImpactPoint()
        {
            return LightningVFXMath.ResolveHitPoint(context.Target, context.TargetPosition, context.Definition);
        }

        private Vector3 GroundPoint()
        {
            Vector3 point = context.Target != null ? context.Target.position : context.TargetPosition;
            return point + Vector3.up * 0.035f;
        }

        private void EmitAt(ParticleSystem system, Vector3 position, int count, float size, Color color)
        {
            if (system == null || count <= 0) return;
            int scaledCount = Mathf.Max(1, Mathf.RoundToInt(count * profile.QualityMultiplier));
            for (int i = 0; i < scaledCount; i++)
            {
                ParticleSystem.EmitParams emit = new()
                {
                    position = position,
                    startSize = size * Mathf.Lerp(0.82f, 1.16f, LightningVFXMath.Next01(random)),
                    startColor = color,
                    rotation = LightningVFXMath.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        private void EmitRadial(ParticleSystem system, Vector3 center, int count, float speed, float lift, float size, Color color)
        {
            if (system == null || count <= 0) return;
            int scaledCount = Mathf.Max(1, Mathf.RoundToInt(count * profile.QualityMultiplier));
            for (int i = 0; i < scaledCount; i++)
            {
                float angle = LightningVFXMath.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * LightningVFXMath.Next01(random) * 0.18f,
                    velocity = radial * speed * Mathf.Lerp(0.55f, 1.15f, LightningVFXMath.Next01(random)) + Vector3.up * lift * LightningVFXMath.Next01(random),
                    startSize = size * Mathf.Lerp(0.72f, 1.24f, LightningVFXMath.Next01(random)),
                    startColor = color,
                    rotation = LightningVFXMath.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        private void SetLines(bool enabled)
        {
            foreach (LineRenderer line in bodyArcs) if (line != null) line.enabled = enabled;
            foreach (LineRenderer line in groundStrikes) if (line != null) line.enabled = enabled;
        }

        private static Vector3[][] CreatePathPool(int count, int points)
        {
            Vector3[][] pool = new Vector3[count][];
            for (int i = 0; i < count; i++) pool[i] = new Vector3[points];
            return pool;
        }

        private static void ConfigureParticle(ParticleSystem system, float lifetime, float size, Color color)
        {
            if (system == null) return;
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSize = Mathf.Max(0.01f, size);
            main.startColor = color;
        }
    }
}
