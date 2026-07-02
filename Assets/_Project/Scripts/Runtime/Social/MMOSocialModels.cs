using System;
using System.Collections.Generic;

namespace RPGClone.Social
{
    public enum MMOCharacterPresenceStatus
    {
        Offline,
        OnlineCharacterSelect,
        OnlineInWorld,
        HostingJoinableSession,
        InvitedToSession,
        JoiningSession,
        BusyUnavailable
    }

    public enum MMOInviteStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired
    }

    [Serializable]
    public sealed class MMOCharacterNameRecord
    {
        public string playerId;
        public string characterId;
        public string characterName;
        public string normalizedCharacterName;
        public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class MMOFriendEntry
    {
        public string characterId;
        public string characterName;
        public string normalizedCharacterName;
        public long addedUtcTicks;
    }

    [Serializable]
    public sealed class MMOFriendListSaveData
    {
        public string ownerCharacterId;
        public List<MMOFriendEntry> friends = new();
    }

    [Serializable]
    public sealed class MMOCharacterPresenceRecord
    {
        public string playerId;
        public string characterId;
        public string characterName;
        public string normalizedCharacterName;
        public MMOCharacterPresenceStatus status;
        public string sessionId;
        public string currentSceneName;
        public bool joinsAllowed;
        public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class MMOSessionPresenceRecord
    {
        public string hostPlayerId;
        public string hostCharacterId;
        public string hostCharacterName;
        public string sessionId;
        public string currentSceneName;
        public int capacity;
        public int participantCount;
        public bool joinsAllowed;
        public long createdUtcTicks;
        public long updatedUtcTicks;
        public string privateConnectionData;
    }

    [Serializable]
    public sealed class MMOInviteRecord
    {
        public string inviteId;
        public string senderPlayerId;
        public string senderCharacterId;
        public string senderCharacterName;
        public string targetCharacterId;
        public string targetCharacterName;
        public string sessionId;
        public MMOInviteStatus status;
        public long createdUtcTicks;
        public long updatedUtcTicks;
    }

    public sealed class MMOServiceResult
    {
        public static MMOServiceResult Success(string message) => new(true, message);
        public static MMOServiceResult Failure(string message) => new(false, message);

        public MMOServiceResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }

    public sealed class MMOInviteResolution
    {
        public MMOInviteResolution(bool succeeded, string message, MMOSessionPresenceRecord session)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Session = session;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public MMOSessionPresenceRecord Session { get; }
    }
}
