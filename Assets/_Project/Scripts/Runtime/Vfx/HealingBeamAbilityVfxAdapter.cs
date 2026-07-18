using System.Collections;
using RPGClone.Abilities;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Healing
{
    /// <summary>
    /// Bridges the generic ability-presentation context to HealingBeamVFX without owning gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealingBeamVFX))]
    public sealed class HealingBeamAbilityVfxAdapter : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler
    {
        [SerializeField] private HealingBeamVFX healingBeam;
        [SerializeField, Min(0f)] private float replicatedHealWaitSeconds = 0.75f;
        [SerializeField, Min(0f)] private float particleReleaseBufferSeconds = 1.8f;

        private MMOAbilityDefinition ability;
        private MMOCombatant sourceCombatant;
        private MMOCombatant targetCombatant;
        private Transform targetAttachmentProxy;
        private Coroutine releaseRoutine;
        private bool tickTriggered;
        private bool released;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnDestroy()
        {
            UnsubscribeFromHealEvents();
            DestroyTargetAttachmentProxy();
        }

        private void OnDisable()
        {
            UnsubscribeFromHealEvents();
            DestroyTargetAttachmentProxy();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            EnsureReferences();
            UnsubscribeFromHealEvents();

            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
                releaseRoutine = null;
            }

            ability = context.Ability;
            sourceCombatant = context.SourceSystem != null ? context.SourceSystem.Combatant : null;
            targetCombatant = context.Target != null ? context.Target.GetComponent<MMOCombatant>() : null;
            tickTriggered = false;
            released = false;

            Transform targetAttachment = CreateTargetAttachment(context);
            if (healingBeam != null && targetAttachment != null)
            {
                healingBeam.Play(transform, targetAttachment);
            }

            if (targetCombatant != null)
            {
                targetCombatant.Healed -= OnTargetHealed;
                targetCombatant.Healed += OnTargetHealed;
            }

            // This prefab is spawned by AbilityReleased, after cast completion. Own the
            // one-shot launch lifetime here because release prefabs are not tracked as
            // active casting instances by MMOAbilityVfxController.
            releaseRoutine = StartCoroutine(ReleaseAfterResolvedTick());
        }

        public void Release(bool immediate)
        {
            if (released)
            {
                return;
            }

            if (immediate)
            {
                released = true;
                if (releaseRoutine != null)
                {
                    StopCoroutine(releaseRoutine);
                    releaseRoutine = null;
                }

                UnsubscribeFromHealEvents();
                healingBeam?.StopImmediate();
                Destroy(gameObject);
                return;
            }

            if (releaseRoutine == null)
            {
                releaseRoutine = StartCoroutine(ReleaseAfterResolvedTick());
            }
        }

        public void ConfigureAuthoring(HealingBeamVFX newHealingBeam)
        {
            healingBeam = newHealingBeam;
        }

        private IEnumerator ReleaseAfterResolvedTick()
        {
            float waitUntil = Time.time + replicatedHealWaitSeconds;
            while (!tickTriggered && Time.time < waitUntil)
            {
                yield return null;
            }

            if (tickTriggered)
            {
                yield return new WaitForSeconds(CalculatePulseTravelSeconds());
            }

            healingBeam?.Stop();
            float fadeSeconds = healingBeam != null && healingBeam.Profile != null
                ? healingBeam.Profile.FadeOutDuration
                : 0.4f;
            yield return new WaitForSeconds(fadeSeconds + particleReleaseBufferSeconds);

            released = true;
            UnsubscribeFromHealEvents();
            Destroy(gameObject);
        }

        private void OnTargetHealed(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition resolvedAbility, int amount)
        {
            if (released
                || tickTriggered
                || amount <= 0
                || target != targetCombatant
                || resolvedAbility != ability
                || (sourceCombatant != null && source != sourceCombatant))
            {
                return;
            }

            tickTriggered = true;
            healingBeam?.TriggerHealingTick();
        }

        private Transform CreateTargetAttachment(MMOAbilityVfxContext context)
        {
            if (targetAttachmentProxy != null)
            {
                Destroy(targetAttachmentProxy.gameObject);
                targetAttachmentProxy = null;
            }

            if (context.Target == null)
            {
                return null;
            }

            Vector3 targetPosition = context.TargetPosition;
            MMOAbilityVfxAnchors targetAnchors = context.Target.GetComponent<MMOAbilityVfxAnchors>();
            if (targetAnchors != null)
            {
                targetPosition = targetAnchors.ResolveHitPosition(context.Definition);
            }

            GameObject proxy = new("Healing Beam Target Attachment");
            targetAttachmentProxy = proxy.transform;
            targetAttachmentProxy.SetParent(context.Target, true);
            targetAttachmentProxy.position = targetPosition;
            targetAttachmentProxy.rotation = Quaternion.identity;
            return targetAttachmentProxy;
        }

        private float CalculatePulseTravelSeconds()
        {
            HealingBeamVFXProfile activeProfile = healingBeam != null ? healingBeam.Profile : null;
            if (activeProfile == null)
            {
                return 0.75f;
            }

            return (1f + activeProfile.PulseWidth) / Mathf.Max(0.1f, activeProfile.PulseSpeed);
        }

        private void UnsubscribeFromHealEvents()
        {
            if (targetCombatant != null)
            {
                targetCombatant.Healed -= OnTargetHealed;
            }
        }

        private void DestroyTargetAttachmentProxy()
        {
            if (targetAttachmentProxy == null)
            {
                return;
            }

            Destroy(targetAttachmentProxy.gameObject);
            targetAttachmentProxy = null;
        }

        private void EnsureReferences()
        {
            if (healingBeam == null)
            {
                healingBeam = GetComponent<HealingBeamVFX>();
            }
        }
    }
}
