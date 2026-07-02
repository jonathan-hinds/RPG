using System;
using System.Collections.Generic;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Services
{
    public sealed class MMOLocalPlayerContext
    {
        private const float ResolveRetrySeconds = 0.25f;

        private readonly Dictionary<Type, Component> componentCache = new();
        private readonly MMOPlayerRegistry registry;
        private GameObject explicitPlayerObject;
        private GameObject cachedPlayerObject;
        private Camera cachedCamera;
        private float nextPlayerResolveTime;
        private float nextCameraResolveTime;

        internal MMOLocalPlayerContext(MMOPlayerRegistry registry)
        {
            this.registry = registry;
        }

        public event Action Changed;

        public GameObject PlayerObject
        {
            get
            {
                if (explicitPlayerObject != null)
                {
                    return explicitPlayerObject;
                }

                if (cachedPlayerObject == null && Time.unscaledTime >= nextPlayerResolveTime)
                {
                    cachedPlayerObject = GameObject.FindGameObjectWithTag("Player");
                    RegisterResolvedLocalPlayer(cachedPlayerObject, string.Empty);
                    componentCache.Clear();
                    nextPlayerResolveTime = Time.unscaledTime + ResolveRetrySeconds;
                }

                return cachedPlayerObject;
            }
        }

        public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

        public MMOCharacterIdentity Identity
        {
            get
            {
                TryGetComponent(out MMOCharacterIdentity identity);
                return identity;
            }
        }

        public Camera MainCamera
        {
            get
            {
                if (cachedCamera == null && Time.unscaledTime >= nextCameraResolveTime)
                {
                    cachedCamera = Camera.main;
                    nextCameraResolveTime = Time.unscaledTime + ResolveRetrySeconds;
                }

                return cachedCamera;
            }
        }

        public void SetLocalPlayer(GameObject playerObject, string participantId, string characterId)
        {
            if (playerObject == explicitPlayerObject)
            {
                RegisterResolvedLocalPlayer(playerObject, characterId, participantId);
                return;
            }

            explicitPlayerObject = playerObject;
            cachedPlayerObject = playerObject;
            componentCache.Clear();
            RegisterResolvedLocalPlayer(playerObject, characterId, participantId);
            Changed?.Invoke();
        }

        public void ClearLocalPlayer(GameObject expectedPlayerObject = null)
        {
            if (expectedPlayerObject != null && explicitPlayerObject != expectedPlayerObject)
            {
                return;
            }

            if (explicitPlayerObject != null && explicitPlayerObject.TryGetComponent(out MMOCharacterIdentity identity))
            {
                registry.Unregister(identity);
            }

            explicitPlayerObject = null;
            cachedPlayerObject = null;
            componentCache.Clear();
            nextPlayerResolveTime = 0f;
            Changed?.Invoke();
        }

        public bool TryGetComponent<T>(out T component) where T : Component
        {
            component = null;
            GameObject player = PlayerObject;
            if (player == null)
            {
                return false;
            }

            Type type = typeof(T);
            if (componentCache.TryGetValue(type, out Component cached)
                && cached is T typedComponent
                && typedComponent != null
                && typedComponent.gameObject == player)
            {
                component = typedComponent;
                return true;
            }

            if (!player.TryGetComponent(out component))
            {
                componentCache.Remove(type);
                return false;
            }

            componentCache[type] = component;
            return true;
        }

        public void InvalidateResolvedReferences()
        {
            if (explicitPlayerObject == null)
            {
                cachedPlayerObject = null;
            }

            cachedCamera = null;
            componentCache.Clear();
            nextPlayerResolveTime = 0f;
            nextCameraResolveTime = 0f;
            registry.RemoveInvalidParticipants();
        }

        private void RegisterResolvedLocalPlayer(GameObject playerObject, string characterId, string participantId = "local-player")
        {
            if (playerObject == null || !playerObject.TryGetComponent(out MMOCharacterIdentity identity))
            {
                return;
            }

            registry.Register(new MMOPlayerParticipant(participantId, characterId, true, true, identity));
        }
    }
}
