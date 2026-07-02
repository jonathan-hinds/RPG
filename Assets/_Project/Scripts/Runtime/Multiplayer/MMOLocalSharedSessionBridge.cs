using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Services;
using RPGClone.Social;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCharacterPersistenceAgent))]
    public sealed class MMOLocalSharedSessionBridge : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float publishSeconds = 0.2f;
        [SerializeField, Min(0.1f)] private float pollSeconds = 0.25f;

        private readonly Dictionary<string, MMORemotePlayerAvatar> remoteAvatarsByCharacterId = new();
        private readonly HashSet<string> appliedEventIds = new();
        private MMOCharacterIdentity identity;
        private MMOCharacterPersistenceAgent persistenceAgent;
        private float nextPublishTime;
        private float nextPollTime;
        private string localCharacterId;
        private bool suppressStoreRemoval;

        public void SuppressStoreRemoval()
        {
            suppressStoreRemoval = true;
        }

        private void Awake()
        {
            identity = GetComponent<MMOCharacterIdentity>();
            persistenceAgent = GetComponent<MMOCharacterPersistenceAgent>();
        }

        private void OnEnable()
        {
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            MMOCombatEventStream.HealResolved += OnHealResolved;
        }

        private void OnDisable()
        {
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            if (!suppressStoreRemoval && !string.IsNullOrWhiteSpace(localCharacterId))
            {
                MMOLocalSharedSessionStore.RemoveParticipant(MMOGameplaySessionService.SessionId, localCharacterId);
            }
        }

        private void Update()
        {
            if (!MMOCharacterSession.HasSelectedCharacter || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            localCharacterId = MMOCharacterSession.SelectedCharacter.characterId;
            if (Time.unscaledTime >= nextPublishTime)
            {
                nextPublishTime = Time.unscaledTime + publishSeconds;
                PublishLocalSnapshot();
            }

            if (Time.unscaledTime >= nextPollTime)
            {
                nextPollTime = Time.unscaledTime + pollSeconds;
                PollSession();
            }
        }

        private void PublishLocalSnapshot()
        {
            MMOCharacterSaveData saveData = persistenceAgent.CaptureCurrentCharacterData();
            saveData.sceneName = SceneManager.GetActiveScene().name;
            saveData.position = new Vector3SaveData(transform.position);
            saveData.rotationEuler = new Vector3SaveData(transform.eulerAngles);
            string participantId = string.IsNullOrWhiteSpace(MMOSocialIdentityService.SessionId)
                ? "local-player"
                : MMOSocialIdentityService.SessionId;
            MMOGameplaySessionService.RegisterLocalPlayer(gameObject, saveData.characterId, participantId);

            MMOLocalSharedSessionStore.UpsertParticipant(new MMOSessionParticipantSnapshot
            {
                participantId = participantId,
                characterId = saveData.characterId,
                accountId = saveData.accountId,
                sessionId = MMOGameplaySessionService.SessionId,
                sceneName = saveData.sceneName,
                isHost = MMOGameplaySessionService.IsHostAuthority,
                characterData = saveData
            });
        }

        private void PollSession()
        {
            IReadOnlyList<MMOSessionParticipantSnapshot> participants = MMOLocalSharedSessionStore.GetParticipants(MMOGameplaySessionService.SessionId);
            HashSet<string> seenRemoteCharacters = new();
            foreach (MMOSessionParticipantSnapshot participant in participants)
            {
                if (participant == null || participant.characterData == null || participant.characterId == localCharacterId)
                {
                    continue;
                }

                seenRemoteCharacters.Add(participant.characterId);
                if (!remoteAvatarsByCharacterId.TryGetValue(participant.characterId, out MMORemotePlayerAvatar avatar) || avatar == null)
                {
                    avatar = SpawnRemoteAvatar(participant);
                    remoteAvatarsByCharacterId[participant.characterId] = avatar;
                }
                else
                {
                    avatar.ApplySnapshot(participant);
                }
            }

            RemoveMissingRemoteAvatars(seenRemoteCharacters);
            ApplyPendingAbilityEvents();
        }

        private MMORemotePlayerAvatar SpawnRemoteAvatar(MMOSessionParticipantSnapshot participant)
        {
            GameObject remoteObject = Instantiate(gameObject, participant.characterData.position.ToVector3(), Quaternion.Euler(participant.characterData.rotationEuler.ToVector3()));
            remoteObject.name = $"Remote Player - {participant.characterData.DisplayName}";

            MMORemotePlayerAvatar avatar = remoteObject.GetComponent<MMORemotePlayerAvatar>() ?? remoteObject.AddComponent<MMORemotePlayerAvatar>();
            avatar.Configure(participant);
            return avatar;
        }

        private void RemoveMissingRemoteAvatars(HashSet<string> seenRemoteCharacters)
        {
            List<string> missing = new();
            foreach (KeyValuePair<string, MMORemotePlayerAvatar> pair in remoteAvatarsByCharacterId)
            {
                if (!seenRemoteCharacters.Contains(pair.Key))
                {
                    missing.Add(pair.Key);
                }
            }

            foreach (string characterId in missing)
            {
                if (remoteAvatarsByCharacterId.TryGetValue(characterId, out MMORemotePlayerAvatar avatar) && avatar != null)
                {
                    Destroy(avatar.gameObject);
                }

                remoteAvatarsByCharacterId.Remove(characterId);
            }
        }

        private void ApplyPendingAbilityEvents()
        {
            IReadOnlyList<MMOSharedAbilityEvent> events = MMOLocalSharedSessionStore.GetPendingEvents(MMOGameplaySessionService.SessionId, localCharacterId);
            foreach (MMOSharedAbilityEvent sharedEvent in events)
            {
                if (sharedEvent == null || appliedEventIds.Contains(sharedEvent.eventId))
                {
                    continue;
                }

                if (sharedEvent.healAmount > 0 && identity.Health.CurrentValue > 0)
                {
                    identity.Health.SetCurrent(identity.Health.CurrentValue + sharedEvent.healAmount);
                }

                appliedEventIds.Add(sharedEvent.eventId);
                MMOLocalSharedSessionStore.MarkEventApplied(sharedEvent.eventId, localCharacterId);
            }
        }

        private void OnHealResolved(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (source == null || target == null || amount <= 0 || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            MMOCharacterIdentity sourceIdentity = source.Identity;
            MMOCharacterIdentity targetIdentity = target.Identity;
            if (sourceIdentity == null || targetIdentity == null)
            {
                return;
            }

            if (!MMOGameplaySessionService.Players.TryGetParticipant(sourceIdentity, out MMOPlayerParticipant sourceParticipant)
                || !MMOGameplaySessionService.Players.TryGetParticipant(targetIdentity, out MMOPlayerParticipant targetParticipant)
                || targetParticipant.IsLocal)
            {
                return;
            }

            MMOLocalSharedSessionStore.PublishHealEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetParticipant.CharacterId,
                ability != null ? ability.AbilityId : string.Empty,
                amount);
        }
    }
}
