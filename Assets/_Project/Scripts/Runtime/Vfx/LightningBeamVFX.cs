using System;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class LightningBeamVFX : MonoBehaviour, ILightningVFX, IMMOAbilityVfxInstance
    {
        [SerializeField] private LightningVFXProfile profile;
        [SerializeField] private LineRenderer whiteCore;
        [SerializeField] private LineRenderer cyanBody;
        [SerializeField] private LineRenderer blueOuterBody;
        [SerializeField] private LineRenderer violetGlow;
        [SerializeField] private LineRenderer[] secondaryBolts = Array.Empty<LineRenderer>();
        [SerializeField] private LineRenderer[] branches = Array.Empty<LineRenderer>();
        [SerializeField] private ParticleSystem beamFlashes;
        [SerializeField] private ParticleSystem particleSheath;

        private MMOAbilityVfxContext context;
        private Vector3[] basePath = Array.Empty<Vector3>();
        private Vector3[][] secondaryPaths = Array.Empty<Vector3[]>();
        private Vector3[][] branchPaths = Array.Empty<Vector3[]>();
        private System.Random random;
        private float startedAt;
        private float nextRefresh;
        private bool initialized;
        private bool requestedHit;

        public bool IsPlaying => initialized && Time.time - startedAt < (profile != null ? profile.BeamDuration : 0f);
        public LightningVFXProfile Profile => profile;

        private void Awake()
        {
            SetAllLines(false);
        }

        private void Update()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            if (elapsed >= profile.BeamDuration)
            {
                SetAllLines(false);
                StopParticles();
                initialized = false;
                return;
            }

            if (Time.time >= nextRefresh)
            {
                Regenerate(elapsed);
                nextRefresh = Time.time + 1f / Mathf.Max(1f, profile.PathRefreshRate);
            }

            ApplyLayerPresentation(elapsed);
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(LightningBeamVFX)} on '{name}' requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            int points = Mathf.Clamp(profile.BeamPathComplexity, 5, 24);
            basePath = new Vector3[points];
            secondaryPaths = CreatePathPool(secondaryBolts.Length, Mathf.Max(5, points - 2));
            branchPaths = CreatePathPool(branches.Length, 4);
            int seed = (context.Ability != null ? context.Ability.AbilityId.GetHashCode() : 31)
                ^ Mathf.RoundToInt(context.TargetPosition.x * 10f)
                ^ Mathf.RoundToInt(context.TargetPosition.z * 100f);
            random = new System.Random(seed);
            startedAt = Time.time;
            nextRefresh = 0f;
            initialized = true;
            requestedHit = false;
            ApplyParticleProfile();
            RequestHitOnce();
            Regenerate(0f);
            EmitSheath();
        }

        public void ConfigureAuthoring(
            LightningVFXProfile newProfile,
            LineRenderer[] mainLayers,
            LineRenderer[] newSecondaryBolts,
            LineRenderer[] newBranches,
            ParticleSystem newBeamFlashes,
            ParticleSystem newParticleSheath)
        {
            if (mainLayers == null || mainLayers.Length != 4)
            {
                throw new ArgumentException("Lightning beam authoring requires exactly four main line layers.");
            }

            profile = newProfile;
            whiteCore = mainLayers[0];
            cyanBody = mainLayers[1];
            blueOuterBody = mainLayers[2];
            violetGlow = mainLayers[3];
            secondaryBolts = newSecondaryBolts ?? Array.Empty<LineRenderer>();
            branches = newBranches ?? Array.Empty<LineRenderer>();
            beamFlashes = newBeamFlashes;
            particleSheath = newParticleSheath;
        }

        public void StopImmediate()
        {
            initialized = false;
            SetAllLines(false);
            StopParticles(true);
            Destroy(gameObject);
        }

        private void Regenerate(float elapsed)
        {
            Vector3 start = ResolveSource();
            Vector3 end = LightningVFXMath.ResolveHitPoint(context.Target, context.TargetPosition, context.Definition);
            float distance = Vector3.Distance(start, end);
            float amplitude = Mathf.Clamp(distance * 0.055f, 0.28f, 1.25f);
            LightningVFXMath.BuildJaggedPath(basePath, start, end, amplitude, profile.LargeBendCount, random, elapsed * 2.7f);

            int activeSecondary = Mathf.Min(profile.SecondaryBoltCount, secondaryBolts.Length);
            for (int i = 0; i < secondaryBolts.Length; i++)
            {
                if (i >= activeSecondary || secondaryBolts[i] == null)
                {
                    if (secondaryBolts[i] != null) secondaryBolts[i].enabled = false;
                    continue;
                }

                Vector3 offset = Vector3.up * LightningVFXMath.NextSigned(random) * 0.24f;
                LightningVFXMath.BuildJaggedPath(
                    secondaryPaths[i],
                    start + offset * 0.1f,
                    end + offset,
                    amplitude * Mathf.Lerp(0.7f, 1.25f, LightningVFXMath.Next01(random)),
                    profile.LargeBendCount + i % 2,
                    random,
                    i * 0.31f + elapsed);
            }

            int activeBranches = Mathf.Min(profile.BranchCount, branches.Length);
            Vector3 beamDirection = (end - start).sqrMagnitude > 0.001f ? (end - start).normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(beamDirection, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            for (int i = 0; i < branches.Length; i++)
            {
                if (i >= activeBranches || branches[i] == null)
                {
                    if (branches[i] != null) branches[i].enabled = false;
                    continue;
                }

                int anchorIndex = Mathf.Clamp(1 + (int)(LightningVFXMath.Next01(random) * (basePath.Length - 2)), 1, basePath.Length - 2);
                Vector3 branchStart = basePath[anchorIndex];
                Vector3 outward = (side * LightningVFXMath.NextSigned(random) + Vector3.up * LightningVFXMath.NextSigned(random) * 0.75f).normalized;
                Vector3 branchEnd = branchStart + outward * profile.BranchLength * Mathf.Lerp(0.45f, 1.1f, LightningVFXMath.Next01(random));
                LightningVFXMath.BuildJaggedPath(branchPaths[i], branchStart, branchEnd, amplitude * 0.22f, 1, random, i * 0.41f);
            }

            EmitFlash(basePath[Mathf.Clamp(1 + (int)(LightningVFXMath.Next01(random) * (basePath.Length - 2)), 1, basePath.Length - 2)]);
        }

        private void ApplyLayerPresentation(float elapsed)
        {
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, profile.BeamDuration));
            float snap = normalized < 0.08f ? normalized / 0.08f : 1f;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, normalized));
            float flicker = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(elapsed * 83f));
            float distance = basePath.Length > 1 ? Vector3.Distance(basePath[0], basePath[basePath.Length - 1]) : 1f;
            float tiling = Mathf.Max(1f, distance * 0.38f);
            float brightness = profile.BeamBrightness * profile.OverallBrightness;

            LightningVFXMath.SetLine(whiteCore, basePath, profile.CoreWidth, LightningVFXMath.Brighten(profile.WhiteHotColor, brightness), snap * fade * flicker, -elapsed * 12f, tiling * 1.8f);
            LightningVFXMath.SetLine(cyanBody, basePath, profile.MainBodyWidth, LightningVFXMath.Brighten(profile.CyanColor, brightness * 0.72f), snap * fade, elapsed * 8f, tiling);
            LightningVFXMath.SetLine(blueOuterBody, basePath, profile.BeamWidth, LightningVFXMath.Brighten(profile.ElectricBlueColor, brightness * 0.48f), Mathf.Min(0.9f, snap * fade), -elapsed * 4.7f, tiling * 0.72f);
            LightningVFXMath.SetLine(violetGlow, basePath, profile.OuterGlowWidth, LightningVFXMath.Brighten(profile.VioletColor, brightness * 0.32f), snap * fade * 0.72f, elapsed * 2.8f, tiling * 0.52f);

            int activeSecondary = Mathf.Min(profile.SecondaryBoltCount, secondaryBolts.Length);
            for (int i = 0; i < secondaryBolts.Length; i++)
            {
                if (i < activeSecondary)
                {
                    float offsetFlicker = Mathf.Repeat(normalized * 8f + i * 0.37f, 1f) > 0.18f ? 1f : 0f;
                    LightningVFXMath.SetLine(secondaryBolts[i], secondaryPaths[i], profile.MainBodyWidth * 0.34f, profile.CyanColor, fade * offsetFlicker * 0.85f, elapsed * (5f + i), tiling);
                }
            }

            int activeBranches = Mathf.Min(profile.BranchCount, branches.Length);
            for (int i = 0; i < branches.Length; i++)
            {
                if (i < activeBranches)
                {
                    float branchFlicker = Mathf.Repeat(normalized * 11f + i * 0.23f, 1f) > 0.28f ? 1f : 0f;
                    LightningVFXMath.SetLine(branches[i], branchPaths[i], profile.CoreWidth * 0.55f, i % 3 == 0 ? profile.VioletColor : profile.CyanColor, fade * branchFlicker, -elapsed * 8f, 1.4f);
                }
            }
        }

        private void EmitSheath()
        {
            if (particleSheath == null || basePath.Length < 2)
            {
                return;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(profile.BeamParticleAmount * profile.QualityMultiplier));
            for (int i = 0; i < count; i++)
            {
                int segment = Mathf.Clamp((int)(LightningVFXMath.Next01(random) * (basePath.Length - 1)), 0, basePath.Length - 2);
                Vector3 position = Vector3.Lerp(basePath[segment], basePath[segment + 1], LightningVFXMath.Next01(random));
                Vector3 velocity = UnityEngine.Random.onUnitSphere * Mathf.Lerp(0.8f, 3.5f, LightningVFXMath.Next01(random));
                ParticleSystem.EmitParams emit = new()
                {
                    position = position,
                    velocity = velocity,
                    startSize = Mathf.Lerp(0.045f, 0.14f, LightningVFXMath.Next01(random)),
                    startColor = i % 4 == 0 ? profile.VioletColor : profile.CyanColor
                };
                particleSheath.Emit(emit, 1);
            }
        }

        private void EmitFlash(Vector3 position)
        {
            if (beamFlashes == null)
            {
                return;
            }

            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                startSize = Mathf.Lerp(0.28f, 0.62f, LightningVFXMath.Next01(random)),
                startColor = profile.WhiteHotColor
            };
            beamFlashes.Emit(emit, 1);
        }

        private void ApplyParticleProfile()
        {
            ConfigureParticle(beamFlashes, 0.11f, 0.42f, profile.WhiteHotColor);
            ConfigureParticle(particleSheath, 0.22f, 0.09f, profile.CyanColor);
        }

        private Vector3 ResolveSource()
        {
            if (context.Source != null)
            {
                MMOAbilityVfxAnchors sourceAnchors = context.Source.GetComponent<MMOAbilityVfxAnchors>();
                if (sourceAnchors != null)
                {
                    return sourceAnchors.ResolveCastOriginPosition(context.Definition);
                }
            }

            return context.SourcePosition;
        }

        private void RequestHitOnce()
        {
            if (requestedHit)
            {
                return;
            }

            requestedHit = true;
            context.RequestHit?.Invoke();
        }

        private void StopParticles(bool clear = false)
        {
            StopParticle(beamFlashes, clear);
            StopParticle(particleSheath, clear);
        }

        private void SetAllLines(bool enabled)
        {
            SetLine(whiteCore, enabled);
            SetLine(cyanBody, enabled);
            SetLine(blueOuterBody, enabled);
            SetLine(violetGlow, enabled);
            foreach (LineRenderer line in secondaryBolts) SetLine(line, enabled);
            foreach (LineRenderer line in branches) SetLine(line, enabled);
        }

        private static void SetLine(LineRenderer line, bool enabled)
        {
            if (line != null) line.enabled = enabled;
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
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startColor = color;
        }

        private static void StopParticle(ParticleSystem system, bool clear)
        {
            if (system != null)
            {
                system.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
