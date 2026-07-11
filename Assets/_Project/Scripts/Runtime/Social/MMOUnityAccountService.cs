using System;
using System.Threading.Tasks;
using RPGClone.Services;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace RPGClone.Social
{
    public sealed class MMOUnityAccountService : IAccountService
    {
        private const int MinimumAccountNameLength = 3;
        private const int MaximumAccountNameLength = 20;
        private const int MinimumPasswordLength = 8;
        private const int MaximumPasswordLength = 30;

        public async Task<MMOAccountServiceResult> RegisterAsync(string accountName, string password)
        {
            if (!TryValidateAccountName(accountName, out string displayName, out string normalizedName, out string error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (!TryValidatePassword(password, out error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            return await AuthenticateAsync(
                displayName,
                normalizedName,
                "Account registered.",
                () => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(normalizedName, password));
        }

        public async Task<MMOAccountServiceResult> LoginAsync(string accountName, string password)
        {
            if (!TryValidateAccountName(accountName, out string displayName, out string normalizedName, out string error))
            {
                return new MMOAccountServiceResult(false, error);
            }

            if (string.IsNullOrEmpty(password))
            {
                return new MMOAccountServiceResult(false, "Enter a password.");
            }

            return await AuthenticateAsync(
                displayName,
                normalizedName,
                "Logged in.",
                () => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(normalizedName, password));
        }

        public MMOServiceResult Heartbeat(MMOAccountSession session)
        {
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
            if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn)
            {
                MMOUnityServicesBootstrap.RefreshAuthenticationState();
                return;
            }

            AuthenticationService.Instance.SignOut(true);
            MMOUnityServicesBootstrap.RefreshAuthenticationState();
        }

        private async Task<MMOAccountServiceResult> AuthenticateAsync(
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
                    return new MMOAccountServiceResult(false, "Unity services are not initialized. Check the project services configuration and network connection.");
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
                    return new MMOAccountServiceResult(false, "Unity Authentication did not return a player id.");
                }

                return new MMOAccountServiceResult(
                    true,
                    successMessage,
                    new MMOAccountSession(playerId, displayName, $"{normalizedName}:{Guid.NewGuid():N}"));
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
                return new MMOAccountServiceResult(false, $"Unity account sign-in failed. {exception.Message}");
            }
        }

        private static MMOAccountServiceResult HandleAuthenticationFailure(RequestFailedException exception)
        {
            if (IsUsernamePasswordProviderUnavailable(exception))
            {
                return new MMOAccountServiceResult(
                    false,
                    "Unity Username & Password authentication is not enabled. Enable it in Services > Authentication before playing.");
            }

            return new MMOAccountServiceResult(false, FormatAuthenticationError(exception));
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
            if (string.IsNullOrEmpty(password)
                || password.Length < MinimumPasswordLength
                || password.Length > MaximumPasswordLength)
            {
                error = $"Passwords must be {MinimumPasswordLength}-{MaximumPasswordLength} characters.";
                return false;
            }

            bool hasLower = false;
            bool hasUpper = false;
            bool hasNumber = false;
            bool hasSymbol = false;
            foreach (char character in password)
            {
                hasLower |= char.IsLower(character);
                hasUpper |= char.IsUpper(character);
                hasNumber |= char.IsDigit(character);
                hasSymbol |= !char.IsLetterOrDigit(character);
            }

            if (!hasLower || !hasUpper || !hasNumber || !hasSymbol)
            {
                error = "Passwords require a lowercase letter, uppercase letter, number, and symbol.";
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
