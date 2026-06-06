using System.Collections;
using RPGClone.Inventory;
using RPGClone.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOLootWindowPresenter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float autoLootDelaySeconds = 2f;

        private RectTransform root;
        private RectTransform rowsRoot;
        private Text titleText;
        private IMMOLootSource lootSource;
        private Coroutine autoLootRoutine;
        private Canvas canvas;

        public static MMOLootWindowPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            BuildIfNeeded();
            Close();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void Open(MMOLootableCorpse corpse, Vector2 screenPosition)
        {
            Open((IMMOLootSource)corpse, screenPosition);
        }

        public static void Open(IMMOLootSource lootSource, Vector2 screenPosition)
        {
            MMOLootWindowPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOLootWindowPresenter>();
            presenter?.OpenLootSource(lootSource, screenPosition);
        }

        public void OpenCorpse(MMOLootableCorpse newCorpse, Vector2 screenPosition)
        {
            OpenLootSource(newCorpse, screenPosition);
        }

        public void OpenLootSource(IMMOLootSource newLootSource, Vector2 screenPosition)
        {
            if (newLootSource == null || !newLootSource.HasLoot)
            {
                return;
            }

            BuildIfNeeded();
            lootSource = newLootSource;
            gameObject.SetActive(true);
            PositionAt(screenPosition);
            Refresh();

            if (autoLootRoutine != null)
            {
                StopCoroutine(autoLootRoutine);
            }

            autoLootRoutine = StartCoroutine(AutoLootAfterDelay());
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (autoLootRoutine != null)
            {
                StopCoroutine(autoLootRoutine);
                autoLootRoutine = null;
            }

            lootSource = null;
            MMOItemTooltipPresenter.HideItem(null);
            gameObject.SetActive(false);
        }

        private IEnumerator AutoLootAfterDelay()
        {
            yield return new WaitForSeconds(autoLootDelaySeconds);
            LootAll();
        }

        private void LootAll()
        {
            if (lootSource == null)
            {
                Close();
                return;
            }

            MMOInventoryContainer inventory = ResolvePlayerInventory();
            if (inventory != null)
            {
                lootSource.TryLootToInventory(inventory);
            }

            Close();
        }

        private void LootSingle(int index)
        {
            if (lootSource == null)
            {
                Close();
                return;
            }

            MMOInventoryContainer inventory = ResolvePlayerInventory();
            if (inventory == null)
            {
                return;
            }

            lootSource.TryLootStackToInventory(index, inventory);
            if (lootSource.HasLoot)
            {
                Refresh();
            }
            else
            {
                Close();
            }
        }

        private MMOInventoryContainer ResolvePlayerInventory()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<MMOInventoryContainer>() : null;
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(230f, 78f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.02f, 0.016f, 0.012f, 0.98f);

            Outline outline = gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.72f, 0.63f, 0.42f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            titleText = MMOUiFactory.CreateText("Title", transform, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.text = "Loot";
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            titleText.rectTransform.sizeDelta = new Vector2(-20f, 22f);

            rowsRoot = MMOUiFactory.CreateRect("Rows", transform);
            rowsRoot.anchorMin = new Vector2(0f, 1f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.pivot = new Vector2(0f, 1f);
            rowsRoot.anchoredPosition = new Vector2(8f, -34f);
            rowsRoot.sizeDelta = new Vector2(-16f, 36f);
        }

        private void Refresh()
        {
            MMOUiFactory.DestroyChildren(rowsRoot);
            if (lootSource == null)
            {
                return;
            }

            titleText.text = string.IsNullOrWhiteSpace(lootSource.DisplayName) ? "Loot" : lootSource.DisplayName;
            int visibleRows = 0;
            for (int i = 0; i < lootSource.Loot.Count; i++)
            {
                MMOItemStack stack = lootSource.Loot[i];
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                CreateLootRow(i, stack, visibleRows);
                visibleRows++;
            }

            root.sizeDelta = new Vector2(230f, 42f + Mathf.Max(1, visibleRows) * 34f);
        }

        private void CreateLootRow(int lootIndex, MMOItemStack stack, int rowIndex)
        {
            Button row = MMOUiFactory.CreateTextButton($"Loot Row {rowIndex + 1}", rowsRoot, string.Empty, new Vector2(214f, 30f), new Color(0.055f, 0.046f, 0.036f, 0.96f));
            row.onClick.AddListener(() => LootSingle(lootIndex));

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -rowIndex * 34f);

            Text label = MMOUiFactory.CreateText("Item", rowRect, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = stack.Quantity > 1 ? $"{stack.Item.DisplayName} x{stack.Quantity}" : stack.Item.DisplayName;
            label.color = GetQualityColor(stack.Item.Quality);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            MMOItemTooltipTrigger.Bind(row.gameObject, stack.Item);
        }

        private void PositionAt(Vector2 screenPosition)
        {
            canvas ??= GetComponentInParent<Canvas>();
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
            root.anchoredPosition = ClampToCanvas(localPosition, canvasRect);
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Rect rect = canvasRect.rect;
            Vector2 size = root.sizeDelta;
            position.x = Mathf.Clamp(position.x, rect.xMin + 8f, rect.xMax - size.x - 8f);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + 8f, rect.yMax - 8f);
            return position;
        }

        private static Color GetQualityColor(MMOItemQuality quality)
        {
            return quality switch
            {
                MMOItemQuality.Common => Color.white,
                MMOItemQuality.Uncommon => new Color(0.12f, 1f, 0f, 1f),
                MMOItemQuality.Rare => new Color(0f, 0.44f, 0.87f, 1f),
                MMOItemQuality.Epic => new Color(0.64f, 0.21f, 0.93f, 1f),
                _ => new Color(0.62f, 0.62f, 0.62f, 1f)
            };
        }
    }
}
