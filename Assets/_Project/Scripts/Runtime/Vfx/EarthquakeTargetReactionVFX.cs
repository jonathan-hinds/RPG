using System;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class EarthquakeTargetReactionVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] private Renderer crackRenderer;
        [SerializeField] private Renderer impactPulseRenderer;
        [SerializeField] private Transform[] groundBlocks = Array.Empty<Transform>();
        [SerializeField] private Renderer[] groundBlockRenderers = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem dustBurst;
        [SerializeField] private ParticleSystem dirtClumps;
        [SerializeField] private ParticleSystem rockDebris;

        private MaterialPropertyBlock properties;
        private EarthquakeVFXProfile profile;
        private Action<EarthquakeTargetReactionVFX> completed;
        private Vector3[] blockBasePositions = Array.Empty<Vector3>();
        private Quaternion[] blockBaseRotations = Array.Empty<Quaternion>();
        private float startedAt;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(
            Renderer newCrackRenderer,
            Renderer newImpactPulseRenderer,
            Transform[] newGroundBlocks,
            Renderer[] newGroundBlockRenderers,
            ParticleSystem newDustBurst,
            ParticleSystem newDirtClumps,
            ParticleSystem newRockDebris)
        {
            crackRenderer = newCrackRenderer;
            impactPulseRenderer = newImpactPulseRenderer;
            groundBlocks = newGroundBlocks ?? Array.Empty<Transform>();
            groundBlockRenderers = newGroundBlockRenderers ?? Array.Empty<Renderer>();
            dustBurst = newDustBurst;
            dirtClumps = newDirtClumps;
            rockDebris = newRockDebris;
        }

        public void Play(EarthquakeVFXProfile newProfile, Transform target, Action<EarthquakeTargetReactionVFX> onCompleted)
        {
            profile = newProfile;
            completed = onCompleted;
            playing = profile != null && target != null;
            gameObject.SetActive(playing);
            if (!playing) return;

            EarthquakeTerrainSample sample = EarthquakeTerrainSampler.Sample(target.position, target);
            transform.position = sample.Position + sample.Normal * profile.GroundLayerOffset;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, sample.Normal);
            startedAt = Time.time;
            CacheBlockState();
            ApplyTerrain(sample);
            SetRenderer(crackRenderer, Color.Lerp(Color.black, profile.DirtColor, 0.12f), 0f);
            SetRenderer(impactPulseRenderer, profile.ImpactColor * profile.ImpactPulseStrength, 0f);

            int seed = Mathf.RoundToInt(sample.Position.x * 10f) * 397
                ^ Mathf.RoundToInt(sample.Position.y * 10f) * 17
                ^ Mathf.RoundToInt(sample.Position.z * 10f);
            System.Random random = new(seed);
            EarthquakeVFXUtility.EmitRadial(dustBurst, transform.position, 12, 2.4f, 0.24f,
                profile.LocalDustRingSize * 0.42f, 0.62f, profile.DustColor, random, 0.12f, sample.Normal);
            EarthquakeVFXUtility.EmitRadial(dirtClumps, transform.position, 8, 3.2f, 0.4f,
                0.18f * profile.OverallScale, 0.72f, profile.DirtColor, random, 0.08f, sample.Normal);
            EarthquakeVFXUtility.EmitRadial(rockDebris, transform.position, profile.LocalRockCount, 2.7f, 0.55f,
                0.14f * profile.OverallScale, 0.82f, profile.StoneColor, random, 0.08f, sample.Normal);
        }

        public void ResetForPool()
        {
            playing = false;
            profile = null;
            completed = null;
            SetRenderer(crackRenderer, Color.black, 0f);
            SetRenderer(impactPulseRenderer, Color.white, 0f);
            for (int i = 0; i < groundBlocks.Length; i++)
            {
                if (groundBlocks[i] != null) groundBlocks[i].gameObject.SetActive(false);
            }
            EarthquakeVFXUtility.StopAndClear(dustBurst);
            EarthquakeVFXUtility.StopAndClear(dirtClumps);
            EarthquakeVFXUtility.StopAndClear(rockDebris);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || profile == null) return;
            float elapsed = Time.time - startedAt;
            float duration = 0.72f;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            float crackAlpha = t < 0.42f ? Mathf.SmoothStep(0f, 1f, t / 0.42f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.42f) / 0.58f);
            if (crackRenderer != null)
            {
                crackRenderer.transform.localScale = Vector3.one * profile.LocalCrackScale * Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, t * 2.2f));
            }
            SetRenderer(crackRenderer, Color.Lerp(Color.black, profile.DirtColor, 0.12f), crackAlpha * profile.CrackDarkness);
            if (impactPulseRenderer != null)
            {
                impactPulseRenderer.transform.localScale = Vector3.one * profile.LocalDustRingSize * Mathf.Lerp(0.22f, 1.35f, t);
            }
            SetRenderer(impactPulseRenderer, profile.ImpactColor * profile.ImpactPulseStrength, pulse * 0.48f);

            for (int i = 0; i < groundBlocks.Length; i++)
            {
                Transform block = groundBlocks[i];
                if (block == null) continue;
                block.gameObject.SetActive(t < 0.95f);
                float localT = Mathf.Clamp01((t - i * 0.035f) / 0.82f);
                float lift = Mathf.Sin(localT * Mathf.PI) * profile.CubeLiftHeightRange.y * Mathf.Lerp(0.38f, 0.76f, (i * 0.37f) % 1f);
                block.localPosition = blockBasePositions[i] + Vector3.up * lift;
                block.localRotation = blockBaseRotations[i] * Quaternion.Euler(
                    Mathf.Sin(localT * Mathf.PI) * profile.TiltAmount * (i % 2 == 0 ? 1f : -1f), 0f,
                    Mathf.Sin(localT * Mathf.PI) * profile.TiltAmount * 0.42f);
            }

            if (t >= 1f)
            {
                playing = false;
                completed?.Invoke(this);
            }
        }

        private void CacheBlockState()
        {
            if (blockBasePositions.Length == groundBlocks.Length) return;
            blockBasePositions = new Vector3[groundBlocks.Length];
            blockBaseRotations = new Quaternion[groundBlocks.Length];
            for (int i = 0; i < groundBlocks.Length; i++)
            {
                if (groundBlocks[i] == null) continue;
                blockBasePositions[i] = groundBlocks[i].localPosition;
                blockBaseRotations[i] = groundBlocks[i].localRotation;
            }
        }

        private void ApplyTerrain(EarthquakeTerrainSample sample)
        {
            properties ??= new MaterialPropertyBlock();
            Vector4 atlas = sample.SurfaceTexture != null
                ? sample.SurfaceTransform
                : EarthquakeTerrainSampler.AtlasTransform(sample.SurfaceFrame);
            Color top = sample.SurfaceTexture != null
                ? Color.Lerp(Color.white, sample.Tint, profile.TerrainTintStrength * 0.35f)
                : Color.Lerp(Color.white, sample.Tint, profile.TerrainTintStrength);
            for (int i = 0; i < groundBlockRenderers.Length; i++)
            {
                Renderer renderer = groundBlockRenderers[i];
                if (renderer == null) continue;
                properties.Clear();
                if (sample.SurfaceTexture != null) properties.SetTexture(BaseMapId, sample.SurfaceTexture);
                properties.SetVector(BaseMapStId, atlas);
                properties.SetColor(TintId, top);
                renderer.SetPropertyBlock(properties, 0);
                properties.Clear();
                properties.SetVector(BaseMapStId, EarthquakeTerrainSampler.AtlasTransform(0));
                properties.SetColor(TintId, Color.Lerp(profile.DirtColor, sample.Tint, 0.24f));
                renderer.SetPropertyBlock(properties, 1);
            }
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity)
        {
            if (renderer == null) return;
            properties ??= new MaterialPropertyBlock();
            properties.Clear();
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, tint * (profile != null ? profile.OverallBrightness : 1f));
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            renderer.SetPropertyBlock(properties);
            renderer.enabled = opacity > 0.001f;
        }
    }
}
