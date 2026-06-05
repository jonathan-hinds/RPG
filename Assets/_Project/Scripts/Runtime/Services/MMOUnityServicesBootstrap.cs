using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGClone.Services
{
    public sealed class MMOUnityServicesBootstrap : MonoBehaviour
    {
        private static Task initializationTask;

        public static bool IsInitialized { get; private set; }
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
                Type unityServicesType = Type.GetType("Unity.Services.Core.UnityServices, Unity.Services.Core");
                Type authenticationType = Type.GetType("Unity.Services.Authentication.AuthenticationService, Unity.Services.Authentication");
                if (unityServicesType == null || authenticationType == null)
                {
                    IsInitialized = false;
                    return;
                }

                MethodInfo initializeMethod = unityServicesType.GetMethod("InitializeAsync", Type.EmptyTypes);
                if (initializeMethod?.Invoke(null, null) is Task initializeTask)
                {
                    await initializeTask;
                }

                object authenticationService = authenticationType.GetProperty("Instance")?.GetValue(null);
                bool isSignedIn = authenticationService != null && (bool)(authenticationService.GetType().GetProperty("IsSignedIn")?.GetValue(authenticationService) ?? false);
                if (!isSignedIn && authenticationService?.GetType().GetMethod("SignInAnonymouslyAsync", Type.EmptyTypes)?.Invoke(authenticationService, null) is Task signInTask)
                {
                    await signInTask;
                }

                PlayerId = authenticationService?.GetType().GetProperty("PlayerId")?.GetValue(authenticationService) as string ?? string.Empty;
                IsInitialized = !string.IsNullOrWhiteSpace(PlayerId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Unity Gaming Services initialization failed. Local persistence remains available. {exception.Message}");
                IsInitialized = false;
                initializationTask = null;
            }
        }
    }
}
