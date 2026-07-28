using System.Collections.Generic;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Characters
{
    [DisallowMultipleComponent]
    public sealed class MMONpcInteractionFacing : MonoBehaviour
    {
        private const float AuthorityDistanceTolerance = 0.75f;
        private const float MinimumDirectionSqrMagnitude = 0.0001f;
        private const int MaximumInteractionKeyLength = 256;

        private static readonly Dictionary<string, MMONpcInteractionFacing> ControllersByInteractionKey = new();

        [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 720f;

        private readonly Dictionary<string, float> interactionDistancesByKey = new();
        private string primaryInteractionKey;
        private float maximumInteractionDistance = 1f;
        private Quaternion targetRotation;
        private bool hasTargetRotation;

        public static string QuestInteractionKey(string npcId)
        {
            return CreateInteractionKey("quest", npcId);
        }

        public static string VendorInteractionKey(string vendorId)
        {
            return CreateInteractionKey("vendor", vendorId);
        }

        public static string TrainerInteractionKey(string trainerId)
        {
            return CreateInteractionKey("trainer", trainerId);
        }

        public static MMONpcInteractionFacing GetOrAdd(GameObject npcObject)
        {
            return npcObject != null
                ? npcObject.GetComponent<MMONpcInteractionFacing>()
                    ?? npcObject.AddComponent<MMONpcInteractionFacing>()
                : null;
        }

        public void RegisterInteractionKey(string interactionKey, float interactionDistance)
        {
            if (!IsValidInteractionKey(interactionKey))
            {
                Debug.LogError($"NPC '{name}' cannot register an invalid interaction-facing key.");
                return;
            }

            if (ControllersByInteractionKey.TryGetValue(interactionKey, out MMONpcInteractionFacing existing)
                && existing != null
                && existing != this)
            {
                Debug.LogError(
                    $"NPC interaction-facing key '{interactionKey}' is already registered by '{existing.name}'. " +
                    "NPC interaction identifiers must be unique within a scene.");
                return;
            }

            interactionDistancesByKey[interactionKey] = Mathf.Max(1f, interactionDistance);
            maximumInteractionDistance = Mathf.Max(maximumInteractionDistance, interactionDistance);
            if (string.IsNullOrWhiteSpace(primaryInteractionKey)
                || string.CompareOrdinal(interactionKey, primaryInteractionKey) < 0)
            {
                primaryInteractionKey = interactionKey;
            }

            ControllersByInteractionKey[interactionKey] = this;
        }

        public void FaceInteractor(string interactionKey, Transform actor, string actorCharacterId)
        {
            if (actor == null || !interactionDistancesByKey.ContainsKey(interactionKey))
            {
                return;
            }

            Vector3 actorPosition = actor.position;
            ApplyFacingPosition(actorPosition);

            string sessionId = MMOGameplaySessionService.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(actorCharacterId))
            {
                return;
            }

            MMOSharedSessionState.UpsertNpcFacingSnapshot(new MMONpcFacingSnapshot
            {
                sessionId = sessionId,
                npcInteractionKey = primaryInteractionKey,
                actorCharacterId = actorCharacterId,
                actorPosition = new RPGClone.CharacterSelection.Vector3SaveData(actorPosition),
                updatedUtcTicks = System.DateTime.UtcNow.Ticks
            });
        }

        public static bool IsValidRemoteInteraction(MMONpcFacingSnapshot snapshot)
        {
            if (snapshot == null
                || !IsValidInteractionKey(snapshot.npcInteractionKey)
                || string.IsNullOrWhiteSpace(snapshot.actorCharacterId)
                || !string.Equals(snapshot.sessionId, MMOGameplaySessionService.SessionId, System.StringComparison.Ordinal)
                || !ControllersByInteractionKey.TryGetValue(snapshot.npcInteractionKey, out MMONpcInteractionFacing controller)
                || controller == null
                || !MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(
                    snapshot.actorCharacterId,
                    out MMOPlayerParticipant participant)
                || participant.GameObject == null)
            {
                return false;
            }

            float maximumDistance = controller.maximumInteractionDistance + AuthorityDistanceTolerance;
            Vector3 offset = participant.GameObject.transform.position - controller.transform.position;
            return offset.sqrMagnitude <= maximumDistance * maximumDistance;
        }

        public static bool TryApplySharedSnapshot(MMONpcFacingSnapshot snapshot)
        {
            if (snapshot == null
                || !ControllersByInteractionKey.TryGetValue(snapshot.npcInteractionKey, out MMONpcInteractionFacing controller)
                || controller == null)
            {
                return false;
            }

            controller.ApplyFacingPosition(snapshot.actorPosition.ToVector3());
            return true;
        }

        public static bool TryGetPlanarFacingRotation(
            Vector3 origin,
            Vector3 target,
            out Quaternion rotation)
        {
            Vector3 direction = target - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }

        private static string CreateInteractionKey(string interactionType, string identifier)
        {
            return $"{interactionType}:{identifier?.Trim()}";
        }

        private static bool IsValidInteractionKey(string interactionKey)
        {
            return !string.IsNullOrWhiteSpace(interactionKey)
                && interactionKey.Length <= MaximumInteractionKeyLength;
        }

        private void Update()
        {
            if (!hasTargetRotation)
            {
                return;
            }

            if (turnSpeedDegreesPerSecond <= 0f)
            {
                transform.rotation = targetRotation;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void OnDestroy()
        {
            foreach (string interactionKey in interactionDistancesByKey.Keys)
            {
                if (ControllersByInteractionKey.TryGetValue(interactionKey, out MMONpcInteractionFacing controller)
                    && controller == this)
                {
                    ControllersByInteractionKey.Remove(interactionKey);
                }
            }
        }

        private void ApplyFacingPosition(Vector3 actorPosition)
        {
            if (TryGetPlanarFacingRotation(transform.position, actorPosition, out Quaternion rotation))
            {
                targetRotation = rotation;
                hasTargetRotation = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ControllersByInteractionKey.Clear();
        }
    }
}
