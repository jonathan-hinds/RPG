using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOInventoryPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOInventoryContainer inventory;

        private RectTransform slotGrid;

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
            BuildIfNeeded();
            Refresh();
            Subscribe();
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
            }
        }

        private void Subscribe()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                inventory.Changed += Refresh;
            }
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
            if (slotGrid != null)
            {
                return;
            }

            MMOUiFactory.DestroyChildren(transform);

            RectTransform root = (RectTransform)transform;
            root.sizeDelta = new Vector2(300f, 330f);

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
            slotGrid.offsetMin = new Vector2(16f, 18f);
            slotGrid.offsetMax = new Vector2(-16f, -58f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            MMOUiFactory.DestroyChildren(slotGrid);

            int slotCount = inventory != null ? inventory.SlotCount : 0;
            for (int i = 0; i < slotCount; i++)
            {
                CreateSlot(i, inventory != null ? inventory.GetSlot(i) : null);
            }
        }

        private void CreateSlot(int index, MMOItemStack itemStack)
        {
            bool hasItem = itemStack != null && !itemStack.IsEmpty;
            Color slotColor = hasItem ? GetQualityColor(itemStack.Item.Quality) : new Color(0.045f, 0.04f, 0.036f, 0.94f);
            Image slot = MMOUiFactory.CreateImage($"Inventory Slot {index + 1}", slotGrid, slotColor);
            RectTransform rectTransform = slot.rectTransform;
            int column = index % 4;
            int row = index / 4;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(column * 66f, -row * 66f);
            rectTransform.sizeDelta = new Vector2(58f, 58f);

            Text number = MMOUiFactory.CreateText("Number", rectTransform, 10, FontStyle.Bold, TextAnchor.UpperLeft);
            number.text = (index + 1).ToString();
            number.color = new Color(0.76f, 0.68f, 0.5f, 1f);
            MMOUiFactory.Stretch(number.rectTransform);
            number.rectTransform.offsetMin = new Vector2(4f, 2f);
            number.rectTransform.offsetMax = new Vector2(-4f, -2f);

            if (!hasItem)
            {
                return;
            }

            MMOItemTooltipTrigger tooltipTrigger = slot.gameObject.GetComponent<MMOItemTooltipTrigger>();
            if (tooltipTrigger == null)
            {
                tooltipTrigger = slot.gameObject.AddComponent<MMOItemTooltipTrigger>();
            }

            tooltipTrigger.Configure(itemStack.Item);

            MMOInventoryItemUseTrigger useTrigger = slot.gameObject.GetComponent<MMOInventoryItemUseTrigger>();
            if (useTrigger == null)
            {
                useTrigger = slot.gameObject.AddComponent<MMOInventoryItemUseTrigger>();
            }

            useTrigger.Configure(inventory, index);

            Text itemName = MMOUiFactory.CreateText("Item Name", rectTransform, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            itemName.text = itemStack.Item.DisplayName;
            itemName.color = Color.white;
            itemName.resizeTextForBestFit = true;
            itemName.resizeTextMinSize = 6;
            itemName.resizeTextMaxSize = 9;
            MMOUiFactory.Stretch(itemName.rectTransform);
            itemName.rectTransform.offsetMin = new Vector2(5f, 8f);
            itemName.rectTransform.offsetMax = new Vector2(-5f, -8f);

            if (itemStack.Quantity > 1)
            {
                Text quantity = MMOUiFactory.CreateText("Quantity", rectTransform, 10, FontStyle.Bold, TextAnchor.LowerRight);
                quantity.text = itemStack.Quantity.ToString();
                quantity.color = Color.white;
                MMOUiFactory.Stretch(quantity.rectTransform);
                quantity.rectTransform.offsetMin = new Vector2(4f, 2f);
                quantity.rectTransform.offsetMax = new Vector2(-4f, -2f);
            }
        }

        private static Color GetQualityColor(MMOItemQuality quality)
        {
            return quality switch
            {
                MMOItemQuality.Common => new Color(0.13f, 0.13f, 0.12f, 0.96f),
                MMOItemQuality.Uncommon => new Color(0.05f, 0.18f, 0.07f, 0.96f),
                MMOItemQuality.Rare => new Color(0.05f, 0.12f, 0.24f, 0.96f),
                MMOItemQuality.Epic => new Color(0.18f, 0.07f, 0.22f, 0.96f),
                _ => new Color(0.11f, 0.105f, 0.095f, 0.96f)
            };
        }
    }
}
