using System;
using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGClone.Loot
{
    public sealed class MMOLootableCorpse : MonoBehaviour
    {
        [SerializeField] private List<MMOItemStack> loot = new();
        [SerializeField, Min(1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private ParticleSystem sparkle;

        public event Action<MMOLootableCorpse> LootEmptied;
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
            loot.Clear();
            if (newLoot != null)
            {
                foreach (MMOItemStack stack in newLoot)
                {
                    if (stack != null && !stack.IsEmpty)
                    {
                        loot.Add(stack.Clone());
                    }
                }
            }

            RefreshSparkle();
        }

        public void ClearLoot()
        {
            loot.Clear();
            RefreshSparkle();
        }

        public bool TryLootToPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            MMOInventoryContainer inventory = player != null ? player.GetComponent<MMOInventoryContainer>() : null;
            return inventory != null && TryLootToInventory(inventory);
        }

        public bool TryLootToInventory(MMOInventoryContainer inventory)
        {
            if (inventory == null || !HasLoot)
            {
                return false;
            }

            bool changed = false;
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                MMOItemStack stack = loot[i];
                if (stack == null || stack.IsEmpty)
                {
                    loot.RemoveAt(i);
                    changed = true;
                    continue;
                }

                inventory.TryAddStack(stack, out int remainingQuantity);
                if (remainingQuantity <= 0)
                {
                    loot.RemoveAt(i);
                    changed = true;
                }
                else if (remainingQuantity != stack.Quantity)
                {
                    stack.Configure(stack.Item, remainingQuantity);
                    changed = true;
                }
            }

            if (changed)
            {
                RefreshSparkle();
            }

            if (!HasLoot)
            {
                LootEmptied?.Invoke(this);
            }

            return changed;
        }

        public bool TryLootStackToInventory(int index, MMOInventoryContainer inventory)
        {
            if (inventory == null || index < 0 || index >= loot.Count)
            {
                return false;
            }

            MMOItemStack stack = loot[index];
            if (stack == null || stack.IsEmpty)
            {
                loot.RemoveAt(index);
                RefreshSparkle();
                return false;
            }

            int originalQuantity = stack.Quantity;
            inventory.TryAddStack(stack, out int remainingQuantity);
            if (remainingQuantity <= 0)
            {
                loot.RemoveAt(index);
            }
            else if (remainingQuantity != stack.Quantity)
            {
                stack.Configure(stack.Item, remainingQuantity);
            }

            RefreshSparkle();
            if (!HasLoot)
            {
                LootEmptied?.Invoke(this);
            }

            return remainingQuantity != originalQuantity;
        }

        private bool IsPointerOverThisCorpse(Vector2 pointerPosition)
        {
            Camera camera = Camera.main;
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

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 interactorPosition = player != null ? player.transform.position : camera.transform.position;
            return Vector3.Distance(interactorPosition, transform.position) <= interactionDistance;
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
    }
}
