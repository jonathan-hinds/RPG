using System;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Services
{
    public static class MMOGameplaySessionService
    {
        private static MMOPlayerRegistry playerRegistry;
        private static MMOLocalPlayerContext localPlayer;

        public static event Action SessionChanged;

        public static string SessionId { get; private set; }
        public static string HostCharacterId { get; private set; }
        public static string SessionSceneName { get; private set; }
        public static bool IsLocalHostedSession { get; private set; }
        public static bool IsHostAuthority => IsLocalHostedSession;
        public static MMOPlayerRegistry Players => playerRegistry ??= new MMOPlayerRegistry();
        public static MMOLocalPlayerContext LocalPlayer => localPlayer ??= new MMOLocalPlayerContext(Players);

        public static void StartLocalHostedSession(string sessionId = null)
        {
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
            HostCharacterId = string.Empty;
            SessionSceneName = SceneManager.GetActiveScene().name;
            IsLocalHostedSession = true;
            Players.Clear();
            LocalPlayer.ClearLocalPlayer();
            SessionChanged?.Invoke();
        }

        public static void JoinHostedSession(string sessionId, string sceneName, string hostCharacterId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            SessionId = sessionId;
            HostCharacterId = hostCharacterId ?? string.Empty;
            SessionSceneName = string.IsNullOrWhiteSpace(sceneName) ? SceneManager.GetActiveScene().name : sceneName;
            IsLocalHostedSession = false;
            Players.Clear();
            LocalPlayer.ClearLocalPlayer();
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
            SessionId = string.Empty;
            HostCharacterId = string.Empty;
            SessionSceneName = string.Empty;
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
