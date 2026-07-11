using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace RPGClone.Services
{
    public sealed class MMOUnityServicesBootstrap : MonoBehaviour
    {
        private static Task initializationTask;

        public static bool IsInitialized { get; private set; }
        public static bool IsSignedIn => IsInitialized && AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;
        public static string PlayerId { get; private set; } = string.Empty;

        public static async Task InitializeAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            initializationTask ??= InitializeInternalAsync();
            await initializationTask;
        }

        private static async Task InitializeInternalAsync()
        {
            try
            {
                InitializationOptions options = new InitializationOptions().SetProfile(ResolveServicesProfile());
                await UnityServices.InitializeAsync(options);
                IsInitialized = true;
                RefreshAuthenticationState();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Unity Gaming Services initialization failed. Online play is unavailable. {exception.Message}");
                IsInitialized = false;
                initializationTask = null;
            }
        }

        public static async Task EnsureAuthenticatedAsync()
        {
            await InitializeAsync();
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Unity Gaming Services could not be initialized.");
            }

            RefreshAuthenticationState();
            if (!IsSignedIn)
            {
                throw new InvalidOperationException("Sign in to a Unity account before starting or joining a gameplay session.");
            }
        }

        public static void RefreshAuthenticationState()
        {
            PlayerId = AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : string.Empty;
        }

        private static string ResolveServicesProfile()
        {
            string source = Application.persistentDataPath;
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619;
                }

                return $"rpg_{hash:x8}";
            }
        }
    }
}
