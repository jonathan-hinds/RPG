using System;
using System.Collections.Generic;
using RPGClone.Buffs;
using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    /// <summary>
    /// Presentation-only reaction driven by replicated combat and buff state.
    /// It never decides whether a target was hit or how long movement is prevented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrostWaveEnemyImpactVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        public const string FrostWaveBuffId = "mage_frost_wave";

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        private static readonly Dictionary<EntityId, FrostWaveEnemyImpactVFX> ActiveByTarget = new();

        [SerializeField] private Transform impactRoot;
        [SerializeField] private Renderer impactFlash;
        [SerializeField] private Renderer impactGroundMark;
        [SerializeField] private Renderer[] impactShardMeshes = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem impactMist;
        [SerializeField] private ParticleSystem impactSnow;
        [SerializeField] private ParticleSystem impactShards;
        [SerializeField] private ParticleSystem impactStreaks;
        [SerializeField] private Transform rootIndicatorRoot;
        [SerializeField] private Renderer rootGroundMark;
        [SerializeField] private Renderer[] rootIceFormations = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem rootVapor;
        [SerializeField] private ParticleSystem rootSparkles;

        private MaterialPropertyBlock properties;
        private FrostWaveVFXProfile profile;
        private Transform target;
        private MMOCharacterBuffController buffController;
        private MMOActiveBuff activeBuff;
        private Action<FrostWaveEnemyImpactVFX> completed;
        private Vector3[] impactShardBasePositions = Array.Empty<Vector3>();
        private Vector3[] impactShardBaseScales = Array.Empty<Vector3>();
        private Vector3[] rootShardBaseScales = Array.Empty<Vector3>();
        private float startedAt;
        private bool impactFinished;
        private bool rootActive;
        private bool playing;
        private EntityId targetId;

        public bool IsPlaying => playing;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveByTarget.Clear();
        }

        public void ConfigureAuthoring(
            Transform newImpactRoot,
            Renderer newImpactFlash,
            Renderer newImpactGroundMark,
            Renderer[] newImpactShardMeshes,
            ParticleSystem newImpactMist,
            ParticleSystem newImpactSnow,
            ParticleSystem newImpactShards,
            ParticleSystem newImpactStreaks,
            Transform newRootIndicatorRoot,
            Renderer newRootGroundMark,
            Renderer[] newRootIceFormations,
            ParticleSystem newRootVapor,
            ParticleSystem newRootSparkles)
        {
            impactRoot = newImpactRoot;
            impactFlash = newImpactFlash;
            impactGroundMark = newImpactGroundMark;
            impactShardMeshes = newImpactShardMeshes ?? Array.Empty<Renderer>();
            impactMist = newImpactMist;
            impactSnow = newImpactSnow;
            impactShards = newImpactShards;
            impactStreaks = newImpactStreaks;
            rootIndicatorRoot = newRootIndicatorRoot;
            rootGroundMark = newRootGroundMark;
            rootIceFormations = newRootIceFormations ?? Array.Empty<Renderer>();
            rootVapor = newRootVapor;
            rootSparkles = newRootSparkles;
        }

        public void Play(FrostWaveVFXProfile newProfile, Transform newTarget, Action<FrostWaveEnemyImpactVFX> onCompleted)
        {
            ResetRuntimeState(false);
            profile = newProfile;
            target = newTarget;
            completed = onCompleted;
            playing = profile != null && target != null;
            gameObject.SetActive(playing);
            if (!playing)
            {
                return;
            }

            targetId = target.GetEntityId();
            if (ActiveByTarget.TryGetValue(targetId, out FrostWaveEnemyImpactVFX existing)
                && existing != null
                && existing != this)
            {
                existing.CancelImmediate();
            }
            ActiveByTarget[targetId] = this;

            startedAt = Time.time;
            impactFinished = false;
            rootActive = false;
            buffController = target.GetComponent<MMOCharacterBuffController>();
            transform.SetPositionAndRotation(target.position + Vector3.up * profile.GroundOffset, Quaternion.identity);
            transform.localScale = Vector3.one * profile.EnemyImpactScale;
            CacheBaseTransforms();
            impactRoot.gameObject.SetActive(true);
            rootIndicatorRoot.gameObject.SetActive(false);
            PlayBurst(impactMist, profile.ImpactMistAmount);
            PlayBurst(impactSnow, profile.ImpactSnowAmount);
            PlayBurst(impactShards, profile.ImpactShardAmount);
            PlayBurst(impactStreaks, Mathf.Max(3, profile.ImpactShardAmount / 2));
            AnimateImpact(0f);
        }

        public void ResetForPool()
        {
            ResetRuntimeState(true);
            gameObject.SetActive(false);
        }

        public void CancelImmediate()
        {
            if (!playing)
            {
                return;
            }

            Action<FrostWaveEnemyImpactVFX> callback = completed;
            ResetRuntimeState(true);
            callback?.Invoke(this);
        }

        private void LateUpdate()
        {
            if (!playing || profile == null || target == null)
            {
                if (playing)
                {
                    Complete();
                }
                return;
            }

            transform.SetPositionAndRotation(target.position + Vector3.up * profile.GroundOffset, Quaternion.identity);
            float elapsed = Time.time - startedAt;
            if (!impactFinished)
            {
                AnimateImpact(elapsed);
                if (elapsed >= profile.ImpactDuration)
                {
                    impactFinished = true;
                    impactRoot.gameObject.SetActive(false);
                }
            }

            if (!rootActive)
            {
                if (TryActivateFromReplicatedBuff())
                {
                    AnimateRoot(0f);
                }
                else if (impactFinished && elapsed >= profile.ImpactDuration + 0.8f)
                {
                    Complete();
                }
                return;
            }

            activeBuff = buffController != null ? buffController.FindBuff(FrostWaveBuffId) : null;
            if (activeBuff == null || activeBuff.IsExpired)
            {
                // A dispel or authoritative expiry removes the persistent indicator immediately.
                Complete();
                return;
            }

            AnimateRoot(1f - activeBuff.NormalizedRemaining);
        }

        private bool TryActivateFromReplicatedBuff()
        {
            if (buffController == null && target != null)
            {
                buffController = target.GetComponent<MMOCharacterBuffController>();
            }

            activeBuff = buffController != null ? buffController.FindBuff(FrostWaveBuffId) : null;
            if (activeBuff == null || activeBuff.IsExpired)
            {
                return false;
            }

            rootActive = true;
            rootIndicatorRoot.gameObject.SetActive(true);
            rootVapor?.Play(true);
            rootSparkles?.Play(true);
            return true;
        }

        private void AnimateImpact(float elapsed)
        {
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, profile.ImpactDuration));
            float flash = Mathf.Exp(-elapsed * 11f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, normalized));
            if (impactFlash != null)
            {
                impactFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.1f, Mathf.Clamp01(elapsed / 0.15f));
                SetRenderer(impactFlash, profile.WhiteHot, flash, 3.2f, normalized);
            }
            SetRenderer(impactGroundMark, profile.PaleCyan, fade * 0.42f, 1.45f, normalized * 0.8f);

            for (int i = 0; i < impactShardMeshes.Length; i++)
            {
                Renderer shard = impactShardMeshes[i];
                if (shard == null) continue;
                float formation = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (0.11f + i * 0.008f)));
                float lift = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI) * (0.24f + (i % 3) * 0.06f);
                shard.transform.localPosition = impactShardBasePositions[i] + Vector3.up * lift;
                shard.transform.localScale = impactShardBaseScales[i] * formation * (1f - normalized * 0.45f);
                shard.transform.Rotate(0f, (i % 2 == 0 ? 1f : -1f) * 110f * Time.deltaTime, 35f * Time.deltaTime, Space.Self);
                SetRenderer(shard, i % 3 == 0 ? profile.WhiteHot : i % 3 == 1 ? profile.PaleCyan : profile.SaturatedBlue, fade, 1.55f, normalized);
            }
        }

        private void AnimateRoot(float normalizedElapsed)
        {
            float formation = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedElapsed / 0.08f));
            float ending = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, normalizedElapsed));
            float alpha = formation * (1f - ending * 0.62f);
            SetRenderer(rootGroundMark, profile.SaturatedBlue, alpha * 0.34f, 0.9f, ending * 0.8f);
            for (int i = 0; i < rootIceFormations.Length; i++)
            {
                Renderer shard = rootIceFormations[i];
                if (shard == null) continue;
                float pulse = 0.92f + Mathf.Sin(Time.time * 2.1f + i * 1.37f) * 0.08f;
                shard.transform.localScale = rootShardBaseScales[i] * formation * pulse;
                SetRenderer(shard, i % 3 == 0 ? profile.DeepBlue : i % 3 == 1 ? profile.SaturatedBlue : profile.PaleCyan, alpha * 0.82f, 1.15f, ending);
            }
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float dissolve)
        {
            if (renderer == null)
            {
                return;
            }

            properties ??= new MaterialPropertyBlock();
            renderer.enabled = opacity > 0.001f;
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, tint);
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            properties.SetFloat(BrightnessId, brightness * profile.OverallIntensity);
            properties.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            properties.SetVector(ScrollId, new Vector4(0.018f, -0.026f, 0.04f, -0.035f));
            renderer.SetPropertyBlock(properties);
        }

        private void CacheBaseTransforms()
        {
            impactShardBasePositions = new Vector3[impactShardMeshes.Length];
            impactShardBaseScales = new Vector3[impactShardMeshes.Length];
            for (int i = 0; i < impactShardMeshes.Length; i++)
            {
                if (impactShardMeshes[i] == null) continue;
                impactShardBasePositions[i] = impactShardMeshes[i].transform.localPosition;
                impactShardBaseScales[i] = impactShardMeshes[i].transform.localScale;
            }

            rootShardBaseScales = new Vector3[rootIceFormations.Length];
            for (int i = 0; i < rootIceFormations.Length; i++)
            {
                if (rootIceFormations[i] != null)
                {
                    rootShardBaseScales[i] = rootIceFormations[i].transform.localScale;
                }
            }
        }

        private void Complete()
        {
            Action<FrostWaveEnemyImpactVFX> callback = completed;
            ResetRuntimeState(true);
            callback?.Invoke(this);
        }

        private void ResetRuntimeState(bool stopParticles)
        {
            if (targetId != default && ActiveByTarget.TryGetValue(targetId, out FrostWaveEnemyImpactVFX active) && active == this)
            {
                ActiveByTarget.Remove(targetId);
            }

            if (stopParticles)
            {
                foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            playing = false;
            rootActive = false;
            impactFinished = false;
            target = null;
            buffController = null;
            activeBuff = null;
            completed = null;
            targetId = default;
            if (impactRoot != null) impactRoot.gameObject.SetActive(false);
            if (rootIndicatorRoot != null) rootIndicatorRoot.gameObject.SetActive(false);
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
    }
}
