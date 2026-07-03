#if UNITY_EDITOR
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Multiplayer;
using RPGClone.Services;
using RPGClone.Social;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Debugging
{
    public static class MMOEditorPerformancePlayModeBootstrap
    {
        private const string GameplaySceneName = "OrcishStarterValley";
        public const string EnabledPrefsKey = "RPGClone.CodexPerformanceBootstrap.Enabled";
        public const string SimulatedPeerCountPrefsKey = "RPGClone.CodexPerformanceBootstrap.SimulatedPeerCount";
        public const string SessionIdPrefsKey = "RPGClone.CodexPerformanceBootstrap.SessionId";
        public const string DebugSessionId = "codex-local-performance-session";
        public const string GameplayScenePath = "Assets/Scenes/OrcishStarterValley.unity";
        private const string DebugCharacterId = "codex-perf-local-player";

        public static bool Enabled => SessionState.GetBool(EnabledPrefsKey, false);
        public static int SimulatedPeerCount => Mathf.Clamp(SessionState.GetInt(SimulatedPeerCountPrefsKey, 0), 0, 4);
        public static string SessionId => SessionState.GetString(SessionIdPrefsKey, DebugSessionId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyBeforeSceneLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (!Enabled)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name != GameplaySceneName)
            {
                return;
            }

            SelectLocalPerformanceCharacter();
            MMOGameplaySessionService.StartLocalHostedSession(SessionId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterSceneLoad()
        {
            TryInstallPeerPublisher();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Enabled || scene.name != GameplaySceneName)
            {
                return;
            }

            SelectLocalPerformanceCharacter();
            MMOGameplaySessionService.StartLocalHostedSession(SessionId);
            TryInstallPeerPublisher();
        }

        private static void TryInstallPeerPublisher()
        {
            if (!Enabled || SceneManager.GetActiveScene().name != GameplaySceneName || SimulatedPeerCount <= 0)
            {
                return;
            }

            if (Object.FindFirstObjectByType<MMOEditorSimulatedPeerPublisher>() != null)
            {
                return;
            }

            GameObject simulatorObject = new("Codex Multiplayer Performance Simulator")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(simulatorObject);
            MMOEditorSimulatedPeerPublisher publisher = simulatorObject.AddComponent<MMOEditorSimulatedPeerPublisher>();
            publisher.Configure(SimulatedPeerCount, SessionId);
        }

        public static void Configure(bool enabled, int simulatedPeerCount)
        {
            SessionState.SetBool(EnabledPrefsKey, enabled);
            SessionState.SetInt(SimulatedPeerCountPrefsKey, Mathf.Clamp(simulatedPeerCount, 0, 4));
            SessionState.SetString(SessionIdPrefsKey, DebugSessionId);
            ClearPersistentEditorPrefs();
        }

        public static void Disable()
        {
            SessionState.SetBool(EnabledPrefsKey, false);
            SessionState.SetInt(SimulatedPeerCountPrefsKey, 0);
            SessionState.EraseString(SessionIdPrefsKey);
            ClearPersistentEditorPrefs();
        }

        public static void ClearPersistentEditorPrefs()
        {
            EditorPrefs.DeleteKey(EnabledPrefsKey);
            EditorPrefs.DeleteKey(SimulatedPeerCountPrefsKey);
            EditorPrefs.DeleteKey(SessionIdPrefsKey);
        }

        private static void SelectLocalPerformanceCharacter()
        {
            MMOCharacterSession.Select(new MMOCharacterSaveData
            {
                characterId = DebugCharacterId,
                accountId = "codex-perf-account",
                characterName = "Codex Perf",
                normalizedCharacterName = MMOCharacterNameUtility.NormalizeLookupName("Codex Perf"),
                race = MMOPlayableRace.Orc,
                characterClass = MMOPlayableClass.Warrior,
                level = 1,
                sceneName = GameplaySceneName,
                position = new Vector3SaveData(new Vector3(-42f, 15.9735f, -178f)),
                rotationEuler = new Vector3SaveData(new Vector3(0f, 18f, 0f))
            });
        }
    }

    public sealed class MMOEditorSimulatedPeerPublisher : MonoBehaviour
    {
        private const float CharacterSnapshotSeconds = 1f;
        private const float RuntimeSnapshotSeconds = 0.05f;

        private int peerCount;
        private string sessionId;
        private float nextCharacterSnapshotTime;
        private float nextRuntimeSnapshotTime;
        private float startTime;

        public void Configure(int newPeerCount, string newSessionId)
        {
            peerCount = Mathf.Clamp(newPeerCount, 0, 4);
            sessionId = string.IsNullOrWhiteSpace(newSessionId)
                ? MMOEditorPerformancePlayModeBootstrap.SessionId
                : newSessionId;
            startTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (peerCount <= 0
                || string.IsNullOrWhiteSpace(sessionId)
                || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            if (Time.unscaledTime >= nextCharacterSnapshotTime)
            {
                nextCharacterSnapshotTime = Time.unscaledTime + CharacterSnapshotSeconds;
                PublishCharacterSnapshots();
            }

            if (Time.unscaledTime >= nextRuntimeSnapshotTime)
            {
                nextRuntimeSnapshotTime = Time.unscaledTime + RuntimeSnapshotSeconds;
                PublishRuntimeSnapshots();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < peerCount; i++)
            {
                MMOLocalSharedSessionStore.RemoveParticipant(sessionId, GetCharacterId(i));
            }
        }

        private void PublishCharacterSnapshots()
        {
            for (int i = 0; i < peerCount; i++)
            {
                MMOCharacterSaveData characterData = CreateCharacterData(i);
                MMOLocalSharedSessionStore.UpsertParticipant(new MMOSessionParticipantSnapshot
                {
                    participantId = GetParticipantId(i),
                    characterId = characterData.characterId,
                    accountId = characterData.accountId,
                    sessionId = sessionId,
                    sceneName = characterData.sceneName,
                    isHost = false,
                    characterData = characterData
                });
            }
        }

        private void PublishRuntimeSnapshots()
        {
            for (int i = 0; i < peerCount; i++)
            {
                Vector3 position = GetPeerPosition(i);
                MMOLocalSharedSessionStore.UpsertParticipantRuntime(
                    sessionId,
                    GetCharacterId(i),
                    position,
                    GetPeerRotation(i).eulerAngles,
                    100,
                    0);
            }
        }

        private MMOCharacterSaveData CreateCharacterData(int index)
        {
            Vector3 position = GetPeerPosition(index);
            return new MMOCharacterSaveData
            {
                characterId = GetCharacterId(index),
                accountId = $"codex-peer-account-{index + 1}",
                characterName = $"Codex Peer {index + 1}",
                normalizedCharacterName = MMOCharacterNameUtility.NormalizeLookupName($"Codex Peer {index + 1}"),
                race = MMOPlayableRace.Orc,
                characterClass = MMOPlayableClass.Warrior,
                level = 1,
                currentHealth = 100,
                currentMana = 0,
                sceneName = SceneManager.GetActiveScene().name,
                position = new Vector3SaveData(position),
                rotationEuler = new Vector3SaveData(GetPeerRotation(index).eulerAngles)
            };
        }

        private Vector3 GetPeerPosition(int index)
        {
            Transform localPlayer = MMOGameplaySessionService.LocalPlayer.PlayerTransform;
            Vector3 origin = localPlayer != null ? localPlayer.position : new Vector3(-42f, 15.9735f, -178f);
            float elapsed = Mathf.Max(0f, Time.unscaledTime - startTime);
            float angle = elapsed * 0.65f + index * Mathf.PI * 2f / Mathf.Max(1, peerCount);
            float radius = 3f + index * 1.2f;
            return origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private Quaternion GetPeerRotation(int index)
        {
            Vector3 position = GetPeerPosition(index);
            Transform localPlayer = MMOGameplaySessionService.LocalPlayer.PlayerTransform;
            Vector3 target = localPlayer != null ? localPlayer.position : position + Vector3.forward;
            Vector3 direction = target - position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        }

        private static string GetParticipantId(int index)
        {
            return $"codex-peer-participant-{index + 1}";
        }

        private static string GetCharacterId(int index)
        {
            return $"codex-peer-character-{index + 1}";
        }
    }
}
#endif
