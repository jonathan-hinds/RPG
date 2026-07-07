using System;
using System.Threading.Tasks;
using RPGClone.Services;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace RPGClone.Social
{
    public sealed class MMOUnityAccountService : IAccountService
    {
        private const int MinimumAccountNameLength = 3;
        private const int MaximumAccountNameLength = 20;
        private const int MinimumPasswordLength = 4;
        private const bool TryUnityUsernamePasswordProvider = false;
        private static bool usernamePasswordProviderUnavailable = !TryUnityUsernamePasswordProvider;

        private readonly IAccountService localFallback = new MMOLocalAccountService();
        private IAccountService activeService;

        public async Task<MMOAccountServiceResult> RegisterAsync(string accountName, string password, string sessionLabel)
        {
            if (!TryValidateAccountName(accountName, out string displayName, out string normalizedName, out string error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (!TryValidatePassword(password, out error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (usernamePasswordProviderUnavailable)
            {
                return await UseLocalFallbackAsync(accountName, password, sessionLabel, true);
            }

            (MMOAccountServiceResult result, bool providerUnavailable) = await AuthenticateAsync(
                displayName,
                normalizedName,
                "Account registered.",
                () => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(normalizedName, password));
            return providerUnavailable
                ? await UseLocalFallbackAsync(accountName, password, sessionLabel, true)
                : result;
        }

        public async Task<MMOAccountServiceResult> LoginAsync(string accountName, string password, string sessionLabel)
        {
            if (!TryValidateAccountName(accountName, out string displayName, out string normalizedName, out string error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (string.IsNullOrEmpty(password))
            {
                return new MMOAccountServiceResult(false, "Enter a password.");
            }

            if (usernamePasswordProviderUnavailable)
            {
                return await UseLocalFallbackAsync(accountName, password, sessionLabel, false);
            }

            (MMOAccountServiceResult result, bool providerUnavailable) = await AuthenticateAsync(
                displayName,
                normalizedName,
                "Logged in.",
                () => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(normalizedName, password));
            return providerUnavailable
                ? await UseLocalFallbackAsync(accountName, password, sessionLabel, false)
                : result;
        }

        public MMOServiceResult Heartbeat(MMOAccountSession session)
        {
            if (activeService != null && !ReferenceEquals(activeService, this))
            {
                return activeService.Heartbeat(session);
            }

            if (session == null || !session.IsAuthenticated)
            {
                return MMOServiceResult.Failure("No account is logged in.");
            }

            if (!MMOUnityServicesBootstrap.IsSignedIn
                || !string.Equals(AuthenticationService.Instance.PlayerId, session.AccountId, StringComparison.Ordinal))
            {
                return MMOServiceResult.Failure("Unity account session is no longer signed in.");
            }

            return MMOServiceResult.Success(string.Empty);
        }

        public void Logout(MMOAccountSession session)
        {
            if (activeService != null && !ReferenceEquals(activeService, this))
            {
                activeService.Logout(session);
                activeService = null;
                return;
            }

            if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn)
            {
                MMOUnityServicesBootstrap.RefreshAuthenticationState();
                return;
            }

            AuthenticationService.Instance.SignOut(true);
            MMOUnityServicesBootstrap.RefreshAuthenticationState();
            activeService = null;
        }

        private async Task<(MMOAccountServiceResult result, bool providerUnavailable)> AuthenticateAsync(
            string displayName,
            string normalizedName,
            string successMessage,
            Func<Task> authenticate)
        {
            try
            {
                await MMOUnityServicesBootstrap.InitializeAsync();
                if (!MMOUnityServicesBootstrap.IsInitialized)
                {
                    return (new MMOAccountServiceResult(false, "Unity services are not initialized. Check the project services configuration and network connection."), false);
                }

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut(true);
                }

                await authenticate();
                MMOUnityServicesBootstrap.RefreshAuthenticationState();
                string playerId = AuthenticationService.Instance.PlayerId;
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    return (new MMOAccountServiceResult(false, "Unity Authentication did not return a player id."), false);
                }

                activeService = this;
                return (new MMOAccountServiceResult(
                    true,
                    successMessage,
                    new MMOAccountSession(playerId, displayName, $"{normalizedName}:{Guid.NewGuid():N}")), false);
            }
            catch (AuthenticationException exception)
            {
                return HandleAuthenticationFailure(exception);
            }
            catch (RequestFailedException exception)
            {
                return HandleAuthenticationFailure(exception);
            }
            catch (Exception exception)
            {
                return (new MMOAccountServiceResult(false, $"Unity account sign-in failed. {exception.Message}"), false);
            }
        }

        private async Task<MMOAccountServiceResult> UseLocalFallbackAsync(
            string accountName,
            string password,
            string sessionLabel,
            bool register)
        {
            usernamePasswordProviderUnavailable = true;
            Debug.LogWarning("Unity username/password authentication is not enabled for this project. Using the local validated account store.");
            MMOAccountServiceResult result = register
                ? await localFallback.RegisterAsync(accountName, password, sessionLabel)
                : await localFallback.LoginAsync(accountName, password, sessionLabel);
            if (result.Succeeded)
            {
                activeService = localFallback;
            }

            return result;
        }

        private static (MMOAccountServiceResult result, bool providerUnavailable) HandleAuthenticationFailure(RequestFailedException exception)
        {
            if (IsUsernamePasswordProviderUnavailable(exception))
            {
                usernamePasswordProviderUnavailable = true;
                return (new MMOAccountServiceResult(false, "Unity username/password authentication is not enabled for this project."), true);
            }

            return (new MMOAccountServiceResult(false, FormatAuthenticationError(exception)), false);
        }

        private static bool TryValidateAccountName(string accountName, out string displayName, out string normalizedName, out string error)
        {
            displayName = string.IsNullOrWhiteSpace(accountName) ? string.Empty : accountName.Trim();
            normalizedName = MMOCharacterNameUtility.NormalizeLookupName(displayName);
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

        private static string FormatAuthenticationError(RequestFailedException exception)
        {
            string message = exception.Message ?? string.Empty;
            if (message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "That account name is already registered.";
            }

            if (message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("credentials", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Account name or password is incorrect.";
            }

            return string.IsNullOrWhiteSpace(message)
                ? "Unity account request failed."
                : $"Unity account request failed. {message}";
        }

        private static bool IsUsernamePasswordProviderUnavailable(RequestFailedException exception)
        {
            string message = exception.Message ?? string.Empty;
            return message.IndexOf("usernamepassword external id provider is not available", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
