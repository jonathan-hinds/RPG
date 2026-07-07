using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGClone.Social
{
    public sealed class MMORuntimeSocialService :
        ICharacterNameDirectory,
        IFriendListService,
        ICharacterPresenceService,
        ISessionPresenceService,
        IInviteService
    {
        private readonly Dictionary<string, MMOCharacterNameRecord> charactersById = new();
        private readonly Dictionary<string, MMOCharacterNameRecord> charactersByName = new();
        private readonly Dictionary<string, MMOCharacterPresenceRecord> presenceByCharacterId = new();
        private readonly Dictionary<string, MMOSessionPresenceRecord> sessionsById = new();
        private readonly Dictionary<string, string> sessionIdByHostCharacterId = new();

        public Task<MMOServiceResult> RegisterOrUpdateAsync(MMOCharacterNameRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.characterId))
            {
                return Task.FromResult(MMOServiceResult.Failure("Character record is invalid."));
            }

            charactersById[record.characterId] = Clone(record);
            if (!string.IsNullOrWhiteSpace(record.normalizedCharacterName))
            {
                charactersByName[record.normalizedCharacterName] = Clone(record);
            }

            return Task.FromResult(MMOServiceResult.Success("Character registered."));
        }

        public Task<MMOCharacterNameRecord> FindByNameAsync(string characterName)
        {
            string normalized = MMOCharacterNameUtility.NormalizeLookupName(characterName);
            return Task.FromResult(charactersByName.TryGetValue(normalized, out MMOCharacterNameRecord record) ? Clone(record) : null);
        }

        public Task<MMOCharacterNameRecord> FindByCharacterIdAsync(string characterId)
        {
            return Task.FromResult(charactersById.TryGetValue(characterId ?? string.Empty, out MMOCharacterNameRecord record) ? Clone(record) : null);
        }

        public Task<IReadOnlyList<MMOFriendEntry>> GetFriendsAsync(string ownerCharacterId)
        {
            return Task.FromResult<IReadOnlyList<MMOFriendEntry>>(Array.Empty<MMOFriendEntry>());
        }

        public Task<MMOServiceResult> AddFriendByNameAsync(string ownerCharacterId, string ownerCharacterName, string friendCharacterName)
        {
            return Task.FromResult(MMOServiceResult.Failure("Use the Unity session join code for online play."));
        }

        public Task<MMOServiceResult> RemoveFriendAsync(string ownerCharacterId, string friendCharacterId)
        {
            return Task.FromResult(MMOServiceResult.Success("No persisted friend list is active."));
        }

        public Task UpdatePresenceAsync(MMOCharacterPresenceRecord record)
        {
            if (record != null && !string.IsNullOrWhiteSpace(record.characterId))
            {
                presenceByCharacterId[record.characterId] = Clone(record);
            }

            return Task.CompletedTask;
        }

        public Task<MMOCharacterPresenceRecord> GetPresenceAsync(string characterId)
        {
            return Task.FromResult(presenceByCharacterId.TryGetValue(characterId ?? string.Empty, out MMOCharacterPresenceRecord record) ? Clone(record) : null);
        }

        public Task SetOfflineAsync(string characterId)
        {
            if (!string.IsNullOrWhiteSpace(characterId))
            {
                presenceByCharacterId.Remove(characterId);
            }

            return Task.CompletedTask;
        }

        public Task AdvertiseSessionAsync(MMOSessionPresenceRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.sessionId))
            {
                return Task.CompletedTask;
            }

            sessionsById[record.sessionId] = Clone(record);
            if (!string.IsNullOrWhiteSpace(record.hostCharacterId))
            {
                sessionIdByHostCharacterId[record.hostCharacterId] = record.sessionId;
            }

            return Task.CompletedTask;
        }

        public Task<MMOSessionPresenceRecord> GetSessionAsync(string sessionId)
        {
            return Task.FromResult(sessionsById.TryGetValue(sessionId ?? string.Empty, out MMOSessionPresenceRecord record) ? Clone(record) : null);
        }

        public Task<MMOSessionPresenceRecord> GetHostedSessionForCharacterAsync(string characterId)
        {
            return Task.FromResult(sessionIdByHostCharacterId.TryGetValue(characterId ?? string.Empty, out string sessionId)
                && sessionsById.TryGetValue(sessionId, out MMOSessionPresenceRecord record)
                    ? Clone(record)
                    : null);
        }

        public Task ClearHostedSessionAsync(string hostCharacterId)
        {
            if (sessionIdByHostCharacterId.TryGetValue(hostCharacterId ?? string.Empty, out string sessionId))
            {
                sessionIdByHostCharacterId.Remove(hostCharacterId);
                sessionsById.Remove(sessionId);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MMOInviteRecord>> GetIncomingInvitesAsync(string characterId)
        {
            return Task.FromResult<IReadOnlyList<MMOInviteRecord>>(Array.Empty<MMOInviteRecord>());
        }

        public Task<MMOServiceResult> SendInviteAsync(string senderCharacterId, string targetCharacterId)
        {
            return Task.FromResult(MMOServiceResult.Failure("Use the Unity session join code for online play."));
        }

        public Task<MMOInviteResolution> AcceptInviteAsync(string inviteId, string targetCharacterId)
        {
            return Task.FromResult(new MMOInviteResolution(false, "Use the Unity session join code for online play.", null));
        }

        public Task<MMOServiceResult> DeclineInviteAsync(string inviteId, string targetCharacterId)
        {
            return Task.FromResult(MMOServiceResult.Success("Invite ignored."));
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
                    normalizedCharacterName = source.normalizedCharacterName
                };
        }

        private static MMOCharacterPresenceRecord Clone(MMOCharacterPresenceRecord source)
        {
            return source == null
                ? null
                : new MMOCharacterPresenceRecord
                {
                    playerId = source.playerId,
                    characterId = source.characterId,
                    characterName = source.characterName,
                    normalizedCharacterName = source.normalizedCharacterName,
                    status = source.status,
                    sessionId = source.sessionId,
                    currentSceneName = source.currentSceneName,
                    joinsAllowed = source.joinsAllowed
                };
        }

        private static MMOSessionPresenceRecord Clone(MMOSessionPresenceRecord source)
        {
            return source == null
                ? null
                : new MMOSessionPresenceRecord
                {
                    hostPlayerId = source.hostPlayerId,
                    hostCharacterId = source.hostCharacterId,
                    hostCharacterName = source.hostCharacterName,
                    sessionId = source.sessionId,
                    currentSceneName = source.currentSceneName,
                    capacity = source.capacity,
                    participantCount = source.participantCount,
                    joinsAllowed = source.joinsAllowed,
                    createdUtcTicks = source.createdUtcTicks,
                    updatedUtcTicks = source.updatedUtcTicks,
                    privateConnectionData = source.privateConnectionData
                };
        }
    }
}
