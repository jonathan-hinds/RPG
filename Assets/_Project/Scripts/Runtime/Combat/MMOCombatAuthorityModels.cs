using System;
using RPGClone.CharacterSelection;
using UnityEngine;

namespace RPGClone.Combat
{
    public interface ICombatAuthority
    {
        bool IsAuthority { get; }
        bool ResolveRequest(CombatActionRequest request, out string failureReason);
    }

    public interface ICombatActionRequestService
    {
        void SubmitRequest(CombatActionRequest request);
    }

    public interface ICombatEventStream
    {
        void Publish(CombatEventRecord record);
    }

    public interface IEnemySessionAuthority
    {
        bool IsAuthorityOwner { get; }
    }

    public interface IEnemyStateReplicator
    {
        EnemySnapshot CreateSnapshot();
        void ApplySnapshot(EnemySnapshot snapshot);
    }

    public interface ISessionSpawnRegistry
    {
        bool TryResolveEnemy(string spawnId, out UnityEngine.Object enemy);
    }

    public enum CombatActionRequestKind
    {
        Ability,
        AutoAttack,
        ChannelStart,
        ChannelCancel,
        ChargeImpact
    }

    public enum CombatEventType
    {
        CastStarted,
        CastInterrupted,
        AbilityReleased,
        DamageResolved,
        HealResolved,
        Missed,
        Blocked,
        Death,
        CastCompleted,
        BuffApplied
    }

    public enum EnemyRuntimeState
    {
        Alive,
        Corpse,
        Respawning
    }

    [Serializable]
    public sealed class CombatActionRequest
    {
        public string requestId;
        public string sessionId;
        public string requesterCharacterId;
        public string casterCharacterId;
        public string targetCharacterId;
        public string targetEnemySpawnId;
        public string abilityId;
        public Vector3SaveData requestedTargetPosition;
        public bool hasGroundTarget;
        public CombatActionRequestKind requestKind;
        public long requestedUtcTicks;
        public bool processed;

        public static CombatActionRequest Create(
            string sessionId,
            string requesterCharacterId,
            string casterCharacterId,
            string targetCharacterId,
            string targetEnemySpawnId,
            string abilityId,
            Vector3 requestedTargetPosition,
            bool hasGroundTarget,
            CombatActionRequestKind requestKind)
        {
            return new CombatActionRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                sessionId = sessionId ?? string.Empty,
                requesterCharacterId = requesterCharacterId ?? string.Empty,
                casterCharacterId = casterCharacterId ?? string.Empty,
                targetCharacterId = targetCharacterId ?? string.Empty,
                targetEnemySpawnId = targetEnemySpawnId ?? string.Empty,
                abilityId = abilityId ?? string.Empty,
                requestedTargetPosition = new Vector3SaveData(requestedTargetPosition),
                hasGroundTarget = hasGroundTarget,
                requestKind = requestKind,
                requestedUtcTicks = DateTime.UtcNow.Ticks
            };
        }
    }

    [Serializable]
    public sealed class DamageResolutionResult
    {
        public int requestedAmount;
        public int appliedAmount;
        public int blockedAmount;
        public bool missed;
        public bool critical;
        public bool killedTarget;
    }

    [Serializable]
    public sealed class HealResolutionResult
    {
        public int requestedAmount;
        public int appliedAmount;
    }

    [Serializable]
    public sealed class CombatEventRecord
    {
        public string eventId;
        public string sessionId;
        public CombatEventType eventType;
        public string sourceCharacterId;
        public string targetCharacterId;
        public string sourceEnemySpawnId;
        public string targetEnemySpawnId;
        public string abilityId;
        public Vector3SaveData targetPosition;
        public bool hasGroundTarget;
        public float castDurationSeconds;
        public int damageAmount;
        public int healAmount;
        public int blockedAmount;
        public int absorbedAsManaAmount;
        public bool hasTargetResourceSnapshot;
        public int targetCurrentHealth;
        public int targetMaxHealth;
        public int targetCurrentMana;
        public int targetMaxMana;
        public bool isCritical;
        public bool killedTarget;
        public long createdUtcTicks;

        public static CombatEventRecord Create(CombatEventType eventType)
        {
            return new CombatEventRecord
            {
                eventId = Guid.NewGuid().ToString("N"),
                eventType = eventType,
                createdUtcTicks = DateTime.UtcNow.Ticks
            };
        }
    }

    [Serializable]
    public sealed class EnemySpawnState
    {
        public string spawnId;
        public string definitionId;
        public string displayName;
        public Vector3SaveData homePosition;
        public Vector3SaveData homeRotationEuler;
    }

    [Serializable]
    public sealed class EnemySnapshot
    {
        public string sessionId;
        public string spawnId;
        public string definitionId;
        public string displayName;
        public EnemyRuntimeState runtimeState;
        public int currentHealth;
        public int maxHealth;
        public int currentMana;
        public int maxMana;
        public Vector3SaveData position;
        public Vector3SaveData rotationEuler;
        public float worldSpeed;
        public string currentTargetCharacterId;
        public bool inCombat;
        public bool leashing;
        public Vector3SaveData leashAnchorPosition;
        public string castAbilityId;
        public string castTargetCharacterId;
        public float castDurationSeconds;
        public float castNormalizedProgress;
        public float corpseRemainingSeconds;
        public float respawnRemainingSeconds;
        public long updatedUtcTicks;
    }
}
