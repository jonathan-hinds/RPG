using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOInventoryPresenter : MonoBehaviour
    {
        private const int Columns = 4;
        private const int MinimumRows = 4;
        private const float PanelWidth = 300f;
        private const float SlotSize = 58f;
        private const float SlotStride = 62f;
        private const float PanelVerticalChrome = 116f;

        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOInventoryContainer inventory;
        [SerializeField] private MMOCurrencyWallet wallet;

        private RectTransform slotGrid;
        private Text moneyText;
        private Vector2 authoredPanelSize;
        private bool authoredPanelSizeCaptured;
        private bool usesAuthoredLayout;

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

            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out inventory);
            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out wallet);
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
            bool hasAuthoredLayout = transform.childCount > 0;
            RectTransform root = (RectTransform)transform;
            if (!authoredPanelSizeCaptured)
            {
                usesAuthoredLayout = hasAuthoredLayout;
                authoredPanelSize = root.sizeDelta;
                if (authoredPanelSize.x <= 0f || authoredPanelSize.y <= 0f)
                {
                    authoredPanelSize = new Vector2(
                        PanelWidth,
                        PanelVerticalChrome + MinimumRows * SlotStride);
                    root.sizeDelta = authoredPanelSize;
                }

                authoredPanelSizeCaptured = true;
            }

            if (!hasAuthoredLayout)
            {
                MMOPanelSkin.ApplyBagPanel(gameObject);
            }

            Transform existingTitle = transform.Find("Title");
            bool createdTitle = existingTitle == null;
            Text title = createdTitle
                ? MMOUiFactory.CreateText("Title", transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft)
                : existingTitle.GetComponent<Text>();
            if (title == null)
            {
                title = existingTitle.gameObject.AddComponent<Text>();
            }

            if (createdTitle)
            {
                title.text = "Backpack";
                title.rectTransform.anchorMin = new Vector2(0f, 1f);
                title.rectTransform.anchorMax = new Vector2(1f, 1f);
                title.rectTransform.pivot = new Vector2(0f, 1f);
                title.rectTransform.anchoredPosition = new Vector2(68f, -8f);
                title.rectTransform.sizeDelta = new Vector2(-126f, 32f);
            }

            Transform existingClose = transform.Find("Close");
            bool createdClose = existingClose == null;
            Button closeButton = createdClose
                ? MMOUiFactory.CreateTextButton("Close", transform, "X", new Vector2(30f, 30f), new Color(0.12f, 0.09f, 0.07f, 0.95f))
                : existingClose.GetComponent<Button>();
            if (closeButton == null)
            {
                closeButton = existingClose.gameObject.AddComponent<Button>();
            }

            if (createdClose)
            {
                MMOPanelSkin.ConfigureCloseButton(closeButton);
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (createdClose)
            {
                RectTransform closeRect = closeButton.GetComponent<RectTransform>();
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 1f);
                closeRect.anchoredPosition = new Vector2(-6f, -7f);
            }

            if (slotGrid == null)
            {
                Transform existingSlots = transform.Find("Slots");
                bool createdSlots = existingSlots == null;
                slotGrid = createdSlots
                    ? MMOUiFactory.CreateRect("Slots", transform)
                    : (RectTransform)existingSlots;
                if (createdSlots)
                {
                    slotGrid.anchorMin = new Vector2(0f, 0f);
                    slotGrid.anchorMax = new Vector2(1f, 1f);
                    slotGrid.offsetMin = new Vector2(28f, 52f);
                    slotGrid.offsetMax = new Vector2(-28f, -58f);
                }
            }

            Transform existingMoney = transform.Find("Money");
            bool createdMoney = existingMoney == null;
            moneyText = createdMoney
                ? MMOUiFactory.CreateText("Money", transform, 12, FontStyle.Bold, TextAnchor.MiddleRight)
                : existingMoney.GetComponent<Text>();
            if (moneyText == null)
            {
                moneyText = existingMoney.gameObject.AddComponent<Text>();
            }

            if (createdMoney)
            {
                moneyText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
                moneyText.rectTransform.anchorMin = new Vector2(0f, 0f);
                moneyText.rectTransform.anchorMax = new Vector2(1f, 0f);
                moneyText.rectTransform.pivot = new Vector2(1f, 0f);
                moneyText.rectTransform.anchoredPosition = new Vector2(-18f, 10f);
                moneyText.rectTransform.sizeDelta = new Vector2(-36f, 28f);
            }
        }

        private void Refresh()
        {
            BuildIfNeeded();
            if (moneyText != null)
            {
                moneyText.text = wallet != null ? MMOCurrencyWallet.FormatCopper(wallet.Copper) : "0c";
            }

            int slotCount = inventory != null ? inventory.SlotCount : 0;
            int rows = Mathf.Max(MinimumRows, Mathf.CeilToInt(slotCount / (float)Columns));
            if (!usesAuthoredLayout)
            {
                ((RectTransform)transform).sizeDelta = new Vector2(
                    authoredPanelSize.x,
                    authoredPanelSize.y + (rows - MinimumRows) * SlotStride);
            }
            for (int i = 0; i < slotCount; i++)
            {
                CreateSlot(i, inventory != null ? inventory.GetSlot(i) : null);
            }

            for (int i = 0; i < slotGrid.childCount; i++)
            {
                Transform child = slotGrid.GetChild(i);
                const string slotPrefix = "Inventory Slot ";
                if (child.name.StartsWith(slotPrefix)
                    && int.TryParse(child.name[slotPrefix.Length..], out int authoredSlotNumber)
                    && authoredSlotNumber > slotCount)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void OnWalletChanged(MMOCurrencyWallet changedWallet)
        {
            Refresh();
        }

        private void CreateSlot(int index, MMOItemStack itemStack)
        {
            bool hasItem = itemStack != null && !itemStack.IsEmpty;
            string slotName = $"Inventory Slot {index + 1}";
            Transform existing = slotGrid.Find(slotName);
            bool created = existing == null;
            Image slot = created
                ? MMOUiFactory.CreateImage(
                    slotName,
                    slotGrid,
                    new Color(1f, 1f, 1f, 0.001f))
                : existing.GetComponent<Image>();
            if (slot == null)
            {
                slot = existing.gameObject.AddComponent<Image>();
            }

            slot.gameObject.SetActive(true);
            RectTransform rectTransform = slot.rectTransform;
            int column = index % Columns;
            int row = index / Columns;
            if (created)
            {
                slot.color = new Color(1f, 1f, 1f, 0.001f);
                slot.raycastTarget = true;
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = new Vector2(column * SlotStride, -row * SlotStride);
                rectTransform.sizeDelta = new Vector2(SlotSize, SlotSize);
            }

            MMOInventoryItemUseTrigger useTrigger = slot.gameObject.GetComponent<MMOInventoryItemUseTrigger>();
            if (useTrigger == null)
            {
                useTrigger = slot.gameObject.AddComponent<MMOInventoryItemUseTrigger>();
            }

            useTrigger.Configure(inventory, index);
            MMOSlotView.Attach(slot.gameObject).Present(MMOSlotPresentation.Empty());

            if (!hasItem)
            {
                MMOItemTooltipTrigger tooltip = slot.GetComponent<MMOItemTooltipTrigger>();
                if (tooltip != null)
                {
                    tooltip.Configure(null);
                }

                return;
            }

            MMOItemIconView.AddToSlot(rectTransform, itemStack.Item, itemStack.Quantity);
        }
    }
}
