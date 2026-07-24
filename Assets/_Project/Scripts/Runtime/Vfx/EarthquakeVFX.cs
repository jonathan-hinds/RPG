using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class EarthquakeVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        private const string AbilityId = "shaman_earthquake";
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] private EarthquakeVFXProfile profile;
        [SerializeField] private ParticleSystem tensionDust;
        [SerializeField] private ParticleSystem compressionFlash;
        [SerializeField] private ParticleSystem pressureRing;
        [SerializeField] private ParticleSystem dirtRing;
        [SerializeField] private ParticleSystem leadingDust;
        [SerializeField] private ParticleSystem mainDust;
        [SerializeField] private ParticleSystem smokeRing;
        [SerializeField] private ParticleSystem fineDustWake;
        [SerializeField] private ParticleSystem dirtDebris;
        [SerializeField] private ParticleSystem rockDebris;
        [SerializeField] private ParticleSystem[] chargeEarthLayers = Array.Empty<ParticleSystem>();
        [SerializeField] private Renderer[] crackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform[] groundChunks = Array.Empty<Transform>();
        [SerializeField] private Renderer[] groundChunkRenderers = Array.Empty<Renderer>();
        [SerializeField] private EarthquakeTargetReactionVFX[] targetReactionPool = Array.Empty<EarthquakeTargetReactionVFX>();

        private readonly List<PendingReaction> pendingReactions = new();
        private readonly HashSet<MMOCombatant> reactedTargets = new();
        private MaterialPropertyBlock properties;
        private MMOAbilityVfxContext context;
        private MMOCombatant sourceCombatant;
        private EarthquakeTerrainSample terrainSample;
        private Vector3[] chunkBasePositions = Array.Empty<Vector3>();
        private Quaternion[] chunkBaseRotations = Array.Empty<Quaternion>();
        private Vector3[] chunkBaseScales = Array.Empty<Vector3>();
        private float[] chunkTimingOffsets = Array.Empty<float>();
        private Vector3[] chunkGroundPositions = Array.Empty<Vector3>();
        private Vector3[] chunkGroundNormals = Array.Empty<Vector3>();
        private Quaternion[] chunkGroundRotations = Array.Empty<Quaternion>();
        private EarthquakeTerrainSample[] chunkTerrainSamples = Array.Empty<EarthquakeTerrainSample>();
        private Vector3[] crackBasePositions = Array.Empty<Vector3>();
        private Quaternion[] crackBaseRotations = Array.Empty<Quaternion>();
        private Vector3[] crackGroundPositions = Array.Empty<Vector3>();
        private Vector3[] crackGroundNormals = Array.Empty<Vector3>();
        private Quaternion[] crackGroundRotations = Array.Empty<Quaternion>();
        private float startedAt;
        private float lodFactor = 1f;
        private bool impactPlayed;
        private bool initialized;

        public EarthquakeVFXProfile Profile => profile;
        public bool IsPlaying => initialized;

        public void ConfigureAuthoring(
            EarthquakeVFXProfile newProfile,
            ParticleSystem[] particleLayers,
            ParticleSystem[] newChargeEarthLayers,
            Renderer[] newCrackRenderers,
            Transform[] newGroundChunks,
            Renderer[] newGroundChunkRenderers,
            EarthquakeTargetReactionVFX[] newTargetReactionPool)
        {
            if (particleLayers == null || particleLayers.Length != 10)
            {
                throw new ArgumentException("Earthquake requires exactly ten authored particle layers.");
            }

            profile = newProfile;
            tensionDust = particleLayers[0];
            compressionFlash = particleLayers[1];
            pressureRing = particleLayers[2];
            dirtRing = particleLayers[3];
            leadingDust = particleLayers[4];
            mainDust = particleLayers[5];
            smokeRing = particleLayers[6];
            fineDustWake = particleLayers[7];
            dirtDebris = particleLayers[8];
            rockDebris = particleLayers[9];
            chargeEarthLayers = newChargeEarthLayers ?? Array.Empty<ParticleSystem>();
            crackRenderers = newCrackRenderers ?? Array.Empty<Renderer>();
            groundChunks = newGroundChunks ?? Array.Empty<Transform>();
            groundChunkRenderers = newGroundChunkRenderers ?? Array.Empty<Renderer>();
            targetReactionPool = newTargetReactionPool ?? Array.Empty<EarthquakeTargetReactionVFX>();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError("EarthquakeVFX requires an Earthquake profile.", this);
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            context = newContext;
            sourceCombatant = context.Source != null ? context.Source.GetComponent<MMOCombatant>() : null;
            Vector3 origin = context.Source != null ? context.Source.position : transform.position;
            terrainSample = EarthquakeTerrainSampler.Sample(origin, context.Source);
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(terrainSample.Position, Quaternion.identity);
            lodFactor = ResolveLodFactor(terrainSample.Position);
            startedAt = Time.time;
            impactPlayed = false;
            initialized = true;
            pendingReactions.Clear();
            reactedTargets.Clear();
            CacheGroundState();
            ResetVisualState();
            ProjectVisualsOntoGround();
            ApplyTerrainToChunks();
            EmitAnticipation();
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        public void ResetForPool()
        {
            initialized = false;
            impactPlayed = false;
            sourceCombatant = null;
            pendingReactions.Clear();
            reactedTargets.Clear();
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            foreach (ParticleSystem system in ParticleLayers()) EarthquakeVFXUtility.StopAndClear(system);
            for (int i = 0; i < crackRenderers.Length; i++) SetRenderer(crackRenderers[i], Color.black, 0f);
            for (int i = 0; i < groundChunks.Length; i++)
            {
                Transform chunk = groundChunks[i];
                if (chunk == null) continue;
                if (i < chunkBasePositions.Length) chunk.localPosition = chunkBasePositions[i];
                if (i < chunkBaseRotations.Length) chunk.localRotation = chunkBaseRotations[i];
                if (i < chunkBaseScales.Length) chunk.localScale = chunkBaseScales[i];
                chunk.gameObject.SetActive(false);
            }
            foreach (EarthquakeTargetReactionVFX reaction in targetReactionPool) reaction?.ResetForPool();
        }

        private void OnDestroy()
        {
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
        }

        private void Update()
        {
            if (!initialized || profile == null) return;
            float elapsed = Time.time - startedAt;
            if (!impactPlayed && elapsed >= profile.AnticipationDuration)
            {
                impactPlayed = true;
                EmitMainImpact();
            }

            AnimateCracks(elapsed);
            AnimateGroundChunks(elapsed);
            PlayDueTargetReactions(elapsed);
            if (elapsed >= profile.TotalLifetime)
            {
                initialized = false;
                MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
                MMOAbilityVfxPool.Release(gameObject);
            }
        }

        private void EmitAnticipation()
        {
            System.Random random = CreateRandom(17);
            Vector3 center = terrainSample.Position + terrainSample.Normal * profile.GroundLayerOffset;
            EarthquakeVFXUtility.EmitRadial(tensionDust, center, ScaleCount(Mathf.Max(8, profile.FineDirtAmount / 5), 4),
                0.32f, 0.04f, 0.12f * profile.OverallScale, profile.AnticipationDuration + 0.18f,
                new Color(profile.DustColor.r, profile.DustColor.g, profile.DustColor.b, profile.DustColor.a * 0.48f), random, 0.75f,
                terrainSample.Normal);
        }

        private void EmitMainImpact()
        {
            System.Random random = CreateRandom(53);
            Vector3 center = terrainSample.Position + terrainSample.Normal * profile.GroundLayerOffset;
            EarthquakeVFXUtility.EmitRingParticle(compressionFlash, center, 1.6f * profile.OverallScale, 0.34f, profile.ImpactColor);
            EarthquakeVFXUtility.EmitRingParticle(pressureRing, center, profile.Radius * 2.04f, profile.WaveDuration, new Color(1f, 0.86f, 0.62f, 0.28f));
            EarthquakeVFXUtility.EmitRadial(dirtRing, center, ScaleCount(profile.DirtClumpCount, 12), profile.DirtHorizontalForce,
                profile.DirtVerticalForceLimit, profile.DirtRingWidth, profile.DirtLifetime, profile.DirtColor, random, 0.28f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(leadingDust, center, ScaleCount(Mathf.Max(12, profile.DustDensity / 3), 8), profile.WaveSpeed * 0.94f,
                0.16f, profile.LeadingEdgeWidth, profile.DustLifetime * 0.72f,
                new Color(0.9f, 0.75f, 0.5f, profile.DustOpacity * 0.58f), random, 0.35f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(mainDust, center, ScaleCount(profile.DustDensity, 20), profile.WaveSpeed * 0.72f,
                profile.DustGroundHeight, profile.MainRingWidth, profile.DustLifetime,
                new Color(profile.DustColor.r, profile.DustColor.g, profile.DustColor.b, profile.DustOpacity), random, 0.42f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(smokeRing, center, ScaleCount(profile.SmokeDensity, 10), profile.SmokeHorizontalSpeed,
                profile.SmokeMaximumHeight * 0.2f, profile.SmokeRingWidth, profile.SmokeFadeDuration, profile.SmokeColor, random, 0.5f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(fineDustWake, center, ScaleCount(profile.WakeAmount, 12), profile.WaveSpeed * 0.38f,
                0.12f, profile.MainRingWidth * 0.72f, profile.DustLifetime * 1.18f,
                new Color(profile.DustColor.r, profile.DustColor.g, profile.DustColor.b, profile.DustColor.a * 0.36f), random, 0.48f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(dirtDebris, center, ScaleCount(profile.FineDirtAmount, 14), profile.DirtHorizontalForce * 0.82f,
                profile.DirtVerticalForceLimit, 0.14f * profile.OverallScale, profile.DirtLifetime, profile.DirtColor, random, 0.25f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(rockDebris, center, ScaleCount(profile.RockCount, 6), profile.RockHorizontalVelocity,
                profile.RockMaximumVerticalVelocity, 0.22f * profile.OverallScale, profile.RockLifetime, profile.StoneColor, random, 0.3f, terrainSample.Normal);

            EmitChargeEarthLibrary(center, random);
        }

        private void EmitChargeEarthLibrary(Vector3 center, System.Random random)
        {
            if (chargeEarthLayers == null || chargeEarthLayers.Length < 8) return;

            Color untinted = Color.white;
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[0], center, ScaleCount(48, 16), profile.WaveSpeed * 0.58f,
                0.18f, 1.25f * profile.OverallScale, 1.45f, untinted, random, 0.56f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[1], center, ScaleCount(72, 20), profile.WaveSpeed * 0.42f,
                0.12f, 0.86f * profile.OverallScale, 1.9f, untinted, random, 0.68f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[2], center, ScaleCount(36, 10), profile.WaveSpeed * 0.78f,
                0.08f, 0.55f * profile.OverallScale, 1.05f, untinted, random, 0.3f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[3], center, ScaleCount(34, 10), profile.DirtHorizontalForce * 0.82f,
                profile.DirtVerticalForceLimit * 0.45f, 0.22f * profile.OverallScale, 1.2f, untinted, random, 0.24f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[4], center, ScaleCount(26, 8), profile.DirtHorizontalForce * 0.68f,
                profile.DirtVerticalForceLimit * 0.28f, 0.13f * profile.OverallScale, 1f, untinted, random, 0.2f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[5], center, ScaleCount(16, 5), profile.RockHorizontalVelocity * 0.72f,
                profile.RockMaximumVerticalVelocity * 0.65f, 0.18f * profile.OverallScale, 1.25f, untinted, random, 0.24f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRadial(chargeEarthLayers[6], center, ScaleCount(20, 8), profile.WaveSpeed * 0.48f,
                0.15f, 0.9f * profile.OverallScale, 0.7f, untinted, random, 0.32f, terrainSample.Normal);
            EarthquakeVFXUtility.EmitRingParticle(chargeEarthLayers[7], center, profile.Radius * 2f, profile.WaveDuration, untinted, terrainSample.Normal);
        }

        private void AnimateCracks(float elapsed)
        {
            for (int i = 0; i < crackRenderers.Length; i++)
            {
                Renderer crack = crackRenderers[i];
                if (crack == null) continue;
                float distance = Vector3.Distance(terrainSample.Position, crackGroundPositions[i]);
                float start = profile.AnticipationDuration + distance / Mathf.Max(0.1f, profile.WaveSpeed);
                float age = elapsed - start;
                float expansion = Mathf.Clamp01(age / Mathf.Max(0.08f, profile.CrackExpansionSpeed));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.22f) / profile.CrackFadeDuration));
                float alpha = age >= 0f ? expansion * fade * profile.CrackDarkness : 0f;
                crack.transform.SetPositionAndRotation(
                    crackGroundPositions[i] + crackGroundNormals[i] * profile.GroundLayerOffset,
                    crackGroundRotations[i]);
                crack.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, profile.CrackWidth * Mathf.Lerp(1.8f, 3.8f, (i * 0.31f) % 1f), expansion);
                Color color = Color.Lerp(new Color(0.035f, 0.025f, 0.02f, 1f), profile.ImpactColor,
                    profile.WarmHighlightStrength * (1f - expansion));
                SetRenderer(crack, color, alpha);
            }
        }

        private void AnimateGroundChunks(float elapsed)
        {
            int activeCount = Mathf.Min(ScaleCount(profile.GroundSectionCount, 6), groundChunks.Length);
            for (int i = 0; i < groundChunks.Length; i++)
            {
                Transform chunk = groundChunks[i];
                if (chunk == null) continue;
                if (i >= activeCount)
                {
                    chunk.gameObject.SetActive(false);
                    continue;
                }

                float distance = Vector3.Distance(terrainSample.Position, chunkGroundPositions[i]);
                float start = profile.AnticipationDuration + distance / Mathf.Max(0.1f, profile.WaveSpeed) + chunkTimingOffsets[i];
                float age = elapsed - start;
                bool visible = age >= -0.04f && age <= profile.SettleDuration + 0.42f;
                chunk.gameObject.SetActive(visible);
                if (!visible) continue;

                float t = Mathf.Clamp01(age / Mathf.Max(0.1f, profile.SettleDuration));
                float liftEnvelope = Mathf.Sin(t * Mathf.PI);
                float lift = Mathf.Lerp(profile.CubeLiftHeightRange.x, profile.CubeLiftHeightRange.y, (i * 0.618f) % 1f) * liftEnvelope;
                float compression = age < 0f ? -profile.SinkDepth * (1f - Mathf.Abs(age) / 0.04f) : 0f;
                chunk.position = chunkGroundPositions[i] + chunkGroundNormals[i] * (lift + compression);
                float sign = i % 2 == 0 ? 1f : -1f;
                chunk.rotation = chunkGroundRotations[i] * Quaternion.Euler(
                    profile.TiltAmount * liftEnvelope * sign,
                    Mathf.Sin(t * Mathf.PI * 2f + i) * profile.TiltAmount * 0.18f,
                    profile.TiltAmount * liftEnvelope * 0.58f * -sign);
            }
        }

        private void OnCombatEventResolved(CombatEventRecord record, MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability)
        {
            if (!initialized || record == null || record.eventType != CombatEventType.DamageResolved || target == null
                || (ability != null ? ability.AbilityId : record.abilityId) != AbilityId || !MatchesSource(source)) return;
            if (!reactedTargets.Add(target)) return;
            float distance = Vector3.Distance(terrainSample.Position, target.transform.position);
            if (distance > profile.Radius + 1.25f) return;
            float playAt = profile.AnticipationDuration + Mathf.Clamp(distance / Mathf.Max(0.1f, profile.WaveSpeed), 0.02f, profile.WaveDuration);
            pendingReactions.Add(new PendingReaction(target.transform, playAt));
        }

        private bool MatchesSource(MMOCombatant source)
        {
            if (source == null) return false;
            return source == sourceCombatant || (context.Source != null
                && (source.transform == context.Source || source.transform.IsChildOf(context.Source) || context.Source.IsChildOf(source.transform)));
        }

        private void PlayDueTargetReactions(float elapsed)
        {
            for (int i = pendingReactions.Count - 1; i >= 0; i--)
            {
                PendingReaction pending = pendingReactions[i];
                if (elapsed < pending.PlayAt) continue;
                pendingReactions.RemoveAt(i);
                if (pending.Target == null) continue;
                EarthquakeTargetReactionVFX reaction = AcquireReaction();
                reaction?.Play(profile, pending.Target, ReleaseReaction);
            }
        }

        private EarthquakeTargetReactionVFX AcquireReaction()
        {
            foreach (EarthquakeTargetReactionVFX reaction in targetReactionPool)
            {
                if (reaction != null && !reaction.IsPlaying) return reaction;
            }
            if (targetReactionPool.Length == 0 || targetReactionPool[0] == null) return null;
            targetReactionPool[0].ResetForPool();
            return targetReactionPool[0];
        }

        private static void ReleaseReaction(EarthquakeTargetReactionVFX reaction) => reaction?.ResetForPool();

        private void CacheGroundState()
        {
            if (chunkBasePositions.Length == groundChunks.Length) return;
            chunkBasePositions = new Vector3[groundChunks.Length];
            chunkBaseRotations = new Quaternion[groundChunks.Length];
            chunkBaseScales = new Vector3[groundChunks.Length];
            chunkTimingOffsets = new float[groundChunks.Length];
            chunkGroundPositions = new Vector3[groundChunks.Length];
            chunkGroundNormals = new Vector3[groundChunks.Length];
            chunkGroundRotations = new Quaternion[groundChunks.Length];
            chunkTerrainSamples = new EarthquakeTerrainSample[groundChunks.Length];
            for (int i = 0; i < groundChunks.Length; i++)
            {
                Transform chunk = groundChunks[i];
                if (chunk == null) continue;
                chunkBasePositions[i] = chunk.localPosition;
                chunkBaseRotations[i] = chunk.localRotation;
                chunkBaseScales[i] = chunk.localScale;
                chunkTimingOffsets[i] = ((i * 0.75487766f) % 1f - 0.5f) * profile.WaveTimingVariation;
            }

            crackBasePositions = new Vector3[crackRenderers.Length];
            crackBaseRotations = new Quaternion[crackRenderers.Length];
            crackGroundPositions = new Vector3[crackRenderers.Length];
            crackGroundNormals = new Vector3[crackRenderers.Length];
            crackGroundRotations = new Quaternion[crackRenderers.Length];
            for (int i = 0; i < crackRenderers.Length; i++)
            {
                Renderer crack = crackRenderers[i];
                if (crack == null) continue;
                crackBasePositions[i] = crack.transform.localPosition;
                crackBaseRotations[i] = crack.transform.localRotation;
            }
        }

        private void ProjectVisualsOntoGround()
        {
            for (int i = 0; i < groundChunks.Length; i++)
            {
                Transform chunk = groundChunks[i];
                if (chunk == null) continue;
                Vector3 authoredOffset = chunkBasePositions[i];
                Vector3 plannedPosition = terrainSample.Position + new Vector3(authoredOffset.x, 0f, authoredOffset.z);
                EarthquakeTerrainSample sample = EarthquakeTerrainSampler.Sample(plannedPosition, context.Source);
                chunkTerrainSamples[i] = sample;
                chunkGroundNormals[i] = sample.Normal;
                chunkGroundPositions[i] = sample.Position + sample.Normal * authoredOffset.y;
                chunkGroundRotations[i] = Quaternion.FromToRotation(Vector3.up, sample.Normal) * chunkBaseRotations[i];
                chunk.SetPositionAndRotation(chunkGroundPositions[i], chunkGroundRotations[i]);
                chunk.localScale = chunkBaseScales[i];
            }

            for (int i = 0; i < crackRenderers.Length; i++)
            {
                Renderer crack = crackRenderers[i];
                if (crack == null) continue;
                Vector3 authoredOffset = crackBasePositions[i];
                Vector3 plannedPosition = terrainSample.Position + new Vector3(authoredOffset.x, 0f, authoredOffset.z);
                EarthquakeTerrainSample sample = EarthquakeTerrainSampler.Sample(plannedPosition, context.Source);
                crackGroundPositions[i] = sample.Position;
                crackGroundNormals[i] = sample.Normal;
                crackGroundRotations[i] = Quaternion.FromToRotation(Vector3.up, sample.Normal) * crackBaseRotations[i];
                crack.transform.SetPositionAndRotation(sample.Position + sample.Normal * profile.GroundLayerOffset, crackGroundRotations[i]);
            }
        }

        private void ResetVisualState()
        {
            foreach (ParticleSystem system in ParticleLayers()) EarthquakeVFXUtility.StopAndClear(system);
            for (int i = 0; i < crackRenderers.Length; i++) SetRenderer(crackRenderers[i], Color.black, 0f);
            for (int i = 0; i < groundChunks.Length; i++)
            {
                Transform chunk = groundChunks[i];
                if (chunk == null) continue;
                chunk.localPosition = chunkBasePositions[i];
                chunk.localRotation = chunkBaseRotations[i];
                chunk.localScale = chunkBaseScales[i];
                chunk.gameObject.SetActive(false);
            }
            foreach (EarthquakeTargetReactionVFX reaction in targetReactionPool) reaction?.ResetForPool();
        }

        private void ApplyTerrainToChunks()
        {
            properties ??= new MaterialPropertyBlock();
            for (int i = 0; i < groundChunkRenderers.Length; i++)
            {
                Renderer renderer = groundChunkRenderers[i];
                if (renderer == null) continue;
                EarthquakeTerrainSample sample = i < chunkTerrainSamples.Length ? chunkTerrainSamples[i] : terrainSample;
                int frame = !profile.GrassTopSupport && sample.SurfaceFrame == 3 ? 0 : sample.SurfaceFrame;
                Vector4 topTransform = sample.SurfaceTexture != null
                    ? sample.SurfaceTransform
                    : EarthquakeTerrainSampler.AtlasTransform(frame);
                Color topTint = sample.SurfaceTexture != null
                    ? Color.Lerp(Color.white, sample.Tint, profile.TerrainTintStrength * 0.35f)
                    : Color.Lerp(Color.white, sample.Tint, profile.TerrainTintStrength);
                properties.Clear();
                if (sample.SurfaceTexture != null) properties.SetTexture(BaseMapId, sample.SurfaceTexture);
                properties.SetVector(BaseMapStId, topTransform);
                properties.SetColor(TintId, topTint);
                renderer.SetPropertyBlock(properties, 0);
                properties.Clear();
                properties.SetVector(BaseMapStId, EarthquakeTerrainSampler.AtlasTransform(0));
                properties.SetColor(TintId, Color.Lerp(profile.DirtColor, sample.Tint, 0.22f));
                renderer.SetPropertyBlock(properties, 1);
            }
        }

        private float ResolveLodFactor(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null) return 1f;
            float distance = Vector3.Distance(camera.transform.position, position);
            if (distance <= profile.DistantReductionStart) return 1f;
            float reduction = Mathf.InverseLerp(profile.DistantReductionStart, profile.CullDistance, distance);
            return Mathf.Lerp(1f, profile.MinimumDistantDensity, reduction);
        }

        private int ScaleCount(int count, int minimum)
        {
            return count <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(count * lodFactor), minimum, count);
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

        private ParticleSystem[] ParticleLayers()
        {
            int chargeCount = chargeEarthLayers?.Length ?? 0;
            ParticleSystem[] layers = new ParticleSystem[10 + chargeCount];
            layers[0] = tensionDust;
            layers[1] = compressionFlash;
            layers[2] = pressureRing;
            layers[3] = dirtRing;
            layers[4] = leadingDust;
            layers[5] = mainDust;
            layers[6] = smokeRing;
            layers[7] = fineDustWake;
            layers[8] = dirtDebris;
            layers[9] = rockDebris;
            if (chargeCount > 0) Array.Copy(chargeEarthLayers, 0, layers, 10, chargeCount);
            return layers;
        }

        private System.Random CreateRandom(int salt)
        {
            int seed = salt;
            seed = seed * 397 ^ Mathf.RoundToInt(terrainSample.Position.x * 10f);
            seed = seed * 397 ^ Mathf.RoundToInt(terrainSample.Position.z * 10f);
            return new System.Random(seed);
        }

        private readonly struct PendingReaction
        {
            public readonly Transform Target;
            public readonly float PlayAt;

            public PendingReaction(Transform target, float playAt)
            {
                Target = target;
                PlayAt = playAt;
            }
        }
    }
}
