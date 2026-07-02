using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Services
{
    public readonly struct MMOPlayerParticipant
    {
        public MMOPlayerParticipant(
            string participantId,
            string characterId,
            bool isLocal,
            bool isHostAuthority,
            MMOCharacterIdentity identity)
        {
            ParticipantId = string.IsNullOrWhiteSpace(participantId) ? "local-player" : participantId;
            CharacterId = characterId ?? string.Empty;
            IsLocal = isLocal;
            IsHostAuthority = isHostAuthority;
            Identity = identity;
        }

        public string ParticipantId { get; }
        public string CharacterId { get; }
        public bool IsLocal { get; }
        public bool IsHostAuthority { get; }
        public MMOCharacterIdentity Identity { get; }
        public GameObject GameObject => Identity != null ? Identity.gameObject : null;
        public Transform Transform => Identity != null ? Identity.transform : null;
        public bool IsValid => Identity != null;
    }
}
