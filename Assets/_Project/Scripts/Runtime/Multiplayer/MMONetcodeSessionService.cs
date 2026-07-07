using System;
using System.Reflection;
using System.Threading.Tasks;
using RPGClone.Services;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    public static class MMONetcodeSessionService
    {
        private const int DefaultMaxPlayers = 6;
        private static ISession activeSession;
        private static Task<bool> activeConnectionTask;
        private static NetworkManager networkManager;

        public static event Action Changed;

        public static bool IsConnected => NetworkManager.Singleton != null
            && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient);

        public static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public static bool IsClientOnly => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost;
        public static string SessionId => activeSession?.Id ?? string.Empty;
        public static string JoinCode => activeSession?.Code ?? string.Empty;
        public static string LastError { get; private set; } = string.Empty;
        public static bool IsConnecting => activeConnectionTask != null && !activeConnectionTask.IsCompleted;

        public static void StartHost(string requestedSessionName = null)
        {
            if (IsConnecting)
            {
                return;
            }

            activeConnectionTask = StartHostAsync(requestedSessionName);
        }

        public static void JoinByCode(string joinCode)
        {
            if (IsConnecting)
            {
                return;
            }

            activeConnectionTask = JoinByCodeAsync(joinCode);
        }

        public static async Task<bool> WaitForConnectionAsync()
        {
            if (activeConnectionTask != null)
            {
                return await activeConnectionTask;
            }

            return IsConnected;
        }

        private static async Task<bool> StartHostAsync(string requestedSessionName)
        {
            try
            {
                LastError = string.Empty;
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsHost
                    && NetworkManager.Singleton.IsListening
                    && activeSession != null)
                {
                    Changed?.Invoke();
                    return true;
                }

                await LeaveActiveSessionAsync();
                await MMOUnityServicesBootstrap.EnsureSignedInAnonymouslyAsync();
                EnsureNetworkManager();
                MMONetcodeSharedSessionTransport.Initialize();

                SessionOptions options = new()
                {
                    MaxPlayers = DefaultMaxPlayers,
                    IsPrivate = false,
                    Name = string.IsNullOrWhiteSpace(requestedSessionName)
                        ? $"{Application.productName} Session"
                        : requestedSessionName
                };
                options.WithRelayNetwork();

                activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"Hosted Unity multiplayer session {activeSession.Id}. Join code: {activeSession.Code}");
                bool hostReady = await WaitForNetworkReadyAsync(true);
                if (!hostReady)
                {
                    LastError = "Unity session was created, but Netcode host did not start listening.";
                    Debug.LogError(LastError);
                    Changed?.Invoke();
                    return false;
                }

                MMOGameplaySessionService.CompleteUnityHostedSession(activeSession.Id, activeSession.Code);
                MMONetcodeSharedSessionTransport.Initialize();
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"Failed to host Unity multiplayer session. {exception}");
                Changed?.Invoke();
                return false;
            }
        }

        private static async Task<bool> JoinByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                LastError = "Cannot join a Unity multiplayer session without a join code.";
                Debug.LogWarning(LastError);
                Changed?.Invoke();
                return false;
            }

            try
            {
                LastError = string.Empty;
                if (NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsConnectedClient
                    && !NetworkManager.Singleton.IsHost
                    && activeSession != null
                    && string.Equals(activeSession.Code, joinCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Changed?.Invoke();
                    return true;
                }

                await LeaveActiveSessionAsync();
                await MMOUnityServicesBootstrap.EnsureSignedInAnonymouslyAsync();
                EnsureNetworkManager();
                MMONetcodeSharedSessionTransport.Initialize();

                activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
                Debug.Log($"Joined Unity multiplayer session {activeSession.Id}.");
                bool clientReady = await WaitForNetworkReadyAsync(false);
                if (!clientReady)
                {
                    LastError = "Unity session joined, but Netcode client did not connect to the host.";
                    Debug.LogError(LastError);
                    Changed?.Invoke();
                    return false;
                }

                MMOGameplaySessionService.CompleteUnityJoinedSession(activeSession.Id);
                MMONetcodeSharedSessionTransport.Initialize();
                MMONetcodeSharedSessionTransport.RequestSnapshotFromHost();
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"Failed to join Unity multiplayer session. {exception}");
                Changed?.Invoke();
                return false;
            }
        }

        private static async Task<bool> WaitForNetworkReadyAsync(bool host)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null)
                {
                    if (host && manager.IsHost && manager.IsListening)
                    {
                        return true;
                    }

                    if (!host && manager.IsClient && manager.IsConnectedClient)
                    {
                        return true;
                    }
                }

                await Task.Yield();
            }

            return false;
        }

        private static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                networkManager = NetworkManager.Singleton;
                networkManager.NetworkConfig ??= new NetworkConfig();
                EnsureTransport(networkManager);
                return;
            }

            GameObject networkObject = new("RPG Clone NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(networkObject);
            networkManager = networkObject.AddComponent<NetworkManager>();
            UnityTransport transport = networkObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.EnableSceneManagement = false;
        }

        private static void EnsureTransport(NetworkManager manager)
        {
            if (manager.NetworkConfig.NetworkTransport != null)
            {
                return;
            }

            UnityTransport transport = manager.GetComponent<UnityTransport>() ?? manager.gameObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
        }

        private static async Task LeaveActiveSessionAsync()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && (manager.IsListening || manager.IsConnectedClient || manager.IsHost || manager.IsServer))
            {
                MMONetcodeSharedSessionTransport.ResetRegistration();
                manager.Shutdown();
                await WaitForShutdownAsync(manager);
            }

            if (activeSession != null)
            {
                try
                {
                    MethodInfo leaveAsync = activeSession.GetType().GetMethod("LeaveAsync", Type.EmptyTypes);
                    if (leaveAsync?.Invoke(activeSession, null) is Task leaveTask)
                    {
                        await leaveTask;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to leave previous Unity multiplayer session cleanly. {exception.Message}");
                }

                activeSession = null;
            }
        }

        private static async Task WaitForShutdownAsync(NetworkManager manager)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (manager != null
                && Time.realtimeSinceStartup < deadline
                && (manager.IsListening || manager.IsConnectedClient || manager.IsHost || manager.IsServer))
            {
                await Task.Yield();
            }
        }
    }
}
