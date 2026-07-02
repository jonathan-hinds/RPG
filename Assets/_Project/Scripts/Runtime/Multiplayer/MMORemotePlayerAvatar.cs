using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Player;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MMORemotePlayerAvatar : MonoBehaviour
    {
        private const string NameplateObjectName = "Remote Player Nameplate";

        private MMOCharacterIdentity identity;
        private MMOCharacterPersistenceAgent persistenceAgent;
        private TextMesh nameplate;
        private Camera cachedCamera;

        public string ParticipantId { get; private set; }
        public string CharacterId { get; private set; }

        public void Configure(MMOSessionParticipantSnapshot snapshot)
        {
            if (snapshot == null || snapshot.characterData == null)
            {
                return;
            }

            ParticipantId = snapshot.participantId;
            CharacterId = snapshot.characterId;
            PrepareAsRemoteReplica();
            ApplySnapshot(snapshot);
            EnsureNameplate();
            MMOGameplaySessionService.RegisterPlayerCharacter(identity, ParticipantId, CharacterId, false, false);
        }

        public void ApplySnapshot(MMOSessionParticipantSnapshot snapshot)
        {
            if (snapshot == null || snapshot.characterData == null)
            {
                return;
            }

            PrepareAsRemoteReplica();
            persistenceAgent.ApplySessionReplica(snapshot.characterData, true);
            Vector3 position = snapshot.characterData.position.ToVector3();
            Vector3 rotationEuler = snapshot.characterData.rotationEuler.ToVector3();
            transform.SetPositionAndRotation(position, Quaternion.Euler(rotationEuler));
            RefreshNameplate();
        }

        private void LateUpdate()
        {
            if (nameplate == null)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = MMOGameplaySessionService.LocalPlayer.MainCamera;
            }

            if (cachedCamera != null)
            {
                nameplate.transform.rotation = Quaternion.LookRotation(nameplate.transform.position - cachedCamera.transform.position, Vector3.up);
            }
        }

        private void OnDisable()
        {
            if (identity != null)
            {
                MMOGameplaySessionService.UnregisterPlayerCharacter(identity);
            }
        }

        private void PrepareAsRemoteReplica()
        {
            identity ??= GetComponent<MMOCharacterIdentity>();
            persistenceAgent ??= GetComponent<MMOCharacterPersistenceAgent>();
            if (persistenceAgent != null)
            {
                persistenceAgent.MarkAsRemoteSessionReplica();
            }

            gameObject.tag = "Untagged";
            MMOLocalSharedSessionBridge bridge = GetComponent<MMOLocalSharedSessionBridge>();
            if (bridge != null)
            {
                bridge.SuppressStoreRemoval();
                bridge.enabled = false;
            }

            DisableLocalOnlyComponent<MMOInputReader>();
            DisableLocalOnlyComponent<MMOPlayerMotor>();
            DisableLocalOnlyComponent<MMOThirdPersonCamera>();

            if (TryGetComponent(out CharacterController characterController))
            {
                characterController.enabled = true;
            }
        }

        private void DisableLocalOnlyComponent<T>() where T : Behaviour
        {
            T component = GetComponent<T>();
            if (component != null)
            {
                component.enabled = false;
            }
        }

        private void EnsureNameplate()
        {
            if (nameplate != null)
            {
                return;
            }

            Transform existing = transform.Find(NameplateObjectName);
            GameObject nameplateObject = existing != null
                ? existing.gameObject
                : new GameObject(NameplateObjectName);
            nameplateObject.transform.SetParent(transform, false);
            nameplateObject.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            nameplate = nameplateObject.GetComponent<TextMesh>() ?? nameplateObject.AddComponent<TextMesh>();
            nameplate.anchor = TextAnchor.MiddleCenter;
            nameplate.alignment = TextAlignment.Center;
            nameplate.characterSize = 0.08f;
            nameplate.fontSize = 42;
            nameplate.color = new Color(0.45f, 0.95f, 0.45f, 1f);
            RefreshNameplate();
        }

        private void RefreshNameplate()
        {
            if (nameplate != null && identity != null)
            {
                nameplate.text = identity.DisplayName;
            }
        }
    }
}
