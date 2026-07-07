using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGClone.Social
{
    public sealed class MMOLocalAccountService : IAccountService
    {
        private const string FileName = "rpg_clone_accounts.json";
        private const int MinimumAccountNameLength = 3;
        private const int MaximumAccountNameLength = 20;
        private const int MinimumPasswordLength = 4;
        private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(45);

        private readonly string path;

        public MMOLocalAccountService()
        {
            path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public Task<MMOAccountServiceResult> RegisterAsync(string accountName, string password, string sessionLabel)
        {
            return Task.FromResult(Register(accountName, password, sessionLabel));
        }

        public Task<MMOAccountServiceResult> LoginAsync(string accountName, string password, string sessionLabel)
        {
            return Task.FromResult(Login(accountName, password, sessionLabel));
        }

        private MMOAccountServiceResult Register(string accountName, string password, string sessionLabel)
        {
            if (!TryValidateAccountName(accountName, out string displayName, out string normalizedName, out string error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (!TryValidatePassword(password, out error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            return MutateStore(store =>
            {
                if (store.accounts.Exists(account => account.normalizedAccountName == normalizedName))
                {
                    return new MMOAccountServiceResult(false, "That account name is already registered.");
                }

                string salt = CreateSalt();
                long now = DateTime.UtcNow.Ticks;
                MMOAccountRecord account = new()
                {
                    accountId = Guid.NewGuid().ToString("N"),
                    accountName = displayName,
                    normalizedAccountName = normalizedName,
                    passwordSalt = salt,
                    passwordHash = HashPassword(salt, password),
                    createdUtcTicks = now
                };
                store.accounts.Add(account);
                return BeginSession(account, sessionLabel, "Account registered.");
            });
        }

        private MMOAccountServiceResult Login(string accountName, string password, string sessionLabel)
        {
            string normalizedName = NormalizeAccountName(accountName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return new MMOAccountServiceResult(false, "Enter an account name.");
            }

            if (string.IsNullOrEmpty(password))
            {
                return new MMOAccountServiceResult(false, "Enter a password.");
            }

            return MutateStore(store =>
            {
                MMOAccountRecord account = store.accounts.Find(candidate => candidate.normalizedAccountName == normalizedName);
                if (account == null || !SlowEquals(account.passwordHash, HashPassword(account.passwordSalt, password)))
                {
                    return new MMOAccountServiceResult(false, "Account name or password is incorrect.");
                }

                if (IsSessionActive(account))
                {
                    return new MMOAccountServiceResult(false, $"{account.accountName} is already logged in.");
                }

                return BeginSession(account, sessionLabel, "Logged in.");
            });
        }

        public MMOServiceResult Heartbeat(MMOAccountSession session)
        {
            if (session == null || !session.IsAuthenticated)
            {
                return MMOServiceResult.Failure("No account is logged in.");
            }

            return MutateStore(store =>
            {
                MMOAccountRecord account = store.accounts.Find(candidate => candidate.accountId == session.AccountId);
                if (account == null)
                {
                    return MMOServiceResult.Failure("Account session is no longer valid.");
                }

                if (!string.Equals(account.activeSessionId, session.SessionId, StringComparison.Ordinal))
                {
                    return MMOServiceResult.Failure("This account was logged in elsewhere.");
                }

                account.activeSessionUpdatedUtcTicks = DateTime.UtcNow.Ticks;
                return MMOServiceResult.Success(string.Empty);
            });
        }

        public void Logout(MMOAccountSession session)
        {
            if (session == null || !session.IsAuthenticated)
            {
                return;
            }

            MutateStore(store =>
            {
                MMOAccountRecord account = store.accounts.Find(candidate => candidate.accountId == session.AccountId);
                if (account != null && string.Equals(account.activeSessionId, session.SessionId, StringComparison.Ordinal))
                {
                    account.activeSessionId = string.Empty;
                    account.activeSessionLabel = string.Empty;
                    account.activeSessionUpdatedUtcTicks = 0;
                }

                return true;
            });
        }

        private static MMOAccountServiceResult BeginSession(MMOAccountRecord account, string sessionLabel, string message)
        {
            string sessionId = Guid.NewGuid().ToString("N");
            long now = DateTime.UtcNow.Ticks;
            account.activeSessionId = sessionId;
            account.activeSessionLabel = string.IsNullOrWhiteSpace(sessionLabel) ? SystemInfo.deviceName : sessionLabel;
            account.activeSessionUpdatedUtcTicks = now;
            account.lastLoginUtcTicks = now;
            return new MMOAccountServiceResult(true, message, new MMOAccountSession(account.accountId, account.accountName, sessionId));
        }

        private static bool IsSessionActive(MMOAccountRecord account)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.activeSessionId) || account.activeSessionUpdatedUtcTicks <= 0)
            {
                return false;
            }

            return DateTime.UtcNow - new DateTime(account.activeSessionUpdatedUtcTicks, DateTimeKind.Utc) < SessionTimeout;
        }

        private static bool TryValidateAccountName(string accountName, out string displayName, out string normalizedName, out string error)
        {
            displayName = string.IsNullOrWhiteSpace(accountName) ? string.Empty : accountName.Trim();
            normalizedName = NormalizeAccountName(displayName);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Enter an account name.";
                return false;
            }

            if (displayName.Length < MinimumAccountNameLength || displayName.Length > MaximumAccountNameLength)
            {
                error = $"Account names must be {MinimumAccountNameLength}-{MaximumAccountNameLength} characters.";
                return false;
            }

            for (int i = 0; i < displayName.Length; i++)
            {
                char c = displayName[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!allowed)
                {
                    error = "Account names use letters, numbers, and underscores only.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidatePassword(string password, out string error)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
            {
                error = $"Passwords must be at least {MinimumPasswordLength} characters.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string NormalizeAccountName(string accountName)
        {
            return string.IsNullOrWhiteSpace(accountName) ? string.Empty : accountName.Trim().ToLowerInvariant();
        }

        private T MutateStore<T>(Func<MMOLocalAccountStore, T> mutate)
        {
            return WithLockedStore(store =>
            {
                T result = mutate(store);
                return (result, true);
            });
        }

        private T WithLockedStore<T>(Func<MMOLocalAccountStore, (T result, bool save)> action)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Exception lastException = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    using FileStream stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    string json;
                    using (StreamReader reader = new(stream, Encoding.UTF8, true, 1024, true))
                    {
                        json = reader.ReadToEnd();
                    }

                    MMOLocalAccountStore store = string.IsNullOrWhiteSpace(json)
                        ? new MMOLocalAccountStore()
                        : JsonUtility.FromJson<MMOLocalAccountStore>(json) ?? new MMOLocalAccountStore();
                    store.accounts ??= new List<MMOAccountRecord>();

                    (T result, bool save) = action(store);
                    if (save)
                    {
                        string output = JsonUtility.ToJson(store, true);
                        stream.SetLength(0);
                        stream.Position = 0;
                        using StreamWriter writer = new(stream, Encoding.UTF8, 1024, true);
                        writer.Write(output);
                    }

                    return result;
                }
                catch (IOException exception)
                {
                    lastException = exception;
                    Thread.Sleep(25 + attempt * 10);
                }
            }

            Debug.LogWarning($"Account store is busy or unavailable. {lastException?.Message}");
            throw lastException ?? new IOException("Account store is unavailable.");
        }

        private static string CreateSalt()
        {
            byte[] bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashPassword(string salt, string password)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

        private static bool SlowEquals(string a, string b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            int diff = a.Length ^ b.Length;
            int length = Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        [Serializable]
        private sealed class MMOLocalAccountStore
        {
            public List<MMOAccountRecord> accounts = new();
        }
    }
}
