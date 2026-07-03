using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Inventory;
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
        private MMORemotePlayerLocomotionSource locomotionSource;
        private TextMesh nameplate;
        private Camera cachedCamera;
        private int appliedPresentationSignature;

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
            int presentationSignature = CalculatePresentationSignature(snapshot.characterData);
            if (appliedPresentationSignature != presentationSignature)
            {
                ClearInheritedEquipmentVisuals();
                persistenceAgent.ApplySessionReplica(snapshot.characterData, true);
                appliedPresentationSignature = presentationSignature;
            }

            ApplyDynamicState(snapshot);
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
            locomotionSource ??= GetComponent<MMORemotePlayerLocomotionSource>() ?? gameObject.AddComponent<MMORemotePlayerLocomotionSource>();
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

            MMOPlayerLocomotionAnimator locomotionAnimator = GetComponent<MMOPlayerLocomotionAnimator>();
            if (locomotionAnimator != null)
            {
                locomotionAnimator.SetLocomotionSource(locomotionSource);
            }

            if (TryGetComponent(out CharacterController characterController))
            {
                characterController.enabled = true;
            }
        }

        private void ApplyDynamicState(MMOSessionParticipantSnapshot snapshot)
        {
            if (identity == null || snapshot?.characterData == null)
            {
                return;
            }

            identity.Health.SetCurrent(snapshot.characterData.currentHealth);
            identity.Mana.SetCurrent(snapshot.characterData.currentMana);

            Vector3 position = snapshot.characterData.position.ToVector3();
            Quaternion rotation = Quaternion.Euler(snapshot.characterData.rotationEuler.ToVector3());
            locomotionSource.ApplySnapshot(position, rotation, snapshot.runtimeUtcTicks > 0 ? snapshot.runtimeUtcTicks : snapshot.updatedUtcTicks);
        }

        private static int CalculatePresentationSignature(MMOCharacterSaveData characterData)
        {
            if (characterData == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StableHash(characterData.characterId);
                hash = hash * 31 + StableHash(characterData.characterName);
                hash = hash * 31 + (int)characterData.race;
                hash = hash * 31 + (int)characterData.characterClass;
                hash = hash * 31 + characterData.level;
                if (characterData.equipment != null)
                {
                    for (int i = 0; i < characterData.equipment.Count; i++)
                    {
                        MMOEquipmentSlotSaveData slot = characterData.equipment[i];
                        if (slot == null)
                        {
                            continue;
                        }

                        hash = hash * 31 + (int)slot.slotType;
                        hash = hash * 31 + StableHash(slot.itemId);
                    }
                }

                return hash;
            }
        }

        private void ClearInheritedEquipmentVisuals()
        {
            MMOCharacterEquipment equipment = GetComponent<MMOCharacterEquipment>();
            if (equipment == null)
            {
                return;
            }

            foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
            {
                MMOEquipmentVisualDefinition visual = equippedItem?.Item != null
                    ? equippedItem.Item.EquipmentVisual
                    : null;
                if (visual == null)
                {
                    continue;
                }

                DestroyVisualPrefabChildren(visual.ModelPrefab);
                DestroyVisualPrefabChildren(visual.GetAttachmentModelPrefab(false));
                DestroyVisualPrefabChildren(visual.GetAttachmentModelPrefab(true));
            }
        }

        private void DestroyVisualPrefabChildren(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = children.Length - 1; i >= 0; i--)
            {
                Transform child = children[i];
                if (child == null || child == transform || child.name != prefab.name)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
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
