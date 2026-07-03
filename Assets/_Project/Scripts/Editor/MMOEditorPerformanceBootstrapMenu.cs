using RPGClone.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    [InitializeOnLoad]
    public static class MMOEditorPerformanceBootstrapMenu
    {
        static MMOEditorPerformanceBootstrapMenu()
        {
            MMOEditorPerformancePlayModeBootstrap.ClearPersistentEditorPrefs();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Enable Solo World Bypass")]
        private static void EnableSoloBypass()
        {
            Configure(enabled: true, simulatedPeerCount: 0, "Codex performance bootstrap enabled for solo world play mode.");
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Enable With 1 Simulated Peer")]
        private static void EnableOnePeer()
        {
            Configure(enabled: true, simulatedPeerCount: 1, "Codex performance bootstrap enabled with 1 simulated peer.");
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Enable With 2 Simulated Peers")]
        private static void EnableTwoPeers()
        {
            Configure(enabled: true, simulatedPeerCount: 2, "Codex performance bootstrap enabled with 2 simulated peers.");
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Disable")]
        private static void Disable()
        {
            MMOEditorPerformancePlayModeBootstrap.Disable();
            Debug.Log("Codex performance bootstrap disabled.");
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Play Starter Valley With Current Profile")]
        private static void PlayStarterValleyWithCurrentProfile()
        {
            if (!MMOEditorPerformancePlayModeBootstrap.Enabled)
            {
                MMOEditorPerformancePlayModeBootstrap.Configure(enabled: true, simulatedPeerCount: 0);
            }

            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                if (SceneManager.GetActiveScene().path != MMOEditorPerformancePlayModeBootstrap.GameplayScenePath)
                {
                    EditorSceneManager.OpenScene(MMOEditorPerformancePlayModeBootstrap.GameplayScenePath, OpenSceneMode.Single);
                }

                EditorApplication.EnterPlaymode();
            }
        }

        [MenuItem("Tools/RPG Clone/Performance/Codex Bootstrap/Install Simulator In Current Play Mode")]
        private static void InstallSimulatorInCurrentPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Codex performance simulator can only be installed while play mode is running.");
                return;
            }

            MMOEditorPerformancePlayModeBootstrap.Configure(enabled: true, simulatedPeerCount: 1);
            MMOEditorSimulatedPeerPublisher publisher = Object.FindFirstObjectByType<MMOEditorSimulatedPeerPublisher>();
            if (publisher == null)
            {
                GameObject simulatorObject = new("Codex Multiplayer Performance Simulator")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Object.DontDestroyOnLoad(simulatorObject);
                publisher = simulatorObject.AddComponent<MMOEditorSimulatedPeerPublisher>();
            }

            publisher.Configure(1, MMOEditorPerformancePlayModeBootstrap.SessionId);
            Debug.Log("Codex performance simulator installed in current play mode.");
        }

        private static void Configure(bool enabled, int simulatedPeerCount, string message)
        {
            MMOEditorPerformancePlayModeBootstrap.Configure(enabled, simulatedPeerCount);
            Debug.Log(message);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode)
            {
                MMOEditorPerformancePlayModeBootstrap.Disable();
            }
        }
    }
}
