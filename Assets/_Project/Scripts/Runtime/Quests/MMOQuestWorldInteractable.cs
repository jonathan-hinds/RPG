using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(Collider))]
    public sealed class MMOQuestWorldInteractable : MonoBehaviour, IMMOLootSource
    {
        [SerializeField] private string worldObjectId = "world_object";
        [SerializeField] private string displayName = "Quest Object";
        [SerializeField] private MMOItemDefinition lootItem;
        [SerializeField, Min(1)] private int lootQuantity = 1;
        [SerializeField, Min(0f)] private float interactionCastSeconds = 1.5f;
        [SerializeField] private bool consumeOnSuccessfulInteraction = true;
        [SerializeField] private bool hideWhenConsumed = true;
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private readonly List<MMOItemStack> availableLoot = new();
        private ParticleSystem sparkle;
        private bool consumed;
        private Vector2 pendingScreenPosition;

        public string WorldObjectId => string.IsNullOrWhiteSpace(worldObjectId) ? name : worldObjectId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public MMOItemDefinition LootItem => lootItem;
        public IReadOnlyList<MMOItemStack> Loot => availableLoot;
        public bool HasLoot => availableLoot.Exists(stack => stack != null && !stack.IsEmpty);

        private void Awake()
        {
            EnsureSparkle();
            RefreshSparkle();
        }

        private void Update()
        {
            RefreshSparkle();

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
            gameObject.SetActive(true);
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
                MarkConsumed();
            }

            return remainingQuantity != originalQuantity;
        }

        private void TryInteract(Vector2 screenPosition)
        {
            if (!CanInteract())
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            MMOInteractionCastController castController = player != null ? player.GetComponent<MMOInteractionCastController>() : null;
            pendingScreenPosition = screenPosition;
            if (castController == null)
            {
                CompleteInteraction();
                return;
            }

            castController.TryBeginCast($"Opening {DisplayName}", interactionCastSeconds, CompleteInteraction, out _);
        }

        private void CompleteInteraction()
        {
            if (consumed)
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
                    MarkConsumed();
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
            if (consumed)
            {
                return false;
            }

            MMOQuestLog questLog = ResolvePlayerQuestLog();
            return questLog != null
                && ((lootItem != null && questLog.NeedsWorldItem(WorldObjectId, lootItem))
                    || questLog.CanUsePendingItemOnWorldObject(WorldObjectId));
        }

        private bool IsPointerOverThisObject(Vector2 pointerPosition)
        {
            Camera camera = Camera.main;
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

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 interactorPosition = player != null ? player.transform.position : camera.transform.position;
            return Vector3.Distance(interactorPosition, transform.position) <= interactionDistance;
        }

        private MMOQuestLog ResolvePlayerQuestLog()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<MMOQuestLog>() : null;
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
            main.startLifetime = 0.8f;
            main.startSpeed = 0.45f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.86f, 0.32f, 0.95f);
            main.maxParticles = 24;

            ParticleSystem.EmissionModule emission = sparkle.emission;
            emission.rateOverTime = 18f;

            ParticleSystem.ShapeModule shape = sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
        }

        private void RefreshSparkle()
        {
            EnsureSparkle();
            if (CanInteract())
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

        private void MarkConsumed()
        {
            if (consumeOnSuccessfulInteraction)
            {
                consumed = true;
            }

            RefreshSparkle();
            if (hideWhenConsumed)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
