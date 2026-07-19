using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Water
{
    [DisallowMultipleComponent]
    public sealed class WaterShieldVFX : MonoBehaviour, IMMOAbilityVfxInstance, IWaterShieldVFX
    {
        private const int OrbCount = 3;

        [Header("Configuration")]
        [SerializeField] private WaterShieldVFXProfile profile;
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private GameObject activationPrefab;
        [SerializeField] private GameObject absorbReactionPrefab;
        [SerializeField] private GameObject manaRestorePrefab;
        [SerializeField] private GameObject expirationPrefab;
        [SerializeField] private bool destroyOnComplete = true;

        [Header("Formation")]
        [SerializeField] private Transform formationRoot;

        private readonly WaterShieldOrbVFX[] orbs = new WaterShieldOrbVFX[OrbCount];
        private readonly List<WaterShieldReactionVFX> reactionPool = new();
        private MMOAbilityVfxContext context;
        private MMOCharacterBuffController buffController;
        private MMOCombatant casterCombatant;
        private Transform caster;
        private float startedAt;
        private float fallbackExpiresAt;
        private float expirationStartedAt;
        private float nextManaPulseAt;
        private float orbitDisturbanceStartedAt = float.NegativeInfinity;
        private int pendingAbsorbedMana;
        private int fallbackReactionIndex;
        private bool playing;
        private bool expiring;
        private bool buffWasObserved;

        public event Action<WaterShieldVFX> Completed;

        public bool IsPlaying => playing;
        public bool ReadyForPool => !playing;
        public WaterShieldVFXProfile Profile => profile;

        private void Awake()
        {
            StopImmediateInternal(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            playing = false;
        }

        private void LateUpdate()
        {
            if (!playing || profile == null || caster == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            if (!expiring)
            {
                UpdateOrbit(elapsed, 1f);
                CheckBuffState();
                if (Time.time >= nextManaPulseAt && elapsed > profile.ActivationDuration)
                {
                    PulseAllMana();
                    nextManaPulseAt = Time.time + profile.PersistentManaPulseInterval;
                }
            }
            else
            {
                float progress = Mathf.Clamp01((Time.time - expirationStartedAt) / profile.ExpirationDuration);
                UpdateOrbit(elapsed, 1f - Smooth01(progress));
                for (int i = 0; i < OrbCount; i++)
                {
                    orbs[i]?.SetFade(1f - Smooth01(progress));
                }

                if (progress >= 1f)
                {
                    Complete();
                }
            }
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            caster = newContext.Target != null ? newContext.Target : newContext.Source;
            if (caster == null)
            {
                Debug.LogError("WaterShieldVFX requires a caster transform.", this);
                StopImmediate();
                return;
            }

            WaterShieldVFX[] existing = caster.GetComponentsInChildren<WaterShieldVFX>(true);
            foreach (WaterShieldVFX candidate in existing)
            {
                if (candidate != null && candidate != this && candidate.IsPlaying)
                {
                    candidate.StopImmediate();
                }
            }

            buffController = caster.GetComponent<MMOCharacterBuffController>();
            casterCombatant = caster.GetComponent<MMOCombatant>();
            transform.position = caster.position;
            fallbackExpiresAt = Time.time + ResolveBuffDuration(newContext.Ability);
            buffWasObserved = HasBuff();
            Subscribe();
            Play();
        }

        public void Play()
        {
            if (profile == null || orbPrefab == null || formationRoot == null)
            {
                Debug.LogError("WaterShieldVFX is missing its profile, orb prefab, or formation root.", this);
                return;
            }

            ResetRuntimeObjects();
            transform.localScale = Vector3.one * profile.EffectScale;
            startedAt = Time.time;
            expirationStartedAt = float.NegativeInfinity;
            orbitDisturbanceStartedAt = float.NegativeInfinity;
            nextManaPulseAt = Time.time + profile.PersistentManaPulseInterval;
            pendingAbsorbedMana = 0;
            fallbackReactionIndex = 0;
            expiring = false;
            playing = true;

            Transform[] targets = new Transform[OrbCount];
            for (int i = 0; i < OrbCount; i++)
            {
                GameObject instance = Instantiate(orbPrefab, formationRoot);
                instance.name = $"Water Shield Orb {i + 1}";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(i * 17f, i * 53f, i * 11f);
                orbs[i] = instance.GetComponent<WaterShieldOrbVFX>();
                if (orbs[i] == null)
                {
                    Debug.LogError("Water Shield orb prefab is missing WaterShieldOrbVFX.", instance);
                    continue;
                }

                orbs[i].Play(profile, i);
                targets[i] = orbs[i].transform;
            }

            UpdateOrbit(0f, 1f);
            if (activationPrefab != null)
            {
                GameObject activationObject = Instantiate(activationPrefab, transform);
                activationObject.name = activationPrefab.name;
                WaterShieldActivationVFX activation = activationObject.GetComponent<WaterShieldActivationVFX>();
                activation?.Play(profile, targets);
            }
        }

        public void ReactToAbsorb(Vector3 incomingDirection, int manaRestored)
        {
            if (!playing || expiring || profile == null)
            {
                return;
            }

            int orbIndex = SelectImpactOrb(incomingDirection);
            WaterShieldOrbVFX reactingOrb = orbs[orbIndex];
            reactingOrb?.PulseAbsorb();
            for (int i = 0; i < OrbCount; i++)
            {
                if (i != orbIndex) orbs[i]?.PulseMana();
            }

            orbitDisturbanceStartedAt = Time.time;
            SpawnReaction(absorbReactionPrefab, reactingOrb, incomingDirection);
            SpawnReaction(manaRestorePrefab, reactingOrb, incomingDirection);
        }

        public void Expire()
        {
            if (!playing || expiring)
            {
                return;
            }

            expiring = true;
            expirationStartedAt = Time.time;
            Unsubscribe();
            if (expirationPrefab != null)
            {
                GameObject expiration = Instantiate(expirationPrefab, transform.position + Vector3.up * 1.05f, Quaternion.identity, transform);
                expiration.name = expirationPrefab.name;
            }
        }

        public void StopImmediate()
        {
            StopImmediateInternal(true);
        }

        public void ResetForPool()
        {
            bool previousDestroy = destroyOnComplete;
            destroyOnComplete = false;
            StopImmediateInternal(false);
            destroyOnComplete = previousDestroy;
            context = default;
            buffController = null;
            casterCombatant = null;
            caster = null;
        }

        public void ConfigureAuthoring(
            WaterShieldVFXProfile newProfile,
            GameObject newOrbPrefab,
            GameObject newActivationPrefab,
            GameObject newAbsorbReactionPrefab,
            GameObject newManaRestorePrefab,
            GameObject newExpirationPrefab,
            bool newDestroyOnComplete,
            Transform newFormationRoot)
        {
            profile = newProfile;
            orbPrefab = newOrbPrefab;
            activationPrefab = newActivationPrefab;
            absorbReactionPrefab = newAbsorbReactionPrefab;
            manaRestorePrefab = newManaRestorePrefab;
            expirationPrefab = newExpirationPrefab;
            destroyOnComplete = newDestroyOnComplete;
            formationRoot = newFormationRoot;
        }

        private void UpdateOrbit(float elapsed, float radiusScale)
        {
            float activationSweep = elapsed < profile.ActivationDuration
                ? Smooth01(Mathf.Clamp01(elapsed / profile.ActivationDuration)) * profile.ActivationSweepDegrees
                : profile.ActivationSweepDegrees;
            float persistentElapsed = Mathf.Max(0f, elapsed - profile.ActivationDuration);
            float disturbance = EvaluateDisturbance();
            float orbitAngle = activationSweep + persistentElapsed * profile.OrbitSpeed * (1f + disturbance * 0.55f);
            Quaternion tilt = Quaternion.Euler(profile.OrbitTilt, 0f, profile.OrbitTilt * 0.35f);

            for (int i = 0; i < OrbCount; i++)
            {
                WaterShieldOrbVFX orb = orbs[i];
                if (orb == null) continue;
                float formationBegin = profile.FirstOrbDelay + i * profile.OrbFormationInterval;
                float formation = Mathf.Clamp01((elapsed - formationBegin) / profile.GatherDuration);
                orb.SetFormationProgress(formation);
                float angle = (profile.FormationRotationOffset + i * 120f + orbitAngle) * Mathf.Deg2Rad;
                float radius = profile.OrbitRadius * radiusScale * (1f + disturbance * profile.OrbitDisturbanceAmount);
                Vector3 local = new(Mathf.Sin(angle) * radius, profile.OrbitHeight, Mathf.Cos(angle) * radius);
                local = tilt * local;
                local.y += Mathf.Sin(elapsed * profile.VerticalBobSpeed + i * 2.094f) * profile.VerticalBobAmount * radiusScale;
                orb.transform.localPosition = local;
            }
        }

        private int SelectImpactOrb(Vector3 incomingDirection)
        {
            if (incomingDirection.sqrMagnitude < 0.001f)
            {
                int selected = fallbackReactionIndex % OrbCount;
                fallbackReactionIndex++;
                return selected;
            }

            Vector3 worldDirection = incomingDirection.normalized;
            int bestIndex = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < OrbCount; i++)
            {
                if (orbs[i] == null) continue;
                Vector3 fromCaster = (orbs[i].transform.position - (caster.position + Vector3.up * profile.OrbitHeight)).normalized;
                float dot = Vector3.Dot(fromCaster, -worldDirection);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void SpawnReaction(GameObject prefab, WaterShieldOrbVFX sourceOrb, Vector3 incomingDirection)
        {
            if (prefab == null || caster == null)
            {
                return;
            }

            WaterShieldReactionVFX reaction = null;
            foreach (WaterShieldReactionVFX candidate in reactionPool)
            {
                if (candidate != null && !candidate.IsPlaying && candidate.name == prefab.name)
                {
                    reaction = candidate;
                    break;
                }
            }

            if (reaction == null)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.name = prefab.name;
                reaction = instance.GetComponent<WaterShieldReactionVFX>();
                if (reaction == null)
                {
                    Destroy(instance);
                    return;
                }

                reactionPool.Add(reaction);
            }

            reaction.gameObject.SetActive(true);
            reaction.Play(profile, sourceOrb != null ? sourceOrb.transform : null, caster, incomingDirection);
        }

        private void PulseAllMana()
        {
            for (int i = 0; i < OrbCount; i++) orbs[i]?.PulseMana();
        }

        private void CheckBuffState()
        {
            TryResolveBuffController();
            bool hasBuff = HasBuff();
            if (hasBuff)
            {
                buffWasObserved = true;
                return;
            }

            if ((buffWasObserved && Time.time - startedAt > 0.15f) || Time.time >= fallbackExpiresAt)
            {
                Expire();
            }
        }

        private bool HasBuff()
        {
            string abilityId = context.Ability != null ? context.Ability.AbilityId : "shaman_water_shield";
            return buffController != null && buffController.FindBuff(abilityId) != null;
        }

        private void TryResolveBuffController()
        {
            if (buffController != null || caster == null) return;
            buffController = caster.GetComponent<MMOCharacterBuffController>();
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.BuffsChanged += OnBuffsChanged;
                buffController.DamageAbsorbedAsMana -= OnDamageAbsorbedAsMana;
                buffController.DamageAbsorbedAsMana += OnDamageAbsorbedAsMana;
            }
        }

        private void OnBuffsChanged(MMOCharacterBuffController source)
        {
            if (source == buffController) CheckBuffState();
        }

        private void OnDamageAbsorbedAsMana(MMOCharacterBuffController source, int amount)
        {
            if (source == buffController && playing && !expiring)
            {
                pendingAbsorbedMana += Mathf.Max(0, amount);
            }
        }

        private void OnCasterDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int appliedAmount)
        {
            if (!playing || expiring || target != casterCombatant || pendingAbsorbedMana <= 0)
            {
                return;
            }

            Vector3 incoming = source != null && caster != null ? caster.position - source.transform.position : Vector3.zero;
            int restored = pendingAbsorbedMana;
            pendingAbsorbedMana = 0;
            ReactToAbsorb(incoming, restored);
        }

        private void Subscribe()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.BuffsChanged += OnBuffsChanged;
                buffController.DamageAbsorbedAsMana -= OnDamageAbsorbedAsMana;
                buffController.DamageAbsorbedAsMana += OnDamageAbsorbedAsMana;
            }

            if (casterCombatant != null)
            {
                casterCombatant.Damaged -= OnCasterDamaged;
                casterCombatant.Damaged += OnCasterDamaged;
            }
        }

        private void Unsubscribe()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.DamageAbsorbedAsMana -= OnDamageAbsorbedAsMana;
            }

            if (casterCombatant != null)
            {
                casterCombatant.Damaged -= OnCasterDamaged;
            }
        }

        private float EvaluateDisturbance()
        {
            float t = (Time.time - orbitDisturbanceStartedAt) / 0.48f;
            return t is >= 0f and < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
        }

        private void Complete()
        {
            if (!playing) return;
            playing = false;
            expiring = false;
            Unsubscribe();
            Completed?.Invoke(this);
            if (destroyOnComplete && Application.isPlaying)
            {
                Destroy(gameObject, 0.15f);
            }
        }

        private void StopImmediateInternal(bool allowDestroy)
        {
            playing = false;
            expiring = false;
            Unsubscribe();
            ResetRuntimeObjects();
            if (allowDestroy && destroyOnComplete && Application.isPlaying && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void ResetRuntimeObjects()
        {
            for (int i = 0; i < OrbCount; i++)
            {
                if (orbs[i] == null) continue;
                orbs[i].StopImmediate();
                if (Application.isPlaying) Destroy(orbs[i].gameObject); else DestroyImmediate(orbs[i].gameObject);
                orbs[i] = null;
            }

            for (int i = reactionPool.Count - 1; i >= 0; i--)
            {
                WaterShieldReactionVFX reaction = reactionPool[i];
                if (reaction == null) continue;
                reaction.StopImmediate();
                if (Application.isPlaying) Destroy(reaction.gameObject); else DestroyImmediate(reaction.gameObject);
            }

            reactionPool.Clear();
        }

        private static float ResolveBuffDuration(MMOAbilityDefinition ability)
        {
            float duration = 600f;
            if (ability == null) return duration;
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect != null && effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier && effect.DamageTakenAsManaPercent > 0f)
                {
                    duration = Mathf.Max(0.1f, effect.DurationSeconds);
                }
            }

            return duration;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
