using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Services
{
    public static class MMORuntimeSceneReferences
    {
        private const float ResolveRetrySeconds = 0.25f;

        private static Camera cachedMainCamera;
        private static float nextCameraResolveTime;

        public static GameObject PlayerObject => MMOGameplaySessionService.LocalPlayer.PlayerObject;
        public static Transform PlayerTransform => MMOGameplaySessionService.LocalPlayer.PlayerTransform;

        public static Camera MainCamera
        {
            get
            {
                if (cachedMainCamera == null && Time.unscaledTime >= nextCameraResolveTime)
                {
                    cachedMainCamera = Camera.main;
                    nextCameraResolveTime = Time.unscaledTime + ResolveRetrySeconds;
                }

                return cachedMainCamera;
            }
        }

        public static bool TryGetPlayerComponent<T>(out T component) where T : Component
        {
            return MMOGameplaySessionService.LocalPlayer.TryGetComponent(out component);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            cachedMainCamera = null;
            nextCameraResolveTime = 0f;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            cachedMainCamera = null;
            nextCameraResolveTime = 0f;
            MMOGameplaySessionService.InvalidateSceneReferences();
        }
    }
}
