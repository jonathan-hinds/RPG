using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOInventoryPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOInventoryContainer inventory;
        [SerializeField] private MMOCurrencyWallet wallet;

        private RectTransform slotGrid;
        private Text moneyText;

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
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(MMOInventoryContainer newInventory)
        {
            Unsubscribe();
            inventory = newInventory;
            ResolveWalletFromInventory();
            BuildIfNeeded();
            Refresh();
            Subscribe();
        }

        public void RefreshNow()
        {
            Refresh();
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf)
            {
                Refresh();
            }
        }

        private void ResolveReferences()
        {
            if (inventory != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponent<MMOInventoryContainer>();
                wallet = player.GetComponent<MMOCurrencyWallet>();
            }
        }

        private void ResolveWalletFromInventory()
        {
            if (wallet != null || inventory == null)
            {
                return;
            }

            wallet = inventory.GetComponent<MMOCurrencyWallet>();
        }

        private void Subscribe()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                inventory.Changed += Refresh;
            }

            if (wallet != null)
            {
                wallet.Changed -= OnWalletChanged;
                wallet.Changed += OnWalletChanged;
            }
        }

        private void Unsubscribe()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
            }

            if (wallet != null)
            {
                wallet.Changed -= OnWalletChanged;
            }
        }

        private void BuildIfNeeded()
        {
            if (slotGrid != null)
            {
                return;
            }

            MMOUiFactory.DestroyChildren(transform);

            RectTransform root = (RectTransform)transform;
            root.sizeDelta = new Vector2(300f, 364f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.035f, 0.032f, 0.028f, 0.96f);

            Text title = MMOUiFactory.CreateText("Title", transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = "Inventory";
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            title.rectTransform.sizeDelta = new Vector2(-28f, 28f);

            Button closeButton = MMOUiFactory.CreateTextButton("Close", transform, "X", new Vector2(26f, 24f), new Color(0.12f, 0.09f, 0.07f, 0.95f));
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);

            slotGrid = MMOUiFactory.CreateRect("Slots", transform);
            slotGrid.anchorMin = new Vector2(0f, 0f);
            slotGrid.anchorMax = new Vector2(1f, 1f);
            slotGrid.offsetMin = new Vector2(16f, 52f);
            slotGrid.offsetMax = new Vector2(-16f, -58f);

            moneyText = MMOUiFactory.CreateText("Money", transform, 12, FontStyle.Bold, TextAnchor.MiddleRight);
            moneyText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
            moneyText.rectTransform.anchorMin = new Vector2(0f, 0f);
            moneyText.rectTransform.anchorMax = new Vector2(1f, 0f);
            moneyText.rectTransform.pivot = new Vector2(1f, 0f);
            moneyText.rectTransform.anchoredPosition = new Vector2(-16f, 16f);
            moneyText.rectTransform.sizeDelta = new Vector2(-32f, 24f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            MMOUiFactory.DestroyChildren(slotGrid);
            if (moneyText != null)
            {
                moneyText.text = wallet != null ? MMOCurrencyWallet.FormatCopper(wallet.Copper) : "0c";
            }

            int slotCount = inventory != null ? inventory.SlotCount : 0;
            for (int i = 0; i < slotCount; i++)
            {
                CreateSlot(i, inventory != null ? inventory.GetSlot(i) : null);
            }
        }

        private void OnWalletChanged(MMOCurrencyWallet changedWallet)
        {
            Refresh();
        }

        private void CreateSlot(int index, MMOItemStack itemStack)
        {
            bool hasItem = itemStack != null && !itemStack.IsEmpty;
            Color slotColor = hasItem ? MMOItemIconView.GetSlotBackgroundColor(itemStack.Item) : new Color(0.045f, 0.04f, 0.036f, 0.94f);
            Image slot = MMOUiFactory.CreateImage($"Inventory Slot {index + 1}", slotGrid, slotColor);
            RectTransform rectTransform = slot.rectTransform;
            int column = index % 4;
            int row = index / 4;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(column * 66f, -row * 66f);
            rectTransform.sizeDelta = new Vector2(58f, 58f);

            MMOInventoryItemUseTrigger useTrigger = slot.gameObject.GetComponent<MMOInventoryItemUseTrigger>();
            if (useTrigger == null)
            {
                useTrigger = slot.gameObject.AddComponent<MMOInventoryItemUseTrigger>();
            }

            useTrigger.Configure(inventory, index);

            if (!hasItem)
            {
                return;
            }

            MMOItemIconView.AddToSlot(rectTransform, itemStack.Item, itemStack.Quantity);
        }
    }
}
