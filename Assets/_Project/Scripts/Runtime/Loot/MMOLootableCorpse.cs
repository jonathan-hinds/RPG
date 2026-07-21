using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Services;
using RPGClone.UI;
using RPGClone.Vfx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Loot
{
    public sealed class MMOLootableCorpse : MonoBehaviour, IMMOLootSource
    {
        [SerializeField] private List<MMOItemStack> loot = new();
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private ParticleSystem sparkle;
        private MMOCorpseLootState corpseLootState;
        private MMOPersonalLootState pendingLocalLootUpdate;

        public event Action<MMOLootableCorpse> LootEmptied;
        public event Action<MMOLootableCorpse> AllPersonalLootEmptied;
        public string DisplayName => "Corpse";
        public IReadOnlyList<MMOItemStack> Loot => loot;
        public bool HasLoot => loot.Exists(stack => stack != null && !stack.IsEmpty);

        private void Awake()
        {
            EnsureSparkle();
            RefreshSparkle();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (IsPointerOverThisCorpse(pointerPosition))
            {
                MMOLootWindowPresenter.Open(this, pointerPosition);
            }
        }

        public void SetLoot(IEnumerable<MMOItemStack> newLoot)
        {
            pendingLocalLootUpdate = null;
            string localCharacterId = ResolveLocalCharacterId();
            corpseLootState = new MMOCorpseLootState
            {
                sessionId = MMOGameplaySessionService.SessionId,
                corpseId = gameObject.name,
                enemySpawnId = gameObject.name
            };
            MMOPersonalLootState personalState = new()
            {
                characterId = localCharacterId,
                participantId = MMOGameplaySessionService.LocalPlayer.ParticipantId
            };
            if (newLoot != null)
            {
                foreach (MMOItemStack stack in newLoot)
                {
                    if (stack != null && !stack.IsEmpty)
                    {
                        personalState.items.Add(new MMOPersonalLootItemState(stack));
                    }
                }
            }

            personalState.looted = !personalState.HasLoot;
            corpseLootState.personalLoot.Add(personalState);
            RefreshVisibleLoot();
            RefreshSparkle();
        }

        public void SetPersonalLoot(MMOCorpseLootState newCorpseLootState)
        {
            corpseLootState = Clone(newCorpseLootState);
            RefreshVisibleLoot();
            RefreshSparkle();
        }

        public void ApplyPersonalLootSnapshot(MMOCorpseLootState snapshot)
        {
            bool hadAnyLoot = HasAnyPersonalLoot();
            MMOCorpseLootState snapshotToApply = Clone(snapshot);
            ReconcilePendingLocalLoot(snapshotToApply);
            SetPersonalLoot(snapshotToApply);
            if (hadAnyLoot && !HasAnyPersonalLoot())
            {
                AllPersonalLootEmptied?.Invoke(this);
            }
        }

        public MMOCorpseLootState CreatePersonalLootSnapshot()
        {
            return Clone(corpseLootState);
        }

        public bool HasAnyPersonalLoot()
        {
            return corpseLootState != null && corpseLootState.HasAnyUnlootedItems();
        }

        public bool HasPersonalLootForLocalPlayer()
        {
            RefreshVisibleLoot();
            return HasLoot;
        }

        public bool IsCorpseLootSnapshot(string enemySpawnId)
        {
            return corpseLootState != null && corpseLootState.enemySpawnId == enemySpawnId;
        }

        public void ClearLoot()
        {
            loot.Clear();
            corpseLootState = null;
            pendingLocalLootUpdate = null;
            RefreshSparkle();
        }

        public bool TryLootToPlayer()
        {
            return MMOInteractionContext.TryCreateForLocalPlayer(out MMOInteractionContext context)
                && TryLootToInventory(context.Inventory);
        }

        public bool TryLootToInventory(MMOInventoryContainer inventory)
        {
            RefreshVisibleLoot();
            if (inventory == null || !HasLoot)
            {
                return false;
            }

            bool changed = false;
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                changed |= TryLootStackToInventory(i, inventory);
            }

            return changed;
        }

        public bool TryLootStackToInventory(int index, MMOInventoryContainer inventory)
        {
            RefreshVisibleLoot();
            if (inventory == null || index < 0 || index >= loot.Count)
            {
                return false;
            }

            MMOItemStack stack = loot[index];
            if (stack == null || stack.IsEmpty)
            {
                RemoveVisibleLootIndex(index);
                RefreshSparkle();
                return false;
            }

            int originalQuantity = stack.Quantity;
            inventory.TryAddStack(stack, out int remainingQuantity);
            if (remainingQuantity <= 0)
            {
                RemoveVisibleLootIndex(index);
            }
            else if (remainingQuantity != stack.Quantity)
            {
                stack.Configure(stack.Item, remainingQuantity);
                UpdatePersonalLootFromVisible();
            }

            RefreshAfterLootChange();
            return remainingQuantity != originalQuantity;
        }

        private void RefreshAfterLootChange()
        {
            RefreshVisibleLoot();
            RefreshSparkle();
            MMOCorpseLootState snapshot = CreatePersonalLootSnapshot();
            if (snapshot != null)
            {
                if (!MMOGameplaySessionService.IsHostAuthority)
                {
                    pendingLocalLootUpdate = Clone(GetLocalPersonalLootState());
                }

                MMOPersonalLootService.PublishCorpseLoot(snapshot);
            }

            if (!HasLoot)
            {
                LootEmptied?.Invoke(this);
            }

            if (!HasAnyPersonalLoot())
            {
                AllPersonalLootEmptied?.Invoke(this);
            }
        }

        private void RefreshVisibleLoot()
        {
            loot.Clear();
            MMOPersonalLootState personalState = GetLocalPersonalLootState();
            if (personalState == null)
            {
                return;
            }

            List<MMOItemStack> stacks = MMOPersonalLootService.ToItemStacks(personalState);
            foreach (MMOItemStack stack in stacks)
            {
                loot.Add(stack);
            }
        }

        private void UpdatePersonalLootFromVisible()
        {
            MMOPersonalLootState personalState = GetLocalPersonalLootState();
            if (personalState == null)
            {
                return;
            }

            personalState.items.Clear();
            foreach (MMOItemStack stack in loot)
            {
                if (stack != null && !stack.IsEmpty)
                {
                    personalState.items.Add(new MMOPersonalLootItemState(stack));
                }
            }

            personalState.looted = !HasLoot;
        }

        private void RemoveVisibleLootIndex(int index)
        {
            if (index >= 0 && index < loot.Count)
            {
                loot.RemoveAt(index);
            }

            UpdatePersonalLootFromVisible();
        }

        private MMOPersonalLootState GetLocalPersonalLootState()
        {
            if (corpseLootState == null)
            {
                return null;
            }

            string characterId = ResolveLocalCharacterId();
            string participantId = MMOGameplaySessionService.LocalPlayer.ParticipantId;
            foreach (MMOPersonalLootState personalState in corpseLootState.personalLoot)
            {
                if (personalState == null)
                {
                    continue;
                }

                bool matchesCharacter = !string.IsNullOrWhiteSpace(characterId)
                    && personalState.characterId == characterId;
                bool matchesParticipant = !string.IsNullOrWhiteSpace(participantId)
                    && personalState.participantId == participantId;
                if (matchesCharacter || matchesParticipant)
                {
                    return personalState;
                }
            }

            if (string.IsNullOrWhiteSpace(characterId) && corpseLootState.personalLoot.Count == 1)
            {
                return corpseLootState.personalLoot[0];
            }

            return null;
        }

        private void ReconcilePendingLocalLoot(MMOCorpseLootState incomingSnapshot)
        {
            if (incomingSnapshot?.personalLoot == null || pendingLocalLootUpdate == null)
            {
                return;
            }

            int incomingIndex = incomingSnapshot.personalLoot.FindIndex(candidate =>
                candidate != null && candidate.characterId == pendingLocalLootUpdate.characterId);
            if (incomingIndex < 0)
            {
                return;
            }

            MMOPersonalLootState incomingPersonalLoot = incomingSnapshot.personalLoot[incomingIndex];
            if (HasNoMoreLootThan(incomingPersonalLoot, pendingLocalLootUpdate))
            {
                pendingLocalLootUpdate = null;
                return;
            }

            incomingSnapshot.personalLoot[incomingIndex] = Clone(pendingLocalLootUpdate);
        }

        private static bool HasNoMoreLootThan(MMOPersonalLootState candidate, MMOPersonalLootState baseline)
        {
            if (candidate == null || baseline == null)
            {
                return candidate == null;
            }

            Dictionary<string, int> baselineQuantities = GetLootQuantities(baseline);
            Dictionary<string, int> candidateQuantities = GetLootQuantities(candidate);
            foreach (KeyValuePair<string, int> pair in candidateQuantities)
            {
                if (!baselineQuantities.TryGetValue(pair.Key, out int baselineQuantity)
                    || pair.Value > baselineQuantity)
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, int> GetLootQuantities(MMOPersonalLootState state)
        {
            Dictionary<string, int> quantities = new(StringComparer.Ordinal);
            if (state?.items == null)
            {
                return quantities;
            }

            foreach (MMOPersonalLootItemState item in state.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity <= 0)
                {
                    continue;
                }

                quantities.TryGetValue(item.itemId, out int quantity);
                quantities[item.itemId] = quantity + item.quantity;
            }

            return quantities;
        }

        private static string ResolveLocalCharacterId()
        {
            if (!string.IsNullOrWhiteSpace(MMOGameplaySessionService.LocalPlayer.CharacterId))
            {
                return MMOGameplaySessionService.LocalPlayer.CharacterId;
            }

            MMOCharacterIdentity localIdentity = MMOGameplaySessionService.LocalPlayer.Identity;
            if (localIdentity != null && MMOGameplaySessionService.Players.TryGetParticipant(localIdentity, out MMOPlayerParticipant participant))
            {
                return participant.CharacterId;
            }

            return string.Empty;
        }

        private bool IsPointerOverThisCorpse(Vector2 pointerPosition)
        {
            Camera camera = MMORuntimeSceneReferences.MainCamera;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f, interactionMask, QueryTriggerInteraction.Ignore)
                || hit.collider == null
                || hit.collider.GetComponentInParent<MMOLootableCorpse>() != this)
            {
                return false;
            }

            Transform playerTransform = MMOGameplaySessionService.LocalPlayer.PlayerTransform;
            Vector3 interactorPosition = playerTransform != null ? playerTransform.position : camera.transform.position;
            float sqrInteractionDistance = interactionDistance * interactionDistance;
            return (interactorPosition - transform.position).sqrMagnitude <= sqrInteractionDistance;
        }

        private void EnsureSparkle()
        {
            if (sparkle != null)
            {
                return;
            }

            Transform existing = transform.Find("Loot Sparkle");
            if (existing != null)
            {
                sparkle = existing.GetComponent<ParticleSystem>();
            }

            if (sparkle == null)
            {
                GameObject sparkleObject = new("Loot Sparkle");
                sparkleObject.transform.SetParent(transform, false);
                sparkleObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                sparkle = sparkleObject.AddComponent<ParticleSystem>();
            }

            ParticleSystem.MainModule main = sparkle.main;
            main.loop = true;
            main.startLifetime = 1.05f;
            main.startSpeed = 0.65f;
            main.startSize = 0.16f;
            main.startColor = new Color(1f, 0.86f, 0.32f, 0.95f);
            main.maxParticles = 48;

            ParticleSystem.EmissionModule emission = sparkle.emission;
            emission.rateOverTime = 34f;

            ParticleSystem.ShapeModule shape = sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.62f;

            Color sparkleColor = new(1f, 0.86f, 0.32f, 0.95f);
            MMOParticleMaterialUtility.ApplyParticleMaterial(sparkle, sparkleColor);
            MMOWorldSparkleEffect sparkleEffect = sparkle.gameObject.GetComponent<MMOWorldSparkleEffect>()
                ?? sparkle.gameObject.AddComponent<MMOWorldSparkleEffect>();
            sparkleEffect.Configure(sparkleColor, 0.52f);
        }

        private void RefreshSparkle()
        {
            EnsureSparkle();
            RefreshVisibleLoot();
            if (sparkle == null)
            {
                return;
            }

            if (HasLoot)
            {
                sparkle.gameObject.SetActive(true);
                if (!sparkle.isPlaying)
                {
                    sparkle.Play();
                }
            }
            else
            {
                sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sparkle.gameObject.SetActive(false);
            }
        }

        private static MMOCorpseLootState Clone(MMOCorpseLootState source)
        {
            if (source == null)
            {
                return null;
            }

            MMOCorpseLootState clone = new()
            {
                sessionId = source.sessionId,
                corpseId = source.corpseId,
                enemySpawnId = source.enemySpawnId,
                updatedUtcTicks = source.updatedUtcTicks,
                personalLoot = new List<MMOPersonalLootState>()
            };

            if (source.personalLoot != null)
            {
                foreach (MMOPersonalLootState state in source.personalLoot)
                {
                    clone.personalLoot.Add(Clone(state));
                }
            }

            return clone;
        }

        private static MMOPersonalLootState Clone(MMOPersonalLootState source)
        {
            if (source == null)
            {
                return null;
            }

            MMOPersonalLootState clone = new()
            {
                characterId = source.characterId,
                participantId = source.participantId,
                looted = source.looted,
                items = new List<MMOPersonalLootItemState>()
            };
            if (source.items != null)
            {
                foreach (MMOPersonalLootItemState item in source.items)
                {
                    clone.items.Add(item == null
                        ? null
                        : new MMOPersonalLootItemState
                        {
                            itemId = item.itemId,
                            quantity = item.quantity
                        });
                }
            }

            return clone;
        }
    }
}
