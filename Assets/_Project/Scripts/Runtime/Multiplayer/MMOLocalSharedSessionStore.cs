using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RPGClone.CharacterSelection;
using RPGClone.Combat;
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
        public const string AbilityReleased = "ability_released";
        public const string AutoAttackWindup = "auto_attack_windup";
        public const string HealResolved = "heal_resolved";
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

    public static class MMOLocalSharedSessionStore
    {
        private const string FileName = "rpg_clone_shared_sessions.json";
        private const string RuntimeFileName = "rpg_clone_shared_session_runtime.json";
        private const string EnemyRuntimeFileName = "rpg_clone_shared_session_enemies.json";
        private const string StoreMutexName = "RPGClone_LocalSharedSessions";
        private static readonly TimeSpan StoreMutexTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CombatRequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan EnemySnapshotTimeout = TimeSpan.FromSeconds(10);
        private static readonly object Gate = new();

        public static void UpsertParticipant(MMOSessionParticipantSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.characterId))
            {
                return;
            }

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

        public static void PublishCastStartedEvent(
            string sessionId,
            string casterCharacterId,
            string targetCharacterId,
            string abilityId,
            float castDurationSeconds,
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
                Vector3.zero,
                false,
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

            using (AcquireStoreLease())
            {
                MMOSharedSessionStore store = LoadStore();
                if (Prune(store))
                {
                    SaveStore(store);
                }

                MMOSharedAbilityEvent sharedEvent = new()
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
                    sharedEvent.appliedCharacterIds.Add(initiallyAppliedCharacterId);
                }

                store.abilityEvents.Add(sharedEvent);
                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMOSharedAbilityEvent> GetPendingEvents(string sessionId, string observerCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(observerCharacterId))
            {
                return Array.Empty<MMOSharedAbilityEvent>();
            }

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

        public static void UpsertEnemySnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || string.IsNullOrWhiteSpace(snapshot.spawnId))
            {
                return;
            }

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

            using (AcquireStoreLease())
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

        private static IDisposable AcquireStoreLease()
        {
            Monitor.Enter(Gate);
            try
            {
                return new StoreLease(TryAcquireStoreMutex());
            }
            catch
            {
                Monitor.Exit(Gate);
                throw;
            }
        }

        private static Mutex TryAcquireStoreMutex()
        {
            try
            {
                Mutex mutex = new(false, StoreMutexName);
                try
                {
                    if (mutex.WaitOne(StoreMutexTimeout))
                    {
                        return mutex;
                    }
                }
                catch (AbandonedMutexException)
                {
                    return mutex;
                }

                mutex.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Shared session store lock unavailable; continuing with in-process synchronization. {exception.Message}");
            }

            return null;
        }

        private static MMOSharedSessionStore LoadStore()
        {
            string path = StorePath;
            if (!File.Exists(path))
            {
                return new MMOSharedSessionStore();
            }

            try
            {
                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json)
                    ? new MMOSharedSessionStore()
                    : JsonUtility.FromJson<MMOSharedSessionStore>(json) ?? new MMOSharedSessionStore();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Shared session store load failed; using empty local store. {exception.Message}");
                return new MMOSharedSessionStore();
            }
        }

        private static MMOSharedSessionRuntimeStore LoadRuntimeStore()
        {
            string path = RuntimeStorePath;
            if (!File.Exists(path))
            {
                return new MMOSharedSessionRuntimeStore();
            }

            try
            {
                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json)
                    ? new MMOSharedSessionRuntimeStore()
                    : JsonUtility.FromJson<MMOSharedSessionRuntimeStore>(json) ?? new MMOSharedSessionRuntimeStore();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Shared session runtime store load failed; using empty local store. {exception.Message}");
                return new MMOSharedSessionRuntimeStore();
            }
        }

        private static MMOSharedEnemyRuntimeStore LoadEnemyRuntimeStore()
        {
            string path = EnemyRuntimeStorePath;
            if (!File.Exists(path))
            {
                return new MMOSharedEnemyRuntimeStore();
            }

            try
            {
                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json)
                    ? new MMOSharedEnemyRuntimeStore()
                    : JsonUtility.FromJson<MMOSharedEnemyRuntimeStore>(json) ?? new MMOSharedEnemyRuntimeStore();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Shared enemy runtime store load failed; using empty local store. {exception.Message}");
                return new MMOSharedEnemyRuntimeStore();
            }
        }

        private static void SaveStore(MMOSharedSessionStore store)
        {
            string path = StorePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(store ?? new MMOSharedSessionStore(), false));
        }

        private static void SaveRuntimeStore(MMOSharedSessionRuntimeStore store)
        {
            string path = RuntimeStorePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(store ?? new MMOSharedSessionRuntimeStore(), false));
        }

        private static void SaveEnemyRuntimeStore(MMOSharedEnemyRuntimeStore store)
        {
            string path = EnemyRuntimeStorePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(store ?? new MMOSharedEnemyRuntimeStore(), false));
        }

        private static string StorePath => Path.Combine(Application.persistentDataPath, FileName);
        private static string RuntimeStorePath => Path.Combine(Application.persistentDataPath, RuntimeFileName);
        private static string EnemyRuntimeStorePath => Path.Combine(Application.persistentDataPath, EnemyRuntimeFileName);

        private static bool Prune(MMOSharedSessionStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            int removedParticipants = store.participants.RemoveAll(participant =>
                participant == null
                || participant.updatedUtcTicks <= 0
                || new TimeSpan(now - participant.updatedUtcTicks) > ParticipantTimeout);
            int removedEvents = store.abilityEvents.RemoveAll(sharedEvent =>
                sharedEvent == null
                || sharedEvent.createdUtcTicks <= 0
                || new TimeSpan(now - sharedEvent.createdUtcTicks) > EventTimeout);
            int removedCombatRequests = store.combatRequests.RemoveAll(request =>
                request == null
                || request.requestedUtcTicks <= 0
                || (request.processed && new TimeSpan(now - request.requestedUtcTicks) > CombatRequestTimeout)
                || new TimeSpan(now - request.requestedUtcTicks) > ParticipantTimeout);
            int removedCombatEvents = store.combatEvents.RemoveAll(sharedEvent =>
                sharedEvent?.record == null
                || sharedEvent.record.createdUtcTicks <= 0
                || new TimeSpan(now - sharedEvent.record.createdUtcTicks) > EventTimeout);
            return removedParticipants > 0 || removedEvents > 0 || removedCombatRequests > 0 || removedCombatEvents > 0;
        }

        private static bool Prune(MMOSharedSessionRuntimeStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            return store.participants.RemoveAll(participant =>
                participant == null
                || participant.updatedUtcTicks <= 0
                || new TimeSpan(now - participant.updatedUtcTicks) > ParticipantTimeout) > 0;
        }

        private static bool Prune(MMOSharedEnemyRuntimeStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            return store.enemySnapshots.RemoveAll(snapshot =>
                snapshot == null
                || snapshot.updatedUtcTicks <= 0
                || new TimeSpan(now - snapshot.updatedUtcTicks) > EnemySnapshotTimeout) > 0;
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

            existing.position = new Vector3SaveData(position);
            existing.rotationEuler = new Vector3SaveData(rotationEuler);
            existing.currentHealth = currentHealth;
            existing.currentMana = currentMana;
            existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
            SaveRuntimeStore(store);
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
                    damageAmount = source.damageAmount,
                    healAmount = source.healAmount,
                    blockedAmount = source.blockedAmount,
                    isCritical = source.isCritical,
                    killedTarget = source.killedTarget,
                    createdUtcTicks = source.createdUtcTicks
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
            destination.currentTargetCharacterId = source.currentTargetCharacterId;
            destination.inCombat = source.inCombat;
            destination.leashing = source.leashing;
            destination.corpseRemainingSeconds = source.corpseRemainingSeconds;
            destination.respawnRemainingSeconds = source.respawnRemainingSeconds;
            destination.updatedUtcTicks = source.updatedUtcTicks;
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

            string json = JsonUtility.ToJson(source);
            return JsonUtility.FromJson<MMOCharacterSaveData>(json) ?? new MMOCharacterSaveData();
        }

        private sealed class StoreLease : IDisposable
        {
            private readonly Mutex mutex;
            private bool disposed;

            public StoreLease(Mutex mutex)
            {
                this.mutex = mutex;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (mutex != null)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                    }

                    mutex.Dispose();
                }

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
        }
    }
}
