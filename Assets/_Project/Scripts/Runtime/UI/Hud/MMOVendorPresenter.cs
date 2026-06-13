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
        private const int Columns = 2;
        private const int Rows = 5;
        private const int VisibleSlotCount = Columns * Rows;
        private const float CardWidth = 192f;
        private const float CardHeight = 54f;
        private const float CardSpacing = 8f;
        private const float IconSize = 42f;

        private MMOVendorNpc vendor;
        private MMOInventoryContainer inventory;
        private MMOCurrencyWallet wallet;
        private RectTransform root;
        private Text titleText;
        private Text statusText;
        private Text moneyText;
        private Text pageText;
        private RectTransform stockRoot;
        private Button previousButton;
        private Button nextButton;
        private int pageIndex;

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
            if (vendor != newVendor)
            {
                pageIndex = 0;
            }

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

            if (vendor != newVendor)
            {
                pageIndex = 0;
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
            Position();
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

            GameObject vendorObject = MMOWindowPrefabResolver.Instantiate(MMOWindowPrefabId.Merchant, canvas.transform, "Vendor Window");
            MMOVendorPresenter createdPresenter = vendorObject.GetComponent<MMOVendorPresenter>();
            return createdPresenter != null ? createdPresenter : vendorObject.AddComponent<MMOVendorPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            bool hasStandardWindow = TryGetComponent(out MMOStandardWindow _);
            if (!hasStandardWindow && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            root = (RectTransform)transform;
            MMOStandardWindow.ApplyDefaultPlacement(root);

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Vendor", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;
            titleText = window.TitleText;

            statusText = window.FindText("Status") ?? MMOUiFactory.CreateText("Status", content, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            statusText.color = new Color(0.86f, 0.78f, 0.64f, 1f);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, 34f);
            statusText.rectTransform.sizeDelta = new Vector2(-12f, 22f);

            stockRoot = window.FindRect("Stock") ?? MMOUiFactory.CreateRect("Stock", content);
            stockRoot.anchorMin = new Vector2(0f, 0f);
            stockRoot.anchorMax = new Vector2(1f, 1f);
            stockRoot.offsetMin = new Vector2(0f, 78f);
            stockRoot.offsetMax = new Vector2(0f, -28f);

            previousButton = window.FindButton("Previous Button") ?? MMOUiFactory.CreateTextButton("Previous Button", content, "Prev", new Vector2(76f, 28f), MMONpcWindowFrame.ButtonColor);
            RectTransform previousRect = previousButton.GetComponent<RectTransform>();
            previousRect.anchorMin = new Vector2(0f, 0f);
            previousRect.anchorMax = new Vector2(0f, 0f);
            previousRect.pivot = new Vector2(0f, 0f);
            previousRect.anchoredPosition = Vector2.zero;
            previousRect.sizeDelta = new Vector2(76f, 28f);
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(PreviousPage);

            nextButton = window.FindButton("Next Button") ?? MMOUiFactory.CreateTextButton("Next Button", content, "Next", new Vector2(76f, 28f), MMONpcWindowFrame.ButtonColor);
            RectTransform nextRect = nextButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 0f);
            nextRect.anchorMax = new Vector2(1f, 0f);
            nextRect.pivot = new Vector2(1f, 0f);
            nextRect.anchoredPosition = Vector2.zero;
            nextRect.sizeDelta = new Vector2(76f, 28f);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextPage);

            pageText = window.FindText("Page") ?? MMOUiFactory.CreateText("Page", content, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            pageText.color = MMONpcWindowFrame.TitleColor;
            pageText.rectTransform.anchorMin = new Vector2(0f, 0f);
            pageText.rectTransform.anchorMax = new Vector2(1f, 0f);
            pageText.rectTransform.pivot = new Vector2(0.5f, 0f);
            pageText.rectTransform.anchoredPosition = Vector2.zero;
            pageText.rectTransform.sizeDelta = new Vector2(-170f, 28f);

            moneyText = window.FindText("Money") ?? MMOUiFactory.CreateText("Money", content, 11, FontStyle.Bold, TextAnchor.MiddleRight);
            moneyText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            moneyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            moneyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            moneyText.rectTransform.pivot = new Vector2(1f, 1f);
            moneyText.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            moneyText.rectTransform.sizeDelta = new Vector2(-12f, 22f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            titleText.text = vendor != null ? vendor.DisplayName : "Vendor";
            int pageCount = GetPageCount();
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            moneyText.text = wallet != null ? $"Money: {MMOCurrencyWallet.FormatCopper(wallet.Copper)}" : "Money: 0c";
            pageText.text = $"Page {pageIndex + 1} of {pageCount}";
            previousButton.interactable = pageIndex > 0;
            nextButton.interactable = pageIndex < pageCount - 1;
            MMOUiFactory.DestroyChildren(stockRoot);

            for (int i = 0; i < VisibleSlotCount; i++)
            {
                int stockIndex = pageIndex * VisibleSlotCount + i;
                MMOVendorStockEntry entry = vendor != null && stockIndex < vendor.Stock.Count ? vendor.Stock[stockIndex] : null;
                CreateStockSlot(entry, i);
            }
        }

        private void CreateStockSlot(MMOVendorStockEntry entry, int index)
        {
            Button card = MMOUiFactory.CreateTextButton($"Vendor Slot {index + 1}", stockRoot, string.Empty, new Vector2(CardWidth, CardHeight), MMONpcWindowFrame.PanelColor);
            RectTransform rect = card.GetComponent<RectTransform>();
            int column = index % Columns;
            int row = index / Columns;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(column * (CardWidth + CardSpacing), -row * (CardHeight + CardSpacing));
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            if (entry == null || !entry.IsValid)
            {
                card.interactable = false;
                return;
            }

            Image iconSlot = MMOUiFactory.CreateImage("Item Icon Slot", rect, MMOItemIconView.GetSlotBackgroundColor(entry.Item), false);
            RectTransform iconRect = iconSlot.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            MMOItemIconView.AddToSlot(iconRect, entry.Item, entry.Quantity, false, false, 4f);

            Text itemName = MMOUiFactory.CreateText("Name", rect, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            itemName.text = entry.Item.DisplayName;
            itemName.color = MMOItemIconView.GetQualityTextColor(entry.Item.Quality);
            itemName.horizontalOverflow = HorizontalWrapMode.Wrap;
            itemName.verticalOverflow = VerticalWrapMode.Truncate;
            itemName.rectTransform.anchorMin = new Vector2(0f, 1f);
            itemName.rectTransform.anchorMax = new Vector2(1f, 1f);
            itemName.rectTransform.pivot = new Vector2(0f, 1f);
            itemName.rectTransform.anchoredPosition = new Vector2(56f, -7f);
            itemName.rectTransform.sizeDelta = new Vector2(-64f, 24f);

            Text price = MMOUiFactory.CreateText("Price", rect, 9, FontStyle.Bold, TextAnchor.LowerCenter);
            price.text = MMOCurrencyWallet.FormatCopper(entry.PriceCopper);
            price.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            price.rectTransform.anchorMin = new Vector2(0f, 0f);
            price.rectTransform.anchorMax = new Vector2(1f, 0f);
            price.rectTransform.pivot = new Vector2(0f, 0f);
            price.rectTransform.anchoredPosition = new Vector2(56f, 6f);
            price.rectTransform.sizeDelta = new Vector2(-64f, 18f);

            MMOVendorItemPurchaseTrigger buyTrigger = card.gameObject.GetComponent<MMOVendorItemPurchaseTrigger>();
            if (buyTrigger == null)
            {
                buyTrigger = card.gameObject.AddComponent<MMOVendorItemPurchaseTrigger>();
            }

            buyTrigger.Configure(this, entry);
            MMOItemTooltipTrigger.Bind(card.gameObject, entry.Item);
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

        private void PreviousPage()
        {
            pageIndex = Mathf.Max(0, pageIndex - 1);
            Refresh();
        }

        private void NextPage()
        {
            pageIndex = Mathf.Min(GetPageCount() - 1, pageIndex + 1);
            Refresh();
        }

        private int GetPageCount()
        {
            int stockCount = vendor != null ? vendor.Stock.Count : 0;
            return Mathf.Max(1, Mathf.CeilToInt(stockCount / (float)VisibleSlotCount));
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

        private void Position()
        {
            MMOStandardWindow.ApplyDefaultPlacement(root);
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
