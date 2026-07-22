using System.Collections;
using RPGClone.Abilities;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    /// <summary>
    /// Presentation-only Arcane Missiles orchestration. Gameplay owns channel state and damage ticks;
    /// this component predicts launches for readability, then confirms impacts from replicated damage events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneMissilesVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler, IMMOAbilityVfxPoolReset
    {
        private const int FabricatorCount = 3;

        [Header("Configuration")]
        [SerializeField] private ArcaneMissilesVFXProfile profile;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private GameObject interruptPrefab;

        [Header("Caster layers")]
        [SerializeField] private Transform leftHandGlow;
        [SerializeField] private Renderer leftHandGlowRenderer;
        [SerializeField] private Transform rightHandGlow;
        [SerializeField] private Renderer rightHandGlowRenderer;
        [SerializeField] private Transform centralCore;
        [SerializeField] private Renderer centralCoreRenderer;
        [SerializeField] private Transform channelCircle;
        [SerializeField] private Renderer channelCircleRenderer;
        [SerializeField] private ParticleSystem channelSparks;
        [SerializeField] private ParticleSystem runeFragments;
        [SerializeField] private LineRenderer handRibbon;
        [SerializeField] private LineRenderer[] energyConnections;
        [SerializeField] private ArcaneMissilesFabricatorVFX[] fabricators;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();
        private readonly ArcaneMissileProjectileVFX[] missiles = new ArcaneMissileProjectileVFX[5];
        private readonly bool[] launched = new bool[5];
        private readonly bool[] impacted = new bool[5];
        private readonly float[] lastOrbLaunchTimes = { -10f, -10f, -10f };

        private MMOAbilityVfxContext context;
        private MMOCombatant sourceCombatant;
        private MMOCombatant targetCombatant;
        private MMOAbilityVfxAnchors sourceAnchors;
        private Transform source;
        private Transform target;
        private float startedAt;
        private float duration;
        private float tickInterval;
        private int missileCount;
        private int confirmedImpactCount;
        private bool playing;
        private bool releasing;
        private bool interrupted;
        private Coroutine releaseRoutine;

        public ArcaneMissilesVFXProfile Profile => profile;
        public bool IsPlaying => playing;
        public int LaunchedMissileCount { get; private set; }
        public int ConfirmedImpactCount => confirmedImpactCount;

        private void Update()
        {
            if (!playing || profile == null || source == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            if (!releasing && context.SourceSystem != null && context.SourceSystem.CurrentCastAbility == context.Ability)
            {
                normalized = context.SourceSystem.CurrentCastNormalized;
                elapsed = normalized * duration;
            }

            UpdateCasterAttachment(elapsed, normalized);
            UpdateFabricators(elapsed);
            if (!releasing)
            {
                LaunchDueMissiles(elapsed);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            ResetForPool();
            context = newContext;
            source = newContext.Source;
            target = newContext.Target;
            if (profile == null || source == null || projectilePrefab == null || impactPrefab == null)
            {
                Debug.LogError("ArcaneMissilesVFX is missing its profile, caster, projectile prefab, or impact prefab.", this);
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            sourceCombatant = newContext.SourceSystem != null ? newContext.SourceSystem.Combatant : source.GetComponent<MMOCombatant>();
            targetCombatant = target != null ? target.GetComponent<MMOCombatant>() : null;
            sourceAnchors = source.GetComponent<MMOAbilityVfxAnchors>();
            duration = ResolveDuration(newContext.Ability, newContext.SourceSystem, profile.ChannelDuration);
            tickInterval = ResolveTickInterval(newContext.Ability, duration);
            missileCount = Mathf.Clamp(Mathf.RoundToInt(duration / tickInterval), 1, missiles.Length);
            startedAt = Time.time;
            playing = true;
            releasing = false;
            interrupted = false;
            LaunchedMissileCount = 0;
            confirmedImpactCount = 0;
            transform.SetParent(source, true);
            transform.SetPositionAndRotation(source.position, source.rotation);
            transform.localScale = Vector3.one;

            for (int i = 0; i < FabricatorCount; i++)
            {
                if (fabricators == null || i >= fabricators.Length || fabricators[i] == null) continue;
                fabricators[i].transform.localPosition = profile.GetOrbOffset(i);
                fabricators[i].Play(profile, i);
            }

            ConfigureCasterParticles();
            channelSparks?.Play(true);
            runeFragments?.Play(true);
            Subscribe();
        }

        public void Release(bool immediate)
        {
            if (!playing || releasing)
            {
                return;
            }

            releasing = true;
            interrupted = immediate;
            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(immediate ? InterruptRoutine() : CompleteRoutine());
        }

        public void ResetForPool()
        {
            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
                releaseRoutine = null;
            }

            Unsubscribe();
            for (int i = 0; i < missiles.Length; i++)
            {
                if (missiles[i] != null && missiles[i].IsPlaying)
                {
                    missiles[i].Dissolve(profile != null ? profile.UnfinishedMissileDissolve : 0.1f);
                }

                missiles[i] = null;
                launched[i] = false;
                impacted[i] = false;
            }

            for (int i = 0; i < lastOrbLaunchTimes.Length; i++) lastOrbLaunchTimes[i] = -10f;
            if (fabricators != null)
            {
                foreach (ArcaneMissilesFabricatorVFX fabricator in fabricators) fabricator?.StopImmediate();
            }

            channelSparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            runeFragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetLineVisible(handRibbon, false);
            if (energyConnections != null)
            {
                foreach (LineRenderer connection in energyConnections) SetLineVisible(connection, false);
            }

            playing = false;
            releasing = false;
            interrupted = false;
            LaunchedMissileCount = 0;
            confirmedImpactCount = 0;
            context = default;
            source = null;
            target = null;
            sourceCombatant = null;
            targetCombatant = null;
            sourceAnchors = null;
        }

        public void ConfigureAuthoring(
            ArcaneMissilesVFXProfile newProfile,
            GameObject newProjectilePrefab,
            GameObject newImpactPrefab,
            GameObject newInterruptPrefab,
            Transform newLeftHandGlow,
            Renderer newLeftHandGlowRenderer,
            Transform newRightHandGlow,
            Renderer newRightHandGlowRenderer,
            Transform newCentralCore,
            Renderer newCentralCoreRenderer,
            Transform newChannelCircle,
            Renderer newChannelCircleRenderer,
            ParticleSystem newChannelSparks,
            ParticleSystem newRuneFragments,
            LineRenderer newHandRibbon,
            LineRenderer[] newEnergyConnections,
            ArcaneMissilesFabricatorVFX[] newFabricators)
        {
            profile = newProfile;
            projectilePrefab = newProjectilePrefab;
            impactPrefab = newImpactPrefab;
            interruptPrefab = newInterruptPrefab;
            leftHandGlow = newLeftHandGlow;
            leftHandGlowRenderer = newLeftHandGlowRenderer;
            rightHandGlow = newRightHandGlow;
            rightHandGlowRenderer = newRightHandGlowRenderer;
            centralCore = newCentralCore;
            centralCoreRenderer = newCentralCoreRenderer;
            channelCircle = newChannelCircle;
            channelCircleRenderer = newChannelCircleRenderer;
            channelSparks = newChannelSparks;
            runeFragments = newRuneFragments;
            handRibbon = newHandRibbon;
            energyConnections = newEnergyConnections;
            fabricators = newFabricators;
        }

        private void UpdateCasterAttachment(float elapsed, float normalized)
        {
            transform.SetPositionAndRotation(source.position, source.rotation);
            Transform leftHand = sourceAnchors != null ? sourceAnchors.LeftHandAnchor : null;
            Transform rightHand = sourceAnchors != null ? sourceAnchors.RightHandAnchor : null;
            Vector3 leftPosition = leftHand != null ? leftHand.position : source.TransformPoint(new Vector3(-0.28f, 1.18f, 0.34f));
            Vector3 rightPosition = rightHand != null ? rightHand.position : source.TransformPoint(new Vector3(0.28f, 1.18f, 0.34f));
            Vector3 corePosition = Vector3.Lerp(leftPosition, rightPosition, 0.5f) + source.forward * 0.12f;
            leftHandGlow.position = leftPosition;
            rightHandGlow.position = rightPosition;
            centralCore.position = corePosition;
            centralCore.rotation = Quaternion.LookRotation(source.forward, source.up);
            centralCore.localScale = Vector3.one * profile.CentralCoreSize * profile.OverallScale * (0.94f + Mathf.Sin(elapsed * 7f) * 0.06f);
            channelCircle.position = source.position + Vector3.up * 0.035f;
            channelCircle.rotation = source.rotation * Quaternion.Euler(90f, 0f, elapsed * 12f);
            channelCircle.localScale = Vector3.one * profile.RuneCircleScale * profile.OverallScale;

            float endingFade = releasing && !interrupted ? 1f - normalized : 1f;
            ArcaneMissilesVFXUtility.SetRenderer(leftHandGlowRenderer, PropertyBlock, endingFade, profile.HandBrightness * profile.OverallBrightness, 0f, profile.BlueColor);
            ArcaneMissilesVFXUtility.SetRenderer(rightHandGlowRenderer, PropertyBlock, endingFade, profile.HandBrightness * profile.OverallBrightness, 0f, profile.PurpleColor);
            ArcaneMissilesVFXUtility.SetRenderer(centralCoreRenderer, PropertyBlock, endingFade, 4.5f * profile.OverallBrightness, 0f, Color.white);
            ArcaneMissilesVFXUtility.SetRenderer(channelCircleRenderer, PropertyBlock, 0.72f * endingFade, 1.3f * profile.OverallBrightness, normalized * 0.15f, profile.PurpleColor);

            ConfigureLine(handRibbon, leftPosition, rightPosition, profile.EnergyConnectionWidth * 0.7f, profile.PurpleColor, 1f);
        }

        private void UpdateFabricators(float elapsed)
        {
            int nextMissile = FindNextUnlaunchedMissile();
            int highlightedOrb = nextMissile >= 0 ? profile.GetFiringOrb(nextMissile) : -1;
            Vector3 targetPosition = ResolveTargetPosition();
            for (int i = 0; i < FabricatorCount; i++)
            {
                if (fabricators == null || i >= fabricators.Length || fabricators[i] == null) continue;
                float begin = i * profile.FormationDelay;
                float formation = (elapsed - begin) / profile.FormationDuration;
                fabricators[i].SetFormationProgress(formation);
                float rebuild = (elapsed - lastOrbLaunchTimes[i]) / profile.RebuildDuration;
                fabricators[i].SetFabricationProgress(rebuild);
                fabricators[i].SetHighlighted(i == highlightedOrb);
                if (i == highlightedOrb) fabricators[i].FaceTarget(targetPosition);

                float highlight = i == highlightedOrb ? 1f : 0f;
                float width = profile.EnergyConnectionWidth * (1f + highlight * 1.15f);
                Color color = Color.Lerp(profile.BlueColor, Color.white, highlight * 0.65f);
                if (energyConnections != null && i < energyConnections.Length)
                {
                    ConfigureLine(energyConnections[i], centralCore.position, fabricators[i].LaunchPosition, width, color, Mathf.Clamp01(formation));
                }
            }
        }

        private void LaunchDueMissiles(float elapsed)
        {
            float distance = Vector3.Distance(centralCore.position, ResolveTargetPosition());
            float lead = Mathf.Clamp(distance / Mathf.Max(0.1f, profile.ProjectileSpeed * 1.4f), profile.MinimumLaunchLeadSeconds, profile.MaximumLaunchLeadSeconds);
            for (int i = 0; i < missileCount; i++)
            {
                if (launched[i]) continue;
                float intendedImpact = Mathf.Min(duration, (i + 1) * tickInterval);
                if (elapsed >= intendedImpact - lead)
                {
                    LaunchMissile(i);
                }
            }
        }

        private void LaunchMissile(int missileIndex)
        {
            if (missileIndex < 0 || missileIndex >= missileCount || launched[missileIndex])
            {
                return;
            }

            int orbIndex = profile.GetFiringOrb(missileIndex);
            ArcaneMissilesFabricatorVFX fabricator = fabricators != null && orbIndex < fabricators.Length ? fabricators[orbIndex] : null;
            Vector3 sourcePosition = fabricator != null ? fabricator.LaunchPosition : centralCore.position;
            bool finalMissile = missileIndex == missileCount - 1;
            if (finalMissile)
            {
                foreach (ArcaneMissilesFabricatorVFX orb in fabricators) orb?.SetHighlighted(true);
            }

            fabricator?.PulseLaunch(finalMissile);
            lastOrbLaunchTimes[orbIndex] = Time.time - startedAt;
            GameObject instance = MMOAbilityVfxPool.Spawn(projectilePrefab, sourcePosition, Quaternion.identity, null);
            ArcaneMissileProjectileVFX projectile = instance != null ? instance.GetComponent<ArcaneMissileProjectileVFX>() : null;
            if (projectile == null)
            {
                if (instance != null) MMOAbilityVfxPool.Release(instance);
                return;
            }

            projectile.Play(profile, context.Definition, sourcePosition, target, ResolveTargetPosition(), missileIndex, finalMissile);
            missiles[missileIndex] = projectile;
            launched[missileIndex] = true;
            LaunchedMissileCount++;
        }

        private void OnTargetDamaged(MMOCombatant damageSource, MMOCombatant damageTarget, MMOAbilityDefinition ability, int amount)
        {
            if (!playing
                || interrupted
                || ability != context.Ability
                || damageTarget != targetCombatant
                || (sourceCombatant != null && damageSource != null && damageSource != sourceCombatant))
            {
                return;
            }

            int missileIndex = Mathf.Clamp(confirmedImpactCount, 0, missileCount - 1);
            if (impacted[missileIndex])
            {
                return;
            }

            if (!launched[missileIndex]) LaunchMissile(missileIndex);
            Vector3 impactPosition = ResolveTargetPosition();
            missiles[missileIndex]?.ConfirmImpact(impactPosition);
            impacted[missileIndex] = true;
            confirmedImpactCount++;
            SpawnImpact(impactPosition, missileIndex == missileCount - 1, missileIndex);
        }

        private void SpawnImpact(Vector3 position, bool finalImpact, int variation)
        {
            Transform parent = target != null ? target : null;
            GameObject instance = MMOAbilityVfxPool.Spawn(impactPrefab, position, Quaternion.identity, parent);
            ArcaneMissilesImpactVFX impact = instance != null ? instance.GetComponent<ArcaneMissilesImpactVFX>() : null;
            if (impact == null)
            {
                if (instance != null) MMOAbilityVfxPool.Release(instance);
                return;
            }

            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, variation * 29f, variation * 41f);
            impact.Play(profile, finalImpact, variation);
        }

        private IEnumerator CompleteRoutine()
        {
            // Cast completion and the final replicated damage record can cross a frame or
            // arrive on adjacent transport polls. Keep the presentation listener alive for
            // a bounded receiver-local grace period; gameplay state is never delayed.
            float finalImpactDeadline = Time.time + profile.MaximumLaunchLeadSeconds + 0.2f;
            while (confirmedImpactCount < missileCount && Time.time < finalImpactDeadline)
            {
                yield return null;
            }

            float durationSeconds = profile.CompletionCleanupDuration;
            float started = Time.time;
            while (Time.time - started < durationSeconds)
            {
                float progress = Mathf.Clamp01((Time.time - started) / durationSeconds);
                for (int i = 0; i < FabricatorCount; i++)
                {
                    float staggered = Mathf.Clamp01((progress * 1.35f - i * 0.14f));
                    fabricators[i]?.SetFade(1f - ArcaneMissilesVFXUtility.Smooth01(staggered));
                }

                FadeConnections(1f - progress);
                yield return null;
            }

            FinishAndPool();
        }

        private IEnumerator InterruptRoutine()
        {
            foreach (ArcaneMissileProjectileVFX missile in missiles)
            {
                if (missile != null && missile.IsPlaying) missile.Dissolve(profile.UnfinishedMissileDissolve);
            }

            if (interruptPrefab != null)
            {
                GameObject instance = MMOAbilityVfxPool.Spawn(interruptPrefab, centralCore.position, Quaternion.identity, null);
                ArcaneMissilesInterruptVFX interrupt = instance != null ? instance.GetComponent<ArcaneMissilesInterruptVFX>() : null;
                if (interrupt != null) interrupt.Play(profile, centralCore.position);
                else if (instance != null) MMOAbilityVfxPool.Release(instance);
            }

            float durationSeconds = profile.InterruptCollapseDuration;
            float started = Time.time;
            while (Time.time - started < durationSeconds)
            {
                float progress = Mathf.Clamp01((Time.time - started) / durationSeconds);
                foreach (ArcaneMissilesFabricatorVFX fabricator in fabricators) fabricator?.SetCollapse(progress);
                FadeConnections(1f - ArcaneMissilesVFXUtility.Smooth01(progress));
                yield return null;
            }

            FinishAndPool();
        }

        private void FadeConnections(float opacity)
        {
            if (handRibbon != null)
            {
                Color color = handRibbon.startColor;
                color.a = opacity;
                handRibbon.startColor = handRibbon.endColor = color;
            }

            if (energyConnections == null) return;
            foreach (LineRenderer connection in energyConnections)
            {
                if (connection == null) continue;
                Color color = connection.startColor;
                color.a = opacity;
                connection.startColor = connection.endColor = color;
            }
        }

        private void FinishAndPool()
        {
            releaseRoutine = null;
            playing = false;
            Unsubscribe();
            MMOAbilityVfxPool.Release(gameObject);
        }

        private void ConfigureCasterParticles()
        {
            ArcaneMissilesVFXUtility.ConfigureBurst(channelSparks, profile.ChannelParticleAmount, profile.BlueColor, 0.75f);
            ArcaneMissilesVFXUtility.ConfigureBurst(runeFragments, Mathf.Max(6, profile.ChannelParticleAmount / 2), profile.PurpleColor, 0.9f);
        }

        private void ConfigureLine(LineRenderer line, Vector3 start, Vector3 end, float width, Color color, float opacity)
        {
            if (line == null) return;
            line.enabled = opacity > 0.001f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = width * profile.OverallScale;
            color.a *= opacity;
            line.startColor = line.endColor = color;
        }

        private static void SetLineVisible(LineRenderer line, bool visible)
        {
            if (line != null) line.enabled = visible;
        }

        private Vector3 ResolveTargetPosition()
        {
            return ArcaneMissilesVFXUtility.SafeTargetPosition(target, context.TargetPosition, context.Definition);
        }

        private int FindNextUnlaunchedMissile()
        {
            for (int i = 0; i < missileCount; i++) if (!launched[i]) return i;
            return -1;
        }

        private void Subscribe()
        {
            if (targetCombatant == null) return;
            targetCombatant.Damaged -= OnTargetDamaged;
            targetCombatant.Damaged += OnTargetDamaged;
        }

        private void Unsubscribe()
        {
            if (targetCombatant != null) targetCombatant.Damaged -= OnTargetDamaged;
        }

        private static float ResolveDuration(MMOAbilityDefinition ability, MMOAbilitySystem sourceSystem, float fallback)
        {
            if (sourceSystem != null && sourceSystem.CurrentCastDuration > 0f) return sourceSystem.CurrentCastDuration;
            return ability != null && ability.CastTimeSeconds > 0f ? ability.CastTimeSeconds : fallback;
        }

        private static float ResolveTickInterval(MMOAbilityDefinition ability, float resolvedDuration)
        {
            if (ability != null)
            {
                foreach (MMOAbilityEffectDefinition effect in ability.Effects)
                {
                    if (effect != null && effect.EffectType == MMOAbilityEffectType.PeriodicDamage)
                    {
                        return Mathf.Max(0.1f, effect.TickSeconds);
                    }
                }
            }

            return Mathf.Max(0.1f, resolvedDuration / 5f);
        }
    }
}
