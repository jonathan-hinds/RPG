using System;
using System.Collections.Generic;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Services
{
    public sealed class MMOPlayerRegistry
    {
        private readonly Dictionary<MMOCharacterIdentity, MMOPlayerParticipant> participantsByIdentity = new();
        private readonly List<MMOPlayerParticipant> participants = new();

        public event Action Changed;
        public IReadOnlyList<MMOPlayerParticipant> Participants => participants;

        public bool TryGetParticipant(MMOCharacterIdentity identity, out MMOPlayerParticipant participant)
        {
            if (identity == null)
            {
                participant = default;
                return false;
            }

            return participantsByIdentity.TryGetValue(identity, out participant);
        }

        public bool Contains(MMOCharacterIdentity identity)
        {
            return identity != null && participantsByIdentity.ContainsKey(identity);
        }

        public void Register(MMOPlayerParticipant participant)
        {
            if (!participant.IsValid)
            {
                return;
            }

            if (participantsByIdentity.TryGetValue(participant.Identity, out MMOPlayerParticipant existing)
                && existing.ParticipantId == participant.ParticipantId
                && existing.CharacterId == participant.CharacterId
                && existing.IsLocal == participant.IsLocal
                && existing.IsHostAuthority == participant.IsHostAuthority)
            {
                return;
            }

            Unregister(participant.Identity, false);
            participantsByIdentity[participant.Identity] = participant;
            participants.Add(participant);
            Changed?.Invoke();
        }

        public void Unregister(MMOCharacterIdentity identity)
        {
            Unregister(identity, true);
        }

        public void RemoveInvalidParticipants()
        {
            bool changed = false;
            for (int i = participants.Count - 1; i >= 0; i--)
            {
                MMOPlayerParticipant participant = participants[i];
                if (participant.Identity != null)
                {
                    continue;
                }

                participants.RemoveAt(i);
                changed = true;
            }

            foreach (MMOCharacterIdentity identity in new List<MMOCharacterIdentity>(participantsByIdentity.Keys))
            {
                if (identity == null)
                {
                    participantsByIdentity.Remove(identity);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (participants.Count == 0 && participantsByIdentity.Count == 0)
            {
                return;
            }

            participants.Clear();
            participantsByIdentity.Clear();
            Changed?.Invoke();
        }

        private void Unregister(MMOCharacterIdentity identity, bool raiseChanged)
        {
            if (identity == null || !participantsByIdentity.Remove(identity))
            {
                return;
            }

            participants.RemoveAll(participant => participant.Identity == identity);
            if (raiseChanged)
            {
                Changed?.Invoke();
            }
        }
    }
}
