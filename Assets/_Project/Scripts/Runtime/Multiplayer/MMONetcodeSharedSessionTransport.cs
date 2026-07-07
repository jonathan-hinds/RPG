using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    public static class MMONetcodeSharedSessionTransport
    {
        private const string OperationMessageName = "rpg_clone_shared_session_operation";
        private const string SnapshotMessageName = "rpg_clone_shared_session_snapshot";
        private const NetworkDelivery SharedSessionDelivery = NetworkDelivery.ReliableFragmentedSequenced;
        private static NetworkManager registeredManager;
        private static readonly Dictionary<ulong, string> HostCharacterIdsByClientId = new();
        private static bool applyingRemoteOperation;
        private static bool applyingSnapshot;

        public static bool IsApplyingRemoteOperation => applyingRemoteOperation;
        public static bool IsApplyingSnapshot => applyingSnapshot;

        public static bool ShouldSubmitToHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsClient
            && !NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsConnectedClient
            && !applyingRemoteOperation
            && !applyingSnapshot;

        private static bool ShouldBroadcastFromHost => NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsHost
            && NetworkManager.Singleton.IsListening
            && !applyingRemoteOperation
            && !applyingSnapshot;

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
            }

            registeredManager = manager;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(OperationMessageName, OnOperationMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessageName, OnSnapshotMessage);
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
            }

            registeredManager.OnClientConnectedCallback -= OnClientConnected;
            registeredManager.OnClientDisconnectCallback -= OnClientDisconnected;
            registeredManager = null;
            HostCharacterIdsByClientId.Clear();
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
                SendJsonToServer(OperationMessageName, json);
                return true;
            }

            if (ShouldBroadcastFromHost)
            {
                BroadcastOperationJsonIfHost(json);
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
                }, false));
        }

        private static void OnClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsHost || clientId == manager.LocalClientId)
            {
                return;
            }

            string snapshotJson = MMOLocalSharedSessionStore.CreateNetworkSnapshotJson();
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
                MMOLocalSharedSessionStore.ApplyNetworkOperation(json);
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
                MMOSharedSessionNetworkOperationKind.UpsertParticipantRuntime
                    => IsSenderParticipant(senderClientId, operation.participantRuntime?.characterId),
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
                ?? operation?.participantRuntime?.characterId
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
                MMOLocalSharedSessionStore.ApplyNetworkSnapshot(json);
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

        private static void SendJsonToServer(string messageName, string json)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null)
            {
                return;
            }

            using FastBufferWriter writer = CreateWriter(json);
            manager.CustomMessagingManager.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer, SharedSessionDelivery);
        }

        private static void SendJsonToClient(string messageName, ulong clientId, string json)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null)
            {
                return;
            }

            using FastBufferWriter writer = CreateWriter(json);
            manager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer, SharedSessionDelivery);
        }

        private static void SendSnapshotToClient(ulong clientId)
        {
            string snapshotJson = MMOLocalSharedSessionStore.CreateNetworkSnapshotJson();
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
