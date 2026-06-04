using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOItemTooltipPresenter : MonoBehaviour
    {
        private const float Padding = 10f;

        [SerializeField] private Vector2 cursorOffset = new(18f, -12f);

        private RectTransform root;
        private Image background;
        private Text nameText;
        private Text typeText;
        private Text descriptionText;
        private Text valueText;
        private Canvas canvas;
        private Vector2 lastScreenPosition;

        public static MMOItemTooltipPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            BuildIfNeeded();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (gameObject.activeSelf)
            {
                FollowCursor();
            }
        }

        public static void ShowItem(MMOItemDefinition item)
        {
            MMOItemTooltipPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOItemTooltipPresenter>();
            presenter?.Show(item);
        }

        public static void ShowItem(MMOItemDefinition item, Vector2 screenPosition)
        {
            MMOItemTooltipPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOItemTooltipPresenter>();
            presenter?.Show(item, screenPosition);
        }

        public static void HideItem(MMOItemDefinition item)
        {
            MMOItemTooltipPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOItemTooltipPresenter>();
            presenter?.Hide();
        }

        public void Show(MMOItemDefinition item)
        {
            Show(item, GetCurrentPointerPosition());
        }

        public void Show(MMOItemDefinition item, Vector2 screenPosition)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            BuildIfNeeded();
            lastScreenPosition = screenPosition;
            gameObject.SetActive(true);
            nameText.text = item.DisplayName;
            nameText.color = GetQualityColor(item.Quality);
            typeText.text = $"{FormatQuality(item.Quality)} {MMOUiFactory.FormatEnumLabel(item.ItemType)}";
            descriptionText.text = item.Description;
            valueText.text = item.VendorValueCopper > 0 ? $"Sell Price: {item.VendorValueCopper} Copper" : string.Empty;
            ResizeToContent();
            SetPosition(screenPosition);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(260f, 124f);

            background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.015f, 0.012f, 0.01f, 0.97f);
            background.raycastTarget = false;

            Outline outline = gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.72f, 0.63f, 0.42f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            nameText = MMOUiFactory.CreateText("Name", transform, 14, FontStyle.Bold, TextAnchor.UpperLeft);
            typeText = MMOUiFactory.CreateText("Type", transform, 11, FontStyle.Normal, TextAnchor.UpperLeft);
            descriptionText = MMOUiFactory.CreateText("Description", transform, 11, FontStyle.Italic, TextAnchor.UpperLeft);
            valueText = MMOUiFactory.CreateText("Value", transform, 11, FontStyle.Normal, TextAnchor.UpperLeft);

            nameText.rectTransform.anchoredPosition = new Vector2(Padding, -Padding);
            typeText.rectTransform.anchoredPosition = new Vector2(Padding, -34f);
            descriptionText.rectTransform.anchoredPosition = new Vector2(Padding, -58f);
            valueText.rectTransform.anchoredPosition = new Vector2(Padding, -98f);

            foreach (Text text in new[] { nameText, typeText, descriptionText, valueText })
            {
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.rectTransform.anchorMin = new Vector2(0f, 1f);
                text.rectTransform.anchorMax = new Vector2(1f, 1f);
                text.rectTransform.pivot = new Vector2(0f, 1f);
                text.rectTransform.sizeDelta = new Vector2(-Padding * 2f, 24f);
            }

            descriptionText.rectTransform.sizeDelta = new Vector2(-Padding * 2f, 42f);
            valueText.color = new Color(0.95f, 0.82f, 0.48f, 1f);
        }

        private void ResizeToContent()
        {
            float descriptionHeight = string.IsNullOrWhiteSpace(descriptionText.text) ? 0f : 42f;
            descriptionText.gameObject.SetActive(descriptionHeight > 0f);
            valueText.rectTransform.anchoredPosition = new Vector2(Padding, descriptionHeight > 0f ? -98f : -62f);
            float height = string.IsNullOrWhiteSpace(valueText.text)
                ? (descriptionHeight > 0f ? 104f : 70f)
                : (descriptionHeight > 0f ? 126f : 92f);
            root.sizeDelta = new Vector2(260f, height);
        }

        private void FollowCursor()
        {
            SetPosition(GetCurrentPointerPosition());
        }

        private void SetPosition(Vector2 screenPosition)
        {
            canvas ??= GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (canvasRect == null)
            {
                return;
            }

            if (screenPosition == Vector2.zero && lastScreenPosition != Vector2.zero)
            {
                screenPosition = lastScreenPosition;
            }

            lastScreenPosition = screenPosition;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPosition);

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = ClampToCanvas(localPosition + cursorOffset, canvasRect);
        }

        private static Vector2 GetCurrentPointerPosition()
        {
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Rect rect = canvasRect.rect;
            Vector2 size = root.sizeDelta;
            position.x = Mathf.Clamp(position.x, rect.xMin + Padding, rect.xMax - size.x - Padding);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + Padding, rect.yMax - Padding);
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

        private static string FormatQuality(MMOItemQuality quality)
        {
            return MMOUiFactory.FormatEnumLabel(quality);
        }
    }
}
