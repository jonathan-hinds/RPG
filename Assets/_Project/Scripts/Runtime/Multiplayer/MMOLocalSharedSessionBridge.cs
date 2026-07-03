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
        [SerializeField, Min(0.25f)] private float publishSeconds = 1f;
        [SerializeField, Min(0.05f)] private float runtimePublishSeconds = 0.05f;
        [SerializeField, Min(0.05f)] private float pollSeconds = 0.05f;

        private readonly Dictionary<string, MMORemotePlayerAvatar> remoteAvatarsByCharacterId = new();
        private readonly HashSet<string> appliedEventIds = new();
        private MMOCharacterIdentity identity;
        private MMOCharacterPersistenceAgent persistenceAgent;
        private MMOAbilitySystem abilitySystem;
        private string participantId;
        private float nextPublishTime;
        private float nextRuntimePublishTime;
        private float nextPollTime;
        private string localCharacterId;
        private string observedSessionId;
        private bool suppressStoreRemoval;

        public void SuppressStoreRemoval()
        {
            suppressStoreRemoval = true;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            identity = GetComponent<MMOCharacterIdentity>();
            persistenceAgent = GetComponent<MMOCharacterPersistenceAgent>();
            abilitySystem = GetComponent<MMOAbilitySystem>();
        }

        private void OnEnable()
        {
            SubscribeAbilitySystem();
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            MMOCombatEventStream.HealResolved += OnHealResolved;
            MMOGameplaySessionService.SessionChanged -= OnSessionChanged;
            MMOGameplaySessionService.SessionChanged += OnSessionChanged;
        }

        private void OnDisable()
        {
            UnsubscribeAbilitySystem();
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            MMOGameplaySessionService.SessionChanged -= OnSessionChanged;
            if (!suppressStoreRemoval && !string.IsNullOrWhiteSpace(localCharacterId))
            {
                MMOLocalSharedSessionStore.RemoveParticipant(MMOGameplaySessionService.SessionId, localCharacterId);
            }

            ClearRemoteAvatars();
        }

        private void Update()
        {
            if (!MMOCharacterSession.HasSelectedCharacter || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            if (observedSessionId != MMOGameplaySessionService.SessionId)
            {
                observedSessionId = MMOGameplaySessionService.SessionId;
                ClearRemoteAvatars();
            }

            localCharacterId = MMOCharacterSession.SelectedCharacter.characterId;
            if (Time.unscaledTime >= nextPublishTime)
            {
                nextPublishTime = Time.unscaledTime + publishSeconds;
                PublishLocalCharacterSnapshot();
            }

            if (Time.unscaledTime >= nextRuntimePublishTime)
            {
                nextRuntimePublishTime = Time.unscaledTime + runtimePublishSeconds;
                PublishLocalRuntimeSnapshot();
            }

            if (Time.unscaledTime >= nextPollTime)
            {
                nextPollTime = Time.unscaledTime + pollSeconds;
                PollSession();
            }
        }

        private void PublishLocalCharacterSnapshot()
        {
            MMOCharacterSaveData saveData = persistenceAgent.CaptureCurrentCharacterData();
            saveData.sceneName = SceneManager.GetActiveScene().name;
            saveData.position = new Vector3SaveData(transform.position);
            saveData.rotationEuler = new Vector3SaveData(transform.eulerAngles);
            participantId = ResolveParticipantId();
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

        private void PublishLocalRuntimeSnapshot()
        {
            if (string.IsNullOrWhiteSpace(localCharacterId))
            {
                return;
            }

            participantId = ResolveParticipantId();
            MMOGameplaySessionService.RegisterLocalPlayer(gameObject, localCharacterId, participantId);
            MMOLocalSharedSessionStore.UpsertParticipantRuntime(
                MMOGameplaySessionService.SessionId,
                localCharacterId,
                transform.position,
                transform.eulerAngles,
                identity.Health.CurrentValue,
                identity.Mana.CurrentValue);
        }

        private static string ResolveParticipantId()
        {
            return string.IsNullOrWhiteSpace(MMOSocialIdentityService.SessionId)
                ? "local-player"
                : MMOSocialIdentityService.SessionId;
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
            MMORemotePlayerAvatar existingAvatar = FindExistingRemoteAvatar(participant.characterId);
            if (existingAvatar != null)
            {
                existingAvatar.Configure(participant);
                return existingAvatar;
            }

            GameObject remoteObject = Instantiate(gameObject, participant.characterData.position.ToVector3(), Quaternion.Euler(participant.characterData.rotationEuler.ToVector3()));
            remoteObject.name = $"Remote Player - {participant.characterData.DisplayName}";

            MMORemotePlayerAvatar avatar = remoteObject.GetComponent<MMORemotePlayerAvatar>() ?? remoteObject.AddComponent<MMORemotePlayerAvatar>();
            avatar.Configure(participant);
            return avatar;
        }

        private MMORemotePlayerAvatar FindExistingRemoteAvatar(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            MMORemotePlayerAvatar[] avatars = FindObjectsByType<MMORemotePlayerAvatar>(FindObjectsInactive.Exclude);
            foreach (MMORemotePlayerAvatar avatar in avatars)
            {
                if (avatar != null && avatar.CharacterId == characterId)
                {
                    return avatar;
                }
            }

            return null;
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

                if (ApplySharedAbilityEvent(sharedEvent))
                {
                    appliedEventIds.Add(sharedEvent.eventId);
                    MMOLocalSharedSessionStore.MarkEventApplied(sharedEvent.eventId, localCharacterId);
                }
            }
        }

        private bool ApplySharedAbilityEvent(MMOSharedAbilityEvent sharedEvent)
        {
            if (!TryResolveParticipantByCharacterId(sharedEvent.casterCharacterId, out MMOPlayerParticipant casterParticipant))
            {
                return false;
            }

            MMOPlayerParticipant targetParticipant = default;
            MMOCharacterIdentity targetIdentity = null;
            if (!string.IsNullOrWhiteSpace(sharedEvent.targetCharacterId)
                && TryResolveParticipantByCharacterId(sharedEvent.targetCharacterId, out targetParticipant))
            {
                targetIdentity = targetParticipant.Identity;
            }

            MMOAbilitySystem casterAbilitySystem = casterParticipant.GameObject != null
                ? casterParticipant.GameObject.GetComponent<MMOAbilitySystem>()
                : null;
            MMOAbilitySystem targetAbilitySystem = targetParticipant.GameObject != null
                ? targetParticipant.GameObject.GetComponent<MMOAbilitySystem>()
                : null;
            MMOAbilityDefinition ability = ResolveAbility(sharedEvent.abilityId, casterAbilitySystem, targetAbilitySystem);
            string eventType = string.IsNullOrWhiteSpace(sharedEvent.eventType)
                ? MMOSharedAbilityEventTypes.HealResolved
                : sharedEvent.eventType;

            switch (eventType)
            {
                case MMOSharedAbilityEventTypes.CastStarted:
                    if (casterAbilitySystem == null || ability == null)
                    {
                        return false;
                    }

                    casterAbilitySystem.PlayReplicatedCastStarted(ability, targetIdentity, sharedEvent.castDurationSeconds);
                    return true;

                case MMOSharedAbilityEventTypes.AbilityReleased:
                    if (casterAbilitySystem == null || ability == null)
                    {
                        return false;
                    }

                    Vector3 targetPosition = sharedEvent.hasGroundTarget
                        ? sharedEvent.targetPosition.ToVector3()
                        : targetIdentity != null
                            ? targetIdentity.transform.position
                            : sharedEvent.targetPosition.ToVector3();
                    casterAbilitySystem.PlayReplicatedAbilityReleased(ability, targetIdentity, targetPosition, sharedEvent.hasGroundTarget);
                    return true;

                case MMOSharedAbilityEventTypes.HealResolved:
                    if (sharedEvent.healAmount <= 0 || targetParticipant.GameObject == null)
                    {
                        return false;
                    }

                    MMOCombatant sourceCombatant = casterParticipant.GameObject != null
                        ? casterParticipant.GameObject.GetComponent<MMOCombatant>()
                        : null;
                    MMOCombatant targetCombatant = targetParticipant.GameObject.GetComponent<MMOCombatant>();
                    if (targetCombatant == null)
                    {
                        return false;
                    }

                    targetCombatant.ApplyHeal(sourceCombatant, ability, sharedEvent.healAmount, false);
                    return true;

                default:
                    return true;
            }
        }

        private MMOAbilityDefinition ResolveAbility(string abilityId, params MMOAbilitySystem[] systems)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            foreach (MMOAbilitySystem system in systems)
            {
                MMOAbilityDefinition ability = FindKnownAbility(system, abilityId);
                if (ability != null)
                {
                    return ability;
                }
            }

            foreach (MMOAbilitySystem system in systems)
            {
                MMOAbilityDefinition ability = FindAbilityThroughPersistence(system, abilityId);
                if (ability != null)
                {
                    return ability;
                }
            }

            MMOAbilityDefinition localKnownAbility = FindKnownAbility(abilitySystem, abilityId);
            return localKnownAbility != null ? localKnownAbility : persistenceAgent != null ? persistenceAgent.FindAbilityById(abilityId) : null;
        }

        private static MMOAbilityDefinition FindKnownAbility(MMOAbilitySystem system, string abilityId)
        {
            if (system == null)
            {
                return null;
            }

            foreach (MMOAbilityDefinition ability in system.KnownAbilities)
            {
                if (ability != null && ability.AbilityId == abilityId)
                {
                    return ability;
                }
            }

            return null;
        }

        private static MMOAbilityDefinition FindAbilityThroughPersistence(MMOAbilitySystem system, string abilityId)
        {
            if (system == null || string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            MMOCharacterPersistenceAgent agent = system.GetComponent<MMOCharacterPersistenceAgent>();
            return agent != null ? agent.FindAbilityById(abilityId) : null;
        }

        private void SubscribeAbilitySystem()
        {
            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.CastStarted -= OnLocalCastStarted;
            abilitySystem.CastStarted += OnLocalCastStarted;
            abilitySystem.AbilityReleased -= OnLocalAbilityReleased;
            abilitySystem.AbilityReleased += OnLocalAbilityReleased;
        }

        private void UnsubscribeAbilitySystem()
        {
            if (abilitySystem == null)
            {
                return;
            }

            abilitySystem.CastStarted -= OnLocalCastStarted;
            abilitySystem.AbilityReleased -= OnLocalAbilityReleased;
        }

        private void OnLocalCastStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            if (!TryResolveSharedPlayerAbility(source, ability, target, out MMOPlayerParticipant sourceParticipant, out string targetCharacterId))
            {
                return;
            }

            MMOLocalSharedSessionStore.PublishCastStartedEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetCharacterId,
                ability.AbilityId,
                duration,
                sourceParticipant.CharacterId);
        }

        private void OnLocalAbilityReleased(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (!TryResolveSharedPlayerAbility(source, ability, target, out MMOPlayerParticipant sourceParticipant, out string targetCharacterId))
            {
                return;
            }

            MMOLocalSharedSessionStore.PublishAbilityReleasedEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetCharacterId,
                ability.AbilityId,
                targetPosition,
                hasGroundTarget,
                sourceParticipant.CharacterId);
        }

        private bool TryResolveSharedPlayerAbility(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            out MMOPlayerParticipant sourceParticipant,
            out string targetCharacterId)
        {
            sourceParticipant = default;
            targetCharacterId = string.Empty;
            if (target != null && TryResolveParticipant(target, out MMOPlayerParticipant targetParticipant))
            {
                targetCharacterId = targetParticipant.CharacterId;
            }

            if (source != abilitySystem
                || ability == null
                || ability.IsAutoAttack
                || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId)
                || !TryResolveParticipant(identity, out sourceParticipant)
                || !sourceParticipant.IsLocal
                || !HasRemoteParticipants())
            {
                return false;
            }

            return true;
        }

        private bool HasRemoteParticipants()
        {
            if (remoteAvatarsByCharacterId.Count > 0)
            {
                return true;
            }

            foreach (MMOPlayerParticipant participant in MMOGameplaySessionService.Players.Participants)
            {
                if (participant.IsValid && !participant.IsLocal)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveParticipant(MMOCharacterIdentity candidate, out MMOPlayerParticipant participant)
        {
            if (MMOGameplaySessionService.Players.TryGetParticipant(candidate, out participant))
            {
                return true;
            }

            if (candidate == null)
            {
                participant = default;
                return false;
            }

            MMORemotePlayerAvatar remoteAvatar = candidate.GetComponent<MMORemotePlayerAvatar>();
            if (remoteAvatar != null && !string.IsNullOrWhiteSpace(remoteAvatar.CharacterId))
            {
                participant = new MMOPlayerParticipant(
                    remoteAvatar.ParticipantId,
                    remoteAvatar.CharacterId,
                    false,
                    false,
                    candidate);
                return true;
            }

            if (candidate == identity && !string.IsNullOrWhiteSpace(localCharacterId))
            {
                participant = new MMOPlayerParticipant(
                    ResolveParticipantId(),
                    localCharacterId,
                    true,
                    MMOGameplaySessionService.IsHostAuthority,
                    candidate);
                return true;
            }

            participant = default;
            return false;
        }

        private bool TryResolveParticipantByCharacterId(string characterId, out MMOPlayerParticipant participant)
        {
            if (MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(characterId, out participant))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                participant = default;
                return false;
            }

            if (characterId == localCharacterId && identity != null)
            {
                participant = new MMOPlayerParticipant(
                    ResolveParticipantId(),
                    localCharacterId,
                    true,
                    MMOGameplaySessionService.IsHostAuthority,
                    identity);
                return true;
            }

            if (remoteAvatarsByCharacterId.TryGetValue(characterId, out MMORemotePlayerAvatar remoteAvatar)
                && remoteAvatar != null
                && remoteAvatar.TryGetComponent(out MMOCharacterIdentity remoteIdentity))
            {
                participant = new MMOPlayerParticipant(
                    remoteAvatar.ParticipantId,
                    remoteAvatar.CharacterId,
                    false,
                    false,
                    remoteIdentity);
                return true;
            }

            participant = default;
            return false;
        }

        private void OnSessionChanged()
        {
            observedSessionId = MMOGameplaySessionService.SessionId;
            ClearRemoteAvatars();
        }

        private void ClearRemoteAvatars()
        {
            foreach (KeyValuePair<string, MMORemotePlayerAvatar> pair in remoteAvatarsByCharacterId)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            remoteAvatarsByCharacterId.Clear();
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

            if (!TryResolveParticipant(sourceIdentity, out MMOPlayerParticipant sourceParticipant)
                || !TryResolveParticipant(targetIdentity, out MMOPlayerParticipant targetParticipant)
                || targetParticipant.IsLocal)
            {
                return;
            }

            MMOLocalSharedSessionStore.PublishHealEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetParticipant.CharacterId,
                ability != null ? ability.AbilityId : string.Empty,
                amount,
                sourceParticipant.CharacterId);
        }
    }
}
