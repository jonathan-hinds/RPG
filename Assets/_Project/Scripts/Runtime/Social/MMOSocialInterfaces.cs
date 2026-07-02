using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGClone.Social
{
    public interface ICharacterNameDirectory
    {
        Task<MMOServiceResult> RegisterOrUpdateAsync(MMOCharacterNameRecord record);
        Task<MMOCharacterNameRecord> FindByNameAsync(string characterName);
        Task<MMOCharacterNameRecord> FindByCharacterIdAsync(string characterId);
    }

    public interface IFriendListService
    {
        Task<IReadOnlyList<MMOFriendEntry>> GetFriendsAsync(string ownerCharacterId);
        Task<MMOServiceResult> AddFriendByNameAsync(string ownerCharacterId, string ownerCharacterName, string friendCharacterName);
        Task<MMOServiceResult> RemoveFriendAsync(string ownerCharacterId, string friendCharacterId);
    }

    public interface ICharacterPresenceService
    {
        Task UpdatePresenceAsync(MMOCharacterPresenceRecord record);
        Task<MMOCharacterPresenceRecord> GetPresenceAsync(string characterId);
        Task SetOfflineAsync(string characterId);
    }

    public interface ISessionPresenceService
    {
        Task AdvertiseSessionAsync(MMOSessionPresenceRecord record);
        Task<MMOSessionPresenceRecord> GetSessionAsync(string sessionId);
        Task<MMOSessionPresenceRecord> GetHostedSessionForCharacterAsync(string characterId);
        Task ClearHostedSessionAsync(string hostCharacterId);
    }

    public interface IInviteService
    {
        Task<IReadOnlyList<MMOInviteRecord>> GetIncomingInvitesAsync(string characterId);
        Task<MMOServiceResult> SendInviteAsync(string senderCharacterId, string targetCharacterId);
        Task<MMOInviteResolution> AcceptInviteAsync(string inviteId, string targetCharacterId);
        Task<MMOServiceResult> DeclineInviteAsync(string inviteId, string targetCharacterId);
    }

    public interface IActiveGameplaySession
    {
        string SessionId { get; }
        bool IsHosting { get; }
        bool JoinsAllowed { get; }
        MMOSessionPresenceRecord CreatePresenceRecord(string playerId, string characterId, string characterName);
    }
}
