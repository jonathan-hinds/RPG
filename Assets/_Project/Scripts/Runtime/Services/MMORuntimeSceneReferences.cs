using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Services
{
    public static class MMORuntimeSceneReferences
    {
        private const float ResolveRetrySeconds = 0.25f;

        private static readonly Dictionary<Type, Component> PlayerComponentCache = new();
        private static GameObject cachedPlayerObject;
        private static Camera cachedMainCamera;
        private static float nextPlayerResolveTime;
        private static float nextCameraResolveTime;

        public static GameObject PlayerObject
        {
            get
            {
                if (cachedPlayerObject == null && Time.unscaledTime >= nextPlayerResolveTime)
                {
                    cachedPlayerObject = GameObject.FindGameObjectWithTag("Player");
                    PlayerComponentCache.Clear();
                    nextPlayerResolveTime = Time.unscaledTime + ResolveRetrySeconds;
                }

                return cachedPlayerObject;
            }
        }

        public static Transform PlayerTransform => PlayerObject != null ? cachedPlayerObject.transform : null;

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
            component = null;
            GameObject player = PlayerObject;
            if (player == null)
            {
                return false;
            }

            Type type = typeof(T);
            if (PlayerComponentCache.TryGetValue(type, out Component cached)
                && cached is T typedComponent
                && typedComponent != null
                && typedComponent.gameObject == player)
            {
                component = typedComponent;
                return true;
            }

            if (!player.TryGetComponent(out component))
            {
                PlayerComponentCache.Remove(type);
                return false;
            }

            PlayerComponentCache[type] = component;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            cachedPlayerObject = null;
            cachedMainCamera = null;
            nextPlayerResolveTime = 0f;
            nextCameraResolveTime = 0f;
            PlayerComponentCache.Clear();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            cachedPlayerObject = null;
            cachedMainCamera = null;
            PlayerComponentCache.Clear();
            nextPlayerResolveTime = 0f;
            nextCameraResolveTime = 0f;
        }
    }
}
