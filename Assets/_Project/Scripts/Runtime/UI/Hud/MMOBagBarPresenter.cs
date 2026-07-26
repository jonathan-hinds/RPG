using RPGClone.Inventory;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOBagBarPresenter : MonoBehaviour
    {
        public const int BackpackBagIndex = -1;

        private const int VisibleSlotCount = 5;
        private const float SlotSize = 42f;
        private const float SlotStride = 46f;

        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOInventoryContainer inventory;
        [SerializeField] private MMOInventoryPresenter inventoryPanel;

        private void Awake()
        {
            ResolveReferences();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(MMOInventoryContainer newInventory, MMOInventoryPresenter newInventoryPanel)
        {
            Unsubscribe();
            inventory = newInventory;
            inventoryPanel = newInventoryPanel;
            BuildIfNeeded();
            Refresh();
            Subscribe();
        }

        public void ToggleBag(int bagIndex)
        {
            inventoryPanel?.ToggleBag(bagIndex);
        }

        public bool CanUnequipBag(int bagSlotIndex, int targetInventorySlotIndex)
        {
            return inventory != null && inventory.CanUnequipBagToInventory(bagSlotIndex, targetInventorySlotIndex);
        }

        public bool TryUnequipBag(int bagSlotIndex, int targetInventorySlotIndex)
        {
            return inventory != null && inventory.TryUnequipBagToInventory(bagSlotIndex, targetInventorySlotIndex);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out inventory);
            }

            if (inventoryPanel == null)
            {
                inventoryPanel = FindAnyObjectByType<MMOInventoryPresenter>(FindObjectsInactive.Include);
            }
        }

        private void Subscribe()
        {
            if (inventory == null)
            {
                return;
            }

            inventory.Changed -= Refresh;
            inventory.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }
        }

        private void BuildIfNeeded()
        {
            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta.x <= 0f || root.sizeDelta.y <= 0f)
            {
                root.sizeDelta = new Vector2(VisibleSlotCount * SlotStride - (SlotStride - SlotSize), SlotSize);
            }

            for (int visualIndex = 0; visualIndex < VisibleSlotCount; visualIndex++)
            {
                int bagIndex = visualIndex < 4 ? 3 - visualIndex : BackpackBagIndex;
                string slotName = bagIndex < 0 ? "Backpack" : $"Bag Slot {bagIndex + 1}";
                Transform existing = transform.Find(slotName);
                bool created = existing == null;
                Image image = created
                    ? MMOUiFactory.CreateImage(slotName, transform, new Color(1f, 1f, 1f, 0.001f))
                    : existing.GetComponent<Image>();
                if (image == null)
                {
                    image = existing.gameObject.AddComponent<Image>();
                }

                image.raycastTarget = true;
                if (created)
                {
                    RectTransform slotRect = image.rectTransform;
                    slotRect.anchorMin = new Vector2(0f, 0.5f);
                    slotRect.anchorMax = new Vector2(0f, 0.5f);
                    slotRect.pivot = new Vector2(0f, 0.5f);
                    slotRect.anchoredPosition = new Vector2(visualIndex * SlotStride, 0f);
                    slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
                }

                MMOBagSlotInteraction interaction = image.GetComponent<MMOBagSlotInteraction>();
                if (interaction == null)
                {
                    interaction = image.gameObject.AddComponent<MMOBagSlotInteraction>();
                }

                interaction.Configure(this, inventory, inventoryPanel, bagIndex);
                MMOSlotView.Attach(image.gameObject);
            }
        }

        private void Refresh()
        {
            BuildIfNeeded();
            for (int visualIndex = 0; visualIndex < VisibleSlotCount; visualIndex++)
            {
                int bagIndex = visualIndex < 4 ? 3 - visualIndex : BackpackBagIndex;
                string slotName = bagIndex < 0 ? "Backpack" : $"Bag Slot {bagIndex + 1}";
                Transform slot = transform.Find(slotName);
                if (slot == null)
                {
                    continue;
                }

                MMOItemDefinition bag = bagIndex >= 0 && inventory != null
                    ? inventory.GetEquippedBag(bagIndex)
                    : null;
                int capacity = inventory != null ? inventory.GetBagCapacity(bagIndex) : 0;
                MMOSlotPresentation presentation;
                if (bag != null)
                {
                    presentation = MMOItemSlotAdapter.Present(bag, secondaryText: capacity.ToString());
                    MMOItemTooltipTrigger.Bind(slot.gameObject, bag);
                }
                else
                {
                    presentation = new MMOSlotPresentation(
                        secondaryText: capacity > 0 ? capacity.ToString() : null,
                        centerText: bagIndex < 0 ? "BP" : "+",
                        iconTint: bagIndex < 0 ? Color.white : new Color(0.72f, 0.68f, 0.58f, 0.72f));
                    MMOItemTooltipTrigger tooltip = slot.GetComponent<MMOItemTooltipTrigger>();
                    if (tooltip != null)
                    {
                        tooltip.Configure(null);
                    }
                }

                MMOSlotView.Attach(slot.gameObject).Present(presentation);
                MMOBagSlotInteraction interaction = slot.GetComponent<MMOBagSlotInteraction>();
                interaction?.Configure(this, inventory, inventoryPanel, bagIndex);
            }
        }
    }
}
