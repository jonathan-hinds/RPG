using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RPGClone.CharacterSelection;
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
        public MMOCharacterSaveData characterData = new();
    }

    [Serializable]
    public sealed class MMOSharedAbilityEvent
    {
        public string eventId;
        public string sessionId;
        public string casterCharacterId;
        public string targetCharacterId;
        public string abilityId;
        public int healAmount;
        public long createdUtcTicks;
        public List<string> appliedCharacterIds = new();
    }

    public static class MMOLocalSharedSessionStore
    {
        private const string FileName = "rpg_clone_shared_sessions.json";
        private const string StoreMutexName = "RPGClone_LocalSharedSessions";
        private static readonly TimeSpan StoreMutexTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(12);
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
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
                Prune(store);
                MMOSessionParticipantSnapshot existing = store.participants.Find(candidate =>
                    candidate.sessionId == snapshot.sessionId && candidate.characterId == snapshot.characterId);
                if (existing == null)
                {
                    existing = new MMOSessionParticipantSnapshot();
                    store.participants.Add(existing);
                }

                CopyParticipant(snapshot, existing);
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                SaveStore(store);
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
                if (store.participants.RemoveAll(candidate => candidate.sessionId == sessionId && candidate.characterId == characterId) > 0)
                {
                    SaveStore(store);
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
                Prune(store);
                List<MMOSessionParticipantSnapshot> result = new();
                foreach (MMOSessionParticipantSnapshot participant in store.participants)
                {
                    if (participant != null && participant.sessionId == sessionId)
                    {
                        result.Add(Clone(participant));
                    }
                }

                SaveStore(store);
                return result;
            }
        }

        public static void PublishHealEvent(string sessionId, string casterCharacterId, string targetCharacterId, string abilityId, int healAmount)
        {
            if (string.IsNullOrWhiteSpace(sessionId)
                || string.IsNullOrWhiteSpace(casterCharacterId)
                || string.IsNullOrWhiteSpace(targetCharacterId)
                || healAmount <= 0)
            {
                return;
            }

            using (AcquireStoreLease())
            {
                MMOSharedSessionStore store = LoadStore();
                Prune(store);
                store.abilityEvents.Add(new MMOSharedAbilityEvent
                {
                    eventId = Guid.NewGuid().ToString("N"),
                    sessionId = sessionId,
                    casterCharacterId = casterCharacterId,
                    targetCharacterId = targetCharacterId,
                    abilityId = abilityId ?? string.Empty,
                    healAmount = healAmount,
                    createdUtcTicks = DateTime.UtcNow.Ticks
                });
                SaveStore(store);
            }
        }

        public static IReadOnlyList<MMOSharedAbilityEvent> GetPendingEvents(string sessionId, string targetCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(targetCharacterId))
            {
                return Array.Empty<MMOSharedAbilityEvent>();
            }

            using (AcquireStoreLease())
            {
                MMOSharedSessionStore store = LoadStore();
                Prune(store);
                List<MMOSharedAbilityEvent> result = new();
                foreach (MMOSharedAbilityEvent sharedEvent in store.abilityEvents)
                {
                    if (sharedEvent != null
                        && sharedEvent.sessionId == sessionId
                        && sharedEvent.targetCharacterId == targetCharacterId
                        && !sharedEvent.appliedCharacterIds.Contains(targetCharacterId))
                    {
                        result.Add(Clone(sharedEvent));
                    }
                }

                SaveStore(store);
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

        private static void SaveStore(MMOSharedSessionStore store)
        {
            string path = StorePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(store ?? new MMOSharedSessionStore(), true));
        }

        private static string StorePath => Path.Combine(Application.persistentDataPath, FileName);

        private static void Prune(MMOSharedSessionStore store)
        {
            long now = DateTime.UtcNow.Ticks;
            store.participants.RemoveAll(participant =>
                participant == null
                || participant.updatedUtcTicks <= 0
                || new TimeSpan(now - participant.updatedUtcTicks) > ParticipantTimeout);
            store.abilityEvents.RemoveAll(sharedEvent =>
                sharedEvent == null
                || sharedEvent.createdUtcTicks <= 0
                || new TimeSpan(now - sharedEvent.createdUtcTicks) > EventTimeout);
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
                    casterCharacterId = source.casterCharacterId,
                    targetCharacterId = source.targetCharacterId,
                    abilityId = source.abilityId,
                    healAmount = source.healAmount,
                    createdUtcTicks = source.createdUtcTicks,
                    appliedCharacterIds = new List<string>(source.appliedCharacterIds ?? new List<string>())
                };
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
        }
    }
}
