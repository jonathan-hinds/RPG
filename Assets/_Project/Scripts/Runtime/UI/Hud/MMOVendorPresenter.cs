using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Vendors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOVendorPresenter : MonoBehaviour
    {
        private const int Columns = 3;
        private const int Rows = 5;
        private const int VisibleSlotCount = Columns * Rows;
        private const float Width = 270f;
        private const float Height = 488f;
        private const float SlotSize = 72f;
        private const float SlotSpacing = 6f;
        private const float CanvasPadding = 8f;

        private MMOVendorNpc vendor;
        private MMOInventoryContainer inventory;
        private MMOCurrencyWallet wallet;
        private RectTransform root;
        private Text titleText;
        private Text statusText;
        private RectTransform stockRoot;

        public static MMOVendorPresenter Instance { get; private set; }
        public static bool HasOpenVendor => Instance != null && Instance.gameObject.activeInHierarchy && Instance.vendor != null;

        private void Awake()
        {
            Instance = this;
            BuildIfNeeded();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            if (wallet != null)
            {
                wallet.Changed -= OnWalletChanged;
                wallet.Changed += OnWalletChanged;
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.Changed -= OnWalletChanged;
            }
        }

        public static void Open(MMOVendorNpc vendor, MMOInventoryContainer inventory, MMOCurrencyWallet wallet, Vector2 screenPosition)
        {
            MMOVendorPresenter presenter = ResolvePresenter();
            presenter?.Show(vendor, inventory, wallet, screenPosition);
        }

        public void Configure(MMOVendorNpc newVendor, MMOInventoryContainer newInventory, MMOCurrencyWallet newWallet)
        {
            vendor = newVendor;
            inventory = newInventory;
            wallet = newWallet;
            Refresh();
        }

        public void Show(MMOVendorNpc newVendor, MMOInventoryContainer newInventory, MMOCurrencyWallet newWallet, Vector2 screenPosition)
        {
            if (wallet != null)
            {
                wallet.Changed -= OnWalletChanged;
            }

            vendor = newVendor;
            inventory = newInventory;
            wallet = newWallet;
            if (wallet != null)
            {
                wallet.Changed += OnWalletChanged;
            }

            BuildIfNeeded();
            gameObject.SetActive(true);
            Position(screenPosition);
            TrackNpcDistance();
            OpenInventoryPanel();
            Refresh();
        }

        public static bool TrySellInventorySlot(MMOInventoryContainer inventory, int slotIndex)
        {
            return Instance != null && Instance.gameObject.activeInHierarchy && Instance.SellInventorySlot(inventory, slotIndex);
        }

        private static MMOVendorPresenter ResolvePresenter()
        {
            if (Instance != null)
            {
                return Instance;
            }

            MMOVendorPresenter presenter = FindAnyObjectByType<MMOVendorPresenter>();
            if (presenter != null)
            {
                return presenter;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            GameObject vendorObject = new("Vendor Window", typeof(RectTransform));
            vendorObject.transform.SetParent(canvas.transform, false);
            return vendorObject.AddComponent<MMOVendorPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Width, Height);

            MMONpcWindowFrame.Apply(gameObject);
            titleText = MMONpcWindowFrame.CreateTitle(transform, "Vendor");
            MMONpcWindowFrame.CreateCloseButton(transform, () => gameObject.SetActive(false));

            statusText = MMOUiFactory.CreateText("Status", transform, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            statusText.color = new Color(0.86f, 0.78f, 0.64f, 1f);
            statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusText.rectTransform.pivot = new Vector2(0f, 1f);
            statusText.rectTransform.anchoredPosition = new Vector2(14f, -44f);
            statusText.rectTransform.sizeDelta = new Vector2(-28f, 28f);

            stockRoot = MMOUiFactory.CreateRect("Stock", transform);
            stockRoot.anchorMin = new Vector2(0f, 0f);
            stockRoot.anchorMax = new Vector2(1f, 1f);
            stockRoot.offsetMin = new Vector2(18f, 18f);
            stockRoot.offsetMax = new Vector2(-18f, -86f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            titleText.text = vendor != null ? vendor.DisplayName : "Vendor";
            MMOUiFactory.DestroyChildren(stockRoot);

            for (int i = 0; i < VisibleSlotCount; i++)
            {
                MMOVendorStockEntry entry = vendor != null && i < vendor.Stock.Count ? vendor.Stock[i] : null;
                CreateStockSlot(entry, i);
            }
        }

        private void CreateStockSlot(MMOVendorStockEntry entry, int index)
        {
            Image slot = MMOUiFactory.CreateImage($"Vendor Slot {index + 1}", stockRoot, MMONpcWindowFrame.PanelColor);
            RectTransform rect = slot.rectTransform;
            int column = index % Columns;
            int row = index / Columns;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(column * (SlotSize + SlotSpacing), -row * (SlotSize + SlotSpacing));
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);

            if (entry == null || !entry.IsValid)
            {
                return;
            }

            Text itemName = MMOUiFactory.CreateText("Item Name", rect, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            itemName.text = entry.Item.DisplayName;
            itemName.resizeTextForBestFit = true;
            itemName.resizeTextMinSize = 6;
            itemName.resizeTextMaxSize = 10;
            MMOUiFactory.Stretch(itemName.rectTransform);
            itemName.rectTransform.offsetMin = new Vector2(5f, 9f);
            itemName.rectTransform.offsetMax = new Vector2(-5f, -23f);

            Text price = MMOUiFactory.CreateText("Price", rect, 9, FontStyle.Bold, TextAnchor.LowerCenter);
            price.text = MMOCurrencyWallet.FormatCopper(entry.PriceCopper);
            price.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            MMOUiFactory.Stretch(price.rectTransform);
            price.rectTransform.offsetMin = new Vector2(4f, 3f);
            price.rectTransform.offsetMax = new Vector2(-4f, -3f);

            MMOVendorItemPurchaseTrigger buyTrigger = slot.gameObject.GetComponent<MMOVendorItemPurchaseTrigger>();
            if (buyTrigger == null)
            {
                buyTrigger = slot.gameObject.AddComponent<MMOVendorItemPurchaseTrigger>();
            }

            buyTrigger.Configure(this, entry);
            MMOItemTooltipTrigger.Bind(slot.gameObject, entry.Item);
        }

        public void Buy(MMOVendorStockEntry entry)
        {
            if (vendor == null || entry == null)
            {
                return;
            }

            bool bought = vendor.TryBuy(entry, inventory, wallet);
            statusText.text = bought ? $"Bought {entry.Item.DisplayName}." : "Cannot buy that.";
            Refresh();
        }

        private bool SellInventorySlot(MMOInventoryContainer sourceInventory, int slotIndex)
        {
            if (vendor == null || sourceInventory == null || sourceInventory != inventory || wallet == null)
            {
                return false;
            }

            bool sold = vendor.TrySellInventorySlot(sourceInventory, wallet, slotIndex, out int earnedCopper, out string itemName);
            statusText.text = sold
                ? $"Sold {itemName} for {MMOCurrencyWallet.FormatCopper(earnedCopper)}."
                : "That item cannot be sold.";
            Refresh();
            return sold;
        }

        private void TrackNpcDistance()
        {
            if (vendor == null || inventory == null)
            {
                return;
            }

            MMONpcPanelDistanceCloser closer = gameObject.GetComponent<MMONpcPanelDistanceCloser>();
            if (closer == null)
            {
                closer = gameObject.AddComponent<MMONpcPanelDistanceCloser>();
            }

            closer.Track(vendor.transform, inventory.transform, vendor.InteractionDistance + 0.75f, () => gameObject.SetActive(false));
        }

        private void OpenInventoryPanel()
        {
            MMOInventoryPresenter inventoryPresenter = FindInventoryPresenter();
            if (inventoryPresenter == null)
            {
                return;
            }

            inventoryPresenter.Configure(inventory);
            inventoryPresenter.gameObject.SetActive(true);
            inventoryPresenter.RefreshNow();
        }

        private MMOInventoryPresenter FindInventoryPresenter()
        {
            MMOInventoryPresenter[] presenters = FindObjectsByType<MMOInventoryPresenter>(FindObjectsInactive.Include);
            return presenters.Length > 0 ? presenters[0] : null;
        }

        private void OnWalletChanged(MMOCurrencyWallet changedWallet)
        {
            Refresh();
        }

        private void Position(Vector2 screenPosition)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (canvasRect == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPosition);

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = ClampToCanvas(localPosition + new Vector2(24f, -20f), canvasRect);
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Rect rect = canvasRect.rect;
            Vector2 size = root.sizeDelta;
            position.x = Mathf.Clamp(position.x, rect.xMin + CanvasPadding, rect.xMax - size.x - CanvasPadding);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + CanvasPadding, rect.yMax - CanvasPadding);
            return position;
        }
    }

    public sealed class MMOVendorItemPurchaseTrigger : MonoBehaviour, IPointerClickHandler
    {
        private MMOVendorPresenter presenter;
        private MMOVendorStockEntry entry;

        public void Configure(MMOVendorPresenter newPresenter, MMOVendorStockEntry newEntry)
        {
            presenter = newPresenter;
            entry = newEntry;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            presenter?.Buy(entry);
        }
    }
}
