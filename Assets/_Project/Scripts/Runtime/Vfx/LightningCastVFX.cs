using System;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class LightningCastVFX : MonoBehaviour, ILightningVFX, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler
    {
        [Header("Configuration")]
        [SerializeField] private LightningVFXProfile profile;

        [Header("Caster-attached electricity")]
        [SerializeField] private LineRenderer[] handArcs = Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] groundCrawlers = Array.Empty<LineRenderer>();
        [SerializeField] private ParticleSystem chargeCore;
        [SerializeField] private ParticleSystem handSparks;
        [SerializeField] private ParticleSystem wristSparks;
        [SerializeField] private LightningChargeWindMeshVFX chargeWindMesh;

        [Header("Charge/Bash world-space environmental systems")]
        [SerializeField] private ParticleSystem dustRing;
        [SerializeField] private ParticleSystem inwardHeavyDust;
        [SerializeField] private ParticleSystem inwardFineDust;
        [SerializeField] private ParticleSystem dirtFragments;
        [SerializeField] private ParticleSystem releaseGroundBurst;

        private MMOAbilityVfxContext context;
        private MMOAbilityVfxAnchors anchors;
        private Vector3[][] handPaths = Array.Empty<Vector3[]>();
        private Vector3[][] crawlerPaths = Array.Empty<Vector3[]>();
        private System.Random random;
        private float startedAt;
        private float chargeDuration;
        private float nextArcRefresh;
        private float nextCrawlerRefresh;
        private float dustAccumulator;
        private float dirtAccumulator;
        private bool initialized;
        private bool released;

        public bool IsPlaying => initialized && !released;
        public LightningVFXProfile Profile => profile;

        private void Awake()
        {
            SetLinesEnabled(handArcs, false);
            SetLinesEnabled(groundCrawlers, false);
        }

        private void Update()
        {
            if (!initialized || released || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, chargeDuration));
            Vector3 left = ResolveHand(true);
            Vector3 right = ResolveHand(false);
            Vector3 center = Vector3.Lerp(left, right, 0.5f);
            UpdateCore(center, progress, elapsed);

            if (Time.time >= nextArcRefresh)
            {
                RefreshHandArcs(left, right, progress, elapsed);
                nextArcRefresh = Time.time + 1f / Mathf.Max(1f, profile.HandArcFlickerSpeed);
            }

            if (Time.time >= nextCrawlerRefresh)
            {
                RefreshGroundCrawlers(progress, elapsed);
                nextCrawlerRefresh = Time.time + 1f / Mathf.Max(1f, profile.GroundCrawlerFrequency);
            }

            EmitInwardEnvironment(progress, Time.deltaTime);
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(LightningCastVFX)} on '{name}' requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            anchors = context.Source != null ? context.Source.GetComponent<MMOAbilityVfxAnchors>() : null;
            int seed = (context.Ability != null ? context.Ability.AbilityId.GetHashCode() : 17) ^ GetHashCode();
            random = new System.Random(seed);
            handPaths = CreatePathPool(handArcs.Length, 7);
            crawlerPaths = CreatePathPool(groundCrawlers.Length, 5);
            startedAt = Time.time;
            chargeDuration = context.Ability != null && context.Ability.CastTimeSeconds > 0.05f
                ? context.Ability.CastTimeSeconds
                : profile.PresentationChargeDuration;
            initialized = true;
            released = false;
            dustAccumulator = 0f;
            dirtAccumulator = 0f;
            ApplyProfile();
            StartParticles();
            EmitChargeRing();
        }

        public void Release(bool immediate)
        {
            if (released)
            {
                return;
            }

            released = true;
            SetLinesEnabled(handArcs, false);
            SetLinesEnabled(groundCrawlers, false);
            StopParticle(chargeCore, immediate);
            StopParticle(handSparks, immediate);
            StopParticle(wristSparks, immediate);
            if (chargeWindMesh != null) chargeWindMesh.Stop();

            if (immediate)
            {
                StopParticle(dustRing, true);
                StopParticle(inwardHeavyDust, true);
                StopParticle(inwardFineDust, true);
                StopParticle(dirtFragments, true);
                StopParticle(releaseGroundBurst, true);
                Destroy(gameObject);
                return;
            }

            Vector3 ground = GroundPosition();
            EmitOutward(releaseGroundBurst, ground, profile.ReleaseDustAmount, 2.8f, 0.45f, 0.65f);
            EmitOutward(inwardHeavyDust, ground, Mathf.Max(6, profile.ReleaseDustAmount / 2), 3.8f, 0.55f, 0.9f);
            EmitOutward(inwardFineDust, ground + Vector3.up * 0.08f, Mathf.Max(8, profile.ReleaseDustAmount), 5.1f, 0.8f, 0.48f);
            EmitOutward(dirtFragments, ground, profile.DirtFragmentCount, 4.4f, 2.2f, profile.DirtFragmentSize);
            transform.SetParent(null, true);
            Destroy(gameObject, 2.6f);
        }

        public void StopImmediate()
        {
            Release(true);
        }

        public void ConfigureAuthoring(
            LightningVFXProfile newProfile,
            LineRenderer[] newHandArcs,
            LineRenderer[] newGroundCrawlers,
            LightningChargeWindMeshVFX newChargeWindMesh,
            ParticleSystem[] attached,
            ParticleSystem[] environmental)
        {
            if (attached == null || attached.Length != 3 || environmental == null || environmental.Length != 5)
            {
                throw new ArgumentException("Lightning cast authoring requires a mesh wind field, 3 attached particles, and 5 environmental particle systems.");
            }

            profile = newProfile;
            handArcs = newHandArcs ?? Array.Empty<LineRenderer>();
            groundCrawlers = newGroundCrawlers ?? Array.Empty<LineRenderer>();
            chargeWindMesh = newChargeWindMesh;
            chargeCore = attached[0];
            handSparks = attached[1];
            wristSparks = attached[2];
            dustRing = environmental[0];
            inwardHeavyDust = environmental[1];
            inwardFineDust = environmental[2];
            dirtFragments = environmental[3];
            releaseGroundBurst = environmental[4];
        }

        private void ApplyProfile()
        {
            transform.localScale = Vector3.one * profile.OverallScale;
            ConfigureParticle(chargeCore, chargeDuration + 0.25f, profile.ElectricalCoreSize, LightningVFXMath.Brighten(profile.CyanColor, profile.ElectricalCoreBrightness * profile.OverallBrightness));
            ConfigureParticle(handSparks, 0.24f, 0.13f, LightningVFXMath.Brighten(profile.WhiteHotColor, profile.OverallBrightness));
            ConfigureParticle(wristSparks, 0.32f, 0.09f, LightningVFXMath.Brighten(profile.ElectricBlueColor, profile.OverallBrightness));
            ConfigureParticle(dustRing, Mathf.Max(0.35f, profile.DustRingRadius / profile.DustRingExpansionSpeed), profile.DustRingRadius * 2f, profile.DustColor);
            ConfigureParticle(inwardHeavyDust, 1.3f, 0.9f, profile.DustColor);
            ConfigureParticle(inwardFineDust, 1.05f, 0.48f, new Color(profile.DustColor.r, profile.DustColor.g, profile.DustColor.b, profile.DustColor.a * 0.7f));
            ConfigureParticle(dirtFragments, 1.1f, profile.DirtFragmentSize, profile.DustColor);
            ConfigureParticle(releaseGroundBurst, 0.75f, 1.3f, profile.DustColor);
        }

        private void StartParticles()
        {
            PlayParticle(chargeCore);
            PlayParticle(handSparks);
            PlayParticle(wristSparks);
            if (chargeWindMesh != null) chargeWindMesh.Begin();
        }

        private void UpdateCore(Vector3 center, float progress, float elapsed)
        {
            float peakCompression = progress > 0.9f ? Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.9f, 1f, progress)) : 1f;
            float pulse = 1f + Mathf.Sin(elapsed * 23f) * 0.08f + Mathf.Sin(elapsed * 8.7f) * 0.05f;
            if (chargeCore != null)
            {
                chargeCore.transform.position = center;
                chargeCore.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, profile.ElectricalCoreSize, progress) * pulse * peakCompression;
            }

            if (handSparks != null)
            {
                handSparks.transform.position = center;
            }

            if (wristSparks != null)
            {
                wristSparks.transform.position = center;
            }

            if (chargeWindMesh != null) chargeWindMesh.UpdatePresentation(GroundPosition(), progress, elapsed);
        }

        private void RefreshHandArcs(Vector3 left, Vector3 right, float progress, float elapsed)
        {
            int active = Mathf.Clamp(profile.HandArcCount, 1, handArcs.Length);
            for (int i = 0; i < handArcs.Length; i++)
            {
                LineRenderer line = handArcs[i];
                if (line == null || i >= active)
                {
                    if (line != null) line.enabled = false;
                    continue;
                }

                float layer = i == 0 ? 1f : Mathf.Lerp(0.42f, 0.7f, LightningVFXMath.Next01(random));
                float amplitude = Mathf.Lerp(0.035f, 0.16f, progress) * (0.65f + i * 0.08f);
                LightningVFXMath.BuildJaggedPath(handPaths[i], left, right, amplitude, 2 + i % 2, random, i * 0.17f);
                Color color = i == 0 ? profile.WhiteHotColor : i % 2 == 0 ? profile.CyanColor : profile.ElectricBlueColor;
                float flicker = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(elapsed * profile.HandArcFlickerSpeed + i * 1.7f));
                LightningVFXMath.SetLine(
                    line,
                    handPaths[i],
                    profile.HandArcThickness * layer * Mathf.Lerp(0.45f, 1.35f, progress),
                    LightningVFXMath.Brighten(color, profile.OverallBrightness),
                    flicker,
                    -elapsed * (1.8f + i * 0.25f),
                    1.4f + i * 0.3f);
            }
        }

        private void RefreshGroundCrawlers(float progress, float elapsed)
        {
            Vector3 center = GroundPosition() + Vector3.up * 0.025f;
            float reach = profile.GroundCrawlerRange * Mathf.Lerp(0.35f, 1f, progress);
            for (int i = 0; i < groundCrawlers.Length; i++)
            {
                LineRenderer line = groundCrawlers[i];
                if (line == null)
                {
                    continue;
                }

                float angle = LightningVFXMath.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 start = center + radial * LightningVFXMath.Next01(random) * 0.35f;
                Vector3 end = center + radial * reach * Mathf.Lerp(0.45f, 1f, LightningVFXMath.Next01(random));
                LightningVFXMath.BuildJaggedPath(crawlerPaths[i], start, end, 0.11f, 2, random, i * 0.23f);
                LightningVFXMath.SetLine(line, crawlerPaths[i], 0.025f + 0.025f * progress, profile.CyanColor, 0.35f + 0.45f * progress, elapsed * 2.4f, 1.6f);
            }
        }

        private void EmitInwardEnvironment(float progress, float deltaTime)
        {
            float rate = profile.DustDensity * profile.QualityMultiplier * Mathf.Lerp(0.35f, 1f, progress);
            dustAccumulator += deltaTime * rate;
            int dustEvents = Mathf.Min(8, Mathf.FloorToInt(dustAccumulator));
            dustAccumulator -= dustEvents;
            for (int i = 0; i < dustEvents; i++)
            {
                bool heavy = (i & 1) == 0;
                EmitInward(heavy ? inwardHeavyDust : inwardFineDust, heavy ? 0.82f : 0.42f, progress);
            }

            dirtAccumulator += deltaTime * profile.DirtFragmentCount / Mathf.Max(0.3f, chargeDuration);
            int dirtEvents = Mathf.Min(3, Mathf.FloorToInt(dirtAccumulator));
            dirtAccumulator -= dirtEvents;
            for (int i = 0; i < dirtEvents; i++)
            {
                EmitInward(dirtFragments, profile.DirtFragmentSize, progress, 0.35f);
            }
        }

        private void EmitInward(ParticleSystem system, float size, float progress, float lift = 0.1f)
        {
            if (system == null)
            {
                return;
            }

            float angle = LightningVFXMath.Next01(random) * Mathf.PI * 2f;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 center = GroundPosition();
            Vector3 spawn = center + radial * profile.InwardDustSpawnRadius * Mathf.Lerp(0.78f, 1.05f, LightningVFXMath.Next01(random));
            spawn.y += LightningVFXMath.Next01(random) * 0.22f;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * profile.DustSpiralAmount;
            Vector3 velocity = -radial * profile.InwardDustSpeed * Mathf.Lerp(0.65f, 1.35f, progress) + tangent + Vector3.up * lift;
            ParticleSystem.EmitParams emit = new()
            {
                position = spawn,
                velocity = velocity,
                startSize = size * Mathf.Lerp(0.72f, 1.18f, LightningVFXMath.Next01(random)),
                startColor = profile.DustColor,
                rotation = LightningVFXMath.Next01(random) * Mathf.PI * 2f
            };
            system.Emit(emit, 1);
        }

        private void EmitChargeRing()
        {
            if (dustRing == null)
            {
                return;
            }

            dustRing.transform.position = GroundPosition() + Vector3.up * 0.025f;
            dustRing.Emit(1);
        }

        private void EmitOutward(ParticleSystem system, Vector3 center, int count, float speed, float lift, float size)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            int scaledCount = Mathf.Max(1, Mathf.RoundToInt(count * profile.QualityMultiplier));
            for (int i = 0; i < scaledCount; i++)
            {
                float angle = LightningVFXMath.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * LightningVFXMath.Next01(random) * 0.28f,
                    velocity = radial * speed * Mathf.Lerp(0.65f, 1.2f, LightningVFXMath.Next01(random)) + Vector3.up * lift * LightningVFXMath.Next01(random),
                    startSize = size * Mathf.Lerp(0.68f, 1.25f, LightningVFXMath.Next01(random)),
                    startColor = profile.DustColor,
                    rotation = LightningVFXMath.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        private Vector3 ResolveHand(bool left)
        {
            Transform hand = anchors != null ? (left ? anchors.LeftHandAnchor : anchors.RightHandAnchor) : null;
            if (hand != null)
            {
                return hand.position;
            }

            Transform source = context.Source != null ? context.Source : transform;
            return source.TransformPoint(new Vector3(left ? -0.32f : 0.32f, 1.18f, 0.42f));
        }

        private Vector3 GroundPosition()
        {
            Vector3 position = context.Source != null ? context.Source.position : transform.position;
            return position + Vector3.up * 0.035f;
        }

        private static Vector3[][] CreatePathPool(int count, int points)
        {
            Vector3[][] pool = new Vector3[count][];
            for (int i = 0; i < count; i++)
            {
                pool[i] = new Vector3[points];
            }

            return pool;
        }

        private static void ConfigureParticle(ParticleSystem system, float lifetime, float size, Color color)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.MainModule main = system.main;
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSize = Mathf.Max(0.01f, size);
            main.startColor = color;
        }

        private static void PlayParticle(ParticleSystem system)
        {
            if (system != null)
            {
                system.Play(true);
            }
        }

        private static void StopParticle(ParticleSystem system, bool clear)
        {
            if (system != null)
            {
                system.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static void SetLinesEnabled(LineRenderer[] lines, bool enabled)
        {
            foreach (LineRenderer line in lines)
            {
                if (line != null)
                {
                    line.enabled = enabled;
                }
            }
        }
    }
}
