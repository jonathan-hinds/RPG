using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.World
{
    public static class MMOPlayerMapMarkerProvider
    {
        private static readonly Color PlayerMarkerColor = new(0.22f, 1f, 0.36f, 1f);

        public static void AddRemotePlayerMarkers(List<MMOMapMarkerData> markers)
        {
            if (markers == null)
            {
                return;
            }

            foreach (MMOPlayerParticipant participant in MMOGameplaySessionService.Players.Participants)
            {
                if (!IsTrackableRemotePlayer(participant, out MMOCharacterIdentity identity))
                {
                    continue;
                }

                Transform playerTransform = identity.transform;
                markers.Add(new MMOMapMarkerData(
                    CreateMarkerId(participant, identity),
                    identity.DisplayName,
                    string.Empty,
                    playerTransform.position,
                    0f,
                    MMOMapMarkerType.PlayerCharacter,
                    PlayerMarkerColor,
                    false,
                    playerTransform.eulerAngles.y));
            }
        }

        private static bool IsTrackableRemotePlayer(MMOPlayerParticipant participant, out MMOCharacterIdentity identity)
        {
            identity = participant.Identity;
            if (!participant.IsValid
                || participant.IsLocal
                || identity == null
                || !identity.gameObject.activeInHierarchy)
            {
                return false;
            }

            MMOCharacterIdentity localIdentity = MMOGameplaySessionService.LocalPlayer.Identity;
            return localIdentity == null || identity != localIdentity;
        }

        private static string CreateMarkerId(MMOPlayerParticipant participant, MMOCharacterIdentity identity)
        {
            if (!string.IsNullOrWhiteSpace(participant.CharacterId))
            {
                return $"player_{participant.CharacterId}";
            }

            if (!string.IsNullOrWhiteSpace(participant.ParticipantId))
            {
                return $"player_{participant.ParticipantId}";
            }

            return $"player_{identity.GetHashCode()}";
        }
    }
}
