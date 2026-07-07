using System;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGClone.Social
{
    public static class MMOSocialIdentityService
    {
        private const string LastAccountNamePrefsKey = "rpg_clone_last_account_name";
        private static readonly IAccountService AccountService = new MMOUnityAccountService();
        private static MMOAccountSession currentSession;

        public static event Action Changed;

        public static bool IsAuthenticated => currentSession != null && currentSession.IsAuthenticated;
        public static string AccountId => IsAuthenticated ? currentSession.AccountId : string.Empty;
        public static string AccountName => IsAuthenticated ? currentSession.AccountName : string.Empty;
        public static string SessionId => IsAuthenticated ? currentSession.SessionId : string.Empty;
        public static string LastAccountName => PlayerPrefs.GetString(LastAccountNamePrefsKey, string.Empty);

        // Existing social/presence code treats this as the authenticated player identifier.
        public static string PlayerId => AccountId;
        public static bool IsLocalTestIdentity => IsAuthenticated;

        public static async Task<MMOAccountServiceResult> RegisterAsync(string accountName, string password)
        {
            MMOAccountServiceResult result = await AccountService.RegisterAsync(accountName, password, CreateSessionLabel());
            if (result.Succeeded)
            {
                Activate(result.Session);
                RememberAccountName(result.Session.AccountName);
            }

            return result;
        }

        public static async Task<MMOAccountServiceResult> LoginAsync(string accountName, string password)
        {
            MMOAccountServiceResult result = await AccountService.LoginAsync(accountName, password, CreateSessionLabel());
            if (result.Succeeded)
            {
                Activate(result.Session);
                RememberAccountName(result.Session.AccountName);
            }

            return result;
        }

        public static MMOServiceResult Heartbeat()
        {
            return IsAuthenticated
                ? AccountService.Heartbeat(currentSession)
                : MMOServiceResult.Failure("No account is logged in.");
        }

        public static void Logout()
        {
            if (currentSession != null)
            {
                AccountService.Logout(currentSession);
            }

            currentSession = null;
            Changed?.Invoke();
        }

        private static void Activate(MMOAccountSession session)
        {
            currentSession = session;
            Changed?.Invoke();
        }

        private static void RememberAccountName(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                return;
            }

            PlayerPrefs.SetString(LastAccountNamePrefsKey, accountName);
            PlayerPrefs.Save();
        }

        private static string CreateSessionLabel()
        {
            return $"{Application.productName} on {SystemInfo.deviceName}";
        }
    }
}
