using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Quests
{
    [Serializable]
    public sealed class MMOSharedWorldObjectSnapshot
    {
        public string sessionId;
        public string worldObjectId;
        public bool available = true;
        public float respawnRemainingSeconds;
        public long updatedUtcTicks;
    }

    [Serializable]
    public sealed class MMOSharedWorldObjectInteractionRequest
    {
        public string requestId;
        public string sessionId;
        public string worldObjectId;
        public string actorCharacterId;
        public long requestedUtcTicks;
        public bool processed;

        public static MMOSharedWorldObjectInteractionRequest Create(string sessionId, string worldObjectId, string actorCharacterId)
        {
            return new MMOSharedWorldObjectInteractionRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                sessionId = sessionId ?? string.Empty,
                worldObjectId = worldObjectId ?? string.Empty,
                actorCharacterId = actorCharacterId ?? string.Empty,
                requestedUtcTicks = DateTime.UtcNow.Ticks
            };
        }
    }

    public static class MMOSharedWorldObjectStateService
    {
        private static readonly Dictionary<string, MMOQuestWorldInteractable> InteractablesById = new();

        public static IReadOnlyCollection<MMOQuestWorldInteractable> ActiveInteractables => InteractablesById.Values;

        public static void Register(MMOQuestWorldInteractable interactable)
        {
            if (interactable == null || string.IsNullOrWhiteSpace(interactable.WorldObjectId))
            {
                return;
            }

            InteractablesById[interactable.WorldObjectId] = interactable;
        }

        public static void Unregister(MMOQuestWorldInteractable interactable)
        {
            if (interactable == null || string.IsNullOrWhiteSpace(interactable.WorldObjectId))
            {
                return;
            }

            if (InteractablesById.TryGetValue(interactable.WorldObjectId, out MMOQuestWorldInteractable registered)
                && registered == interactable)
            {
                InteractablesById.Remove(interactable.WorldObjectId);
            }
        }

        public static bool TryGetInteractable(string worldObjectId, out MMOQuestWorldInteractable interactable)
        {
            if (!string.IsNullOrWhiteSpace(worldObjectId)
                && InteractablesById.TryGetValue(worldObjectId, out interactable)
                && interactable != null)
            {
                return true;
            }

            interactable = null;
            return false;
        }
    }
}
