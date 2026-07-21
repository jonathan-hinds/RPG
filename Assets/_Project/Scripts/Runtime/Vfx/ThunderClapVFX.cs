using System;
using System.Collections.Generic;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        private const string AbilityId = "warrior_thunderclap";

        [SerializeField] private ThunderClapVFXProfile profile;
        [SerializeField] private ThunderClapCastVFX castVfx;
        [SerializeField] private ThunderClapImpactVFX impactVfx;
        [SerializeField] private ThunderClapShockwaveVFX shockwaveVfx;
        [SerializeField] private ThunderClapAftermathVFX aftermathVfx;
        [SerializeField] private ThunderClapTargetReactionVFX[] targetReactionPool = Array.Empty<ThunderClapTargetReactionVFX>();

        private readonly List<PendingReaction> pendingReactions = new();
        private readonly HashSet<int> reactedTargetIds = new();
        private MMOAbilityVfxContext context;
        private MMOCombatant sourceCombatant;
        private Vector3 impactPosition;
        private float startedAt;
        private bool impactPlayed;
        private bool aftermathPlayed;
        private bool initialized;

        public bool IsPlaying => initialized;
        public ThunderClapVFXProfile Profile => profile;

        public void ConfigureAuthoring(
            ThunderClapVFXProfile newProfile,
            ThunderClapCastVFX newCastVfx,
            ThunderClapImpactVFX newImpactVfx,
            ThunderClapShockwaveVFX newShockwaveVfx,
            ThunderClapAftermathVFX newAftermathVfx,
            ThunderClapTargetReactionVFX[] newTargetReactionPool)
        {
            profile = newProfile;
            castVfx = newCastVfx;
            impactVfx = newImpactVfx;
            shockwaveVfx = newShockwaveVfx;
            aftermathVfx = newAftermathVfx;
            targetReactionPool = newTargetReactionPool ?? Array.Empty<ThunderClapTargetReactionVFX>();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null || castVfx == null || impactVfx == null || shockwaveVfx == null || aftermathVfx == null)
            {
                Debug.LogError("ThunderClapVFX is missing its profile or one or more phase controllers.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            sourceCombatant = context.Source != null ? context.Source.GetComponent<MMOCombatant>() : null;
            impactPosition = ResolveImpactPosition();
            transform.SetParent(null, true);
            transform.position = impactPosition;
            startedAt = Time.time;
            impactPlayed = false;
            aftermathPlayed = false;
            initialized = true;
            pendingReactions.Clear();
            reactedTargetIds.Clear();
            ResetChildren();
            castVfx.Play(profile, context.Source, impactPosition);
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        private void OnDestroy()
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
            if (!impactPlayed && elapsed >= profile.AnticipationDuration)
            {
                impactPlayed = true;
                castVfx.StopAnticipation();
                impactVfx.Play(profile, impactPosition);
                shockwaveVfx.Play(profile, impactPosition);
            }

            if (!aftermathPlayed && elapsed >= profile.AnticipationDuration + profile.ExpansionDuration * 0.42f)
            {
                aftermathPlayed = true;
                aftermathVfx.Play(profile, impactPosition);
            }

            PlayDueTargetReactions(elapsed);
            if (elapsed >= profile.TotalLifetime)
            {
                initialized = false;
                MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
                Destroy(gameObject);
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

            int targetId = target.GetInstanceID();
            if (!reactedTargetIds.Add(targetId))
            {
                return;
            }

            float travelDelay = Vector3.Distance(impactPosition, target.transform.position) / Mathf.Max(0.1f, profile.ExpansionSpeed);
            pendingReactions.Add(new PendingReaction(target.transform, profile.AnticipationDuration + Mathf.Clamp(travelDelay, 0.035f, profile.ExpansionDuration)));
        }

        private bool MatchesSource(MMOCombatant source)
        {
            if (source == null)
            {
                return false;
            }

            return source == sourceCombatant
                || (context.Source != null && (source.transform == context.Source || source.transform.IsChildOf(context.Source) || context.Source.IsChildOf(source.transform)));
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

                ThunderClapTargetReactionVFX reaction = AcquireTargetReaction();
                if (reaction != null)
                {
                    reaction.Play(profile, pending.Target, ReleaseTargetReaction);
                }
            }
        }

        private ThunderClapTargetReactionVFX AcquireTargetReaction()
        {
            foreach (ThunderClapTargetReactionVFX reaction in targetReactionPool)
            {
                if (reaction != null && !reaction.IsPlaying)
                {
                    return reaction;
                }
            }

            if (targetReactionPool.Length == 0 || targetReactionPool[0] == null)
            {
                return null;
            }

            targetReactionPool[0].ResetForPool();
            return targetReactionPool[0];
        }

        private static void ReleaseTargetReaction(ThunderClapTargetReactionVFX reaction)
        {
            reaction?.ResetForPool();
        }

        private Vector3 ResolveImpactPosition()
        {
            Vector3 position = context.HasGroundTarget ? context.TargetPosition
                : context.Target != null ? context.Target.position : context.Source != null ? context.Source.position : transform.position;
            return position + Vector3.up * 0.035f;
        }

        private void ResetChildren()
        {
            castVfx.ResetForPool();
            impactVfx.ResetForPool();
            shockwaveVfx.ResetForPool();
            aftermathVfx.ResetForPool();
            foreach (ThunderClapTargetReactionVFX reaction in targetReactionPool)
            {
                reaction?.ResetForPool();
            }
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
