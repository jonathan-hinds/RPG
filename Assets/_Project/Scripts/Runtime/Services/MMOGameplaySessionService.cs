using System;
using System.Threading.Tasks;
using RPGClone.Characters;
using RPGClone.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Services
{
    public static class MMOGameplaySessionService
    {
        private static MMOPlayerRegistry playerRegistry;
        private static MMOLocalPlayerContext localPlayer;
        private static MMOPartyService partyService;

        public static event Action SessionChanged;

        public static string SessionId { get; private set; }
        public static string HostCharacterId { get; private set; }
        public static string SessionSceneName { get; private set; }
        public static string JoinCode { get; private set; }
        public static bool IsLocalHostedSession { get; private set; }
        public static bool IsHostAuthority => IsLocalHostedSession && (!MMONetcodeSessionService.IsConnected || MMONetcodeSessionService.IsHost);
        public static MMOPlayerRegistry Players => playerRegistry ??= new MMOPlayerRegistry();
        public static MMOLocalPlayerContext LocalPlayer => localPlayer ??= new MMOLocalPlayerContext(Players);
        public static MMOPartyService Party => partyService ??= new MMOPartyService();

        public static void StartLocalHostedSession(string sessionId = null)
        {
            if (IsLocalHostedSession && !string.IsNullOrWhiteSpace(SessionId))
            {
                SessionSceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrWhiteSpace(JoinCode) && !string.IsNullOrWhiteSpace(MMONetcodeSessionService.JoinCode))
                {
                    JoinCode = MMONetcodeSessionService.JoinCode;
                }

                SessionChanged?.Invoke();
                MMONetcodeSessionService.StartHost(SessionId);
                return;
            }

            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
            HostCharacterId = string.Empty;
            JoinCode = string.Empty;
            SessionSceneName = SceneManager.GetActiveScene().name;
            IsLocalHostedSession = true;
            Players.Clear();
            LocalPlayer.ClearLocalPlayer();
            SessionChanged?.Invoke();
            MMONetcodeSessionService.StartHost(SessionId);
        }

        public static void JoinHostedSession(string sessionId, string sceneName, string hostCharacterId)
        {
            _ = JoinHostedSessionAsync(sessionId, sceneName, hostCharacterId);
        }

        public static async Task<bool> JoinHostedSessionAsync(string sessionId, string sceneName, string hostCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            string previousSessionId = SessionId;
            string previousHostCharacterId = HostCharacterId;
            string previousSessionSceneName = SessionSceneName;
            string previousJoinCode = JoinCode;
            bool previousIsLocalHostedSession = IsLocalHostedSession;

            SessionId = sessionId;
            HostCharacterId = hostCharacterId ?? string.Empty;
            JoinCode = sessionId ?? string.Empty;
            SessionSceneName = string.IsNullOrWhiteSpace(sceneName) ? SceneManager.GetActiveScene().name : sceneName;
            IsLocalHostedSession = false;
            Players.Clear();
            LocalPlayer.ClearLocalPlayer();
            SessionChanged?.Invoke();
            MMONetcodeSessionService.JoinByCode(sessionId);
            bool connected = await MMONetcodeSessionService.WaitForConnectionAsync();
            if (!connected)
            {
                SessionId = previousSessionId;
                HostCharacterId = previousHostCharacterId;
                SessionSceneName = previousSessionSceneName;
                JoinCode = previousJoinCode;
                IsLocalHostedSession = previousIsLocalHostedSession;
                SessionChanged?.Invoke();
            }

            return connected;
        }

        public static void CompleteUnityHostedSession(string sessionId, string joinCode)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            SessionId = sessionId;
            JoinCode = joinCode ?? string.Empty;
            IsLocalHostedSession = true;
            SessionSceneName = SceneManager.GetActiveScene().name;
            SessionChanged?.Invoke();
        }

        public static void CompleteUnityJoinedSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            SessionId = sessionId;
            IsLocalHostedSession = false;
            SessionSceneName = SceneManager.GetActiveScene().name;
            SessionChanged?.Invoke();
        }

        public static void EnsureLocalHostedSession()
        {
            EnsureSession();
        }

        public static void RegisterLocalPlayer(GameObject playerObject, string characterId = null, string participantId = "local-player")
        {
            EnsureSession();
            LocalPlayer.SetLocalPlayer(playerObject, participantId, characterId ?? string.Empty);
        }

        public static void UnregisterLocalPlayer(GameObject playerObject)
        {
            LocalPlayer.ClearLocalPlayer(playerObject);
        }

        public static void RegisterPlayerCharacter(
            MMOCharacterIdentity identity,
            string participantId,
            string characterId,
            bool isLocal,
            bool isHostAuthority)
        {
            EnsureSession();
            Players.Register(new MMOPlayerParticipant(participantId, characterId, isLocal, isHostAuthority, identity));
            if (isLocal && identity != null)
            {
                LocalPlayer.SetLocalPlayer(identity.gameObject, participantId, characterId);
            }
        }

        public static void UnregisterPlayerCharacter(MMOCharacterIdentity identity)
        {
            Players.Unregister(identity);
        }

        internal static void InvalidateSceneReferences()
        {
            LocalPlayer.InvalidateResolvedReferences();
        }

        private static void EnsureSession()
        {
            if (string.IsNullOrWhiteSpace(SessionId))
            {
                StartLocalHostedSession();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            playerRegistry = new MMOPlayerRegistry();
            localPlayer = new MMOLocalPlayerContext(playerRegistry);
            partyService = new MMOPartyService();
            SessionId = string.Empty;
            HostCharacterId = string.Empty;
            SessionSceneName = string.Empty;
            JoinCode = string.Empty;
            IsLocalHostedSession = true;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            InvalidateSceneReferences();
        }
    }
}
