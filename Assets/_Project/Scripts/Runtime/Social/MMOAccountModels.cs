using System.Threading.Tasks;

namespace RPGClone.Social
{
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

    public interface IAccountService
    {
        Task<MMOAccountServiceResult> RegisterAsync(string accountName, string password);
        Task<MMOAccountServiceResult> LoginAsync(string accountName, string password);
        MMOServiceResult Heartbeat(MMOAccountSession session);
        void Logout(MMOAccountSession session);
    }
}
