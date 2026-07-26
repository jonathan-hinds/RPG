using System;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOStandardWindow : MonoBehaviour
    {
        public static readonly Vector2 NativeWindowSize = new(1026f, 1402f);
        public static readonly Vector2 NativeCloseButtonSize = new(100f, 100f);
        public static readonly Vector2 NativeQuestButtonSize = new(332f, 64f);
        public const float DefaultWindowScale = 0.45f;
        public static readonly Vector2 WindowSize = NativeWindowSize * DefaultWindowScale;
        public static readonly Vector2 CloseButtonSize = NativeCloseButtonSize * DefaultWindowScale;
        public static readonly Vector2 QuestButtonSize = NativeQuestButtonSize * DefaultWindowScale;
        public static readonly Vector2 DefaultContentOffsetMin = new Vector2(62f, 126f) * DefaultWindowScale;
        public static readonly Vector2 DefaultContentOffsetMax = new Vector2(-62f, -126f) * DefaultWindowScale;
        public static readonly Vector2 DefaultCloseButtonPosition = new Vector2(-62f, -62f) * DefaultWindowScale;
        public static readonly Vector2 DefaultActionButtonPosition = new Vector2(-54f, -96f) * DefaultWindowScale;
        public static readonly Vector2 PrimaryWindowPosition = new(32f, -128f);
        public static readonly Vector2 SecondaryWindowPosition = new(512f, -128f);
        private const string GenericWindowResourcePath = "RPGClone/UI/Windows/GenericWindow";
        private const string QuestWindowResourcePath = "RPGClone/UI/Windows/QuestWindow";

        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private bool deactivateOnClose = true;

        private Action closeRequested;

        public RectTransform ContentRoot
        {
            get
            {
                ResolveReferences();
                return contentRoot != null ? contentRoot : (RectTransform)transform;
            }
        }

        public Text TitleText
        {
            get
            {
                ResolveReferences();
                return titleText;
            }
        }

        public Button CloseButton
        {
            get
            {
                ResolveReferences();
                return closeButton;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RegisterCloseButton();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestClose);
            }
        }

        public static MMOStandardWindow Ensure(GameObject windowObject, string title, Action onClose)
        {
            MMOStandardWindow window = windowObject.GetComponent<MMOStandardWindow>();
            if (window == null)
            {
                window = windowObject.AddComponent<MMOStandardWindow>();
            }

            window.Initialize(title, onClose);
            return window;
        }

        public void Initialize(string title, Action onClose)
        {
            closeRequested = onClose;
            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = WindowSize;
            }

            ResolveReferences();
            EnsureFallbackVisuals();
            RegisterCloseButton();
            SetTitle(title);
        }

        public void SetTitle(string title)
        {
            ResolveReferences();
            if (titleText != null && !string.IsNullOrWhiteSpace(title))
            {
                titleText.text = title;
            }
        }

        public static void ApplyDefaultPlacement(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            ApplyPlacement(root, PrimaryWindowPosition);
        }

        public static void ApplySecondaryPlacement(RectTransform root)
        {
            ApplyPlacement(root, SecondaryWindowPosition);
        }

        private static void ApplyPlacement(RectTransform root, Vector2 anchoredPosition)
        {
            if (root == null)
            {
                return;
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = WindowSize;
        }

        public static void ApplyDefaultPlacementIfGenerated(RectTransform root)
        {
            if (root == null || HasAuthoredWindowLayout(root.gameObject))
            {
                return;
            }

            ApplyDefaultPlacement(root);
        }

        public static bool HasAuthoredWindowLayout(GameObject windowObject)
        {
            return windowObject != null
                && windowObject.TryGetComponent(out MMOStandardWindow _)
                && windowObject.transform.childCount > 0;
        }

        public static Button CreateQuestActionButton(string objectName, Transform parent, string label)
        {
            (Sprite normal, Sprite highlighted, Sprite pressed) = ResolveQuestButtonSprites();
            return MMOUiFactory.CreateTextButton(
                objectName,
                parent,
                label,
                QuestButtonSize,
                MMONpcWindowFrame.AccentButtonColor,
                normal,
                highlighted,
                pressed);
        }

        public RectTransform FindRect(string objectName)
        {
            ResolveReferences();
            return FindChild<RectTransform>(ContentRoot, objectName);
        }

        public Text FindText(string objectName)
        {
            ResolveReferences();
            return FindChild<Text>(ContentRoot, objectName);
        }

        public Button FindButton(string objectName)
        {
            ResolveReferences();
            return FindChild<Button>(ContentRoot, objectName);
        }

        private void RequestClose()
        {
            if (closeRequested != null)
            {
                closeRequested.Invoke();
                return;
            }

            if (deactivateOnClose)
            {
                gameObject.SetActive(false);
            }
        }

        private void RegisterCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
        }

        private void ResolveReferences()
        {
            backgroundImage ??= GetComponent<Image>();
            contentRoot ??= FindChild<RectTransform>(transform, "Content");
            titleText ??= FindChild<Text>(transform, "Title");
            closeButton ??= FindChild<Button>(transform, "Close Button") ?? FindChild<Button>(transform, "Close");
        }

        private void EnsureFallbackVisuals()
        {
            bool createdBackground = false;
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
                backgroundImage.color = MMONpcWindowFrame.BackgroundColor;
                createdBackground = true;
            }

            Sprite backgroundSprite = ResolveWindowSprite();
            if (backgroundSprite != null && (createdBackground || backgroundImage.sprite == null))
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.type = backgroundSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.color = Color.white;
            }

            if (contentRoot == null)
            {
                contentRoot = MMOUiFactory.CreateRect("Content", transform);
                MMOUiFactory.Stretch(contentRoot);
                contentRoot.offsetMin = DefaultContentOffsetMin;
                contentRoot.offsetMax = DefaultContentOffsetMax;
            }

            if (titleText == null)
            {
                titleText = MMOUiFactory.CreateText("Title", transform, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
                titleText.color = MMONpcWindowFrame.TitleColor;
                titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
                titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
                titleText.rectTransform.pivot = new Vector2(0f, 1f);
                titleText.rectTransform.anchoredPosition = new Vector2(92f, -34f) * DefaultWindowScale;
                titleText.rectTransform.sizeDelta = new Vector2(-220f, 42f) * DefaultWindowScale;
            }

            if (closeButton == null)
            {
                (Sprite normal, Sprite highlighted, Sprite pressed) = ResolveCloseButtonSprites();
                closeButton = MMOUiFactory.CreateTextButton("Close Button", transform, string.Empty, CloseButtonSize, MMONpcWindowFrame.ButtonColor, normal, highlighted, pressed);
                RectTransform closeRect = closeButton.GetComponent<RectTransform>();
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = DefaultCloseButtonPosition;
            }
        }

        private static Sprite ResolveWindowSprite()
        {
            GameObject prefab = Resources.Load<GameObject>(GenericWindowResourcePath);
            return prefab != null ? prefab.GetComponent<Image>()?.sprite : null;
        }

        private static (Sprite normal, Sprite highlighted, Sprite pressed) ResolveCloseButtonSprites()
        {
            return ResolveButtonSprites(GenericWindowResourcePath, "Close Button");
        }

        private static (Sprite normal, Sprite highlighted, Sprite pressed) ResolveQuestButtonSprites()
        {
            return ResolveButtonSprites(QuestWindowResourcePath, "Accept Button");
        }

        private static (Sprite normal, Sprite highlighted, Sprite pressed) ResolveButtonSprites(string resourcePath, string objectName)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return (null, null, null);
            }

            Button button = FindChild<Button>(prefab.transform, objectName);
            Image image = button != null ? button.targetGraphic as Image : null;
            SpriteState spriteState = button != null ? button.spriteState : default;
            return (image != null ? image.sprite : null, spriteState.highlightedSprite, spriteState.pressedSprite);
        }

        private static T FindChild<T>(Transform root, string objectName) where T : Component
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            T[] children = root.GetComponentsInChildren<T>(true);
            foreach (T child in children)
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
