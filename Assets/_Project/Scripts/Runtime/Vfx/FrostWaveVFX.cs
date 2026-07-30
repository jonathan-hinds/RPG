using System;
using System.Collections.Generic;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    [DisallowMultipleComponent]
    public sealed class FrostWaveVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        private const string AbilityId = "mage_frost_wave";
        private static readonly RaycastHit[] GroundHits = new RaycastHit[24];
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        [SerializeField] private FrostWaveVFXProfile profile;
        [SerializeField] private Renderer centerGlow;
        [SerializeField] private FrostWaveRingVFX expandingRing;
        [SerializeField] private FrostWaveGroundFrostVFX groundFrost;
        [SerializeField] private FrostWaveRadialFrontVFX radialFront;
        [SerializeField] private ParticleSystem openingSnow;
        [SerializeField] private ParticleSystem openingMist;
        [SerializeField] private ParticleSystem outwardShards;
        [SerializeField] private ParticleSystem waveSnow;
        [SerializeField] private ParticleSystem frostStreaks;
        [SerializeField] private ParticleSystem waveMist;
        [SerializeField] private Light pulseLight;
        [SerializeField] private GameObject enemyImpactPrefab;

        private MaterialPropertyBlock properties;
        private readonly List<PendingReaction> pendingReactions = new();
        private readonly List<FrostWaveEnemyImpactVFX> activeTargetReactions = new();
        private readonly HashSet<EntityId> reactedTargetIds = new();
        private MMOAbilityVfxContext context;
        private MMOCombatant sourceCombatant;
        private float radius;
        private float startedAt;
        private float visibility = 1f;
        private bool initialized;

        public FrostWaveVFXProfile Profile => profile;
        public bool IsPlaying => initialized;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        public void ConfigureAuthoring(
            FrostWaveVFXProfile newProfile,
            Renderer newCenterGlow,
            FrostWaveRingVFX newExpandingRing,
            FrostWaveGroundFrostVFX newGroundFrost,
            FrostWaveRadialFrontVFX newRadialFront,
            ParticleSystem newOpeningSnow,
            ParticleSystem newOpeningMist,
            ParticleSystem newOutwardShards,
            ParticleSystem newWaveSnow,
            ParticleSystem newFrostStreaks,
            ParticleSystem newWaveMist,
            Light newPulseLight,
            GameObject newEnemyImpactPrefab)
        {
            profile = newProfile;
            centerGlow = newCenterGlow;
            expandingRing = newExpandingRing;
            groundFrost = newGroundFrost;
            radialFront = newRadialFront;
            openingSnow = newOpeningSnow;
            openingMist = newOpeningMist;
            outwardShards = newOutwardShards;
            waveSnow = newWaveSnow;
            frostStreaks = newFrostStreaks;
            waveMist = newWaveMist;
            pulseLight = newPulseLight;
            enemyImpactPrefab = newEnemyImpactPrefab;
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null || expandingRing == null || groundFrost == null || radialFront == null || enemyImpactPrefab == null)
            {
                Debug.LogError("FrostWaveVFX is missing its profile, phase controllers, or enemy impact prefab.", this);
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            context = newContext;
            sourceCombatant = context.Source != null ? context.Source.GetComponent<MMOCombatant>() : null;
            radius = profile.ResolveRadius(context.Ability);
            Vector3 origin = context.Source != null ? context.Source.position : context.SourcePosition;
            Vector3 groundPosition = SampleGround(origin, context.Source);
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(groundPosition + Vector3.up * profile.GroundOffset, Quaternion.identity);
            transform.localScale = Vector3.one;
            startedAt = Time.time;
            initialized = true;
            pendingReactions.Clear();
            reactedTargetIds.Clear();
            activeTargetReactions.Clear();
            ResetChildren();
            UpdateVisibility();
            expandingRing.Play(profile, radius);
            groundFrost.Play(profile, radius);
            radialFront.Play(profile, radius);
            PlayOpening();
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        public void ResetForPool()
        {
            initialized = false;
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            pendingReactions.Clear();
            reactedTargetIds.Clear();
            sourceCombatant = null;
            if (centerGlow != null) centerGlow.enabled = false;
            if (pulseLight != null) pulseLight.enabled = false;
            expandingRing?.ResetForPool();
            groundFrost?.ResetForPool();
            radialFront?.ResetForPool();
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            for (int i = activeTargetReactions.Count - 1; i >= 0; i--)
            {
                FrostWaveEnemyImpactVFX reaction = activeTargetReactions[i];
                if (reaction != null)
                {
                    reaction.CancelImmediate();
                }
            }
            activeTargetReactions.Clear();
        }

        private void OnDisable()
        {
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
        }

        private void Update()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            AnimateCenter(elapsed);
            PlayDueTargetReactions(elapsed);
            if (elapsed >= profile.ControllerLifetime && pendingReactions.Count == 0 && !HasActiveTargetReaction())
            {
                initialized = false;
                MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
                MMOAbilityVfxPool.Release(gameObject);
            }
        }

        private void OnCombatEventResolved(
            CombatEventRecord record,
            MMOCombatant source,
            MMOCombatant target,
            RPGClone.Abilities.MMOAbilityDefinition ability)
        {
            if (!initialized
                || record == null
                || record.eventType != CombatEventType.DamageResolved
                || target == null
                || (ability != null ? ability.AbilityId : record.abilityId) != AbilityId
                || !MatchesSource(source))
            {
                return;
            }

            EntityId targetId = target.GetEntityId();
            if (!reactedTargetIds.Add(targetId))
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, target.transform.position);
            float travelDelay = profile.RingExpansionDuration * Mathf.Clamp01(distance / Mathf.Max(0.1f, radius));
            pendingReactions.Add(new PendingReaction(target.transform, Mathf.Clamp(travelDelay, 0.035f, profile.RingExpansionDuration)));
        }

        private bool MatchesSource(MMOCombatant source)
        {
            if (source == null)
            {
                return false;
            }

            return source == sourceCombatant
                || (context.Source != null
                    && (source.transform == context.Source
                        || source.transform.IsChildOf(context.Source)
                        || context.Source.IsChildOf(source.transform)));
        }

        private void PlayDueTargetReactions(float elapsed)
        {
            for (int i = pendingReactions.Count - 1; i >= 0; i--)
            {
                PendingReaction pending = pendingReactions[i];
                if (elapsed < pending.PlayAt)
                {
                    continue;
                }

                pendingReactions.RemoveAt(i);
                if (pending.Target == null)
                {
                    continue;
                }

                GameObject instance = MMOAbilityVfxPool.Spawn(
                    enemyImpactPrefab,
                    pending.Target.position,
                    Quaternion.identity,
                    null);
                FrostWaveEnemyImpactVFX reaction = instance != null
                    ? instance.GetComponent<FrostWaveEnemyImpactVFX>()
                    : null;
                if (reaction == null)
                {
                    if (instance != null) MMOAbilityVfxPool.Release(instance);
                    continue;
                }

                activeTargetReactions.Add(reaction);
                reaction.Play(profile, pending.Target, ReleaseTargetReaction);
            }
        }

        private void ReleaseTargetReaction(FrostWaveEnemyImpactVFX reaction)
        {
            if (reaction == null)
            {
                return;
            }

            activeTargetReactions.Remove(reaction);
            MMOAbilityVfxPool.Release(reaction.gameObject);
        }

        private bool HasActiveTargetReaction()
        {
            for (int i = activeTargetReactions.Count - 1; i >= 0; i--)
            {
                FrostWaveEnemyImpactVFX reaction = activeTargetReactions[i];
                if (reaction == null)
                {
                    activeTargetReactions.RemoveAt(i);
                }
            }
            return activeTargetReactions.Count > 0;
        }

        private void PlayOpening()
        {
            PlayBurst(openingSnow, profile.OpeningSnowAmount);
            PlayBurst(openingMist, Mathf.Max(4, profile.MistAmount / 2));
            PlayBurst(outwardShards, profile.OutwardShardAmount);
            PlayBurst(waveSnow, profile.WaveSnowAmount);
            PlayBurst(frostStreaks, profile.FrostStreakAmount);
            PlayBurst(waveMist, profile.MistAmount);
            if (centerGlow != null)
            {
                centerGlow.enabled = true;
            }
            if (pulseLight != null)
            {
                pulseLight.color = profile.PaleCyan;
                pulseLight.range = profile.LightRadius;
                pulseLight.intensity = 0f;
                pulseLight.enabled = profile.LightIntensity > 0f;
            }
        }

        private void AnimateCenter(float elapsed)
        {
            float glow = Mathf.Exp(-elapsed * 8.5f);
            if (centerGlow != null)
            {
                properties ??= new MaterialPropertyBlock();
                centerGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 3.4f, Mathf.Clamp01(elapsed / 0.16f));
                centerGlow.enabled = glow > 0.01f;
                centerGlow.GetPropertyBlock(properties);
                properties.SetColor(TintId, profile.PaleCyan);
                properties.SetFloat(OpacityId, glow * visibility);
                properties.SetFloat(BrightnessId, 3.5f * profile.OverallIntensity);
                properties.SetFloat(DissolveId, Mathf.Clamp01(elapsed / 0.28f));
                centerGlow.SetPropertyBlock(properties);
            }

            if (pulseLight != null)
            {
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, profile.LightDuration));
                float pulse = Mathf.Sin(normalized * Mathf.PI) * (1f - normalized * 0.25f);
                pulseLight.intensity = profile.LightIntensity * pulse * visibility;
                pulseLight.range = profile.LightRadius * Mathf.Lerp(0.55f, 1f, Mathf.Sin(normalized * Mathf.PI));
                pulseLight.enabled = normalized < 1f && pulseLight.intensity > 0.01f;
            }
        }

        private void UpdateVisibility()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                visibility = 1f;
                return;
            }

            float distance = Vector3.Distance(camera.transform.position, transform.position);
            visibility = distance <= profile.DistantReductionStart
                ? 1f
                : 1f - Mathf.InverseLerp(profile.DistantReductionStart, profile.CullDistance, distance);
        }

        private void ResetChildren()
        {
            expandingRing.ResetForPool();
            groundFrost.ResetForPool();
            radialFront.ResetForPool();
        }

        private Vector3 SampleGround(Vector3 position, Transform ignoredRoot)
        {
            int hitCount = Physics.RaycastNonAlloc(
                position + Vector3.up * profile.GroundProbeHeight,
                Vector3.down,
                GroundHits,
                profile.GroundProbeDistance,
                profile.GroundLayers,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = position;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.collider == null || hit.normal.y < 0.15f)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (ignoredRoot != null
                    && (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot) || ignoredRoot.IsChildOf(hitTransform)))
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<RPGClone.Characters.MMOCharacterIdentity>() != null
                    || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestPoint = hit.point;
            }

            return bestPoint;
        }

        private static void PlayBurst(ParticleSystem system, int count)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
            system.Clear(true);
            system.Play(true);
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
