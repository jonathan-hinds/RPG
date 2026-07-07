using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Loot;
using RPGClone.Quests;
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
        [SerializeField, Min(0.1f)] private float idlePollSeconds = 0.25f;
        [SerializeField, Min(0.25f)] private float participantRosterPollSeconds = 1f;
        [SerializeField, Min(0.05f)] private float enemySnapshotPublishSeconds = 0.1f;
        [SerializeField, Min(0.05f)] private float enemySnapshotPollSeconds = 0.1f;
        [SerializeField, Min(0.5f)] private float fullEnemySnapshotPublishSeconds = 2f;
        [SerializeField, Min(0.1f)] private float worldObjectSnapshotPublishSeconds = 0.25f;

        private readonly Dictionary<string, MMORemotePlayerAvatar> remoteAvatarsByCharacterId = new();
        private readonly HashSet<string> appliedEventIds = new();
        private readonly HashSet<string> seenRemoteCharacters = new();
        private readonly Dictionary<string, long> appliedEnemySnapshotTicks = new();
        private readonly Dictionary<string, long> appliedCorpseLootSnapshotTicks = new();
        private readonly Dictionary<string, int> publishedEnemySnapshotSignatures = new();
        private readonly List<EnemySnapshot> enemySnapshotBuffer = new();
        private readonly List<MMOSharedWorldObjectSnapshot> worldObjectSnapshotBuffer = new();
        private readonly List<string> missingRemoteCharacters = new();
        private MMOCharacterIdentity identity;
        private MMOCharacterPersistenceAgent persistenceAgent;
        private MMOAbilitySystem abilitySystem;
        private MMOAutoAttackController autoAttackController;
        private string participantId;
        private float nextPublishTime;
        private float nextRuntimePublishTime;
        private float nextPollTime;
        private float nextParticipantRosterPollTime;
        private float nextEnemySnapshotPublishTime;
        private float nextEnemySnapshotPollTime;
        private float nextFullEnemySnapshotPublishTime;
        private float nextWorldObjectSnapshotPublishTime;
        private string localCharacterId;
        private string observedSessionId;
        private bool suppressStoreRemoval;
        private bool hasSessionPeers;

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
            autoAttackController = GetComponent<MMOAutoAttackController>();
        }

        private void OnEnable()
        {
            SubscribeAbilitySystem();
            SubscribeAutoAttackController();
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            MMOCombatEventStream.HealResolved += OnHealResolved;
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
            MMOGameplaySessionService.SessionChanged -= OnSessionChanged;
            MMOGameplaySessionService.SessionChanged += OnSessionChanged;
        }

        private void OnDisable()
        {
            UnsubscribeAbilitySystem();
            UnsubscribeAutoAttackController();
            MMOCombatEventStream.HealResolved -= OnHealResolved;
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
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
                appliedEnemySnapshotTicks.Clear();
                appliedCorpseLootSnapshotTicks.Clear();
                publishedEnemySnapshotSignatures.Clear();
                hasSessionPeers = false;
            }

            localCharacterId = MMOCharacterSession.SelectedCharacter.characterId;
            if (Time.unscaledTime >= nextPublishTime)
            {
                nextPublishTime = Time.unscaledTime + publishSeconds;
                PublishLocalCharacterSnapshot();
            }

            if (HasKnownSessionPeers() && Time.unscaledTime >= nextRuntimePublishTime)
            {
                nextRuntimePublishTime = Time.unscaledTime + runtimePublishSeconds;
                PublishLocalRuntimeSnapshot();
            }

            if (MMOGameplaySessionService.IsHostAuthority
                && Time.unscaledTime >= nextEnemySnapshotPublishTime)
            {
                nextEnemySnapshotPublishTime = Time.unscaledTime + enemySnapshotPublishSeconds;
                PublishEnemySnapshots();
            }

            if (MMOGameplaySessionService.IsHostAuthority
                && HasKnownSessionPeers()
                && Time.unscaledTime >= nextWorldObjectSnapshotPublishTime)
            {
                nextWorldObjectSnapshotPublishTime = Time.unscaledTime + worldObjectSnapshotPublishSeconds;
                PublishWorldObjectSnapshots();
            }

            if (Time.unscaledTime >= nextPollTime)
            {
                nextPollTime = Time.unscaledTime + (HasKnownSessionPeers() ? pollSeconds : idlePollSeconds);
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
            if (Time.unscaledTime >= nextParticipantRosterPollTime || !hasSessionPeers || remoteAvatarsByCharacterId.Count == 0)
            {
                nextParticipantRosterPollTime = Time.unscaledTime + participantRosterPollSeconds;
                PollParticipantRoster();
            }
            else
            {
                ApplyParticipantRuntimeSnapshots();
            }

            if (!hasSessionPeers)
            {
                return;
            }

            if (MMOGameplaySessionService.IsHostAuthority)
            {
                ProcessPendingCombatRequests();
                ProcessPendingWorldObjectRequests();
            }

            if (!MMOGameplaySessionService.IsHostAuthority
                && Time.unscaledTime >= nextEnemySnapshotPollTime)
            {
                nextEnemySnapshotPollTime = Time.unscaledTime + enemySnapshotPollSeconds;
                ApplyEnemySnapshots();
            }

            ApplyPendingAbilityEvents();
            ApplyPendingCombatEvents();
            ApplyPendingRewardEvents();
            ApplyCorpseLootSnapshots();
            ApplyWorldObjectSnapshots();
        }

        private void PollParticipantRoster()
        {
            IReadOnlyList<MMOSessionParticipantSnapshot> participants = MMOLocalSharedSessionStore.GetParticipants(MMOGameplaySessionService.SessionId);
            seenRemoteCharacters.Clear();
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
            hasSessionPeers = participants.Count > 1 || HasRemoteParticipants();
        }

        private void ApplyParticipantRuntimeSnapshots()
        {
            IReadOnlyList<MMOSessionParticipantRuntimeSnapshot> snapshots = MMOLocalSharedSessionStore.GetParticipantRuntimeSnapshots(MMOGameplaySessionService.SessionId);
            bool sawRemote = false;
            foreach (MMOSessionParticipantRuntimeSnapshot snapshot in snapshots)
            {
                if (snapshot == null || snapshot.characterId == localCharacterId)
                {
                    continue;
                }

                sawRemote = true;
                if (remoteAvatarsByCharacterId.TryGetValue(snapshot.characterId, out MMORemotePlayerAvatar avatar) && avatar != null)
                {
                    avatar.ApplyRuntimeSnapshot(snapshot);
                }
            }

            hasSessionPeers = sawRemote || HasRemoteParticipants();
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
            if (remoteObject.TryGetComponent(out MMOCharacterPersistenceAgent remotePersistenceAgent))
            {
                remotePersistenceAgent.MarkAsRemoteSessionReplica();
            }

            if (remoteObject.TryGetComponent(out MMOLocalSharedSessionBridge remoteBridge))
            {
                remoteBridge.SuppressStoreRemoval();
            }

            remoteObject.tag = "Untagged";
            remoteObject.SetActive(false);
            remoteObject.name = $"Remote Player - {participant.characterData.DisplayName}";

            MMORemotePlayerAvatar avatar = remoteObject.GetComponent<MMORemotePlayerAvatar>() ?? remoteObject.AddComponent<MMORemotePlayerAvatar>();
            avatar.Configure(participant);
            remoteObject.SetActive(true);
            return avatar;
        }

        private void ProcessPendingCombatRequests()
        {
            IReadOnlyList<CombatActionRequest> requests = MMOLocalSharedSessionStore.GetPendingCombatRequests(MMOGameplaySessionService.SessionId);
            foreach (CombatActionRequest request in requests)
            {
                if (request == null)
                {
                    continue;
                }

                TryResolveCombatRequest(request, out _);
                MMOLocalSharedSessionStore.MarkCombatRequestProcessed(request.requestId);
            }
        }

        private bool TryResolveCombatRequest(CombatActionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if (!TryResolveParticipantByCharacterId(request.casterCharacterId, out MMOPlayerParticipant casterParticipant)
                || casterParticipant.GameObject == null)
            {
                failureReason = "Caster was not available on the host.";
                return false;
            }

            MMOAbilitySystem casterAbilitySystem = casterParticipant.GameObject.GetComponent<MMOAbilitySystem>();
            if (casterAbilitySystem == null)
            {
                failureReason = "Caster has no ability system.";
                return false;
            }

            MMOCharacterIdentity targetIdentity = ResolveCombatRequestTarget(request);
            return casterAbilitySystem.TryResolveAuthorityRequest(request, targetIdentity, out failureReason);
        }

        private void ProcessPendingWorldObjectRequests()
        {
            IReadOnlyList<MMOSharedWorldObjectInteractionRequest> requests = MMOLocalSharedSessionStore.GetPendingWorldObjectInteractionRequests(MMOGameplaySessionService.SessionId);
            foreach (MMOSharedWorldObjectInteractionRequest request in requests)
            {
                if (request == null)
                {
                    continue;
                }

                if (MMOSharedWorldObjectStateService.TryGetInteractable(request.worldObjectId, out MMOQuestWorldInteractable interactable))
                {
                    interactable.TryAuthorityConsumeFromRequest(request.actorCharacterId);
                }

                MMOLocalSharedSessionStore.MarkWorldObjectInteractionRequestProcessed(request.requestId);
            }
        }

        private MMOCharacterIdentity ResolveCombatRequestTarget(CombatActionRequest request)
        {
            if (request == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.targetEnemySpawnId)
                && MMOEnemyController.TryGetEnemy(request.targetEnemySpawnId, out MMOEnemyController enemy)
                && enemy != null)
            {
                return enemy.GetComponent<MMOCharacterIdentity>();
            }

            if (!string.IsNullOrWhiteSpace(request.targetCharacterId)
                && TryResolveParticipantByCharacterId(request.targetCharacterId, out MMOPlayerParticipant participant))
            {
                return participant.Identity;
            }

            return null;
        }

        private void PublishAuthorityAbilityRelease(CombatActionRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.abilityId))
            {
                return;
            }

            MMOCharacterIdentity targetIdentity = ResolveCombatRequestTarget(request);
            Vector3 targetPosition = request.hasGroundTarget
                ? request.requestedTargetPosition.ToVector3()
                : targetIdentity != null
                    ? targetIdentity.transform.position
                    : request.requestedTargetPosition.ToVector3();

            CombatEventRecord record = CombatEventRecord.Create(CombatEventType.AbilityReleased);
            record.sessionId = MMOGameplaySessionService.SessionId;
            record.sourceCharacterId = request.casterCharacterId;
            record.targetCharacterId = request.targetCharacterId;
            record.targetEnemySpawnId = request.targetEnemySpawnId;
            record.abilityId = request.abilityId;
            record.targetPosition = new Vector3SaveData(targetPosition);
            record.hasGroundTarget = request.hasGroundTarget;
            MMOLocalSharedSessionStore.PublishCombatEvent(record, localCharacterId);
        }

        private void PublishEnemySnapshots()
        {
            if (!HasRemoteParticipants())
            {
                return;
            }

            bool forceFullSnapshot = Time.unscaledTime >= nextFullEnemySnapshotPublishTime;
            if (forceFullSnapshot)
            {
                nextFullEnemySnapshotPublishTime = Time.unscaledTime + fullEnemySnapshotPublishSeconds;
            }

            enemySnapshotBuffer.Clear();
            foreach (MMOEnemyController enemy in MMOEnemyController.ActiveEnemies)
            {
                if (enemy != null)
                {
                    EnemySnapshot snapshot = enemy.CreateSnapshot();
                    int signature = CalculateEnemySnapshotSignature(snapshot);
                    if (forceFullSnapshot
                        || !publishedEnemySnapshotSignatures.TryGetValue(snapshot.spawnId, out int previousSignature)
                        || previousSignature != signature)
                    {
                        enemySnapshotBuffer.Add(snapshot);
                        publishedEnemySnapshotSignatures[snapshot.spawnId] = signature;
                    }
                }
            }

            if (enemySnapshotBuffer.Count > 0)
            {
                MMOLocalSharedSessionStore.UpsertEnemySnapshots(enemySnapshotBuffer);
            }
        }

        private void PublishWorldObjectSnapshots()
        {
            if (string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            worldObjectSnapshotBuffer.Clear();
            foreach (MMOQuestWorldInteractable interactable in MMOSharedWorldObjectStateService.ActiveInteractables)
            {
                if (interactable != null)
                {
                    worldObjectSnapshotBuffer.Add(interactable.CreateSharedSnapshot());
                }
            }

            if (worldObjectSnapshotBuffer.Count > 0)
            {
                MMOLocalSharedSessionStore.UpsertWorldObjectSnapshots(worldObjectSnapshotBuffer);
            }
        }

        private void ApplyEnemySnapshots()
        {
            if (MMOGameplaySessionService.IsHostAuthority)
            {
                return;
            }

            IReadOnlyList<EnemySnapshot> snapshots = MMOLocalSharedSessionStore.GetEnemySnapshots(MMOGameplaySessionService.SessionId);
            foreach (EnemySnapshot snapshot in snapshots)
            {
                if (snapshot != null
                    && MMOEnemyController.TryGetEnemy(snapshot.spawnId, out MMOEnemyController enemy)
                    && enemy != null
                    && (!appliedEnemySnapshotTicks.TryGetValue(snapshot.spawnId, out long appliedTicks)
                        || snapshot.updatedUtcTicks > appliedTicks))
                {
                    enemy.ApplySnapshot(snapshot);
                    appliedEnemySnapshotTicks[snapshot.spawnId] = snapshot.updatedUtcTicks;
                }
            }
        }

        private static int CalculateEnemySnapshotSignature(EnemySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            Vector3 position = snapshot.position.ToVector3();
            Vector3 rotation = snapshot.rotationEuler.ToVector3();
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StableHash(snapshot.spawnId);
                hash = hash * 31 + StableHash(snapshot.definitionId);
                hash = hash * 31 + (int)snapshot.runtimeState;
                hash = hash * 31 + snapshot.currentHealth;
                hash = hash * 31 + snapshot.maxHealth;
                hash = hash * 31 + snapshot.currentMana;
                hash = hash * 31 + snapshot.maxMana;
                hash = hash * 31 + Quantize(position.x, 0.05f);
                hash = hash * 31 + Quantize(position.y, 0.05f);
                hash = hash * 31 + Quantize(position.z, 0.05f);
                hash = hash * 31 + Quantize(rotation.y, 1f);
                hash = hash * 31 + Quantize(snapshot.worldSpeed, 0.05f);
                hash = hash * 31 + StableHash(snapshot.currentTargetCharacterId);
                hash = hash * 31 + (snapshot.inCombat ? 1 : 0);
                hash = hash * 31 + (snapshot.leashing ? 1 : 0);
                hash = hash * 31 + Quantize(snapshot.corpseRemainingSeconds, 0.25f);
                hash = hash * 31 + Quantize(snapshot.respawnRemainingSeconds, 0.25f);
                return hash;
            }
        }

        private static int Quantize(float value, float step)
        {
            return Mathf.RoundToInt(value / Mathf.Max(0.0001f, step));
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash;
            }
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
            missingRemoteCharacters.Clear();
            foreach (KeyValuePair<string, MMORemotePlayerAvatar> pair in remoteAvatarsByCharacterId)
            {
                if (!seenRemoteCharacters.Contains(pair.Key))
                {
                    missingRemoteCharacters.Add(pair.Key);
                }
            }

            foreach (string characterId in missingRemoteCharacters)
            {
                if (remoteAvatarsByCharacterId.TryGetValue(characterId, out MMORemotePlayerAvatar avatar) && avatar != null)
                {
                    Destroy(avatar.gameObject);
                }

                remoteAvatarsByCharacterId.Remove(characterId);
            }
        }

        private bool HasKnownSessionPeers()
        {
            return hasSessionPeers || remoteAvatarsByCharacterId.Count > 0;
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

        private void ApplyPendingCombatEvents()
        {
            IReadOnlyList<CombatEventRecord> events = MMOLocalSharedSessionStore.GetPendingCombatEvents(MMOGameplaySessionService.SessionId, localCharacterId);
            foreach (CombatEventRecord combatEvent in events)
            {
                if (combatEvent == null || appliedEventIds.Contains(combatEvent.eventId))
                {
                    continue;
                }

                if (ApplyCombatEvent(combatEvent))
                {
                    appliedEventIds.Add(combatEvent.eventId);
                    MMOLocalSharedSessionStore.MarkCombatEventApplied(combatEvent.eventId, localCharacterId);
                }
            }
        }

        private void ApplyPendingRewardEvents()
        {
            IReadOnlyList<MMOSharedRewardEvent> events = MMOLocalSharedSessionStore.GetPendingRewardEvents(MMOGameplaySessionService.SessionId, localCharacterId);
            foreach (MMOSharedRewardEvent rewardEvent in events)
            {
                if (rewardEvent == null || appliedEventIds.Contains(rewardEvent.eventId))
                {
                    continue;
                }

                if (ApplyRewardEvent(rewardEvent))
                {
                    appliedEventIds.Add(rewardEvent.eventId);
                    MMOLocalSharedSessionStore.MarkRewardEventApplied(rewardEvent.eventId, localCharacterId);
                    _ = persistenceAgent.SaveCurrentCharacterAsync();
                }
            }
        }

        private bool ApplyRewardEvent(MMOSharedRewardEvent rewardEvent)
        {
            if (rewardEvent == null || rewardEvent.targetCharacterId != localCharacterId)
            {
                return false;
            }

            switch (rewardEvent.eventType)
            {
                case MMOSharedRewardEventTypes.Experience:
                    if (rewardEvent.experienceAmount <= 0)
                    {
                        return true;
                    }

                    MMOExperienceComponent experience = GetComponent<MMOExperienceComponent>();
                    if (experience == null)
                    {
                        return false;
                    }

                    experience.AddExperience(rewardEvent.experienceAmount);
                    return true;

                case MMOSharedRewardEventTypes.QuestKillCredit:
                    MMOQuestLog questLog = GetComponent<MMOQuestLog>();
                    if (questLog == null)
                    {
                        return false;
                    }

                    MMOEnemyDefinition enemyDefinition = null;
                    if (!string.IsNullOrWhiteSpace(rewardEvent.enemySpawnId)
                        && MMOEnemyController.TryGetEnemy(rewardEvent.enemySpawnId, out MMOEnemyController enemy)
                        && enemy != null)
                    {
                        enemyDefinition = enemy.Definition;
                    }

                    questLog.RecordCreatureKilled(enemyDefinition, rewardEvent.creatureId);
                    return true;

                default:
                    return true;
            }
        }

        private void ApplyCorpseLootSnapshots()
        {
            IReadOnlyList<MMOCorpseLootState> snapshots = MMOLocalSharedSessionStore.GetCorpseLootSnapshots(MMOGameplaySessionService.SessionId);
            foreach (MMOCorpseLootState snapshot in snapshots)
            {
                if (snapshot == null
                    || string.IsNullOrWhiteSpace(snapshot.enemySpawnId)
                    || (appliedCorpseLootSnapshotTicks.TryGetValue(snapshot.enemySpawnId, out long appliedTicks)
                        && snapshot.updatedUtcTicks <= appliedTicks)
                    || !MMOEnemyController.TryGetEnemy(snapshot.enemySpawnId, out MMOEnemyController enemy)
                    || enemy == null
                    || !enemy.TryGetComponent(out MMOLootableCorpse corpse))
                {
                    continue;
                }

                corpse.ApplyPersonalLootSnapshot(snapshot);
                appliedCorpseLootSnapshotTicks[snapshot.enemySpawnId] = snapshot.updatedUtcTicks;
            }
        }

        private void ApplyWorldObjectSnapshots()
        {
            IReadOnlyList<MMOSharedWorldObjectSnapshot> snapshots = MMOLocalSharedSessionStore.GetWorldObjectSnapshots(MMOGameplaySessionService.SessionId);
            foreach (MMOSharedWorldObjectSnapshot snapshot in snapshots)
            {
                if (snapshot == null
                    || !MMOSharedWorldObjectStateService.TryGetInteractable(snapshot.worldObjectId, out MMOQuestWorldInteractable interactable))
                {
                    continue;
                }

                interactable.ApplySharedSnapshot(snapshot);
            }
        }

        private bool ApplyCombatEvent(CombatEventRecord combatEvent)
        {
            MMOCombatant sourceCombatant = ResolveCombatant(combatEvent.sourceCharacterId, combatEvent.sourceEnemySpawnId);
            MMOCombatant targetCombatant = ResolveCombatant(combatEvent.targetCharacterId, combatEvent.targetEnemySpawnId);
            MMOAbilityDefinition ability = ResolveAbility(
                combatEvent.abilityId,
                sourceCombatant != null ? sourceCombatant.GetComponent<MMOAbilitySystem>() : null,
                targetCombatant != null ? targetCombatant.GetComponent<MMOAbilitySystem>() : null);

            switch (combatEvent.eventType)
            {
                case CombatEventType.AbilityReleased:
                    if (sourceCombatant == null)
                    {
                        return false;
                    }

                    MMOAbilitySystem sourceAbilitySystem = sourceCombatant.GetComponent<MMOAbilitySystem>();
                    if (sourceAbilitySystem == null || ability == null)
                    {
                        return false;
                    }

                    sourceAbilitySystem.PlayReplicatedAbilityReleased(
                        ability,
                        targetCombatant != null ? targetCombatant.Identity : null,
                        combatEvent.targetPosition.ToVector3(),
                        combatEvent.hasGroundTarget);
                    return true;

                case CombatEventType.DamageResolved:
                    if (targetCombatant == null || combatEvent.damageAmount <= 0)
                    {
                        return false;
                    }

                    targetCombatant.ApplyResolvedDamage(sourceCombatant, ability, combatEvent.damageAmount, combatEvent.isCritical, false);
                    return true;

                case CombatEventType.HealResolved:
                    if (targetCombatant == null || combatEvent.healAmount <= 0)
                    {
                        return false;
                    }

                    targetCombatant.ApplyHeal(sourceCombatant, ability, combatEvent.healAmount, false);
                    return true;

                case CombatEventType.Missed:
                    if (targetCombatant == null)
                    {
                        return false;
                    }

                    targetCombatant.NotifyMiss(sourceCombatant, ability, false);
                    return true;

                case CombatEventType.Blocked:
                    if (targetCombatant == null || combatEvent.blockedAmount <= 0)
                    {
                        return false;
                    }

                    targetCombatant.NotifyBlock(sourceCombatant, ability, combatEvent.blockedAmount, false);
                    return true;

                case CombatEventType.Death:
                    return targetCombatant != null;

                default:
                    return true;
            }
        }

        private MMOCombatant ResolveCombatant(string characterId, string enemySpawnId)
        {
            if (!string.IsNullOrWhiteSpace(enemySpawnId)
                && MMOEnemyController.TryGetEnemy(enemySpawnId, out MMOEnemyController enemy)
                && enemy != null)
            {
                return enemy.GetComponent<MMOCombatant>();
            }

            if (!string.IsNullOrWhiteSpace(characterId)
                && TryResolveParticipantByCharacterId(characterId, out MMOPlayerParticipant participant)
                && participant.GameObject != null)
            {
                return participant.GameObject.GetComponent<MMOCombatant>();
            }

            return null;
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
            else if (!string.IsNullOrWhiteSpace(sharedEvent.targetEnemySpawnId)
                && MMOEnemyController.TryGetEnemy(sharedEvent.targetEnemySpawnId, out MMOEnemyController targetEnemy)
                && targetEnemy != null)
            {
                targetIdentity = targetEnemy.GetComponent<MMOCharacterIdentity>();
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

                case MMOSharedAbilityEventTypes.AutoAttackWindup:
                    if (ability == null || casterParticipant.GameObject == null)
                    {
                        return false;
                    }

                    MMOAutoAttackController replicatedAutoAttack = casterParticipant.GameObject.GetComponent<MMOAutoAttackController>();
                    IMMOAutoAttackPresentation presentation = casterParticipant.GameObject.GetComponent<IMMOAutoAttackPresentation>();
                    if (replicatedAutoAttack == null || presentation == null)
                    {
                        return false;
                    }

                    float swingDurationSeconds = Mathf.Max(0.1f, sharedEvent.castDurationSeconds);
                    presentation.NotifyAutoAttackWindup(
                        replicatedAutoAttack,
                        ability,
                        targetIdentity,
                        swingDurationSeconds,
                        Time.time + swingDurationSeconds);
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

        private void SubscribeAutoAttackController()
        {
            if (autoAttackController == null)
            {
                autoAttackController = GetComponent<MMOAutoAttackController>();
            }

            if (autoAttackController == null)
            {
                return;
            }

            autoAttackController.AutoAttackWindupStarted -= OnLocalAutoAttackWindupStarted;
            autoAttackController.AutoAttackWindupStarted += OnLocalAutoAttackWindupStarted;
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

        private void UnsubscribeAutoAttackController()
        {
            if (autoAttackController != null)
            {
                autoAttackController.AutoAttackWindupStarted -= OnLocalAutoAttackWindupStarted;
            }
        }

        private void OnLocalCastStarted(MMOAbilitySystem source, MMOAbilityDefinition ability, MMOCharacterIdentity target, float duration)
        {
            if (!TryResolveSharedPlayerAbility(
                    source,
                    ability,
                    target,
                    out MMOPlayerParticipant sourceParticipant,
                    out string targetCharacterId,
                    out string targetEnemySpawnId))
            {
                return;
            }

            MMOLocalSharedSessionStore.PublishCastStartedEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetCharacterId,
                ability.AbilityId,
                duration,
                sourceParticipant.CharacterId,
                targetEnemySpawnId);
        }

        private void OnLocalAbilityReleased(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (!TryResolveSharedPlayerAbility(
                    source,
                    ability,
                    target,
                    out MMOPlayerParticipant sourceParticipant,
                    out string targetCharacterId,
                    out string targetEnemySpawnId))
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
                sourceParticipant.CharacterId,
                targetEnemySpawnId);
        }

        private void OnLocalAutoAttackWindupStarted(
            MMOAutoAttackController source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float swingDurationSeconds,
            float impactTime)
        {
            if (source != autoAttackController
                || ability == null
                || !ability.IsAutoAttack
                || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId)
                || !TryResolveParticipant(identity, out MMOPlayerParticipant sourceParticipant)
                || !sourceParticipant.IsLocal)
            {
                return;
            }

            string targetCharacterId = string.Empty;
            string targetEnemySpawnId = string.Empty;
            if (target != null && TryResolveParticipant(target, out MMOPlayerParticipant targetParticipant))
            {
                targetCharacterId = targetParticipant.CharacterId;
            }

            MMOEnemyController targetEnemy = target != null ? target.GetComponent<MMOEnemyController>() : null;
            if (targetEnemy != null)
            {
                targetEnemySpawnId = targetEnemy.SpawnId;
            }

            MMOLocalSharedSessionStore.PublishAutoAttackWindupEvent(
                MMOGameplaySessionService.SessionId,
                sourceParticipant.CharacterId,
                targetCharacterId,
                targetEnemySpawnId,
                ability.AbilityId,
                swingDurationSeconds,
                sourceParticipant.CharacterId);
        }

        private bool TryResolveSharedPlayerAbility(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            out MMOPlayerParticipant sourceParticipant,
            out string targetCharacterId,
            out string targetEnemySpawnId)
        {
            sourceParticipant = default;
            targetCharacterId = string.Empty;
            targetEnemySpawnId = string.Empty;
            if (target != null && TryResolveParticipant(target, out MMOPlayerParticipant targetParticipant))
            {
                targetCharacterId = targetParticipant.CharacterId;
            }

            MMOEnemyController targetEnemy = target != null ? target.GetComponent<MMOEnemyController>() : null;
            if (targetEnemy != null)
            {
                targetEnemySpawnId = targetEnemy.SpawnId;
            }

            if (source != abilitySystem
                || ability == null
                || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId)
                || !TryResolveParticipant(identity, out sourceParticipant)
                || !sourceParticipant.IsLocal)
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

        private void OnCombatEventResolved(
            CombatEventRecord record,
            MMOCombatant source,
            MMOCombatant target,
            MMOAbilityDefinition ability)
        {
            if (!MMOGameplaySessionService.IsHostAuthority
                || record == null
                || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(record.sessionId))
            {
                record.sessionId = MMOGameplaySessionService.SessionId;
            }

            MMOLocalSharedSessionStore.PublishCombatEvent(record, localCharacterId);
        }
    }
}
