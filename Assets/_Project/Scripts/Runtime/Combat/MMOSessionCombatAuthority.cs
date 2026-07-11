using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Combat
{
    public static class MMOSessionCombatAuthority
    {
        public static bool IsLocalAuthority => MMOGameplaySessionService.IsHostAuthority;

        public static bool ShouldRouteThroughHost(MMOCombatant caster, MMOAbilityDefinition ability, MMOCharacterIdentity target)
        {
            if (IsLocalAuthority || caster == null || ability == null)
            {
                return false;
            }

            return IsHostileCombatAbility(ability)
                && (ability.RequiresGroundTarget || target == null || target.GetComponent<MMOEnemyController>() != null);
        }

        public static bool TrySubmitRequest(
            MMOCombatant caster,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 requestedTargetPosition,
            bool hasGroundTarget,
            CombatActionRequestKind requestKind,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!ShouldRouteThroughHost(caster, ability, target))
            {
                return false;
            }

            if (!MMOGameplaySessionService.Players.TryGetParticipant(caster.Identity, out MMOPlayerParticipant casterParticipant)
                || string.IsNullOrWhiteSpace(casterParticipant.CharacterId))
            {
                failureReason = "No local session participant was available for the combat request.";
                return false;
            }

            string targetCharacterId = string.Empty;
            string targetEnemySpawnId = string.Empty;
            if (target != null)
            {
                if (MMOGameplaySessionService.Players.TryGetParticipant(target, out MMOPlayerParticipant targetParticipant))
                {
                    targetCharacterId = targetParticipant.CharacterId;
                }

                MMOEnemyController enemy = target.GetComponent<MMOEnemyController>();
                if (enemy != null)
                {
                    targetEnemySpawnId = enemy.SpawnId;
                }
            }

            if (!hasGroundTarget && string.IsNullOrWhiteSpace(targetCharacterId) && string.IsNullOrWhiteSpace(targetEnemySpawnId))
            {
                failureReason = "The host combat request could not resolve its target.";
                return false;
            }

            CombatActionRequest request = CombatActionRequest.Create(
                MMOGameplaySessionService.SessionId,
                casterParticipant.CharacterId,
                casterParticipant.CharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                ability.AbilityId,
                requestedTargetPosition,
                hasGroundTarget,
                requestKind);
            MMOSharedSessionState.PublishCombatRequest(request);
            return true;
        }

        public static bool IsHostileCombatAbility(MMOAbilityDefinition ability)
        {
            if (ability == null)
            {
                return false;
            }

            if (ability.TargetType == MMOAbilityTargetType.Hostile
                || (ability.HasArea && ability.AreaTargetFilter == MMOAbilityAreaTargetFilter.Hostile))
            {
                return true;
            }

            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (effect.EffectType == MMOAbilityEffectType.Damage
                    || effect.EffectType == MMOAbilityEffectType.PeriodicDamage
                    || effect.EffectType == MMOAbilityEffectType.Charge)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
