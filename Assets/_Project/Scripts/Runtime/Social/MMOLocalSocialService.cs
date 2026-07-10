using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGClone.Social
{
    public sealed class MMOLocalSocialService :
        ICharacterNameDirectory,
        IFriendListService,
        ICharacterPresenceService,
        ISessionPresenceService,
        IInviteService
    {
        private const string FileName = "rpg_clone_social_directory.json";
        private const string StoreMutexName = "RPGClone_LocalSocialDirectory";
        private static readonly TimeSpan StoreMutexTimeout = TimeSpan.FromSeconds(5);
        private static readonly object Gate = new();
        private readonly string path;

        public MMOLocalSocialService()
        {
            path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public Task<MMOServiceResult> RegisterOrUpdateAsync(MMOCharacterNameRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.characterId))
            {
                return Task.FromResult(MMOServiceResult.Failure("Character record is incomplete."));
            }

            if (!MMOCharacterNameUtility.TryValidate(record.characterName, out string displayName, out string normalizedName, out string error))
            {
                return Task.FromResult(MMOServiceResult.Failure(error));
            }

            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterNameRecord duplicate = store.characterDirectory.Find(existing =>
                    existing != null
                    && existing.normalizedCharacterName == normalizedName
                    && existing.characterId != record.characterId);
                if (duplicate != null)
                {
                    return Task.FromResult(MMOServiceResult.Failure($"{displayName} is already taken."));
                }

                MMOCharacterNameRecord existing = store.characterDirectory.Find(candidate => candidate.characterId == record.characterId);
                if (existing != null
                    && !string.IsNullOrWhiteSpace(existing.playerId)
                    && !string.IsNullOrWhiteSpace(record.playerId)
                    && !string.Equals(existing.playerId, record.playerId, StringComparison.Ordinal))
                {
                    return Task.FromResult(MMOServiceResult.Failure("Character ownership does not match the current account."));
                }

                if (existing == null)
                {
                    existing = new MMOCharacterNameRecord();
                    store.characterDirectory.Add(existing);
                }

                existing.playerId = record.playerId ?? string.Empty;
                existing.characterId = record.characterId;
                existing.characterName = displayName;
                existing.normalizedCharacterName = normalizedName;
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                SaveStore(store);
            }

            return Task.FromResult(MMOServiceResult.Success($"{displayName} registered."));
        }

        public Task<MMOCharacterNameRecord> FindByNameAsync(string characterName)
        {
            string normalizedName = MMOCharacterNameUtility.NormalizeLookupName(characterName);
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                return Task.FromResult(Clone(store.characterDirectory.Find(record => record.normalizedCharacterName == normalizedName)));
            }
        }

        public Task<MMOCharacterNameRecord> FindByCharacterIdAsync(string characterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                return Task.FromResult(Clone(store.characterDirectory.Find(record => record.characterId == characterId)));
            }
        }

        public Task<IReadOnlyList<MMOFriendEntry>> GetFriendsAsync(string ownerCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOFriendListSaveData list = GetFriendList(store, ownerCharacterId, false);
                IReadOnlyList<MMOFriendEntry> result = list == null
                    ? Array.Empty<MMOFriendEntry>()
                    : CloneFriends(list.friends);
                return Task.FromResult(result);
            }
        }

        public Task<MMOServiceResult> AddFriendByNameAsync(string ownerCharacterId, string ownerCharacterName, string friendCharacterName)
        {
            if (string.IsNullOrWhiteSpace(ownerCharacterId))
            {
                return Task.FromResult(MMOServiceResult.Failure("Select a character before adding friends."));
            }

            string normalizedOwner = MMOCharacterNameUtility.NormalizeLookupName(ownerCharacterName);
            string normalizedFriend = MMOCharacterNameUtility.NormalizeLookupName(friendCharacterName);
            if (string.IsNullOrWhiteSpace(normalizedFriend))
            {
                return Task.FromResult(MMOServiceResult.Failure("Enter a character name."));
            }

            if (normalizedFriend == normalizedOwner)
            {
                return Task.FromResult(MMOServiceResult.Failure("You cannot add yourself."));
            }

            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterNameRecord friend = store.characterDirectory.Find(record => record.normalizedCharacterName == normalizedFriend);
                if (friend == null)
                {
                    return Task.FromResult(MMOServiceResult.Failure("Character not found."));
                }

                if (friend.characterId == ownerCharacterId)
                {
                    return Task.FromResult(MMOServiceResult.Failure("You cannot add yourself."));
                }

                MMOFriendListSaveData list = GetFriendList(store, ownerCharacterId, true);
                if (list.friends.Exists(entry => entry.characterId == friend.characterId))
                {
                    return Task.FromResult(MMOServiceResult.Failure($"{friend.characterName} is already on your friends list."));
                }

                list.friends.Add(new MMOFriendEntry
                {
                    characterId = friend.characterId,
                    characterName = friend.characterName,
                    normalizedCharacterName = friend.normalizedCharacterName,
                    addedUtcTicks = DateTime.UtcNow.Ticks
                });
                SaveStore(store);
                return Task.FromResult(MMOServiceResult.Success($"{friend.characterName} added."));
            }
        }

        public Task<MMOServiceResult> RemoveFriendAsync(string ownerCharacterId, string friendCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOFriendListSaveData list = GetFriendList(store, ownerCharacterId, false);
                if (list == null || list.friends.RemoveAll(entry => entry.characterId == friendCharacterId) == 0)
                {
                    return Task.FromResult(MMOServiceResult.Failure("Friend entry was not found."));
                }

                SaveStore(store);
                return Task.FromResult(MMOServiceResult.Success("Friend removed."));
            }
        }

        public Task UpdatePresenceAsync(MMOCharacterPresenceRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.characterId))
            {
                return Task.CompletedTask;
            }

            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterPresenceRecord existing = store.presenceRecords.Find(candidate => candidate.characterId == record.characterId);
                if (existing == null)
                {
                    existing = new MMOCharacterPresenceRecord();
                    store.presenceRecords.Add(existing);
                }

                CopyPresence(record, existing);
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                SaveStore(store);
            }

            return Task.CompletedTask;
        }

        public Task<MMOCharacterPresenceRecord> GetPresenceAsync(string characterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterPresenceRecord presence = Clone(store.presenceRecords.Find(record => record.characterId == characterId));
                if (presence != null && IsStale(presence.updatedUtcTicks))
                {
                    presence.status = MMOCharacterPresenceStatus.Offline;
                    presence.joinsAllowed = false;
                    presence.sessionId = string.Empty;
                }

                return Task.FromResult(presence);
            }
        }

        public Task SetOfflineAsync(string characterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterPresenceRecord existing = store.presenceRecords.Find(record => record.characterId == characterId);
                if (existing != null)
                {
                    existing.status = MMOCharacterPresenceStatus.Offline;
                    existing.joinsAllowed = false;
                    existing.sessionId = string.Empty;
                    existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                    SaveStore(store);
                }
            }

            return Task.CompletedTask;
        }

        public Task AdvertiseSessionAsync(MMOSessionPresenceRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.sessionId))
            {
                return Task.CompletedTask;
            }

            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOSessionPresenceRecord existing = store.sessions.Find(session => session.sessionId == record.sessionId);
                if (existing == null)
                {
                    existing = new MMOSessionPresenceRecord { createdUtcTicks = DateTime.UtcNow.Ticks };
                    store.sessions.Add(existing);
                }

                CopySession(record, existing);
                existing.updatedUtcTicks = DateTime.UtcNow.Ticks;
                if (existing.createdUtcTicks <= 0)
                {
                    existing.createdUtcTicks = existing.updatedUtcTicks;
                }

                SaveStore(store);
            }

            return Task.CompletedTask;
        }

        public Task<MMOSessionPresenceRecord> GetSessionAsync(string sessionId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                return Task.FromResult(Clone(store.sessions.Find(session => session.sessionId == sessionId && !IsStale(session.updatedUtcTicks))));
            }
        }

        public Task<MMOSessionPresenceRecord> GetHostedSessionForCharacterAsync(string characterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                return Task.FromResult(Clone(store.sessions.Find(session => session.hostCharacterId == characterId && session.joinsAllowed && !IsStale(session.updatedUtcTicks))));
            }
        }

        public Task ClearHostedSessionAsync(string hostCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                if (store.sessions.RemoveAll(session => session.hostCharacterId == hostCharacterId) > 0)
                {
                    SaveStore(store);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MMOInviteRecord>> GetIncomingInvitesAsync(string characterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                List<MMOInviteRecord> invites = new();
                foreach (MMOInviteRecord invite in store.invites)
                {
                    if (invite != null && invite.targetCharacterId == characterId && invite.status == MMOInviteStatus.Pending && !IsStale(invite.updatedUtcTicks))
                    {
                        invites.Add(Clone(invite));
                    }
                }

                return Task.FromResult((IReadOnlyList<MMOInviteRecord>)invites);
            }
        }

        public Task<MMOServiceResult> SendInviteAsync(string senderCharacterId, string targetCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOCharacterNameRecord sender = store.characterDirectory.Find(record => record.characterId == senderCharacterId);
                MMOCharacterNameRecord target = store.characterDirectory.Find(record => record.characterId == targetCharacterId);
                if (sender == null || target == null)
                {
                    return Task.FromResult(MMOServiceResult.Failure("Invite target could not be resolved."));
                }

                MMOCharacterPresenceRecord targetPresence = store.presenceRecords.Find(record => record.characterId == targetCharacterId);
                if (targetPresence == null || targetPresence.status == MMOCharacterPresenceStatus.Offline || IsStale(targetPresence.updatedUtcTicks))
                {
                    return Task.FromResult(MMOServiceResult.Failure($"{target.characterName} is offline."));
                }

                MMOSessionPresenceRecord session = store.sessions.Find(record => record.hostCharacterId == senderCharacterId && record.joinsAllowed && !IsStale(record.updatedUtcTicks));
                if (session == null)
                {
                    return Task.FromResult(MMOServiceResult.Failure("You are not advertising a joinable session."));
                }

                MMOInviteRecord existing = store.invites.Find(invite =>
                    invite.senderCharacterId == senderCharacterId
                    && invite.targetCharacterId == targetCharacterId
                    && invite.sessionId == session.sessionId
                    && invite.status == MMOInviteStatus.Pending);
                if (existing != null)
                {
                    return Task.FromResult(MMOServiceResult.Failure($"{target.characterName} already has a pending invite."));
                }

                long now = DateTime.UtcNow.Ticks;
                store.invites.Add(new MMOInviteRecord
                {
                    inviteId = Guid.NewGuid().ToString("N"),
                    senderPlayerId = sender.playerId,
                    senderCharacterId = sender.characterId,
                    senderCharacterName = sender.characterName,
                    targetCharacterId = target.characterId,
                    targetCharacterName = target.characterName,
                    sessionId = session.sessionId,
                    status = MMOInviteStatus.Pending,
                    createdUtcTicks = now,
                    updatedUtcTicks = now
                });
                SaveStore(store);
                return Task.FromResult(MMOServiceResult.Success($"Invite sent to {target.characterName}."));
            }
        }

        public Task<MMOInviteResolution> AcceptInviteAsync(string inviteId, string targetCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOInviteRecord invite = store.invites.Find(record => record.inviteId == inviteId && record.targetCharacterId == targetCharacterId);
                if (invite == null || invite.status != MMOInviteStatus.Pending)
                {
                    return Task.FromResult(new MMOInviteResolution(false, "Invite is no longer available.", null));
                }

                MMOSessionPresenceRecord session = store.sessions.Find(record => record.sessionId == invite.sessionId && record.joinsAllowed && !IsStale(record.updatedUtcTicks));
                if (session == null)
                {
                    invite.status = MMOInviteStatus.Expired;
                    invite.updatedUtcTicks = DateTime.UtcNow.Ticks;
                    SaveStore(store);
                    return Task.FromResult(new MMOInviteResolution(false, "Session is no longer joinable.", null));
                }

                invite.status = MMOInviteStatus.Accepted;
                invite.updatedUtcTicks = DateTime.UtcNow.Ticks;

                MMOCharacterPresenceRecord targetPresence = store.presenceRecords.Find(record => record.characterId == targetCharacterId);
                if (targetPresence != null)
                {
                    targetPresence.status = MMOCharacterPresenceStatus.JoiningSession;
                    targetPresence.sessionId = session.sessionId;
                    targetPresence.updatedUtcTicks = DateTime.UtcNow.Ticks;
                }

                SaveStore(store);
                return Task.FromResult(new MMOInviteResolution(true, $"Invite accepted; session {session.sessionId} resolved.", Clone(session)));
            }
        }

        public Task<MMOServiceResult> DeclineInviteAsync(string inviteId, string targetCharacterId)
        {
            using (AcquireStoreLease())
            {
                MMOLocalSocialStore store = LoadStore();
                MMOInviteRecord invite = store.invites.Find(record => record.inviteId == inviteId && record.targetCharacterId == targetCharacterId);
                if (invite == null || invite.status != MMOInviteStatus.Pending)
                {
                    return Task.FromResult(MMOServiceResult.Failure("Invite is no longer available."));
                }

                invite.status = MMOInviteStatus.Declined;
                invite.updatedUtcTicks = DateTime.UtcNow.Ticks;
                SaveStore(store);
                return Task.FromResult(MMOServiceResult.Success("Invite declined."));
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
                Debug.LogWarning("Social directory store lock timed out; continuing with in-process synchronization.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Social directory store lock unavailable; continuing with in-process synchronization. {exception.Message}");
            }

            return null;
        }

        private MMOLocalSocialStore LoadStore()
        {
            if (!File.Exists(path))
            {
                return new MMOLocalSocialStore();
            }

            try
            {
                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json)
                    ? new MMOLocalSocialStore()
                    : JsonUtility.FromJson<MMOLocalSocialStore>(json) ?? new MMOLocalSocialStore();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Social directory load failed; using empty local directory. {exception.Message}");
                return new MMOLocalSocialStore();
            }
        }

        private void SaveStore(MMOLocalSocialStore store)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(store ?? new MMOLocalSocialStore(), true));
        }

        private static MMOFriendListSaveData GetFriendList(MMOLocalSocialStore store, string ownerCharacterId, bool create)
        {
            MMOFriendListSaveData list = store.friendLists.Find(candidate => candidate.ownerCharacterId == ownerCharacterId);
            if (list == null && create)
            {
                list = new MMOFriendListSaveData { ownerCharacterId = ownerCharacterId };
                store.friendLists.Add(list);
            }

            list?.friends.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.characterId));
            return list;
        }

        private static bool IsStale(long utcTicks)
        {
            if (utcTicks <= 0)
            {
                return true;
            }

            return DateTime.UtcNow - new DateTime(utcTicks, DateTimeKind.Utc) > TimeSpan.FromMinutes(5);
        }

        private static IReadOnlyList<MMOFriendEntry> CloneFriends(List<MMOFriendEntry> source)
        {
            List<MMOFriendEntry> result = new();
            foreach (MMOFriendEntry entry in source ?? new List<MMOFriendEntry>())
            {
                result.Add(Clone(entry));
            }

            return result;
        }

        private static MMOCharacterNameRecord Clone(MMOCharacterNameRecord source)
        {
            return source == null
                ? null
                : new MMOCharacterNameRecord
                {
                    playerId = source.playerId,
                    characterId = source.characterId,
                    characterName = source.characterName,
                    normalizedCharacterName = source.normalizedCharacterName,
                    updatedUtcTicks = source.updatedUtcTicks
                };
        }

        private static MMOFriendEntry Clone(MMOFriendEntry source)
        {
            return source == null
                ? null
                : new MMOFriendEntry
                {
                    characterId = source.characterId,
                    characterName = source.characterName,
                    normalizedCharacterName = source.normalizedCharacterName,
                    addedUtcTicks = source.addedUtcTicks
                };
        }

        private static MMOCharacterPresenceRecord Clone(MMOCharacterPresenceRecord source)
        {
            if (source == null)
            {
                return null;
            }

            MMOCharacterPresenceRecord clone = new();
            CopyPresence(source, clone);
            return clone;
        }

        private static MMOSessionPresenceRecord Clone(MMOSessionPresenceRecord source)
        {
            if (source == null)
            {
                return null;
            }

            MMOSessionPresenceRecord clone = new();
            CopySession(source, clone);
            return clone;
        }

        private static MMOInviteRecord Clone(MMOInviteRecord source)
        {
            return source == null
                ? null
                : new MMOInviteRecord
                {
                    inviteId = source.inviteId,
                    senderPlayerId = source.senderPlayerId,
                    senderCharacterId = source.senderCharacterId,
                    senderCharacterName = source.senderCharacterName,
                    targetCharacterId = source.targetCharacterId,
                    targetCharacterName = source.targetCharacterName,
                    sessionId = source.sessionId,
                    status = source.status,
                    createdUtcTicks = source.createdUtcTicks,
                    updatedUtcTicks = source.updatedUtcTicks
                };
        }

        private static void CopyPresence(MMOCharacterPresenceRecord source, MMOCharacterPresenceRecord destination)
        {
            destination.playerId = source.playerId;
            destination.characterId = source.characterId;
            destination.characterName = source.characterName;
            destination.normalizedCharacterName = source.normalizedCharacterName;
            destination.status = source.status;
            destination.sessionId = source.sessionId;
            destination.currentSceneName = source.currentSceneName;
            destination.joinsAllowed = source.joinsAllowed;
            destination.updatedUtcTicks = source.updatedUtcTicks;
        }

        private static void CopySession(MMOSessionPresenceRecord source, MMOSessionPresenceRecord destination)
        {
            destination.hostPlayerId = source.hostPlayerId;
            destination.hostCharacterId = source.hostCharacterId;
            destination.hostCharacterName = source.hostCharacterName;
            destination.sessionId = source.sessionId;
            destination.currentSceneName = source.currentSceneName;
            destination.capacity = source.capacity;
            destination.participantCount = source.participantCount;
            destination.joinsAllowed = source.joinsAllowed;
            destination.createdUtcTicks = source.createdUtcTicks;
            destination.updatedUtcTicks = source.updatedUtcTicks;
            destination.privateConnectionData = source.privateConnectionData;
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
        private sealed class MMOLocalSocialStore
        {
            public List<MMOCharacterNameRecord> characterDirectory = new();
            public List<MMOFriendListSaveData> friendLists = new();
            public List<MMOCharacterPresenceRecord> presenceRecords = new();
            public List<MMOSessionPresenceRecord> sessions = new();
            public List<MMOInviteRecord> invites = new();
        }
    }
}
