using System;

namespace RPGClone.Social
{
    [Serializable]
    public sealed class MMOAccountRecord
    {
        public string accountId;
        public string accountName;
        public string normalizedAccountName;
        public string passwordSalt;
        public string passwordHash;
        public long createdUtcTicks;
        public long lastLoginUtcTicks;
        public string activeSessionId;
        public string activeSessionLabel;
        public long activeSessionUpdatedUtcTicks;
    }

    public sealed class MMOAccountSession
    {
        public MMOAccountSession(string accountId, string accountName, string sessionId)
        {
            AccountId = accountId ?? string.Empty;
            AccountName = accountName ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
        }

        public string AccountId { get; }
        public string AccountName { get; }
        public string SessionId { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccountId) && !string.IsNullOrWhiteSpace(SessionId);
    }

    public sealed class MMOAccountServiceResult
    {
        public MMOAccountServiceResult(bool succeeded, string message, MMOAccountSession session = null)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Session = session;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public MMOAccountSession Session { get; }
    }

    public interface IAccountService
    {
        MMOAccountServiceResult Register(string accountName, string password, string sessionLabel);
        MMOAccountServiceResult Login(string accountName, string password, string sessionLabel);
        MMOServiceResult Heartbeat(MMOAccountSession session);
        void Logout(MMOAccountSession session);
    }
}
