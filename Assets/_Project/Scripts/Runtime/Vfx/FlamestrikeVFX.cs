using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FlamestrikeVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        private const string AbilityId = "mage_flamestrike";
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");

        [SerializeField] private FlamestrikeVFXProfile profile;
        [SerializeField] private Transform impactRoot;
        [SerializeField] private Transform groundRoot;
        [SerializeField] private Transform mainColumn;
        [SerializeField] private Transform[] secondaryPillars = Array.Empty<Transform>();
        [SerializeField] private Transform shockwave;
        [SerializeField] private Transform pulseRing;
        [SerializeField] private Renderer centralFlash;
        [SerializeField] private Renderer[] impactRenderers = Array.Empty<Renderer>();
        [SerializeField] private FlamestrikeTubeShellVFX[] impactTubeShells = Array.Empty<FlamestrikeTubeShellVFX>();
        [SerializeField] private Renderer scorchRenderer;
        [SerializeField] private Renderer[] crackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform[] flamePatches = Array.Empty<Transform>();
        [SerializeField] private Renderer[] flameRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] perimeterRenderers = Array.Empty<Renderer>();
        [SerializeField] private FlamestrikeTubeShellVFX[] persistentTubeShells = Array.Empty<FlamestrikeTubeShellVFX>();
        [SerializeField] private Renderer[] magicalRenderers = Array.Empty<Renderer>();
        [SerializeField] private FlamestrikeExpandingRingVFX[] expandingRings = Array.Empty<FlamestrikeExpandingRingVFX>();
        [SerializeField] private Renderer heatDistortionRenderer;
        [SerializeField] private ParticleSystem impactEmbers;
        [SerializeField] private ParticleSystem impactDebris;
        [SerializeField] private ParticleSystem smokeCrown;
        [SerializeField] private ParticleSystem persistentEmbers;
        [SerializeField] private ParticleSystem persistentSmoke;
        [SerializeField] private ParticleSystem ash;
        [SerializeField] private ParticleSystem pulseEmbers;
        [SerializeField] private ParticleSystem localEruptions;
        [SerializeField] private ParticleSystem finalSmoke;
        [SerializeField] private FlamestrikeTargetReactionVFX[] targetReactionPool = Array.Empty<FlamestrikeTargetReactionVFX>();

        private MaterialPropertyBlock properties;
        private readonly HashSet<int> reactedThisWindow = new();
        private MMOAbilityVfxContext context;
        private MMOCombatant sourceCombatant;
        private Vector3 impactPosition;
        private Vector3[] flameBaseScales = Array.Empty<Vector3>();
        private float[] flamePhases = Array.Empty<float>();
        private float startedAt;
        private float pulseStartedAt = float.NegativeInfinity;
        private float lastDamageEventAt = float.NegativeInfinity;
        private int pulseIndex;
        private bool initialized;
        private bool impactBurstsPlayed;
        private bool expirationBurstsPlayed;
        private float visibility = 1f;

        public FlamestrikeVFXProfile Profile => profile;
        public bool IsPlaying => initialized;

        public void ConfigureAuthoring(
            FlamestrikeVFXProfile newProfile, Transform newImpactRoot, Transform newGroundRoot,
            Transform newMainColumn, Transform[] newSecondaryPillars, Transform newShockwave, Transform newPulseRing,
            Renderer newCentralFlash, Renderer[] newImpactRenderers, FlamestrikeTubeShellVFX[] newImpactTubeShells, Renderer newScorchRenderer,
            Renderer[] newCrackRenderers, Transform[] newFlamePatches, Renderer[] newFlameRenderers,
            Renderer[] newPerimeterRenderers, FlamestrikeTubeShellVFX[] newPersistentTubeShells, Renderer[] newMagicalRenderers, FlamestrikeExpandingRingVFX[] newExpandingRings, Renderer newHeatDistortionRenderer,
            ParticleSystem newImpactEmbers, ParticleSystem newImpactDebris, ParticleSystem newSmokeCrown,
            ParticleSystem newPersistentEmbers, ParticleSystem newPersistentSmoke, ParticleSystem newAsh,
            ParticleSystem newPulseEmbers, ParticleSystem newLocalEruptions, ParticleSystem newFinalSmoke,
            FlamestrikeTargetReactionVFX[] newTargetReactionPool)
        {
            profile = newProfile; impactRoot = newImpactRoot; groundRoot = newGroundRoot; mainColumn = newMainColumn;
            secondaryPillars = newSecondaryPillars ?? Array.Empty<Transform>(); shockwave = newShockwave; pulseRing = newPulseRing;
            centralFlash = newCentralFlash; impactRenderers = newImpactRenderers ?? Array.Empty<Renderer>(); impactTubeShells = newImpactTubeShells ?? Array.Empty<FlamestrikeTubeShellVFX>(); scorchRenderer = newScorchRenderer;
            crackRenderers = newCrackRenderers ?? Array.Empty<Renderer>(); flamePatches = newFlamePatches ?? Array.Empty<Transform>(); flameRenderers = newFlameRenderers ?? Array.Empty<Renderer>();
            perimeterRenderers = newPerimeterRenderers ?? Array.Empty<Renderer>(); persistentTubeShells = newPersistentTubeShells ?? Array.Empty<FlamestrikeTubeShellVFX>(); magicalRenderers = newMagicalRenderers ?? Array.Empty<Renderer>(); expandingRings = newExpandingRings ?? Array.Empty<FlamestrikeExpandingRingVFX>(); heatDistortionRenderer = newHeatDistortionRenderer;
            impactEmbers = newImpactEmbers; impactDebris = newImpactDebris; smokeCrown = newSmokeCrown; persistentEmbers = newPersistentEmbers;
            persistentSmoke = newPersistentSmoke; ash = newAsh; pulseEmbers = newPulseEmbers; localEruptions = newLocalEruptions; finalSmoke = newFinalSmoke;
            targetReactionPool = newTargetReactionPool ?? Array.Empty<FlamestrikeTargetReactionVFX>();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError("FlamestrikeVFX requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            sourceCombatant = context.Source != null ? context.Source.GetComponent<MMOCombatant>() : null;
            impactPosition = context.HasGroundTarget ? context.TargetPosition : transform.position;
            transform.SetParent(null, true);
            transform.position = impactPosition + Vector3.up * 0.035f;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * profile.OverallEffectScale;
            startedAt = Time.time;
            pulseStartedAt = float.NegativeInfinity;
            lastDamageEventAt = float.NegativeInfinity;
            pulseIndex = 0;
            impactBurstsPlayed = false;
            expirationBurstsPlayed = false;
            initialized = true;
            CacheFlameAnimation();
            ResetVisuals();
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        private void OnDestroy()
        {
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
        }

        private void Update()
        {
            if (!initialized || profile == null) return;
            float elapsed = Time.time - startedAt;
            UpdateLod();
            UpdateContinuousParticleRates();
            float impactT = Mathf.Clamp01(elapsed / 0.82f);
            float groundT = Mathf.Clamp01((elapsed - 0.28f) / 0.58f);
            float expirationStart = 0.65f + profile.BurnDuration;
            float expirationT = Mathf.Clamp01((elapsed - expirationStart) / Mathf.Max(0.1f, profile.ScorchedGroundFadeDuration));
            float pulseT = Mathf.Clamp01((Time.time - pulseStartedAt) / profile.PulseDuration);
            bool pulsing = pulseT < 1f;
            float pulse = pulsing ? Mathf.Sin(pulseT * Mathf.PI) : 0f;
            float finalMultiplier = pulseIndex >= 4 ? profile.FinalPulseMultiplier : 1f;

            AnimateImpact(impactT);
            AnimateGround(groundT, expirationT, pulse * finalMultiplier);
            AnimatePulse(pulseT, pulse * finalMultiplier);
            if (!impactBurstsPlayed)
            {
                impactBurstsPlayed = true;
                PlayBurst(impactEmbers, ScaleParticleCount(profile.ImpactEmberCount));
                PlayBurst(impactDebris, ScaleParticleCount(profile.ImpactDebrisCount));
                PlayBurst(smokeCrown, ScaleParticleCount(profile.SmokeCrownAmount));
                persistentEmbers?.Play(true);
                persistentSmoke?.Play(true);
                ash?.Play(true);
            }

            if (expirationT > 0f && !expirationBurstsPlayed)
            {
                expirationBurstsPlayed = true;
                persistentEmbers?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                persistentSmoke?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ash?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                smokeCrown?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                PlayBurst(finalSmoke, ScaleParticleCount(profile.FinalSmokeAmount));
            }

            if (elapsed >= profile.TotalLifetime)
            {
                initialized = false;
                MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
                Destroy(gameObject);
            }
        }

        private void AnimateImpact(float t)
        {
            float alpha = 1f - Mathf.SmoothStep(0.28f, 1f, t);
            float rise = 1f - Mathf.Pow(1f - t, 3f);
            if (mainColumn != null)
            {
                float height = profile.MainColumnHeight * Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI * 0.5f);
                mainColumn.localScale = new Vector3(profile.MainColumnWidth, height, profile.MainColumnWidth);
            }
            for (int i = 0; i < secondaryPillars.Length; i++)
            {
                Transform pillar = secondaryPillars[i];
                if (pillar == null) continue;
                float delayT = Mathf.Clamp01((t - i * 0.035f) / 0.78f);
                float height = profile.SecondaryPillarSize * Mathf.Lerp(0.55f, 1.3f, (i * 0.37f) % 1f);
                float width = height * 0.72f;
                pillar.localScale = new Vector3(width, height * 2.1f * Mathf.Sin(delayT * Mathf.PI), width);
            }
            if (centralFlash != null)
            {
                centralFlash.transform.localScale = Vector3.one * profile.CentralFlashSize * Mathf.Lerp(0.2f, 1.4f, rise);
                SetRenderer(centralFlash, profile.HotColor * profile.CentralFlashBrightness, alpha);
            }
            if (shockwave != null)
            {
                float size = Mathf.Min(profile.ShockwaveRadius * 2f, t * profile.ShockwaveSpeed);
                shockwave.localScale = new Vector3(size, 1f, size);
            }
            foreach (Renderer renderer in impactRenderers) SetRenderer(renderer, profile.FlameColor, alpha);
            foreach (FlamestrikeTubeShellVFX shell in impactTubeShells)
                shell?.Animate(t, alpha * profile.OverallBrightness, profile.FlameColor, 0f);
        }

        private void AnimateGround(float groundT, float expirationT, float pulse)
        {
            float flameFade = 1f - Mathf.Clamp01(expirationT * profile.ScorchedGroundFadeDuration / profile.FlameFadeDuration);
            float scorchFade = 1f - expirationT;
            SetRenderer(scorchRenderer, profile.OuterColor, groundT * profile.ScorchedGroundOpacity * scorchFade);
            foreach (Renderer crack in crackRenderers)
            {
                Color color = Color.Lerp(profile.OuterColor, profile.HotColor, Mathf.Clamp01(pulse * profile.CrackBrighteningStrength));
                SetRenderer(crack, color, groundT * (1f - expirationT) * Mathf.Lerp(0.68f, 1f, pulse));
            }

            for (int i = 0; i < flamePatches.Length; i++)
            {
                Transform patch = flamePatches[i];
                if (patch == null) continue;
                float flicker = 1f + Mathf.Sin(Time.time * (6.3f + (i % 5)) + flamePhases[i]) * 0.14f;
                float surge = 1f + pulse * (profile.FlameSurgeScale - 1f);
                patch.localScale = flameBaseScales[i] * flicker * surge * groundT * flameFade;
            }
            foreach (Renderer flame in flameRenderers) SetRenderer(flame, profile.FlameColor, groundT * flameFade * Mathf.Lerp(0.82f, 1f, pulse));
            foreach (Renderer perimeter in perimeterRenderers) SetRenderer(perimeter, profile.OuterColor, groundT * flameFade * Mathf.Lerp(0.56f, 1f, pulse));
            float burnProgress = Mathf.Clamp01((Time.time - startedAt - 0.58f) / Mathf.Max(0.1f, profile.BurnDuration));
            float lingeringFade = 1f - Mathf.SmoothStep(0f, 1f, burnProgress);
            float tubeOpacity = groundT * flameFade * lingeringFade * Mathf.Lerp(0.82f, 1f, pulse) * profile.OverallBrightness;
            foreach (FlamestrikeTubeShellVFX shell in persistentTubeShells)
                shell?.Animate(burnProgress, tubeOpacity, profile.FlameColor, pulse);
            float ringElapsed = Mathf.Max(0f, Time.time - startedAt - 0.58f);
            foreach (FlamestrikeExpandingRingVFX ring in expandingRings)
                ring?.Animate(ringElapsed, burnProgress, groundT * flameFade * profile.OverallBrightness, profile.FlameColor);
            foreach (Renderer magical in magicalRenderers) SetRenderer(magical, profile.FlameColor, groundT * flameFade * (0.26f + pulse * 0.45f));
            float distortionQuality = profile.DistortionQuality == 0 ? 0.35f : profile.DistortionQuality == 1 ? 0.7f : 1f;
            SetRenderer(heatDistortionRenderer, Color.white, groundT * flameFade * profile.HeatDistortionAmount * distortionQuality * Mathf.Lerp(0.5f, 1f, pulse), profile.DistortionStrength * Mathf.Lerp(1f, 1.7f, pulse));
        }

        private void AnimatePulse(float t, float strength)
        {
            if (pulseRing == null) return;
            bool visible = t < 1f;
            pulseRing.gameObject.SetActive(visible);
            if (!visible) return;
            float size = Mathf.Min(profile.AreaRadius * 2f, t * profile.CircularPulseSpeed);
            pulseRing.localScale = new Vector3(size, 1f, size);
            Renderer renderer = pulseRing.GetComponent<Renderer>();
            SetRenderer(renderer, profile.HotColor * profile.PulseBrightness, strength * 0.62f);
        }

        private void OnCombatEventResolved(CombatEventRecord record, MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability)
        {
            if (!initialized || record == null || record.eventType != CombatEventType.DamageResolved || target == null
                || (ability != null ? ability.AbilityId : record.abilityId) != AbilityId || !MatchesSource(source)
                || Vector3.SqrMagnitude(target.transform.position - impactPosition) > Mathf.Pow(profile.AreaRadius + 1.2f, 2f))
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            bool initial = elapsed < 0.95f;
            bool beginsNewPulse = !initial && Time.time - lastDamageEventAt >= 0.18f;
            if (beginsNewPulse)
            {
                lastDamageEventAt = Time.time;
                reactedThisWindow.Clear();
            }
            PlayTargetReaction(target.transform, initial);
            if (!beginsNewPulse) return;
            pulseIndex = Mathf.Min(4, pulseIndex + 1);
            pulseStartedAt = Time.time;
            PlayBurst(pulseEmbers, ScaleParticleCount(Mathf.RoundToInt(profile.PulseEmberAmount * (pulseIndex >= 4 ? profile.FinalPulseMultiplier : 1f))));
            PlayBurst(localEruptions, ScaleParticleCount(profile.LocalEruptionCount));
        }

        private void PlayTargetReaction(Transform target, bool initial)
        {
            int targetId = target.GetInstanceID();
            if (!initial && !reactedThisWindow.Add(targetId)) return;
            foreach (FlamestrikeTargetReactionVFX reaction in targetReactionPool)
            {
                if (reaction == null || reaction.IsPlaying) continue;
                reaction.Play(profile, target, initial);
                return;
            }
            if (targetReactionPool.Length > 0 && targetReactionPool[0] != null)
            {
                targetReactionPool[0].ResetForPool();
                targetReactionPool[0].Play(profile, target, initial);
            }
        }

        private bool MatchesSource(MMOCombatant source)
        {
            if (source == null) return false;
            return source == sourceCombatant || (context.Source != null && (source.transform == context.Source || source.transform.IsChildOf(context.Source) || context.Source.IsChildOf(source.transform)));
        }

        private void CacheFlameAnimation()
        {
            flameBaseScales = new Vector3[flamePatches.Length];
            flamePhases = new float[flamePatches.Length];
            for (int i = 0; i < flamePatches.Length; i++)
            {
                flameBaseScales[i] = flamePatches[i] != null ? flamePatches[i].localScale : Vector3.one;
                flamePhases[i] = i * 1.731f;
            }
        }

        private void ResetVisuals()
        {
            if (impactRoot != null) impactRoot.gameObject.SetActive(true);
            if (groundRoot != null) groundRoot.gameObject.SetActive(true);
            if (pulseRing != null) pulseRing.gameObject.SetActive(false);
            foreach (FlamestrikeTargetReactionVFX reaction in targetReactionPool) reaction?.ResetForPool();
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float distortion = 0f)
        {
            if (renderer == null) return;
            properties ??= new MaterialPropertyBlock();
            opacity *= visibility;
            renderer.enabled = opacity > 0.001f;
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, tint * profile.OverallBrightness);
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            properties.SetVector(ScrollId, new Vector4(0.015f * Time.time, -0.045f * Time.time, 0f, 0f));
            properties.SetFloat(DistortionId, distortion);
            renderer.SetPropertyBlock(properties);
        }

        private void UpdateLod()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                visibility = 1f;
                return;
            }

            float distance = Vector3.Distance(camera.transform.position, impactPosition);
            visibility = distance <= profile.DistantReductionStart
                ? 1f
                : 1f - Mathf.InverseLerp(profile.DistantReductionStart, profile.CullDistance, distance);
        }

        private void UpdateContinuousParticleRates()
        {
            float quality = profile.ParticleQualityLevel == 0 ? 0.45f : profile.ParticleQualityLevel == 1 ? 0.75f : 1f;
            SetRate(persistentEmbers, profile.EmberRate * quality * visibility);
            SetRate(persistentSmoke, profile.SmokeAmount * quality * visibility);
            SetRate(ash, profile.AshAmount * quality * visibility);
        }

        private int ScaleParticleCount(int count)
        {
            float quality = profile.ParticleQualityLevel == 0 ? 0.45f : profile.ParticleQualityLevel == 1 ? 0.75f : 1f;
            return Mathf.RoundToInt(count * quality * Mathf.Max(0.2f, visibility));
        }

        private static void SetRate(ParticleSystem system, float rate)
        {
            if (system == null) return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        private static void PlayBurst(ParticleSystem system, int count)
        {
            if (system == null || count <= 0) return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
            system.Clear(true);
            system.Play(true);
        }
    }
}
