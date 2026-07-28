using System;
using System.Collections.Generic;
using System.Text;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Services;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    public static class MMONetcodeSharedSessionTransport
    {
        private const string OperationMessageName = "rpg_clone_shared_session_operation";
        private const string SnapshotMessageName = "rpg_clone_shared_session_snapshot";
        private const string ParticipantRuntimeMessageName = "rpg_clone_participant_runtime";
        private const string EnemyRuntimeMessageName = "rpg_clone_enemy_runtime";
        private const int MaximumRuntimeIdentifierLength = 64;
        private const int MaximumEnemySpawnIdentifierLength = 512;
        private const int MaximumAbilityIdentifierLength = 128;
        private const int MaximumRememberedConsumableRequests = 4096;
        private const int MaximumJsonPayloadBytes = 2 * 1024 * 1024;
        private const int RuntimeSnapshotWriterCapacity = (MaximumRuntimeIdentifierLength * 2) + 64;
        private const NetworkDelivery SharedSessionDelivery = NetworkDelivery.ReliableFragmentedSequenced;
        private const NetworkDelivery RuntimeSnapshotDelivery = NetworkDelivery.UnreliableSequenced;
        private static NetworkManager registeredManager;
        private static readonly Dictionary<ulong, string> HostCharacterIdsByClientId = new();
        private static readonly HashSet<string> ProcessedConsumableRequestIds = new();
        private static readonly Queue<string> ProcessedConsumableRequestOrder = new();
        private static bool applyingRemoteOperation;
        private static bool applyingSnapshot;
        private static bool applyingRemoteRuntimeSnapshot;
        private static bool applyingRemoteEnemySnapshot;

        public static bool IsApplyingRemoteOperation => applyingRemoteOperation;
        public static bool IsApplyingSnapshot => applyingSnapshot;
        public static bool HasRemoteClients
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsListening)
                {
                    return false;
                }

                foreach (ulong clientId in manager.ConnectedClientsIds)
                {
                    if (clientId != manager.LocalClientId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool ShouldSubmitToHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsClient
            && !NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsConnectedClient
            && !applyingRemoteOperation
            && !applyingSnapshot
            && !applyingRemoteRuntimeSnapshot
            && !applyingRemoteEnemySnapshot;

        private static bool ShouldBroadcastFromHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsListening
            && !applyingRemoteOperation
            && !applyingSnapshot
            && !applyingRemoteRuntimeSnapshot
            && !applyingRemoteEnemySnapshot;

        public static void Initialize()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.CustomMessagingManager == null || registeredManager == manager)
            {
                return;
            }

            if (registeredManager != null && registeredManager.CustomMessagingManager != null)
            {
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(OperationMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(ParticipantRuntimeMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(EnemyRuntimeMessageName);
            }

            registeredManager = manager;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(OperationMessageName, OnOperationMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessageName, OnSnapshotMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ParticipantRuntimeMessageName, OnParticipantRuntimeMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(EnemyRuntimeMessageName, OnEnemyRuntimeMessage);
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public static void ResetRegistration()
        {
            if (registeredManager == null)
            {
                return;
            }

            if (registeredManager.CustomMessagingManager != null)
            {
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(OperationMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(ParticipantRuntimeMessageName);
                registeredManager.CustomMessagingManager.UnregisterNamedMessageHandler(EnemyRuntimeMessageName);
            }

            registeredManager.OnClientConnectedCallback -= OnClientConnected;
            registeredManager.OnClientDisconnectCallback -= OnClientDisconnected;
            registeredManager = null;
            HostCharacterIdsByClientId.Clear();
            ProcessedConsumableRequestIds.Clear();
            ProcessedConsumableRequestOrder.Clear();
            applyingRemoteRuntimeSnapshot = false;
            applyingRemoteEnemySnapshot = false;
        }

        public static bool TrySubmitToHost(MMOSharedSessionNetworkOperation operation)
        {
            Initialize();
            if (operation == null)
            {
                return false;
            }

            string json = JsonUtility.ToJson(operation, false);
            if (ShouldSubmitToHost)
            {
                SendJsonToServer(OperationMessageName, json, SharedSessionDelivery);
                return true;
            }

            if (ShouldBroadcastFromHost)
            {
                BroadcastOperationJsonIfHost(json);
            }

            return false;
        }

        public static bool TrySubmitParticipantRuntime(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            Initialize();
            if (!IsValidRuntimeSnapshot(snapshot))
            {
                Debug.LogWarning("Rejected participant runtime snapshot with invalid session or character identity.");
                return ShouldSubmitToHost;
            }

            if (ShouldSubmitToHost)
            {
                SendParticipantRuntimeToServer(snapshot);
                return true;
            }

            if (ShouldBroadcastFromHost)
            {
                BroadcastParticipantRuntimeIfHost(snapshot);
            }

            return false;
        }

        public static bool TryBroadcastEnemyRuntime(EnemySnapshot snapshot)
        {
            Initialize();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null
                || !manager.IsHost
                || !manager.IsListening
                || applyingRemoteEnemySnapshot
                || !IsValidEnemyRuntimeSnapshot(snapshot))
            {
                return false;
            }

            bool sent = false;
            using FastBufferWriter writer = CreateEnemyRuntimeWriter(snapshot);
            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId == manager.LocalClientId)
                {
                    continue;
                }

                manager.CustomMessagingManager.SendNamedMessage(
                    EnemyRuntimeMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.Unreliable);
                sent = true;
            }

            return sent;
        }

        public static void BroadcastSnapshotIfHost(string snapshotJson)
        {
            Initialize();
            NetworkManager manager = NetworkManager.Singleton;
            if (string.IsNullOrWhiteSpace(snapshotJson)
                || manager == null
                || !manager.IsHost
                || applyingRemoteOperation
                || applyingSnapshot)
            {
                return;
            }

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId != manager.LocalClientId)
                {
                    SendJsonToClient(SnapshotMessageName, clientId, snapshotJson);
                }
            }
        }

        public static void BroadcastOperationIfHost(MMOSharedSessionNetworkOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            Initialize();
            BroadcastOperationJsonIfHost(JsonUtility.ToJson(operation, false));
        }

        public static void RequestSnapshotFromHost()
        {
            Initialize();
            if (!ShouldSubmitToHost)
            {
                return;
            }

            SendJsonToServer(
                OperationMessageName,
                JsonUtility.ToJson(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.RequestSnapshot
                }, false),
                SharedSessionDelivery);
        }

        private static void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsHost || clientId == manager.LocalClientId)
            {
                return;
            }

            string snapshotJson = MMOSharedSessionState.CreateNetworkSnapshotJson();
            if (!string.IsNullOrWhiteSpace(snapshotJson))
            {
                SendJsonToClient(SnapshotMessageName, clientId, snapshotJson);
            }
        }

        private static void OnOperationMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            string json = ReadJson(reader);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            MMOSharedSessionNetworkOperation operation = JsonUtility.FromJson<MMOSharedSessionNetworkOperation>(json);
            if (!manager.IsHost && senderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"Rejected shared session operation from non-host client {senderClientId}.");
                return;
            }

            if (manager.IsHost
                && operation != null
                && operation.kind == MMOSharedSessionNetworkOperationKind.RequestSnapshot)
            {
                SendSnapshotToClient(senderClientId);
                return;
            }

            if (manager.IsHost && !IsOperationAllowedFromClient(senderClientId, operation))
            {
                Debug.LogWarning($"Rejected shared session operation '{operation?.kind}' from client {senderClientId}.");
                return;
            }

            if (manager.IsHost)
            {
                NormalizeOperationTimestampsForReceiver(operation, true);
                if (operation != null
                    && operation.kind == MMOSharedSessionNetworkOperationKind.RequestConsumableUse)
                {
                    MMOConsumableUseRequest request = operation.consumableUseRequest;
                    if (request == null
                        || string.IsNullOrWhiteSpace(request.requestId)
                        || request.requestId.Length > MaximumRuntimeIdentifierLength
                        || !TryRegisterConsumableRequest(request.requestId))
                    {
                        Debug.LogWarning($"Rejected invalid or duplicate consumable request from client {senderClientId}.");
                        return;
                    }

                    if (!MMOConsumableRewardAuthority.TryProcessHostRequest(request))
                    {
                        Debug.LogWarning($"Rejected consumable request '{request.requestId}' from client {senderClientId}.");
                    }

                    return;
                }

                if (operation != null
                    && operation.kind == MMOSharedSessionNetworkOperationKind.UpsertCorpseLootSnapshot)
                {
                    if (!HostCharacterIdsByClientId.TryGetValue(senderClientId, out string characterId)
                        || !MMOSharedSessionState.TryApplyPersonalLootUpdate(
                            operation.corpseLootSnapshot,
                            characterId,
                            out MMOCorpseLootState authoritativeSnapshot))
                    {
                        Debug.LogWarning($"Rejected invalid corpse loot update from client {senderClientId}.");
                        return;
                    }

                    operation.corpseLootSnapshot = authoritativeSnapshot;
                    BroadcastOperationJsonIfHost(JsonUtility.ToJson(operation, false));
                    return;
                }

                json = JsonUtility.ToJson(operation, false);
            }
            else
            {
                NormalizeOperationTimestampsForReceiver(operation, false);
                json = JsonUtility.ToJson(operation, false);
            }

            try
            {
                applyingRemoteOperation = true;
                MMOSharedSessionState.ApplyNetworkOperation(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to apply shared session network operation. {exception.Message}");
            }
            finally
            {
                applyingRemoteOperation = false;
            }

            if (manager.IsHost)
            {
                TrackClientParticipant(senderClientId, operation);
                BroadcastOperationJsonIfHost(json);
            }
        }

        private static void OnParticipantRuntimeMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !TryReadParticipantRuntime(reader, out MMOSessionParticipantRuntimeSnapshot snapshot))
            {
                return;
            }

            if (!string.Equals(snapshot.sessionId, MMOGameplaySessionService.SessionId, StringComparison.Ordinal))
            {
                Debug.LogWarning($"Rejected participant runtime snapshot for session '{snapshot.sessionId}'.");
                return;
            }

            if (manager.IsHost)
            {
                if (senderClientId == manager.LocalClientId
                    || !IsRegisteredSenderParticipant(senderClientId, snapshot.characterId))
                {
                    Debug.LogWarning($"Rejected participant runtime snapshot from client {senderClientId}.");
                    return;
                }

                ApplyAuthoritativeParticipantResources(snapshot);
                ApplyParticipantRuntimeSnapshot(snapshot);
                HostCharacterIdsByClientId[senderClientId] = snapshot.characterId;
                BroadcastParticipantRuntimeIfHost(snapshot, senderClientId);
                return;
            }

            if (senderClientId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"Rejected participant runtime snapshot from non-host client {senderClientId}.");
                return;
            }

            ApplyParticipantRuntimeSnapshot(snapshot);
        }

        private static void OnEnemyRuntimeMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null
                || manager.IsHost
                || senderClientId != NetworkManager.ServerClientId
                || !TryReadEnemyRuntime(reader, out EnemySnapshot snapshot))
            {
                return;
            }

            if (!string.Equals(snapshot.sessionId, MMOGameplaySessionService.SessionId, StringComparison.Ordinal))
            {
                Debug.LogWarning($"Rejected enemy runtime snapshot for session '{snapshot.sessionId}'.");
                return;
            }

            ApplyEnemyRuntimeSnapshot(snapshot);
        }

        private static void OnClientDisconnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null
                && manager.IsHost
                && HostCharacterIdsByClientId.TryGetValue(clientId, out string characterId))
            {
                MMOSharedSessionState.RemoveParticipant(MMOGameplaySessionService.SessionId, characterId);
            }

            HostCharacterIdsByClientId.Remove(clientId);
        }

        private static bool TryRegisterConsumableRequest(string requestId)
        {
            if (!ProcessedConsumableRequestIds.Add(requestId))
            {
                return false;
            }

            ProcessedConsumableRequestOrder.Enqueue(requestId);
            while (ProcessedConsumableRequestOrder.Count > MaximumRememberedConsumableRequests)
            {
                ProcessedConsumableRequestIds.Remove(ProcessedConsumableRequestOrder.Dequeue());
            }

            return true;
        }

        private static bool IsOperationAllowedFromClient(ulong senderClientId, MMOSharedSessionNetworkOperation operation)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (operation == null)
            {
                return false;
            }

            if (manager == null || !manager.IsHost || senderClientId == manager.LocalClientId)
            {
                return true;
            }

            return operation.kind switch
            {
                MMOSharedSessionNetworkOperationKind.UpsertParticipant
                    => IsSenderParticipant(senderClientId, operation.participant?.characterId),
                MMOSharedSessionNetworkOperationKind.RemoveParticipant
                    => IsRegisteredSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.PublishAbilityEvent
                    => IsRegisteredSenderParticipant(senderClientId, operation.abilityEvent?.casterCharacterId),
                MMOSharedSessionNetworkOperationKind.MarkAbilityEventApplied
                    => IsRegisteredSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.PublishCombatRequest
                    => IsRegisteredSenderParticipant(senderClientId, operation.combatRequest?.casterCharacterId),
                MMOSharedSessionNetworkOperationKind.MarkCombatEventApplied
                    => IsRegisteredSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.MarkRewardEventApplied
                    => IsRegisteredSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.UpsertCorpseLootSnapshot
                    => IsSenderCorpseLootParticipant(senderClientId, operation.corpseLootSnapshot),
                MMOSharedSessionNetworkOperationKind.PublishWorldObjectInteractionRequest
                    => IsRegisteredSenderParticipant(senderClientId, operation.worldObjectInteractionRequest?.actorCharacterId),
                MMOSharedSessionNetworkOperationKind.UpsertNpcFacingSnapshot
                    => IsRegisteredSenderParticipant(senderClientId, operation.npcFacingSnapshot?.actorCharacterId)
                        && MMONpcInteractionFacing.IsValidRemoteInteraction(operation.npcFacingSnapshot),
                MMOSharedSessionNetworkOperationKind.RequestConsumableUse
                    => IsRegisteredSenderParticipant(senderClientId, operation.consumableUseRequest?.characterId),
                _ => false
            };
        }

        private static bool IsSenderParticipant(ulong senderClientId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            return !HostCharacterIdsByClientId.TryGetValue(senderClientId, out string knownCharacterId)
                || string.Equals(knownCharacterId, characterId, StringComparison.Ordinal);
        }

        private static bool IsRegisteredSenderParticipant(ulong senderClientId, string characterId)
        {
            return !string.IsNullOrWhiteSpace(characterId)
                && HostCharacterIdsByClientId.TryGetValue(senderClientId, out string knownCharacterId)
                && string.Equals(knownCharacterId, characterId, StringComparison.Ordinal);
        }

        private static bool IsSenderCorpseLootParticipant(ulong senderClientId, MMOCorpseLootState snapshot)
        {
            if (snapshot?.personalLoot == null
                || !HostCharacterIdsByClientId.TryGetValue(senderClientId, out string characterId))
            {
                return false;
            }

            return snapshot.personalLoot.Exists(candidate =>
                candidate != null && string.Equals(candidate.characterId, characterId, StringComparison.Ordinal));
        }

        private static void NormalizeOperationTimestampsForReceiver(
            MMOSharedSessionNetworkOperation operation,
            bool normalizeClientAuthoredState)
        {
            if (operation == null)
            {
                return;
            }

            long receivedUtcTicks = DateTime.UtcNow.Ticks;
            if (normalizeClientAuthoredState
                && operation.participant?.characterData != null
                && MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(
                    operation.participant.characterId,
                    out MMOPlayerParticipant participant)
                && participant.Identity != null)
            {
                operation.participant.characterData.currentHealth = participant.Identity.Health.CurrentValue;
                operation.participant.characterData.currentMana = participant.Identity.Mana.CurrentValue;
            }

            if (operation.abilityEvent != null)
            {
                operation.abilityEvent.createdUtcTicks = receivedUtcTicks;
            }

            if (operation.combatRequest != null)
            {
                operation.combatRequest.requestedUtcTicks = receivedUtcTicks;
            }

            if (operation.combatEvent != null)
            {
                operation.combatEvent.createdUtcTicks = receivedUtcTicks;
            }

            if (operation.rewardEvent != null)
            {
                operation.rewardEvent.createdUtcTicks = receivedUtcTicks;
            }

            if (operation.worldObjectInteractionRequest != null)
            {
                operation.worldObjectInteractionRequest.requestedUtcTicks = receivedUtcTicks;
            }

            if (operation.worldObjectSnapshot != null)
            {
                operation.worldObjectSnapshot.updatedUtcTicks = receivedUtcTicks;
            }

            if (operation.npcFacingSnapshot != null)
            {
                operation.npcFacingSnapshot.updatedUtcTicks = receivedUtcTicks;
                if (normalizeClientAuthoredState
                    && MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(
                        operation.npcFacingSnapshot.actorCharacterId,
                        out MMOPlayerParticipant facingActor)
                    && facingActor.GameObject != null)
                {
                    operation.npcFacingSnapshot.actorPosition =
                        new Vector3SaveData(facingActor.GameObject.transform.position);
                }
            }

            if (operation.worldObjectSnapshots != null)
            {
                foreach (RPGClone.Quests.MMOSharedWorldObjectSnapshot snapshot in operation.worldObjectSnapshots)
                {
                    if (snapshot != null)
                    {
                        snapshot.updatedUtcTicks = receivedUtcTicks;
                    }
                }
            }

            if (operation.corpseLootSnapshot != null)
            {
                operation.corpseLootSnapshot.updatedUtcTicks = receivedUtcTicks;
            }
        }

        private static void ApplyAuthoritativeParticipantResources(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            if (snapshot == null
                || !MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(
                    snapshot.characterId,
                    out MMOPlayerParticipant participant)
                || participant.Identity == null)
            {
                return;
            }

            snapshot.currentHealth = participant.Identity.Health.CurrentValue;
            snapshot.currentMana = participant.Identity.Mana.CurrentValue;
        }

        private static void TrackClientParticipant(ulong senderClientId, MMOSharedSessionNetworkOperation operation)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || senderClientId == manager.LocalClientId)
            {
                return;
            }

            string characterId = operation?.participant?.characterId
                ?? operation?.combatRequest?.casterCharacterId
                ?? operation?.abilityEvent?.casterCharacterId
                ?? operation?.worldObjectInteractionRequest?.actorCharacterId
                ?? operation?.characterId;
            if (!string.IsNullOrWhiteSpace(characterId))
            {
                HostCharacterIdsByClientId[senderClientId] = characterId;
            }
        }

        private static void OnSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsHost || senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            string json = ReadJson(reader);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                applyingSnapshot = true;
                MMOSharedSessionState.ApplyNetworkSnapshot(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to apply shared session network snapshot. {exception.Message}");
            }
            finally
            {
                applyingSnapshot = false;
            }
        }

        private static void SendJsonToServer(string messageName, string json, NetworkDelivery delivery)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null)
            {
                return;
            }

            if (!IsValidJsonPayloadSize(json))
            {
                return;
            }

            using FastBufferWriter writer = CreateWriter(json);
            manager.CustomMessagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer, delivery);
        }

        private static void SendParticipantRuntimeToServer(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null)
            {
                return;
            }

            using FastBufferWriter writer = CreateParticipantRuntimeWriter(snapshot);
            manager.CustomMessagingManager.SendNamedMessage(
                ParticipantRuntimeMessageName,
                NetworkManager.ServerClientId,
                writer,
                RuntimeSnapshotDelivery);
        }

        private static void SendJsonToClient(
            string messageName,
            ulong clientId,
            string json,
            NetworkDelivery delivery = SharedSessionDelivery)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null)
            {
                return;
            }

            if (!IsValidJsonPayloadSize(json))
            {
                return;
            }

            using FastBufferWriter writer = CreateWriter(json);
            manager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer, delivery);
        }

        private static void SendSnapshotToClient(ulong clientId)
        {
            string snapshotJson = MMOSharedSessionState.CreateNetworkSnapshotJson();
            if (!string.IsNullOrWhiteSpace(snapshotJson))
            {
                SendJsonToClient(SnapshotMessageName, clientId, snapshotJson);
            }
        }

        private static void BroadcastOperationJsonIfHost(string json)
        {
            Initialize();
            NetworkManager manager = NetworkManager.Singleton;
            if (string.IsNullOrWhiteSpace(json)
                || manager == null
                || !manager.IsHost
                || !manager.IsListening
                || applyingRemoteOperation
                || applyingSnapshot)
            {
                return;
            }

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId != manager.LocalClientId)
                {
                    SendJsonToClient(OperationMessageName, clientId, json);
                }
            }
        }

        private static void BroadcastParticipantRuntimeIfHost(
            MMOSessionParticipantRuntimeSnapshot snapshot,
            ulong excludedClientId = ulong.MaxValue)
        {
            Initialize();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null
                || !manager.IsHost
                || !manager.IsListening
                || applyingRemoteRuntimeSnapshot)
            {
                return;
            }

            using FastBufferWriter writer = CreateParticipantRuntimeWriter(snapshot);
            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId == manager.LocalClientId || clientId == excludedClientId)
                {
                    continue;
                }

                manager.CustomMessagingManager.SendNamedMessage(
                    ParticipantRuntimeMessageName,
                    clientId,
                    writer,
                    RuntimeSnapshotDelivery);
            }
        }

        private static void ApplyParticipantRuntimeSnapshot(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            try
            {
                applyingRemoteRuntimeSnapshot = true;
                MMOSharedSessionState.UpsertParticipantRuntime(
                    snapshot.sessionId,
                    snapshot.characterId,
                    snapshot.position.ToVector3(),
                    snapshot.rotationEuler.ToVector3(),
                    snapshot.currentHealth,
                    snapshot.currentMana);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to apply participant runtime snapshot. {exception.Message}");
            }
            finally
            {
                applyingRemoteRuntimeSnapshot = false;
            }
        }

        private static void ApplyEnemyRuntimeSnapshot(EnemySnapshot snapshot)
        {
            try
            {
                applyingRemoteEnemySnapshot = true;
                if (MMOEnemyController.TryGetEnemy(snapshot.spawnId, out MMOEnemyController enemy) && enemy != null)
                {
                    enemy.ApplySnapshot(snapshot);
                }
                else
                {
                    MMOSharedSessionState.UpsertEnemySnapshot(snapshot);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to apply enemy runtime snapshot. {exception.Message}");
            }
            finally
            {
                applyingRemoteEnemySnapshot = false;
            }
        }

        private static FastBufferWriter CreateParticipantRuntimeWriter(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            FastBufferWriter writer = new(RuntimeSnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(snapshot.sessionId, true);
            writer.WriteValueSafe(snapshot.characterId, true);

            Vector3 position = snapshot.position.ToVector3();
            Vector3 rotationEuler = snapshot.rotationEuler.ToVector3();
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(rotationEuler);
            writer.WriteValueSafe(snapshot.currentHealth);
            writer.WriteValueSafe(snapshot.currentMana);
            writer.WriteValueSafe(snapshot.updatedUtcTicks);
            return writer;
        }

        private static bool TryReadParticipantRuntime(
            FastBufferReader reader,
            out MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                reader.ReadValueSafe(out string sessionId, true);
                reader.ReadValueSafe(out string characterId, true);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out Vector3 rotationEuler);
                reader.ReadValueSafe(out int currentHealth);
                reader.ReadValueSafe(out int currentMana);
                reader.ReadValueSafe(out long updatedUtcTicks);

                snapshot = new MMOSessionParticipantRuntimeSnapshot
                {
                    sessionId = sessionId,
                    characterId = characterId,
                    position = new Vector3SaveData(position),
                    rotationEuler = new Vector3SaveData(rotationEuler),
                    currentHealth = currentHealth,
                    currentMana = currentMana,
                    updatedUtcTicks = updatedUtcTicks
                };
                return IsValidRuntimeSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Rejected malformed participant runtime snapshot. {exception.Message}");
                return false;
            }
        }

        private static bool IsValidRuntimeSnapshot(MMOSessionParticipantRuntimeSnapshot snapshot)
        {
            if (snapshot == null
                || !IsValidRuntimeIdentifier(snapshot.sessionId)
                || !IsValidRuntimeIdentifier(snapshot.characterId)
                || snapshot.currentHealth < 0
                || snapshot.currentMana < 0
                || snapshot.updatedUtcTicks <= 0)
            {
                return false;
            }

            return IsFinite(snapshot.position.ToVector3())
                && IsFinite(snapshot.rotationEuler.ToVector3());
        }

        private static FastBufferWriter CreateEnemyRuntimeWriter(EnemySnapshot snapshot)
        {
            int stringCapacity = snapshot.sessionId.Length
                + snapshot.spawnId.Length
                + (snapshot.currentTargetCharacterId?.Length ?? 0)
                + (snapshot.castAbilityId?.Length ?? 0)
                + (snapshot.castTargetCharacterId?.Length ?? 0);
            FastBufferWriter writer = new(stringCapacity + 160, Allocator.Temp);
            writer.WriteValueSafe(snapshot.sessionId, true);
            writer.WriteValueSafe(snapshot.spawnId, true);
            writer.WriteValueSafe((int)snapshot.runtimeState);
            writer.WriteValueSafe(snapshot.currentHealth);
            writer.WriteValueSafe(snapshot.maxHealth);
            writer.WriteValueSafe(snapshot.currentMana);
            writer.WriteValueSafe(snapshot.maxMana);
            writer.WriteValueSafe(snapshot.position.ToVector3());
            writer.WriteValueSafe(snapshot.rotationEuler.ToVector3());
            writer.WriteValueSafe(snapshot.worldSpeed);
            writer.WriteValueSafe(snapshot.currentTargetCharacterId ?? string.Empty, true);
            writer.WriteValueSafe(snapshot.inCombat);
            writer.WriteValueSafe(snapshot.leashing);
            writer.WriteValueSafe(snapshot.leashAnchorPosition.ToVector3());
            writer.WriteValueSafe(snapshot.castAbilityId ?? string.Empty, true);
            writer.WriteValueSafe(snapshot.castTargetCharacterId ?? string.Empty, true);
            writer.WriteValueSafe(snapshot.castDurationSeconds);
            writer.WriteValueSafe(snapshot.castNormalizedProgress);
            writer.WriteValueSafe(snapshot.corpseRemainingSeconds);
            writer.WriteValueSafe(snapshot.respawnRemainingSeconds);
            writer.WriteValueSafe(snapshot.updatedUtcTicks);
            return writer;
        }

        private static bool TryReadEnemyRuntime(FastBufferReader reader, out EnemySnapshot snapshot)
        {
            snapshot = null;
            try
            {
                reader.ReadValueSafe(out string sessionId, true);
                reader.ReadValueSafe(out string spawnId, true);
                reader.ReadValueSafe(out int runtimeState);
                reader.ReadValueSafe(out int currentHealth);
                reader.ReadValueSafe(out int maxHealth);
                reader.ReadValueSafe(out int currentMana);
                reader.ReadValueSafe(out int maxMana);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out Vector3 rotationEuler);
                reader.ReadValueSafe(out float worldSpeed);
                reader.ReadValueSafe(out string currentTargetCharacterId, true);
                reader.ReadValueSafe(out bool inCombat);
                reader.ReadValueSafe(out bool leashing);
                reader.ReadValueSafe(out Vector3 leashAnchorPosition);
                reader.ReadValueSafe(out string castAbilityId, true);
                reader.ReadValueSafe(out string castTargetCharacterId, true);
                reader.ReadValueSafe(out float castDurationSeconds);
                reader.ReadValueSafe(out float castNormalizedProgress);
                reader.ReadValueSafe(out float corpseRemainingSeconds);
                reader.ReadValueSafe(out float respawnRemainingSeconds);
                reader.ReadValueSafe(out long updatedUtcTicks);

                snapshot = new EnemySnapshot
                {
                    sessionId = sessionId,
                    spawnId = spawnId,
                    runtimeState = (EnemyRuntimeState)runtimeState,
                    currentHealth = currentHealth,
                    maxHealth = maxHealth,
                    currentMana = currentMana,
                    maxMana = maxMana,
                    position = new Vector3SaveData(position),
                    rotationEuler = new Vector3SaveData(rotationEuler),
                    worldSpeed = worldSpeed,
                    currentTargetCharacterId = currentTargetCharacterId,
                    inCombat = inCombat,
                    leashing = leashing,
                    leashAnchorPosition = new Vector3SaveData(leashAnchorPosition),
                    castAbilityId = castAbilityId,
                    castTargetCharacterId = castTargetCharacterId,
                    castDurationSeconds = castDurationSeconds,
                    castNormalizedProgress = castNormalizedProgress,
                    corpseRemainingSeconds = corpseRemainingSeconds,
                    respawnRemainingSeconds = respawnRemainingSeconds,
                    updatedUtcTicks = updatedUtcTicks
                };
                return IsValidEnemyRuntimeSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Rejected malformed enemy runtime snapshot. {exception.Message}");
                return false;
            }
        }

        private static bool IsValidEnemyRuntimeSnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null
                || !IsValidRuntimeIdentifier(snapshot.sessionId)
                || !IsValidRuntimeIdentifier(snapshot.spawnId, MaximumEnemySpawnIdentifierLength)
                || !IsValidOptionalRuntimeIdentifier(snapshot.currentTargetCharacterId, MaximumRuntimeIdentifierLength)
                || !IsValidOptionalRuntimeIdentifier(snapshot.castAbilityId, MaximumAbilityIdentifierLength)
                || !IsValidOptionalRuntimeIdentifier(snapshot.castTargetCharacterId, MaximumRuntimeIdentifierLength)
                || !Enum.IsDefined(typeof(EnemyRuntimeState), snapshot.runtimeState)
                || snapshot.currentHealth < 0
                || snapshot.maxHealth < 0
                || snapshot.currentMana < 0
                || snapshot.maxMana < 0
                || snapshot.worldSpeed < 0f
                || snapshot.updatedUtcTicks <= 0)
            {
                return false;
            }

            return IsFinite(snapshot.position.ToVector3())
                && IsFinite(snapshot.rotationEuler.ToVector3())
                && IsFinite(snapshot.leashAnchorPosition.ToVector3())
                && IsFinite(snapshot.worldSpeed)
                && IsFinite(snapshot.castDurationSeconds)
                && IsFinite(snapshot.castNormalizedProgress)
                && IsFinite(snapshot.corpseRemainingSeconds)
                && IsFinite(snapshot.respawnRemainingSeconds);
        }

        private static bool IsValidRuntimeIdentifier(string value)
        {
            return IsValidRuntimeIdentifier(value, MaximumRuntimeIdentifierLength);
        }

        private static bool IsValidOptionalRuntimeIdentifier(string value, int maximumLength)
        {
            return string.IsNullOrEmpty(value) || IsValidRuntimeIdentifier(value, maximumLength);
        }

        private static bool IsValidRuntimeIdentifier(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z)
                && !float.IsInfinity(value.z);
        }

        private static FastBufferWriter CreateWriter(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            FastBufferWriter writer = new(sizeof(int) + bytes.Length, Allocator.Temp);
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            return writer;
        }

        private static bool IsValidJsonPayloadSize(string json)
        {
            int byteCount = Encoding.UTF8.GetByteCount(json ?? string.Empty);
            if (byteCount <= MaximumJsonPayloadBytes)
            {
                return true;
            }

            Debug.LogError($"Shared-session payload exceeded the {MaximumJsonPayloadBytes}-byte safety limit.");
            return false;
        }

        private static string ReadJson(FastBufferReader reader)
        {
            try
            {
                reader.ReadValueSafe(out int byteCount);
                if (byteCount <= 0
                    || byteCount > MaximumJsonPayloadBytes
                    || byteCount > reader.Length - reader.Position)
                {
                    Debug.LogWarning($"Rejected malformed shared-session JSON payload length {byteCount}.");
                    return string.Empty;
                }

                byte[] bytes = new byte[byteCount];
                reader.ReadBytesSafe(ref bytes, byteCount);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Rejected malformed shared-session JSON payload. {exception.Message}");
                return string.Empty;
            }
        }
    }
}
