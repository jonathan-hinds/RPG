using System.Collections.Generic;
using System;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Services;
using RPGClone.UI;
using RPGClone.Vfx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(Collider))]
    public sealed class MMOQuestWorldInteractable : MonoBehaviour, IMMOLootSource
    {
        private const float SparkleRefreshSeconds = 0.25f;

        [SerializeField] private string worldObjectId = "world_object";
        [SerializeField] private string displayName = "Quest Object";
        [SerializeField] private MMOItemDefinition lootItem;
        [SerializeField, Min(1)] private int lootQuantity = 1;
        [SerializeField, Min(0f)] private float interactionCastSeconds = 1.5f;
        [SerializeField] private bool consumeOnSuccessfulInteraction = true;
        [SerializeField] private bool hideWhenConsumed = true;
        [SerializeField, Min(0f)] private float respawnSeconds = 30f;
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private readonly List<MMOItemStack> availableLoot = new();
        private ParticleSystem sparkle;
        private MMOQuestLog observedQuestLog;
        private bool consumed;
        private bool sharedAvailable = true;
        private bool sparkleVisible;
        private float nextSparkleRefreshTime;
        private Vector2 pendingScreenPosition;
        private Coroutine respawnRoutine;
        private float respawnEndTime;
        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        public string WorldObjectId => string.IsNullOrWhiteSpace(worldObjectId) ? name : worldObjectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public MMOItemDefinition LootItem => lootItem;
        public IReadOnlyList<MMOItemStack> Loot => availableLoot;
        public bool HasLoot => availableLoot.Exists(stack => stack != null && !stack.IsEmpty);
        public bool IsSharedAvailable => sharedAvailable && !consumed;

        private void Awake()
        {
            CachePresentationComponents();
            EnsureSparkle();
            RefreshSparkle();
        }

        private void OnEnable()
        {
            MMOSharedWorldObjectStateService.Register(this);
            SubscribeToPlayerQuestLog();
            RefreshSparkle();
        }

        private void OnDisable()
        {
            MMOSharedWorldObjectStateService.Unregister(this);
            if (observedQuestLog != null)
            {
                observedQuestLog.Changed -= OnQuestLogChanged;
            }

            observedQuestLog = null;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextSparkleRefreshTime)
            {
                nextSparkleRefreshTime = Time.unscaledTime + SparkleRefreshSeconds;
                if (observedQuestLog == null)
                {
                    SubscribeToPlayerQuestLog();
                }

                RefreshSparkle();
            }

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
            if (IsPointerOverThisObject(pointerPosition))
            {
                TryInteract(pointerPosition);
            }
        }

        public void Configure(string newWorldObjectId, string newDisplayName, MMOItemDefinition newLootItem, int newLootQuantity)
        {
            Configure(newWorldObjectId, newDisplayName, newLootItem, newLootQuantity, interactionCastSeconds, consumeOnSuccessfulInteraction, hideWhenConsumed);
        }

        public void Configure(
            string newWorldObjectId,
            string newDisplayName,
            MMOItemDefinition newLootItem,
            int newLootQuantity,
            float newInteractionCastSeconds,
            bool newConsumeOnSuccessfulInteraction,
            bool newHideWhenConsumed)
        {
            worldObjectId = newWorldObjectId;
            displayName = newDisplayName;
            lootItem = newLootItem;
            lootQuantity = Mathf.Max(1, newLootQuantity);
            interactionCastSeconds = Mathf.Max(0f, newInteractionCastSeconds);
            consumeOnSuccessfulInteraction = newConsumeOnSuccessfulInteraction;
            hideWhenConsumed = newHideWhenConsumed;
            availableLoot.Clear();
            consumed = false;
            sharedAvailable = true;
            SetAvailabilityPresentation(true);
            RefreshSparkle();
        }

        public bool TryLootToInventory(MMOInventoryContainer inventory)
        {
            if (inventory == null || !HasLoot)
            {
                return false;
            }

            bool changed = false;
            for (int i = availableLoot.Count - 1; i >= 0; i--)
            {
                changed |= TryLootStackToInventory(i, inventory);
            }

            return changed;
        }

        public bool TryLootStackToInventory(int index, MMOInventoryContainer inventory)
        {
            if (inventory == null || index < 0 || index >= availableLoot.Count)
            {
                return false;
            }

            MMOItemStack stack = availableLoot[index];
            if (stack == null || stack.IsEmpty)
            {
                availableLoot.RemoveAt(index);
                return false;
            }

            int originalQuantity = stack.Quantity;
            inventory.TryAddStack(stack, out int remainingQuantity);
            if (remainingQuantity <= 0)
            {
                availableLoot.RemoveAt(index);
            }
            else if (remainingQuantity != stack.Quantity)
            {
                stack.Configure(stack.Item, remainingQuantity);
            }

            if (!HasLoot)
            {
                CompleteSharedConsumption();
            }

            return remainingQuantity != originalQuantity;
        }

        private void TryInteract(Vector2 screenPosition)
        {
            if (!CanInteract())
            {
                return;
            }

            MMOInteractionContext.TryCreateForLocalPlayer(out MMOInteractionContext context);
            pendingScreenPosition = screenPosition;
            if (context.InteractionCaster == null)
            {
                CompleteInteraction();
                return;
            }

            context.InteractionCaster.TryBeginCast($"Opening {DisplayName}", interactionCastSeconds, CompleteInteraction, out _);
        }

        private void CompleteInteraction()
        {
            if (!IsSharedAvailable)
            {
                return;
            }

            MMOQuestLog questLog = ResolvePlayerQuestLog();
            if (questLog == null)
            {
                return;
            }

            if (questLog.CanUsePendingItemOnWorldObject(WorldObjectId))
            {
                if (questLog.TryUsePendingItemOnWorldObject(WorldObjectId))
                {
                    CompleteSharedConsumption();
                }

                return;
            }

            if (lootItem == null || !questLog.NeedsWorldItem(WorldObjectId, lootItem))
            {
                return;
            }

            availableLoot.Clear();
            availableLoot.Add(new MMOItemStack(lootItem, lootQuantity));
            MMOLootWindowPresenter.Open(this, pendingScreenPosition);
            RefreshSparkle();
        }

        private bool CanInteract()
        {
            if (!IsSharedAvailable)
            {
                return false;
            }

            MMOQuestLog questLog = ResolvePlayerQuestLog();
            return questLog != null
                && ((lootItem != null && questLog.NeedsWorldItem(WorldObjectId, lootItem))
                    || questLog.CanUsePendingItemOnWorldObject(WorldObjectId));
        }

        public bool TryAuthorityConsumeFromRequest(string actorCharacterId)
        {
            if (string.IsNullOrWhiteSpace(actorCharacterId) || !IsSharedAvailable)
            {
                return false;
            }

            return CompleteSharedConsumption();
        }

        public void ApplySharedSnapshot(MMOSharedWorldObjectSnapshot snapshot)
        {
            if (snapshot == null || snapshot.worldObjectId != WorldObjectId)
            {
                return;
            }

            sharedAvailable = snapshot.available;
            consumed = !snapshot.available;
            availableLoot.Clear();
            SetAvailabilityPresentation(snapshot.available);
            RefreshSparkle();
        }

        public MMOSharedWorldObjectSnapshot CreateSharedSnapshot(float remainingRespawnSeconds = -1f)
        {
            float remainingSeconds = remainingRespawnSeconds >= 0f
                ? remainingRespawnSeconds
                : !IsSharedAvailable && respawnEndTime > 0f
                    ? Mathf.Max(0f, respawnEndTime - Time.time)
                    : 0f;
            return new MMOSharedWorldObjectSnapshot
            {
                sessionId = MMOGameplaySessionService.SessionId,
                worldObjectId = WorldObjectId,
                available = IsSharedAvailable,
                respawnRemainingSeconds = remainingSeconds,
                updatedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        private bool IsPointerOverThisObject(Vector2 pointerPosition)
        {
            Camera camera = MMORuntimeSceneReferences.MainCamera;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f, interactionMask, QueryTriggerInteraction.Collide)
                || hit.collider == null
                || hit.collider.GetComponentInParent<MMOQuestWorldInteractable>() != this)
            {
                return false;
            }

            Transform playerTransform = MMOGameplaySessionService.LocalPlayer.PlayerTransform;
            Vector3 interactorPosition = playerTransform != null ? playerTransform.position : camera.transform.position;
            float sqrInteractionDistance = interactionDistance * interactionDistance;
            return (interactorPosition - transform.position).sqrMagnitude <= sqrInteractionDistance;
        }

        private MMOQuestLog ResolvePlayerQuestLog()
        {
            if (observedQuestLog != null)
            {
                return observedQuestLog;
            }

            return MMORuntimeSceneReferences.TryGetPlayerComponent(out MMOQuestLog questLog) ? questLog : null;
        }

        private void SubscribeToPlayerQuestLog()
        {
            MMOQuestLog questLog = ResolvePlayerQuestLog();
            if (questLog == observedQuestLog)
            {
                return;
            }

            if (observedQuestLog != null)
            {
                observedQuestLog.Changed -= OnQuestLogChanged;
            }

            observedQuestLog = questLog;
            if (observedQuestLog != null)
            {
                observedQuestLog.Changed += OnQuestLogChanged;
            }
        }

        private void OnQuestLogChanged(MMOQuestLog questLog)
        {
            RefreshSparkle();
        }

        private void EnsureSparkle()
        {
            if (sparkle != null)
            {
                return;
            }

            Transform existing = transform.Find("Quest Sparkle");
            GameObject sparkleObject = existing != null ? existing.gameObject : new GameObject("Quest Sparkle");
            sparkleObject.transform.SetParent(transform, false);
            sparkleObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            sparkle = sparkleObject.GetComponent<ParticleSystem>();
            if (sparkle == null)
            {
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
            bool shouldShowSparkle = CanInteract();
            if (shouldShowSparkle == sparkleVisible && sparkle.gameObject.activeSelf == shouldShowSparkle)
            {
                return;
            }

            sparkleVisible = shouldShowSparkle;
            if (shouldShowSparkle)
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

        private bool CompleteSharedConsumption()
        {
            if (!IsSharedAvailable)
            {
                return false;
            }

            if (!MMOGameplaySessionService.IsHostAuthority)
            {
                PublishInteractionRequest();
                availableLoot.Clear();
                RefreshSparkle();
                return true;
            }

            if (consumeOnSuccessfulInteraction)
            {
                consumed = true;
                sharedAvailable = false;
            }

            RefreshSparkle();
            if (hideWhenConsumed)
            {
                SetAvailabilityPresentation(false);
            }

            PublishSharedSnapshot(respawnSeconds);
            if (MMOGameplaySessionService.IsHostAuthority && respawnSeconds > 0f)
            {
                if (respawnRoutine != null)
                {
                    StopCoroutine(respawnRoutine);
                }

                respawnEndTime = Time.time + respawnSeconds;
                respawnRoutine = StartCoroutine(RespawnAfterDelay(respawnSeconds));
            }

            return true;
        }

        private void PublishInteractionRequest()
        {
            string characterId = MMOGameplaySessionService.LocalPlayer.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            RPGClone.Multiplayer.MMOSharedSessionState.PublishWorldObjectInteractionRequest(
                MMOSharedWorldObjectInteractionRequest.Create(
                    MMOGameplaySessionService.SessionId,
                    WorldObjectId,
                    characterId));
        }

        private void PublishSharedSnapshot(float remainingRespawnSeconds)
        {
            RPGClone.Multiplayer.MMOSharedSessionState.UpsertWorldObjectSnapshot(CreateSharedSnapshot(remainingRespawnSeconds));
        }

        private System.Collections.IEnumerator RespawnAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            consumed = false;
            sharedAvailable = true;
            respawnEndTime = 0f;
            availableLoot.Clear();
            SetAvailabilityPresentation(true);
            RefreshSparkle();
            PublishSharedSnapshot(0f);
            respawnRoutine = null;
        }

        private void CachePresentationComponents()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void SetAvailabilityPresentation(bool available)
        {
            cachedRenderers ??= GetComponentsInChildren<Renderer>(true);
            cachedColliders ??= GetComponentsInChildren<Collider>(true);

            if (!hideWhenConsumed)
            {
                return;
            }

            foreach (Renderer renderer in cachedRenderers)
            {
                if (renderer != null && renderer.GetComponentInParent<ParticleSystem>() == null)
                {
                    renderer.enabled = available;
                }
            }

            foreach (Collider objectCollider in cachedColliders)
            {
                if (objectCollider != null)
                {
                    objectCollider.enabled = available;
                }
            }
        }
    }
}
