using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float windowSpacing = 12f;
        [SerializeField, Min(0f)] private float columnSpacing = 12f;
        [SerializeField, Min(0f)] private float viewportTopInset = 12f;
        [SerializeField] private MMOInventoryContainer inventory;
        [SerializeField] private MMOInventoryPresenter inventoryPanel;

        private readonly Dictionary<int, MMOInventoryPresenter> bagWindows = new();
        private readonly List<int> openWindowOrder = new();
        private Vector2 stackAnchor;
        private Vector2 lastViewportSize;
        private bool windowPoolInitialized;
        private bool suppressReflow;

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

        private void OnDestroy()
        {
            UnsubscribeFromWindows();
        }

        private void LateUpdate()
        {
            if (openWindowOrder.Count == 0
                || inventoryPanel == null
                || inventoryPanel.transform.parent is not RectTransform viewport)
            {
                return;
            }

            Vector2 viewportSize = viewport.rect.size;
            if ((viewportSize - lastViewportSize).sqrMagnitude > 0.01f)
            {
                ReflowOpenWindows();
            }
        }

        public void Configure(MMOInventoryContainer newInventory, MMOInventoryPresenter newInventoryPanel)
        {
            Unsubscribe();
            if (inventoryPanel != newInventoryPanel)
            {
                UnsubscribeFromWindows();
                bagWindows.Clear();
                openWindowOrder.Clear();
                windowPoolInitialized = false;
            }

            inventory = newInventory;
            inventoryPanel = newInventoryPanel;
            BuildIfNeeded();
            EnsureWindowPool();
            ConfigureWindows();
            Refresh();
            Subscribe();
        }

        public void ToggleBag(int bagIndex)
        {
            EnsureWindowPool();
            if (!TryGetEligibleWindow(bagIndex, out MMOInventoryPresenter window))
            {
                return;
            }

            window.gameObject.SetActive(!window.gameObject.activeSelf);
            RebuildOpenWindowOrder();
            ReflowOpenWindows();
        }

        public void ToggleAllBags()
        {
            EnsureWindowPool();
            List<MMOInventoryPresenter> eligibleWindows = GetEligibleWindows();
            if (eligibleWindows.Count == 0)
            {
                return;
            }

            bool allOpen = eligibleWindows.TrueForAll(window => window.gameObject.activeSelf);
            suppressReflow = true;
            for (int i = 0; i < eligibleWindows.Count; i++)
            {
                eligibleWindows[i].gameObject.SetActive(!allOpen);
            }

            suppressReflow = false;
            RebuildOpenWindowOrder();
            ReflowOpenWindows();
        }

        public void CloseAllBags()
        {
            EnsureWindowPool();
            suppressReflow = true;
            foreach (MMOInventoryPresenter window in bagWindows.Values)
            {
                if (window != null)
                {
                    window.gameObject.SetActive(false);
                }
            }

            suppressReflow = false;
            RebuildOpenWindowOrder();
            ReflowOpenWindows();
        }

        public bool IsBagOpen(int bagIndex)
        {
            return bagWindows.TryGetValue(bagIndex, out MMOInventoryPresenter window)
                && window != null
                && window.gameObject.activeSelf;
        }

        public void ReflowOpenWindows()
        {
            if (inventoryPanel == null || inventoryPanel.transform.parent is not RectTransform viewport)
            {
                return;
            }

            RemoveClosedWindowsFromOrder();
            lastViewportSize = viewport.rect.size;
            float viewportHeight = viewport.rect.height;
            float availableTop = viewportHeight - Mathf.Max(0f, viewportTopInset);
            float columnWidth = Mathf.Max(1f, ((RectTransform)inventoryPanel.transform).rect.width);
            float currentColumnHeight = 0f;
            int column = 0;

            for (int i = 0; i < openWindowOrder.Count; i++)
            {
                if (!bagWindows.TryGetValue(openWindowOrder[i], out MMOInventoryPresenter window)
                    || window == null
                    || !window.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform windowRect = (RectTransform)window.transform;
                float windowHeight = Mathf.Max(1f, windowRect.rect.height);
                bool startsColumn = currentColumnHeight <= 0f;
                float nextBottom = startsColumn
                    ? stackAnchor.y
                    : stackAnchor.y + currentColumnHeight + windowSpacing;
                if (!startsColumn && nextBottom + windowHeight > availableTop)
                {
                    column++;
                    currentColumnHeight = 0f;
                    nextBottom = stackAnchor.y;
                }

                windowRect.anchorMin = new Vector2(1f, 0f);
                windowRect.anchorMax = new Vector2(1f, 0f);
                windowRect.pivot = new Vector2(1f, 0f);
                windowRect.anchoredPosition = new Vector2(
                    stackAnchor.x - column * (columnWidth + columnSpacing),
                    nextBottom);
                currentColumnHeight = currentColumnHeight <= 0f
                    ? windowHeight
                    : currentColumnHeight + windowSpacing + windowHeight;
                windowRect.SetAsLastSibling();
            }
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

                interaction.Configure(this, inventory, bagIndex);
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
                interaction?.Configure(this, inventory, bagIndex);
            }

            EnsureWindowPool();
            CloseWindowsForUnequippedBags();
            ReflowOpenWindows();
        }

        private void EnsureWindowPool()
        {
            if (windowPoolInitialized || inventoryPanel == null)
            {
                return;
            }

            windowPoolInitialized = true;
            stackAnchor = ((RectTransform)inventoryPanel.transform).anchoredPosition;
            RegisterWindow(BackpackBagIndex, inventoryPanel);

            Transform parent = inventoryPanel.transform.parent;
            for (int bagIndex = 0; bagIndex < VisibleSlotCount - 1; bagIndex++)
            {
                GameObject clone = Instantiate(inventoryPanel.gameObject, parent);
                clone.name = $"InventoryPanel - Bag Slot {bagIndex + 1}";
                clone.SetActive(false);
                MMOInventoryPresenter presenter = clone.GetComponent<MMOInventoryPresenter>();
                presenter.Configure(inventory, bagIndex);
                RegisterWindow(bagIndex, presenter);
            }

            inventoryPanel.Configure(inventory, BackpackBagIndex);
            RebuildOpenWindowOrder();
        }

        private void ConfigureWindows()
        {
            if (!windowPoolInitialized)
            {
                return;
            }

            suppressReflow = true;
            foreach (KeyValuePair<int, MMOInventoryPresenter> entry in bagWindows)
            {
                entry.Value?.Configure(inventory, entry.Key);
            }

            suppressReflow = false;
            RebuildOpenWindowOrder();
            ReflowOpenWindows();
        }

        private void RegisterWindow(int bagIndex, MMOInventoryPresenter window)
        {
            bagWindows[bagIndex] = window;
            window.VisibilityChanged -= OnWindowVisibilityChanged;
            window.VisibilityChanged += OnWindowVisibilityChanged;
            window.LayoutChanged -= OnWindowLayoutChanged;
            window.LayoutChanged += OnWindowLayoutChanged;
        }

        private void UnsubscribeFromWindows()
        {
            foreach (MMOInventoryPresenter window in bagWindows.Values)
            {
                if (window == null)
                {
                    continue;
                }

                window.VisibilityChanged -= OnWindowVisibilityChanged;
                window.LayoutChanged -= OnWindowLayoutChanged;
            }
        }

        private void OnWindowVisibilityChanged(MMOInventoryPresenter window, bool isVisible)
        {
            int bagIndex = FindBagIndex(window);
            if (bagIndex < BackpackBagIndex)
            {
                return;
            }

            openWindowOrder.Remove(bagIndex);
            if (isVisible)
            {
                openWindowOrder.Add(bagIndex);
            }

            if (!suppressReflow)
            {
                ReflowOpenWindows();
            }
        }

        private void OnWindowLayoutChanged(MMOInventoryPresenter window)
        {
            if (!suppressReflow && window != null && window.gameObject.activeSelf)
            {
                ReflowOpenWindows();
            }
        }

        private int FindBagIndex(MMOInventoryPresenter window)
        {
            foreach (KeyValuePair<int, MMOInventoryPresenter> entry in bagWindows)
            {
                if (entry.Value == window)
                {
                    return entry.Key;
                }
            }

            return BackpackBagIndex - 1;
        }

        private bool TryGetEligibleWindow(int bagIndex, out MMOInventoryPresenter window)
        {
            window = null;
            if (!bagWindows.TryGetValue(bagIndex, out MMOInventoryPresenter candidate)
                || candidate == null)
            {
                return false;
            }

            if (bagIndex >= 0 && (inventory == null || inventory.GetEquippedBag(bagIndex) == null))
            {
                return false;
            }

            window = candidate;
            return true;
        }

        private List<MMOInventoryPresenter> GetEligibleWindows()
        {
            List<MMOInventoryPresenter> windows = new(VisibleSlotCount);
            if (TryGetEligibleWindow(BackpackBagIndex, out MMOInventoryPresenter backpack))
            {
                windows.Add(backpack);
            }

            for (int bagIndex = 0; bagIndex < VisibleSlotCount - 1; bagIndex++)
            {
                if (TryGetEligibleWindow(bagIndex, out MMOInventoryPresenter bagWindow))
                {
                    windows.Add(bagWindow);
                }
            }

            return windows;
        }

        private void CloseWindowsForUnequippedBags()
        {
            suppressReflow = true;
            for (int bagIndex = 0; bagIndex < VisibleSlotCount - 1; bagIndex++)
            {
                if (bagWindows.TryGetValue(bagIndex, out MMOInventoryPresenter window)
                    && window != null
                    && (inventory == null || inventory.GetEquippedBag(bagIndex) == null))
                {
                    window.gameObject.SetActive(false);
                }
            }

            suppressReflow = false;
        }

        private void RebuildOpenWindowOrder()
        {
            openWindowOrder.Clear();
            if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
            {
                openWindowOrder.Add(BackpackBagIndex);
            }

            for (int bagIndex = 0; bagIndex < VisibleSlotCount - 1; bagIndex++)
            {
                if (bagWindows.TryGetValue(bagIndex, out MMOInventoryPresenter window)
                    && window != null
                    && window.gameObject.activeSelf)
                {
                    openWindowOrder.Add(bagIndex);
                }
            }
        }

        private void RemoveClosedWindowsFromOrder()
        {
            openWindowOrder.RemoveAll(bagIndex =>
                !bagWindows.TryGetValue(bagIndex, out MMOInventoryPresenter window)
                || window == null
                || !window.gameObject.activeSelf);
        }
    }
}
