using System;
using System.Collections.Generic;
using System.Text;
using RPGClone.CharacterSelection;
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
        private const int MaximumRuntimeIdentifierLength = 64;
        private const int RuntimeSnapshotWriterCapacity = (MaximumRuntimeIdentifierLength * 2) + 64;
        private const NetworkDelivery SharedSessionDelivery = NetworkDelivery.ReliableFragmentedSequenced;
        private const NetworkDelivery RuntimeSnapshotDelivery = NetworkDelivery.UnreliableSequenced;
        private static NetworkManager registeredManager;
        private static readonly Dictionary<ulong, string> HostCharacterIdsByClientId = new();
        private static bool applyingRemoteOperation;
        private static bool applyingSnapshot;
        private static bool applyingRemoteRuntimeSnapshot;

        public static bool IsApplyingRemoteOperation => applyingRemoteOperation;
        public static bool IsApplyingSnapshot => applyingSnapshot;

        public static bool ShouldSubmitToHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsClient
            && !NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsConnectedClient
            && !applyingRemoteOperation
            && !applyingSnapshot
            && !applyingRemoteRuntimeSnapshot;

        private static bool ShouldBroadcastFromHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsListening
            && !applyingRemoteOperation
            && !applyingSnapshot
            && !applyingRemoteRuntimeSnapshot;

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
            }

            registeredManager = manager;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(OperationMessageName, OnOperationMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessageName, OnSnapshotMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ParticipantRuntimeMessageName, OnParticipantRuntimeMessage);
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
            }

            registeredManager.OnClientConnectedCallback -= OnClientConnected;
            registeredManager.OnClientDisconnectCallback -= OnClientDisconnected;
            registeredManager = null;
            HostCharacterIdsByClientId.Clear();
            applyingRemoteRuntimeSnapshot = false;
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
                    || !IsSenderParticipant(senderClientId, snapshot.characterId))
                {
                    Debug.LogWarning($"Rejected participant runtime snapshot from client {senderClientId}.");
                    return;
                }

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

        private static void OnClientDisconnected(ulong clientId)
        {
            HostCharacterIdsByClientId.Remove(clientId);
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
                    => IsSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.PublishAbilityEvent
                    => IsSenderParticipant(senderClientId, operation.abilityEvent?.casterCharacterId),
                MMOSharedSessionNetworkOperationKind.MarkAbilityEventApplied
                    => IsSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.PublishCombatRequest
                    => IsSenderParticipant(senderClientId, operation.combatRequest?.casterCharacterId),
                MMOSharedSessionNetworkOperationKind.MarkCombatEventApplied
                    => IsSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.MarkRewardEventApplied
                    => IsSenderParticipant(senderClientId, operation.characterId),
                MMOSharedSessionNetworkOperationKind.PublishWorldObjectInteractionRequest
                    => IsSenderParticipant(senderClientId, operation.worldObjectInteractionRequest?.actorCharacterId),
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

        private static bool IsValidRuntimeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumRuntimeIdentifierLength)
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

        private static string ReadJson(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int byteCount);
            if (byteCount <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = new byte[byteCount];
            reader.ReadBytesSafe(ref bytes, byteCount);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
