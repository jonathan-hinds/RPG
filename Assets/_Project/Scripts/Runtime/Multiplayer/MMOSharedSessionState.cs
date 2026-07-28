using System;
using System.Collections.Generic;
using System.Threading;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Loot;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    [Serializable]
    public sealed class MMOSessionParticipantSnapshot
    {
        public string participantId;
        public string characterId;
        public string accountId;
        public string sessionId;
        public string sceneName;
        public bool isHost;
        public long updatedUtcTicks;
        public long runtimeUtcTicks;
        public MMOCharacterSaveData characterData = new();
    }

    [Serializable]
    public sealed class MMOSharedAbilityEvent
    {
        public string eventId;
        public string sessionId;
        public string eventType;
        public string casterCharacterId;
        public string targetCharacterId;
        public string targetEnemySpawnId;
        public string abilityId;
        public int healAmount;
        public Vector3SaveData targetPosition;
        public bool hasGroundTarget;
        public float castDurationSeconds;
        public long createdUtcTicks;
        public List<string> appliedCharacterIds = new();
    }

    public static class MMOSharedAbilityEventTypes
    {
        public const string CastStarted = "cast_started";
        public const string CastInterrupted = "cast_interrupted";
        public const string CastCompleted = "cast_completed";
        public const string AbilityReleased = "ability_released";
        public const string ChargeStarted = "charge_started";
        public const string ChargeImpactStarted = "charge_impact_started";
        public const string ChargeCompleted = "charge_completed";
        public const string AutoAttackWindup = "auto_attack_windup";
        public const string HealResolved = "heal_resolved";
    }

    public static class MMOSharedRewardEventTypes
    {
        public const string Experience = "experience";
        public const string QuestKillCredit = "quest_kill_credit";
    }

    [Serializable]
    public sealed class MMOSharedRewardEvent
    {
        public string eventId;
        public string sessionId;
        public string eventType;
        public string targetCharacterId;
        public string enemySpawnId;
        public string enemyDefinitionId;
        public string creatureId;
        public int experienceAmount;
        public bool isPartyCredit;
        public long createdUtcTicks;
        public List<string> appliedCharacterIds = new();
    }

    [Serializable]
    public sealed class MMOSessionParticipantRuntimeSnapshot
    {
        public string sessionId;
        public string characterId;
        public Vector3SaveData position;
        public Vector3SaveData rotationEuler;
        public int currentHealth;
        public int currentMana;
        public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class MMONpcFacingSnapshot
    {
        public string sessionId;
        public string npcInteractionKey;
        public string actorCharacterId;
        public Vector3SaveData actorPosition;
        public long updatedUtcTicks;
    }

    public static class MMOSharedSessionState
    {
        private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CombatRequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan WorldObjectRequestTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan WorldObjectSnapshotTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan EnemySnapshotTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan CorpseLootSnapshotTimeout = TimeSpan.FromMinutes(5);
        private static readonly object Gate = new();
        private static MMOSharedSessionStore sharedState = new();
        private static MMOSharedSessionRuntimeStore participantRuntimeState = new();
        private static MMOSharedEnemyRuntimeStore worldRuntimeState = new();

        public static void Reset()
        {
            using (AcquireStateLease())
            {
                sharedState = new MMOSharedSessionStore();
                participantRuntimeState = new MMOSharedSessionRuntimeStore();
                worldRuntimeState = new MMOSharedEnemyRuntimeStore();
            }
        }

        public static void UpsertParticipant(MMOSessionParticipantSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.characterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertParticipant,
                    participant = snapshot
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                MMOSessionParticipantSnapshot existing = store.participants.Find(candidate =>
                    candidate.sessionId == snapshot.sessionId && candidate.characterId == snapshot.characterId);
                if (existing == null)
                {
                    existing = new MMOSessionParticipantSnapshot();
                    store.participants.Add(existing);
                }

                CopyParticipant(snapshot, existing);
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                existing.runtimeUtcTicks = existing.updatedUtcTicks;
                SaveStore(store);
                UpsertParticipantRuntimeInLease(
                    snapshot.sessionId,
                    snapshot.characterId,
                    snapshot.characterData.position.ToVector3(),
                    snapshot.characterData.rotationEuler.ToVector3(),
                    snapshot.characterData.currentHealth,
                    snapshot.characterData.currentMana);
            }
        }

        public static void UpsertParticipantRuntime(
            string sessionId,
            string characterId,
            Vector3 position,
            Vector3 rotationEuler,
            int currentHealth,
            int currentMana)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            MMOSessionParticipantRuntimeSnapshot runtimeSnapshot = new()
            {
                sessionId = sessionId,
                characterId = characterId,
                position = new Vector3SaveData(position),
                rotationEuler = new Vector3SaveData(rotationEuler),
                currentHealth = currentHealth,
                currentMana = currentMana,
                updatedUtcTicks = DateTime.UtcNow.Ticks
            };
            if (MMONetcodeSharedSessionTransport.TrySubmitParticipantRuntime(runtimeSnapshot))
            {
                return;
            }

            using (AcquireStateLease())
            {
                UpsertParticipantRuntimeInLease(sessionId, characterId, position, rotationEuler, currentHealth, currentMana);
            }
        }

        public static void RemoveParticipant(string sessionId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.RemoveParticipant,
                    sessionId = sessionId,
                    characterId = characterId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedSessionRuntimeStore runtimeStore = LoadRuntimeStore();
                if (store.participants.RemoveAll(candidate => candidate.sessionId == sessionId && candidate.characterId == characterId) > 0)
                {
                    SaveStore(store);
                }

                if (runtimeStore.participants.RemoveAll(candidate => candidate.sessionId == sessionId && candidate.characterId == characterId) > 0)
                {
                    SaveRuntimeStore(runtimeStore);
                }
            }
        }

        public static IReadOnlyList<MMOSessionParticipantSnapshot> GetParticipants(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMOSessionParticipantSnapshot>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedSessionRuntimeStore runtimeStore = LoadRuntimeStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                if (Prune(runtimeStore))
                {
                    SaveRuntimeStore(runtimeStore);
                }

                List<MMOSessionParticipantSnapshot> result = new();
                foreach (MMOSessionParticipantSnapshot participant in store.participants)
                {
                    if (participant != null && participant.sessionId == sessionId)
                    {
                        MMOSessionParticipantSnapshot clone = Clone(participant);
                        ApplyRuntimeSnapshot(clone, runtimeStore);
                        result.Add(clone);
                    }
                }

                return result;
            }
        }

        public static IReadOnlyList<MMOSessionParticipantRuntimeSnapshot> GetParticipantRuntimeSnapshots(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMOSessionParticipantRuntimeSnapshot>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionRuntimeStore runtimeStore = LoadRuntimeStore();
                if (Prune(runtimeStore))
                {
                    SaveRuntimeStore(runtimeStore);
                }

                List<MMOSessionParticipantRuntimeSnapshot> result = new();
                foreach (MMOSessionParticipantRuntimeSnapshot snapshot in runtimeStore.participants)
                {
                    if (snapshot != null && snapshot.sessionId == sessionId)
                    {
                        result.Add(Clone(snapshot));
                    }
                }

                return result;
            }
        }

        public static void PublishCastStartedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            float castDurationSeconds,
            Vector3 targetPosition,
            bool hasGroundTarget,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityEvent(
                sessionId,
                MMOSharedAbilityEventTypes.CastStarted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0,
                targetPosition,
                hasGroundTarget,
                castDurationSeconds,
                initiallyAppliedCharacterId);
        }

        public static void PublishAbilityReleasedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            Vector3 targetPosition,
            bool hasGroundTarget,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityEvent(
                sessionId,
                MMOSharedAbilityEventTypes.AbilityReleased,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0,
                targetPosition,
                hasGroundTarget,
                0f,
                initiallyAppliedCharacterId);
        }

        public static void PublishCastInterruptedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityLifecycleEvent(
                sessionId,
                MMOSharedAbilityEventTypes.CastInterrupted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0f,
                initiallyAppliedCharacterId);
        }

        public static void PublishCastCompletedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityLifecycleEvent(
                sessionId,
                MMOSharedAbilityEventTypes.CastCompleted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0f,
                initiallyAppliedCharacterId);
        }

        public static void PublishChargeStartedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityLifecycleEvent(
                sessionId,
                MMOSharedAbilityEventTypes.ChargeStarted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0f,
                initiallyAppliedCharacterId);
        }

        public static void PublishChargeImpactStartedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            float impactDelaySeconds,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityLifecycleEvent(
                sessionId,
                MMOSharedAbilityEventTypes.ChargeImpactStarted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                impactDelaySeconds,
                initiallyAppliedCharacterId);
        }

        public static void PublishChargeCompletedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            string initiallyAppliedCharacterId,
            string targetEnemySpawnId = "")
        {
            PublishAbilityLifecycleEvent(
                sessionId,
                MMOSharedAbilityEventTypes.ChargeCompleted,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0f,
                initiallyAppliedCharacterId);
        }

        private static void PublishAbilityLifecycleEvent(
            string sessionId,
            string eventType,
            string casterCharacterId,
            string targetCharacterId,
            string targetEnemySpawnId,
            string abilityId,
            float phaseDurationSeconds,
            string initiallyAppliedCharacterId)
        {
            PublishAbilityEvent(
                sessionId,
                eventType,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0,
                Vector3.zero,
                false,
                phaseDurationSeconds,
                initiallyAppliedCharacterId);
        }

        public static void PublishAutoAttackWindupEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string targetEnemySpawnId,
            string abilityId,
            float swingDurationSeconds,
            string initiallyAppliedCharacterId)
        {
            PublishAbilityEvent(
                sessionId,
                MMOSharedAbilityEventTypes.AutoAttackWindup,
                casterCharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                abilityId,
                0,
                Vector3.zero,
                false,
                swingDurationSeconds,
                initiallyAppliedCharacterId);
        }

        public static void PublishHealEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            int healAmount,
            string initiallyAppliedCharacterId)
        {
            PublishAbilityEvent(
                sessionId,
                MMOSharedAbilityEventTypes.HealResolved,
                casterCharacterId,
                targetCharacterId,
                string.Empty,
                abilityId,
                healAmount,
                Vector3.zero,
                false,
                0f,
                initiallyAppliedCharacterId);
        }

        private static void PublishAbilityEvent(
            string sessionId,
            string eventType,
            string casterCharacterId,
            string targetCharacterId,
            string targetEnemySpawnId,
            string abilityId,
            int healAmount,
            Vector3 targetPosition,
            bool hasGroundTarget,
            float castDurationSeconds,
            string initiallyAppliedCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)
                || string.IsNullOrWhiteSpace(casterCharacterId)
                || string.IsNullOrWhiteSpace(eventType))
            {
                return;
            }

            MMOSharedAbilityEvent networkEvent = new()
            {
                eventId = Guid.NewGuid().ToString("N"),
                sessionId = sessionId,
                eventType = eventType,
                casterCharacterId = casterCharacterId,
                targetCharacterId = targetCharacterId ?? string.Empty,
                targetEnemySpawnId = targetEnemySpawnId ?? string.Empty,
                abilityId = abilityId ?? string.Empty,
                healAmount = healAmount,
                targetPosition = new Vector3SaveData(targetPosition),
                hasGroundTarget = hasGroundTarget,
                castDurationSeconds = Mathf.Max(0f, castDurationSeconds),
                createdUtcTicks = DateTime.UtcNow.Ticks
            };
            if (!string.IsNullOrWhiteSpace(initiallyAppliedCharacterId))
            {
                networkEvent.appliedCharacterIds.Add(initiallyAppliedCharacterId);
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.PublishAbilityEvent,
                    abilityEvent = networkEvent
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.abilityEvents.Add(networkEvent);
                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMOSharedAbilityEvent> GetPendingEvents(string sessionId, string observerCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(observerCharacterId))
            {
                return Array.Empty<MMOSharedAbilityEvent>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<MMOSharedAbilityEvent> result = new();
                foreach (MMOSharedAbilityEvent sharedEvent in store.abilityEvents)
                {
                    if (sharedEvent != null
                        && sharedEvent.sessionId == sessionId
                        && !sharedEvent.appliedCharacterIds.Contains(observerCharacterId))
                    {
                        result.Add(Clone(sharedEvent));
                    }
                }

                return result;
            }
        }

        public static void MarkEventApplied(string eventId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.MarkAbilityEventApplied,
                    eventId = eventId,
                    characterId = characterId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedAbilityEvent sharedEvent = store.abilityEvents.Find(candidate => candidate.eventId == eventId);
                if (sharedEvent != null && !sharedEvent.appliedCharacterIds.Contains(characterId))
                {
                    sharedEvent.appliedCharacterIds.Add(characterId);
                    SaveStore(store);
                }
            }
        }

        public static void PublishCombatRequest(CombatActionRequest request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.sessionId)
                || string.IsNullOrWhiteSpace(request.casterCharacterId)
                || string.IsNullOrWhiteSpace(request.abilityId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.PublishCombatRequest,
                    combatRequest = request
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.combatRequests.Add(Clone(request));
                SaveStore(store);
            }
        }

        public static IReadOnlyList<CombatActionRequest> GetPendingCombatRequests(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<CombatActionRequest>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<CombatActionRequest> result = new();
                foreach (CombatActionRequest request in store.combatRequests)
                {
                    if (request != null && request.sessionId == sessionId && !request.processed)
                    {
                        result.Add(Clone(request));
                    }
                }

                return result;
            }
        }

        public static void MarkCombatRequestProcessed(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.MarkCombatRequestProcessed,
                    requestId = requestId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                CombatActionRequest request = store.combatRequests.Find(candidate => candidate.requestId == requestId);
                if (request != null)
                {
                    request.processed = true;
                    SaveStore(store);
                }
            }
        }

        public static void PublishCombatEvent(CombatEventRecord record, string initiallyAppliedCharacterId)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.sessionId) || string.IsNullOrWhiteSpace(record.eventId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.PublishCombatEvent,
                    combatEvent = record,
                    initiallyAppliedCharacterId = initiallyAppliedCharacterId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                MMOSharedCombatEvent sharedEvent = new()
                {
                    record = Clone(record)
                };
                if (!string.IsNullOrWhiteSpace(initiallyAppliedCharacterId))
                {
                    sharedEvent.appliedCharacterIds.Add(initiallyAppliedCharacterId);
                }

                store.combatEvents.Add(sharedEvent);
                SaveStore(store);
            }
        }

        public static IReadOnlyList<CombatEventRecord> GetPendingCombatEvents(string sessionId, string observerCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(observerCharacterId))
            {
                return Array.Empty<CombatEventRecord>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<CombatEventRecord> result = new();
                foreach (MMOSharedCombatEvent sharedEvent in store.combatEvents)
                {
                    if (sharedEvent?.record != null
                        && sharedEvent.record.sessionId == sessionId
                        && !sharedEvent.appliedCharacterIds.Contains(observerCharacterId))
                    {
                        result.Add(Clone(sharedEvent.record));
                    }
                }

                return result;
            }
        }

        public static void MarkCombatEventApplied(string eventId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.MarkCombatEventApplied,
                    eventId = eventId,
                    characterId = characterId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedCombatEvent sharedEvent = store.combatEvents.Find(candidate => candidate?.record != null && candidate.record.eventId == eventId);
                if (sharedEvent != null && !sharedEvent.appliedCharacterIds.Contains(characterId))
                {
                    sharedEvent.appliedCharacterIds.Add(characterId);
                    SaveStore(store);
                }
            }
        }

        public static void PublishExperienceRewardEvent(
            string sessionId,
            string targetCharacterId,
            string enemyDefinitionId,
            int experienceAmount,
            string initiallyAppliedCharacterId)
        {
            PublishRewardEvent(
                sessionId,
                MMOSharedRewardEventTypes.Experience,
                targetCharacterId,
                string.Empty,
                enemyDefinitionId,
                string.Empty,
                Mathf.Max(0, experienceAmount),
                false,
                initiallyAppliedCharacterId);
        }

        public static void PublishQuestKillCreditEvent(
            string sessionId,
            string targetCharacterId,
            string enemySpawnId,
            string enemyDefinitionId,
            string creatureId,
            bool isPartyCredit,
            string initiallyAppliedCharacterId)
        {
            PublishRewardEvent(
                sessionId,
                MMOSharedRewardEventTypes.QuestKillCredit,
                targetCharacterId,
                enemySpawnId,
                enemyDefinitionId,
                creatureId,
                0,
                isPartyCredit,
                initiallyAppliedCharacterId);
        }

        private static void PublishRewardEvent(
            string sessionId,
            string eventType,
            string targetCharacterId,
            string enemySpawnId,
            string enemyDefinitionId,
            string creatureId,
            int experienceAmount,
            bool isPartyCredit,
            string initiallyAppliedCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)
                || string.IsNullOrWhiteSpace(eventType)
                || string.IsNullOrWhiteSpace(targetCharacterId))
            {
                return;
            }

            MMOSharedRewardEvent rewardEvent = new()
            {
                eventId = Guid.NewGuid().ToString("N"),
                sessionId = sessionId,
                eventType = eventType,
                targetCharacterId = targetCharacterId,
                enemySpawnId = enemySpawnId ?? string.Empty,
                enemyDefinitionId = enemyDefinitionId ?? string.Empty,
                creatureId = creatureId ?? string.Empty,
                experienceAmount = Mathf.Max(0, experienceAmount),
                isPartyCredit = isPartyCredit,
                createdUtcTicks = DateTime.UtcNow.Ticks
            };
            if (!string.IsNullOrWhiteSpace(initiallyAppliedCharacterId))
            {
                rewardEvent.appliedCharacterIds.Add(initiallyAppliedCharacterId);
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.PublishRewardEvent,
                    rewardEvent = rewardEvent
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.rewardEvents.Add(rewardEvent);
                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMOSharedRewardEvent> GetPendingRewardEvents(string sessionId, string observerCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(observerCharacterId))
            {
                return Array.Empty<MMOSharedRewardEvent>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<MMOSharedRewardEvent> result = new();
                foreach (MMOSharedRewardEvent rewardEvent in store.rewardEvents)
                {
                    if (rewardEvent != null
                        && rewardEvent.sessionId == sessionId
                        && rewardEvent.targetCharacterId == observerCharacterId
                        && !rewardEvent.appliedCharacterIds.Contains(observerCharacterId))
                    {
                        result.Add(Clone(rewardEvent));
                    }
                }

                return result;
            }
        }

        public static void MarkRewardEventApplied(string eventId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.MarkRewardEventApplied,
                    eventId = eventId,
                    characterId = characterId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedRewardEvent rewardEvent = store.rewardEvents.Find(candidate => candidate != null && candidate.eventId == eventId);
                if (rewardEvent != null && !rewardEvent.appliedCharacterIds.Contains(characterId))
                {
                    rewardEvent.appliedCharacterIds.Add(characterId);
                    SaveStore(store);
                }
            }
        }

        public static void UpsertWorldObjectSnapshot(MMOSharedWorldObjectSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.worldObjectId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertWorldObjectSnapshot,
                    worldObjectSnapshot = snapshot
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                MMOSharedWorldObjectSnapshot existing = store.worldObjectSnapshots.Find(candidate =>
                    candidate != null && candidate.sessionId == snapshot.sessionId && candidate.worldObjectId == snapshot.worldObjectId);
                if (existing == null)
                {
                    store.worldObjectSnapshots.Add(Clone(snapshot));
                }
                else
                {
                    Copy(snapshot, existing);
                }

                SaveStore(store);
            }
        }

        public static void UpsertWorldObjectSnapshots(IEnumerable<MMOSharedWorldObjectSnapshot> snapshots)
        {
            if (snapshots == null)
            {
                return;
            }

            List<MMOSharedWorldObjectSnapshot> snapshotList = new(snapshots);
            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertWorldObjectSnapshots,
                    worldObjectSnapshots = snapshotList
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                bool changed = false;
                foreach (MMOSharedWorldObjectSnapshot snapshot in snapshots)
                {
                    if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.worldObjectId))
                    {
                        continue;
                    }

                    MMOSharedWorldObjectSnapshot existing = store.worldObjectSnapshots.Find(candidate =>
                        candidate != null && candidate.sessionId == snapshot.sessionId && candidate.worldObjectId == snapshot.worldObjectId);
                    if (existing == null)
                    {
                        store.worldObjectSnapshots.Add(Clone(snapshot));
                    }
                    else
                    {
                        Copy(snapshot, existing);
                    }

                    changed = true;
                }

                if (changed)
                {
                    SaveStore(store);
                }
            }
        }

        public static IReadOnlyList<MMOSharedWorldObjectSnapshot> GetWorldObjectSnapshots(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMOSharedWorldObjectSnapshot>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<MMOSharedWorldObjectSnapshot> result = new();
                foreach (MMOSharedWorldObjectSnapshot snapshot in store.worldObjectSnapshots)
                {
                    if (snapshot != null && snapshot.sessionId == sessionId)
                    {
                        result.Add(Clone(snapshot));
                    }
                }

                return result;
            }
        }

        public static void PublishWorldObjectInteractionRequest(MMOSharedWorldObjectInteractionRequest request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.sessionId)
                || string.IsNullOrWhiteSpace(request.worldObjectId)
                || string.IsNullOrWhiteSpace(request.actorCharacterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.PublishWorldObjectInteractionRequest,
                    worldObjectInteractionRequest = request
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.worldObjectInteractionRequests.Add(Clone(request));
                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMOSharedWorldObjectInteractionRequest> GetPendingWorldObjectInteractionRequests(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMOSharedWorldObjectInteractionRequest>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<MMOSharedWorldObjectInteractionRequest> result = new();
                foreach (MMOSharedWorldObjectInteractionRequest request in store.worldObjectInteractionRequests)
                {
                    if (request != null && request.sessionId == sessionId && !request.processed)
                    {
                        result.Add(Clone(request));
                    }
                }

                return result;
            }
        }

        public static void MarkWorldObjectInteractionRequestProcessed(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.MarkWorldObjectInteractionRequestProcessed,
                    requestId = requestId
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                MMOSharedWorldObjectInteractionRequest request = store.worldObjectInteractionRequests.Find(candidate => candidate != null && candidate.requestId == requestId);
                if (request != null)
                {
                    request.processed = true;
                    SaveStore(store);
                }
            }
        }

        public static void UpsertNpcFacingSnapshot(MMONpcFacingSnapshot snapshot)
        {
            if (snapshot == null
                || string.IsNullOrWhiteSpace(snapshot.sessionId)
                || string.IsNullOrWhiteSpace(snapshot.npcInteractionKey)
                || string.IsNullOrWhiteSpace(snapshot.actorCharacterId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertNpcFacingSnapshot,
                    npcFacingSnapshot = snapshot
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                MMONpcFacingSnapshot existing = store.npcFacingSnapshots.Find(candidate =>
                    candidate != null
                    && candidate.sessionId == snapshot.sessionId
                    && candidate.npcInteractionKey == snapshot.npcInteractionKey);
                if (existing == null)
                {
                    store.npcFacingSnapshots.Add(Clone(snapshot));
                }
                else
                {
                    Copy(snapshot, existing);
                }

                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMONpcFacingSnapshot> GetNpcFacingSnapshots(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMONpcFacingSnapshot>();
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                List<MMONpcFacingSnapshot> result = new();
                foreach (MMONpcFacingSnapshot snapshot in store.npcFacingSnapshots)
                {
                    if (snapshot != null && snapshot.sessionId == sessionId)
                    {
                        result.Add(Clone(snapshot));
                    }
                }

                return result;
            }
        }

        public static void UpsertEnemySnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.spawnId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertEnemySnapshot,
                    enemySnapshot = snapshot
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                if (Prune(store))
                {
                    SaveEnemyRuntimeStore(store);
                }

                EnemySnapshot existing = store.enemySnapshots.Find(candidate =>
                    candidate.sessionId == snapshot.sessionId && candidate.spawnId == snapshot.spawnId);
                if (existing == null)
                {
                    store.enemySnapshots.Add(Clone(snapshot));
                }
                else
                {
                    Copy(snapshot, existing);
                }

                SaveEnemyRuntimeStore(store);
            }
        }

        public static void UpsertEnemySnapshots(IEnumerable<EnemySnapshot> snapshots)
        {
            if (snapshots == null)
            {
                return;
            }

            List<EnemySnapshot> snapshotList = new(snapshots);
            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertEnemySnapshots,
                    enemySnapshots = snapshotList
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                if (Prune(store))
                {
                    SaveEnemyRuntimeStore(store);
                }

                bool changed = false;
                foreach (EnemySnapshot snapshot in snapshots)
                {
                    if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.spawnId))
                    {
                        continue;
                    }

                    EnemySnapshot existing = store.enemySnapshots.Find(candidate =>
                        candidate.sessionId == snapshot.sessionId && candidate.spawnId == snapshot.spawnId);
                    if (existing == null)
                    {
                        store.enemySnapshots.Add(Clone(snapshot));
                    }
                    else
                    {
                        Copy(snapshot, existing);
                    }

                    changed = true;
                }

                if (changed)
                {
                    SaveEnemyRuntimeStore(store);
                }
            }
        }

        public static IReadOnlyList<EnemySnapshot> GetEnemySnapshots(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<EnemySnapshot>();
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                if (Prune(store))
                {
                    SaveEnemyRuntimeStore(store);
                }

                List<EnemySnapshot> result = new();
                foreach (EnemySnapshot snapshot in store.enemySnapshots)
                {
                    if (snapshot != null && snapshot.sessionId == sessionId)
                    {
                        result.Add(Clone(snapshot));
                    }
                }

                return result;
            }
        }

        public static void UpsertCorpseLootSnapshot(MMOCorpseLootState snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.enemySpawnId))
            {
                return;
            }

            if (MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.UpsertCorpseLootSnapshot,
                    corpseLootSnapshot = snapshot
                }))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                if (Prune(store))
                {
                    SaveEnemyRuntimeStore(store);
                }

                MMOCorpseLootState existing = store.corpseLootSnapshots.Find(candidate =>
                    candidate != null && candidate.sessionId == snapshot.sessionId && candidate.enemySpawnId == snapshot.enemySpawnId);
                if (existing == null)
                {
                    store.corpseLootSnapshots.Add(Clone(snapshot));
                }
                else
                {
                    Copy(snapshot, existing);
                }

                SaveEnemyRuntimeStore(store);
            }
        }

        public static bool TryApplyPersonalLootUpdate(
            MMOCorpseLootState proposedSnapshot,
            string characterId,
            out MMOCorpseLootState authoritativeSnapshot)
        {
            authoritativeSnapshot = null;
            if (proposedSnapshot == null
                || string.IsNullOrWhiteSpace(proposedSnapshot.sessionId)
                || string.IsNullOrWhiteSpace(proposedSnapshot.enemySpawnId)
                || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                MMOCorpseLootState existing = store.corpseLootSnapshots.Find(candidate =>
                    candidate != null
                    && candidate.sessionId == proposedSnapshot.sessionId
                    && candidate.enemySpawnId == proposedSnapshot.enemySpawnId);
                MMOPersonalLootState proposedPersonalLoot = proposedSnapshot.personalLoot?.Find(candidate =>
                    candidate != null && candidate.characterId == characterId);
                MMOPersonalLootState existingPersonalLoot = existing?.personalLoot?.Find(candidate =>
                    candidate != null && candidate.characterId == characterId);
                if (existing == null
                    || proposedPersonalLoot == null
                    || existingPersonalLoot == null
                    || !IsValidLootDepletion(existingPersonalLoot, proposedPersonalLoot))
                {
                    return false;
                }

                MMOPersonalLootState acceptedPersonalLoot = Clone(proposedPersonalLoot);
                acceptedPersonalLoot.characterId = existingPersonalLoot.characterId;
                acceptedPersonalLoot.participantId = existingPersonalLoot.participantId;
                acceptedPersonalLoot.looted = !acceptedPersonalLoot.HasLoot;
                int personalLootIndex = existing.personalLoot.IndexOf(existingPersonalLoot);
                existing.personalLoot[personalLootIndex] = acceptedPersonalLoot;
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                SaveEnemyRuntimeStore(store);
                authoritativeSnapshot = Clone(existing);
                return true;
            }
        }

        public static IReadOnlyList<MMOCorpseLootState> GetCorpseLootSnapshots(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Array.Empty<MMOCorpseLootState>();
            }

            using (AcquireStateLease())
            {
                MMOSharedEnemyRuntimeStore store = LoadEnemyRuntimeStore();
                if (Prune(store))
                {
                    SaveEnemyRuntimeStore(store);
                }

                List<MMOCorpseLootState> result = new();
                foreach (MMOCorpseLootState snapshot in store.corpseLootSnapshots)
                {
                    if (snapshot != null && snapshot.sessionId == sessionId)
                    {
                        result.Add(Clone(snapshot));
                    }
                }

                return result;
            }
        }

        private static IDisposable AcquireStateLease()
        {
            Monitor.Enter(Gate);
            try
            {
                return new StateLease();
            }
            catch
            {
                Monitor.Exit(Gate);
                throw;
            }
        }

        private static MMOSharedSessionStore LoadStore()
        {
            return sharedState;
        }

        private static MMOSharedSessionRuntimeStore LoadRuntimeStore()
        {
            return participantRuntimeState;
        }

        private static MMOSharedEnemyRuntimeStore LoadEnemyRuntimeStore()
        {
            return worldRuntimeState;
        }

        private static void SaveStore(MMOSharedSessionStore store)
        {
            sharedState = store ?? new MMOSharedSessionStore();
        }

        private static void SaveRuntimeStore(MMOSharedSessionRuntimeStore store)
        {
            participantRuntimeState = store ?? new MMOSharedSessionRuntimeStore();
        }

        private static void SaveEnemyRuntimeStore(MMOSharedEnemyRuntimeStore store)
        {
            worldRuntimeState = store ?? new MMOSharedEnemyRuntimeStore();
        }

        public static string CreateNetworkSnapshotJson()
        {
            MMOSharedSessionNetworkSnapshot snapshot = new()
            {
                sharedStoreJson = JsonUtility.ToJson(LoadStore(), false),
                runtimeStoreJson = JsonUtility.ToJson(LoadRuntimeStore(), false),
                enemyRuntimeStoreJson = JsonUtility.ToJson(LoadEnemyRuntimeStore(), false)
            };
            return JsonUtility.ToJson(snapshot, false);
        }

        public static void ApplyNetworkSnapshot(string snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                return;
            }

            MMOSharedSessionNetworkSnapshot snapshot = JsonUtility.FromJson<MMOSharedSessionNetworkSnapshot>(snapshotJson);
            if (snapshot == null)
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore previousSharedStore = LoadStore();
                MMOSharedSessionRuntimeStore previousRuntimeStore = LoadRuntimeStore();
                MMOSessionParticipantSnapshot localParticipant = CreateLocalParticipantSnapshot(previousSharedStore);
                MMOSessionParticipantRuntimeSnapshot localRuntime = CreateLocalParticipantRuntimeSnapshot(previousRuntimeStore);

                MMOSharedSessionStore sharedStore = string.IsNullOrWhiteSpace(snapshot.sharedStoreJson)
                    ? new MMOSharedSessionStore()
                    : JsonUtility.FromJson<MMOSharedSessionStore>(snapshot.sharedStoreJson) ?? new MMOSharedSessionStore();
                MMOSharedSessionRuntimeStore runtimeStore = string.IsNullOrWhiteSpace(snapshot.runtimeStoreJson)
                    ? new MMOSharedSessionRuntimeStore()
                    : JsonUtility.FromJson<MMOSharedSessionRuntimeStore>(snapshot.runtimeStoreJson) ?? new MMOSharedSessionRuntimeStore();
                MMOSharedEnemyRuntimeStore worldRuntimeStore = string.IsNullOrWhiteSpace(snapshot.enemyRuntimeStoreJson)
                    ? new MMOSharedEnemyRuntimeStore()
                    : JsonUtility.FromJson<MMOSharedEnemyRuntimeStore>(snapshot.enemyRuntimeStoreJson) ?? new MMOSharedEnemyRuntimeStore();
                NormalizeSnapshotRetentionTimestamps(sharedStore, runtimeStore, worldRuntimeStore);
                string authoritativeSessionId = ResolveAuthoritativeSessionId(sharedStore, runtimeStore);
                PreserveLocalParticipant(sharedStore, runtimeStore, localParticipant, localRuntime, authoritativeSessionId);

                sharedState = sharedStore;
                participantRuntimeState = runtimeStore;
                worldRuntimeState = worldRuntimeStore;
            }
        }

        private static MMOSessionParticipantSnapshot CreateLocalParticipantSnapshot(MMOSharedSessionStore previousStore)
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return null;
            }

            string localCharacterId = MMOCharacterSession.SelectedCharacter.characterId;
            if (string.IsNullOrWhiteSpace(localCharacterId))
            {
                return null;
            }

            MMOSessionParticipantSnapshot previous = previousStore?.participants.Find(candidate =>
                candidate != null && candidate.characterId == localCharacterId);
            if (previous != null)
            {
                return CloneSerializable(previous);
            }

            MMOCharacterSaveData characterData = CaptureLocalCharacterData(localCharacterId);
            if (characterData == null)
            {
                return null;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            return new MMOSessionParticipantSnapshot
            {
                participantId = MMOGameplaySessionService.LocalPlayer.ParticipantId,
                characterId = localCharacterId,
                accountId = characterData.accountId,
                sessionId = MMOGameplaySessionService.SessionId,
                sceneName = characterData.sceneName,
                isHost = MMOGameplaySessionService.IsHostAuthority,
                updatedUtcTicks = nowTicks,
                runtimeUtcTicks = nowTicks,
                characterData = characterData
            };
        }

        private static MMOSessionParticipantRuntimeSnapshot CreateLocalParticipantRuntimeSnapshot(MMOSharedSessionRuntimeStore previousStore)
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return null;
            }

            string localCharacterId = MMOCharacterSession.SelectedCharacter.characterId;
            if (string.IsNullOrWhiteSpace(localCharacterId))
            {
                return null;
            }

            MMOSessionParticipantRuntimeSnapshot previous = previousStore?.participants.Find(candidate =>
                candidate != null && candidate.characterId == localCharacterId);
            if (previous != null)
            {
                return CloneSerializable(previous);
            }

            GameObject localPlayer = MMOGameplaySessionService.LocalPlayer.PlayerObject;
            if (localPlayer == null || !localPlayer.TryGetComponent(out MMOCharacterIdentity identity))
            {
                return null;
            }

            return new MMOSessionParticipantRuntimeSnapshot
            {
                sessionId = MMOGameplaySessionService.SessionId,
                characterId = localCharacterId,
                position = new Vector3SaveData(localPlayer.transform.position),
                rotationEuler = new Vector3SaveData(localPlayer.transform.eulerAngles),
                currentHealth = identity.Health.CurrentValue,
                currentMana = identity.Mana.CurrentValue,
                updatedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        private static MMOCharacterSaveData CaptureLocalCharacterData(string localCharacterId)
        {
            GameObject localPlayer = MMOGameplaySessionService.LocalPlayer.PlayerObject;
            if (localPlayer != null
                && localPlayer.TryGetComponent(out MMOCharacterPersistenceAgent persistenceAgent)
                && !persistenceAgent.IsRemoteSessionReplica)
            {
                MMOCharacterSaveData captured = persistenceAgent.CaptureCurrentCharacterData();
                if (captured != null && captured.characterId == localCharacterId)
                {
                    return captured;
                }
            }

            return CloneSerializable(MMOCharacterSession.SelectedCharacter);
        }

        private static void PreserveLocalParticipant(
            MMOSharedSessionStore sharedStore,
            MMOSharedSessionRuntimeStore runtimeStore,
            MMOSessionParticipantSnapshot localParticipant,
            MMOSessionParticipantRuntimeSnapshot localRuntime,
            string authoritativeSessionId)
        {
            if (localParticipant == null || string.IsNullOrWhiteSpace(localParticipant.characterId))
            {
                return;
            }

            string sessionId = string.IsNullOrWhiteSpace(authoritativeSessionId)
                ? MMOGameplaySessionService.SessionId
                : authoritativeSessionId;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                localParticipant.sessionId = sessionId;
                if (localRuntime != null)
                {
                    localRuntime.sessionId = sessionId;
                }
            }

            int participantIndex = sharedStore.participants.FindIndex(candidate =>
                candidate != null && candidate.characterId == localParticipant.characterId);
            if (participantIndex >= 0)
            {
                sharedStore.participants[participantIndex] = localParticipant;
            }
            else
            {
                sharedStore.participants.Add(localParticipant);
            }

            if (localRuntime == null)
            {
                return;
            }

            int runtimeIndex = runtimeStore.participants.FindIndex(candidate =>
                candidate != null && candidate.characterId == localRuntime.characterId);
            if (runtimeIndex >= 0)
            {
                runtimeStore.participants[runtimeIndex] = localRuntime;
            }
            else
            {
                runtimeStore.participants.Add(localRuntime);
            }
        }

        private static string ResolveAuthoritativeSessionId(
            MMOSharedSessionStore sharedStore,
            MMOSharedSessionRuntimeStore runtimeStore)
        {
            foreach (MMOSessionParticipantSnapshot participant in sharedStore.participants)
            {
                if (!string.IsNullOrWhiteSpace(participant?.sessionId))
                {
                    return participant.sessionId;
                }
            }

            foreach (MMOSessionParticipantRuntimeSnapshot participant in runtimeStore.participants)
            {
                if (!string.IsNullOrWhiteSpace(participant?.sessionId))
                {
                    return participant.sessionId;
                }
            }

            return MMOGameplaySessionService.SessionId;
        }

        private static T CloneSerializable<T>(T value) where T : class
        {
            return value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value, false));
        }

        public static void ApplyNetworkOperation(string operationJson)
        {
            if (string.IsNullOrWhiteSpace(operationJson))
            {
                return;
            }

            MMOSharedSessionNetworkOperation operation = JsonUtility.FromJson<MMOSharedSessionNetworkOperation>(operationJson);
            if (operation == null || string.IsNullOrWhiteSpace(operation.kind))
            {
                return;
            }

            switch (operation.kind)
            {
                case MMOSharedSessionNetworkOperationKind.UpsertParticipant:
                    UpsertParticipant(operation.participant);
                    break;
                case MMOSharedSessionNetworkOperationKind.RemoveParticipant:
                    RemoveParticipant(operation.sessionId, operation.characterId);
                    break;
                case MMOSharedSessionNetworkOperationKind.PublishAbilityEvent:
                    AddAbilityEvent(operation.abilityEvent);
                    break;
                case MMOSharedSessionNetworkOperationKind.MarkAbilityEventApplied:
                    MarkEventApplied(operation.eventId, operation.characterId);
                    break;
                case MMOSharedSessionNetworkOperationKind.PublishCombatRequest:
                    PublishCombatRequest(operation.combatRequest);
                    break;
                case MMOSharedSessionNetworkOperationKind.MarkCombatRequestProcessed:
                    MarkCombatRequestProcessed(operation.requestId);
                    break;
                case MMOSharedSessionNetworkOperationKind.PublishCombatEvent:
                    PublishCombatEvent(operation.combatEvent, operation.initiallyAppliedCharacterId);
                    break;
                case MMOSharedSessionNetworkOperationKind.MarkCombatEventApplied:
                    MarkCombatEventApplied(operation.eventId, operation.characterId);
                    break;
                case MMOSharedSessionNetworkOperationKind.PublishRewardEvent:
                    AddRewardEvent(operation.rewardEvent);
                    break;
                case MMOSharedSessionNetworkOperationKind.MarkRewardEventApplied:
                    MarkRewardEventApplied(operation.eventId, operation.characterId);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertWorldObjectSnapshot:
                    UpsertWorldObjectSnapshot(operation.worldObjectSnapshot);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertWorldObjectSnapshots:
                    UpsertWorldObjectSnapshots(operation.worldObjectSnapshots);
                    break;
                case MMOSharedSessionNetworkOperationKind.PublishWorldObjectInteractionRequest:
                    PublishWorldObjectInteractionRequest(operation.worldObjectInteractionRequest);
                    break;
                case MMOSharedSessionNetworkOperationKind.MarkWorldObjectInteractionRequestProcessed:
                    MarkWorldObjectInteractionRequestProcessed(operation.requestId);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertNpcFacingSnapshot:
                    UpsertNpcFacingSnapshot(operation.npcFacingSnapshot);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertEnemySnapshot:
                    UpsertEnemySnapshot(operation.enemySnapshot);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertEnemySnapshots:
                    UpsertEnemySnapshots(operation.enemySnapshots);
                    break;
                case MMOSharedSessionNetworkOperationKind.UpsertCorpseLootSnapshot:
                    UpsertCorpseLootSnapshot(operation.corpseLootSnapshot);
                    break;
            }
        }

        private static void AddAbilityEvent(MMOSharedAbilityEvent abilityEvent)
        {
            if (abilityEvent == null || string.IsNullOrWhiteSpace(abilityEvent.sessionId) || string.IsNullOrWhiteSpace(abilityEvent.eventId))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.abilityEvents.Add(Clone(abilityEvent));
                SaveStore(store);
            }
        }

        private static void AddRewardEvent(MMOSharedRewardEvent rewardEvent)
        {
            if (rewardEvent == null || string.IsNullOrWhiteSpace(rewardEvent.sessionId) || string.IsNullOrWhiteSpace(rewardEvent.eventId))
            {
                return;
            }

            using (AcquireStateLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                store.rewardEvents.Add(Clone(rewardEvent));
                SaveStore(store);
            }
        }

        private static bool Prune(MMOSharedSessionStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            int removedParticipants = store.participants.RemoveAll(participant =>
                participant == null
                || IsExpired(participant.updatedUtcTicks, now, ParticipantTimeout));
            int removedEvents = store.abilityEvents.RemoveAll(sharedEvent =>
                sharedEvent == null
                || IsExpired(sharedEvent.createdUtcTicks, now, EventTimeout));
            int removedCombatRequests = store.combatRequests.RemoveAll(request =>
                request == null
                || IsExpired(request.requestedUtcTicks, now, request.processed ? CombatRequestTimeout : ParticipantTimeout));
            int removedCombatEvents = store.combatEvents.RemoveAll(sharedEvent =>
                sharedEvent?.record == null
                || IsExpired(sharedEvent.record.createdUtcTicks, now, EventTimeout));
            int removedRewardEvents = store.rewardEvents.RemoveAll(rewardEvent =>
                rewardEvent == null
                || IsExpired(rewardEvent.createdUtcTicks, now, EventTimeout));
            int removedWorldObjectSnapshots = store.worldObjectSnapshots.RemoveAll(snapshot =>
                snapshot == null
                || IsExpired(snapshot.updatedUtcTicks, now, WorldObjectSnapshotTimeout));
            int removedWorldObjectRequests = store.worldObjectInteractionRequests.RemoveAll(request =>
                request == null
                || IsExpired(request.requestedUtcTicks, now, request.processed ? WorldObjectRequestTimeout : ParticipantTimeout));
            return removedParticipants > 0
                || removedEvents > 0
                || removedCombatRequests > 0
                || removedCombatEvents > 0
                || removedRewardEvents > 0
                || removedWorldObjectSnapshots > 0
                || removedWorldObjectRequests > 0;
        }

        private static bool Prune(MMOSharedSessionRuntimeStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            return store.participants.RemoveAll(participant =>
                participant == null
                || IsExpired(participant.updatedUtcTicks, now, ParticipantTimeout)) > 0;
        }

        private static bool Prune(MMOSharedEnemyRuntimeStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            int removedEnemySnapshots = store.enemySnapshots.RemoveAll(snapshot =>
                snapshot == null
                || IsExpired(snapshot.updatedUtcTicks, now, EnemySnapshotTimeout));
            int removedCorpseSnapshots = store.corpseLootSnapshots.RemoveAll(snapshot =>
                snapshot == null
                || IsExpired(snapshot.updatedUtcTicks, now, CorpseLootSnapshotTimeout));
            return removedEnemySnapshots > 0 || removedCorpseSnapshots > 0;
        }

        private static bool IsExpired(long timestampUtcTicks, long nowUtcTicks, TimeSpan timeout)
        {
            if (timestampUtcTicks <= 0)
            {
                return true;
            }

            return nowUtcTicks >= timestampUtcTicks
                && nowUtcTicks - timestampUtcTicks > timeout.Ticks;
        }

        private static void NormalizeSnapshotRetentionTimestamps(
            MMOSharedSessionStore sharedStore,
            MMOSharedSessionRuntimeStore runtimeStore,
            MMOSharedEnemyRuntimeStore worldRuntimeStore)
        {
            long receivedUtcTicks = DateTime.UtcNow.Ticks;
            if (sharedStore != null)
            {
                foreach (MMOSharedAbilityEvent abilityEvent in sharedStore.abilityEvents)
                {
                    if (abilityEvent != null)
                    {
                        abilityEvent.createdUtcTicks = receivedUtcTicks;
                    }
                }

                foreach (CombatActionRequest request in sharedStore.combatRequests)
                {
                    if (request != null)
                    {
                        request.requestedUtcTicks = receivedUtcTicks;
                    }
                }

                foreach (MMOSharedCombatEvent combatEvent in sharedStore.combatEvents)
                {
                    if (combatEvent?.record != null)
                    {
                        combatEvent.record.createdUtcTicks = receivedUtcTicks;
                    }
                }

                foreach (MMOSharedRewardEvent rewardEvent in sharedStore.rewardEvents)
                {
                    if (rewardEvent != null)
                    {
                        rewardEvent.createdUtcTicks = receivedUtcTicks;
                    }
                }

                foreach (MMOSharedWorldObjectSnapshot worldObjectSnapshot in sharedStore.worldObjectSnapshots)
                {
                    if (worldObjectSnapshot != null)
                    {
                        worldObjectSnapshot.updatedUtcTicks = receivedUtcTicks;
                    }
                }

                foreach (MMOSharedWorldObjectInteractionRequest request in sharedStore.worldObjectInteractionRequests)
                {
                    if (request != null)
                    {
                        request.requestedUtcTicks = receivedUtcTicks;
                    }
                }
            }

            if (runtimeStore == null)
            {
                return;
            }

            foreach (MMOSessionParticipantRuntimeSnapshot participant in runtimeStore.participants)
            {
                if (participant != null)
                {
                    participant.updatedUtcTicks = receivedUtcTicks;
                }
            }

            if (worldRuntimeStore == null)
            {
                return;
            }

            foreach (MMOCorpseLootState corpseLoot in worldRuntimeStore.corpseLootSnapshots)
            {
                if (corpseLoot != null)
                {
                    corpseLoot.updatedUtcTicks = receivedUtcTicks;
                }
            }
        }

        private static bool IsValidLootDepletion(MMOPersonalLootState existing, MMOPersonalLootState proposed)
        {
            if (existing == null || proposed == null || existing.characterId != proposed.characterId)
            {
                return false;
            }

            if (proposed.items != null && proposed.items.Exists(item =>
                    item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity < 0))
            {
                return false;
            }

            Dictionary<string, int> existingQuantities = GetLootQuantities(existing);
            Dictionary<string, int> proposedQuantities = GetLootQuantities(proposed);
            foreach (KeyValuePair<string, int> pair in proposedQuantities)
            {
                if (!existingQuantities.TryGetValue(pair.Key, out int existingQuantity)
                    || pair.Value < 0
                    || pair.Value > existingQuantity)
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, int> GetLootQuantities(MMOPersonalLootState state)
        {
            Dictionary<string, int> quantities = new(StringComparer.Ordinal);
            if (state?.items == null)
            {
                return quantities;
            }

            foreach (MMOPersonalLootItemState item in state.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity < 0)
                {
                    continue;
                }

                quantities.TryGetValue(item.itemId, out int currentQuantity);
                quantities[item.itemId] = currentQuantity + item.quantity;
            }

            return quantities;
        }

        private static void UpsertParticipantRuntimeInLease(
            string sessionId,
            string characterId,
            Vector3 position,
            Vector3 rotationEuler,
            int currentHealth,
            int currentMana)
        {
            MMOSharedSessionRuntimeStore store = LoadRuntimeStore();
            if (Prune(store))
            {
                SaveRuntimeStore(store);
            }

            MMOSessionParticipantRuntimeSnapshot existing = store.participants.Find(candidate =>
                candidate.sessionId == sessionId && candidate.characterId == characterId);
            if (existing == null)
            {
                existing = new MMOSessionParticipantRuntimeSnapshot
                {
                    sessionId = sessionId,
                    characterId = characterId
                };
                store.participants.Add(existing);
            }
            else if (IsSameRuntimeSnapshot(existing, position, rotationEuler, currentHealth, currentMana))
            {
                return;
            }

            existing.position = new Vector3SaveData(position);
            existing.rotationEuler = new Vector3SaveData(rotationEuler);
            existing.currentHealth = currentHealth;
            existing.currentMana = currentMana;
            existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
            SaveRuntimeStore(store);
        }

        private static bool IsSameRuntimeSnapshot(
            MMOSessionParticipantRuntimeSnapshot snapshot,
            Vector3 position,
            Vector3 rotationEuler,
            int currentHealth,
            int currentMana)
        {
            if (snapshot == null || currentHealth != snapshot.currentHealth || currentMana != snapshot.currentMana)
            {
                return false;
            }

            Vector3 previousPosition = snapshot.position.ToVector3();
            Vector3 previousRotation = snapshot.rotationEuler.ToVector3();
            return (previousPosition - position).sqrMagnitude < 0.0004f
                && Mathf.Abs(Mathf.DeltaAngle(previousRotation.x, rotationEuler.x)) < 0.25f
                && Mathf.Abs(Mathf.DeltaAngle(previousRotation.y, rotationEuler.y)) < 0.25f
                && Mathf.Abs(Mathf.DeltaAngle(previousRotation.z, rotationEuler.z)) < 0.25f;
        }

        private static void ApplyRuntimeSnapshot(MMOSessionParticipantSnapshot participant, MMOSharedSessionRuntimeStore runtimeStore)
        {
            if (participant == null || participant.characterData == null || runtimeStore == null)
            {
                return;
            }

            MMOSessionParticipantRuntimeSnapshot runtime = runtimeStore.participants.Find(candidate =>
                candidate.sessionId == participant.sessionId && candidate.characterId == participant.characterId);
            if (runtime == null)
            {
                return;
            }

            participant.characterData.position = runtime.position;
            participant.characterData.rotationEuler = runtime.rotationEuler;
            participant.characterData.currentHealth = runtime.currentHealth;
            participant.characterData.currentMana = runtime.currentMana;
            participant.runtimeUtcTicks = runtime.updatedUtcTicks;
        }

        private static MMOSessionParticipantRuntimeSnapshot Clone(MMOSessionParticipantRuntimeSnapshot source)
        {
            return source == null
                ? null
                : new MMOSessionParticipantRuntimeSnapshot
                {
                    sessionId = source.sessionId,
                    characterId = source.characterId,
                    position = source.position,
                    rotationEuler = source.rotationEuler,
                    currentHealth = source.currentHealth,
                    currentMana = source.currentMana,
                    updatedUtcTicks = source.updatedUtcTicks
                };
        }

        private static MMOSessionParticipantSnapshot Clone(MMOSessionParticipantSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            MMOSessionParticipantSnapshot clone = new();
            CopyParticipant(source, clone);
            return clone;
        }

        private static MMOSharedAbilityEvent Clone(MMOSharedAbilityEvent source)
        {
            return source == null
                ? null
                : new MMOSharedAbilityEvent
                {
                    eventId = source.eventId,
                    sessionId = source.sessionId,
                    eventType = string.IsNullOrWhiteSpace(source.eventType)
                        ? MMOSharedAbilityEventTypes.HealResolved
                        : source.eventType,
                    casterCharacterId = source.casterCharacterId,
                    targetCharacterId = source.targetCharacterId,
                    targetEnemySpawnId = source.targetEnemySpawnId,
                    abilityId = source.abilityId,
                    healAmount = source.healAmount,
                    targetPosition = source.targetPosition,
                    hasGroundTarget = source.hasGroundTarget,
                    castDurationSeconds = source.castDurationSeconds,
                    createdUtcTicks = source.createdUtcTicks,
                    appliedCharacterIds = new List<string>(source.appliedCharacterIds ?? new List<string>())
                };
        }

        private static CombatActionRequest Clone(CombatActionRequest source)
        {
            return source == null
                ? null
                : new CombatActionRequest
                {
                    requestId = source.requestId,
                    sessionId = source.sessionId,
                    requesterCharacterId = source.requesterCharacterId,
                    casterCharacterId = source.casterCharacterId,
                    targetCharacterId = source.targetCharacterId,
                    targetEnemySpawnId = source.targetEnemySpawnId,
                    abilityId = source.abilityId,
                    requestedTargetPosition = source.requestedTargetPosition,
                    hasGroundTarget = source.hasGroundTarget,
                    requestKind = source.requestKind,
                    requestedUtcTicks = source.requestedUtcTicks,
                    processed = source.processed
                };
        }

        private static CombatEventRecord Clone(CombatEventRecord source)
        {
            return source == null
                ? null
                : new CombatEventRecord
                {
                    eventId = source.eventId,
                    sessionId = source.sessionId,
                    eventType = source.eventType,
                    sourceCharacterId = source.sourceCharacterId,
                    targetCharacterId = source.targetCharacterId,
                    sourceEnemySpawnId = source.sourceEnemySpawnId,
                    targetEnemySpawnId = source.targetEnemySpawnId,
                    abilityId = source.abilityId,
                    targetPosition = source.targetPosition,
                    hasGroundTarget = source.hasGroundTarget,
                    castDurationSeconds = source.castDurationSeconds,
                    damageAmount = source.damageAmount,
                    healAmount = source.healAmount,
                    blockedAmount = source.blockedAmount,
                    absorbedAsManaAmount = source.absorbedAsManaAmount,
                    hasTargetResourceSnapshot = source.hasTargetResourceSnapshot,
                    targetCurrentHealth = source.targetCurrentHealth,
                    targetMaxHealth = source.targetMaxHealth,
                    targetCurrentMana = source.targetCurrentMana,
                    targetMaxMana = source.targetMaxMana,
                    isCritical = source.isCritical,
                    killedTarget = source.killedTarget,
                    createdUtcTicks = source.createdUtcTicks
                };
        }

        private static MMOSharedRewardEvent Clone(MMOSharedRewardEvent source)
        {
            return source == null
                ? null
                : new MMOSharedRewardEvent
                {
                    eventId = source.eventId,
                    sessionId = source.sessionId,
                    eventType = source.eventType,
                    targetCharacterId = source.targetCharacterId,
                    enemySpawnId = source.enemySpawnId,
                    enemyDefinitionId = source.enemyDefinitionId,
                    creatureId = source.creatureId,
                    experienceAmount = source.experienceAmount,
                    isPartyCredit = source.isPartyCredit,
                    createdUtcTicks = source.createdUtcTicks,
                    appliedCharacterIds = new List<string>(source.appliedCharacterIds ?? new List<string>())
                };
        }

        private static MMOSharedWorldObjectSnapshot Clone(MMOSharedWorldObjectSnapshot source)
        {
            return source == null
                ? null
                : new MMOSharedWorldObjectSnapshot
                {
                    sessionId = source.sessionId,
                    worldObjectId = source.worldObjectId,
                    available = source.available,
                    respawnRemainingSeconds = source.respawnRemainingSeconds,
                    updatedUtcTicks = source.updatedUtcTicks
                };
        }

        private static void Copy(MMOSharedWorldObjectSnapshot source, MMOSharedWorldObjectSnapshot destination)
        {
            destination.sessionId = source.sessionId;
            destination.worldObjectId = source.worldObjectId;
            destination.available = source.available;
            destination.respawnRemainingSeconds = source.respawnRemainingSeconds;
            destination.updatedUtcTicks = source.updatedUtcTicks;
        }

        private static MMONpcFacingSnapshot Clone(MMONpcFacingSnapshot source)
        {
            return source == null
                ? null
                : new MMONpcFacingSnapshot
                {
                    sessionId = source.sessionId,
                    npcInteractionKey = source.npcInteractionKey,
                    actorCharacterId = source.actorCharacterId,
                    actorPosition = source.actorPosition,
                    updatedUtcTicks = source.updatedUtcTicks
                };
        }

        private static void Copy(MMONpcFacingSnapshot source, MMONpcFacingSnapshot destination)
        {
            destination.sessionId = source.sessionId;
            destination.npcInteractionKey = source.npcInteractionKey;
            destination.actorCharacterId = source.actorCharacterId;
            destination.actorPosition = source.actorPosition;
            destination.updatedUtcTicks = source.updatedUtcTicks;
        }

        private static MMOSharedWorldObjectInteractionRequest Clone(MMOSharedWorldObjectInteractionRequest source)
        {
            return source == null
                ? null
                : new MMOSharedWorldObjectInteractionRequest
                {
                    requestId = source.requestId,
                    sessionId = source.sessionId,
                    worldObjectId = source.worldObjectId,
                    actorCharacterId = source.actorCharacterId,
                    requestedUtcTicks = source.requestedUtcTicks,
                    processed = source.processed
                };
        }

        private static EnemySnapshot Clone(EnemySnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            EnemySnapshot clone = new();
            Copy(source, clone);
            return clone;
        }

        private static void Copy(EnemySnapshot source, EnemySnapshot destination)
        {
            destination.sessionId = source.sessionId;
            destination.spawnId = source.spawnId;
            destination.definitionId = source.definitionId;
            destination.displayName = source.displayName;
            destination.runtimeState = source.runtimeState;
            destination.currentHealth = source.currentHealth;
            destination.maxHealth = source.maxHealth;
            destination.currentMana = source.currentMana;
            destination.maxMana = source.maxMana;
            destination.position = source.position;
            destination.rotationEuler = source.rotationEuler;
            destination.worldSpeed = source.worldSpeed;
            destination.currentTargetCharacterId = source.currentTargetCharacterId;
            destination.inCombat = source.inCombat;
            destination.leashing = source.leashing;
            destination.leashAnchorPosition = source.leashAnchorPosition;
            destination.castAbilityId = source.castAbilityId;
            destination.castTargetCharacterId = source.castTargetCharacterId;
            destination.castDurationSeconds = source.castDurationSeconds;
            destination.castNormalizedProgress = source.castNormalizedProgress;
            destination.corpseRemainingSeconds = source.corpseRemainingSeconds;
            destination.respawnRemainingSeconds = source.respawnRemainingSeconds;
            destination.updatedUtcTicks = source.updatedUtcTicks;
        }

        private static MMOCorpseLootState Clone(MMOCorpseLootState source)
        {
            if (source == null)
            {
                return null;
            }

            MMOCorpseLootState clone = new();
            Copy(source, clone);
            return clone;
        }

        private static void Copy(MMOCorpseLootState source, MMOCorpseLootState destination)
        {
            destination.sessionId = source.sessionId;
            destination.corpseId = source.corpseId;
            destination.enemySpawnId = source.enemySpawnId;
            destination.updatedUtcTicks = source.updatedUtcTicks;
            destination.personalLoot = new List<MMOPersonalLootState>();
            if (source.personalLoot == null)
            {
                return;
            }

            foreach (MMOPersonalLootState personalState in source.personalLoot)
            {
                destination.personalLoot.Add(Clone(personalState));
            }
        }

        private static MMOPersonalLootState Clone(MMOPersonalLootState source)
        {
            if (source == null)
            {
                return null;
            }

            MMOPersonalLootState clone = new()
            {
                characterId = source.characterId,
                participantId = source.participantId,
                looted = source.looted,
                items = new List<MMOPersonalLootItemState>()
            };
            if (source.items != null)
            {
                foreach (MMOPersonalLootItemState item in source.items)
                {
                    clone.items.Add(item == null
                        ? null
                        : new MMOPersonalLootItemState
                        {
                            itemId = item.itemId,
                            quantity = item.quantity
                        });
                }
            }

            return clone;
        }

        private static void CopyParticipant(MMOSessionParticipantSnapshot source, MMOSessionParticipantSnapshot destination)
        {
            destination.participantId = source.participantId;
            destination.characterId = source.characterId;
            destination.accountId = source.accountId;
            destination.sessionId = source.sessionId;
            destination.sceneName = source.sceneName;
            destination.isHost = source.isHost;
            destination.updatedUtcTicks = source.updatedUtcTicks;
            destination.runtimeUtcTicks = source.runtimeUtcTicks;
            destination.characterData = CloneCharacter(source.characterData);
        }

        private static MMOCharacterSaveData CloneCharacter(MMOCharacterSaveData source)
        {
            if (source == null)
            {
                return new MMOCharacterSaveData();
            }

            return new MMOCharacterSaveData
            {
                characterId = source.characterId,
                accountId = source.accountId,
                characterName = source.characterName,
                normalizedCharacterName = source.normalizedCharacterName,
                race = source.race,
                characterClass = source.characterClass,
                headStyleId = source.headStyleId,
                faceId = source.faceId,
                hairstyleId = source.hairstyleId,
                hairColorId = source.hairColorId,
                level = source.level,
                currentExperience = source.currentExperience,
                totalExperienceEarned = source.totalExperienceEarned,
                currentHealth = source.currentHealth,
                currentMana = source.currentMana,
                sceneName = source.sceneName,
                position = source.position,
                rotationEuler = source.rotationEuler,
                copper = source.copper,
                inventory = CloneInventorySlots(source.inventory),
                equippedBagItemIds = source.equippedBagItemIds != null ? new List<string>(source.equippedBagItemIds) : new List<string>(),
                equipment = CloneEquipmentSlots(source.equipment),
                weaponSkills = CloneWeaponSkills(source.weaponSkills),
                learnedAbilityIds = source.learnedAbilityIds != null ? new List<string>(source.learnedAbilityIds) : new List<string>(),
                actionBarSlots = CloneActionBarSlots(source.actionBarSlots),
                activeQuests = CloneQuestStates(source.activeQuests),
                completedQuestIds = source.completedQuestIds != null ? new List<string>(source.completedQuestIds) : new List<string>(),
                pendingUsableItemId = source.pendingUsableItemId
            };
        }

        private static List<MMOInventorySlotSaveData> CloneInventorySlots(List<MMOInventorySlotSaveData> source)
        {
            List<MMOInventorySlotSaveData> result = new(source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            foreach (MMOInventorySlotSaveData slot in source)
            {
                result.Add(slot == null
                    ? null
                    : new MMOInventorySlotSaveData
                    {
                        slotIndex = slot.slotIndex,
                        itemId = slot.itemId,
                        quantity = slot.quantity
                    });
            }

            return result;
        }

        private static List<MMOEquipmentSlotSaveData> CloneEquipmentSlots(List<MMOEquipmentSlotSaveData> source)
        {
            List<MMOEquipmentSlotSaveData> result = new(source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            foreach (MMOEquipmentSlotSaveData slot in source)
            {
                result.Add(slot == null
                    ? null
                    : new MMOEquipmentSlotSaveData
                    {
                        slotType = slot.slotType,
                        itemId = slot.itemId
                    });
            }

            return result;
        }

        private static List<MMOWeaponSkillSaveEntry> CloneWeaponSkills(List<MMOWeaponSkillSaveEntry> source)
        {
            List<MMOWeaponSkillSaveEntry> result = new(source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            foreach (MMOWeaponSkillSaveEntry skill in source)
            {
                result.Add(skill == null
                    ? null
                    : new MMOWeaponSkillSaveEntry
                    {
                        weaponType = skill.weaponType,
                        skillValue = skill.skillValue
                    });
            }

            return result;
        }

        private static List<MMOActionBarSlotSaveData> CloneActionBarSlots(List<MMOActionBarSlotSaveData> source)
        {
            List<MMOActionBarSlotSaveData> result = new(source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            foreach (MMOActionBarSlotSaveData slot in source)
            {
                result.Add(slot == null
                    ? null
                    : new MMOActionBarSlotSaveData
                    {
                        slotIndex = slot.slotIndex,
                        bindingType = slot.bindingType,
                        abilityId = slot.abilityId,
                        itemId = slot.itemId,
                        key = slot.key
                    });
            }

            return result;
        }

        private static List<MMOQuestStateSaveData> CloneQuestStates(List<MMOQuestStateSaveData> source)
        {
            List<MMOQuestStateSaveData> result = new(source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            foreach (MMOQuestStateSaveData quest in source)
            {
                result.Add(quest == null
                    ? null
                    : new MMOQuestStateSaveData
                    {
                        questId = quest.questId,
                        tracked = quest.tracked,
                        objectiveProgress = quest.objectiveProgress != null ? new List<int>(quest.objectiveProgress) : new List<int>()
                    });
            }

            return result;
        }

        private sealed class StateLease : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Monitor.Exit(Gate);
            }
        }

        [Serializable]
        private sealed class MMOSharedSessionStore
        {
            public List<MMOSessionParticipantSnapshot> participants = new();
            public List<MMOSharedAbilityEvent> abilityEvents = new();
            public List<CombatActionRequest> combatRequests = new();
            public List<MMOSharedCombatEvent> combatEvents = new();
            public List<MMOSharedRewardEvent> rewardEvents = new();
            public List<MMOSharedWorldObjectSnapshot> worldObjectSnapshots = new();
            public List<MMOSharedWorldObjectInteractionRequest> worldObjectInteractionRequests = new();
            public List<MMONpcFacingSnapshot> npcFacingSnapshots = new();
        }

        [Serializable]
        private sealed class MMOSharedCombatEvent
        {
            public CombatEventRecord record;
            public List<string> appliedCharacterIds = new();
        }

        [Serializable]
        private sealed class MMOSharedSessionRuntimeStore
        {
            public List<MMOSessionParticipantRuntimeSnapshot> participants = new();
        }

        [Serializable]
        private sealed class MMOSharedEnemyRuntimeStore
        {
            public List<EnemySnapshot> enemySnapshots = new();
            public List<MMOCorpseLootState> corpseLootSnapshots = new();
        }

        [Serializable]
        private sealed class MMOSharedSessionNetworkSnapshot
        {
            public string sharedStoreJson;
            public string runtimeStoreJson;
            public string enemyRuntimeStoreJson;
        }
    }
}
