using System;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Physical
{
    [DisallowMultipleComponent]
    public sealed class GougeVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        [Header("Configuration")]
        [SerializeField] private GougeVFXProfile profile;

        [Header("Attachment")]
        [SerializeField] private Transform attachedRoot;
        [SerializeField] private Transform groundReactionRoot;

        [Header("Initial Impact")]
        [SerializeField] private ParticleSystem contactFlash;
        [SerializeField] private ParticleSystem criticalFlash;
        [SerializeField] private ParticleSystem impactLines;
        [SerializeField] private ParticleSystem directionalBloodSpray;
        [SerializeField] private ParticleSystem closeBloodBurst;
        [SerializeField] private ParticleSystem tornFragments;
        [SerializeField] private ParticleSystem impactGroundBurst;
        [SerializeField] private ParticleSystem environmentalHeavyDust;
        [SerializeField] private ParticleSystem environmentalFineDust;
        [SerializeField] private ParticleSystem impactDustRing;
        [SerializeField] private ParticleSystem groundDebris;

        [Header("Persistent Wound")]
        [SerializeField] private ParticleSystem[] woundBases = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem[] woundInners = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem[] wetHighlights = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem bloodSeepage;
        [SerializeField] private ParticleSystem bloodDrips;
        [SerializeField] private ParticleSystem woundMist;

        [Header("Bleed Tick")]
        [SerializeField] private ParticleSystem woundPulse;
        [SerializeField] private ParticleSystem bodyAccent;
        [SerializeField] private ParticleSystem tickBloodSpray;
        [SerializeField] private ParticleSystem tickHeavyDrips;

        [Header("Stack And Critical")]
        [SerializeField] private ParticleSystem[] stackTearingStreaks = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem criticalResetRing;
        [SerializeField] private ParticleSystem[] criticalTearingStreaks = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem criticalBloodBurst;
        [SerializeField] private ParticleSystem criticalSparks;

        [Header("Expiration")]
        [SerializeField] private ParticleSystem expirationFragments;
        [SerializeField] private ParticleSystem finalDroplets;

        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private MMOCombatant targetCombatant;
        private Transform target;
        private Transform source;
        private float lastReleaseAt;
        private float expiresAt;
        private float expirationStartedAt;
        private float nextDripAt;
        private int stackCount;
        private int tickCount;
        private int tickVariation;
        private bool persistent;
        private bool expiring;
        private Vector3 woundAnchorLocal;
        private Vector3 groundAnchorLocal;
        private bool attachmentResolved;
        private Camera cachedCamera;

        public int StackCount => stackCount;
        public bool IsPersistent => persistent && !expiring;

        private void Awake()
        {
            CacheParticles();
            StopAllParticles();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            StopAllParticles();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            target = context.Target;
            source = context.Source;
            if (profile == null || target == null)
            {
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            foreach (GougeVFX existing in target.GetComponentsInChildren<GougeVFX>(true))
            {
                if (existing != null && existing != this && existing.IsPersistent)
                {
                    existing.Reapply(context);
                    MMOAbilityVfxPool.Release(gameObject);
                    return;
                }
            }

            targetCombatant = target.GetComponent<MMOCombatant>();
            persistent = true;
            expiring = false;
            stackCount = 1;
            tickCount = 0;
            tickVariation = UnityEngine.Random.Range(0, 3);
            lastReleaseAt = Time.realtimeSinceStartup;
            expiresAt = Time.time + profile.BleedDuration;
            nextDripAt = Time.time + UnityEngine.Random.Range(0.35f, 0.85f);
            ResolveAttachment(context.TargetPosition);
            UpdateAttachment();
            SubscribeEvents();
            ApplyPersistentWound();
            PlayImpact(false);

            if (GougeVFXEventRelay.TryGetRecent(targetCombatant, 0.45f, out GougeVFXEventRelay.DamagePresentation recent)
                && recent.Record.isCritical)
            {
                PlayCritical();
            }
        }

        private void LateUpdate()
        {
            if (!persistent || profile == null || target == null)
            {
                return;
            }

            UpdateAttachment();
            if (!expiring && Time.time >= expiresAt)
            {
                BeginExpiration();
            }

            if (expiring)
            {
                if (Time.time - expirationStartedAt >= Mathf.Max(profile.WoundDissolveDuration, profile.MistFadeDuration))
                {
                    persistent = false;
                    MMOAbilityVfxPool.Release(gameObject);
                }

                return;
            }

            if (Time.time >= nextDripAt)
            {
                ConfigureAndPlay(
                    bloodDrips,
                    Mathf.Max(1, Mathf.RoundToInt(profile.StackDroplets(stackCount))),
                    0.8f,
                    0.12f,
                    profile.DripSize,
                    profile.Colors.Crimson);
                nextDripAt = Time.time + UnityEngine.Random.Range(0.65f, 1.35f)
                    / Mathf.Max(0.05f, profile.DripFrequency * profile.StackDroplets(stackCount));
            }
        }

        public void ConfigureAuthoring(
            GougeVFXProfile newProfile,
            Transform newAttachedRoot,
            Transform newGroundReactionRoot,
            ParticleSystem newContactFlash,
            ParticleSystem newCriticalFlash,
            ParticleSystem newImpactLines,
            ParticleSystem[] newWoundBases,
            ParticleSystem[] newWoundInners,
            ParticleSystem[] newWetHighlights,
            ParticleSystem newWoundPulse,
            ParticleSystem newBodyAccent,
            ParticleSystem[] newStackTearingStreaks,
            ParticleSystem newCriticalResetRing,
            ParticleSystem[] newCriticalTearingStreaks,
            ParticleSystem newExpirationFragments,
            ParticleSystem newDirectionalBloodSpray,
            ParticleSystem newCloseBloodBurst,
            ParticleSystem newTornFragments,
            ParticleSystem newImpactGroundBurst,
            ParticleSystem newEnvironmentalHeavyDust,
            ParticleSystem newEnvironmentalFineDust,
            ParticleSystem newImpactDustRing,
            ParticleSystem newGroundDebris,
            ParticleSystem newBloodSeepage,
            ParticleSystem newBloodDrips,
            ParticleSystem newWoundMist,
            ParticleSystem newTickBloodSpray,
            ParticleSystem newTickHeavyDrips,
            ParticleSystem newCriticalBloodBurst,
            ParticleSystem newCriticalSparks,
            ParticleSystem newFinalDroplets)
        {
            profile = newProfile;
            attachedRoot = newAttachedRoot;
            groundReactionRoot = newGroundReactionRoot;
            contactFlash = newContactFlash;
            criticalFlash = newCriticalFlash;
            impactLines = newImpactLines;
            woundBases = newWoundBases ?? Array.Empty<ParticleSystem>();
            woundInners = newWoundInners ?? Array.Empty<ParticleSystem>();
            wetHighlights = newWetHighlights ?? Array.Empty<ParticleSystem>();
            woundPulse = newWoundPulse;
            bodyAccent = newBodyAccent;
            stackTearingStreaks = newStackTearingStreaks ?? Array.Empty<ParticleSystem>();
            criticalResetRing = newCriticalResetRing;
            criticalTearingStreaks = newCriticalTearingStreaks ?? Array.Empty<ParticleSystem>();
            expirationFragments = newExpirationFragments;
            directionalBloodSpray = newDirectionalBloodSpray;
            closeBloodBurst = newCloseBloodBurst;
            tornFragments = newTornFragments;
            impactGroundBurst = newImpactGroundBurst;
            environmentalHeavyDust = newEnvironmentalHeavyDust;
            environmentalFineDust = newEnvironmentalFineDust;
            impactDustRing = newImpactDustRing;
            groundDebris = newGroundDebris;
            bloodSeepage = newBloodSeepage;
            bloodDrips = newBloodDrips;
            woundMist = newWoundMist;
            tickBloodSpray = newTickBloodSpray;
            tickHeavyDrips = newTickHeavyDrips;
            criticalBloodBurst = newCriticalBloodBurst;
            criticalSparks = newCriticalSparks;
            finalDroplets = newFinalDroplets;
            CacheParticles();
        }

        public void ResetForPool()
        {
            UnsubscribeEvents();
            StopAllParticles();
            persistent = false;
            expiring = false;
            target = null;
            source = null;
            targetCombatant = null;
            stackCount = 0;
            tickCount = 0;
            attachmentResolved = false;
            cachedCamera = null;
        }

        private void Reapply(MMOAbilityVfxContext context)
        {
            source = context.Source;
            lastReleaseAt = Time.realtimeSinceStartup;
            expiresAt = Time.time + profile.BleedDuration;
            tickCount = 0;
            int previousStack = stackCount;
            stackCount = Mathf.Min(3, stackCount + 1);
            UpdateAttachment();
            ApplyPersistentWound();
            PlayImpact(false);
            PlayStackIncrease(stackCount > previousStack);
        }

        private void SubscribeEvents()
        {
            GougeVFXEventRelay.DamageResolved -= OnGougeDamageResolved;
            GougeVFXEventRelay.DamageResolved += OnGougeDamageResolved;
            if (targetCombatant != null)
            {
                targetCombatant.Died -= OnTargetDied;
                targetCombatant.Died += OnTargetDied;
            }
        }

        private void UnsubscribeEvents()
        {
            GougeVFXEventRelay.DamageResolved -= OnGougeDamageResolved;
            if (targetCombatant != null)
            {
                targetCombatant.Died -= OnTargetDied;
            }
        }

        private void OnGougeDamageResolved(GougeVFXEventRelay.DamagePresentation damage)
        {
            if (!persistent || damage.Target != targetCombatant)
            {
                return;
            }

            float sinceRelease = Time.realtimeSinceStartup - lastReleaseAt;
            if (sinceRelease <= 0.8f)
            {
                if (damage.Record.isCritical)
                {
                    PlayCritical();
                }

                return;
            }

            PlayBleedTick();
        }

        private void OnTargetDied(MMOCombatant _)
        {
            BeginExpiration();
        }

        private void PlayImpact(bool critical)
        {
            float criticalScale = critical ? profile.CriticalFlashMultiplier : 1f;
            ConfigureAndPlay(contactFlash, 1, profile.ImpactDuration * 0.5f, 0f,
                profile.ContactFlashSize * 1.35f * criticalScale, Brighten(profile.Colors.ImpactWhite, profile.ContactFlashBrightness));
            ConfigureAndPlay(impactLines, critical ? 14 : 11, profile.ImpactDuration * 0.72f, 4.3f,
                0.4f * profile.OverallScale, Brighten(profile.Colors.ImpactYellow, 1.3f));
            ConfigureAndPlay(directionalBloodSpray,
                Mathf.RoundToInt(profile.MainBloodSprayAmount * (critical ? profile.CriticalBloodMultiplier : 1f)),
                0.68f, profile.SprayLength * 2.1f, 0.52f * profile.OverallScale, Brighten(profile.Colors.Crimson, 1.08f));
            ConfigureAndPlay(closeBloodBurst,
                Mathf.RoundToInt(profile.CloseBurstAmount * (critical ? profile.CriticalBloodMultiplier : 1f)),
                0.58f, 2.4f, 0.48f * profile.OverallScale, Brighten(profile.Colors.DeepRed, 1.12f));
            ConfigureAndPlay(woundPulse, 1, 0.4f, 0f,
                profile.WoundSize * profile.OverallScale * 1.65f, Brighten(profile.Colors.Crimson, 1.35f));
            ConfigureAndPlay(bodyAccent, 1, 0.34f, 0f,
                profile.WoundSize * profile.OverallScale * 1.85f, WithAlpha(profile.Colors.DeepRed, 0.56f));
            ConfigureAndPlay(tornFragments, profile.TornFragmentCount, 0.52f, 1.2f,
                0.13f * profile.OverallScale, Brighten(profile.Colors.BrownRed, 1.08f));
            ConfigureAndPlay(impactGroundBurst, profile.DustBurstAmount, 0.78f, 3.1f,
                profile.DustBurstSize * profile.OverallScale, Color.white);
            ConfigureAndPlay(environmentalHeavyDust, profile.EnvironmentalHeavyDustAmount,
                profile.EnvironmentalHeavyDustLifetime, 2.55f,
                profile.EnvironmentalHeavyDustSize * profile.OverallScale, Color.white);
            ConfigureAndPlay(environmentalFineDust, profile.EnvironmentalFineDustAmount,
                profile.EnvironmentalFineDustLifetime, 1.4f,
                profile.EnvironmentalFineDustSize * profile.OverallScale, Color.white);
            ConfigureAndPlay(impactDustRing, 1, 0.56f, 0f,
                profile.DustRingSize * profile.OverallScale, new Color(0.66f, 0.58f, 0.46f, 0.72f));
            ConfigureAndPlay(groundDebris, profile.GroundDebrisCount, 0.72f, 3.2f,
                profile.GroundDebrisSize * profile.OverallScale, Color.white);
        }

        private void PlayBleedTick()
        {
            tickCount++;
            tickVariation = (tickVariation + 1) % 3;
            float finalMultiplier = tickCount % 3 == 0 ? profile.FinalTickMultiplier : 1f;
            float intensity = profile.StackTickIntensity(stackCount) * finalMultiplier;
            if (tickBloodSpray != null)
            {
                tickBloodSpray.transform.localRotation = Quaternion.Euler(0f, tickVariation * 37f - 24f,
                    profile.SprayAngle * (tickVariation - 1));
            }

            ConfigureAndPlay(woundPulse, 1, profile.TickDuration, 0f,
                profile.TickPulseSize * profile.OverallScale * intensity, Brighten(profile.Colors.Crimson, profile.TickFlashBrightness));
            ConfigureAndPlay(bodyAccent, 1, profile.TickDuration * 0.8f, 0f,
                profile.WoundSize * profile.OverallScale * 1.45f, WithAlpha(profile.Colors.DeepRed, profile.TickBodyAccentStrength));
            ConfigureAndPlay(tickBloodSpray, Mathf.RoundToInt(profile.TickSprayAmount * intensity),
                0.42f, 0.9f + tickVariation * 0.12f, 0.18f, profile.Colors.Crimson);
            ConfigureAndPlay(tickHeavyDrips, Mathf.RoundToInt(profile.TickDropletCount * intensity),
                0.72f, 0.45f, profile.DripSize * 1.2f, profile.Colors.DeepRed);
        }

        private void PlayStackIncrease(bool increased)
        {
            int count = increased ? stackCount : 3;
            for (int i = 0; i < stackTearingStreaks.Length; i++)
            {
                ConfigureAndPlay(stackTearingStreaks[i], i < count ? 1 : 0, 0.32f, 1.1f,
                    0.28f * profile.OverallScale, Brighten(profile.Colors.Crimson, 1.2f));
            }

            ConfigureAndPlay(closeBloodBurst, Mathf.RoundToInt(profile.CloseBurstAmount * 0.85f),
                0.36f, 0.8f, 0.17f, profile.Colors.Crimson);
        }

        private void PlayCritical()
        {
            PlayImpact(true);
            ConfigureAndPlay(criticalFlash, 1, profile.ImpactDuration * 0.42f, 0f,
                profile.ContactFlashSize * profile.CriticalFlashMultiplier, Brighten(profile.Colors.ImpactWhite, 1.8f));
            ConfigureAndPlay(criticalBloodBurst,
                Mathf.RoundToInt(profile.MainBloodSprayAmount * profile.CriticalBloodMultiplier),
                0.5f, 1.45f, 0.25f, profile.Colors.Crimson);
            ConfigureAndPlay(criticalSparks, profile.CriticalSparkAmount, 0.32f, 2.2f,
                0.11f, Brighten(profile.Colors.Metallic, 1.45f));
            for (int i = 0; i < criticalTearingStreaks.Length; i++)
            {
                ConfigureAndPlay(criticalTearingStreaks[i], i < profile.CriticalTrailCount ? 1 : 0,
                    0.26f, 1.25f, 0.24f, Brighten(profile.Colors.Crimson, 1.25f));
            }

            Transform hand = ResolveWeaponHand();
            if (criticalResetRing != null && hand != null)
            {
                criticalResetRing.transform.position = hand.position;
            }

            ConfigureAndPlay(criticalResetRing, 1, profile.ResetRingDuration, 0f,
                profile.ResetRingSize * profile.OverallScale, Brighten(profile.Colors.ImpactYellow, profile.ResetRingBrightness));
        }

        private void ApplyPersistentWound()
        {
            float lifetime = profile.BleedDuration + Mathf.Max(profile.WoundDissolveDuration, profile.MistFadeDuration);
            float stackScale = profile.StackWoundScale(stackCount) * profile.WoundSize * profile.OverallScale;
            float brightness = profile.StackBrightness(stackCount);
            for (int i = 0; i < woundBases.Length; i++)
            {
                bool visible = i == Mathf.Clamp(stackCount - 1, 0, woundBases.Length - 1);
                EmitPersistent(woundBases[i], visible, lifetime, stackScale,
                    WithAlpha(Brighten(profile.Colors.Maroon, brightness * 0.9f), profile.WoundBaseOpacity),
                    profile.WoundOrientation);
                EmitPersistent(woundInners[i], visible, lifetime, stackScale * 0.9f,
                    Brighten(profile.Colors.Crimson, profile.InnerCutBrightness * brightness),
                    profile.WoundOrientation);
                EmitPersistent(wetHighlights[i], visible, lifetime, stackScale * 0.62f,
                    WithAlpha(Brighten(profile.Colors.ImpactYellow, brightness), profile.WetHighlightAmount),
                    profile.WoundOrientation);
            }

            ConfigureLoop(bloodSeepage, 1.4f * profile.StackSeepage(stackCount) * profile.GoreIntensity,
                0.75f, 0.08f, profile.Colors.Crimson);
            ConfigureLoop(woundMist, 0.8f * profile.StackMist(stackCount) * profile.WoundMistIntensity,
                0.9f, 0.28f, WithAlpha(profile.Colors.DeepRed, 0.24f));
        }

        private void BeginExpiration()
        {
            if (expiring || !persistent)
            {
                return;
            }

            expiring = true;
            expirationStartedAt = Time.time;
            StopLoop(bloodSeepage);
            StopLoop(woundMist);
            ConfigureAndPlay(expirationFragments, 3, profile.WoundDissolveDuration, 0.3f,
                0.16f * profile.OverallScale, profile.Colors.DeepRed);
            ConfigureAndPlay(finalDroplets, profile.FinalDropletAmount, 0.72f, 0.35f,
                profile.DripSize, profile.Colors.DeepRed);
        }

        private void UpdateAttachment()
        {
            if (!attachmentResolved || attachedRoot == null || target == null)
            {
                return;
            }

            Vector3 anchor = target.TransformPoint(woundAnchorLocal);
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            Vector3 toCamera = cachedCamera != null
                ? cachedCamera.transform.position - anchor
                : source != null ? source.position - anchor : -target.forward;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                toCamera = -target.forward;
            }

            Vector3 facing = toCamera.normalized;
            Vector3 up = cachedCamera != null ? cachedCamera.transform.up : target.up;
            attachedRoot.SetPositionAndRotation(
                anchor + facing * profile.WoundCameraOffset,
                Quaternion.LookRotation(facing, up));
            if (groundReactionRoot != null)
            {
                Vector3 ground = target.TransformPoint(groundAnchorLocal);
                groundReactionRoot.SetPositionAndRotation(ground, Quaternion.identity);
            }
        }

        private void ResolveAttachment(Vector3 requestedAnchor)
        {
            Bounds bounds = ResolveTargetBounds();
            Vector3 center = bounds.size.sqrMagnitude > 0.001f
                ? bounds.center
                : target.TransformPoint(new Vector3(0f, 1.05f, 0f));
            Vector3 anchor = requestedAnchor;
            if ((anchor - target.position).sqrMagnitude < 0.01f)
            {
                anchor = center + Vector3.up * (bounds.size.sqrMagnitude > 0.001f ? bounds.extents.y * 0.08f : 0f);
            }

            woundAnchorLocal = target.InverseTransformPoint(new Vector3(center.x, anchor.y, center.z));
            float groundY = bounds.size.sqrMagnitude > 0.001f ? bounds.min.y + 0.025f : target.position.y + 0.025f;
            groundAnchorLocal = target.InverseTransformPoint(new Vector3(center.x, groundY, center.z));
            attachmentResolved = true;
        }

        private Bounds ResolveTargetBounds()
        {
            Collider rootCollider = target.GetComponent<Collider>();
            if (IsUsableBodyCollider(rootCollider))
            {
                return rootCollider.bounds;
            }

            bool found = false;
            Bounds combined = default;
            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            {
                if (!IsUsableBodyCollider(collider) || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!found)
                {
                    combined = collider.bounds;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(collider.bounds);
                }
            }

            if (found)
            {
                return combined;
            }

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsUsableBodyRenderer(renderer) || renderer.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!found)
                {
                    combined = renderer.bounds;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return found ? combined : default;
        }

        private static bool IsUsableBodyCollider(Collider collider)
        {
            return collider != null
                && collider.enabled
                && !collider.isTrigger
                && collider.gameObject.activeInHierarchy;
        }

        private static bool IsUsableBodyRenderer(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer is MeshRenderer or SkinnedMeshRenderer;
        }

        private Transform ResolveWeaponHand()
        {
            if (source == null)
            {
                return null;
            }

            MMOAbilityVfxAnchors anchors = source.GetComponent<MMOAbilityVfxAnchors>();
            return anchors != null && anchors.RightHandAnchor != null ? anchors.RightHandAnchor : source;
        }

        private Color Brighten(Color color, float multiplier)
        {
            float brightness = profile.OverallBrightness * multiplier;
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static void ConfigureAndPlay(
            ParticleSystem system,
            int count,
            float lifetime,
            float speed,
            float size,
            Color color)
        {
            if (system == null)
            {
                return;
            }

            int safeCount = Mathf.Clamp(count, 0, short.MaxValue);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSpeed = Mathf.Max(0f, speed);
            main.startSize = Mathf.Max(0.01f, size);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, safeCount);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = safeCount > 0;
            emission.rateOverTime = 0f;
            emission.SetBursts(safeCount > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)safeCount) }
                : Array.Empty<ParticleSystem.Burst>());
            if (safeCount > 0)
            {
                system.Play(true);
            }
        }

        private static void EmitPersistent(
            ParticleSystem system,
            bool visible,
            float lifetime,
            float size,
            Color color,
            float rotationDegrees)
        {
            if (system == null)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!visible)
            {
                return;
            }

            ParticleSystem.EmitParams parameters = new()
            {
                startLifetime = Mathf.Max(0.1f, lifetime),
                startSize = Mathf.Max(0.01f, size),
                startColor = color,
                rotation = rotationDegrees * Mathf.Deg2Rad
            };
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            system.Play(true);
            system.Emit(parameters, 1);
        }

        private static void ConfigureLoop(ParticleSystem system, float rate, float lifetime, float size, Color color)
        {
            if (system == null)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = Mathf.Max(0.1f, lifetime);
            main.startSize = Mathf.Max(0.01f, size);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = rate > 0f;
            emission.rateOverTime = Mathf.Max(0f, rate);
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            if (rate > 0f)
            {
                system.Play(true);
            }
        }

        private static void StopLoop(ParticleSystem system)
        {
            system?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void CacheParticles()
        {
            allParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void StopAllParticles()
        {
            foreach (ParticleSystem system in allParticles)
            {
                system?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
