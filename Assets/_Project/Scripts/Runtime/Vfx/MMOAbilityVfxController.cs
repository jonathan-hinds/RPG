using System.Collections;
using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Vfx
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MMOAbilitySystem))]
    public sealed class MMOAbilityVfxController : MonoBehaviour
    {
        [SerializeField] private MMOAbilitySystem abilitySystem;
        [SerializeField] private MMOAbilityVfxAnchors anchors;
        [SerializeField] private Transform vfxRoot;

        private readonly System.Collections.Generic.List<GameObject> activeCastingInstances = new();
        private Coroutine pendingHitRoutine;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.CastStarted -= OnCastStarted;
            abilitySystem.CastStarted += OnCastStarted;
            abilitySystem.CastInterrupted -= OnCastInterrupted;
            abilitySystem.CastInterrupted += OnCastInterrupted;
            abilitySystem.CastCompleted -= OnCastCompleted;
            abilitySystem.CastCompleted += OnCastCompleted;
            abilitySystem.AbilityReleased -= OnAbilityReleased;
            abilitySystem.AbilityReleased += OnAbilityReleased;
            abilitySystem.ChargeStarted -= OnChargeStarted;
            abilitySystem.ChargeStarted += OnChargeStarted;
            abilitySystem.ChargeCompleted -= OnChargeCompleted;
            abilitySystem.ChargeCompleted += OnChargeCompleted;
        }

        private void OnDisable()
        {
            if (abilitySystem != null)
            {
                abilitySystem.CastStarted -= OnCastStarted;
                abilitySystem.CastInterrupted -= OnCastInterrupted;
                abilitySystem.CastCompleted -= OnCastCompleted;
                abilitySystem.AbilityReleased -= OnAbilityReleased;
                abilitySystem.ChargeStarted -= OnChargeStarted;
                abilitySystem.ChargeCompleted -= OnChargeCompleted;
            }

            StopCastingVfx(true);
            if (pendingHitRoutine != null)
            {
                StopCoroutine(pendingHitRoutine);
                pendingHitRoutine = null;
            }
        }

        private void OnCastStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            if (source != abilitySystem || ability == null)
            {
                return;
            }

            MMOAbilityVfxDefinition definition = ability.VisualEffects;
            if (definition == null || definition.CastingPrefab == null)
            {
                return;
            }

            StopCastingVfx(true);
            if (anchors != null && anchors.HasExplicitCastingAnchor)
            {
                SpawnCastingVfx(ability, definition, target, ResolveCastingParent(), anchors.ExplicitCastingPosition);
                return;
            }

            if (definition.UseHandCastingAnchors && TrySpawnHandCastingVfx(ability, definition, target))
            {
                return;
            }

            SpawnCastingVfx(ability, definition, target, ResolveCastingParent(), anchors != null ? anchors.ResolveCastingPosition(definition) : transform.TransformPoint(definition.CastingLocalOffset));
        }

        private void OnCastInterrupted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, string reason)
        {
            if (source == abilitySystem)
            {
                StopCastingVfx(true);
            }
        }

        private void OnCastCompleted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source == abilitySystem)
            {
                StopCastingVfx(false);
            }
        }

        private void OnAbilityReleased(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (source != abilitySystem || ability == null || ability.VisualEffects == null)
            {
                return;
            }

            PlayRelease(ability, ability.VisualEffects, target, targetPosition, hasGroundTarget, false);
        }

        private void OnChargeStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source != abilitySystem || ability == null || ability.VisualEffects == null)
            {
                return;
            }

            PlayRelease(ability, ability.VisualEffects, target, ResolveTargetPosition(target, transform.position, false), false, true);
        }

        private void OnChargeCompleted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (source == abilitySystem && ability != null && ability.VisualEffects != null)
            {
                SpawnHit(ability, ability.VisualEffects, target, ResolveTargetPosition(target, transform.position, false), false);
            }
        }

        private void PlayRelease(
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget,
            bool suppressAutomaticHit)
        {
            Vector3 sourcePosition = anchors != null ? anchors.ResolveCastOriginPosition(definition) : transform.TransformPoint(definition.CastOriginLocalOffset);
            bool hitRequested = false;
            void RequestHit()
            {
                if (hitRequested)
                {
                    return;
                }

                hitRequested = true;
                SpawnHit(ability, definition, target, targetPosition, hasGroundTarget);
            }

            if (definition.CastPrefab != null)
            {
                Quaternion rotation = transform.rotation;
                if (definition.AlignCastPrefabToTarget)
                {
                    Vector3 direction = targetPosition - sourcePosition;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    }
                }

                GameObject instance = MMOAbilityVfxPool.Spawn(definition.CastPrefab, sourcePosition, rotation, vfxRoot);
                InitializeInstance(instance, ability, definition, target, sourcePosition, targetPosition, hasGroundTarget, RequestHit);
            }

            if (!suppressAutomaticHit && !definition.CastPrefabControlsHitTiming)
            {
                if (pendingHitRoutine != null)
                {
                    StopCoroutine(pendingHitRoutine);
                }

                pendingHitRoutine = StartCoroutine(SpawnHitAfterDelay(ability, definition, target, targetPosition, hasGroundTarget));
            }
        }

        private IEnumerator SpawnHitAfterDelay(
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (definition.HitDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(definition.HitDelaySeconds);
            }

            SpawnHit(ability, definition, target, targetPosition, hasGroundTarget);
            pendingHitRoutine = null;
        }

        private void SpawnHit(
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (definition == null || definition.HitPrefab == null)
            {
                return;
            }

            Transform targetTransform = target != null ? target.transform : null;
            Transform parent = definition.AttachHitToTarget && targetTransform != null ? targetTransform : vfxRoot;
            Vector3 position = ResolveTargetPosition(target, targetPosition, hasGroundTarget, definition);
            Quaternion rotation = hasGroundTarget ? Quaternion.identity : transform.rotation;
            GameObject instance = MMOAbilityVfxPool.Spawn(definition.HitPrefab, position, rotation, parent);
            InitializeInstance(instance, ability, definition, target, anchors != null ? anchors.ResolveCastOriginPosition(definition) : transform.position, position, hasGroundTarget, null);
        }

        private Vector3 ResolveTargetPosition(MMOCharacterIdentity target, Vector3 fallbackPosition, bool hasGroundTarget, MMOAbilityVfxDefinition definition = null)
        {
            if (!hasGroundTarget && target != null)
            {
                MMOAbilityVfxAnchors targetAnchors = target.GetComponent<MMOAbilityVfxAnchors>();
                if (targetAnchors != null)
                {
                    return targetAnchors.ResolveHitPosition(definition);
                }

                return target.transform.TransformPoint(new Vector3(0f, 1.05f, 0f));
            }

            return hasGroundTarget ? fallbackPosition + Vector3.up * 0.05f : fallbackPosition;
        }

        private void InitializeInstance(
            GameObject instance,
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            MMOCharacterIdentity target,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            bool hasGroundTarget,
            System.Action requestHit)
        {
            if (instance == null)
            {
                return;
            }

            MMOAbilityVfxContext context = new(
                abilitySystem,
                ability,
                definition,
                transform,
                target != null ? target.transform : null,
                sourcePosition,
                targetPosition,
                hasGroundTarget,
                requestHit);

            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IMMOAbilityVfxInstance vfxInstance)
                {
                    vfxInstance.Initialize(context);
                }
            }
        }

        private Transform ResolveCastingParent()
        {
            if (anchors != null && anchors.CastingAnchor != null)
            {
                return anchors.CastingAnchor;
            }

            return transform;
        }

        private bool TrySpawnHandCastingVfx(MMOAbilityDefinition ability, MMOAbilityVfxDefinition definition, MMOCharacterIdentity target)
        {
            if (anchors == null)
            {
                return false;
            }

            bool spawned = false;
            if (anchors.LeftHandAnchor != null)
            {
                SpawnCastingVfx(ability, definition, target, definition.AttachCastingToCaster ? anchors.LeftHandAnchor : null, anchors.ResolveLeftHandCastingPosition(definition));
                spawned = true;
            }

            if (anchors.RightHandAnchor != null && anchors.RightHandAnchor != anchors.LeftHandAnchor)
            {
                SpawnCastingVfx(ability, definition, target, definition.AttachCastingToCaster ? anchors.RightHandAnchor : null, anchors.ResolveRightHandCastingPosition(definition));
                spawned = true;
            }

            return spawned;
        }

        private void SpawnCastingVfx(
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            MMOCharacterIdentity target,
            Transform parent,
            Vector3 position)
        {
            GameObject instance = MMOAbilityVfxPool.Spawn(definition.CastingPrefab, position, transform.rotation, parent);
            activeCastingInstances.Add(instance);
            bool hasGroundTarget = abilitySystem != null && abilitySystem.CurrentCastHasGroundTarget;
            Vector3 targetPosition = hasGroundTarget
                ? abilitySystem.CurrentCastGroundTargetPosition
                : ResolveTargetPosition(target, position, false);
            InitializeInstance(instance, ability, definition, target, position, targetPosition, hasGroundTarget, null);
        }

        private void StopCastingVfx(bool immediate)
        {
            if (activeCastingInstances.Count == 0)
            {
                return;
            }

            foreach (GameObject activeCastingInstance in activeCastingInstances)
            {
                if (activeCastingInstance == null)
                {
                    continue;
                }

                bool customReleaseHandled = false;
                MonoBehaviour[] behaviours = activeCastingInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IMMOAbilityVfxReleaseHandler releaseHandler)
                    {
                        releaseHandler.Release(immediate);
                        customReleaseHandled = true;
                    }
                }

                if (customReleaseHandled)
                {
                    continue;
                }

                MMOAbilityVfxLifetime lifetime = activeCastingInstance.GetComponent<MMOAbilityVfxLifetime>();
                if (lifetime != null)
                {
                    lifetime.StopAndRelease();
                }
                else
                {
                    MMOAbilityVfxPool.Release(activeCastingInstance);
                }
            }

            activeCastingInstances.Clear();
        }

        private void EnsureReferences()
        {
            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (anchors == null)
            {
                anchors = GetComponent<MMOAbilityVfxAnchors>();
                if (anchors == null)
                {
                    anchors = gameObject.AddComponent<MMOAbilityVfxAnchors>();
                }
            }

            if (vfxRoot == null)
            {
                Transform existing = transform.Find("Ability VFX");
                if (existing == null)
                {
                    GameObject root = new("Ability VFX");
                    root.transform.SetParent(transform, false);
                    existing = root.transform;
                }

                vfxRoot = existing;
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureReferences();
            }
        }
    }
}
